using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Crafting;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.Network;
using Durango.Offline;
using Durango.System;
using Durango.UI;
using HarmonyLib;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Building;
using Shared.Etc;
using Shared.Item;
using UnityEngine;
using Yaml;
using Durango.Utils;
using Shared.Economy;

namespace BaoX.DurangoOriginal.CraftBuildMod
{
    internal static class CraftBuildMode
    {
        internal const string PreferenceKey = "baox_select_game_mode";
        internal const string CreativeKey = "free_offline";
        internal const string SurvivalKey = "single_multi_offline";

        internal static bool IsCreative
        {
            get
            {
                string mode = Preferences.GetString(PreferenceKey, string.Empty, Preferences.Level.Device);
                if (string.Equals(mode, CreativeKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (string.Equals(mode, SurvivalKey, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return GameManager.ClusterMode == Mode.Editable;
            }
        }
    }

    internal static class CraftBuildBackend
    {
        private const float CraftDuration = 2f;
        private const float BuildDuration = 2.5f;
        private const float CapsulateDuration = 0.5f;
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo ContextField = typeof(Durango.Offline.Player).GetField("_context", InstanceFlags);
        private static readonly FieldInfo WorldField = typeof(Durango.Offline.Player).GetField("_world", InstanceFlags);
        private static readonly FieldInfo ArtifactsField = typeof(Durango.Offline.ArtifactManager).GetField("_artifacts", InstanceFlags);
        private static readonly MethodInfo ContextChangedMethod = typeof(Durango.Offline.Player).GetMethod("OnContextChanged", InstanceFlags);
        private static readonly object Sync = new object();
        private static readonly HashSet<uint> TimedReplySequences = new HashSet<uint>();
        private static readonly Dictionary<string, object> ActiveDestructActions = new Dictionary<string, object>(StringComparer.Ordinal);
        private static MethodInfo _skillUnlockMethod;
        private static Durango.Offline.Player _localPlayer;
        private static bool _skillApiMissingLogged;
        private static bool _serverDataLoaded;
        private static Dictionary<string, JObject> _blueprintServerData;
        private static Dictionary<string, JObject> _artifactServerData;
        private static JObject _clanServerData;
        private static JObject _pioneerServerData;

        private static Dictionary<string, Dictionary<string, List<Item>>> _siteMaterials = new Dictionary<string, Dictionary<string, List<Item>>>();
        private static Dictionary<string, ArtifactDisplay> _siteCompletedDisplays = new Dictionary<string, ArtifactDisplay>();
        private static readonly HashSet<string> LikedRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LikedBlueprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        private static string SiteMaterialsPath
        {
            get { return Path.Combine(Paths.PluginPath, "CraftBuildPlugin/build_site_materials.json"); }
        }

        private static string SiteDisplaysPath
        {
            get { return Path.Combine(Paths.PluginPath, "CraftBuildPlugin/build_site_displays.json"); }
        }

        internal static void Register(Durango.Offline.Player player, Durango.Offline.Connection connection)
        {
            Load();

            if (player.IsLocalPlayer)
            {
                _localPlayer = player;
                EnsureServerDataLoaded();
                RepairLegacyCompletedDisplays(player);
                RepairLegacyWorkbenchTags(player);
                RepairLegacySkewerItems(player);
                player.Closed += delegate()
                {
                    if (_localPlayer == player)
                    {
                        CancelTimedAction(player.EntityId, "destruct");
                        _localPlayer = null;
                    }
                };
            }

            connection.Recv<OccupyArtifactSite>(delegate(OccupyArtifactSite msg, PacketHeader header) { HandleOccupyArtifactSite(player, msg, header.Seq); });
            connection.Recv<GetArtifact>(delegate(GetArtifact msg, PacketHeader header) { HandleGetArtifact(player, msg, header.Seq); });
            connection.Recv<PutMaterialsIntoArtifact>(delegate(PutMaterialsIntoArtifact msg, PacketHeader header) { HandlePutMaterials(player, msg, header.Seq); });
            connection.Recv<EstimateBuild>(delegate(EstimateBuild msg, PacketHeader header) { HandleEstimateBuild(player, msg, header.Seq); });
            connection.Recv<BuildArtifact>(delegate(BuildArtifact msg, PacketHeader header) { HandleBuildArtifact(player, msg, header.Seq); });
            connection.Recv<EstimateRemodeling>(delegate(EstimateRemodeling msg, PacketHeader header) { HandleEstimateRemodeling(player, msg, header.Seq); });
            connection.Recv<RemodelArtifact>(delegate(RemodelArtifact msg, PacketHeader header) { HandleRemodelArtifact(player, msg, header.Seq); });
            connection.Recv<GetWorkbench>(delegate(GetWorkbench msg, PacketHeader header) { HandleGetWorkbench(player, msg, header.Seq); });
            connection.Recv<CancelCrafting>(delegate(CancelCrafting msg, PacketHeader header) { player.Send<OK>(default(OK), header.Seq); });
            connection.Recv<SkipEntrustedCraft>(delegate(SkipEntrustedCraft msg, PacketHeader header) { player.Send<OK>(default(OK), header.Seq); });
            connection.Recv<SetRecipeLike>(delegate(SetRecipeLike msg, PacketHeader header) { HandleSetRecipeLike(player, msg, header.Seq); });
            connection.Recv<SetBlueprintLike>(delegate(SetBlueprintLike msg, PacketHeader header) { HandleSetBlueprintLike(player, msg, header.Seq); });
            connection.Recv<CompleteArtifact>(delegate(CompleteArtifact msg, PacketHeader header) { HandleCompleteArtifact(player, msg, header.Seq); });
            connection.Recv<DestructArtifact>(delegate(DestructArtifact msg, PacketHeader header) { HandleDestructArtifact(player, msg, header.Seq); });
            connection.Recv<GetCapsulatingCost>(delegate(GetCapsulatingCost msg, PacketHeader header) { HandleGetCapsulatingCost(player, msg, header.Seq); });
            connection.Recv<CapsulateArtifact>(delegate(CapsulateArtifact msg, PacketHeader header) { HandleCapsulateArtifact(player, msg, header.Seq); });
            connection.Recv<SkipPostprocess>(delegate(SkipPostprocess msg, PacketHeader header) { HandleSkipPostprocess(player, msg, header.Seq); });

            CraftBuildPlugin.Log.LogInfo("Registered restored craft/build backend (mode=" + (CraftBuildMode.IsCreative ? "creative" : "survival") + ").");
        }

        private static void EnsureServerDataLoaded()
        {
            if (_serverDataLoaded)
            {
                return;
            }
            _serverDataLoaded = true;
            try
            {
                _blueprintServerData = Durango.Utils.Json.ReadFromFile<Dictionary<string, JObject>>(
                    "offline/assets/building/blueprints");
                _artifactServerData = Durango.Utils.Json.ReadFromFile<Dictionary<string, JObject>>(
                    "offline/assets/entity_types/artifact");
                _clanServerData = Durango.Utils.Json.ReadFromFile<JObject>("offline/assets/clan");
                _pioneerServerData = Durango.Utils.Json.ReadFromFile<JObject>("offline/assets/pioneer");
                CraftBuildPlugin.Log.LogInfo("Loaded original build metadata: blueprints=" +
                    ((_blueprintServerData == null) ? 0 : _blueprintServerData.Count) + ", artifacts=" +
                    ((_artifactServerData == null) ? 0 : _artifactServerData.Count) +
                    ", clan=" + ((_clanServerData == null) ? 0 : _clanServerData.Count) +
                    ", pioneer=" + ((_pioneerServerData == null) ? 0 : _pioneerServerData.Count) + ".");
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogWarning("Could not load original build metadata: " + exception.Message);
                _blueprintServerData = new Dictionary<string, JObject>();
                _artifactServerData = new Dictionary<string, JObject>();
                _clanServerData = new JObject();
                _pioneerServerData = new JObject();
            }
        }

        private static JObject GetBlueprintServerData(Building.Blueprint blueprint)
        {
            EnsureServerDataLoaded();
            JObject data;
            return blueprint != null && _blueprintServerData != null &&
                _blueprintServerData.TryGetValue(blueprint.Id, out data) ? data : null;
        }

        private static JObject GetArtifactServerData(Building.Blueprint blueprint)
        {
            EnsureServerDataLoaded();
            JObject data;
            return blueprint != null && _artifactServerData != null &&
                _artifactServerData.TryGetValue(blueprint.EntityType.ToString(), out data) ? data : null;
        }

        private static bool ReadBool(JObject data, string name, bool fallback)
        {
            if (data == null)
            {
                return fallback;
            }
            JToken value;
            return data.TryGetValue(name, out value) && value != null ? value.Value<bool>() : fallback;
        }

        private static int ReadInt(JObject data, string name, int fallback)
        {
            if (data == null)
            {
                return fallback;
            }
            JToken value;
            return data.TryGetValue(name, out value) && value != null ? value.Value<int>() : fallback;
        }

        private static string ReadString(JObject data, string name, string fallback)
        {
            if (data == null)
            {
                return fallback;
            }
            JToken value;
            string result = data.TryGetValue(name, out value) && value != null ? value.Value<string>() : null;
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        internal static bool CanCapsulate(Building.Blueprint blueprint)
        {
            return blueprint != null && !blueprint.Permanent &&
                ReadBool(GetArtifactServerData(blueprint), "capsulizable", false);
        }

        private static int GetPostprocessHelperMax(Building.Blueprint blueprint)
        {
            return Math.Max(0, ReadInt(GetBlueprintServerData(blueprint), "postprocess_helper_max", 0));
        }

        private static string GetCapsulePrototypeId(Building.Blueprint blueprint)
        {
            return ReadString(GetArtifactServerData(blueprint), "capsule_prototype_id", "artifact_capsule");
        }

        internal static void SendRecipeAvailability(Durango.Offline.Player player, uint seq)
        {
            string[] ids = GetRecipeIds();
            player.Send<Recipes>(new Recipes
            {
                Ids = ids,
                LikedRecipeIds = FilterIds(LikedRecipes, ids),
                NewRecipeIds = new string[0]
            }, seq);
        }

        internal static void SendBlueprintAvailability(Durango.Offline.Player player, uint seq)
        {
            string[] ids = GetBlueprintIds();
            player.Send<ArtifactBlueprints>(new ArtifactBlueprints
            {
                Ids = ids,
                LikedBlueprintIds = FilterIds(LikedBlueprints, ids),
                NewBlueprintIds = new string[0]
            }, seq);
        }

        internal static void RefreshLocalAvailability()
        {
            Durango.Offline.Player player = _localPlayer;
            if (player == null)
            {
                return;
            }

            SendRecipeAvailability(player, 0U);
            SendBlueprintAvailability(player, 0U);
            if (CraftBuildPlugin.Log != null)
            {
                CraftBuildPlugin.Log.LogInfo("Craft/build availability refreshed (mode=" +
                    (CraftBuildMode.IsCreative ? "creative" : "survival") + ").");
            }
        }

        internal static void HandleEstimateCraft(Durango.Offline.Player player, EstimateCraft msg, uint seq)
        {
            try
            {
                Yaml.Recipe recipe = Yaml.RecipeDict.Get(msg.RecipeId, null);
                Crafting.Recipe runtimeRecipe = GameSystem<RecipeSystem>.Instance().GetRecipe(msg.RecipeId);
                if (recipe == null || runtimeRecipe == null || !IsRecipeAllowed(msg.RecipeId) ||
                    !IsValidWorkbench(player, recipe, msg.Workbench))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                List<Item> materials;
                bool found = TryResolveItems(GetContext(player), msg.Materials, out materials, null);
                if (!CraftBuildMode.IsCreative && (!found ||
                    !ValidateRecipeMaterials(GetContext(player), runtimeRecipe, msg.Materials) ||
                    !ValidateRecipeTool(GetContext(player), runtimeRecipe, msg.ToolItemId)))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                string prototypeId = ResolveCraftPrototypeId(msg.RecipeId, recipe, materials);
                if (string.IsNullOrEmpty(prototypeId))
                {
                    CraftBuildPlugin.Log.LogWarning("Craft estimation rejected because result prototype could not be resolved: recipe=" + msg.RecipeId + ".");
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }
                int level = CalculateLevel(recipe.min_level, recipe.max_level, materials);
                Prototype prototype = PrototypeYaml.GetItemPrototype(prototypeId);
                string itemName = prototypeId;
                Dictionary<string, int> estimatedTags = new Dictionary<string, int>(StringComparer.Ordinal);
                int estimatedModifiableCount = 0;
                Item? previewItem = Cheats.MakeItem(prototypeId, level);
                if (previewItem != null)
                {
                    Item preview = previewItem.Value;
                    NormalizeCraftedItem(msg.RecipeId, ref preview);
                    itemName = string.IsNullOrEmpty(preview.Name) ? prototypeId : preview.Name;
                    estimatedModifiableCount = preview.ModifiableCount;
                    if (preview.Tags != null)
                    {
                        for (int i = 0; i < preview.Tags.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(preview.Tags[i].Id))
                            {
                                estimatedTags[preview.Tags[i].Id] = preview.Tags[i].Level;
                            }
                        }
                    }
                }
                else if (prototype != null)
                {
                    itemName = prototype.Name;
                }
                CraftEstimation estimation = new CraftEstimation
                {
                    PrototypeId = prototypeId,
                    Level = level,
                    Name = itemName,
                    Durability = new Vector2(100f, 100f),
                    Tags = estimatedTags,
                    UnrevealedRareTagCount = 0,
                    ModifiableCount = estimatedModifiableCount,
                    SuccessRate = 1f,
                    GreatSuccessRate = 0f,
                    RequiredAbilityValue = runtimeRecipe.RequiredAbility.HasValue
                        ? GameSystem<StatisticsSystem>.Instance().GetDeriveds(runtimeRecipe.RequiredAbility.Value, 0f)
                        : 0f
                };
                player.Send<CraftEstimationInfo>(new CraftEstimationInfo
                {
                    CraftLevel = level,
                    CraftEstimation = new CraftEstimation?(estimation)
                }, seq);
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("EstimateCraft failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        internal static void HandleCraft(Durango.Offline.Player player, Craft msg, uint seq)
        {
            try
            {
                Yaml.Recipe recipe = Yaml.RecipeDict.Get(msg.RecipeId, null);
                Crafting.Recipe runtimeRecipe = GameSystem<RecipeSystem>.Instance().GetRecipe(msg.RecipeId);
                Durango.Offline.PlayerContext context = GetContext(player);
                if (recipe == null || runtimeRecipe == null || context == null || !IsRecipeAllowed(msg.RecipeId) ||
                    !IsValidWorkbench(player, recipe, msg.Workbench))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                List<Item> materials;
                List<string> materialIds = new List<string>();
                bool found = TryResolveItems(context, msg.Materials, out materials, materialIds);
                if (!CraftBuildMode.IsCreative)
                {
                    if (!found || !ValidateRecipeMaterials(context, runtimeRecipe, msg.Materials) ||
                        !ValidateRecipeTool(context, runtimeRecipe, msg.ToolItemId))
                    {
                        player.Send<Abort>(default(Abort), seq);
                        return;
                    }
                }

                string prototypeId = ResolveCraftPrototypeId(msg.RecipeId, recipe, materials);
                if (string.IsNullOrEmpty(prototypeId))
                {
                    CraftBuildPlugin.Log.LogWarning("Craft rejected because result prototype could not be resolved: recipe=" + msg.RecipeId + ".");
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }
                int level = CalculateLevel(recipe.min_level, recipe.max_level, materials);
                int count = Math.Max(1, recipe.count);
                List<Item> crafted = new List<Item>();
                for (int i = 0; i < count; i++)
                {
                    Item? item = Cheats.MakeItem(prototypeId, level);
                    if (item == null)
                    {
                        CraftBuildPlugin.Log.LogWarning("Craft rejected because result prototype does not exist: recipe=" +
                            msg.RecipeId + ", prototype=" + prototypeId + ".");
                        player.Send<Abort>(default(Abort), seq);
                        return;
                    }
                    Item craftedItem = item.Value;
                    NormalizeCraftedItem(msg.RecipeId, ref craftedItem);
                    crafted.Add(craftedItem);
                }

                if (!CraftBuildMode.IsCreative)
                {
                    RemoveInventoryItems(context, materialIds);
                }
                context.InventoryItems.AddRange(crafted);

                bool creative = CraftBuildMode.IsCreative;
                float craftDuration = Mathf.Max(0.1f, runtimeRecipe.DurationWait);
                BeginTimedReply(seq);
                player.Send<Messages.Timer>(new Messages.Timer { Duration = craftDuration }, seq);
                CraftBuildPlugin.Log.LogInfo("Craft started: recipe=" + msg.RecipeId + ", prototype=" + prototypeId +
                    ", level=" + level + ", count=" + crafted.Count + ", duration=" + craftDuration + "s.");
                CraftBuildPlugin.Schedule(craftDuration, delegate
                {
                    player.Send<InventoryUpdated>(new InventoryUpdated
                    {
                        EntityId = player.EntityId,
                        Items = crafted.ToArray(),
                        RemovedItemIds = creative ? new string[0] : materialIds.ToArray()
                    }, 0U);
                    player.Send<Crafted>(new Crafted
                    {
                        Result = Result.Success,
                        ActionInfo = default(ActionInfo),
                        Items = crafted.ToArray()
                    }, seq);
                    CraftBuildPlugin.Log.LogInfo("Craft completed and delivered: recipe=" + msg.RecipeId +
                        ", prototype=" + prototypeId + ", count=" + crafted.Count + ".");
                });
                SavePlayer(context, player);
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("Craft failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleOccupyArtifactSite(Durango.Offline.Player player, OccupyArtifactSite msg, uint seq)
        {
            try
            {
                RecipeSystem recipeSystem = GameSystem<RecipeSystem>.Instance();
                Building.Blueprint blueprint = recipeSystem == null ? null : recipeSystem.GetBlueprint(msg.BlueprintId);
                Durango.Offline.World world = GetWorld(player);
                if (blueprint == null || world == null || blueprint.EntityType <= 0 || !IsBlueprintAllowed(blueprint.Id))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                string[] arguments = BuildCheatArguments(blueprint, msg);
                AddOns? addons;
                AppearArtifact? made = Cheats.MakeAppearArtifact(arguments, out addons);
                if (made == null)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                AppearArtifact artifact = made.Value;
                ArtifactDisplay completedDisplay = artifact.Display;
                artifact.Tile = msg.Tile;
                artifact.Size = (msg.Size.x > 0 && msg.Size.y > 0) ? msg.Size : blueprint.Size;
                artifact.Floor = msg.Floor;
                artifact.Stories = msg.Stories;
                artifact.Rotation = msg.Rotation;
                artifact.FounderEntityId = player.EntityId;
                artifact.IsAlive = true;
                ArtifactDisplay siteDisplay = default(ArtifactDisplay);
                siteDisplay.EntityId = artifact.EntityId;
                artifact.Display = siteDisplay;
                artifact.States.EntityId = artifact.EntityId;
                artifact.States.BuildingState = BuildingState.Occupied;
                artifact.States.Durability = FullGauge();

                lock (Sync)
                {
                    _siteMaterials[artifact.EntityId] = NewMaterialMap(blueprint);
                    completedDisplay.EntityId = artifact.EntityId;
                    _siteCompletedDisplays[artifact.EntityId] = completedDisplay;
                    Save();
                }

                float occupyDuration = 2f + Math.Max(1, artifact.Size.x * artifact.Size.y);
                BeginTimedReply(seq);
                player.Send<Messages.Timer>(new Messages.Timer { Duration = occupyDuration }, seq);
                CraftBuildPlugin.Log.LogInfo("Occupy started: entity=" + artifact.EntityId + ", blueprint=" + blueprint.Id +
                    ", duration=" + occupyDuration + "s.");
                CraftBuildPlugin.Schedule(occupyDuration, delegate
                {
                    try
                    {
                        world.ConstructArtifact(artifact, null);
                        player.Send<Occupied>(new Occupied
                        {
                            EntityId = artifact.EntityId,
                            TileX = artifact.Tile.x,
                            TileY = artifact.Tile.y,
                            Floor = artifact.Floor
                        }, seq);
                        CraftBuildPlugin.Log.LogInfo("Occupy completed: entity=" + artifact.EntityId + ", blueprint=" + blueprint.Id + ".");
                    }
                    catch (Exception exception)
                    {
                        CraftBuildPlugin.Log.LogError("OccupyArtifactSite completion failed: " + exception);
                        player.Send<Abort>(default(Abort), seq);
                    }
                });
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("OccupyArtifactSite failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleGetArtifact(Durango.Offline.Player player, GetArtifact msg, uint seq)
        {
            player.Send<ArtifactMaterials>(MakeArtifactMaterials(msg.EntityId), seq);
        }

        private static void HandlePutMaterials(Durango.Offline.Player player, PutMaterialsIntoArtifact msg, uint seq)
        {
            try
            {
                Durango.Offline.PlayerContext context = GetContext(player);
                Durango.Offline.World world = GetWorld(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(msg.EntityId);
                Building.Blueprint blueprint = found == null ? null : GameSystem<RecipeSystem>.Instance().GetBlueprint((int)found.Value.EntityType);
                if (context == null || world == null || found == null || blueprint == null || !IsBlueprintAllowed(blueprint.Id))
                {
                    CraftBuildPlugin.Log.LogWarning("Put materials rejected: entity=" + msg.EntityId +
                        ", context=" + (context != null) + ", world=" + (world != null) + ", artifact=" + (found != null) +
                        ", blueprint=" + ((blueprint == null) ? "<null>" : blueprint.Id) + ".");
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                List<Item> items;
                List<string> ids = new List<string>();
                if (!CraftBuildMode.IsCreative && !TryResolveItems(context, msg.Materials, out items, ids))
                {
                    CraftBuildPlugin.Log.LogWarning("Put materials rejected because one or more selected inventory items were missing: entity=" +
                        msg.EntityId + ", blueprint=" + blueprint.Id + ".");
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                lock (Sync)
                {
                    Dictionary<string, List<Item>> slots;
                    if (!_siteMaterials.TryGetValue(msg.EntityId, out slots))
                    {
                        slots = new Dictionary<string, List<Item>>();
                        _siteMaterials[msg.EntityId] = slots;
                    }
                    if (!CraftBuildMode.IsCreative)
                    {
                        foreach (KeyValuePair<string, string[]> pair in msg.Materials)
                        {
                            List<Item> slotItems;
                            if (!slots.TryGetValue(pair.Key, out slotItems))
                            {
                                slotItems = new List<Item>();
                                slots[pair.Key] = slotItems;
                            }
                            for (int i = 0; i < pair.Value.Length; i++)
                            {
                                Item selected;
                                if (TryGetInventoryItem(context, pair.Value[i], out selected))
                                {
                                    slotItems.Add(selected);
                                }
                            }
                        }
                        RemoveInventoryItems(context, ids);
                    }
                    Save();
                }

                if (!CraftBuildMode.IsCreative && ids.Count > 0)
                {
                    player.Send<InventoryUpdated>(new InventoryUpdated
                    {
                        EntityId = player.EntityId,
                        RemovedItemIds = ids.ToArray()
                    }, 0U);
                    SavePlayer(context, player);
                }
                player.Send<ArtifactMaterials>(MakeArtifactMaterials(msg.EntityId), 0U);
                player.Send<OK>(default(OK), seq);
                CraftBuildPlugin.Log.LogInfo("Materials accepted: entity=" + msg.EntityId + ", blueprint=" + blueprint.Id +
                    ", itemCount=" + ids.Count + ", mode=" + (CraftBuildMode.IsCreative ? "creative" : "survival") + ".");
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("PutMaterialsIntoArtifact failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleEstimateBuild(Durango.Offline.Player player, EstimateBuild msg, uint seq)
        {
            SendBuildEstimation(player, msg.EntityId, msg.Materials, seq);
        }

        private static void HandleEstimateRemodeling(Durango.Offline.Player player, EstimateRemodeling msg, uint seq)
        {
            SendBuildEstimation(player, msg.EntityId, msg.Materials, seq);
        }

        private static void SendBuildEstimation(Durango.Offline.Player player, string entityId, Dictionary<string, string[]> materials, uint seq)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(entityId);
                if (found == null)
                {
                    CraftBuildPlugin.Log.LogWarning("Build estimation rejected because construction site was not found: entity=" + entityId + ".");
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }
                AppearArtifact artifact = found.Value;
                Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().GetBlueprint((int)artifact.EntityType);
                if (blueprint == null || !IsBlueprintAllowed(blueprint.Id))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }
                List<Item> selected;
                TryResolveItems(GetContext(player), materials, out selected, null);
                int level = CalculateLevel(blueprint == null ? 1 : blueprint.MinLevel, blueprint == null ? 100 : blueprint.MaxLevel, selected);
                Dictionary<string, int> artifactTags = GetWorkbenchCapabilityTags(blueprint, level);
                ArtifactDisplay previewDisplay = artifact.Display;
                if (blueprint != null && artifact.States.BuildingState != BuildingState.Completed)
                {
                    previewDisplay = GetCompletedDisplay(artifact, blueprint);
                }
                player.Send<BuildEstimation>(new BuildEstimation
                {
                    Level = level,
                    Durability = 100f,
                    Tags = artifactTags,
                    UnrevealedRareTagCount = 0,
                    ArtifactPreview = new ArtifactPreview
                    {
                        Size = artifact.Size,
                        Rotation = artifact.Rotation,
                        Display = previewDisplay,
                        IsModular = blueprint != null && blueprint.IsModular
                    }
                }, seq);
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("Build estimation failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleBuildArtifact(Durango.Offline.Player player, BuildArtifact msg, uint seq)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(msg.EntityId);
                if (found == null)
                {
                    CraftBuildPlugin.Log.LogWarning("Build rejected because construction site was not found: entity=" + msg.EntityId + ".");
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }
                AppearArtifact artifact = found.Value;
                Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().GetBlueprint((int)artifact.EntityType);
                if (blueprint == null || !IsBlueprintAllowed(blueprint.Id) ||
                    (!CraftBuildMode.IsCreative && !HasAllBuildMaterials(artifact, blueprint)))
                {
                    CraftBuildPlugin.Log.LogWarning("Build rejected: entity=" + msg.EntityId + ", blueprint=" +
                        ((blueprint == null) ? "<null>" : blueprint.Id) + ", allowed=" +
                        (blueprint != null && IsBlueprintAllowed(blueprint.Id)) + ", materialsComplete=" +
                        (blueprint != null && HasAllBuildMaterials(artifact, blueprint)) + ", mode=" +
                        (CraftBuildMode.IsCreative ? "creative" : "survival") + ".");
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                BeginTimedReply(seq);
                player.Send<Messages.Timer>(new Messages.Timer { Duration = BuildDuration }, seq);
                CraftBuildPlugin.Log.LogInfo("Build started: entity=" + msg.EntityId + ", blueprint=" + blueprint.Id +
                    ", duration=" + BuildDuration + "s.");
                CraftBuildPlugin.Schedule(BuildDuration, delegate
                {
                    try
                    {
                        CompleteBuild(player, world, artifact, blueprint, msg.EntityId, seq);
                    }
                    catch (Exception exception)
                    {
                        CraftBuildPlugin.Log.LogError("BuildArtifact completion failed: " + exception);
                        player.Send<Abort>(default(Abort), seq);
                    }
                });
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("BuildArtifact failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleCompleteArtifact(Durango.Offline.Player player, CompleteArtifact msg, uint seq)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(msg.EntityId);
                if (found == null || found.Value.States.BuildingState != BuildingState.Built)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                AppearArtifact artifact = found.Value;
                Postprocess? postprocess = artifact.States.Postprocess;
                if (postprocess != null && postprocess.Value.EndsAt > Times.UnixTimeNow())
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                FinishPostprocess(player, world, artifact, seq);
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("CompleteArtifact failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleSkipPostprocess(Durango.Offline.Player player, SkipPostprocess msg, uint seq)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(msg.EntityId);
                if (found == null || found.Value.States.BuildingState != BuildingState.Built)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }
                FinishPostprocess(player, world, found.Value, seq);
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("SkipPostprocess failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleDestructArtifact(Durango.Offline.Player player, DestructArtifact msg, uint seq)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(msg.EntityId);
                if (found == null)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                AppearArtifact artifact = found.Value;
                Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().GetBlueprint((int)artifact.EntityType);
                if (blueprint == null || blueprint.Permanent)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                float durability = artifact.States.Durability == null ? 0f :
                    Math.Max(0f, artifact.States.Durability.Get(Times.UnixTimeNow()));
                float destructDuration = 5f + durability / 10f;
                player.Send<Destructing>(new Destructing { Duration = destructDuration, ToolType = 0 }, seq);
                CraftBuildPlugin.Log.LogInfo("Remove started: entity=" + msg.EntityId + ", blueprint=" + blueprint.Id +
                    ", duration=" + destructDuration + "s.");

                object scheduled = null;
                scheduled = CraftBuildPlugin.Schedule(destructDuration, delegate
                {
                    if (!ConsumeTimedAction(player.EntityId, "destruct", scheduled))
                    {
                        return;
                    }
                    try
                    {
                        if (world.ArtifactManager.Get(msg.EntityId) == null)
                        {
                            return;
                        }
                        world.DestructArtifact(msg.EntityId);
                        lock (Sync)
                        {
                            _siteMaterials.Remove(msg.EntityId);
                            _siteCompletedDisplays.Remove(msg.EntityId);
                            Save();
                        }
                        CraftBuildPlugin.Log.LogInfo("Artifact removed after destruct motion: entity=" + msg.EntityId + ".");
                    }
                    catch (Exception exception)
                    {
                        CraftBuildPlugin.Log.LogError("DestructArtifact completion failed: " + exception);
                        player.Send<Abort>(default(Abort), seq);
                    }
                });
                SetTimedAction(player.EntityId, "destruct", scheduled);
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("DestructArtifact failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleGetCapsulatingCost(Durango.Offline.Player player, GetCapsulatingCost msg, uint seq)
        {
            AppearArtifact? artifact = GetWorld(player) == null ? null : GetWorld(player).ArtifactManager.Get(msg.EntityId);
            Building.Blueprint blueprint = artifact == null ? null :
                GameSystem<RecipeSystem>.Instance().GetBlueprint((int)artifact.Value.EntityType);
            if (artifact == null || artifact.Value.States.BuildingState != BuildingState.Completed || !CanCapsulate(blueprint))
            {
                player.Send<Abort>(default(Abort), seq);
                return;
            }
            player.Send<Messages.Cost>(new Messages.Cost { Currency = Currency.TStone, Amount = 0L }, seq);
        }

        private static void HandleCapsulateArtifact(Durango.Offline.Player player, CapsulateArtifact msg, uint seq)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                Durango.Offline.PlayerContext context = GetContext(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(msg.EntityId);
                if (context == null || found == null || found.Value.States.BuildingState != BuildingState.Completed)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                AppearArtifact artifact = found.Value;
                Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().GetBlueprint((int)artifact.EntityType);
                Item? made = blueprint == null || !CanCapsulate(blueprint) ? null :
                    Cheats.MakeItem(GetCapsulePrototypeId(blueprint), Math.Max(1, (int)artifact.States.Level));
                if (made == null)
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                Item capsuleItem = made.Value;
                ArtifactCapsule capsule = default(ArtifactCapsule);
                capsule.EntityId = artifact.EntityId;
                capsule.BlueprintId = blueprint.Id;
                capsule.ArtifactLevel = Math.Max(1, (int)artifact.States.Level);
                capsule.Tags = artifact.Tags._Tags ?? new Messages.Tag[0];
                capsule.Performance = new Performance[0];
                capsule.Display = artifact.Display;
                capsule.State = artifact.States;
                capsule.State.Postprocess = null;
                capsule.LookNames = new Dictionary<string, string>();
                capsule.OccupySize = artifact.Size;
                capsuleItem.Ext = capsule;
                capsuleItem.Icon = blueprint.ArtifactIcon;
                capsuleItem.Name = blueprint.Name;

                BeginTimedReply(seq);
                player.Send<Messages.Timer>(new Messages.Timer { Duration = CapsulateDuration }, seq);
                CraftBuildPlugin.Schedule(CapsulateDuration, delegate
                {
                    try
                    {
                        context.InventoryItems.Add(capsuleItem);
                        world.DestructArtifact(artifact.EntityId);
                        SavePlayer(context, player);
                        player.Send<InventoryUpdated>(new InventoryUpdated
                        {
                            EntityId = player.EntityId,
                            Items = new Item[] { capsuleItem },
                            RemovedItemIds = new string[0]
                        }, 0U);
                        player.Send<ArtifactCapsulated>(new ArtifactCapsulated
                        {
                            Tile = artifact.Tile,
                            Floor = artifact.Floor,
                            Size = artifact.Size
                        }, 0U);
                        player.Send<OK>(default(OK), seq);
                        CraftBuildPlugin.Log.LogInfo("Artifact packaged: entity=" + artifact.EntityId + ", blueprint=" + blueprint.Id + ".");
                    }
                    catch (Exception exception)
                    {
                        CraftBuildPlugin.Log.LogError("CapsulateArtifact completion failed: " + exception);
                        player.Send<Abort>(default(Abort), seq);
                    }
                });
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("CapsulateArtifact failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleRemodelArtifact(Durango.Offline.Player player, RemodelArtifact msg, uint seq)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                Durango.Offline.PlayerContext context = GetContext(player);
                AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(msg.EntityId);
                Building.Blueprint current = found == null ? null : GameSystem<RecipeSystem>.Instance().GetBlueprint((int)found.Value.EntityType);
                Building.Blueprint target = current == null ? null : GameSystem<RecipeSystem>.Instance().RemodelingBlueprints.Get(current.Id, msg.SlotId);
                if (found == null || current == null || target == null || !IsBlueprintAllowed(current.Id))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }

                List<Item> selected;
                List<string> ids = new List<string>();
                if (!CraftBuildMode.IsCreative && !TryResolveItems(context, msg.Materials, out selected, ids))
                {
                    player.Send<Abort>(default(Abort), seq);
                    return;
                }
                if (!CraftBuildMode.IsCreative)
                {
                    RemoveInventoryItems(context, ids);
                }

                AppearArtifact artifact = found.Value;
                BeginTimedReply(seq);
                player.Send<Messages.Timer>(new Messages.Timer { Duration = BuildDuration }, seq);
                if (!CraftBuildMode.IsCreative && ids.Count > 0)
                {
                    player.Send<InventoryUpdated>(new InventoryUpdated { EntityId = player.EntityId, RemovedItemIds = ids.ToArray() }, 0U);
                    SavePlayer(context, player);
                }
                CraftBuildPlugin.Schedule(BuildDuration, delegate
                {
                    try
                    {
                        artifact.EntityType = (ushort)target.EntityType;
                        artifact.Display = CreateCompletedDisplay(target, artifact.EntityId);
                        artifact.States.BuildingState = BuildingState.Completed;
                        artifact.States.Durability = FullGauge();
                        UpdateArtifact(world, artifact);
                        player.Send<ArtifactDisplay>(artifact.Display, 0U);
                        player.Send<ArtifactState>(artifact.States, 0U);
                        player.Send<ArtifactCompleted>(new ArtifactCompleted { EntityId = artifact.EntityId }, 0U);
                        player.Send<OK>(default(OK), seq);
                    }
                    catch (Exception exception)
                    {
                        CraftBuildPlugin.Log.LogError("RemodelArtifact completion failed: " + exception);
                        player.Send<Abort>(default(Abort), seq);
                    }
                });
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogError("RemodelArtifact failed: " + exception);
                player.Send<Abort>(default(Abort), seq);
            }
        }

        private static void HandleGetWorkbench(Durango.Offline.Player player, GetWorkbench msg, uint seq)
        {
            player.Send<Workbench>(GetWorkbenchSnapshot(msg.EntityId), seq);
        }

        internal static Workbench GetWorkbenchSnapshot(string entityId)
        {
            return new Workbench
            {
                EntityId = entityId,
                Capacity = 3U,
                Craftings = new Messages.Crafting[0],
                Crafteds = new CraftedResult[0]
            };
        }

        private static void HandleSetRecipeLike(Durango.Offline.Player player, SetRecipeLike msg, uint seq)
        {
            if (msg.Like) LikedRecipes.Add(msg.RecipeId); else LikedRecipes.Remove(msg.RecipeId);
            SendRecipeAvailability(player, seq);
        }

        private static void HandleSetBlueprintLike(Durango.Offline.Player player, SetBlueprintLike msg, uint seq)
        {
            if (msg.Like) LikedBlueprints.Add(msg.BlueprintId); else LikedBlueprints.Remove(msg.BlueprintId);
            SendBlueprintAvailability(player, seq);
        }

        internal static string[] GetRecipeIds()
        {
            List<string> ids = new List<string>();
            RecipeSystem system = GameSystem<RecipeSystem>.Instance();
            UnlockState unlocks = null;
            bool hasSkillUnlocks = CraftBuildMode.IsCreative || TryGetSkillUnlockState(out unlocks);
            if (system != null && system.RecipeContainer != null)
            {
                foreach (Category category in system.RecipeContainer.Categories)
                {
                    foreach (Crafting.Recipe recipe in category.Recipes)
                    {
                        if (CraftBuildMode.IsCreative ||
                            (hasSkillUnlocks && IsSkillUnlocked(recipe.Id, unlocks.AllRecipes, unlocks.LearnedRecipes)))
                        {
                            AddUniqueId(ids, recipe.Id);
                        }
                    }
                }
            }
            if (!CraftBuildMode.IsCreative)
            {
                AddReachedDynamicRecipeIds(ids, system);
            }
            PruneRecipePrerequisites(ids, system);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids.ToArray();
        }

        internal static string[] GetBlueprintIds()
        {
            List<string> ids = new List<string>();
            RecipeSystem system = GameSystem<RecipeSystem>.Instance();
            UnlockState unlocks = null;
            bool creative = CraftBuildMode.IsCreative;
            bool hasSkillUnlocks = creative || TryGetSkillUnlockState(out unlocks);
            if (system != null && system.RecipeContainer != null)
            {
                foreach (Building.Blueprint blueprint in system.RecipeContainer.GetAllBlueprints())
                {
                    if (creative)
                    {
                        // Creative follows the local PC Final craft-mode catalog.
                        if (blueprint.IsShowCraftMode)
                        {
                            AddUniqueId(ids, blueprint.Id);
                        }
                    }
                    else if (hasSkillUnlocks &&
                        IsSkillUnlocked(blueprint.Id, unlocks.AllBlueprints, unlocks.LearnedBlueprints))
                    {
                        // In survival IsShowCraftMode is display metadata, not ownership.
                        AddUniqueId(ids, blueprint.Id);
                    }
                }
            }

            if (!creative)
            {
                AddReachedDynamicBlueprintIds(ids, system);
                AddPioneerBlueprintIds(ids, system);
                AddClanBlueprintIds(ids, system);
            }

            PruneBlueprintPrerequisites(ids, system);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids.ToArray();
        }

        internal static bool IsRecipeAllowed(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            if (CraftBuildMode.IsCreative) return GameSystem<RecipeSystem>.Instance().GetRecipe(recipeId) != null;
            return Array.IndexOf(GetRecipeIds(), recipeId) >= 0;
        }

        internal static bool IsBlueprintAllowed(string blueprintId)
        {
            if (string.IsNullOrEmpty(blueprintId)) return false;
            RecipeSystem system = GameSystem<RecipeSystem>.Instance();
            Building.Blueprint blueprint = system == null ? null : system.GetBlueprint(blueprintId);
            if (blueprint == null) return false;
            if (CraftBuildMode.IsCreative) return true;
            return Array.IndexOf(GetBlueprintIds(), blueprintId) >= 0;
        }

        private sealed class UnlockState
        {
            internal HashSet<string> AllRecipes;
            internal HashSet<string> LearnedRecipes;
            internal HashSet<string> AllBlueprints;
            internal HashSet<string> LearnedBlueprints;
        }

        private static bool TryGetSkillUnlockState(out UnlockState state)
        {
            state = null;
            try
            {
                if (_skillUnlockMethod == null)
                {
                    Type apiType = AccessTools.TypeByName("BaoX.DurangoOriginal.SkillSystemMod.SkillSystemApi");
                    if (apiType != null)
                    {
                        _skillUnlockMethod = apiType.GetMethod("GetCraftBuildUnlockState", BindingFlags.Public | BindingFlags.Static);
                    }
                }
                if (_skillUnlockMethod == null)
                {
                    if (!_skillApiMissingLogged)
                    {
                        _skillApiMissingLogged = true;
                        CraftBuildPlugin.Log.LogWarning("SkillSystemPlugin 0.5.31 or newer is required for survival craft/build availability.");
                    }
                    return false;
                }

                object[] args = new object[] { null, null, null, null };
                object result = _skillUnlockMethod.Invoke(null, args);
                if (!(result is bool) || !(bool)result)
                {
                    return false;
                }
                state = new UnlockState
                {
                    AllRecipes = NewIdSet(args[0] as string[]),
                    LearnedRecipes = NewIdSet(args[1] as string[]),
                    AllBlueprints = NewIdSet(args[2] as string[]),
                    LearnedBlueprints = NewIdSet(args[3] as string[])
                };
                return true;
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogWarning("Skill unlock state is not ready: " + exception.Message);
                return false;
            }
        }

        private static HashSet<string> NewIdSet(string[] ids)
        {
            return new HashSet<string>(ids ?? new string[0], StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsSkillUnlocked(string id, HashSet<string> skillOwned, HashSet<string> learned)
        {
            return skillOwned.Contains(id) && learned.Contains(id);
        }

        private static void AddUniqueId(List<string> ids, string id)
        {
            if (ids == null || string.IsNullOrEmpty(id) || ids.Contains(id)) return;
            ids.Add(id);
        }

        private static void AddReachedDynamicRecipeIds(List<string> ids, RecipeSystem system)
        {
            if (ids == null || system == null || !GameSystem<StatisticsSystem>.HasInstance()) return;
            foreach (KeyValuePair<Shared.Ability.Derived, Yaml.DerivedRewardData[]> pair in
                Yaml.Util.SingletonDict<Shared.Ability.Derived, Yaml.DerivedRewardData[]>.Instance)
            {
                Yaml.DerivedRewardData[] rewards = pair.Value;
                if (rewards == null) continue;
                float derivedValue = GameSystem<StatisticsSystem>.Instance().GetDeriveds(pair.Key, 0f);
                for (int i = 0; i < rewards.Length; i++)
                {
                    Yaml.DerivedRewardData data = rewards[i];
                    if (data == null || derivedValue < data.RequiredValue || string.IsNullOrEmpty(data.RewardId)) continue;
                    Yaml.DerivedReward reward = Yaml.Util.SingletonDict<string, Yaml.DerivedReward>.Get(data.RewardId, null);
                    if (reward == null ||
                        reward.Type != Shared.Faction.RewardType.DynamicRecipe ||
                        string.IsNullOrEmpty(reward.RecipeId) || system.GetRecipe(reward.RecipeId) == null) continue;
                    AddUniqueId(ids, reward.RecipeId);
                }
            }
        }

        private static void AddReachedDynamicBlueprintIds(List<string> ids, RecipeSystem system)
        {
            if (ids == null || system == null || !GameSystem<StatisticsSystem>.HasInstance()) return;
            foreach (KeyValuePair<Shared.Ability.Derived, Yaml.DerivedRewardData[]> pair in
                Yaml.Util.SingletonDict<Shared.Ability.Derived, Yaml.DerivedRewardData[]>.Instance)
            {
                Yaml.DerivedRewardData[] rewards = pair.Value;
                if (rewards == null) continue;
                float derivedValue = GameSystem<StatisticsSystem>.Instance().GetDeriveds(pair.Key, 0f);
                for (int i = 0; i < rewards.Length; i++)
                {
                    Yaml.DerivedRewardData data = rewards[i];
                    if (data == null || derivedValue < data.RequiredValue || string.IsNullOrEmpty(data.RewardId)) continue;
                    Yaml.DerivedReward reward = Yaml.Util.SingletonDict<string, Yaml.DerivedReward>.Get(data.RewardId, null);
                    Building.Blueprint target = reward == null || string.IsNullOrEmpty(reward.BlueprintId)
                        ? null
                        : system.GetBlueprint(reward.BlueprintId);
                    if (reward == null ||
                        reward.Type != Shared.Faction.RewardType.DynamicBlueprint ||
                        target == null) continue;
                    AddUniqueId(ids, reward.BlueprintId);
                }
            }
        }

        private static void AddPioneerBlueprintIds(List<string> ids, RecipeSystem system)
        {
            if (ids == null || system == null)
            {
                return;
            }

            int grade;
            if (!TryGetPioneerGrade(out grade))
            {
                return;
            }

            EnsureServerDataLoaded();
            JToken rewardsToken;
            JObject gradeRewards = _pioneerServerData != null &&
                _pioneerServerData.TryGetValue("reward_blueprints", out rewardsToken)
                ? rewardsToken as JObject
                : null;
            if (gradeRewards == null)
            {
                return;
            }

            foreach (JProperty gradeReward in gradeRewards.Properties())
            {
                int requiredGrade;
                if (!int.TryParse(gradeReward.Name, out requiredGrade) || requiredGrade > grade)
                {
                    continue;
                }

                JObject rewardTypes = gradeReward.Value as JObject;
                JToken blueprintToken;
                JArray blueprintIds = rewardTypes != null &&
                    rewardTypes.TryGetValue("10", out blueprintToken)
                    ? blueprintToken as JArray
                    : null;
                if (blueprintIds == null)
                {
                    continue;
                }

                foreach (JToken idToken in blueprintIds)
                {
                    string blueprintId = idToken == null ? null : idToken.Value<string>();
                    if (!string.IsNullOrEmpty(blueprintId) && system.GetBlueprint(blueprintId) != null)
                    {
                        AddUniqueId(ids, blueprintId);
                    }
                }
            }
        }

        private static void AddClanBlueprintIds(List<string> ids, RecipeSystem system)
        {
            if (ids == null || system == null)
            {
                return;
            }

            int clanLevel;
            if (!TryGetClanLevel(out clanLevel))
            {
                return;
            }

            EnsureServerDataLoaded();
            JToken rewardsToken;
            JObject levelRewards = _clanServerData != null &&
                _clanServerData.TryGetValue("level_rewards", out rewardsToken)
                ? rewardsToken as JObject
                : null;
            if (levelRewards == null)
            {
                return;
            }

            foreach (JProperty levelReward in levelRewards.Properties())
            {
                int requiredLevel;
                if (!int.TryParse(levelReward.Name, out requiredLevel) || requiredLevel > clanLevel)
                {
                    continue;
                }

                JObject reward = levelReward.Value as JObject;
                JToken blueprintToken;
                string blueprintId = reward != null && reward.TryGetValue("blueprint_id", out blueprintToken)
                    ? blueprintToken.Value<string>()
                    : null;
                if (!string.IsNullOrEmpty(blueprintId) && system.GetBlueprint(blueprintId) != null)
                {
                    AddUniqueId(ids, blueprintId);
                }
            }
        }

        private static bool TryGetPioneerGrade(out int grade)
        {
            grade = 0;
            try
            {
                Type pluginType = AccessTools.TypeByName(
                    "BaoX.DurangoOriginal.TamedIslandRestoration.TamedIslandRestorationPlugin");
                bool enabled;
                if (!TryReadConfigValue(pluginType, "Enabled", out enabled) || !enabled || _localPlayer == null)
                {
                    return false;
                }

                Type stateType = AccessTools.TypeByName(
                    "BaoX.DurangoOriginal.TamedIslandRestoration.TamedPioneerState");
                if (stateType == null)
                {
                    return false;
                }

                MethodInfo get = stateType.GetMethod(
                    "Get",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo gradeProperty = stateType.GetProperty("Grade", InstanceFlags);
                if (get == null || gradeProperty == null)
                {
                    return false;
                }

                object state = get.Invoke(null, new object[] { _localPlayer.EntityId });
                object rawGrade = state == null ? null : gradeProperty.GetValue(state, null);
                if (rawGrade == null)
                {
                    return false;
                }

                grade = Math.Max(0, Convert.ToInt32(rawGrade));
                return true;
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogWarning("Pioneer blueprint entitlement state unavailable: " + exception.Message);
                return false;
            }
        }

        private static bool TryGetClanLevel(out int clanLevel)
        {
            clanLevel = 0;
            try
            {
                Type pluginType = AccessTools.TypeByName(
                    "BaoX.DurangoOriginal.OfflineClanRestoration.OfflineClanRestorationPlugin");
                bool enabled;
                bool hasClan;
                int level;
                if (!TryReadConfigValue(pluginType, "Enabled", out enabled) || !enabled ||
                    !TryReadConfigValue(pluginType, "HasClan", out hasClan) || !hasClan ||
                    !TryReadConfigValue(pluginType, "ClanLevel", out level))
                {
                    return false;
                }

                clanLevel = Math.Max(0, level);
                return true;
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogWarning("Clan blueprint entitlement state unavailable: " + exception.Message);
                return false;
            }
        }

        private static bool TryReadConfigValue<T>(Type pluginType, string fieldName, out T value)
        {
            value = default(T);
            if (pluginType == null)
            {
                return false;
            }

            FieldInfo field = pluginType.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object entry = field == null ? null : field.GetValue(null);
            if (entry == null)
            {
                return false;
            }

            PropertyInfo valueProperty = entry.GetType().GetProperty("Value", InstanceFlags);
            object raw = valueProperty == null ? null : valueProperty.GetValue(entry, null);
            if (!(raw is T))
            {
                return false;
            }

            value = (T)raw;
            return true;
        }

        private static void PruneRecipePrerequisites(List<string> ids, RecipeSystem system)
        {
            if (system == null) { ids.Clear(); return; }
            HashSet<string> allowed = NewIdSet(ids.ToArray());
            ids.RemoveAll(delegate(string id)
            {
                Crafting.Recipe recipe = system.GetRecipe(id);
                return recipe == null || !HasRecipePrerequisite(recipe, allowed, system, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            });
        }

        private static bool HasRecipePrerequisite(Crafting.Recipe recipe, HashSet<string> allowed, RecipeSystem system, HashSet<string> visiting)
        {
            if (recipe == null || !visiting.Add(recipe.Id)) return false;
            if (string.IsNullOrEmpty(recipe.RequiredRecipe)) return true;
            if (!allowed.Contains(recipe.RequiredRecipe)) return false;
            return HasRecipePrerequisite(system.GetRecipe(recipe.RequiredRecipe), allowed, system, visiting);
        }

        private static void PruneBlueprintPrerequisites(List<string> ids, RecipeSystem system)
        {
            if (system == null) { ids.Clear(); return; }
            HashSet<string> allowed = NewIdSet(ids.ToArray());
            ids.RemoveAll(delegate(string id)
            {
                Building.Blueprint blueprint = system.GetBlueprint(id);
                return blueprint == null || !HasBlueprintPrerequisite(blueprint, allowed, system, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            });
        }

        private static bool HasBlueprintPrerequisite(Building.Blueprint blueprint, UnlockState unlocks, RecipeSystem system, HashSet<string> visiting)
        {
            if (blueprint == null || !visiting.Add(blueprint.Id)) return false;
            if (string.IsNullOrEmpty(blueprint.RequiredBlueprint)) return true;
            Building.Blueprint required = system.GetBlueprint(blueprint.RequiredBlueprint);
            if (required == null || !IsSkillUnlocked(required.Id, unlocks.AllBlueprints, unlocks.LearnedBlueprints)) return false;
            return HasBlueprintPrerequisite(required, unlocks, system, visiting);
        }

        private static bool HasBlueprintPrerequisite(Building.Blueprint blueprint, HashSet<string> allowed, RecipeSystem system, HashSet<string> visiting)
        {
            if (blueprint == null || !visiting.Add(blueprint.Id)) return false;
            if (string.IsNullOrEmpty(blueprint.RequiredBlueprint)) return true;
            if (!allowed.Contains(blueprint.RequiredBlueprint)) return false;
            return HasBlueprintPrerequisite(system.GetBlueprint(blueprint.RequiredBlueprint), allowed, system, visiting);
        }

        private static string[] FilterIds(HashSet<string> source, string[] allowed)
        {
            HashSet<string> allowedSet = NewIdSet(allowed);
            List<string> result = new List<string>();
            foreach (string id in source)
            {
                if (allowedSet.Contains(id)) result.Add(id);
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result.ToArray();
        }

        private static bool ValidateRecipeMaterials(Durango.Offline.PlayerContext context, Crafting.Recipe recipe, Dictionary<string, string[]> materials)
        {
            if (recipe == null || context == null) return false;
            if (recipe.Slots == null || recipe.Slots.Length == 0) return materials == null || materials.Count == 0;
            if (materials == null) return false;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> knownSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < recipe.Slots.Length; i++)
            {
                Crafting.RecipeSlot slot = recipe.Slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Id)) return false;
                knownSlots.Add(slot.Id);

                string[] ids;
                if (!materials.TryGetValue(slot.Id, out ids) || ids == null || ids.Length < slot.Count) return false;
                if (slot.CountMax > 0 && ids.Length > slot.CountMax) return false;

                for (int j = 0; j < ids.Length; j++)
                {
                    string id = ids[j];
                    Item item;
                    if (string.IsNullOrEmpty(id) || !seen.Add(id) || !TryGetInventoryItem(context, id, out item)) return false;
                    ItemData itemData = new ItemData(item);
                    if (!slot.IsSuitableItem(itemData, false)) return false;
                }
            }

            foreach (KeyValuePair<string, string[]> pair in materials)
            {
                if (!knownSlots.Contains(pair.Key) && pair.Value != null && pair.Value.Length > 0) return false;
            }
            return true;
        }

        private static bool ValidateRecipeTool(Durango.Offline.PlayerContext context, Crafting.Recipe recipe, string toolId)
        {
            if (recipe == null || !recipe.HasRequiredTool) return true;
            if (context == null || string.IsNullOrEmpty(toolId)) return false;
            Item tool;
            if (!TryGetInventoryItem(context, toolId, out tool)) return false;
            ItemData toolData = new ItemData(tool);
            return !toolData.IsDestroyed() && toolData.HasTag(recipe.AllowedTool, false);
        }

        private static bool TryResolveItems(Durango.Offline.PlayerContext context, Dictionary<string, string[]> materialIds, out List<Item> items, List<string> flatIds)
        {
            items = new List<Item>();
            if (materialIds == null || materialIds.Count == 0) return true;
            if (context == null) return false;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string[]> pair in materialIds)
            {
                if (pair.Value == null) continue;
                for (int i = 0; i < pair.Value.Length; i++)
                {
                    string id = pair.Value[i];
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) return false;
                    Item item;
                    if (!TryGetInventoryItem(context, id, out item)) return false;
                    items.Add(item);
                    if (flatIds != null) flatIds.Add(id);
                }
            }
            return true;
        }

        private static bool TryGetInventoryItem(Durango.Offline.PlayerContext context, string id, out Item item)
        {
            if (context != null && context.InventoryItems != null)
            {
                for (int i = 0; i < context.InventoryItems.Count; i++)
                {
                    if (context.InventoryItems[i].Id == id)
                    {
                        item = context.InventoryItems[i];
                        return true;
                    }
                }
            }
            item = default(Item);
            return false;
        }

        private static void RemoveInventoryItems(Durango.Offline.PlayerContext context, List<string> ids)
        {
            if (context == null || ids == null) return;
            HashSet<string> remove = new HashSet<string>(ids, StringComparer.Ordinal);
            context.InventoryItems.RemoveAll(delegate(Item item) { return remove.Contains(item.Id); });
        }

        private static int CalculateLevel(int min, int max, List<Item> materials)
        {
            int level = Math.Max(1, min);
            if (materials != null && materials.Count > 0)
            {
                long sum = 0;
                for (int i = 0; i < materials.Count; i++) sum += materials[i].Level;
                level = (int)Math.Round((double)sum / materials.Count);
                if (level < min) level = min;
            }
            if (max > 0 && level > max) level = max;
            return Math.Max(1, level);
        }

        private static string ResolveCraftPrototypeId(string recipeId, Yaml.Recipe recipe, List<Item> materials)
        {
            if (recipe != null && !string.IsNullOrEmpty(recipe.prototype_id)) return recipe.prototype_id;
            if (string.Equals(recipeId, "skewer", StringComparison.Ordinal))
            {
                Item? ingredient = FindFirstCraftIngredient(materials);
                if (ingredient != null)
                {
                    Item item = ingredient.Value;
                    if (HasItemTag(item, "marshmallow")) return "skewer_marshmallow";
                    if (HasItemTag(item, "lizard") || HasItemTag(item, "lizard_body")) return "skewer_lizard";
                    if (HasItemTag(item, "fish")) return "skewer_fish";
                    if (HasItemTag(item, "meat")) return "skewer_meat";
                }
                return "skewer_vege";
            }
            return null;
        }

        private static Item? FindFirstCraftIngredient(List<Item> materials)
        {
            if (materials == null || materials.Count == 0) return null;
            return materials[0];
        }

        private static bool HasItemTag(Item item, string tagId)
        {
            if (item.Tags == null || string.IsNullOrEmpty(tagId)) return false;
            for (int i = 0; i < item.Tags.Length; i++)
            {
                if (string.Equals(item.Tags[i].Id, tagId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void NormalizeCraftedItem(string recipeId, ref Item item)
        {
            if (!string.Equals(recipeId, "skewer", StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(item.Prototype) || !item.Prototype.StartsWith("skewer_", StringComparison.Ordinal)))
            {
                return;
            }

            List<Messages.Tag> tags = new List<Messages.Tag>();
            tags.Add(new Messages.Tag { Id = "eatable", Level = item.Level });
            if (string.Equals(item.Prototype, "skewer_meat", StringComparison.Ordinal))
                tags.Add(new Messages.Tag { Id = "meat", Level = item.Level });
            else if (string.Equals(item.Prototype, "skewer_fish", StringComparison.Ordinal))
                tags.Add(new Messages.Tag { Id = "fish", Level = item.Level });
            else if (string.Equals(item.Prototype, "skewer_lizard", StringComparison.Ordinal))
            {
                tags.Add(new Messages.Tag { Id = "meat", Level = item.Level });
                tags.Add(new Messages.Tag { Id = "lizard", Level = item.Level });
            }
            else if (string.Equals(item.Prototype, "skewer_marshmallow", StringComparison.Ordinal))
                tags.Add(new Messages.Tag { Id = "marshmallow", Level = item.Level });
            else if (string.Equals(item.Prototype, "skewer_vege", StringComparison.Ordinal))
                tags.Add(new Messages.Tag { Id = "vegetable", Level = item.Level });
            item.Tags = tags.ToArray();
            item.TagModifications = new Messages.Tag[0];
            item.Performance = new Messages.Performance[0];
        }

        private static string[] BuildCheatArguments(Building.Blueprint blueprint, OccupyArtifactSite msg)
        {
            List<string> args = new List<string>();
            args.Add("prop");
            args.Add(blueprint.EntityType.ToString());
            args.Add("position:" + msg.Tile.x + "," + msg.Tile.y);
            Point2 size = (msg.Size.x > 0 && msg.Size.y > 0) ? msg.Size : blueprint.Size;
            args.Add("size:" + size.x + "," + size.y);
            args.Add("rotation:" + msg.Rotation);
            if (msg.Floor != null) args.Add("floor:" + msg.Floor.Value);
            if (msg.Stories != null) args.Add("stories:" + msg.Stories.Value);

            HashSet<string> suppliedSlots = new HashSet<string>(StringComparer.Ordinal);
            if (blueprint.Slots != null)
            {
                for (int i = 0; i < blueprint.Slots.Length; i++)
                {
                    Building.BlueprintSlot slot = blueprint.Slots[i];
                    if (slot == null || string.IsNullOrEmpty(slot.Id) || slot.Looks == null || slot.Looks.Count == 0 || !suppliedSlots.Add(slot.Id)) continue;
                    string lookKey = null;
                    Yaml.ArtifactLook look;
                    if (!string.IsNullOrEmpty(slot.DefaultLook) && slot.Looks.TryGetValue(slot.DefaultLook, out look) && look != null && !string.IsNullOrEmpty(look.model_key))
                        lookKey = slot.DefaultLook;
                    if (lookKey == null)
                    {
                        foreach (KeyValuePair<string, Yaml.ArtifactLook> pair in slot.Looks)
                        {
                            if (pair.Value != null && !string.IsNullOrEmpty(pair.Value.model_key)) { lookKey = pair.Key; break; }
                        }
                    }
                    if (!string.IsNullOrEmpty(lookKey)) args.Add(slot.Id + ":" + lookKey);
                }
            }
            return args.ToArray();
        }

        private static Dictionary<string, List<Item>> NewMaterialMap(Building.Blueprint blueprint)
        {
            Dictionary<string, List<Item>> result = new Dictionary<string, List<Item>>();
            if (blueprint != null && blueprint.Slots != null)
            {
                for (int i = 0; i < blueprint.Slots.Length; i++) result[blueprint.Slots[i].Id] = new List<Item>();
            }
            return result;
        }

        private static ArtifactMaterials MakeArtifactMaterials(string entityId)
        {
            Dictionary<string, Item[]> result = new Dictionary<string, Item[]>();
            lock (Sync)
            {
                Dictionary<string, List<Item>> slots;
                if (_siteMaterials.TryGetValue(entityId, out slots))
                {
                    foreach (KeyValuePair<string, List<Item>> pair in slots) result[pair.Key] = pair.Value.ToArray();
                }
            }
            return new ArtifactMaterials { EntityId = entityId, Materials = result };
        }

        private static bool HasAllBuildMaterials(AppearArtifact artifact, Building.Blueprint blueprint)
        {
            if (blueprint == null || blueprint.Slots == null) return false;
            Dictionary<string, List<Item>> slots;
            if (!_siteMaterials.TryGetValue(artifact.EntityId, out slots)) return blueprint.Slots.Length == 0;
            for (int i = 0; i < blueprint.Slots.Length; i++)
            {
                Building.BlueprintSlot slot = blueprint.Slots[i];
                int modifier = blueprint.IsSizeVariable ? slot.GetSlotCountModifier(artifact.Size) : 1;
                int needed = slot.Count * Math.Max(1, modifier);
                List<Item> supplied;
                if (!slots.TryGetValue(slot.Id, out supplied) || supplied.Count < needed) return false;
            }
            return true;
        }

        private static Gauge FullGauge()
        {
            return new Gauge(1f, 0f, new GaugeNode[] { new GaugeNode { Time = 0.0, Value = 1f } });
        }

        private static void BeginTimedReply(uint seq)
        {
            if (seq == 0U) return;
            lock (Sync) TimedReplySequences.Add(seq);
        }

        internal static bool IsTimedReply(uint seq)
        {
            lock (Sync) return TimedReplySequences.Contains(seq);
        }

        internal static void EndTimedReply(uint seq)
        {
            lock (Sync) TimedReplySequences.Remove(seq);
        }

        private static string TimedActionKey(string playerId, string subject)
        {
            return (playerId ?? string.Empty) + "\u001f" + (subject ?? string.Empty);
        }

        private static void SetTimedAction(string playerId, string subject, object token)
        {
            object previous = null;
            lock (Sync)
            {
                string key = TimedActionKey(playerId, subject);
                ActiveDestructActions.TryGetValue(key, out previous);
                ActiveDestructActions[key] = token;
            }
            if (previous != null && !ReferenceEquals(previous, token)) CraftBuildPlugin.CancelScheduled(previous);
        }

        private static bool ConsumeTimedAction(string playerId, string subject, object token)
        {
            lock (Sync)
            {
                string key = TimedActionKey(playerId, subject);
                object active;
                if (!ActiveDestructActions.TryGetValue(key, out active) || !ReferenceEquals(active, token)) return false;
                ActiveDestructActions.Remove(key);
                return true;
            }
        }

        internal static void CancelTimedAction(string playerId, string subject)
        {
            object token = null;
            lock (Sync)
            {
                string key = TimedActionKey(playerId, subject);
                if (ActiveDestructActions.TryGetValue(key, out token)) ActiveDestructActions.Remove(key);
            }
            if (token != null)
            {
                CraftBuildPlugin.CancelScheduled(token);
                CraftBuildPlugin.Log.LogInfo("Cancelled interrupted " + subject + " action for player=" + playerId + ".");
            }
        }

        private static void CompleteBuild(Durango.Offline.Player player, Durango.Offline.World world, AppearArtifact artifact, Building.Blueprint blueprint, string entityId, uint seq)
        {
            artifact.Display = GetCompletedDisplay(artifact, blueprint);
            artifact.Display.EntityId = artifact.EntityId;

            ArtifactState builtState = artifact.States;
            builtState.EntityId = artifact.EntityId;
            builtState.BuildingState = BuildingState.Built;
            builtState.Durability = FullGauge();
            int artifactLevel = GetBuiltArtifactLevel(artifact.EntityId, blueprint);
            builtState.Level = (byte)Math.Max(1, Math.Min(byte.MaxValue, artifactLevel));
            ApplyWorkbenchCapabilityTags(ref artifact, blueprint, artifactLevel);

            int postprocessSeconds = CraftBuildMode.IsCreative ? 0 : Math.Max(0, blueprint.PostprocessTime);
            artifact.States = builtState;
            if (postprocessSeconds > 0)
            {
                double now = Times.UnixTimeNow();
                artifact.States.Postprocess = new Postprocess
                {
                    StartedAt = now,
                    EndsAt = now + postprocessSeconds,
                    Helpers = new string[0],
                    MaxHelperCount = GetPostprocessHelperMax(blueprint),
                    RemodelSlotId = null
                };
            }
            else
            {
                artifact.States.BuildingState = BuildingState.Completed;
                artifact.States.Postprocess = null;
            }
            UpdateArtifact(world, artifact);

            lock (Sync)
            {
                _siteMaterials.Remove(entityId);
                _siteCompletedDisplays.Remove(entityId);
                Save();
            }

            player.Send<ArtifactDisplay>(artifact.Display, 0U);
            player.Send<ArtifactState>(builtState, 0U);
            player.Send<ArtifactBuilt>(new ArtifactBuilt { EntityId = artifact.EntityId, BuilderId = player.EntityId }, 0U);
            player.Send<ArtifactState>(artifact.States, 0U);
            player.Send<Tags>(artifact.Tags, 0U);
            if (postprocessSeconds <= 0) player.Send<ArtifactCompleted>(new ArtifactCompleted { EntityId = artifact.EntityId }, 0U);
            player.Send<OK>(default(OK), seq);
            if (postprocessSeconds > 0)
                CraftBuildPlugin.Log.LogInfo("Build entered postprocess: entity=" + artifact.EntityId + ", blueprint=" + blueprint.Id + ", duration=" + postprocessSeconds + "s; waiting for Complete.");
            else
                CraftBuildPlugin.Log.LogInfo("Build completed: entity=" + artifact.EntityId + ", blueprint=" + blueprint.Id + ".");
        }

        private static void FinishPostprocess(Durango.Offline.Player player, Durango.Offline.World world, AppearArtifact artifact, uint seq)
        {
            artifact.States.BuildingState = BuildingState.Completed;
            artifact.States.Postprocess = null;
            UpdateArtifact(world, artifact);
            player.Send<ArtifactState>(artifact.States, 0U);
            player.Send<ArtifactCompleted>(new ArtifactCompleted { EntityId = artifact.EntityId }, 0U);
            if (seq != 0U) player.Send<OK>(default(OK), seq);
            CraftBuildPlugin.Log.LogInfo("Build postprocess completed: entity=" + artifact.EntityId + ".");
        }

        private static ArtifactDisplay GetCompletedDisplay(AppearArtifact artifact, Building.Blueprint blueprint)
        {
            ArtifactDisplay display;
            lock (Sync)
            {
                if (_siteCompletedDisplays.TryGetValue(artifact.EntityId, out display))
                {
                    display.EntityId = artifact.EntityId;
                    return display;
                }
            }
            return CreateCompletedDisplay(blueprint, artifact.EntityId);
        }

        private static ArtifactDisplay CreateCompletedDisplay(Building.Blueprint blueprint, string entityId)
        {
            ArtifactDisplay display = blueprint.GetDefaultDisplay();
            display.EntityId = entityId;
            if (HasComponent(blueprint, "Burnable") && !string.IsNullOrEmpty(blueprint.DefaultLook))
            {
                if (display.Parts == null) display.Parts = new Dictionary<string, string>();
                display.Parts["common"] = blueprint.DefaultLook + "_burning";
            }
            return display;
        }

        private static bool HasComponent(Building.Blueprint blueprint, string component)
        {
            return blueprint != null && blueprint.Components != null && Array.IndexOf(blueprint.Components, component) >= 0;
        }

        private static int GetBuiltArtifactLevel(string entityId, Building.Blueprint blueprint)
        {
            if (blueprint == null) return 1;
            if (CraftBuildMode.IsCreative) return Math.Max(1, blueprint.MaxLevel);

            List<Item> materials = new List<Item>();
            lock (Sync)
            {
                Dictionary<string, List<Item>> slots;
                if (_siteMaterials.TryGetValue(entityId, out slots))
                {
                    foreach (List<Item> slotItems in slots.Values)
                        if (slotItems != null) materials.AddRange(slotItems);
                }
            }
            return CalculateLevel(blueprint.MinLevel, blueprint.MaxLevel, materials);
        }

        private static Dictionary<string, int> GetWorkbenchCapabilityTags(Building.Blueprint blueprint, int level)
        {
            Dictionary<string, int> tags = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!HasComponent(blueprint, "Workbench")) return tags;
            if (string.Equals(blueprint.Id, "bonfire", StringComparison.Ordinal) || string.Equals(blueprint.Id, "bonfire_01", StringComparison.Ordinal))
                tags["cook"] = Math.Max(1, level);
            return tags;
        }

        private static void ApplyWorkbenchCapabilityTags(ref AppearArtifact artifact, Building.Blueprint blueprint, int level)
        {
            Dictionary<string, int> capabilityTags = GetWorkbenchCapabilityTags(blueprint, level);
            if (capabilityTags.Count == 0) return;

            Dictionary<string, int> merged = new Dictionary<string, int>(StringComparer.Ordinal);
            if (artifact.Tags._Tags != null)
            {
                for (int i = 0; i < artifact.Tags._Tags.Length; i++)
                {
                    Messages.Tag tag = artifact.Tags._Tags[i];
                    if (!string.IsNullOrEmpty(tag.Id)) merged[tag.Id] = tag.Level;
                }
            }
            foreach (KeyValuePair<string, int> pair in capabilityTags) merged[pair.Key] = pair.Value;
            List<Messages.Tag> result = new List<Messages.Tag>();
            foreach (KeyValuePair<string, int> pair in merged) result.Add(new Messages.Tag { Id = pair.Key, Level = pair.Value });
            artifact.Tags = new Tags { EntityId = artifact.EntityId, _Tags = result.ToArray() };
        }

        private static bool IsValidWorkbench(Durango.Offline.Player player, Yaml.Recipe recipe, PropKey? key)
        {
            if (recipe == null || recipe.workbench_tags == null || recipe.workbench_tags.Count == 0) return true;
            if (key == null) return false;

            Durango.Offline.World world = GetWorld(player);
            AppearArtifact? found = world == null ? null : world.ArtifactManager.Get(key.Value.EntityId);
            if (found == null || found.Value.States.BuildingState != BuildingState.Completed) return false;
            Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().GetBlueprint((int)found.Value.EntityType);
            if (!HasComponent(blueprint, "Workbench") || found.Value.Tags._Tags == null) return false;

            for (int i = 0; i < found.Value.Tags._Tags.Length; i++)
            {
                Messages.Tag actual = found.Value.Tags._Tags[i];
                int required;
                if (!string.IsNullOrEmpty(actual.Id) && recipe.workbench_tags.TryGetValue(actual.Id, out required) && actual.Level >= required) return true;
            }
            return false;
        }

        private static void RepairLegacyWorkbenchTags(Durango.Offline.Player player)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                Dictionary<string, AppearArtifact> artifacts = world == null || ArtifactsField == null ? null : ArtifactsField.GetValue(world.ArtifactManager) as Dictionary<string, AppearArtifact>;
                RecipeSystem recipes = GameSystem<RecipeSystem>.Instance();
                if (artifacts == null || recipes == null) return;

                int repaired = 0;
                List<string> ids = new List<string>(artifacts.Keys);
                for (int i = 0; i < ids.Count; i++)
                {
                    AppearArtifact artifact = artifacts[ids[i]];
                    if (artifact.States.BuildingState != BuildingState.Completed) continue;
                    Building.Blueprint blueprint = recipes.GetBlueprint((int)artifact.EntityType);
                    Dictionary<string, int> expected = GetWorkbenchCapabilityTags(blueprint, artifact.States.Level > 0 ? artifact.States.Level : Math.Max(1, blueprint == null ? 1 : blueprint.MaxLevel));
                    if (expected.Count == 0) continue;

                    bool needsRepair = artifact.Tags._Tags == null;
                    foreach (KeyValuePair<string, int> pair in expected)
                    {
                        bool foundTag = false;
                        if (artifact.Tags._Tags != null)
                        {
                            for (int j = 0; j < artifact.Tags._Tags.Length; j++)
                            {
                                if (string.Equals(artifact.Tags._Tags[j].Id, pair.Key, StringComparison.Ordinal) && artifact.Tags._Tags[j].Level == pair.Value)
                                { foundTag = true; break; }
                            }
                        }
                        if (!foundTag) { needsRepair = true; break; }
                    }
                    if (!needsRepair) continue;

                    int level = artifact.States.Level > 0 ? artifact.States.Level : Math.Max(1, blueprint.MaxLevel);
                    artifact.States.Level = (byte)Math.Min(byte.MaxValue, level);
                    ApplyWorkbenchCapabilityTags(ref artifact, blueprint, level);
                    artifacts[artifact.EntityId] = artifact;
                    repaired++;
                }

                if (repaired > 0)
                {
                    world.Save();
                    CraftBuildPlugin.Log.LogInfo("Repaired workbench capability tags on " + repaired + " completed artifact(s).");
                }
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogWarning("Could not repair legacy workbench tags: " + exception.Message);
            }
        }

        private static void RepairLegacySkewerItems(Durango.Offline.Player player)
        {
            try
            {
                Durango.Offline.PlayerContext context = GetContext(player);
                if (context == null || context.InventoryItems == null) return;

                int repaired = 0;
                for (int i = 0; i < context.InventoryItems.Count; i++)
                {
                    Item item = context.InventoryItems[i];
                    if (string.IsNullOrEmpty(item.Prototype) || !item.Prototype.StartsWith("skewer_", StringComparison.Ordinal)) continue;
                    if (HasItemTag(item, "eatable") && !HasItemTag(item, "blunt_onehand")) continue;
                    NormalizeCraftedItem("skewer", ref item);
                    context.InventoryItems[i] = item;
                    repaired++;
                }

                if (repaired > 0)
                {
                    SavePlayer(context, player);
                    CraftBuildPlugin.Log.LogInfo("Repaired " + repaired + " legacy skewer item(s) from weapon to food.");
                }
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogWarning("Could not repair legacy skewer items: " + exception.Message);
            }
        }

        private static void RepairLegacyCompletedDisplays(Durango.Offline.Player player)
        {
            try
            {
                Durango.Offline.World world = GetWorld(player);
                Dictionary<string, AppearArtifact> artifacts = world == null || ArtifactsField == null ? null : ArtifactsField.GetValue(world.ArtifactManager) as Dictionary<string, AppearArtifact>;
                RecipeSystem recipes = GameSystem<RecipeSystem>.Instance();
                if (artifacts == null || recipes == null) return;

                int repaired = 0;
                List<string> ids = new List<string>(artifacts.Keys);
                for (int i = 0; i < ids.Count; i++)
                {
                    AppearArtifact artifact = artifacts[ids[i]];
                    if (artifact.States.BuildingState != BuildingState.Completed) continue;
                    Building.Blueprint blueprint = recipes.GetBlueprint((int)artifact.EntityType);
                    if (!HasComponent(blueprint, "Burnable") || string.IsNullOrEmpty(blueprint.DefaultLook)) continue;
                    string current;
                    if (artifact.Display.Parts != null && artifact.Display.Parts.TryGetValue("common", out current) && string.Equals(current, blueprint.DefaultLook + "_burning", StringComparison.Ordinal)) continue;
                    artifact.Display = CreateCompletedDisplay(blueprint, artifact.EntityId);
                    artifacts[artifact.EntityId] = artifact;
                    repaired++;
                }

                if (repaired > 0)
                {
                    world.Save();
                    CraftBuildPlugin.Log.LogInfo("Repaired " + repaired + " completed burnable artifact display(s).");
                }
            }
            catch (Exception exception)
            {
                CraftBuildPlugin.Log.LogWarning("Could not repair legacy artifact displays: " + exception.Message);
            }
        }

        private static void UpdateArtifact(Durango.Offline.World world, AppearArtifact artifact)
        {
            Dictionary<string, AppearArtifact> artifacts = ArtifactsField == null ? null : ArtifactsField.GetValue(world.ArtifactManager) as Dictionary<string, AppearArtifact>;
            if (artifacts == null) throw new InvalidOperationException("Offline artifact dictionary was not found.");
            artifacts[artifact.EntityId] = artifact;
            world.Save();
        }

        private static Durango.Offline.PlayerContext GetContext(Durango.Offline.Player player)
        {
            return ContextField == null ? null : ContextField.GetValue(player) as Durango.Offline.PlayerContext;
        }

        private static Durango.Offline.World GetWorld(Durango.Offline.Player player)
        {
            return WorldField == null ? null : WorldField.GetValue(player) as Durango.Offline.World;
        }

        private static void SavePlayer(Durango.Offline.PlayerContext context, Durango.Offline.Player player)
        {
            if (ContextChangedMethod != null) ContextChangedMethod.Invoke(player, null);
            context.Save();
        }

        private static string[] ToArray(HashSet<string> set)
        {
            string[] result = new string[set.Count];
            set.CopyTo(result);
            return result;
        }

        private static void Load()
        {
            lock (Sync)
            {
                if (_loaded) return;
                _loaded = true;
                try
                {
                    if (File.Exists(SiteMaterialsPath))
                    {
                        _siteMaterials = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<Item>>>>(File.ReadAllText(SiteMaterialsPath))
                            ?? new Dictionary<string, Dictionary<string, List<Item>>>();
                    }
                    if (File.Exists(SiteDisplaysPath))
                    {
                        _siteCompletedDisplays = JsonConvert.DeserializeObject<Dictionary<string, ArtifactDisplay>>(File.ReadAllText(SiteDisplaysPath))
                            ?? new Dictionary<string, ArtifactDisplay>();
                    }
                }
                catch (Exception exception)
                {
                    CraftBuildPlugin.Log.LogWarning("Could not load build-site materials: " + exception.Message);
                    _siteMaterials = new Dictionary<string, Dictionary<string, List<Item>>>();
                    _siteCompletedDisplays = new Dictionary<string, ArtifactDisplay>();
                }
            }
        }

        private static void Save()
        {
            string directory = Path.GetDirectoryName(SiteMaterialsPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SiteMaterialsPath, JsonConvert.SerializeObject(_siteMaterials, Formatting.Indented));
            File.WriteAllText(SiteDisplaysPath, JsonConvert.SerializeObject(_siteCompletedDisplays, Formatting.Indented));
        }
    }

    [HarmonyPatch(typeof(Durango.Logic.Interactions.ArtifactInteractions), "SendDestructArtifact")]
    internal static class OfflineDestructMotionPatch
    {
        private static readonly MethodInfo OnDestructedReplied = typeof(Durango.Logic.Interactions.ArtifactInteractions).GetMethod("OnDestructedReplied", BindingFlags.Static | BindingFlags.NonPublic);

        private static bool Prefix(Artifact artifact)
        {
            if (GameManager.ClusterMode == Mode.Online || artifact == null || OnDestructedReplied == null) return true;

            Connections.Frontend.Send<DestructArtifact>(new DestructArtifact
            {
                EntityId = artifact.EntityId,
                Tile = artifact.WorldTile
            }, false, 0U).On<Destructing>(delegate(Destructing destructing, PacketHeader header)
            {
                OnDestructedReplied.Invoke(null, new object[] { artifact, destructing });
            });
            return false;
        }
    }

    [HarmonyPatch(typeof(Durango.Logic.Timer.Timer), "Stop", new Type[] { typeof(bool) })]
    internal static class InterruptedDestructCancellationPatch
    {
        private static void Postfix(Durango.Logic.Timer.Timer __instance)
        {
            if (GameManager.ClusterMode != Mode.Online && __instance != null && __instance.IsInterrupt && string.Equals(__instance.Subject, "destruct", StringComparison.Ordinal))
                CraftBuildBackend.CancelTimedAction(__instance.EntityId, "destruct");
        }
    }

    [HarmonyPatch(typeof(Durango.Network.Connection), "HandleMsg")]
    internal static class OfflineTimedReplySequencePatch
    {
        private static readonly FieldInfo ContinuousRepliesField = AccessTools.Field(typeof(Durango.Network.Connection), "_continuousReplies");

        private static void Prefix(Durango.Network.Connection __instance, Packet packet)
        {
            if (packet.Header.ReplyOf == 0U || ContinuousRepliesField == null || !CraftBuildBackend.IsTimedReply(packet.Header.ReplyOf)) return;
            HashSet<uint> replies = ContinuousRepliesField.GetValue(__instance) as HashSet<uint>;
            if (replies == null) return;
            if (packet.Header.TypeCode == Messages.Timer.TypeCode) replies.Add(packet.Header.ReplyOf);
            else if (replies.Contains(packet.Header.ReplyOf))
            {
                replies.Remove(packet.Header.ReplyOf);
                CraftBuildBackend.EndTimedReply(packet.Header.ReplyOf);
            }
        }
    }

    [HarmonyPatch(typeof(CraftSlotContainer), "GetReadyState")]
    internal static class CreativeCraftReadyPatch
    {
        private static void Postfix(ref CraftSlotContainer.CraftState __result)
        {
            if (CraftBuildMode.IsCreative) __result = CraftSlotContainer.CraftState.ReadyToCraft;
        }
    }

    [HarmonyPatch(typeof(BuildSlotContainer), "GetReadyState")]
    internal static class CreativeBuildReadyPatch
    {
        private static void Postfix(ref BuildSlotContainer.BuildState __result)
        {
            if (CraftBuildMode.IsCreative) __result = BuildSlotContainer.BuildState.ReadyToBuild;
        }
    }

    [HarmonyPatch(typeof(RecipeSystem), "CanCraftNow", new Type[] { typeof(CategoryItem) })]
    internal static class SurvivalCanCraftNowPatch
    {
        private static bool Prefix(RecipeSystem __instance, CategoryItem categoryItem, ref bool __result)
        {
            if (CraftBuildMode.IsCreative) return true;

            Crafting.Recipe recipe = categoryItem as Crafting.Recipe;
            if (recipe != null)
            {
                __result = CraftBuildBackend.IsRecipeAllowed(recipe.Id) && (!recipe.HasRequiredWorkbench || __instance.FindNearestAvailableWorkbench(recipe) != null) && RecipeSystem.HasMaterials(recipe, 1);
                return false;
            }

            Building.Blueprint blueprint = categoryItem as Building.Blueprint;
            if (blueprint != null)
            {
                __result = CraftBuildBackend.IsBlueprintAllowed(blueprint.Id) && RecipeSystem.HasMaterials(blueprint, 1);
                return false;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(SlotContainer), "CreateMaterialItemsDictionary", new Type[] { typeof(int), typeof(bool) })]
    internal static class CreativeMaterialSelectionPatch
    {
        private static void Postfix(SlotContainer __instance, ref Dictionary<string, ItemData[]> __result)
        {
            if (CraftBuildMode.IsCreative && __instance is CraftSlotContainer) __result = new Dictionary<string, ItemData[]>();
        }
    }

    [HarmonyPatch(typeof(CraftGroupBase), "SetSlotContainer", new Type[] { typeof(SlotContainer), typeof(bool) })]
    internal static class PcCraftGroupMissingBonusWidgetPatch
    {
        private static readonly FieldInfo EstimateResultWidgetField = AccessTools.Field(typeof(CraftGroupBase), "_estimateResultWidget");
        private static readonly FieldInfo BonusItemWidgetField = AccessTools.Field(typeof(ExpectResultWidget), "_bonusItemWidget");

        private static void Prefix(CraftGroupBase __instance)
        {
            ExpectResultWidget widget = EstimateResultWidgetField.GetValue(__instance) as ExpectResultWidget;
            if (widget == null || BonusItemWidgetField.GetValue(widget) != null) return;

            GameObject dummyObject = new GameObject("CraftBuildBonusItemWidgetCompat");
            dummyObject.transform.SetParent(widget.transform, false);
            UIWidget dummyWidget = dummyObject.AddComponent<UIWidget>();
            dummyObject.SetActive(false);
            BonusItemWidgetField.SetValue(widget, dummyWidget);
        }
    }
}
