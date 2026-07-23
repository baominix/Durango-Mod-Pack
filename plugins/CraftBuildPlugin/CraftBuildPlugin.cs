using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Network;
using Durango.Offline;
using Durango.Terrain;
using HarmonyLib;
using InteractionData;
using Messages;
using Newtonsoft.Json;
using Shared.Item;
using Yaml;
using System.Timers;

namespace BaoX.DurangoOriginal.CraftBuildMod
{
    [BepInPlugin("baox.durango.original.craftbuild", "CraftBuildPlugin", "0.1.0")]
    public sealed class CraftBuildPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private Harmony _harmony;

        // Custom storage dictionaries for persistence in offline mode
        public static Dictionary<string, List<Item>> BoxInventories = new Dictionary<string, List<Item>>();
        public static Dictionary<string, Dictionary<int, Item>> AddOnsMap = new Dictionary<string, Dictionary<int, Item>>();

        private static string _boxSavePath;
        private static string _addonSavePath;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony("baox.durango.original.craftbuild");

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
                    HandleEstimateCraft(__instance, msg, header.Seq);
                });

                // 4. Craft
                connection.Recv<Craft>(delegate(Craft msg, PacketHeader header)
                {
                    HandleCraft(__instance, msg, header.Seq);
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

                // 8. GetAddOns
                connection.Recv<GetAddOns>(delegate(GetAddOns msg, PacketHeader header)
                {
                    HandleGetAddOns(__instance, msg, header.Seq);
                });

                // 9. PlaceAddOns
                connection.Recv<PlaceAddOns>(delegate(PlaceAddOns msg, PacketHeader header)
                {
                    HandlePlaceAddOns(__instance, msg, header.Seq);
                });
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

                        // Always allow demolish in offline mode
                        list.Add(Interaction.DestructArtifact);

                        if (Array.IndexOf(blueprint.Components, "Washable") != -1)
                        {
                            list.Add(Interaction.Wash);
                        }
                        if (Array.IndexOf(blueprint.Components, "Inventory") != -1)
                        {
                            list.Add(Interaction.Inventory);
                            list.Add(Interaction.BrokenInventory);
                            list.Add(Interaction.RenameArtifact);
                        }
                        if (Array.IndexOf(blueprint.Components, "Shelter") != -1)
                        {
                            list.Add(Interaction.Rest);
                        }
                        if (Array.IndexOf(blueprint.Components, "Home") != -1)
                        {
                            list.Add(Interaction.SetAsHome);
                        }
                        if (Array.IndexOf(blueprint.Components, "Growable") != -1)
                        {
                            list.Add(Interaction.Plant);
                            list.Add(Interaction.Fertilize);
                            list.Add(Interaction.Watering);
                            list.Add(Interaction.Uproot);
                        }
                        if (Array.IndexOf(blueprint.Components, "Modular") != -1)
                        {
                            list.Add(Interaction.AddOnManage);
                            list.Add(Interaction.RemodelArtifact);
                        }
                        if (Array.IndexOf(blueprint.Components, "Scribble") != -1)
                        {
                            list.Add(Interaction.ScribbleDrawing);
                            list.Add(Interaction.ScribbleText);
                        }
                        if (Array.IndexOf(blueprint.Components, "Workbench") != -1)
                        {
                            list.Add(Interaction.Craft);
                        }
                        if (Array.IndexOf(blueprint.Components, "Gate") != -1)
                        {
                            var worldField = typeof(Durango.Offline.Player).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance);
                            Durango.Offline.World world = worldField.GetValue(__instance) as Durango.Offline.World;
                            AppearArtifact? appearArtifact = world.ArtifactManager.Get(touch.EntityId);
                            if (appearArtifact != null)
                            {
                                list.Add((!appearArtifact.Value.States.GateOpened) ? Interaction.OpenGate : Interaction.CloseGate);
                            }
                        }
                        if (Array.IndexOf(blueprint.Components, "Mannequin") != -1)
                        {
                            list.Add(Interaction.ChangeMannequinHead);
                            list.Add(Interaction.ChangeMannequinBody);
                        }

                        // The Original client opens Tamed Island Pioneer Rank from the
                        // Personal Communication Station. The offline touch backend did
                        // not restore this blueprint-specific interaction.
                        if (blueprint.Id == "operating_office_01")
                        {
                            list.Add(Interaction.ManagePioneerGrade);
                        }

                        if (blueprint.Id.StartsWith("living_tech_", StringComparison.Ordinal) ||
                            blueprint.Id.StartsWith("light_tech_", StringComparison.Ordinal) ||
                            blueprint.Id.StartsWith("heavy_tech_", StringComparison.Ordinal))
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
                List<string> recipeIds = new List<string>();
                var recipeSystem = GameSystem<RecipeSystem>.Instance();
                if (recipeSystem != null && recipeSystem.RecipeContainer != null)
                {
                    foreach (var category in recipeSystem.RecipeContainer.Categories)
                    {
                        foreach (var recipe in category.Recipes)
                        {
                            recipeIds.Add(recipe.Id);
                        }
                    }
                }

                player.Send<Recipes>(new Recipes
                {
                    Ids = recipeIds.ToArray()
                }, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleGetRecipes: " + ex);
            }
        }

        private static void HandleGetArtifactBlueprints(Durango.Offline.Player player, GetArtifactBlueprints msg, uint seq)
        {
            try
            {
                List<string> bpIds = new List<string>();
                var recipeSystem = GameSystem<RecipeSystem>.Instance();
                if (recipeSystem != null && recipeSystem.RecipeContainer != null)
                {
                    foreach (var bp in recipeSystem.RecipeContainer.GetAllBlueprints())
                    {
                        bpIds.Add(bp.Id);
                    }
                }

                player.Send<ArtifactBlueprints>(new ArtifactBlueprints
                {
                    Ids = bpIds.ToArray()
                }, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleGetArtifactBlueprints: " + ex);
            }
        }

        private static void HandleEstimateCraft(Durango.Offline.Player player, EstimateCraft msg, uint seq)
        {
            try
            {
                Yaml.Recipe recipe = Yaml.RecipeDict.Get(msg.RecipeId, null);
                string prototypeId = (recipe != null) ? recipe.prototype_id : "stone_axe";
                int level = (recipe != null) ? recipe.min_level : 1;

                string name = "Item";
                Prototype itemProto = PrototypeYaml.GetItemPrototype(prototypeId);
                if (itemProto != null)
                {
                    name = itemProto.Name;
                }

                CraftEstimation estimation = new CraftEstimation
                {
                    PrototypeId = prototypeId,
                    Level = level,
                    Name = name,
                    Durability = new UnityEngine.Vector2(100f, 100f),
                    Tags = new Dictionary<string, int>(),
                    UnrevealedRareTagCount = 0,
                    ModifiableCount = 3,
                    SuccessRate = 1.0f,
                    GreatSuccessRate = 0.1f,
                    RequiredAbilityValue = 0.0f
                };

                CraftEstimationInfo info = new CraftEstimationInfo
                {
                    CraftLevel = level,
                    CraftEstimation = new CraftEstimation?(estimation)
                };

                player.Send<CraftEstimationInfo>(info, seq);
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleEstimateCraft: " + ex);
            }
        }

        private static void HandleCraft(Durango.Offline.Player player, Craft msg, uint seq)
        {
            try
            {
                Yaml.Recipe recipe = Yaml.RecipeDict.Get(msg.RecipeId, null);
                string prototypeId = (recipe != null) ? recipe.prototype_id : "stone_axe";
                int level = (recipe != null) ? recipe.min_level : 1;

                // Send Timer to trigger client-side progress bar
                player.Send<Messages.Timer>(new Messages.Timer
                {
                    Duration = 2f
                }, seq);

                System.Timers.Timer timer = new System.Timers.Timer(2000.0);
                timer.AutoReset = false;
                timer.Enabled = true;
                timer.Elapsed += delegate(object sender, ElapsedEventArgs e)
                {
                    try
                    {
                        timer.Stop();
                        timer.Dispose();

                        Item? craftedItem = Durango.Offline.Cheats.MakeItem(prototypeId, level);
                        if (craftedItem != null)
                        {
                            var contextField = typeof(Durango.Offline.Player).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
                            Durango.Offline.PlayerContext context = contextField.GetValue(player) as Durango.Offline.PlayerContext;

                            context.InventoryItems.Add(craftedItem.Value);

                            player.Send<InventoryUpdated>(new InventoryUpdated
                            {
                                EntityId = player.EntityId,
                                Items = new Item[] { craftedItem.Value }
                            }, 0U);

                            player.Send<Crafted>(new Crafted
                            {
                                Result = Result.Success,
                                ActionInfo = default(Messages.ActionInfo),
                                Items = new Item[] { craftedItem.Value }
                            }, 0U);

                            player.Send<OK>(default(OK), seq);

                            var onContextChanged = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (onContextChanged != null)
                            {
                                onContextChanged.Invoke(player, null);
                            }
                            context.Save();
                        }
                        else
                        {
                            player.Send<Abort>(default(Abort), seq);
                        }
                    }
                    catch (Exception ex)
                    {
                        CraftBuildPlugin.Log.LogError("Error in HandleCraft timer callback: " + ex);
                        player.Send<Abort>(default(Abort), seq);
                    }
                };
            }
            catch (Exception ex)
            {
                CraftBuildPlugin.Log.LogError("Error in HandleCraft: " + ex);
                player.Send<Abort>(default(Abort), seq);
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
                foreach (string itemId in msg.ItemIds)
                {
                    int foundIdx = boxItems.FindIndex((Item o) => o.Id == itemId);
                    if (foundIdx != -1)
                    {
                        Item item = boxItems[foundIdx];
                        context.InventoryItems.Add(item);
                        boxItems.RemoveAt(foundIdx);
                        removedIds.Add(itemId);
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
                        Items = context.InventoryItems.ToArray()
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
                CraftBuildPlugin.Log.LogError("Error in HandleTakeOutItem: " + ex);
                player.Send<Abort>(default(Abort), seq);
            }
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
