using System;
using System.Collections.Generic;
using System.Reflection;
using Baominix.DurangoOriginal.CombatSystem.Actions;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Baominix.DurangoOriginal.CombatSystem.Damage;
using Baominix.DurangoOriginal.CombatSystem.Geometry;
using Baominix.DurangoOriginal.CombatSystem.Presentation;
using Baominix.DurangoOriginal.CombatSystem.SaurusAI;
using Durango.Logic;
using Durango.Network;
using Durango.Offline;
using Durango.Terrain;
using Durango.Utils;
using Messages;
using Shared.Animal;
using Shared.Battle;
using UnityEngine;
using Yaml;
using Yaml.Util;
using OfflineConnection = Durango.Offline.Connection;

namespace Baominix.DurangoOriginal.CombatSystem.Runtime
{
    internal sealed class OfflineCombatSession : IDisposable
    {
        private const double MaximumFutureStartSeconds = 10.0;
        private const double MaximumPastStartSeconds = 30.0;
        private const int RememberedPacketCount = 128;
        private const double BattleIdleTimeoutSeconds = 8.0;
        private const double ActionRefreshRetrySeconds = 0.25;
        private static readonly MethodInfo PlayerContextChangedMethod =
            typeof(Player).GetMethod(
                "OnContextChanged",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public);

        private readonly Player _player;
        private readonly OfflineConnection _connection;
        private readonly World _world;
        private readonly PlayerContext _context;
        private readonly AnimalCombatTargetRegistry _targets =
            new AnimalCombatTargetRegistry();
        private readonly AnimalInjuryRuntime _injuries =
            new AnimalInjuryRuntime();
        private readonly List<ScheduledPlayerHit> _scheduledHits =
            new List<ScheduledPlayerHit>();
        private readonly List<ScheduledAnimalHit> _scheduledAnimalHits =
            new List<ScheduledAnimalHit>();
        private readonly Dictionary<long, int> _pendingActionHits =
            new Dictionary<long, int>();
        private readonly Dictionary<string, double> _cooldownUntil =
            new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<uint> _processedPackets =
            new HashSet<uint>();
        private readonly Queue<uint> _processedPacketOrder =
            new Queue<uint>();
        private readonly SaurusAiSession _saurusAi;

        private EquipSystem _equipSystem;
        private SkillSystem _skillSystem;
        private CombatActionSnapshot _actions;
        private long _nextActionInstanceId;
        private long _nextAnimalAttackInstanceId;
        private double _actionLockUntil;
        private string _battleEnemyId;
        private double _battleLastActivityAt;
        private bool _actionRefreshPending;
        private bool _pendingActionsReply;
        private uint _pendingActionsReplyOf;
        private double _nextActionRefreshAt;

        internal readonly int Generation;
        internal bool IsDisposed { get; private set; }
        internal AcceptedPlayerAction LastAcceptedAction { get; private set; }

        internal OfflineCombatSession(
            Player player,
            OfflineConnection connection,
            World world,
            PlayerContext context,
            int generation)
        {
            _player = player;
            _connection = connection;
            _world = world;
            _context = context;
            Generation = generation;
            _player.Closed += OnPlayerClosed;
            TrySubscribeClientState();
            if (DurangoCombatSystemPlugin.SaurusAiEnabled != null &&
                DurangoCombatSystemPlugin.SaurusAiEnabled.Value)
            {
                _saurusAi = new SaurusAiSession(generation);
                _saurusAi.AttackCommitted += OnAnimalAttackCommitted;
                _saurusAi.GroggyGaugeChanged += OnAnimalGroggyGaugeChanged;
                _saurusAi.StatusChanged += OnAnimalStatusChanged;
            }
        }

        internal void HandleGetActions(GetActions message, PacketHeader header)
        {
            if (!CanProcess())
            {
                return;
            }

            try
            {
                RefreshActions();
                if (IsActionDataReady(_actions))
                {
                    SendActions(header.Seq);
                }
                else
                {
                    QueueActionRefresh(header.Seq, true);
                }
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogError(
                    "GetActions failed without closing the offline connection: " +
                    exception);
                if (_actions == null)
                {
                    _actions = new CombatActionSnapshot(
                        new ActionStatus[0],
                        new HashSet<string>(StringComparer.Ordinal),
                        "error",
                        true,
                        false);
                }
                SendActions(header.Seq);
            }
        }

        internal bool TryGetSaurusContextReport(
            string selector,
            out string[] lines)
        {
            if (IsDisposed || _saurusAi == null)
            {
                lines = new string[]
                {
                    "Saurus AI is not active in the current offline session."
                };
                return false;
            }
            return _saurusAi.TryGetContextReport(selector, out lines);
        }

        internal bool TryGetSaurusIntentReport(
            string selector,
            out string[] lines)
        {
            if (IsDisposed || _saurusAi == null)
            {
                lines = new string[]
                {
                    "Saurus AI is not active in the current offline session."
                };
                return false;
            }
            return _saurusAi.TryGetIntentReport(selector, out lines);
        }

        internal void HandleUseBattleAction(
            UseBattleAction message,
            PacketHeader header)
        {
            try
            {
                HandleUseBattleActionCore(message, header);
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogError(
                    "UseBattleAction failed without closing the offline connection: " +
                    exception);
            }
        }

        internal void Process(double now)
        {
            if (!CanProcess())
            {
                return;
            }

            try
            {
                ProcessPendingActionRefresh(now);
                RefreshPendingAttackGeometry(now);
                if (_saurusAi != null)
                {
                    _saurusAi.Process(now);
                }
                RefreshPendingAnimalAttackGeometry(now);

                while (_scheduledHits.Count > 0 &&
                    _scheduledHits[0].Attack.HitAt <= now)
                {
                    ScheduledPlayerHit scheduled = _scheduledHits[0];
                    _scheduledHits.RemoveAt(0);
                    ResolveScheduledHit(scheduled, now);
                }

                while (_scheduledAnimalHits.Count > 0 &&
                    _scheduledAnimalHits[0].Attack.HitAt <= now)
                {
                    ScheduledAnimalHit scheduled =
                        _scheduledAnimalHits[0];
                    _scheduledAnimalHits.RemoveAt(0);
                    ResolveScheduledAnimalHit(scheduled, now);
                }

                ProcessAnimalInjuryDegeneration(now);

                if (!string.IsNullOrEmpty(_battleEnemyId) &&
                    _scheduledHits.Count == 0 &&
                    _scheduledAnimalHits.Count == 0 &&
                    now >= _battleLastActivityAt +
                        BattleIdleTimeoutSeconds)
                {
                    EndBattle(now);
                }
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogError(
                    "Combat scheduler failed without closing the offline connection: " +
                    exception);
            }
        }

        private void HandleUseBattleActionCore(
            UseBattleAction message,
            PacketHeader header)
        {
            if (!CanProcess() || !RememberPacket(header.Seq))
            {
                return;
            }

            if (_actions == null)
            {
                RefreshActions();
            }

            string actionId = message.ActionId;
            if (string.IsNullOrEmpty(actionId) ||
                _actions == null ||
                !_actions.ActionIds.Contains(actionId))
            {
                WarnRejected(actionId, "action is not available");
                return;
            }

            PlayerAction action =
                SingletonDict<string, PlayerAction>.Get(actionId, null);
            if (action == null || action.Meta == null)
            {
                WarnRejected(actionId, "action data is missing");
                return;
            }

            double now = Times.UnixTimeNow();
            if (message.StartAt <= 0.0 ||
                message.StartAt > now + MaximumFutureStartSeconds ||
                message.StartAt < now - MaximumPastStartSeconds)
            {
                WarnRejected(actionId, "client start time is outside tolerance");
                return;
            }

            double actionAllowedAt =
                GetActionAllowedAt(action);
            if (now < actionAllowedAt)
            {
                WarnRejected(
                    actionId,
                    "action is still prohibited by the active action");
                return;
            }
            bool interruptsActiveAction = now < _actionLockUntil;

            double cooldown;
            if (_cooldownUntil.TryGetValue(actionId, out cooldown) &&
                now < cooldown)
            {
                WarnRejected(actionId, "action cooldown has not finished");
                return;
            }


            Vector2 actorOrigin;
            float actorYaw;
            if (!TryGetPlayerPose(out actorOrigin, out actorYaw))
            {
                WarnRejected(actionId, "player pose is unavailable");
                return;
            }

            AnimalCombatTarget selectedTarget = null;
            if (!string.IsNullOrEmpty(message.TargetEntityId))
            {
                if (!_targets.TryGet(
                    message.TargetEntityId,
                    now,
                    out selectedTarget) ||
                    !selectedTarget.IsAlive)
                {
                    WarnRejected(
                        actionId,
                        "selected target is missing, dead, or outside the supported animal set");
                    return;
                }

                if (action.Meta.UseRange > 0f &&
                    Vector2.Distance(actorOrigin, selectedTarget.Position) >
                        action.Meta.UseRange + selectedTarget.Radius)
                {
                    WarnRejected(actionId, "selected target is outside use_range");
                    return;
                }
            }

            if (RequiresSelectedTarget(action) && selectedTarget == null)
            {
                WarnRejected(
                    actionId,
                    "Melee/Ranged action requires a selected target");
                return;
            }

            float staminaCost = Mathf.Max(0f, action.Meta.Stamina);
            if (!HasStamina(staminaCost, now))
            {
                WarnRejected(actionId, "not enough stamina");
                return;
            }

            if (interruptsActiveAction && LastAcceptedAction != null)
            {
                CancelPendingAction(LastAcceptedAction.InstanceId);
            }

            _nextActionInstanceId++;
            LastAcceptedAction = new AcceptedPlayerAction(
                _nextActionInstanceId,
                Generation,
                header.Seq,
                _player.EntityId,
                actionId,
                now,
                message.StartAt,
                message.TargetEntityId);

            _cooldownUntil[actionId] =
                now + Math.Max(0.0, action.Meta.Cooldown);
            double actionStartedAt =
                LastAcceptedAction.ClientStartAt > 0.0
                    ? LastAcceptedAction.ClientStartAt
                    : LastAcceptedAction.AcceptedAt;
            _actionLockUntil = actionStartedAt +
                Math.Max(0.0, action.Meta.ActionLength);

            ConsumeStamina(staminaCost, now);
            // Starting an action changes several client combat/UI states.  Send
            // both authoritative gauges together so a stale client-side gauge
            // cannot replace values set by DeveloperModePlugin (/hp and /sp).
            // This is a one-shot resync, not a locked/infinite-gauge mode.
            SendPlayerSurvivalSnapshot();

            List<AttackSnapshot> attacks = BuildAttackSnapshots(
                LastAcceptedAction,
                action,
                actorOrigin,
                actorYaw,
                selectedTarget);
            if (attacks.Count > 0)
            {
                if (selectedTarget != null)
                {
                    BeginBattle(selectedTarget.EntityId, now);
                }
                ScheduleAttacks(attacks);
            }

            DurangoCombatSystemPlugin.Log.LogInfo(
                "Accepted player action instance=" +
                LastAcceptedAction.InstanceId +
                " generation=" + Generation +
                " action=" + actionId +
                " target=" +
                (message.TargetEntityId ?? "none") +
                " hits=" + attacks.Count + ".");
        }

        private List<AttackSnapshot> BuildAttackSnapshots(
            AcceptedPlayerAction accepted,
            PlayerAction action,
            Vector2 actorOrigin,
            float actorYaw,
            AnimalCombatTarget selectedTarget)
        {
            List<AttackSnapshot> result = new List<AttackSnapshot>();
            if (action.AttackInfo == null)
            {
                return result;
            }

            Vector2? targetOrigin = selectedTarget == null
                ? (Vector2?)null
                : selectedTarget.Position;
            bool isMale = _context.AppearPlayer.IsMale();
            PlayerRootMotionPrediction.Sample rootMotionSample =
                PlayerRootMotionPrediction.CreateSample(
                    action,
                    isMale,
                    actorYaw,
                    accepted.ClientStartAt,
                    accepted.AcceptedAt);
            int i;
            for (i = 0; i < action.AttackInfo.Length; i++)
            {
                PlayerActionAttackInfo info = action.AttackInfo[i];
                Vector2 rootMotionDelta = rootMotionSample == null
                    ? Vector2.zero
                    : rootMotionSample.GetRemainingWorldDelta(
                        info.AttackTime);
                if (selectedTarget != null)
                {
                    rootMotionDelta =
                        PlayerRootMotionPrediction.ClampAgainstTarget(
                            actorOrigin,
                            rootMotionDelta,
                            selectedTarget.Position,
                            selectedTarget.Radius);
                }
                Vector2 predictedActorOrigin =
                    actorOrigin + rootMotionDelta;
                AttackSnapshot snapshot = AttackSnapshot.Create(
                    accepted,
                    i,
                    info,
                    predictedActorOrigin,
                    actorYaw,
                    targetOrigin);
                if (snapshot == null)
                {
                    DurangoCombatSystemPlugin.Log.LogWarning(
                        "Skipped unsupported attack geometry: action=" +
                        accepted.ActionId + " hit=" + i + ".");
                    continue;
                }
                result.Add(snapshot);
                if (rootMotionDelta.sqrMagnitude > 0.001f)
                {
                    DurangoCombatSystemPlugin.Log.LogInfo(
                        "Committed player root motion: action=" +
                        accepted.ActionId + " hit=" + i + " delta=" +
                        rootMotionDelta + " center=" + snapshot.Center + ".");
                }
            }
            return result;
        }

        private static bool RequiresSelectedTarget(PlayerAction action)
        {
            if (action == null || action.AttackInfo == null)
            {
                return false;
            }

            int i;
            for (i = 0; i < action.AttackInfo.Length; i++)
            {
                PlayerActionAttackInfo info = action.AttackInfo[i];
                if (info != null &&
                    IsSelectedTargetDamageType(info.DamageType))
                {
                    return true;
                }
            }
            return false;
        }

        private void ScheduleAttacks(List<AttackSnapshot> attacks)
        {
            _pendingActionHits[attacks[0].ActionInstanceId] = attacks.Count;
            int i;
            for (i = 0; i < attacks.Count; i++)
            {
                AttackSnapshot attack = attacks[i];
                _scheduledHits.Add(new ScheduledPlayerHit(attack));
                SendAttackAlert(attack);
            }
            _scheduledHits.Sort(delegate(
                ScheduledPlayerHit left,
                ScheduledPlayerHit right)
            {
                int time = left.Attack.HitAt.CompareTo(right.Attack.HitAt);
                if (time != 0) return time;
                return left.Attack.HitIndex.CompareTo(
                    right.Attack.HitIndex);
            });
        }

        private void RefreshPendingAttackGeometry(double now)
        {
            if (_scheduledHits.Count == 0)
            {
                return;
            }

            Vector2 actorOrigin;
            float currentYaw;
            if (!TryGetPlayerPose(out actorOrigin, out currentYaw))
            {
                return;
            }

            bool isMale = _context.AppearPlayer.IsMale();
            long cachedActionInstanceId = long.MinValue;
            bool cachedActionValid = false;
            AnimalCombatTarget selectedTarget = null;
            PlayerRootMotionPrediction.Sample rootMotionSample = null;
            int i;
            for (i = 0; i < _scheduledHits.Count; i++)
            {
                AttackSnapshot attack = _scheduledHits[i].Attack;
                if (attack == null || attack.Generation != Generation)
                {
                    continue;
                }

                if (cachedActionInstanceId !=
                    attack.ActionInstanceId)
                {
                    cachedActionInstanceId = attack.ActionInstanceId;
                    cachedActionValid = false;
                    selectedTarget = null;
                    rootMotionSample = null;

                    PlayerAction action =
                        SingletonDict<string, PlayerAction>.Get(
                            attack.ActionId,
                            null);
                    if (action != null && action.Meta != null)
                    {
                        if (!string.IsNullOrEmpty(
                                attack.SelectedTargetEntityId))
                        {
                            _targets.TryGet(
                                attack.SelectedTargetEntityId,
                                now,
                                out selectedTarget);
                        }

                        rootMotionSample =
                            PlayerRootMotionPrediction.CreateSample(
                                action,
                                isMale,
                                attack.ActorYawAtCommit,
                                attack.EventAt,
                                now);
                        cachedActionValid = true;
                    }
                }

                if (!cachedActionValid)
                {
                    continue;
                }

                Vector2 rootMotionDelta = rootMotionSample == null
                    ? Vector2.zero
                    : rootMotionSample.GetRemainingWorldDelta(
                        attack.Info.AttackTime);
                if (selectedTarget != null && selectedTarget.IsAlive)
                {
                    rootMotionDelta =
                        PlayerRootMotionPrediction.ClampAgainstTarget(
                            actorOrigin,
                            rootMotionDelta,
                            selectedTarget.Position,
                            selectedTarget.Radius);
                }

                Vector2? targetOrigin = selectedTarget == null
                    ? attack.TargetOriginAtCommit
                    : (Vector2?)selectedTarget.Position;
                if (attack.RefreshGeometry(
                    actorOrigin + rootMotionDelta,
                    targetOrigin))
                {
                    PlayerAttackTelegraph.Move(attack);
                }
            }
        }

        private double GetActionAllowedAt(PlayerAction nextAction)
        {
            if (LastAcceptedAction == null)
            {
                return double.MinValue;
            }

            PlayerAction activeAction =
                SingletonDict<string, PlayerAction>.Get(
                    LastAcceptedAction.ActionId,
                    null);
            if (activeAction == null || activeAction.Meta == null ||
                nextAction == null || nextAction.Meta == null)
            {
                // An accepted action should always remain in the same source
                // dictionary.  If data disappears during a world transition,
                // keep the conservative legacy lock instead of overlapping
                // actions with unknown rules.
                return _actionLockUntil;
            }

            float prohibitedSeconds = 0f;
            if (activeAction.Meta.ProhibitedTime != null)
            {
                activeAction.Meta.ProhibitedTime.TryGetValue(
                    nextAction.Meta.ProhibitType,
                    out prohibitedSeconds);
            }

            double activeStartedAt =
                LastAcceptedAction.ClientStartAt > 0.0
                    ? LastAcceptedAction.ClientStartAt
                    : LastAcceptedAction.AcceptedAt;
            return activeStartedAt +
                Math.Max(0.0, prohibitedSeconds);
        }

        private void CancelPendingAction(long actionInstanceId)
        {
            int i;
            for (i = _scheduledHits.Count - 1; i >= 0; i--)
            {
                AttackSnapshot attack = _scheduledHits[i].Attack;
                if (attack == null ||
                    attack.ActionInstanceId != actionInstanceId)
                {
                    continue;
                }

                PlayerAttackTelegraph.Cancel(attack);
                _scheduledHits.RemoveAt(i);
            }
            _pendingActionHits.Remove(actionInstanceId);
        }

        private void ResolveScheduledHit(
            ScheduledPlayerHit scheduled,
            double now)
        {
            AttackSnapshot attack = scheduled.Attack;
            int remaining;
            if (attack.Generation != Generation ||
                !_pendingActionHits.TryGetValue(
                    attack.ActionInstanceId,
                    out remaining))
            {
                return;
            }

            // The last scheduler refresh committed the collision-constrained
            // player base used by both the visible warning and damage query.
            PlayerAttackTelegraph.Release(attack);

            int matchedTargets = 0;
            if (IsSelectedTargetDamageType(attack.DamageType))
            {
                AnimalCombatTarget selectedTarget;
                if (!string.IsNullOrEmpty(
                        attack.SelectedTargetEntityId) &&
                    _targets.TryGet(
                        attack.SelectedTargetEntityId,
                        now,
                        out selectedTarget) &&
                    selectedTarget.IsAlive)
                {
                    matchedTargets = 1;
                    ResolveHitAgainstTarget(
                        attack,
                        selectedTarget,
                        now);
                }
            }
            else
            {
                List<AnimalCombatTarget> candidates =
                    _targets.GetEnemyCandidates(now);
                int i;
                for (i = 0; i < candidates.Count; i++)
                {
                    AnimalCombatTarget target = candidates[i];
                    if (!AttackGeometry.Contains(
                        attack,
                        target.Position,
                        target.Radius))
                    {
                        continue;
                    }

                    matchedTargets++;
                    ResolveHitAgainstTarget(attack, target, now);
                }
            }

            if (matchedTargets == 0)
            {
                DurangoCombatSystemPlugin.Log.LogInfo(
                    (IsSelectedTargetDamageType(attack.DamageType)
                        ? "Player selected-target hit lost its target: actionInstance="
                        : "Player hit resolved out-of-range: actionInstance=") +
                    attack.ActionInstanceId + " hit=" +
                    attack.HitIndex + ".");
            }

            remaining--;
            if (remaining <= 0)
            {
                _pendingActionHits.Remove(attack.ActionInstanceId);
            }
            else
            {
                _pendingActionHits[attack.ActionInstanceId] = remaining;
            }
        }

        private void ResolveHitAgainstTarget(
            AttackSnapshot attack,
            AnimalCombatTarget target,
            double now)
        {
            if (_saurusAi != null)
            {
                _saurusAi.NotifyPlayerAttack(
                    target,
                    attack.ActionInstanceId,
                    attack.ActionId,
                    now);
            }
            if (string.IsNullOrEmpty(_battleEnemyId))
            {
                BeginBattle(target.EntityId, now);
            }

            AnimalInjuryModifiers injuryModifiers =
                _injuries.GetModifiers(target);
            ResolvedPlayerHit resolved = PlayerHitResolver.Resolve(
                attack,
                target,
                attack.ActionId,
                injuryModifiers);
            if (!resolved.UsedExactDefenseFormula ||
                !resolved.UsedExactDodgeFormula ||
                !resolved.UsedExactEvadeFormula ||
                (resolved.Result == DamageResult.Hit &&
                 (!resolved.UsedExactPartProbability ||
                  !resolved.UsedExactImpactData ||
                  !resolved.UsedExactBlowResistance ||
                  !resolved.UsedExactKnockBackResistance)))
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Player damage used a fallback animal formula: entity=" +
                    target.EntityId + " type=" + target.EntityTypeId +
                    " action=" + attack.ActionId +
                    " defense=" + resolved.UsedExactDefenseFormula +
                    " dodge=" + resolved.UsedExactDodgeFormula +
                    " evade=" + resolved.UsedExactEvadeFormula +
                    " part=" + resolved.UsedExactPartProbability +
                    " impact=" + resolved.UsedExactImpactData +
                    " blowResistance=" +
                    resolved.UsedExactBlowResistance +
                    " knockBackResistance=" +
                    resolved.UsedExactKnockBackResistance + ".");
            }
            float remainingLife = resolved.Result == DamageResult.Hit &&
                resolved.Value > 0
                ? Mathf.Max(0f, target.CurrentLife - resolved.Value)
                : target.CurrentLife;
            SendDamage(attack, target, resolved, now);
            if (_saurusAi != null)
            {
                _saurusAi.NotifyPlayerHit(
                    target,
                    resolved,
                    remainingLife,
                    attack.ActionInstanceId,
                    attack.ActionId,
                    now);
            }
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Player hit resolved: action=" + attack.ActionId +
                " instance=" + attack.ActionInstanceId +
                " hit=" + attack.HitIndex +
                " target=" + target.EntityId +
                " result=" + resolved.Result +
                " value=" + resolved.Value +
                " part=" + resolved.Part +
                " direction=" + resolved.Direction +
                " groggy=" + resolved.GroggyDamage +
                " blowPower=" + resolved.BlowPower +
                " knockBackForce=" + resolved.KnockBackForce +
                " effects=" + resolved.Effects +
                " mode=" +
                (IsSelectedTargetDamageType(attack.DamageType)
                    ? "selected-target"
                    : "area") +
                " center=" + attack.Center +
                " yaw=" + attack.Yaw +
                " targetPos=" + target.Position + ".");
            _battleLastActivityAt = now;
        }

        private void SendAttackAlert(AttackSnapshot attack)
        {
            if (IsSelectedTargetDamageType(attack.DamageType))
            {
                return;
            }

            AttackAlerted alert = attack.ToMessage();
            PlayerAttackTelegraph.Show(attack);
            _player.Send(alert, 0U);
        }

        private static bool IsSelectedTargetDamageType(
            DamageType damageType)
        {
            return damageType == DamageType.Melee ||
                damageType == DamageType.Ranged;
        }

        private void SendDamage(
            AttackSnapshot attack,
            AnimalCombatTarget target,
            ResolvedPlayerHit resolved,
            double now)
        {
            Messages.Damage damage = default(Messages.Damage);
            damage.Result = resolved.Result;
            damage.Value = resolved.Value;
            damage.Part = resolved.Part;
            damage.Direction = resolved.Direction;
            damage.AttackType = resolved.AttackType;
            damage.Effects = resolved.Effects;

            Damaged damaged = default(Damaged);
            damaged.VictimId = target.EntityId;
            damaged.AttackerId = _player.EntityId;
            damaged.Damage = damage;
            damaged.EventAt = attack.HitAt;
            _player.Send(damaged, 0U);

            if (resolved.Result != DamageResult.Hit || resolved.Value <= 0)
            {
                return;
            }

            float remainingLife;
            Gauge life = _targets.ApplyDamage(
                target,
                resolved.Value,
                now,
                out remainingLife);
            SurvivalUpdated update = default(SurvivalUpdated);
            update.EntityId = target.EntityId;
            update.Updated = new Dictionary<string, Gauge>();
            update.Updated["life"] = life;
            update.Removed = new string[0];
            _player.Send(update, 0U);

            if (remainingLife > 0f)
            {
                AnimalPartDamageResult injury = _injuries.ApplyDamage(
                    target,
                    resolved.Part,
                    resolved.Value,
                    now);
                if (injury.IsTracked)
                {
                    DurangoCombatSystemPlugin.Log.LogInfo(
                        "Animal part damage: entity=" + target.EntityId +
                        " type=" + target.EntityTypeId +
                        " part=" + injury.Part +
                        " remaining=" + injury.Remaining + "/" +
                        injury.Maximum +
                        " broke=" + injury.Broke + ".");
                }
                if (injury.Broke)
                {
                    SendAnimalStatusEffects(
                        target.EntityId,
                        injury.ActiveStatusEffects,
                        injury.ManagedStatusEffectIds);
                    DurangoCombatSystemPlugin.Log.LogInfo(
                        "Animal body part broke: entity=" +
                        target.EntityId + " type=" +
                        target.EntityTypeId + " part=" + injury.Part +
                        " activeStatuses=" +
                        injury.ActiveStatusEffects.Length + ".");
                    ApplyAnimalInjuryModifiers(target, now);
                }
            }

            if (remainingLife <= 0f)
            {
                HandleAnimalDeath(target, now);
            }
        }

        private void ApplyAnimalInjuryModifiers(
            AnimalCombatTarget target,
            double now)
        {
            AnimalInjuryModifiers modifiers =
                _injuries.GetModifiers(target);
            float remainingLife;
            Gauge life = _targets.SetLifeVelocity(
                target,
                modifiers.LifePerSecond,
                now,
                out remainingLife);
            SendAnimalLifeUpdate(target.EntityId, life);
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Animal injury modifiers updated: entity=" +
                target.EntityId + " damageBonus=" +
                modifiers.DamageBonus + " dodgePlus=" +
                modifiers.DodgePlus + " hitRatePlus=" +
                modifiers.HitRatePlus + " lifePerSecond=" +
                modifiers.LifePerSecond + ".");
        }

        private void ProcessAnimalInjuryDegeneration(double now)
        {
            List<string> entityIds =
                _injuries.GetDegeneratingEntityIds();
            int i;
            for (i = 0; i < entityIds.Count; i++)
            {
                AnimalCombatTarget target;
                if (!_targets.TryGet(entityIds[i], now, out target))
                {
                    _injuries.Remove(entityIds[i]);
                    continue;
                }
                if (target.CurrentLife <= 0f)
                {
                    DurangoCombatSystemPlugin.Log.LogInfo(
                        "Animal died from injury degeneration: entity=" +
                        target.EntityId + ".");
                    HandleAnimalDeath(target, now);
                }
            }
        }

        private void HandleAnimalDeath(
            AnimalCombatTarget target,
            double now)
        {
            if (target == null)
            {
                return;
            }
            Messages.StatusEffect[] active;
            HashSet<string> managed;
            if (_injuries.TryGetStatusSnapshot(
                target.EntityId,
                out active,
                out managed))
            {
                // Injury templates are clear_on_death in the original data.
                SendAnimalStatusEffects(
                    target.EntityId,
                    new Messages.StatusEffect[0],
                    managed);
            }
            _injuries.Remove(target.EntityId);
            if (_saurusAi != null)
            {
                _saurusAi.NotifyAnimalDied(target.EntityId, now);
            }
            CancelScheduledAnimalAttacks(target.EntityId);
            EntityDied died = default(EntityDied);
            died.EntityId = target.EntityId;
            died.At = now;
            _player.Send(died, 0U);
            EndBattle(now);
        }

        private void SendAnimalLifeUpdate(string entityId, Gauge life)
        {
            SurvivalUpdated update = default(SurvivalUpdated);
            update.EntityId = entityId;
            update.Updated = new Dictionary<string, Gauge>();
            update.Updated["life"] = life;
            update.Removed = new string[0];
            _player.Send(update, 0U);
        }

        private void OnAnimalGroggyGaugeChanged(
            SaurusAnimalController controller,
            Gauge gauge,
            string section)
        {
            if (!CanProcess() || controller == null || gauge == null ||
                string.IsNullOrEmpty(controller.EntityId))
            {
                return;
            }
            SurvivalUpdated update = default(SurvivalUpdated);
            update.EntityId = controller.EntityId;
            update.Updated = new Dictionary<string, Gauge>();
            update.Updated["groggy"] = gauge;
            update.Removed = new string[0];
            _player.Send(update, 0U);
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Animal groggy gauge updated: entity=" +
                controller.EntityId + " section=" + section +
                " value=" + gauge.Get() + "/" + gauge.Max() + ".");
        }

        private void OnAnimalStatusChanged(
            SaurusAnimalController controller,
            AnimalStatus status)
        {
            if (!CanProcess() || controller == null ||
                string.IsNullOrEmpty(controller.EntityId))
            {
                return;
            }
            CombatInteraction interaction = default(CombatInteraction);
            interaction.EntityId = controller.EntityId;
            interaction.TargetId = _player.EntityId;
            interaction.Details = new Dictionary<string, long>();
            interaction.Details["status"] = (long)status;
            _player.Send(interaction, 0U);
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Animal combat status updated: entity=" +
                controller.EntityId + " status=" + status + ".");
        }

        private void SendAnimalStatusEffects(
            string entityId,
            Messages.StatusEffect[] injuryEffects,
            HashSet<string> managedEffectIds)
        {
            List<Messages.StatusEffect> snapshot =
                new List<Messages.StatusEffect>();

            // Messages.StatusEffects replaces the complete list for an
            // entity.  Preserve effects owned by other systems and replace
            // only the injury ids controlled by AnimalInjuryRuntime.
            if (GameSystem<StatusEffectSystem>.HasInstance())
            {
                StatusEffectSystem system =
                    GameSystem<StatusEffectSystem>.Instance();
                Durango.Logic.StatusEffects current = system == null
                    ? null
                    : system.GetStatusEffects(entityId);
                if (current != null && current.List != null)
                {
                    int i;
                    for (i = 0; i < current.List.Count; i++)
                    {
                        Durango.Logic.StatusEffect effect = current.List[i];
                        if (effect == null ||
                            (managedEffectIds != null &&
                             managedEffectIds.Contains(effect.Id)))
                        {
                            continue;
                        }
                        snapshot.Add(ToStatusEffectMessage(
                            entityId,
                            effect));
                    }
                }
            }

            if (injuryEffects != null)
            {
                int i;
                for (i = 0; i < injuryEffects.Length; i++)
                {
                    snapshot.Add(injuryEffects[i]);
                }
            }

            Messages.StatusEffects message =
                default(Messages.StatusEffects);
            message.EntityId = entityId;
            message._StatusEffects = snapshot.ToArray();
            _player.Send(message, 0U);
        }

        private static Messages.StatusEffect ToStatusEffectMessage(
            string entityId,
            Durango.Logic.StatusEffect source)
        {
            Messages.StatusEffect message =
                default(Messages.StatusEffect);
            message.Id = entityId + ":preserved:" + source.Id + ":" +
                source.Level;
            message.EffectId = source.Id;
            message.Level = source.Level;
            message.Since = source.Since;
            message.Until = source.Until;
            message.Stacked = source.Stack;
            message.DurationHidden = source.Until <= 0.0;
            message.NameGettext = null;
            if (source.EffectDetails == null)
            {
                message.Effects = new Messages.EffectDetail[0];
            }
            else
            {
                message.Effects = new Messages.EffectDetail[
                    source.EffectDetails.Count];
                int i;
                for (i = 0; i < source.EffectDetails.Count; i++)
                {
                    message.Effects[i] = source.EffectDetails[i];
                }
            }
            message.DailyContents = source.DailyContents;
            return message;
        }

        private void OnAnimalAttackCommitted(
            SaurusAnimalController controller,
            SaurusActionPlan plan)
        {
            AnimalAttackDefinition attack = plan == null
                ? null
                : plan.Attack;
            if (!CanProcess() || controller == null || attack == null ||
                controller.IsDisposed || controller.Animal == null ||
                !controller.Animal.IsAlive || attack.Hits == null ||
                attack.Hits.Length == 0)
            {
                return;
            }

            double committedAt = plan.CommittedAt;
            Vector3 actorWorldAtCommit =
                Util.ClientPositionToWorldPosition(
                    plan.ActorPositionAtCommit);
            Vector2 actorOrigin = new Vector2(
                actorWorldAtCommit.x,
                actorWorldAtCommit.z);
            float actorYaw = plan.ActorYawAtCommit;

            Vector2 playerOrigin;
            float playerYaw;
            if (!TryGetPlayerPose(out playerOrigin, out playerYaw))
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Skipped Saurus attack because player pose is unavailable: " +
                    controller.EntityId + " action=" + attack.Key + ".");
                return;
            }

            float frameRate;
            int runtimeHitCount;
            if (!TryGetAnimalAttackTiming(
                controller.Animal,
                attack.Key,
                out frameRate,
                out runtimeHitCount))
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Skipped Saurus attack damage because original clip timing " +
                    "is unavailable: entity=" + controller.EntityId +
                    " action=" + attack.Key + ".");
                return;
            }
            if (runtimeHitCount != attack.Hits.Length)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Saurus attack snapshot/runtime hit count differs: entity=" +
                    controller.EntityId + " action=" + attack.Key +
                    " reference=" + attack.Hits.Length +
                    " runtime=" + runtimeHitCount + ".");
            }

            long attackInstanceId = ++_nextAnimalAttackInstanceId;
            List<AnimalAttackSnapshot> snapshots =
                new List<AnimalAttackSnapshot>();
            int i;
            for (i = 0; i < attack.Hits.Length; i++)
            {
                Vector3 plannedClientAtHit =
                    plan.GetPlannedActorPositionAtFrame(
                        attack.Hits[i].Frame,
                        frameRate);
                Vector3 plannedWorldAtHit =
                    Util.ClientPositionToWorldPosition(plannedClientAtHit);
                Vector2 actorOriginAtHit = new Vector2(
                    plannedWorldAtHit.x,
                    plannedWorldAtHit.z);
                float actorYawAtHit =
                    plan.GetPlannedActorYawAtFrame(
                        attack.Hits[i].Frame,
                        frameRate);
                AnimalAttackSnapshot snapshot = AnimalAttackSnapshot.Create(
                    attackInstanceId,
                    Generation,
                    i,
                    controller.Animal,
                    controller.Profile,
                    attack.Key,
                    attack.Hits[i],
                    committedAt,
                    frameRate,
                    actorOrigin,
                    actorOriginAtHit,
                    actorYaw,
                    actorYawAtHit,
                    playerOrigin,
                    plan);
                if (snapshot == null)
                {
                    DurangoCombatSystemPlugin.Log.LogWarning(
                        "Skipped unsupported Saurus hit geometry: entity=" +
                        controller.EntityId + " action=" + attack.Key +
                        " hit=" + i + ".");
                    continue;
                }
                snapshots.Add(snapshot);
            }
            if (snapshots.Count == 0)
            {
                return;
            }

            for (i = 0; i < snapshots.Count; i++)
            {
                AnimalAttackSnapshot snapshot = snapshots[i];
                _scheduledAnimalHits.Add(
                    new ScheduledAnimalHit(snapshot));
                AnimalAttackTelegraph.Show(snapshot);
            }
            _scheduledAnimalHits.Sort(delegate(
                ScheduledAnimalHit left,
                ScheduledAnimalHit right)
            {
                int time = left.Attack.HitAt.CompareTo(
                    right.Attack.HitAt);
                if (time != 0)
                {
                    return time;
                }
                int instance = left.Attack.AttackInstanceId.CompareTo(
                    right.Attack.AttackInstanceId);
                return instance != 0
                    ? instance
                    : left.Attack.HitIndex.CompareTo(
                        right.Attack.HitIndex);
            });

            if (string.IsNullOrEmpty(_battleEnemyId))
            {
                BeginBattle(controller.EntityId, committedAt);
            }
            _battleLastActivityAt = committedAt;
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Scheduled Saurus attack entity=" + controller.EntityId +
                " action=" + attack.Key +
                " instance=" + attackInstanceId +
                " hits=" + snapshots.Count +
                " fps=" + frameRate + ".");
        }

        private void ResolveScheduledAnimalHit(
            ScheduledAnimalHit scheduled,
            double now)
        {
            AnimalAttackSnapshot attack = scheduled == null
                ? null
                : scheduled.Attack;
            if (!IsAnimalAttackCurrent(attack))
            {
                AnimalAttackTelegraph.Cancel(attack);
                return;
            }

            AnimalAttackTelegraph.Release(attack);

            Vector2 playerPosition;
            float playerYaw;
            if (!TryGetPlayerPose(out playerPosition, out playerYaw))
            {
                return;
            }
            PlayerBehavior local = PlayerBehavior.LocalPlayer;
            if (local == null || !local.IsAlive)
            {
                return;
            }

            float playerRadius = GetCharacterRadius(local);
            if (!attack.Contains(playerPosition, playerRadius))
            {
                DurangoCombatSystemPlugin.Log.LogInfo(
                    "Saurus hit resolved outside area: entity=" +
                    attack.ActorEntityId + " action=" + attack.AttackId +
                    " hit=" + attack.HitIndex +
                    " center=" + attack.Center +
                    " player=" + playerPosition + ".");
                _battleLastActivityAt = now;
                return;
            }

            AnimalInjuryModifiers injuryModifiers =
                _injuries.GetModifiers(
                    attack.ActorEntityId,
                    attack.AnimalObjectInstanceId);
            ResolvedAnimalHit resolved = AnimalHitResolver.Resolve(
                attack,
                playerPosition,
                playerYaw,
                injuryModifiers);
            SendAnimalDamage(attack, resolved, now);
            _battleLastActivityAt = now;

            if (!resolved.UsedExactAttackFormula ||
                !resolved.UsedExactAccuracyFormula ||
                !resolved.UsedExactAttackRatingFormula)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Saurus damage used a fallback formula: entity=" +
                    attack.ActorEntityId + " action=" + attack.AttackId +
                    " attack=" + resolved.UsedExactAttackFormula +
                    " accuracy=" + resolved.UsedExactAccuracyFormula +
                    " rating=" + resolved.UsedExactAttackRatingFormula + ".");
            }
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Saurus hit resolved: entity=" + attack.ActorEntityId +
                " action=" + attack.AttackId +
                " subAction=" + (attack.SubActionId ?? "none") +
                " hit=" + attack.HitIndex +
                " result=" + resolved.Result +
                " value=" + resolved.Value + ".");
        }

        private void RefreshPendingAnimalAttackGeometry(double now)
        {
            int i;
            for (i = 0; i < _scheduledAnimalHits.Count; i++)
            {
                AnimalAttackSnapshot attack =
                    _scheduledAnimalHits[i].Attack;
                if (!IsAnimalAttackCurrent(attack) ||
                    attack.ActionPlan == null || attack.Animal == null)
                {
                    continue;
                }

                SaurusActionPlan plan = attack.ActionPlan;
                float elapsed = Mathf.Max(
                    0f,
                    (float)(now - plan.CommittedAt));
                Vector3 actualClient = attack.Animal.CurrentPosition;
                Vector3 actorClientAtHit;
                float actorYawAtHit;
                if (now >= attack.HitAt)
                {
                    actorClientAtHit = actualClient;
                    float hitElapsed = Mathf.Max(
                        0f,
                        (float)(attack.HitAt - plan.CommittedAt));
                    actorYawAtHit =
                        plan.GetPlannedActorYaw(hitElapsed);
                }
                else
                {
                    Vector3 plannedNow =
                        plan.GetPlannedActorPosition(elapsed);
                    Vector3 collisionCorrection =
                        actualClient - plannedNow;
                    float hitElapsed = Mathf.Max(
                        0f,
                        (float)(attack.HitAt - plan.CommittedAt));
                    actorClientAtHit =
                        plan.GetPlannedActorPosition(hitElapsed) +
                        collisionCorrection;
                    actorYawAtHit =
                        plan.GetPlannedActorYaw(hitElapsed);
                }

                Vector3 actorWorldAtHit =
                    Util.ClientPositionToWorldPosition(actorClientAtHit);
                attack.RefreshGeometry(
                    new Vector2(
                        actorWorldAtHit.x,
                        actorWorldAtHit.z),
                    actorYawAtHit);
                // Move is intentionally called even when the center stayed
                // unchanged: this makes /dev attackalert on/off take effect
                // for an already-pending animal hit without a second owner.
                AnimalAttackTelegraph.Move(attack);
            }
        }

        private void CancelScheduledAnimalAttacks(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                return;
            }
            int i;
            for (i = _scheduledAnimalHits.Count - 1; i >= 0; i--)
            {
                AnimalAttackSnapshot attack =
                    _scheduledAnimalHits[i].Attack;
                if (attack == null || !string.Equals(
                    attack.ActorEntityId,
                    entityId,
                    StringComparison.Ordinal))
                {
                    continue;
                }
                AnimalAttackTelegraph.Cancel(attack);
                _scheduledAnimalHits.RemoveAt(i);
            }
        }

        private void SendAnimalDamage(
            AnimalAttackSnapshot attack,
            ResolvedAnimalHit resolved,
            double now)
        {
            Messages.Damage damage = default(Messages.Damage);
            damage.Result = resolved.Result;
            damage.Value = resolved.Value;
            damage.Part = resolved.Part;
            damage.Direction = resolved.Direction;
            damage.AttackType = resolved.AttackType;
            damage.Effects = resolved.Effects;

            Damaged damaged = default(Damaged);
            damaged.VictimId = _player.EntityId;
            damaged.AttackerId = attack.ActorEntityId;
            damaged.Damage = damage;
            damaged.EventAt = attack.HitAt;
            _player.Send(damaged, 0U);

            if (resolved.Result != DamageResult.Hit ||
                resolved.Value <= 0)
            {
                return;
            }

            AppearPlayer appear = _context.AppearPlayer;
            Survival survival = appear.Survival;
            Gauge current = survival.Life;
            if (current == null && PlayerBehavior.LocalPlayer != null)
            {
                current = PlayerBehavior.LocalPlayer.Life;
            }
            if (current == null)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Saurus damage message was sent, but player life gauge " +
                    "is unavailable for persistence.");
                return;
            }

            float maximum = Mathf.Max(1f, current.Max(now));
            float minimum = current.Min(now);
            float next = Mathf.Max(
                minimum,
                current.Get(now) - resolved.Value);
            Gauge updated = new Gauge(
                maximum,
                minimum,
                new GaugeNode[] { new GaugeNode(now, next) });
            survival.Life = updated;
            appear.Survival = survival;
            _context.AppearPlayer = appear;
            NotifyPlayerContextChanged();

            SurvivalUpdated message = default(SurvivalUpdated);
            message.EntityId = _player.EntityId;
            message.Updated = new Dictionary<string, Gauge>();
            message.Updated["life"] = updated;
            message.Removed = new string[0];
            _player.Send(message, 0U);

            if (next <= minimum)
            {
                EntityDied died = default(EntityDied);
                died.EntityId = _player.EntityId;
                died.At = now;
                _player.Send(died, 0U);
                EndBattle(now);
            }
        }

        private bool IsAnimalAttackCurrent(AnimalAttackSnapshot attack)
        {
            if (attack == null || attack.Generation != Generation ||
                attack.Animal == null || !attack.Animal.IsAlive ||
                attack.Animal.gameObject.GetInstanceID() !=
                    attack.AnimalObjectInstanceId)
            {
                return false;
            }
            if (!Durango.Utils.Singleton<AnimalManager>.HasInstance())
            {
                return false;
            }
            AnimalBehavior indexed =
                Durango.Utils.Singleton<AnimalManager>.Instance()
                    .GetAnimal(attack.ActorEntityId);
            return object.ReferenceEquals(indexed, attack.Animal);
        }

        private static bool TryGetAnimalAttackTiming(
            AnimalBehavior animal,
            string attackKey,
            out float frameRate,
            out int runtimeHitCount)
        {
            frameRate = 0f;
            runtimeHitCount = 0;
            if (animal == null || animal.AnimalFrameworkResource == null ||
                string.IsNullOrEmpty(attackKey))
            {
                return false;
            }
            AnimationElemAttack runtimeAttack =
                animal.AnimalFrameworkResource.GetAnimationElements(
                    attackKey) as AnimationElemAttack;
            if (runtimeAttack == null || runtimeAttack.meta == null ||
                runtimeAttack.meta.Clip == null ||
                runtimeAttack.meta.Clip.frameRate <= 0f)
            {
                return false;
            }
            frameRate = runtimeAttack.meta.Clip.frameRate;
            runtimeHitCount = runtimeAttack.attack_info == null
                ? 0
                : runtimeAttack.attack_info.Count;
            return true;
        }

        private static bool TryGetAnimalPose(
            AnimalBehavior animal,
            out Vector2 position,
            out float yaw)
        {
            position = Vector2.zero;
            yaw = 0f;
            if (animal == null)
            {
                return false;
            }
            Vector3 world = Util.ClientPositionToWorldPosition(
                animal.CurrentPosition);
            position = new Vector2(world.x, world.z);
            yaw = animal.CurrentYaw;
            return true;
        }

        private static float GetCharacterRadius(PlayerBehavior character)
        {
            if (character == null)
            {
                return 0f;
            }
            float scale = Mathf.Max(
                Mathf.Abs(character.transform.lossyScale.x),
                Mathf.Abs(character.transform.lossyScale.z));
            return Mathf.Max(character.XRadius, character.YRadius) *
                Mathf.Max(0.01f, scale);
        }

        internal bool TryAddPlayerGauge(
            string gaugeName,
            float amount,
            out string response)
        {
            response = null;
            if (!CanProcess())
            {
                response = "Enter an offline world first.";
                return false;
            }
            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                response = "Amount must be a finite number.";
                return false;
            }

            double now = Times.UnixTimeNow();
            AppearPlayer appear = _context.AppearPlayer;
            Survival survival = appear.Survival;
            Gauge current;
            string label;
            bool isLife = string.Equals(
                gaugeName,
                "life",
                StringComparison.Ordinal);
            if (isLife)
            {
                current = survival.Life;
                label = "HP";
            }
            else if (string.Equals(
                gaugeName,
                "stamina",
                StringComparison.Ordinal))
            {
                current = null;
                if (survival.Gauges != null)
                {
                    survival.Gauges.TryGetValue(
                        gaugeName,
                        out current);
                }
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

            float maximum = Mathf.Max(1f, current.Max(now));
            float minimum = current.Min(now);
            float next = Mathf.Max(
                minimum,
                current.Get(now) + amount);
            Gauge updated = new Gauge(
                maximum,
                minimum,
                new GaugeNode[] { new GaugeNode(now, next) });

            if (isLife)
            {
                survival.Life = updated;
            }
            else
            {
                if (survival.Gauges == null)
                {
                    survival.Gauges =
                        new Dictionary<string, Gauge>();
                }
                survival.Gauges[gaugeName] = updated;
            }
            appear.Survival = survival;
            _context.AppearPlayer = appear;
            NotifyPlayerContextChanged();

            SurvivalUpdated message = default(SurvivalUpdated);
            message.EntityId = _player.EntityId;
            message.Updated = new Dictionary<string, Gauge>();
            message.Updated[gaugeName] = updated;
            message.Removed = new string[0];
            _player.Send(message, 0U);

            response = label + " " + Mathf.RoundToInt(next) + "/" +
                Mathf.RoundToInt(maximum) + " (saved)";
            return true;
        }

        private void BeginBattle(string enemyId, double now)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                return;
            }
            if (!string.IsNullOrEmpty(_battleEnemyId) &&
                !string.Equals(
                    _battleEnemyId,
                    enemyId,
                    StringComparison.Ordinal))
            {
                EndBattle(now);
            }
            if (string.IsNullOrEmpty(_battleEnemyId))
            {
                _battleEnemyId = enemyId;
                BattleBegun begun = default(BattleBegun);
                begun.EntityId = _player.EntityId;
                begun.EventAt = now;
                begun.EnemyId = enemyId;
                begun.StartDamaged = false;
                _player.Send(begun, 0U);
            }
            _battleLastActivityAt = now;
        }

        private void EndBattle(double now)
        {
            if (string.IsNullOrEmpty(_battleEnemyId))
            {
                return;
            }
            BattleEnded ended = default(BattleEnded);
            ended.EntityId = _player.EntityId;
            ended.EventAt = now;
            _player.Send(ended, 0U);
            SendPlayerSurvivalSnapshot();
            _battleEnemyId = null;
            _battleLastActivityAt = 0.0;
        }

        private void SendPlayerSurvivalSnapshot()
        {
            AppearPlayer appear = _context.AppearPlayer;
            Survival survival = appear.Survival;
            Dictionary<string, Gauge> updated =
                new Dictionary<string, Gauge>();

            if (survival.Life != null)
            {
                updated["life"] = survival.Life;
            }

            Gauge stamina;
            if (survival.Gauges != null &&
                survival.Gauges.TryGetValue("stamina", out stamina) &&
                stamina != null)
            {
                updated["stamina"] = stamina;
            }

            if (updated.Count == 0)
            {
                return;
            }

            SurvivalUpdated message = default(SurvivalUpdated);
            message.EntityId = _player.EntityId;
            message.Updated = updated;
            message.Removed = new string[0];
            _player.Send(message, 0U);
        }

        private bool TryGetPlayerPose(
            out Vector2 position,
            out float yaw)
        {
            position = Vector2.zero;
            yaw = 0f;
            try
            {
                PlayerBehavior local = PlayerBehavior.LocalPlayer;
                if (local != null &&
                    string.Equals(
                        local.EntityId,
                        _player.EntityId,
                        StringComparison.Ordinal))
                {
                    Vector3 world = Util.ClientPositionToWorldPosition(
                        local.CurrentPosition);
                    position = new Vector2(world.x, world.z);
                    yaw = local.CurrentYaw;
                    return true;
                }
            }
            catch
            {
            }

            Movement[] movements = _context.AppearPlayer.Move.Movements;
            if (movements == null || movements.Length == 0 ||
                movements[0].Path == null ||
                movements[0].Path.Length == 0)
            {
                return false;
            }
            Location location = movements[0].Path[0];
            position = location.Position.ToVector2();
            yaw = location.Yaw;
            return true;
        }

        private bool HasStamina(float cost, double now)
        {
            if (cost <= 0f)
            {
                return true;
            }
            Gauge stamina = GetStaminaGauge();
            float available = stamina == null ? 100f : stamina.Get(now);
            return available + 0.001f >= cost;
        }

        private void ConsumeStamina(float cost, double now)
        {
            if (cost <= 0f)
            {
                return;
            }

            AppearPlayer appear = _context.AppearPlayer;
            Survival survival = appear.Survival;
            if (survival.Gauges == null)
            {
                survival.Gauges = new Dictionary<string, Gauge>();
            }
            Gauge current;
            survival.Gauges.TryGetValue("stamina", out current);
            float maximum = current == null
                ? 100f
                : Mathf.Max(1f, current.Max(now));
            float minimum = current == null ? 0f : current.Min(now);
            float value = current == null ? maximum : current.Get(now);
            // DeveloperModePlugin intentionally allows /sp to raise the
            // current value above the normal maximum.  Consuming the first
            // action must subtract its cost from that current value instead
            // of clamping the whole gauge back to maximum.
            float next = Mathf.Max(minimum, value - cost);
            Gauge updated = new Gauge(
                maximum,
                minimum,
                new GaugeNode[] { new GaugeNode(now, next) });
            survival.Gauges["stamina"] = updated;
            appear.Survival = survival;
            _context.AppearPlayer = appear;
            NotifyPlayerContextChanged();

            SurvivalUpdated message = default(SurvivalUpdated);
            message.EntityId = _player.EntityId;
            message.Updated = new Dictionary<string, Gauge>();
            message.Updated["stamina"] = updated;
            message.Removed = new string[0];
            _player.Send(message, 0U);
        }

        private void NotifyPlayerContextChanged()
        {
            if (PlayerContextChangedMethod == null)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Player.OnContextChanged was not found; stamina remains " +
                    "valid for this session but may not persist on title return.");
                return;
            }
            try
            {
                PlayerContextChangedMethod.Invoke(_player, null);
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Could not notify the original player persistence owner " +
                    "after stamina changed: " + exception.Message);
            }
        }

        private Gauge GetStaminaGauge()
        {
            Dictionary<string, Gauge> gauges =
                _context.AppearPlayer.Survival.Gauges;
            Gauge stamina;
            return gauges != null &&
                gauges.TryGetValue("stamina", out stamina)
                ? stamina
                : null;
        }

        private bool CanProcess()
        {
            return !IsDisposed &&
                _player != null &&
                _connection != null &&
                _world != null &&
                CombatRuntime.IsCurrent(this);
        }

        private void RefreshActions()
        {
            TrySubscribeClientState();
            _actions = AvailableActionProvider.Build();
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Combat actions refreshed: count=" +
                _actions.Statuses.Length +
                " equipment=" + _actions.EquipmentSource +
                " equipmentReady=" + _actions.EquipmentDataReady +
                " skillsReady=" + _actions.SkillDataReady + ".");
        }

        private static bool IsActionDataReady(
            CombatActionSnapshot actions)
        {
            return actions != null &&
                actions.EquipmentDataReady &&
                actions.SkillDataReady;
        }

        private void QueueActionRefresh(uint replyOf, bool hasReply)
        {
            _actionRefreshPending = true;
            if (hasReply)
            {
                _pendingActionsReply = true;
                _pendingActionsReplyOf = replyOf;
            }
            _nextActionRefreshAt =
                Times.UnixTimeNow() + ActionRefreshRetrySeconds;
        }

        private void ProcessPendingActionRefresh(double now)
        {
            if (!_actionRefreshPending || now < _nextActionRefreshAt)
            {
                return;
            }

            RefreshActions();
            if (!IsActionDataReady(_actions))
            {
                _nextActionRefreshAt =
                    now + ActionRefreshRetrySeconds;
                return;
            }

            uint replyOf = _pendingActionsReply
                ? _pendingActionsReplyOf
                : 0U;
            _actionRefreshPending = false;
            _pendingActionsReply = false;
            _pendingActionsReplyOf = 0U;
            SendActions(replyOf);
        }

        private void SendActions(uint replyOf)
        {
            if (_actions == null || !CanProcess())
            {
                return;
            }

            Messages.Actions response = default(Messages.Actions);
            response.BattleActions = _actions.Statuses;
            _player.Send(response, replyOf);
        }

        private void TrySubscribeClientState()
        {
            try
            {
                if (_equipSystem == null)
                {
                    _equipSystem = GameSystem<EquipSystem>.Instance();
                    if (_equipSystem != null)
                    {
                        _equipSystem.EquipmentsUpdated += OnClientStateChanged;
                    }
                }

                if (_skillSystem == null)
                {
                    _skillSystem = GameSystem<SkillSystem>.Instance();
                    if (_skillSystem != null)
                    {
                        _skillSystem.SkillListUpdated += OnClientStateChanged;
                    }
                }
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Combat action refresh events are not ready yet: " +
                    exception.Message);
            }
        }

        private void OnClientStateChanged()
        {
            if (!CanProcess())
            {
                return;
            }

            try
            {
                RefreshActions();
                if (IsActionDataReady(_actions))
                {
                    uint replyOf = _pendingActionsReply
                        ? _pendingActionsReplyOf
                        : 0U;
                    _actionRefreshPending = false;
                    _pendingActionsReply = false;
                    _pendingActionsReplyOf = 0U;
                    SendActions(replyOf);
                }
                else
                {
                    QueueActionRefresh(
                        _pendingActionsReplyOf,
                        _pendingActionsReply);
                }
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogError(
                    "Failed to refresh combat actions after client state update: " +
                    exception);
            }
        }

        private bool RememberPacket(uint sequence)
        {
            if (_processedPackets.Contains(sequence))
            {
                return false;
            }

            _processedPackets.Add(sequence);
            _processedPacketOrder.Enqueue(sequence);
            while (_processedPacketOrder.Count > RememberedPacketCount)
            {
                _processedPackets.Remove(_processedPacketOrder.Dequeue());
            }
            return true;
        }

        private static void WarnRejected(string actionId, string reason)
        {
            DurangoCombatSystemPlugin.Log.LogWarning(
                "Rejected player action " +
                (actionId ?? "<null>") + ": " + reason + ".");
        }

        private void OnPlayerClosed()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }
            IsDisposed = true;

            if (_player != null)
            {
                _player.Closed -= OnPlayerClosed;
            }
            if (_equipSystem != null)
            {
                _equipSystem.EquipmentsUpdated -= OnClientStateChanged;
                _equipSystem = null;
            }
            if (_skillSystem != null)
            {
                _skillSystem.SkillListUpdated -= OnClientStateChanged;
                _skillSystem = null;
            }

            _actions = null;
            _actionRefreshPending = false;
            _pendingActionsReply = false;
            _pendingActionsReplyOf = 0U;
            _scheduledHits.Clear();
            _scheduledAnimalHits.Clear();
            PlayerAttackTelegraph.Clear();
            AnimalAttackTelegraph.Clear();
            _pendingActionHits.Clear();
            if (_saurusAi != null)
            {
                _saurusAi.AttackCommitted -= OnAnimalAttackCommitted;
                _saurusAi.GroggyGaugeChanged -=
                    OnAnimalGroggyGaugeChanged;
                _saurusAi.StatusChanged -= OnAnimalStatusChanged;
                _saurusAi.Dispose();
            }
            _targets.Clear();
            _injuries.Clear();
            _cooldownUntil.Clear();
            _processedPackets.Clear();
            _processedPacketOrder.Clear();
            LastAcceptedAction = null;
            _battleEnemyId = null;
            CombatRuntime.Release(this);
        }
    }
}
