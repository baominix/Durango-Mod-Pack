using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Network;
using Durango.Offline;
using Durango.Terrain;
using Durango.Utils;
using HarmonyLib;
using InteractionData;
using Messages;
using Newtonsoft.Json;
using Shared.Item;
using Yaml;
using System.Timers;

namespace BaoX.DurangoOriginal.CraftBuildMod
{
    [BepInPlugin("com.baominix.durango.original.craftbuild", "CraftBuildPlugin", "0.4.8")]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class CraftBuildPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private Harmony _harmony;
        private static readonly object ScheduledSync = new object();
        private static readonly List<ScheduledAction> ScheduledActions = new List<ScheduledAction>();

        private sealed class ScheduledAction
        {
            internal float DueAt;
            internal Action Action;
            internal bool Cancelled;
        }

        // Custom storage dictionaries for persistence in offline mode
        public static Dictionary<string, List<Item>> BoxInventories = new Dictionary<string, List<Item>>();
        public static Dictionary<string, Dictionary<int, Item>> AddOnsMap = new Dictionary<string, Dictionary<int, Item>>();

        private static string _boxSavePath;
        private static string _addonSavePath;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony("com.baominix.durango.original.craftbuild");

            _boxSavePath = Path.Combine(Paths.PluginPath, "CraftBuildPlugin/box_inventories.json");
            _addonSavePath = Path.Combine(Paths.PluginPath, "CraftBuildPlugin/addons.json");

            LoadData();

            Type playerType = AccessTools.TypeByName("Durango.Offline.Player");
            if (playerType == null)
            {
                Logger.LogError("Durango.Offline.Player class not found!");
                return;
            }

            var ctor = playerType.GetConstructor(new Type[] {
                typeof(string),
                typeof(Durango.Offline.Connection),
                typeof(Durango.Offline.World),
                typeof(Durango.Offline.PlayerContext),
                typeof(bool)
            });
            if (ctor != null)
            {
                _harmony.Patch(ctor, null, new HarmonyMethod(typeof(CraftBuildPatches).GetMethod("ConstructorPostfix")), null, null, null);
                Logger.LogInfo("Successfully patched Player constructor.");
            }
            else
            {
                Logger.LogError("Player constructor not found!");
            }

            var handleTouchMsgMethod = playerType.GetMethod("HandleTouchMsg", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (handleTouchMsgMethod != null)
            {
                _harmony.Patch(handleTouchMsgMethod, new HarmonyMethod(typeof(CraftBuildPatches).GetMethod("HandleTouchMsgPrefix")), null, null, null, null);
                Logger.LogInfo("Successfully patched HandleTouchMsg.");
            }
            else
            {
                Logger.LogError("HandleTouchMsg method not found!");
            }

            var handleDumpItemsMsgMethod = playerType.GetMethod("HandleDumpItemsMsg", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (handleDumpItemsMsgMethod != null)
            {
                _harmony.Patch(handleDumpItemsMsgMethod, new HarmonyMethod(typeof(CraftBuildPatches).GetMethod("HandleDumpItemsMsgPrefix")), null, null, null, null);
                Logger.LogInfo("Successfully patched HandleDumpItemsMsg.");
            }
            else
            {
                Logger.LogError("HandleDumpItemsMsg method not found!");
            }

            var handleDestructMsgMethod = playerType.GetMethod("HandleDestructMsg", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (handleDestructMsgMethod != null)
            {
                _harmony.Patch(handleDestructMsgMethod, null, new HarmonyMethod(typeof(CraftBuildPatches).GetMethod("HandleDestructMsgPostfix")), null, null, null);
                Logger.LogInfo("Successfully patched HandleDestructMsg.");
            }
            else
            {
                Logger.LogError("HandleDestructMsg method not found!");
            }

            _harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        internal static object Schedule(float delaySeconds, Action action)
        {
            if (action == null)
            {
                return null;
            }

            ScheduledAction scheduled = new ScheduledAction
            {
                DueAt = UnityEngine.Time.realtimeSinceStartup + Math.Max(0f, delaySeconds),
                Action = action
            };
            lock (ScheduledSync)
            {
                ScheduledActions.Add(scheduled);
            }
            return scheduled;
        }

        internal static void CancelScheduled(object token)
        {
            ScheduledAction scheduled = token as ScheduledAction;
            if (scheduled == null)
            {
                return;
            }
            lock (ScheduledSync)
            {
                scheduled.Cancelled = true;
                ScheduledActions.Remove(scheduled);
            }
        }

        private void Update()
        {
            List<Action> due = null;
            float now = UnityEngine.Time.realtimeSinceStartup;
            lock (ScheduledSync)
            {
                for (int i = ScheduledActions.Count - 1; i >= 0; i--)
                {
                    if (ScheduledActions[i].Cancelled)
                    {
                        ScheduledActions.RemoveAt(i);
                        continue;
                    }
                    if (ScheduledActions[i].DueAt > now)
                    {
                        continue;
                    }

                    if (due == null)
                    {
                        due = new List<Action>();
                    }
                    due.Add(ScheduledActions[i].Action);
                    ScheduledActions.RemoveAt(i);
                }
            }

            if (due == null)
            {
                return;
            }

            for (int i = due.Count - 1; i >= 0; i--)
            {
                try
                {
                    due[i]();
                }
                catch (Exception ex)
                {
                    Log.LogError("Scheduled craft/build action failed: " + ex);
                }
            }
        }

        public static void LoadData()
        {
            try
            {
                string dir = Path.GetDirectoryName(_boxSavePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (File.Exists(_boxSavePath))
                {
                    string json = File.ReadAllText(_boxSavePath);
                    BoxInventories = JsonConvert.DeserializeObject<Dictionary<string, List<Item>>>(json) ?? new Dictionary<string, List<Item>>();
                    Log.LogInfo("Loaded " + BoxInventories.Count + " chest inventories.");
                }

                if (File.Exists(_addonSavePath))
                {
                    string json = File.ReadAllText(_addonSavePath);
                    AddOnsMap = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, Item>>>(json) ?? new Dictionary<string, Dictionary<int, Item>>();
                    Log.LogInfo("Loaded " + AddOnsMap.Count + " house addon layouts.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to load persistence data: " + ex);
            }
        }

        public static void SaveData()
        {
            try
            {
                string dir = Path.GetDirectoryName(_boxSavePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(_boxSavePath, JsonConvert.SerializeObject(BoxInventories, Formatting.Indented));
                File.WriteAllText(_addonSavePath, JsonConvert.SerializeObject(AddOnsMap, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to save persistence data: " + ex);
            }
        }
    }

    internal static class CraftBuildPatches
    {
        public static void ConstructorPostfix(Durango.Offline.Player __instance, Durango.Offline.Connection connection, Durango.Offline.World world)
        {
            try
            {
                // 1. GetRecipes
                connection.Recv<GetRecipes>(delegate(GetRecipes msg, PacketHeader header)
                {
                    HandleGetRecipes(__instance, msg, header.Seq);
                });

                // 2. GetArtifactBlueprints
                connection.Recv<GetArtifactBlueprints>(delegate(GetArtifactBlueprints msg, PacketHeader header)
                {
                    HandleGetArtifactBlueprints(__instance, msg, header.Seq);
                });

                // 3. EstimateCraft
                connection.Recv<EstimateCraft>(delegate(EstimateCraft msg, PacketHeader header)
                {
                    CraftBuildBackend.HandleEstimateCraft(__instance, msg, header.Seq);
                });

                // 4. Craft
                connection.Recv<Craft>(delegate(Craft msg, PacketHeader header)
                {
                    CraftBuildBackend.HandleCraft(__instance, msg, header.Seq);
                });

                // 5. GetInventory
                connection.Recv<GetInventory>(delegate(GetInventory msg, PacketHeader header)
                {
                    HandleGetInventory(__instance, msg, header.Seq);
                });

                // 6. PutInItem
                connection.Recv<PutInItem>(delegate(PutInItem msg, PacketHeader header)
                {
                    HandlePutInItem(__instance, msg, header.Seq);
                });

                // 7. TakeOutItem
                connection.Recv<TakeOutItem>(delegate(TakeOutItem msg, PacketHeader header)
                {
                    HandleTakeOutItem(__instance, msg, header.Seq);
                });

                // The stock offline Player already owns GetAddOns/PlaceAddOns and
                // persists them in WorldContext. Do not shadow those handlers with
                // a second, disconnected add-on store.

                CraftBuildBackend.Register(__instance, connection);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Failed to register crafting and building connection handlers: " + ex);
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

                if (touch.EntityType < 10000)
                {
                    Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().RecipeContainer.GetBlueprint((int)touch.EntityType);
                    if (blueprint != null)
                    {
                        Touched msg = default(Touched);
                        msg.EntityId = touch.EntityId;
                        msg.EntityName = blueprint.Name;
                        List<Interaction> list = new List<Interaction>();

                        // Construction sites need to remain actionable after loading
                        // a saved world, not only immediately after Occupied is sent.
                        var buildWorldField = typeof(Durango.Offline.Player).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance);
                        Durango.Offline.World buildWorld = buildWorldField == null ? null : buildWorldField.GetValue(__instance) as Durango.Offline.World;
                        AppearArtifact? buildArtifact = buildWorld == null ? null : buildWorld.ArtifactManager.Get(touch.EntityId);
                        bool isConstructionSite = buildArtifact != null && buildArtifact.Value.States.BuildingState != Shared.Building.BuildingState.Completed;
                        bool acceptsBuildAction = buildArtifact != null && buildArtifact.Value.States.BuildingState == Shared.Building.BuildingState.Occupied;
                        bool acceptsCompleteAction = buildArtifact != null &&
                            buildArtifact.Value.States.BuildingState == Shared.Building.BuildingState.Built &&
                            (buildArtifact.Value.States.Postprocess == null ||
                             buildArtifact.Value.States.Postprocess.Value.EndsAt <= Times.UnixTimeNow());

                        bool isDispenser = Array.IndexOf(blueprint.Components, "Dispenser") != -1;
                        if (!blueprint.Permanent)
                        {
                            list.Add(Interaction.DestructArtifact);
                        }
                        if (acceptsBuildAction)
                        {
                            list.Add(Interaction.BuildArtifact);
                        }
                        if (acceptsCompleteAction)
                        {
                            list.Add(Interaction.CompleteArtifact);
                        }

                        // An occupied site is only a material/build target. Do not
                        // expose the completed artifact's inventory/workbench/etc.
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Washable") != -1)
                        {
                            list.Add(Interaction.Wash);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Inventory") != -1)
                        {
                            list.Add(Interaction.Inventory);
                            list.Add(Interaction.BrokenInventory);
                            list.Add(Interaction.RenameArtifact);
                        }
                        if (!isConstructionSite && isDispenser)
                        {
                            list.Add(Interaction.Take);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Shelter") != -1)
                        {
                            list.Add(Interaction.Rest);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Home") != -1)
                        {
                            list.Add(Interaction.SetAsHome);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Growable") != -1)
                        {
                            list.Add(Interaction.Plant);
                            list.Add(Interaction.Fertilize);
                            list.Add(Interaction.Watering);
                            list.Add(Interaction.Uproot);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Modular") != -1)
                        {
                            list.Add(Interaction.AddOnManage);
                            list.Add(Interaction.RemodelArtifact);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Scribble") != -1)
                        {
                            list.Add(Interaction.ScribbleDrawing);
                            list.Add(Interaction.ScribbleText);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Workbench") != -1)
                        {
                            list.Add(Interaction.Craft);
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Gate") != -1)
                        {
                            var worldField = typeof(Durango.Offline.Player).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance);
                            Durango.Offline.World world = worldField.GetValue(__instance) as Durango.Offline.World;
                            AppearArtifact? appearArtifact = world.ArtifactManager.Get(touch.EntityId);
                            if (appearArtifact != null)
                            {
                                list.Add((!appearArtifact.Value.States.GateOpened) ? Interaction.OpenGate : Interaction.CloseGate);
                            }
                        }
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Mannequin") != -1)
                        {
                            list.Add(Interaction.ChangeMannequinHead);
                            list.Add(Interaction.ChangeMannequinBody);
                        }

                        if (!isConstructionSite && CraftBuildBackend.CanCapsulate(blueprint))
                        {
                            list.Add(Interaction.Capsulate);
                        }

                        // The Original client opens Tamed Island Pioneer Rank from the
                        // Personal Communication Station. The offline touch backend did
                        // not restore this blueprint-specific interaction.
                        if (!isConstructionSite && blueprint.Id == "operating_office_01")
                        {
                            list.Add(Interaction.ManagePioneerGrade);
                        }

                        if (!isConstructionSite && (blueprint.Id.StartsWith("living_tech_", StringComparison.Ordinal) ||
                            blueprint.Id.StartsWith("light_tech_", StringComparison.Ordinal) ||
                            blueprint.Id.StartsWith("heavy_tech_", StringComparison.Ordinal)))
                        {
                            list.Add(Interaction.PersonalResearch);
                        }

                        List<int> intInteractions = new List<int>();
                        foreach (var interaction in list)
                        {
                            intInteractions.Add((int)interaction);
                        }
                        msg.Interactions = intInteractions.ToArray();
                        msg.DisabledInteractions = new int[0];
                        msg.AccessDeniedInteractions = new int[0];

                        var worldField2 = typeof(Durango.Offline.Player).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance);
                        Durango.Offline.World world2 = worldField2.GetValue(__instance) as Durango.Offline.World;
                        msg.Mannequin = world2.ArtifactManager.GetMannequin(touch.EntityId);
                        if (!isConstructionSite && Array.IndexOf(blueprint.Components, "Workbench") != -1)
                        {
                            msg.Workbench = CraftBuildBackend.GetWorkbenchSnapshot(touch.EntityId);
                        }

                        __instance.Send<Touched>(msg, seq);

                        var onContextChanged = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (onContextChanged != null)
                        {
                            onContextChanged.Invoke(__instance, null);
                        }

                        return false; // Bypass original touch handler for artifacts
                    }
                }
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleTouchMsgPrefix: " + ex);
            }

            return true;
        }

        private static void HandleGetRecipes(Durango.Offline.Player player, GetRecipes msg, uint seq)
        {
            try
            {
                CraftBuildBackend.SendRecipeAvailability(player, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleGetRecipes: " + ex);
            }
        }

        public static bool HandleDumpItemsMsgPrefix(Durango.Offline.Player __instance, DumpItems msg)
        {
            try
            {
                if (msg.ItemIds == null || msg.ItemIds.Length == 0)
                {
                    return false;
                }

                FieldInfo contextField = typeof(Durango.Offline.Player).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo worldField = typeof(Durango.Offline.Player).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance);
                PlayerContext context = contextField == null ? null : contextField.GetValue(__instance) as PlayerContext;
                Durango.Offline.World world = worldField == null ? null : worldField.GetValue(__instance) as Durango.Offline.World;
                if (context == null || world == null)
                {
                    CraftBuildPlugin.Log.LogError("Cannot drop items because the offline player context or world is unavailable.");
                    return false;
                }

                List<Item> sourceItems;
                string sourceEntityId;
                bool sourceIsPet = !string.IsNullOrEmpty(msg.SourcePetEntityId);
                if (sourceIsPet)
                {
                    sourceEntityId = msg.SourcePetEntityId;
                    if (!TryGetPetInventory(__instance, sourceEntityId, out sourceItems))
                    {
                        CraftBuildPlugin.Log.LogWarning("Pet inventory was not found; no item was removed: " + sourceEntityId);
                        return false;
                    }
                }
                else if (msg.SourceProp.HasValue)
                {
                    sourceEntityId = msg.SourceProp.Value.EntityId;
                    if (!CraftBuildPlugin.BoxInventories.TryGetValue(sourceEntityId, out sourceItems))
                    {
                        CraftBuildPlugin.Log.LogWarning("Drop source inventory was not found: " + sourceEntityId);
                        return false;
                    }
                }
                else
                {
                    sourceEntityId = __instance.EntityId;
                    sourceItems = context.InventoryItems;
                }

                HashSet<string> requestedIds = new HashSet<string>(msg.ItemIds, StringComparer.Ordinal);
                List<Item> removedItems = new List<Item>();
                for (int i = 0; i < sourceItems.Count; i++)
                {
                    if (requestedIds.Contains(sourceItems[i].Id))
                    {
                        removedItems.Add(sourceItems[i]);
                    }
                }
                if (removedItems.Count == 0)
                {
                    return false;
                }

                Point2 dropTile;
                int? dropFloor = msg.Floor;
                if (msg.Tile.HasValue)
                {
                    dropTile = msg.Tile.Value;
                }
                else if (!TryGetPlayerLocation(context, out dropTile, out dropFloor))
                {
                    CraftBuildPlugin.Log.LogWarning("Player position was unavailable; no item was removed.");
                    return false;
                }

                List<Item> recoverableItems = new List<Item>();
                for (int i = 0; i < removedItems.Count; i++)
                {
                    // The original UI warns that non-tradable items cannot be picked
                    // back up after discarding, so only tradable items enter a package.
                    if (removedItems[i].Tradable)
                    {
                        recoverableItems.Add(removedItems[i]);
                    }
                }

                AppearArtifact? package = null;
                if (recoverableItems.Count > 0)
                {
                    foreach (AppearArtifact candidate in world.ArtifactManager.Enumerable(delegate(AppearArtifact artifact)
                    {
                        return artifact.EntityType == 8000 &&
                            artifact.Tile.x == dropTile.x && artifact.Tile.y == dropTile.y &&
                            artifact.Floor == dropFloor && artifact.EntityId != sourceEntityId;
                    }))
                    {
                        package = candidate;
                        break;
                    }

                    if (!package.HasValue)
                    {
                        AddOns? addons;
                        AppearArtifact? made = Cheats.MakeAppearArtifact(new string[]
                        {
                            "prop",
                            "8000",
                            "position:" + dropTile.x + "," + dropTile.y,
                            "size:1,1"
                        }, out addons);
                        if (!made.HasValue)
                        {
                            CraftBuildPlugin.Log.LogError("The package blueprint (entity type 8000) could not be created; no item was removed.");
                            return false;
                        }

                        AppearArtifact created = made.Value;
                        created.Tile = dropTile;
                        created.Floor = dropFloor;
                        created.FounderEntityId = __instance.EntityId;
                        created.IsAlive = true;
                        created.States.EntityId = created.EntityId;
                        world.ConstructArtifact(created, null);
                        CraftBuildPlugin.BoxInventories[created.EntityId] = new List<Item>();
                        package = created;
                    }

                    List<Item> packageItems;
                    if (!CraftBuildPlugin.BoxInventories.TryGetValue(package.Value.EntityId, out packageItems))
                    {
                        packageItems = new List<Item>();
                        CraftBuildPlugin.BoxInventories[package.Value.EntityId] = packageItems;
                    }
                    packageItems.AddRange(recoverableItems);
                }

                HashSet<string> removedIds = new HashSet<string>(removedItems.ConvertAll(delegate(Item item) { return item.Id; }), StringComparer.Ordinal);
                sourceItems.RemoveAll(delegate(Item item) { return removedIds.Contains(item.Id); });

                if (sourceIsPet)
                {
                    SavePetInventory(__instance, sourceEntityId, sourceItems.Count);
                }

                __instance.Send<InventoryUpdated>(new InventoryUpdated
                {
                    EntityId = sourceEntityId,
                    RemovedItemIds = new List<string>(removedIds).ToArray()
                }, 0U);

                MethodInfo onContextChanged = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (onContextChanged != null)
                {
                    onContextChanged.Invoke(__instance, null);
                }
                context.Save();
                CraftBuildPlugin.SaveData();
                CraftBuildPlugin.Log.LogInfo("Dropped " + removedItems.Count + " item(s) at " + dropTile.x + "," + dropTile.y +
                    (recoverableItems.Count == 0 ? "; all were non-tradable." : "; package=" + package.Value.EntityId + "."));
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleDumpItemsMsg: " + ex);
            }
            return false;
        }

        public static void HandleDestructMsgPostfix(DestructArtifact msg)
        {
            try
            {
                if (!string.IsNullOrEmpty(msg.EntityId) && CraftBuildPlugin.BoxInventories.Remove(msg.EntityId))
                {
                    CraftBuildPlugin.SaveData();
                }
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Could not remove the destroyed artifact inventory: " + ex);
            }
        }

        private static bool TryGetPetInventory(Durango.Offline.Player player, string petId, out List<Item> items)
        {
            items = null;
            try
            {
                Type pluginType = AccessTools.TypeByName("AnimalHandlingPlugin.AnimalHandlingPlugin");
                MethodInfo getData = pluginType == null ? null : pluginType.GetMethod("GetData", BindingFlags.Static | BindingFlags.NonPublic);
                object data = getData == null ? null : getData.Invoke(null, new object[] { player });
                FieldInfo inventoriesField = data == null ? null : data.GetType().GetField("PetInventories", BindingFlags.Instance | BindingFlags.Public);
                Dictionary<string, List<Item>> inventories = inventoriesField == null
                    ? null
                    : inventoriesField.GetValue(data) as Dictionary<string, List<Item>>;
                return inventories != null && inventories.TryGetValue(petId, out items);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Could not read the pet inventory: " + ex);
                return false;
            }
        }

        private static void SavePetInventory(Durango.Offline.Player player, string petId, int itemCount)
        {
            try
            {
                Type pluginType = AccessTools.TypeByName("AnimalHandlingPlugin.AnimalHandlingPlugin");
                if (pluginType == null)
                {
                    return;
                }

                MethodInfo updateUsage = pluginType.GetMethod("UpdatePetInventoryUsage", BindingFlags.Static | BindingFlags.NonPublic);
                if (updateUsage != null)
                {
                    updateUsage.Invoke(null, new object[] { player, petId, itemCount });
                }

                MethodInfo saveData = pluginType.GetMethod("SaveData", BindingFlags.Static | BindingFlags.NonPublic);
                if (saveData != null)
                {
                    saveData.Invoke(null, new object[] { player });
                }
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Could not save the pet inventory after dropping items: " + ex);
            }
        }

        private static bool TryGetPlayerLocation(PlayerContext context, out Point2 tile, out int? floor)
        {
            tile = default(Point2);
            floor = null;
            Movement[] movements = context.AppearPlayer.Move.Movements;
            if (movements == null || movements.Length == 0)
            {
                return false;
            }

            Movement movement = movements[movements.Length - 1];
            if (movement.Path == null || movement.Path.Length == 0)
            {
                return false;
            }

            Location location = movement.Path[movement.Path.Length - 1];
            tile = new Point2((int)Math.Floor(location.Position.x / 200f), (int)Math.Floor(location.Position.y / 200f));
            // Outdoor artifacts use a null floor in the saved WorldContext. The
            // movement packet reports zero even outdoors, so do not turn that
            // sentinel into an indoor floor for the ordinary "Discard" action.
            floor = null;
            return true;
        }

        private static void HandleGetArtifactBlueprints(Durango.Offline.Player player, GetArtifactBlueprints msg, uint seq)
        {
            try
            {
                CraftBuildBackend.SendBlueprintAvailability(player, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleGetArtifactBlueprints: " + ex);
            }
        }

        private static void HandleGetInventory(Durango.Offline.Player player, GetInventory msg, uint seq)
        {
            try
            {
                if (msg.Target == null)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                string entityId = msg.Target.Value.EntityId;
                List<Item> boxItems;
                if (!CraftBuildPlugin.BoxInventories.TryGetValue(entityId, out boxItems))
                {
                    boxItems = new List<Item>();
                    CraftBuildPlugin.BoxInventories[entityId] = boxItems;
                }

                Inventory reply = default(Inventory);
                reply.EntityId = entityId;
                reply.InventoryInfos.EntityId = entityId;
                reply.InventoryItems.EntityId = entityId;
                reply.InventoryInfos.MaxSize = 200;
                reply.InventoryItems.Items = boxItems.ToArray();

                player.Send<Inventory>(reply, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleGetInventory: " + ex);
            }
        }

        private static void HandlePutInItem(Durango.Offline.Player player, PutInItem msg, uint seq)
        {
            try
            {
                var contextField = typeof(Durango.Offline.Player).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
                Durango.Offline.PlayerContext context = contextField.GetValue(player) as Durango.Offline.PlayerContext;

                List<Item> boxItems;
                if (!CraftBuildPlugin.BoxInventories.TryGetValue(msg.EntityId, out boxItems))
                {
                    boxItems = new List<Item>();
                    CraftBuildPlugin.BoxInventories[msg.EntityId] = boxItems;
                }

                List<string> addedIds = new List<string>();
                foreach (string itemId in msg.ItemIds)
                {
                    int foundIdx = context.InventoryItems.FindIndex((Item o) => o.Id == itemId);
                    if (foundIdx != -1)
                    {
                        Item item = context.InventoryItems[foundIdx];
                        boxItems.Add(item);
                        context.InventoryItems.RemoveAt(foundIdx);
                        addedIds.Add(itemId);
                    }
                }

                if (addedIds.Count > 0)
                {
                    player.Send<InventoryUpdated>(new InventoryUpdated
                    {
                        EntityId = player.EntityId,
                        RemovedItemIds = addedIds.ToArray()
                    }, 0U);

                    player.Send<InventoryUpdated>(new InventoryUpdated
                    {
                        EntityId = msg.EntityId,
                        Items = boxItems.ToArray()
                    }, 0U);

                    var onContextChanged = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (onContextChanged != null)
                    {
                        onContextChanged.Invoke(player, null);
                    }
                    context.Save();
                    CraftBuildPlugin.SaveData();
                }

                player.Send<OK>(default(OK), seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandlePutInItem: " + ex);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleTakeOutItem(Durango.Offline.Player player, TakeOutItem msg, uint seq)
        {
            try
            {
                var contextField = typeof(Durango.Offline.Player).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
                Durango.Offline.PlayerContext context = contextField.GetValue(player) as Durango.Offline.PlayerContext;

                List<Item> boxItems;
                if (!CraftBuildPlugin.BoxInventories.TryGetValue(msg.EntityId, out boxItems))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                List<string> removedIds = new List<string>();
                List<Item> pickedUpItems = new List<Item>();
                foreach (string itemId in msg.ItemIds)
                {
                    int foundIdx = boxItems.FindIndex((Item o) => o.Id == itemId);
                    if (foundIdx != -1)
                    {
                        Item item = boxItems[foundIdx];
                        context.InventoryItems.Add(item);
                        boxItems.RemoveAt(foundIdx);
                        removedIds.Add(itemId);
                        pickedUpItems.Add(item);
                    }
                }

                if (removedIds.Count > 0)
                {
                    player.Send<InventoryUpdated>(new InventoryUpdated
                    {
                        EntityId = msg.EntityId,
                        RemovedItemIds = removedIds.ToArray()
                    }, 0U);

                    player.Send<InventoryUpdated>(new InventoryUpdated
                    {
                        EntityId = player.EntityId,
                        // InventoryUpdated is a delta message. Sending the complete
                        // bag makes the client process every existing item again.
                        Items = pickedUpItems.ToArray()
                    }, 0U);

                    var onContextChanged = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (onContextChanged != null)
                    {
                        onContextChanged.Invoke(player, null);
                    }
                    context.Save();

                    FieldInfo worldField = typeof(Durango.Offline.Player).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance);
                    Durango.Offline.World world = worldField == null ? null : worldField.GetValue(player) as Durango.Offline.World;
                    if (boxItems.Count == 0 && IsDispenserPackage(world, msg.EntityId))
                    {
                        CraftBuildPlugin.BoxInventories.Remove(msg.EntityId);
                        world.DestructArtifact(msg.EntityId);
                    }
                    CraftBuildPlugin.SaveData();
                }

                player.Send<OK>(default(OK), seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleTakeOutItem: " + ex);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static bool IsDispenserPackage(Durango.Offline.World world, string entityId)
        {
            if (world == null || string.IsNullOrEmpty(entityId))
            {
                return false;
            }

            AppearArtifact? artifact = world.ArtifactManager.Get(entityId);
            if (!artifact.HasValue)
            {
                return false;
            }

            Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().RecipeContainer.GetBlueprint(artifact.Value.EntityType);
            return blueprint != null && Array.IndexOf(blueprint.Components, "Dispenser") != -1;
        }

        private static void HandleGetAddOns(Durango.Offline.Player player, GetAddOns msg, uint seq)
        {
            try
            {
                Dictionary<int, Item> addonLayout;
                if (!CraftBuildPlugin.AddOnsMap.TryGetValue(msg.EntityId, out addonLayout))
                {
                    addonLayout = new Dictionary<int, Item>();
                }

                player.Send<AddOns>(new AddOns
                {
                    _AddOns = addonLayout
                }, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleGetAddOns: " + ex);
            }
        }

        private static void HandlePlaceAddOns(Durango.Offline.Player player, PlaceAddOns msg, uint seq)
        {
            try
            {
                var contextField = typeof(Durango.Offline.Player).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
                Durango.Offline.PlayerContext context = contextField.GetValue(player) as Durango.Offline.PlayerContext;

                Dictionary<int, Item> addonLayout;
                if (!CraftBuildPlugin.AddOnsMap.TryGetValue(msg.EntityId, out addonLayout))
                {
                    addonLayout = new Dictionary<int, Item>();
                    CraftBuildPlugin.AddOnsMap[msg.EntityId] = addonLayout;
                }

                List<string> removedItemIds = new List<string>();
                foreach (var placement in msg.AddOnPlacements)
                {
                    int slotIndex = placement.Key;
                    string itemId = placement.Value;

                    int foundIdx = context.InventoryItems.FindIndex((Item o) => o.Id == itemId);
                    if (foundIdx != -1)
                    {
                        Item item = context.InventoryItems[foundIdx];
                        addonLayout[slotIndex] = item;
                        context.InventoryItems.RemoveAt(foundIdx);
                        removedItemIds.Add(itemId);
                    }
                }

                if (removedItemIds.Count > 0)
                {
                    player.Send<InventoryUpdated>(new InventoryUpdated
                    {
                        EntityId = player.EntityId,
                        RemovedItemIds = removedItemIds.ToArray()
                    }, 0U);

                    var onContextChanged = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (onContextChanged != null)
                    {
                        onContextChanged.Invoke(player, null);
                    }
                    context.Save();
                    CraftBuildPlugin.SaveData();
                }

                player.Send<AddOns>(new AddOns
                {
                    _AddOns = addonLayout
                }, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandlePlaceAddOns: " + ex);
            }
        }
}
}
