using System;
using System.Collections.Generic;
using System.Reflection;
using BaoX.DurangoOriginal.CombatSystemMod.Geometry;
using Durango.Logic;
using Durango.Logic.Combat;
using Durango.Network;
using Durango.UI;
using Durango.Utils;
using HarmonyLib;
using Messages;
using Shared.Ability;
using Shared.Battle;
using UnityEngine;
using Yaml;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal sealed class OfflineCombatState
    {
        internal Durango.Offline.Player Player;
        internal Durango.Offline.Connection Connection;
        internal Durango.Offline.PlayerContext Context;
        internal bool CombatActive;
        internal string TargetId;
        internal bool ActionsRequested;
        internal uint ActionsReplyOf;
        internal float ActionsRequestedAt;
        internal double ReviveInvulnerableUntil;
        internal double DodgeActiveFrom;
        internal double DodgeActiveUntil;
        internal readonly Dictionary<string, double> Cooldowns =
            new Dictionary<string, double>(StringComparer.Ordinal);
    }

    internal sealed class PendingCombatHit
    {
        internal OfflineCombatState State;
        internal string ActionId;
        internal string PrimaryTargetId;
        internal int HitIndex;
        internal double DueAt;
        internal Vector3 AttackerPosition;
        internal bool HasAttackerPosition;
    }

    internal sealed class PendingAnimalDeath
    {
        internal OfflineCombatState State;
        internal string EntityId;
        internal double DueAt;
    }

    internal static class OfflineCombatRuntime
    {
        private const int BrachioEntityType = 2004;
        private const float BrachioDefenseValue = 0.8f;
        private static readonly Dictionary<Durango.Offline.Player, OfflineCombatState> States =
            new Dictionary<Durango.Offline.Player, OfflineCombatState>();
        private static readonly List<PendingCombatHit> PendingHits = new List<PendingCombatHit>();
        private static readonly List<PendingAnimalDeath> PendingDeaths = new List<PendingAnimalDeath>();
        private static readonly MethodInfo OnContextChangedMethod = typeof(Durango.Offline.Player).GetMethod(
            "OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ShowRewardAlarmMethod = typeof(AlarmGroup).GetMethod(
            "ShowRewardAlarm",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(object) },
            null);
        private static readonly FieldInfo AnimalsField = AccessTools.Field(typeof(AnimalManager), "_animals");
        private static OfflineCombatState _localState;

        internal static void Register(
            Durango.Offline.Player player,
            Durango.Offline.Connection connection,
            Durango.Offline.PlayerContext context)
        {
            OfflineCombatState state = new OfflineCombatState
            {
                Player = player,
                Connection = connection,
                Context = context
            };
            States[player] = state;
            _localState = state;
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Registered local offline combat handlers for " + player.EntityId);
        }

        internal static void Reset()
        {
            PendingHits.Clear();
            PendingDeaths.Clear();
            States.Clear();
            _localState = null;
        }

        internal static void Tick()
        {
            TryReplyActions();
            double now = ServerTime();

            for (int i = PendingHits.Count - 1; i >= 0; i--)
            {
                PendingCombatHit hit = PendingHits[i];
                if (hit.DueAt > now)
                {
                    continue;
                }

                PendingHits.RemoveAt(i);
                try
                {
                    ResolveHit(hit, now);
                }
                catch (Exception exception)
                {
                    OfflineCombatBackendPlugin.Log.LogError(
                        "Combat hit failed action=" + hit.ActionId + ": " + exception);
                }
            }

            for (int i = PendingDeaths.Count - 1; i >= 0; i--)
            {
                PendingAnimalDeath death = PendingDeaths[i];
                if (death.DueAt > now)
                {
                    continue;
                }

                PendingDeaths.RemoveAt(i);
                FinalizeAnimalDeath(death, now);
            }
        }

        internal static void RequestActions(Durango.Offline.Player player, uint replyOf)
        {
            OfflineCombatState state;
            if (!States.TryGetValue(player, out state))
            {
                return;
            }

            state.ActionsRequested = true;
            state.ActionsReplyOf = replyOf;
            state.ActionsRequestedAt = Time.unscaledTime;
        }

        private static void TryReplyActions()
        {
            OfflineCombatState state = _localState;
            if (state == null || !state.ActionsRequested ||
                !GameSystem<CombatSystem>.HasInstance())
            {
                return;
            }

            List<ActionStatus> statuses = new List<ActionStatus>();
            foreach (BattleAction action in GameSystem<CombatSystem>.Instance().GetCurrentBattleActions())
            {
                if (action == null || action.Data == null)
                {
                    continue;
                }

                ActionStatus status = default(ActionStatus);
                status.Id = action.Data.Id;
                status.Stamina = Mathf.RoundToInt(action.Stamina);
                status.Cooltime = action.Cooldown;
                statuses.Add(status);
            }

            if (statuses.Count == 0 && Time.unscaledTime - state.ActionsRequestedAt < 1f)
            {
                return;
            }

            Actions message = default(Actions);
            message.BattleActions = statuses.ToArray();
            state.Player.Send<Actions>(message, state.ActionsReplyOf);
            state.ActionsRequested = false;
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Sent local combat actions: " + statuses.Count);
        }

        internal static void BeginCombat(string targetId)
        {
            OfflineCombatState state = _localState;
            if (state == null || string.IsNullOrEmpty(targetId))
            {
                return;
            }

            AnimalBehavior target = FindAnimal(targetId);
            if (target == null || !target.IsAlive ||
                target.Life == null || target.Life.Get() <= 0f)
            {
                return;
            }

            state.TargetId = targetId;
            if (state.CombatActive)
            {
                return;
            }

            state.CombatActive = true;
            BattleBegun begun = default(BattleBegun);
            begun.EntityId = state.Player.EntityId;
            begun.EnemyId = targetId;
            begun.EventAt = ServerTime();
            begun.StartDamaged = false;
            state.Player.Send<BattleBegun>(begun, 0U);
            OfflineCombatBackendPlugin.Log.LogInfo("Combat begun target=" + targetId);
        }

        internal static void EndCombat(Durango.Offline.Player player, string reason)
        {
            OfflineCombatState state;
            if (!States.TryGetValue(player, out state))
            {
                return;
            }

            if (state.CombatActive)
            {
                BattleEnded ended = default(BattleEnded);
                ended.EntityId = player.EntityId;
                ended.EventAt = ServerTime();
                player.Send<BattleEnded>(ended, 0U);
            }

            state.CombatActive = false;
            state.TargetId = null;
            state.DodgeActiveFrom = 0.0;
            state.DodgeActiveUntil = 0.0;
            PendingHits.RemoveAll(delegate(PendingCombatHit hit) { return hit.State == state; });
            OfflineCombatBackendPlugin.Log.LogInfo("Combat ended reason=" + reason);
        }

        internal static void NotifyAnimalDisengaged(AnimalBehavior animal)
        {
            OfflineCombatState state = _localState;
            if (state == null || animal == null ||
                !string.Equals(state.TargetId, animal.EntityId, StringComparison.Ordinal))
            {
                return;
            }
            EndCombat(state.Player, "AnimalDisengaged");
        }

        internal static bool AddPersistentGauge(
            string gaugeName,
            float amount,
            out string response)
        {
            response = null;
            OfflineCombatState state = _localState;
            if (state == null || PlayerBehavior.LocalPlayer == null)
            {
                response = "Enter an offline world first.";
                return false;
            }

            Gauge current;
            string label;
            if (string.Equals(gaugeName, "life", StringComparison.Ordinal))
            {
                current = PlayerBehavior.LocalPlayer.Life;
                label = "HP";
            }
            else if (string.Equals(gaugeName, "stamina", StringComparison.Ordinal))
            {
                current = PlayerBehavior.LocalPlayer.Stamina;
                label = "SP";
            }
            else
            {
                response = "Unknown gauge: " + gaugeName;
                return false;
            }

            if (current == null)
            {
                response = label + " is not initialized yet.";
                return false;
            }

            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                response = "Amount must be a finite number.";
                return false;
            }

            float next = Mathf.Max(0f, current.Get() + amount);
            Gauge updated = new Gauge(current.Max(), 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = next }
            });

            SurvivalUpdated message = default(SurvivalUpdated);
            message.EntityId = state.Player.EntityId;
            message.Updated = new Dictionary<string, Gauge>();
            message.Updated[gaugeName] = updated;
            message.Removed = new string[0];
            state.Player.Send<SurvivalUpdated>(message, 0U);

            if (string.Equals(gaugeName, "life", StringComparison.Ordinal))
            {
                state.Context.AppearPlayer.Survival.Life = updated;
            }
            else
            {
                if (state.Context.AppearPlayer.Survival.Gauges == null)
                {
                    state.Context.AppearPlayer.Survival.Gauges =
                        new Dictionary<string, Gauge>();
                }
                state.Context.AppearPlayer.Survival.Gauges[gaugeName] = updated;
            }
            state.Context.Save();

            response = label + " " + Mathf.RoundToInt(next) + "/" +
                Mathf.RoundToInt(current.Max()) + " (saved)";
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Persistent " + label + " changed by " + amount +
                " current=" + next + "/" + current.Max());
            return true;
        }

        internal static void ReviveImmediately(
            Durango.Offline.Player player,
            ReviveImmediately request,
            uint replyOf)
        {
            OfflineCombatState state;
            if (!States.TryGetValue(player, out state) || PlayerBehavior.LocalPlayer == null)
            {
                return;
            }

            Gauge current = PlayerBehavior.LocalPlayer.Life;
            float maximum = current == null ? 1f : Mathf.Max(1f, current.Max());
            Gauge life = new Gauge(maximum, 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = maximum }
            });

            state.Context.AppearPlayer.Survival.Life = life;
            if (OnContextChangedMethod != null)
            {
                OnContextChangedMethod.Invoke(state.Player, null);
            }

            SurvivalUpdated survival = default(SurvivalUpdated);
            survival.EntityId = state.Player.EntityId;
            survival.Updated = new Dictionary<string, Gauge>();
            survival.Updated["life"] = life;
            survival.Removed = new string[0];
            state.Player.Send<SurvivalUpdated>(survival, 0U);

            state.Player.Send<Revived>(default(Revived), replyOf);
            EntityRevived revived = default(EntityRevived);
            revived.EntityId = state.Player.EntityId;
            revived.At = ServerTime() + 0.05;
            state.Player.Send<EntityRevived>(revived, 0U);

            state.ReviveInvulnerableUntil = ServerTime() + 3.0;
            EndCombat(state.Player, "InstantRevival");
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Instant revival completed hp=" + maximum +
                " voucher=" + (request.VoucherId ?? "none"));
        }

        internal static void UseAction(Durango.Offline.Player player, UseBattleAction message)
        {
            OfflineCombatState state;
            if (!States.TryGetValue(player, out state) ||
                !GameSystem<CombatSystem>.HasInstance())
            {
                return;
            }

            BattleAction action = GameSystem<CombatSystem>.Instance().GetBattleAction(message.ActionId);
            if (action == null || action.Data == null)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Rejected unknown combat action: " + message.ActionId);
                return;
            }

            double now = ServerTime();
            double availableAt;
            if (state.Cooldowns.TryGetValue(message.ActionId, out availableAt) && now < availableAt)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Rejected action on cooldown: " + message.ActionId);
                return;
            }

            PlayerActionAttackInfo[] hitInfos = action.Data.AttackInfo;
            bool hasDamage = hitInfos != null && hitInfos.Length > 0;
            if (hasDamage)
            {
                if (string.IsNullOrEmpty(message.TargetEntityId))
                {
                    OfflineCombatBackendPlugin.Log.LogWarning(
                        "Rejected combat action without target: " + message.ActionId);
                    return;
                }

                AnimalBehavior target = FindAnimal(message.TargetEntityId);
                if (target == null || !target.IsAlive)
                {
                    OfflineCombatBackendPlugin.Log.LogWarning(
                        "Rejected combat action with invalid animal target: " + message.TargetEntityId);
                    return;
                }
            }

            if (!ConsumeStamina(state, action.Stamina))
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Rejected action without stamina: " + message.ActionId);
                return;
            }

            if (action.Cooldown > 0f)
            {
                state.Cooldowns[message.ActionId] = now + action.Cooldown;
            }

            if (!hasDamage)
            {
                CombatDefenseProfile defense;
                if (CombatActionProfiles.TryGetDefenseProfile(message.ActionId, out defense))
                {
                    double defenseStartAt = message.StartAt > 0.0 ? message.StartAt : now;
                    state.DodgeActiveFrom = defenseStartAt + defense.StandByTime;
                    state.DodgeActiveUntil = state.DodgeActiveFrom + defense.ActiveTime;
                    OfflineCombatBackendPlugin.Log.LogInfo(
                        "Dodge window action=" + message.ActionId +
                        " from=" + state.DodgeActiveFrom.ToString("F3") +
                        " until=" + state.DodgeActiveUntil.ToString("F3") +
                        " force=" + defense.DodgeForce);
                    return;
                }

                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Used non-damaging combat action: " + message.ActionId);
                return;
            }

            BeginCombat(message.TargetEntityId);
            double startAt = message.StartAt > 0.0 ? message.StartAt : now;
            bool hasAttackerPosition = PlayerBehavior.LocalPlayer != null;
            Vector3 attackerPosition = hasAttackerPosition
                ? PlayerBehavior.LocalPlayer.CurrentPosition
                : Vector3.zero;
            for (int i = 0; i < hitInfos.Length; i++)
            {
                float hitDelay = hitInfos[i].AttackTime;
                if (hitDelay <= 0f && AttackGeometry.UsesKylloxProfile(message.ActionId))
                {
                    hitDelay = WeaponSkillTuning.GetPlayerAnimalHitDelay(
                        message.ActionId,
                        i);
                }
                double dueAt = startAt + hitDelay;
                if (dueAt < now + 0.01)
                {
                    dueAt = now + 0.01;
                }

                PendingHits.Add(new PendingCombatHit
                {
                    State = state,
                    ActionId = message.ActionId,
                    PrimaryTargetId = message.TargetEntityId,
                    HitIndex = i,
                    DueAt = dueAt,
                    AttackerPosition = attackerPosition,
                    HasAttackerPosition = hasAttackerPosition
                });
            }

            OfflineCombatBackendPlugin.Log.LogInfo(
                "Scheduled combat action=" + message.ActionId +
                " hits=" + hitInfos.Length +
                " target=" + message.TargetEntityId);
        }

        private static bool ConsumeStamina(OfflineCombatState state, float cost)
        {
            if (cost <= 0f)
            {
                return true;
            }

            Gauge current = PlayerBehavior.LocalPlayer == null ? null : PlayerBehavior.LocalPlayer.Stamina;
            if (current == null && state.Context != null &&
                state.Context.AppearPlayer.Survival.Gauges != null)
            {
                state.Context.AppearPlayer.Survival.Gauges.TryGetValue("stamina", out current);
            }

            if (current == null || current.Get() < cost)
            {
                return false;
            }

            float next = Mathf.Max(0f, current.Get() - cost);
            Gauge updated = new Gauge(current.Max(), 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = next }
            });

            if (state.Context.AppearPlayer.Survival.Gauges == null)
            {
                state.Context.AppearPlayer.Survival.Gauges = new Dictionary<string, Gauge>();
            }
            state.Context.AppearPlayer.Survival.Gauges["stamina"] = updated;
            if (OnContextChangedMethod != null)
            {
                OnContextChangedMethod.Invoke(state.Player, null);
            }

            SurvivalUpdated message = default(SurvivalUpdated);
            message.EntityId = state.Player.EntityId;
            message.Updated = new Dictionary<string, Gauge>();
            message.Updated["stamina"] = updated;
            message.Removed = new string[0];
            state.Player.Send<SurvivalUpdated>(message, 0U);
            return true;
        }

        private static void ResolveHit(PendingCombatHit pending, double now)
        {
            if (pending.State == null || !pending.State.CombatActive ||
                !GameSystem<CombatSystem>.HasInstance())
            {
                return;
            }

            BattleAction action = GameSystem<CombatSystem>.Instance().GetBattleAction(pending.ActionId);
            if (action == null || action.Data == null || action.Data.AttackInfo == null ||
                pending.HitIndex < 0 || pending.HitIndex >= action.Data.AttackInfo.Length)
            {
                return;
            }

            AnimalBehavior primary = FindAnimal(pending.PrimaryTargetId);
            if (primary == null || !primary.IsAlive)
            {
                return;
            }

            PlayerActionAttackInfo hitInfo = action.Data.AttackInfo[pending.HitIndex];
            List<AnimalBehavior> targets = FindTargets(
                action,
                hitInfo,
                primary,
                pending.HitIndex);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Resolved combat hit action=" + action.Data.Id +
                " index=" + pending.HitIndex +
                " type=" + hitInfo.DamageType +
                " targets=" + targets.Count +
                " range=" + CombatActionProfiles.GetMaximumRange(
                    action.Data.Id, action.Data.Meta.UseRange));
            for (int i = 0; i < targets.Count; i++)
            {
                ApplyDamage(pending, action, hitInfo, targets[i], now);
            }
        }

        private static List<AnimalBehavior> FindTargets(
            BattleAction action,
            PlayerActionAttackInfo hitInfo,
            AnimalBehavior primary,
            int hitIndex)
        {
            List<AnimalBehavior> result = new List<AnimalBehavior>();
            Vector3 playerPosition = PlayerBehavior.LocalPlayer.CurrentPosition;
            Vector3 forward = PlayerBehavior.LocalPlayer.transform.forward;
            float maximumRange = CombatActionProfiles.GetMaximumRange(
                action.Data.Id, action.Data.Meta.UseRange);
            if (HorizontalDistance(playerPosition, primary.CurrentPosition) > maximumRange)
            {
                return result;
            }

            PlayerAttackArea kylloxArea;
            bool useKylloxArea = AttackGeometry.TryCreatePlayerArea(
                action.Data.Id,
                hitIndex,
                primary.CurrentPosition,
                out kylloxArea);
            if (useKylloxArea)
            {
                AttackAreaLineRenderer.Show(kylloxArea);
            }

            bool isArea = useKylloxArea ||
                hitInfo.DamageType == DamageType.CircularArea ||
                hitInfo.DamageType == DamageType.RectangularArea;
            if (!isArea)
            {
                result.Add(primary);
                return result;
            }

            Vector3 shapeOrigin = hitInfo.UseTargetOrigin
                ? primary.CurrentPosition
                : playerPosition;
            if (useKylloxArea
                ? AttackGeometry.Contains(kylloxArea, primary.CurrentPosition)
                : IsInsideAttackShape(shapeOrigin, forward, primary, hitInfo))
            {
                result.Add(primary);
            }

            Dictionary<string, AnimalBehavior> animals = GetAnimals();
            if (animals == null)
            {
                return result;
            }

            foreach (AnimalBehavior animal in animals.Values)
            {
                if (animal == null || !animal.IsAlive || animal == primary)
                {
                    continue;
                }

                if (useKylloxArea
                    ? AttackGeometry.Contains(kylloxArea, animal.CurrentPosition)
                    : IsInsideAttackShape(shapeOrigin, forward, animal, hitInfo))
                {
                    result.Add(animal);
                }
            }
            return result;
        }

        private static bool IsInsideAttackShape(
            Vector3 origin,
            Vector3 forward3D,
            AnimalBehavior target,
            PlayerActionAttackInfo hitInfo)
        {
            Vector3 rotatedForward = Quaternion.Euler(0f, hitInfo.DamageAngle, 0f) * forward3D;
            Vector2 forward = new Vector2(rotatedForward.x, rotatedForward.z).normalized;
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector3 delta3D = target.CurrentPosition - origin;
            Vector2 center = forward * hitInfo.Offset.Item2 + right * hitInfo.Offset.Item1;
            Vector2 delta = new Vector2(delta3D.x, delta3D.z) - center;

            if (hitInfo.DamageType == DamageType.CircularArea)
            {
                float radius = Mathf.Max(1f, hitInfo.Radius);
                if (delta.sqrMagnitude > radius * radius)
                {
                    return false;
                }

                if (Mathf.Abs(hitInfo.Angles.Item1) < 0.01f &&
                    Mathf.Abs(hitInfo.Angles.Item2) < 0.01f)
                {
                    return true;
                }

                float angle = Vector2.Angle(forward, delta.normalized);
                float sign = Mathf.Sign(Vector2.Dot(right, delta.normalized));
                float signedAngle = angle * sign;
                return signedAngle >= hitInfo.Angles.Item1 && signedAngle <= hitInfo.Angles.Item2;
            }

            float along = Vector2.Dot(delta, forward);
            float side = Vector2.Dot(delta, right);
            return Mathf.Abs(along) <= Mathf.Max(1f, hitInfo.RectHalfSize.Item1) &&
                Mathf.Abs(side) <= Mathf.Max(1f, hitInfo.RectHalfSize.Item2);
        }

        private static void ApplyDamage(
            PendingCombatHit pending,
            BattleAction action,
            PlayerActionAttackInfo hitInfo,
            AnimalBehavior animal,
            double now)
        {
            CombatHitProfile profile = CombatActionProfiles.GetHitProfile(
                action.Data.Id, pending.HitIndex);
            bool armorPenetrated;
            Damage damage = RollDamage(action, pending.HitIndex, animal, profile, out armorPenetrated);
            damage.Direction = CalculateDamageDirectionFromAttacker(
                animal, pending.AttackerPosition, pending.HasAttackerPosition);
            Damaged message = default(Damaged);
            message.AttackerId = pending.State.Player.EntityId;
            message.VictimId = animal.EntityId;
            message.Damage = damage;
            message.EventAt = now;
            pending.State.Player.Send<Damaged>(message, 0U);

            if (damage.Value <= 0 || animal.Life == null)
            {
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Combat no-damage action=" + action.Data.Id +
                    " target=" + animal.EntityId +
                    " result=" + damage.Result);
                return;
            }

            float nextLife = Mathf.Max(0f, animal.Life.Get() - damage.Value);
            Gauge life = new Gauge(animal.Life.Max(), 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = nextLife }
            });
            animal.SetSurvivalGauge(life, null);

            LocalWildAnimalCombatAI ai = LocalWildAnimalCombatAI.Attach(animal);
            if (ai != null)
            {
                float impactScale = damage.Result == DamageResult.Missed ? 0.2f : 1f;
                ai.NotifyDamaged(
                    damage,
                    (profile.Groggy + Mathf.Max(0f, GetStatisticModifier("groggy_plus"))) *
                        impactScale,
                    (profile.BlowPower + Mathf.Max(0f, GetStatisticModifier("blow_power_plus"))) *
                        impactScale);
            }

            SurvivalUpdated survival = default(SurvivalUpdated);
            survival.EntityId = animal.EntityId;
            survival.Updated = new Dictionary<string, Gauge>();
            survival.Updated["life"] = life;
            survival.Removed = new string[0];
            pending.State.Player.Send<SurvivalUpdated>(survival, 0U);

            OfflineCombatBackendPlugin.Log.LogInfo(
                "Combat hit action=" + action.Data.Id +
                " index=" + pending.HitIndex +
                " target=" + animal.EntityId +
                " damage=" + damage.Value +
                " life=" + nextLife + "/" + animal.Life.Max() +
                " result=" + damage.Result +
                " direction=" + damage.Direction +
                " effect=" + damage.Effects +
                " armorPenetrated=" + armorPenetrated +
                " groggy=" + profile.Groggy +
                " blow=" + profile.BlowPower);

            if (nextLife <= 0f && !HasPendingDeath(animal.EntityId))
            {
                PendingDeaths.Add(new PendingAnimalDeath
                {
                    State = pending.State,
                    EntityId = animal.EntityId,
                    DueAt = now + 0.08
                });
            }
        }

        private static Damage RollDamage(
            BattleAction action,
            int hitIndex,
            AnimalBehavior target,
            CombatHitProfile profile,
            out bool armorPenetrated)
        {
            armorPenetrated = false;
            float attack = 40f;
            float attackRating = 0f;
            float accuracy = 100f;
            float critical = 0f;
            if (GameSystem<StatisticsSystem>.HasInstance())
            {
                StatisticsSystem statistics = GameSystem<StatisticsSystem>.Instance();
                attack = Mathf.Max(1f, statistics.GetDeriveds(Derived.Attack, attack));
                attackRating = Mathf.Max(0f, statistics.GetDeriveds(Derived.AttackRating, 0f));
                accuracy = statistics.GetDeriveds(Derived.Accuracy, accuracy);
                critical = statistics.GetDeriveds(Derived.Critical, critical);
            }

            float damageModifier = GetStatisticModifier("damage_bonus");
            float hitModifier = GetStatisticModifier("hit_rate_plus") +
                GetStatisticModifier("accuracy_plus");
            float criticalRateModifier = GetStatisticModifier("critical_rate_plus");
            float criticalDamageModifier = GetStatisticModifier("critical_damage_bonus");
            float armorPenetrationModifier = GetStatisticModifier("armor_penetration_plus");

            float hitChance = Mathf.Clamp(
                0.8f + accuracy * profile.AccuracyRatio / 1000f + hitModifier, 0.2f, 0.98f);
            Damage damage = default(Damage);
            damage.Result = UnityEngine.Random.value <= hitChance ? DamageResult.Hit : DamageResult.Missed;
            damage.Part = BodyPart.Auto;
            damage.Direction = DamageDirection.Front;
            damage.AttackType = MapAttackType(action.Data.Id);
            damage.Effects = DamageEffects.None;
            damage.Value = 0;

            float criticalChance = Mathf.Clamp(
                0.1f + critical / 1000f + criticalRateModifier, 0.05f, 0.75f);
            bool isMissed = damage.Result == DamageResult.Missed;
            bool isCritical = UnityEngine.Random.value <= criticalChance;
            damage.Effects = isCritical
                ? DamageEffects.Critical
                : DamageEffects.None;

            float blowPower = profile.BlowPower +
                Mathf.Max(0f, GetStatisticModifier("blow_power_plus"));
            LocalWildAnimalCombatAI ai = LocalWildAnimalCombatAI.Attach(target);
            bool incapacitate = false;
            bool blowActive = !isMissed && ai != null &&
                ai.RegisterBlowImpact(blowPower, out incapacitate);
            if (incapacitate)
            {
                damage.Effects |= DamageEffects.Incapacitate;
            }
            else if (blowActive)
            {
                damage.Effects |= DamageEffects.Blow;
            }

            float basePower = Mathf.Max(10f, attack);
            float bonus = profile.DamageBonus;
            float targetReduction = Mathf.Max(0, target.Level - 1) * 0.005f;
            float penetrationChance = Mathf.Clamp01(
                profile.ArmorPenetration + armorPenetrationModifier + attackRating / 1000f);
            armorPenetrated = !isMissed && penetrationChance > 0f &&
                UnityEngine.Random.value <= penetrationChance;
            if (armorPenetrated)
            {
                targetReduction = 0f;
            }
            float defenseScale = Mathf.Clamp(1f - targetReduction, 0.45f, 1f);
            if (target != null && target.EntityTypeId == BrachioEntityType)
            {
                defenseScale *= BrachioDefenseValue;
            }
            float low = isCritical ? 1.4f : 1f;
            float high = isCritical ? 2f * (1f + Mathf.Max(0f, criticalDamageModifier)) : 1.4f;
            float rolled = UnityEngine.Random.Range(basePower * low, basePower * high);
            float resultScale = isMissed ? 0.2f : 1f;
            damage.Value = Mathf.Max(1, Mathf.RoundToInt(
                rolled * bonus * defenseScale *
                (1f + Mathf.Max(-0.9f, damageModifier)) * resultScale));

            float knockBackChance = profile.StrongAttack
                ? Mathf.Clamp((blowPower - 100f) / 1000f, 0f, 0.4f)
                : 0f;
            if (!isMissed && !incapacitate && knockBackChance > 0f &&
                UnityEngine.Random.value <= knockBackChance)
            {
                damage.Effects = DamageEffects.KnockBack |
                    (isCritical ? DamageEffects.Critical : DamageEffects.None);
            }
            return damage;
        }

        private static float GetStatisticModifier(string key)
        {
            if (!GameSystem<StatisticsSystem>.HasInstance() ||
                GameSystem<StatisticsSystem>.Instance().Statistics == null)
            {
                return 0f;
            }

            Statistics statistics = GameSystem<StatisticsSystem>.Instance().Statistics.Value;
            float value;
            return statistics.Modifiers != null && statistics.Modifiers.TryGetValue(key, out value)
                ? value
                : 0f;
        }

        private static AttackType MapAttackType(string actionId)
        {
            if (string.IsNullOrEmpty(actionId) ||
                actionId.IndexOf("barehand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                actionId.IndexOf("tackle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AttackType.BareHands;
            }
            if (actionId.IndexOf("bow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AttackType.Arrow;
            }
            if (actionId.IndexOf("lance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AttackType.Spear;
            }
            if (actionId.IndexOf("axe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AttackType.Axe;
            }
            if (actionId.IndexOf("blunt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AttackType.Blunt;
            }
            return AttackType.Sword;
        }

        private static void FinalizeAnimalDeath(PendingAnimalDeath pending, double now)
        {
            AnimalBehavior animal = FindAnimal(pending.EntityId);
            if (animal == null || animal.Life == null || animal.Life.Get() > 0f)
            {
                return;
            }

            EntityDied died = default(EntityDied);
            died.EntityId = animal.EntityId;
            died.At = now;
            pending.State.Player.Send<EntityDied>(died, 0U);
            animal.SetAsDead();
            LocalWildAnimalCombatAI ai = animal.GetComponent<LocalWildAnimalCombatAI>();
            if (ai != null)
            {
                ai.NotifyDead();
            }
            BrachioLootRuntime.Create(pending.State.Player, animal);
            ShowHuntReward(animal);
            EndCombat(pending.State.Player, "AnimalDead");
            OfflineCombatBackendPlugin.Log.LogInfo("Animal defeated: " + animal.EntityId);
        }

        private static void ShowHuntReward(AnimalBehavior animal)
        {
            if (animal == null || ShowRewardAlarmMethod == null)
            {
                return;
            }

            try
            {
                AlarmGroup alarm = UIManager.FindScript<AlarmGroup>();
                if (alarm == null)
                {
                    return;
                }

                HuntRewardEffect effect = default(HuntRewardEffect);
                effect.TargetAnimal = animal.GetName();
                effect.TargetEntityType = animal.EntityTypeId;
                effect.Type = Shared.System.RewardEffect.Hunted;
                ShowRewardAlarmMethod.Invoke(alarm, new object[] { effect });
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Could not show hunt reward for " + animal.EntityId + ": " + exception.Message);
            }
        }

        internal static void ApplyAnimalAttack(AnimalBehavior animal)
        {
            if (animal == null)
            {
                return;
            }

            AnimalCombatProfile profile = AnimalCombatProfiles.Get(animal.EntityTypeId);
            float attackRange = Mathf.Max(
                300f, Mathf.Max(animal.XRadius, profile.BoundRadius) + 250f);
            ApplyAnimalAttack(
                animal,
                attackRange,
                0f,
                0f,
                1f,
                DamageDirection.Front,
                DamageEffects.None,
                "generic",
                animal.transform.forward);
        }

        internal static void ApplyAnimalAttack(
            AnimalBehavior animal,
            float attackRange,
            float arcStart,
            float arcEnd,
            float damageScale,
            DamageDirection direction,
            DamageEffects forcedEffects,
            string attackId,
            Vector3 attackForward)
        {
            ApplyAnimalAttack(
                animal,
                attackRange,
                arcStart,
                arcEnd,
                damageScale,
                direction,
                forcedEffects,
                attackId,
                attackForward,
                null);
        }

        internal static void ApplyAnimalAttack(
            AnimalBehavior animal,
            float attackRange,
            float arcStart,
            float arcEnd,
            float damageScale,
            DamageDirection direction,
            DamageEffects forcedEffects,
            string attackId,
            Vector3 attackForward,
            AnimalAttackArea? lockedAttackArea)
        {
            OfflineCombatState state = _localState;
            if (state == null || animal == null || !animal.IsAlive ||
                PlayerBehavior.LocalPlayer == null || !PlayerBehavior.LocalPlayer.IsAlive)
            {
                return;
            }

            if (ServerTime() < state.ReviveInvulnerableUntil)
            {
                return;
            }

            BeginCombat(animal.EntityId);
            AnimalCombatProfile profile = AnimalCombatProfiles.Get(animal.EntityTypeId);
            float distance = HorizontalDistance(
                animal.CurrentPosition, PlayerBehavior.LocalPlayer.CurrentPosition);
            attackRange = Mathf.Max(1f, attackRange);
            AnimalAttackArea attackArea = lockedAttackArea.HasValue
                ? lockedAttackArea.Value
                : AnimalAttackGeometry.Create(
                    animal,
                    attackRange,
                    arcStart,
                    arcEnd,
                    attackId,
                    attackForward);
            AnimalAttackAreaLineRenderer.Show(animal, attackArea);

            Damage damage = default(Damage);
            float hitChance = Mathf.Clamp(
                0.65f + profile.AccuracyAt(animal.Level) / 1000f, 0.65f, 0.98f);
            bool inArea = AnimalAttackGeometry.Contains(
                attackArea,
                PlayerBehavior.LocalPlayer.CurrentPosition);
            if (!inArea)
            {
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Animal attack outside area entity=" + animal.EntityId +
                    " attack=" + attackId +
                    " distance=" + distance +
                    " range=" + attackRange +
                    " shape=" + attackArea.Shape);
                return;
            }

            bool hit = UnityEngine.Random.value <= hitChance;
            damage.Result = hit ? DamageResult.Hit : DamageResult.Missed;
            damage.AttackType = animal.XRadius >= 150f ? AttackType.LargeBody : AttackType.SmallBody;
            damage.Direction = direction;
            damage.Part = string.Equals(
                    attackId,
                    BrachioAttackProfiles.TailAttackId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    attackId,
                    BrachioAttackProfiles.WoundedTailAttackId,
                    StringComparison.OrdinalIgnoreCase)
                ? BodyPart.Tail
                : BodyPart.Body;
            damage.Effects = DamageEffects.None;

            if (hit)
            {
                double attackAt = ServerTime();
                if (attackAt >= state.DodgeActiveFrom && attackAt <= state.DodgeActiveUntil)
                {
                    damage.Result = DamageResult.Dodged;
                    hit = false;
                }
                else
                {
                    float dodge = 0f;
                    float evade = 0f;
                    if (GameSystem<StatisticsSystem>.HasInstance())
                    {
                        StatisticsSystem statistics = GameSystem<StatisticsSystem>.Instance();
                        dodge = statistics.GetDeriveds(Derived.Dodge, 0f);
                        evade = statistics.GetDeriveds(Derived.Evade, 0f);
                    }
                    float autoDodgeChance = Mathf.Clamp(
                        Mathf.Max(dodge, evade) / 1000f, 0f, 0.45f);
                    if (autoDodgeChance > 0f && UnityEngine.Random.value <= autoDodgeChance)
                    {
                        damage.Result = DamageResult.AutoDodged;
                        hit = false;
                    }
                }
            }

            if (hit)
            {
                float raw = profile.AttackAt(animal.Level) *
                    Mathf.Max(0.05f, damageScale) *
                    UnityEngine.Random.Range(0.85f, 1.15f);
                float defense = GameSystem<StatisticsSystem>.HasInstance()
                    ? GameSystem<StatisticsSystem>.Instance().GetDeriveds(Derived.Defense, 0f)
                    : 0f;
                float defenseScale = 100f / (100f + Mathf.Max(0f, defense));
                float criticalChance = Mathf.Clamp(
                    profile.CriticalAt(animal.Level) / 1000f, 0f, 0.35f);
                bool critical = criticalChance > 0f &&
                    UnityEngine.Random.value <= criticalChance;
                if (critical)
                {
                    raw *= 2f;
                }

                float autoGuardChance = Mathf.Clamp01(GetStatisticModifier("auto_guard_ratio"));
                if (autoGuardChance > 0f && UnityEngine.Random.value <= autoGuardChance)
                {
                    float guardFlat = Mathf.Max(0f, GetStatisticModifier("guard_protection"));
                    float guardRatio = Mathf.Clamp01(GetStatisticModifier("guard_protection_ratio"));
                    raw = Mathf.Max(0f, raw - guardFlat) * (1f - guardRatio);
                    damage.Result = DamageResult.AutoGuarded;
                }

                damage.Value = Mathf.Max(0, Mathf.RoundToInt(raw * defenseScale));
                if (forcedEffects != DamageEffects.None)
                {
                    damage.Effects = forcedEffects |
                        (critical ? DamageEffects.Critical : DamageEffects.None);
                }
                else
                {
                    damage.Effects = critical ? DamageEffects.Critical : DamageEffects.Blow;
                }

                if (damage.Result == DamageResult.AutoGuarded)
                {
                    damage.Effects = DamageEffects.None;
                }
                else if (forcedEffects == DamageEffects.None)
                {
                    float knockResistance = GameSystem<StatisticsSystem>.HasInstance()
                        ? GameSystem<StatisticsSystem>.Instance().GetDeriveds(
                            Derived.KnockBackResistance, 0f)
                        : 0f;
                float knockChance = Mathf.Clamp(
                    0.04f + profile.SizeLevel * 0.025f - knockResistance / 1000f,
                        0f,
                        0.45f);
                    if (knockChance > 0f && UnityEngine.Random.value <= knockChance)
                    {
                        damage.Effects = DamageEffects.KnockBack |
                            (critical ? DamageEffects.Critical : DamageEffects.None);
                    }
                    else if (!critical && GameSystem<StatisticsSystem>.HasInstance())
                    {
                        float blowResistance = GameSystem<StatisticsSystem>.Instance().GetDeriveds(
                            Derived.BlowResistance, 0f);
                        if (blowResistance > 0f &&
                            UnityEngine.Random.value <= Mathf.Clamp01(blowResistance / 1000f))
                        {
                            damage.Effects = DamageEffects.None;
                        }
                    }
                }
            }

            if (damage.Result == DamageResult.Dodged ||
                damage.Result == DamageResult.AutoDodged)
            {
                hit = false;
                damage.Value = 0;
                damage.Effects = DamageEffects.None;
            }

            double now = ServerTime();
            Damaged damaged = default(Damaged);
            damaged.AttackerId = animal.EntityId;
            damaged.VictimId = state.Player.EntityId;
            damaged.Damage = damage;
            damaged.EventAt = now;
            state.Player.Send<Damaged>(damaged, 0U);

            Gauge currentLife = PlayerBehavior.LocalPlayer.Life;
            if (!hit || damage.Value <= 0 || currentLife == null)
            {
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Animal attack avoided entity=" + animal.EntityId +
                    " attack=" + attackId +
                    " result=" + damage.Result +
                    " damage=" + damage.Value +
                    " distance=" + distance +
                    " range=" + attackRange +
                    " inArea=" + inArea +
                    " shape=" + attackArea.Shape);
                return;
            }

            float nextLife = Mathf.Max(0f, currentLife.Get() - damage.Value);
            Gauge life = new Gauge(currentLife.Max(), 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = nextLife }
            });
            state.Context.AppearPlayer.Survival.Life = life;
            if (OnContextChangedMethod != null)
            {
                OnContextChangedMethod.Invoke(state.Player, null);
            }

            SurvivalUpdated survival = default(SurvivalUpdated);
            survival.EntityId = state.Player.EntityId;
            survival.Updated = new Dictionary<string, Gauge>();
            survival.Updated["life"] = life;
            survival.Removed = new string[0];
            state.Player.Send<SurvivalUpdated>(survival, 0U);

            OfflineCombatBackendPlugin.Log.LogInfo(
                "Animal attack entity=" + animal.EntityId +
                " attack=" + attackId +
                " damage=" + damage.Value +
                " result=" + damage.Result +
                " effect=" + damage.Effects +
                " profile=" + profile.Name +
                " distance=" + distance +
                " range=" + attackRange +
                " playerLife=" + nextLife + "/" + currentLife.Max());

            if (nextLife <= 0f)
            {
                EntityDied died = default(EntityDied);
                died.EntityId = state.Player.EntityId;
                died.At = now + 0.05;
                state.Player.Send<EntityDied>(died, 0U);
                EndCombat(state.Player, "PlayerDead");
            }
        }

        private static bool HasPendingDeath(string entityId)
        {
            for (int i = 0; i < PendingDeaths.Count; i++)
            {
                if (PendingDeaths[i].EntityId == entityId)
                {
                    return true;
                }
            }
            return false;
        }

        private static AnimalBehavior FindAnimal(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) ||
                !Durango.Utils.Singleton<AnimalManager>.HasInstance())
            {
                return null;
            }
            return Durango.Utils.Singleton<AnimalManager>.Instance().GetAnimal(entityId);
        }

        private static Dictionary<string, AnimalBehavior> GetAnimals()
        {
            if (AnimalsField == null || !Durango.Utils.Singleton<AnimalManager>.HasInstance())
            {
                return null;
            }
            return AnimalsField.GetValue(Durango.Utils.Singleton<AnimalManager>.Instance()) as
                Dictionary<string, AnimalBehavior>;
        }

        private static bool IsAnimalAttackArcHit(
            AnimalBehavior animal,
            Vector3 targetPosition,
            float arcStart,
            float arcEnd)
        {
            if (Mathf.Abs(NormalizeAngle(arcStart) - NormalizeAngle(arcEnd)) < 0.01f)
            {
                return true;
            }

            Vector3 delta = targetPosition - animal.CurrentPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f)
            {
                return true;
            }
            delta.Normalize();

            Vector3 forward = animal.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 right = animal.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            float angle = Vector3.Angle(forward, delta);
            if (Vector3.Dot(right, delta) < 0f)
            {
                angle = 360f - angle;
            }
            angle = NormalizeAngle(angle);
            arcStart = NormalizeAngle(arcStart);
            arcEnd = NormalizeAngle(arcEnd);

            if (arcStart <= arcEnd)
            {
                return angle >= arcStart && angle <= arcEnd;
            }
            return angle >= arcStart || angle <= arcEnd;
        }

        private static DamageDirection CalculateDamageDirectionFromAttacker(
            AnimalBehavior animal,
            Vector3 attackerPosition,
            bool hasAttackerPosition)
        {
            if (animal == null)
            {
                return DamageDirection.Front;
            }

            if (!hasAttackerPosition)
            {
                if (PlayerBehavior.LocalPlayer == null)
                {
                    return DamageDirection.Front;
                }
                attackerPosition = PlayerBehavior.LocalPlayer.CurrentPosition;
            }

            Vector3 delta = attackerPosition - animal.CurrentPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f)
            {
                return DamageDirection.Front;
            }
            delta.Normalize();

            Vector3 forward = animal.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 right = animal.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            float angle = Vector3.Angle(forward, delta);
            if (Vector3.Dot(right, delta) < 0f)
            {
                angle = 360f - angle;
            }
            angle = NormalizeAngle(angle);

            if (angle <= 45f || angle >= 315f)
            {
                return DamageDirection.Front;
            }
            if (angle >= 135f && angle <= 225f)
            {
                return DamageDirection.Back;
            }
            if (angle > 45f && angle < 135f)
            {
                return DamageDirection.Right;
            }
            return DamageDirection.Left;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle < 0f)
            {
                angle += 360f;
            }
            while (angle >= 360f)
            {
                angle -= 360f;
            }
            return angle;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static double ServerTime()
        {
            return Connections.Frontend == null
                ? Time.unscaledTime
                : Connections.Frontend.GetPredictedServerTime();
        }
    }
}
