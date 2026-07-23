using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Offline;
using Durango.Terrain;
using HarmonyLib;
using Messages;
using Shared.Item;
using Durango.Network;

namespace BaoX.DurangoOriginal.GatheringMod
{
    [BepInPlugin("com.baox.durango.original.gathering", "Gathering Plugin (Original)", "0.2.0")]
    public sealed class GatheringPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony("com.baox.durango.original.gathering");

            Type playerType = AccessTools.TypeByName("Durango.Offline.Player");
            if (playerType == null)
            {
                Logger.LogError("Durango.Offline.Player class not found!");
                return;
            }

            bool hasHandleTouchNatural = playerType.GetMethod("HandleTouchNatural", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
            if (hasHandleTouchNatural)
            {
                Logger.LogInfo("Detecting Kyllox client. Registering Kyllox patches.");

                var generatorMethod = playerType.GetMethod("Generator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (generatorMethod != null)
                {
                    _harmony.Patch(generatorMethod, null, new HarmonyMethod(typeof(KylloxPatches).GetMethod("GeneratorPostfix")), null, null, null);
                }

                var handleTouchNaturalMethod = playerType.GetMethod("HandleTouchNatural", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (handleTouchNaturalMethod != null)
                {
                    _harmony.Patch(handleTouchNaturalMethod, null, new HarmonyMethod(typeof(KylloxPatches).GetMethod("HandleTouchNaturalPostfix")), null, null, null);
                }

                var collectNaturalMethod = playerType.GetMethod("CollectNatural", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (collectNaturalMethod != null)
                {
                    _harmony.Patch(collectNaturalMethod, new HarmonyMethod(typeof(KylloxPatches).GetMethod("CollectNaturalPrefix")), null, null, null, null);
                }

                var sendCollectedMethod = playerType.GetMethod("SendCollected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendCollectedMethod != null)
                {
                    _harmony.Patch(sendCollectedMethod, null, new HarmonyMethod(typeof(KylloxPatches).GetMethod("SendCollectedPostfix")), null, null, null);
                }
            }
            else
            {
                Logger.LogInfo("Detecting Original client. Registering Original patches.");

                var ctor = playerType.GetConstructor(new Type[] {
                    typeof(string),
                    typeof(Durango.Offline.Connection),
                    typeof(Durango.Offline.World),
                    typeof(Durango.Offline.PlayerContext),
                    typeof(bool)
                });
                if (ctor != null)
                {
                    _harmony.Patch(ctor, null, new HarmonyMethod(typeof(OriginalPatches).GetMethod("ConstructorPostfix")), null, null, null);
                }
                else
                {
                    Logger.LogError("Player constructor not found!");
                }

                var handleTouchMsgMethod = playerType.GetMethod("HandleTouchMsg", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (handleTouchMsgMethod != null)
                {
                    _harmony.Patch(handleTouchMsgMethod, new HarmonyMethod(typeof(OriginalPatches).GetMethod("HandleTouchMsgPrefix")), null, null, null, null);
                }
                else
                {
                    Logger.LogError("HandleTouchMsg method not found!");
                }

                Type gatheringSystemType = AccessTools.TypeByName("GatheringSystem");
                if (gatheringSystemType != null)
                {
                    var onCollectedMethod = gatheringSystemType.GetMethod("OnCollected", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (onCollectedMethod != null)
                    {
                        _harmony.Patch(onCollectedMethod,
                            new HarmonyMethod(typeof(ClientPatches).GetMethod("OnCollectedPrefix")),
                            new HarmonyMethod(typeof(ClientPatches).GetMethod("OnCollectedPostfix")),
                            null, null, null);
                        Logger.LogInfo("GatheringSystem.OnCollected patched.");
                    }
                    else
                    {
                        Logger.LogError("GatheringSystem.OnCollected not found!");
                    }

                    var prop = gatheringSystemType.GetProperty("CurrentGatheringData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (prop != null)
                    {
                        var setter = prop.GetSetMethod(true);
                        if (setter != null)
                        {
                            _harmony.Patch(setter,
                                new HarmonyMethod(typeof(ClientPatches).GetMethod("set_CurrentGatheringDataPrefix")),
                                null, null, null, null);
                            Logger.LogInfo("GatheringSystem.CurrentGatheringData setter patched.");
                        }
                        else
                        {
                            Logger.LogError("GatheringSystem.CurrentGatheringData setter not found!");
                        }
                    }
                    else
                    {
                        Logger.LogError("GatheringSystem.CurrentGatheringData property not found!");
                    }
                }
                else
                {
                    Logger.LogError("GatheringSystem type not found.");
                }
            }

            Logger.LogInfo("Gathering Plugin loaded. Collectibles=Date Palm");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }

    internal static class DatePalmGatheringData
    {
        internal const string CollectibleId = "tree_date";
        internal const int XpReward = 2;

        private static readonly HashSet<string> RewardGeneratorIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "date",
            "wood_log",
            "leaf_large"
        };

        internal static bool IsDatePalm(string collectibleId)
        {
            return string.Equals(collectibleId, CollectibleId, StringComparison.Ordinal);
        }

        internal static bool IsRewardGenerator(string generatorId)
        {
            return generatorId != null && RewardGeneratorIds.Contains(generatorId);
        }

        internal static bool HasOnlyDatePalmGenerators(Generator[] generators)
        {
            if (generators == null || generators.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < generators.Length; i++)
            {
                if (!IsRewardGenerator(generators[i].Id))
                {
                    return false;
                }
            }

            return true;
        }

        internal static List<Generator> CreateGenerators(int level)
        {
            if (level <= 0)
            {
                level = 1;
            }

            List<Generator> generators = new List<Generator>();
            generators.Add(CreateGenerator("date", "Date", "icon_nat_fruit_date", 5, level));
            generators.Add(CreateGenerator("wood_log", "Log", "icon_nat_wood_log", 2, level));
            generators.Add(CreateGenerator("leaf_large", "Large Leaf", "icon_nat_leaf_big", 2, level));
            return generators;
        }

        private static Generator CreateGenerator(string id, string name, string icon, int amount, int level)
        {
            Dictionary<string, int> tools = new Dictionary<string, int>();
            tools.Add("bare_hands", 1);

            return new Generator
            {
                Id = id,
                Name = name,
                Icon = icon,
                Amount = amount,
                Level = level,
                Effort = 20f,
                Duration = 3f,
                Enabled = true,
                ToolRequirements = tools
            };
        }
    }

    internal static class DatePalmCollectState
    {
        private class GatherSession
        {
            public System.Timers.Timer Timer;
            public uint Seq;
            public DateTime StartTime;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<object, string> PendingGenerators = new Dictionary<object, string>();
        private static readonly Dictionary<object, GatherSession> ActiveSessions = new Dictionary<object, GatherSession>();

        internal static void Mark(object player, string generatorId)
        {
            lock (Gate)
            {
                if (generatorId == null)
                {
                    PendingGenerators.Remove(player);
                }
                else
                {
                    PendingGenerators[player] = generatorId;
                }
            }
        }

        internal static string Consume(object player)
        {
            lock (Gate)
            {
                string generatorId;
                if (!PendingGenerators.TryGetValue(player, out generatorId))
                {
                    return null;
                }

                PendingGenerators.Remove(player);
                return generatorId;
            }
        }

        internal static void RegisterSession(object player, System.Timers.Timer timer, uint seq)
        {
            lock (Gate)
            {
                GatherSession oldSession;
                if (ActiveSessions.TryGetValue(player, out oldSession))
                {
                    if (oldSession != null && oldSession.Timer != null)
                    {
                        try { oldSession.Timer.Stop(); oldSession.Timer.Dispose(); } catch {}
                    }
                }
                ActiveSessions[player] = new GatherSession 
                { 
                    Timer = timer, 
                    Seq = seq,
                    StartTime = DateTime.UtcNow
                };
            }
        }

        internal static bool IsActiveTimer(object player, System.Timers.Timer timer)
        {
            lock (Gate)
            {
                GatherSession session;
                if (ActiveSessions.TryGetValue(player, out session))
                {
                    return session != null && session.Timer == timer;
                }
                return false;
            }
        }

        internal static void RemoveSession(object player, System.Timers.Timer timer)
        {
            lock (Gate)
            {
                GatherSession session;
                if (ActiveSessions.TryGetValue(player, out session))
                {
                    if (session != null && session.Timer == timer)
                    {
                        ActiveSessions.Remove(player);
                    }
                }
            }
        }

        // Called by server when it receives Messages.Canceled from client (player moved mid-gather).
        // Stops and removes the active System.Timers.Timer for this player.
        internal static void CancelSession(object player)
        {
            lock (Gate)
            {
                GatherSession session;
                if (ActiveSessions.TryGetValue(player, out session))
                {
                    if (session != null && session.Timer != null)
                    {
                        try { session.Timer.Stop(); session.Timer.Dispose(); } catch {}
                    }
                    ActiveSessions.Remove(player);
                    PendingGenerators.Remove(player);
                    GatheringPlugin.Log.LogInfo("[Server] Harvest session cancelled by client cancel signal.");
                }
            }
        }

    }

    internal static class DatePalmReflection
    {
        private static readonly Dictionary<string, Collectible> CustomCollectedFrom = new Dictionary<string, Collectible>();

        internal static Collectible? GetCollectedFrom(object player, string tileKey)
        {
            try
            {
                object context = GetWorldContext(player);
                if (context == null)
                {
                    Collectible col;
                    if (CustomCollectedFrom.TryGetValue(tileKey, out col))
                    {
                        return col;
                    }
                    return null;
                }

                FieldInfo field = AccessTools.Field(context.GetType(), "CollectedFrom");
                if (field == null)
                {
                    Collectible col;
                    if (CustomCollectedFrom.TryGetValue(tileKey, out col))
                    {
                        return col;
                    }
                    return null;
                }

                IDictionary<string, Collectible> collectedFrom = field.GetValue(context) as IDictionary<string, Collectible>;
                if (collectedFrom == null)
                {
                    Collectible col;
                    if (CustomCollectedFrom.TryGetValue(tileKey, out col))
                    {
                        return col;
                    }
                    return null;
                }

                Collectible collectible;
                if (!collectedFrom.TryGetValue(tileKey, out collectible))
                {
                    return null;
                }

                return new Collectible?(collectible);
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("GetCollectedFrom failed: " + ex.Message);
                return null;
            }
        }

        internal static void SetCollectedFrom(object player, string tileKey, Collectible collectible)
        {
            try
            {
                object context = GetWorldContext(player);
                if (context == null)
                {
                    CustomCollectedFrom[tileKey] = collectible;
                    return;
                }

                FieldInfo field = AccessTools.Field(context.GetType(), "CollectedFrom");
                if (field == null)
                {
                    CustomCollectedFrom[tileKey] = collectible;
                    return;
                }

                IDictionary<string, Collectible> collectedFrom = field.GetValue(context) as IDictionary<string, Collectible>;
                if (collectedFrom != null)
                {
                    collectedFrom[tileKey] = collectible;
                }
                else
                {
                    CustomCollectedFrom[tileKey] = collectible;
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("SetCollectedFrom failed: " + ex.Message);
            }
        }

        internal static void SetGenerators(object player, List<Generator> generators)
        {
            try
            {
                FieldInfo field = AccessTools.Field(player.GetType(), "_generators");
                if (field != null)
                {
                    field.SetValue(player, generators);
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("SetGenerators failed: " + ex.Message);
            }
        }

        private static object GetWorldContext(object player)
        {
            FieldInfo worldField = AccessTools.Field(player.GetType(), "_world");
            if (worldField == null)
            {
                return null;
            }

            object world = worldField.GetValue(player);
            if (world == null)
            {
                return null;
            }

            FieldInfo contextField = AccessTools.Field(world.GetType(), "_context");
            if (contextField == null)
            {
                return null;
            }

            return contextField.GetValue(world);
        }
    }

    internal static class DatePalmXpBridge
    {
        private static bool _warnedMissingApi;

        internal static void AddLevelXp(int amount)
        {
            try
            {
                Type apiType = AccessTools.TypeByName("BaoX.DurangoOriginal.PlayerProgressionMod.PlayerProgressionApi");
                if (apiType == null)
                {
                    WarnMissingApi();
                    return;
                }

                MethodInfo method = apiType.GetMethod("AddExperience", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    WarnMissingApi();
                    return;
                }

                object[] args = new object[] { amount, null };
                bool ok = (bool)method.Invoke(null, args);
                if (!ok && args.Length > 1 && args[1] != null)
                {
                    GatheringPlugin.Log.LogWarning("XP reward failed: " + args[1]);
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("XP reward failed: " + ex.Message);
            }
        }

        private static void WarnMissingApi()
        {
            if (_warnedMissingApi)
            {
                return;
            }

            _warnedMissingApi = true;
            GatheringPlugin.Log.LogWarning("PlayerProgressionApi not found; Date Palm XP reward disabled");
        }
    }

    internal static class KylloxPatches
    {
        public static void GeneratorPostfix(BiomeSpriteInfo biomeSpriteInfo, ref List<Generator> __result)
        {
            if (biomeSpriteInfo == null || !DatePalmGatheringData.IsDatePalm(biomeSpriteInfo.CollectibleId))
            {
                return;
            }
            __result = DatePalmGatheringData.CreateGenerators(1);
        }

        public static void HandleTouchNaturalPostfix(object __instance, Messages.Touch touch, BiomeSpriteInfo biomeSpriteInfo, ref Touched __result)
        {
            if (biomeSpriteInfo == null && !DatePalmGatheringData.IsDatePalm(__result.Collectible.CollectibleId))
            {
                return;
            }
            if (biomeSpriteInfo != null && !DatePalmGatheringData.IsDatePalm(biomeSpriteInfo.CollectibleId))
            {
                return;
            }

            if (DatePalmGatheringData.HasOnlyDatePalmGenerators(__result.Collectible.Generators))
            {
                return;
            }

            List<Generator> generators = DatePalmGatheringData.CreateGenerators(1);
            Collectible collectible = __result.Collectible;
            collectible.CollectibleId = DatePalmGatheringData.CollectibleId;
            collectible.Generators = generators.ToArray();
            __result.Collectible = collectible;

            DatePalmReflection.SetGenerators(__instance, generators);
            DatePalmReflection.SetCollectedFrom(__instance, touch.Tile.ToString(), collectible);
        }

        public static void CollectNaturalPrefix(object __instance, Collect msg)
        {
            Collectible? collectible = DatePalmReflection.GetCollectedFrom(__instance, msg.Tile.ToString());
            if (collectible == null || !DatePalmGatheringData.IsDatePalm(collectible.Value.CollectibleId))
            {
                DatePalmCollectState.Mark(__instance, null);
                return;
            }

            DatePalmCollectState.Mark(__instance, msg.GeneratorId);
        }

        public static void SendCollectedPostfix(object __instance, List<Item> list, Result result)
        {
            string generatorId = DatePalmCollectState.Consume(__instance);
            if (!DatePalmGatheringData.IsRewardGenerator(generatorId))
            {
                return;
            }

            if (result != Result.Success && result != Result.GreatSuccess)
            {
                return;
            }

            DatePalmXpBridge.AddLevelXp(DatePalmGatheringData.XpReward);
        }
    }

    internal static class ClientPatches
    {
        private static bool _isNaturallyEnding = false;

        public static void OnCollectedPrefix()
        {
            _isNaturallyEnding = true;
        }

        public static void OnCollectedPostfix()
        {
            _isNaturallyEnding = false;
        }

        public static void set_CurrentGatheringDataPrefix(object __instance, object value)
        {
            try
            {
                if (_isNaturallyEnding)
                {
                    return;
                }

                if (value != null)
                {
                    return;
                }

                System.Reflection.FieldInfo dataField = AccessTools.Field(__instance.GetType(), "_currentGatheringData");
                if (dataField == null)
                {
                    return;
                }

                object currentData = dataField.GetValue(__instance);
                if (currentData == null)
                {
                    return;
                }

                GatheringPlugin.Log.LogInfo("[Client] Gathering cancelled by client. Sending Canceled to server.");
                Connections.Frontend.Send<Messages.Canceled>(new Messages.Canceled(), false, 0U);
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in set_CurrentGatheringDataPrefix: " + ex);
            }
        }
    }

    internal static class OriginalPatches
    {
        public static void ConstructorPostfix(Durango.Offline.Player __instance, Durango.Offline.Connection connection, Durango.Offline.World world)
        {
            try
            {
                connection.Recv<Messages.Collect>(delegate(Messages.Collect msg, Durango.Network.PacketHeader header)
                {
                    HandleCollectOriginal(__instance, world, msg, header);
                });

                connection.Recv<GetCollectible>(delegate(GetCollectible msg, Durango.Network.PacketHeader header)
                {
                    HandleGetCollectibleOriginal(__instance, msg, header);
                });

                // Server receives Canceled (TypeCode 2038) from client when player moves mid-gather.
                // Cancel the active System.Timers.Timer for this player.
                connection.Recv<Messages.Canceled>(delegate(Messages.Canceled msg, Durango.Network.PacketHeader header)
                {
                    try
                    {
                        GatheringPlugin.Log.LogInfo("[Server] Received Canceled from client — cancelling active harvest session.");
                        DatePalmCollectState.CancelSession(__instance);
                    }
                    catch (Exception ex)
                    {
                        GatheringPlugin.Log.LogError("Error handling Canceled on server: " + ex);
                    }
                });
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Failed to register Collect/GetCollectible handlers in ConstructorPostfix: " + ex);
            }
        }

        private static void HandleGetCollectibleOriginal(Durango.Offline.Player player, GetCollectible msg, Durango.Network.PacketHeader header)
        {
            try
            {
                Collectible? col = DatePalmReflection.GetCollectedFrom(player, msg.Tile.ToString());
                if (col != null && DatePalmGatheringData.IsDatePalm(col.Value.CollectibleId))
                {
                    player.Send<Collectible>(col.Value, header.Seq);
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in HandleGetCollectibleOriginal: " + ex);
            }
        }

        public static bool HandleTouchMsgPrefix(Durango.Offline.Player __instance, Messages.Touch touch, uint seq)
        {
            try
            {
                if (touch.EntityType == 0)
                {
                    return true;
                }

                if (DataHelper.IsNaturalObject((int)touch.EntityType))
                {
                    BiomeSpriteInfo biomeSpriteInfo = DataHelper.GetBiomeSpriteInfo((int)touch.EntityType);
                    if (biomeSpriteInfo != null && DatePalmGatheringData.IsDatePalm(biomeSpriteInfo.CollectibleId))
                    {
                        Touched msg = default(Touched);
                        msg.EntityId = touch.EntityId;
                        msg.EntityName = biomeSpriteInfo.Name;
                        msg.Level = 1;
                        msg.PrototypeId = string.Empty;

                        bool flag = GameManager.ClusterMode == Durango.Logic.Clusters.Mode.Editable;
                        if (flag)
                        {
                            msg.Interactions = new int[]
                            {
                                (int)InteractionData.Interaction.Collect
                            };

                            Collectible collectible = default(Collectible);
                            collectible.EntityId = touch.EntityId;
                            collectible.CollectibleId = DatePalmGatheringData.CollectibleId;

                            Collectible? cachedCollectible = DatePalmReflection.GetCollectedFrom(__instance, touch.Tile.ToString());
                            if (cachedCollectible != null && DatePalmGatheringData.IsDatePalm(cachedCollectible.Value.CollectibleId))
                            {
                                collectible = cachedCollectible.Value;
                            }
                            else
                            {
                                List<Generator> generators = DatePalmGatheringData.CreateGenerators(1);
                                collectible.Generators = generators.ToArray();
                                DatePalmReflection.SetCollectedFrom(__instance, touch.Tile.ToString(), collectible);
                            }

                            msg.Collectible = collectible;
                        }
                        else
                        {
                            msg.Interactions = new int[0];
                        }

                        msg.DisabledInteractions = new int[0];
                        msg.AccessDeniedInteractions = new int[0];

                        __instance.Send<Touched>(msg, seq);

                        MethodInfo onContextChanged = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (onContextChanged != null)
                        {
                            onContextChanged.Invoke(__instance, null);
                        }

                        return false; // Bypass original touch handler for date palms
                    }
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in HandleTouchMsgPrefix: " + ex);
            }

            return true;
        }

        private static void HandleCollectOriginal(Durango.Offline.Player player, Durango.Offline.World world, Messages.Collect msg, Durango.Network.PacketHeader header)
        {
            try
            {
                GatheringPlugin.Log.LogInfo("[Collect] start gen=" + msg.GeneratorId + " tile=" + msg.Tile);
                Collectible? col = DatePalmReflection.GetCollectedFrom(player, msg.Tile.ToString());
                if (col == null || !DatePalmGatheringData.IsDatePalm(col.Value.CollectibleId))
                {
                    return;
                }

                DatePalmCollectState.Mark(player, msg.GeneratorId);

                List<Item> list = new List<Item>();
                Result result = Result.Invalid;
                int roll = new System.Random().Next(1, 100);
                if (roll < 5) result = Result.BigFailure;
                else if (roll < 10) result = Result.Failure;
                else if (roll < 85) result = Result.Success;
                else result = Result.GreatSuccess;

                if (roll >= 5)
                {
                    string actualProtoId = msg.GeneratorId;
                    if (actualProtoId == "date")
                    {
                        actualProtoId = "fruit";
                    }
                    Item? item = Durango.Offline.Cheats.MakeItem(actualProtoId, msg.Level);
                    if (item != null)
                    {
                        Item itemVal = item.Value;
                        if (msg.GeneratorId == "date")
                        {
                            itemVal.Icon = "icon_nat_fruit_date";
                        }
                        if (col.Value.Generators != null)
                        {
                            int genIndex = Array.FindIndex(col.Value.Generators, delegate(Generator o) { return o.Id == msg.GeneratorId; });
                            if (genIndex != -1)
                            {
                                itemVal.Name = col.Value.Generators[genIndex].Name;
                            }
                        }
                        list.Add(itemVal);
                    }
                }

                player.Send<Messages.Timer>(new Messages.Timer
                {
                    Duration = 2f
                }, header.Seq);

                var delayTimer = new System.Timers.Timer
                {
                    Interval = 2000.0,
                    Enabled = true,
                    AutoReset = false
                };

                DatePalmCollectState.RegisterSession(player, delayTimer, header.Seq);

                delayTimer.Elapsed += delegate(object sender, System.Timers.ElapsedEventArgs args)
                {
                    try
                    {
                        GatheringPlugin.Log.LogInfo("[Elapsed] fired gen=" + msg.GeneratorId + " tile=" + msg.Tile);
                        if (!DatePalmCollectState.IsActiveTimer(player, delayTimer))
                        {
                            GatheringPlugin.Log.LogInfo("Timer session inactive, aborting.");
                            delayTimer.Dispose();
                            return;
                        }

                        DatePalmCollectState.RemoveSession(player, delayTimer);
                        Collectible? currentCol = DatePalmReflection.GetCollectedFrom(player, msg.Tile.ToString());
                        if (currentCol == null)
                        {
                            GatheringPlugin.Log.LogError("Collectible state vanished.");
                            delayTimer.Dispose();
                            return;
                        }

                        if (roll >= 5 && list.Count > 0)
                        {
                            player.AddItems(list);
                            player.Send<InventoryUpdated>(new InventoryUpdated
                            {
                                EntityId = player.EntityId,
                                Items = list.ToArray()
                            }, 0U);
                        }

                        List<Generator> genList = new List<Generator>();
                        if (currentCol.Value.Generators != null)
                        {
                            genList.AddRange(currentCol.Value.Generators);
                        }

                        int index = genList.FindIndex(delegate(Generator o) { return o.Id == msg.GeneratorId; });
                        if (index != -1)
                        {
                            Generator gen = genList[index];
                            gen.Amount = gen.Amount - 1;
                            if (gen.Amount <= 0)
                            {
                                genList.RemoveAt(index);
                            }
                            else
                            {
                                genList[index] = gen;
                            }
                        }

                        bool ranOut = (genList.Count == 0);
                        if (ranOut)
                        {
                            world.DestroyNatural(msg.Tile);
                        }

                        Collectible updatedCol = currentCol.Value;
                        updatedCol.Generators = genList.ToArray();
                        DatePalmReflection.SetCollectedFrom(player, msg.Tile.ToString(), updatedCol);

                        Messages.CollectibleChanged changed = default(Messages.CollectibleChanged);
                        changed.EntityId = msg.EntityId;
                        player.Send<Messages.CollectibleChanged>(changed, 0U);

                        Messages.Collected collected = default(Messages.Collected);
                        collected.Items = list.ToArray();
                        collected.Result = result;
                        collected.RanOut = ranOut;
                        player.Send<Messages.Collected>(collected, header.Seq);

                        string consumedGenerator = DatePalmCollectState.Consume(player);
                        if (DatePalmGatheringData.IsRewardGenerator(consumedGenerator) && (result == Result.Success || result == Result.GreatSuccess))
                        {
                            DatePalmXpBridge.AddLevelXp(DatePalmGatheringData.XpReward);
                        }

                        world.Save();
                        delayTimer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        GatheringPlugin.Log.LogError("Error in HandleCollectOriginal timer: " + ex);
                    }
                };
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in HandleCollectOriginal: " + ex);
            }
        }


    }
}
