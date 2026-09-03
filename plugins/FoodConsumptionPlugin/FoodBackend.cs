using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.Offline;
using Durango.Utils;
using HarmonyLib;
using Messages;

namespace BaoX.DurangoOriginal.FoodConsumptionMod
{
    public sealed class FoodState
    {
        public long UpdatedUtcTicks;
        public List<PersistedFoodEffect> Effects = new List<PersistedFoodEffect>();
    }

    public sealed class PersistedFoodEffect
    {
        public string EffectId;
        public int Level;
        public long EndUtcTicks;
        public bool DurationHidden;
        public Messages.EffectDetail[] Details;
    }

    internal sealed class FoodSession
    {
        internal Durango.Offline.Player Player;
        internal PlayerContext Context;
        internal string PlayerId;
        internal string ItemId;
        internal string ItemName;
        internal FoodEffect Effect;
        internal object ScheduledToken;
    }

    internal static class FoodBackend
    {
        private const string StorageKey = "food_consumption_v3";
        private const float DefaultConsumeDuration = 2f;
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, FoodSession> Sessions = new Dictionary<string, FoodSession>();
        private static readonly FieldInfo ContextField = AccessTools.Field(typeof(Durango.Offline.Player), "_context");
        private static readonly MethodInfo ContextChangedMethod = typeof(Durango.Offline.Player).GetMethod(
            "OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static void ConstructorPostfix(Durango.Offline.Player __instance, Durango.Offline.Connection connection)
        {
            connection.Recv<UseItem>(delegate(UseItem message, PacketHeader header)
            {
                try { HandleUseItem(__instance, message, header); }
                catch (Exception ex)
                {
                    FoodConsumptionPlugin.Log.LogError("UseItem failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });

            connection.Recv<GetStatusEffects>(delegate(GetStatusEffects message, PacketHeader header)
            {
                try
                {
                    PlayerContext context = GetContext(__instance);
                    FoodState state = LoadState(context);
                    RefreshState(state);
                    SaveState(context, state, false);
                    SendStatusEffects(__instance, state, header.Seq);
                }
                catch (Exception ex)
                {
                    FoodConsumptionPlugin.Log.LogError("GetStatusEffects failed: " + ex);
                    __instance.Send<Messages.StatusEffects>(new Messages.StatusEffects
                    {
                        EntityId = __instance.EntityId,
                        _StatusEffects = new Messages.StatusEffect[0]
                    }, header.Seq);
                }
            });
        }

        private static PlayerContext GetContext(Durango.Offline.Player player)
        {
            return ContextField == null ? null : ContextField.GetValue(player) as PlayerContext;
        }

        private static void HandleUseItem(Durango.Offline.Player player, UseItem message, PacketHeader header)
        {
            PlayerContext context = GetContext(player);
            if (context == null || context.InventoryItems == null)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            int index = context.InventoryItems.FindIndex(delegate(Item candidate) { return candidate.Id == message.ItemId; });
            if (index < 0)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            Item item = context.InventoryItems[index];
            if (!FoodDatabase.IsEdible(item))
            {
                FoodConsumptionPlugin.Log.LogWarning("Rejected non-food UseItem: " + item.Prototype);
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            FoodState state = LoadState(context);
            RefreshState(state);
            if (HasActiveEffect(state, "satiety_high"))
            {
                SaveState(context, state, true);
                SendStatusEffects(player, state, 0U);
                player.Send<ItemUsed>(new ItemUsed { Motion = string.Empty, Time = 0f, Msg = "You are too full to eat." }, 0U);
                player.Send<Abort>(default(Abort), header.Seq);
                FoodConsumptionPlugin.Log.LogInfo("Eating blocked by active satiety_high: player=" + player.EntityId);
                return;
            }

            FoodEffect effect = FoodDatabase.Resolve(item);
            if (effect.HasModifier && HasActiveEffect(state, "food_ability") && !message.Accept)
            {
                player.Send<AskEatFoodOverrideStatusEffect>(new AskEatFoodOverrideStatusEffect { ItemId = item.Id }, 0U);
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            float consumeDuration = Math.Max(0.1f, effect.Number("digestivetime", DefaultConsumeDuration));
            lock (Sync)
            {
                if (Sessions.ContainsKey(player.EntityId))
                {
                    player.Send<Abort>(default(Abort), header.Seq);
                    return;
                }

                FoodSession session = new FoodSession
                {
                    Player = player,
                    Context = context,
                    PlayerId = player.EntityId,
                    ItemId = item.Id,
                    ItemName = string.IsNullOrEmpty(item.Name) ? item.Prototype : item.Name,
                    Effect = effect
                };
                session.ScheduledToken = FoodConsumptionPlugin.Schedule(consumeDuration, delegate { Complete(session); });
                Sessions[player.EntityId] = session;
            }

            string motion = effect.Text("eat_motion", FoodDatabase.HasTag(item, "drinkable") ? "Drink" : "Eat");
            player.Send<ItemUsed>(new ItemUsed { Motion = motion, Time = consumeDuration, Msg = string.Empty }, 0U);
            player.Send<StartTimer>(new StartTimer
            {
                EntityId = player.EntityId,
                Subject = "Eat",
                Current = 0f,
                Time = consumeDuration,
                AdditionalTime = 0f
            }, header.Seq);
            FoodConsumptionPlugin.Log.LogInfo("Started eating item=" + item.Prototype + " duration=" + consumeDuration + " player=" + player.EntityId);
        }

        private static void Complete(FoodSession session)
        {
            lock (Sync)
            {
                FoodSession current;
                if (!Sessions.TryGetValue(session.PlayerId, out current) || !object.ReferenceEquals(current, session)) return;
                Sessions.Remove(session.PlayerId);
            }

            PlayerContext context = session.Context;
            if (context == null || context.InventoryItems == null) return;
            int index = context.InventoryItems.FindIndex(delegate(Item candidate) { return candidate.Id == session.ItemId; });
            if (index < 0)
            {
                FoodConsumptionPlugin.Log.LogWarning("Food disappeared before completion: " + session.ItemId);
                return;
            }

            Item item = context.InventoryItems[index];
            FoodState state = LoadState(context);
            RefreshState(state);
            ApplyFood(context, item, session.Effect, state);
            context.InventoryItems.RemoveAt(index);
            SaveState(context, state, true);
            NotifyContextChanged(session.Player);

            session.Player.Send<InventoryUpdated>(new InventoryUpdated
            {
                EntityId = session.PlayerId,
                RemovedItemIds = new string[] { session.ItemId },
                Items = new Item[0]
            }, 0U);

            Survival survival = context.AppearPlayer.Survival;
            session.Player.Send<Survival>(new Survival
            {
                EntityId = session.PlayerId,
                Life = survival.Life,
                Gauges = survival.Gauges
            }, 0U);
            SendStatusEffects(session.Player, state, 0U);
            FoodConsumptionPlugin.Log.LogInfo("Finished eating item=" + item.Prototype + " satiety_debuff=" + session.Effect.Number("satiety", 0f) + "s");
        }

        private static void ApplyFood(PlayerContext context, Item item, FoodEffect effect, FoodState state)
        {
            AppearPlayer appear = context.AppearPlayer;
            Survival survival = appear.Survival;
            if (survival.Gauges == null) survival.Gauges = new Dictionary<string, Gauge>();
            double now = Gauge.CurrentTime;

            float lifeMax = survival.Life == null ? 100f : Math.Max(1f, survival.Life.Max(now));
            float lifeDelta = effect.Number("life", 0f) + effect.Number("health", 0f)
                + lifeMax * (effect.Number("life_ratio", 0f) + effect.Number("health_ratio", 0f));
            survival.Life = ChangeGauge(survival.Life, lifeDelta, 100f, now);
            if (survival.Gauges.ContainsKey("life")) survival.Gauges["life"] = survival.Life;

            Gauge stamina;
            survival.Gauges.TryGetValue("stamina", out stamina);
            float staminaMax = stamina == null ? 100f : Math.Max(1f, stamina.Max(now));
            float potential = effect.Number("energy_potential", 0f);
            float energy = potential * (effect.Number("energy_expression", 1f) + effect.Number("energy_expression_over_time", 0f))
                + effect.Number("energy_ratio", 0f) * staminaMax
                + effect.Number("energy_per_sec", 0f) * Math.Max(0f, effect.Number("digestivetime", 0f));
            survival.Gauges["stamina"] = ChangeGauge(stamina, energy, 100f, now);

            Gauge fatigue;
            survival.Gauges.TryGetValue("fatigue", out fatigue);
            survival.Gauges["fatigue"] = ChangeGauge(fatigue, effect.Number("fatigue", 0f), 100f, now);

            appear.Survival = survival;
            context.AppearPlayer = appear;

            string effectOff = effect.Text("effect_off", string.Empty);
            if (!string.IsNullOrEmpty(effectOff)) RemoveEffect(state, effectOff);
            if (effect.Number("water", 0f) > 0f) RemoveEffect(state, "thirsty");

            string effectOn = effect.Text("effect_on", string.Empty);
            if (!string.IsNullOrEmpty(effectOn))
                AddEffect(state, effectOn, Math.Max(1, (int)effect.Number("effect_on_level", 1f)),
                    FoodDatabase.StatusDuration(effectOn, item.Level, 300f), false,
                    FoodDatabase.TemplateDetails(effectOn, item.Level));

            if (FoodDatabase.HasTag(item, "raw") || FoodDatabase.HasTag(item, "raw_food"))
                AddEffect(state, "raw_food", 1, FoodDatabase.StatusDuration("raw_food", 1, 300f), false,
                    FoodDatabase.TemplateDetails("raw_food", 1));

            if (effect.HasModifier)
            {
                RemoveEffect(state, "food_ability");
                AddEffect(state, effect.Text("effect_container", "food_ability"), 1,
                    Math.Max(1f, effect.Number("modifier_effect_time", 300f)), false, effect.ModifierDetails());
            }
            float satietyDuration = Math.Max(0f, effect.Number("satiety", 0f));
            if (satietyDuration > 0f)
                AddEffect(state, "satiety_high", 1, satietyDuration, false,
                    FoodDatabase.TemplateDetails("satiety_high", 1));
        }

        private static Gauge ChangeGauge(Gauge gauge, float delta, float fallbackMax, double now)
        {
            float max = gauge == null ? fallbackMax : Math.Max(1f, gauge.Max(now));
            float min = gauge == null ? 0f : gauge.Min(now);
            float current = gauge == null ? min : gauge.Get(now);
            float next = Math.Min(max, Math.Max(min, current + delta));
            return new Gauge(max, min, new GaugeNode[] { new GaugeNode(now, next) });
        }

        internal static void Cancel(string playerId)
        {
            FoodSession session = null;
            lock (Sync)
            {
                if (!Sessions.TryGetValue(playerId, out session)) return;
                Sessions.Remove(playerId);
            }
            FoodConsumptionPlugin.CancelScheduled(session.ScheduledToken);
            FoodConsumptionPlugin.Log.LogInfo("Eating interrupted; item and survival unchanged. player=" + playerId);
        }

        internal static void CancelAll()
        {
            FoodSession[] sessions;
            lock (Sync)
            {
                sessions = new List<FoodSession>(Sessions.Values).ToArray();
                Sessions.Clear();
            }
            for (int i = 0; i < sessions.Length; i++) FoodConsumptionPlugin.CancelScheduled(sessions[i].ScheduledToken);
        }

        private static FoodState LoadState(PlayerContext context)
        {
            if (context == null) return new FoodState { UpdatedUtcTicks = DateTime.UtcNow.Ticks };
            if (context.Storage == null) context.Storage = new Dictionary<string, byte[]>();
            byte[] data;
            if (context.Storage.TryGetValue(StorageKey, out data) && data != null && data.Length > 0)
            {
                try
                {
                    FoodState loaded = Json.Read<FoodState>(data, false);
                    if (loaded != null)
                    {
                        if (loaded.Effects == null) loaded.Effects = new List<PersistedFoodEffect>();
                        return loaded;
                    }
                }
                catch (Exception ex) { FoodConsumptionPlugin.Log.LogWarning("Food state reset: " + ex.Message); }
            }
            return new FoodState { UpdatedUtcTicks = DateTime.UtcNow.Ticks };
        }

        private static void RefreshState(FoodState state)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            state.UpdatedUtcTicks = nowTicks;
            if (state.Effects == null) state.Effects = new List<PersistedFoodEffect>();
            for (int i = state.Effects.Count - 1; i >= 0; i--)
                if (state.Effects[i].EndUtcTicks > 0 && state.Effects[i].EndUtcTicks <= nowTicks) state.Effects.RemoveAt(i);
        }

        private static void SaveState(PlayerContext context, FoodState state, bool saveContext)
        {
            if (context == null) return;
            if (context.Storage == null) context.Storage = new Dictionary<string, byte[]>();
            context.Storage[StorageKey] = Json.WriteToBytes<FoodState>(state, false, null);
            if (saveContext) context.Save();
        }

        private static bool HasActiveEffect(FoodState state, string effectId)
        {
            if (state.Effects == null) return false;
            for (int i = 0; i < state.Effects.Count; i++)
                if (string.Equals(state.Effects[i].EffectId, effectId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void RemoveEffect(FoodState state, string effectId)
        {
            if (state.Effects == null || string.IsNullOrEmpty(effectId)) return;
            for (int i = state.Effects.Count - 1; i >= 0; i--)
                if (string.Equals(state.Effects[i].EffectId, effectId, StringComparison.OrdinalIgnoreCase)) state.Effects.RemoveAt(i);
        }

        private static void AddEffect(FoodState state, string effectId, int level, float durationSeconds, bool hidden, Messages.EffectDetail[] details)
        {
            if (string.IsNullOrEmpty(effectId)) return;
            RemoveEffect(state, effectId);
            state.Effects.Add(new PersistedFoodEffect
            {
                EffectId = effectId,
                Level = Math.Max(1, level),
                EndUtcTicks = durationSeconds <= 0f ? 0L : DateTime.UtcNow.AddSeconds(durationSeconds).Ticks,
                DurationHidden = hidden,
                Details = details ?? new Messages.EffectDetail[0]
            });
        }

        private static void SendStatusEffects(Durango.Offline.Player player, FoodState state, uint replyOf)
        {
            double serverNow = Gauge.CurrentTime;
            long utcNow = DateTime.UtcNow.Ticks;
            List<Messages.StatusEffect> messages = new List<Messages.StatusEffect>();
            if (state.Effects != null)
            {
                for (int i = 0; i < state.Effects.Count; i++)
                {
                    PersistedFoodEffect effect = state.Effects[i];
                    double until = effect.EndUtcTicks <= 0
                        ? serverNow + 315360000.0
                        : serverNow + Math.Max(0.0, TimeSpan.FromTicks(effect.EndUtcTicks - utcNow).TotalSeconds);
                    messages.Add(new Messages.StatusEffect
                    {
                        Id = player.EntityId + ":food:" + effect.EffectId,
                        EffectId = effect.EffectId,
                        Level = effect.Level,
                        Since = serverNow,
                        Until = until,
                        Stacked = 1,
                        DurationHidden = effect.DurationHidden,
                        NameGettext = null,
                        Effects = effect.Details ?? new Messages.EffectDetail[0],
                        DailyContents = null
                    });
                }
            }
            player.Send<Messages.StatusEffects>(new Messages.StatusEffects
            {
                EntityId = player.EntityId,
                _StatusEffects = messages.ToArray()
            }, replyOf);
        }

        private static void NotifyContextChanged(Durango.Offline.Player player)
        {
            if (ContextChangedMethod != null) ContextChangedMethod.Invoke(player, null);
        }
    }

    [HarmonyPatch(typeof(Durango.Logic.Timer.Timer), "Stop", new Type[] { typeof(bool) })]
    internal static class InterruptedEatingCancellationPatch
    {
        private static void Postfix(Durango.Logic.Timer.Timer __instance)
        {
            if (GameManager.ClusterMode != Mode.Online && __instance != null && __instance.IsInterrupt &&
                string.Equals(__instance.Subject, "Eat", StringComparison.Ordinal))
                FoodBackend.Cancel(__instance.EntityId);
        }
    }
}
