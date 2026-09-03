using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BaoX.DurangoOriginal.HarborSailingMap;
using Durango.Offline;
using HarmonyLib;
using Messages;
using Shared.Estate;
using Shared.Region;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;
using PacketHeader = Durango.Network.PacketHeader;

namespace BaoX.DurangoOriginal.TamedIslandRestoration
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.baominix.durango.original.harborsailingmap", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class TamedIslandRestorationPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.tamedislandrestoration";
        public const string PluginName = "Tamed Island Restoration Plugin";
        public const string PluginVersion = "0.9.6";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> IslandName;
        internal static ConfigEntry<string> SelectedTerrainId;
        internal static ConfigEntry<int> EstateSize;
        internal static ConfigFile PluginConfig;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Restore the offline Tamed Island Domain backend.");
            IslandName = Config.Bind("Tamed Island", "IslandName", "Tamed Grassland Island",
                "Name shown for the offline personal region.");
            SelectedTerrainId = Config.Bind("Tamed Island", "SelectedTerrainId", "pe10gr_1",
                "Personal terrain selected for this offline profile (all packaged personal-region terrains).");
            EstateSize = Config.Bind("Tamed Island", "EstateSize", 1,
                "Current estate size. This value is maintained by the offline estate backend.");
            PluginConfig = Config;
            HarborIslandApi.RegisterPersonalTerrainProvider(
                delegate { return TamedIslandData.SelectedTerrain; });

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(PluginName + " loaded. Terrain=" + SelectedTerrainId.Value +
                ", Region=" + HarborIslandApi.GetTamedRegionId(SelectedTerrainId.Value));
        }

        private void OnDestroy()
        {
            HarborIslandApi.RegisterPersonalTerrainProvider(null);
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }

    internal static class TamedIslandData
    {
        private static string _playerName = string.Empty;
        private static readonly Dictionary<string, string> PlayerTerrains =
            new Dictionary<string, string>();

        internal static void SetPlayerName(string playerName)
        {
            _playerName = playerName ?? string.Empty;
        }

        internal static string IslandDisplayName
        {
            get
            {
                return string.IsNullOrEmpty(_playerName)
                    ? TamedIslandLocalization.ResolveIslandName(
                        TamedIslandRestorationPlugin.IslandName.Value)
                    : _playerName;
            }
        }

        public static string SelectedTerrain
        {
            get
            {
                string terrainId = TamedIslandRestorationPlugin.SelectedTerrainId.Value;
                return HarborIslandApi.IsTamedTerrain(terrainId) ? terrainId : HarborIslandApi.TamedTerrainId;
            }
        }

        public static string GetSelectedTerrain(string ownerId)
        {
            string terrainId;
            if (!string.IsNullOrEmpty(ownerId) &&
                PlayerTerrains.TryGetValue(ownerId, out terrainId) &&
                HarborIslandApi.IsTamedTerrain(terrainId))
            {
                return terrainId;
            }
            return SelectedTerrain;
        }

        public static void SetPlayerTerrain(string ownerId, string terrainId)
        {
            if (string.IsNullOrEmpty(ownerId) || !HarborIslandApi.IsTamedTerrain(terrainId)) return;
            PlayerTerrains[ownerId] = terrainId;
        }

        public static void SelectTerrain(string terrainId)
        {
            if (!HarborIslandApi.IsTamedTerrain(terrainId)) return;
            if (TamedIslandRestorationPlugin.SelectedTerrainId.Value == terrainId) return;
            TamedIslandRestorationPlugin.SelectedTerrainId.Value = terrainId;
            TamedIslandRestorationPlugin.SelectedTerrainId.ConfigFile.Save();
            TamedIslandRestorationPlugin.Log.LogInfo("Selected Tamed terrain " + terrainId);
        }

        public static void SelectTerrain(string ownerId, string terrainId)
        {
            if (!HarborIslandApi.IsTamedTerrain(terrainId)) return;
            SetPlayerTerrain(ownerId, terrainId);
            SelectTerrain(terrainId);
        }

        public static Messages.Region MakeRegion(string ownerId)
        {
            string terrainId = GetSelectedTerrain(ownerId);
            return new Messages.Region
            {
                Id = HarborIslandApi.GetTamedRegionId(terrainId),
                TerrainId = terrainId,
                TemplateId = HarborIslandApi.GetTamedRegionTemplateId(terrainId),
                Role = Role.Personal,
                Name = IslandDisplayName,
                CreatedAt = Now()
            };
        }

        public static PersonalRegion MakePersonalRegion(string ownerId)
        {
            return new PersonalRegion
            {
                Region = MakeRegion(ownerId),
                OwnerId = ownerId,
                PioneerExp = 0,
                AdmissionCategories = new LicenseCategory[0]
            };
        }

        public static EstateLicense MakeLicense(string ownerId)
        {
            string terrainId = GetSelectedTerrain(ownerId);
            int size = TamedEstateState.GetUnits(ownerId, terrainId).Count;
            return new EstateLicense
            {
                EstateId = "offline:tamed:estate:" + ownerId,
                Type = OwnerType.PersonalPlayer,
                OwnerId = ownerId,
                ActivatedAt = Now() - 1.0,
                DepositRunsOutAt = null,
                ExpiresAt = null,
                Deposit = null,
                AccessRights = null,
                Size = size,
                RegionId = HarborIslandApi.GetTamedRegionId(terrainId),
                Tile = TamedEstateState.GetAnchorTile(ownerId, terrainId),
                ProtectedUntil = null,
                CycleStartsAt = null,
                CycleEndsAt = null,
                RemovableAt = null
            };
        }

        internal static Point2 GetEstateTile(string terrainId)
        {
            if (terrainId == "pe10gr_2") return new Point2(176, 52);
            if (terrainId == "pe10gr_3") return new Point2(144, 32);
            if (terrainId == "pe10gr_4") return new Point2(164, 56);
            if (terrainId == "pe10gr_5") return new Point2(76, 32);
            return new Point2(60, 68);
        }

        public static PersonalRegionInfo MakePersonalRegionInfo(string ownerId)
        {
            string terrainId = GetSelectedTerrain(ownerId);
            EstateLicense? estate = TamedEstateState.IsDeclared(ownerId, terrainId)
                ? new EstateLicense?(MakeLicense(ownerId))
                : null;
            return new PersonalRegionInfo
            {
                PersonalRegion = MakePersonalRegion(ownerId),
                PersonalEstate = estate
            };
        }

        public static EstateLicenses MakeLicenses(string ownerId)
        {
            string terrainId = GetSelectedTerrain(ownerId);
            int size = TamedEstateState.GetUnits(ownerId, terrainId).Count;
            int largestSize = Math.Max(size, TamedPioneerState.GetMaximumEstateSize(ownerId));
            EstateLicense? estate = TamedEstateState.IsDeclared(ownerId, terrainId)
                ? new EstateLicense?(MakeLicense(ownerId))
                : null;
            return new EstateLicenses
            {
                UrbanEstate = null,
                LargestUrbanEstateSize = 0,
                PersonalEstate = estate,
                LargestPersonalEstateSize = largestSize,
                ClanEstate = null,
                LargestClanEstateSize = 0,
                ClanCargoWarphole = null,
                ClanCargoWarpholeVisitAvailableAt = 0.0,
                PersonalWarpholeTile = null,
                UrbanWarpholeTile = null
            };
        }

        private static double Now()
        {
            return (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string), typeof(OfflineConnection), typeof(World), typeof(PlayerContext), typeof(bool)
    })]
    internal static class TamedIslandBackendPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(string entityId, PlayerContext context, bool isLocalPlayer)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value || !isLocalPlayer || context == null)
            {
                return;
            }

            TamedIslandData.SetPlayerName(context.PlayerInfo.PlayerName);

            // Offline Player sends its initial Inventory from inside the original
            // constructor. Restore Pioneer-only server fields before that packet.
            // Tradable is owned by the separate Trade Available plugin.
            int changed = TamedPioneerItemData.NormalizeInventory(context);
            if (changed > 0)
            {
                TamedIslandRestorationPlugin.Log.LogInfo(
                    "Restored offline item server fields before inventory sync: owner=" +
                    entityId + ", items=" + changed);
            }
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(OfflinePlayer __instance, string entityId,
            OfflineConnection connection, World world, PlayerContext context, bool isLocalPlayer)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value || connection == null || !isLocalPlayer)
            {
                return;
            }

            if (context != null)
            {
                TamedIslandData.SetPlayerName(context.PlayerInfo.PlayerName);
            }

            string homeTerrainId = HarborIslandApi.GetHomeTamedTerrainId(__instance);
            if (string.IsNullOrEmpty(homeTerrainId) && world != null &&
                HarborIslandApi.IsCurrentTamedWorld(world))
            {
                homeTerrainId = HarborIslandApi.GetCurrentTamedTerrainId(world);
            }
            TamedIslandData.SetPlayerTerrain(entityId, homeTerrainId);

            connection.Recv<GetEstateLicenses>(delegate(GetEstateLicenses request, PacketHeader header)
            {
                // Domain is the normal entry into Expand/Reduce. Push the owned
                // cells before the license reply so EstatePage can resolve the
                // player's current tile to this estate immediately.
                TamedEstateState.SendGrid(__instance, entityId);
                __instance.Send<EstateLicenses>(TamedIslandData.MakeLicenses(entityId), header.Seq);
            });
            TamedPioneerBackend.Register(__instance, connection, context, entityId);
            connection.Recv<GetPersonalRegionInfo>(delegate(GetPersonalRegionInfo request, PacketHeader header)
            {
                __instance.Send<PersonalRegionInfo>(TamedIslandData.MakePersonalRegionInfo(entityId), header.Seq);
            });
            connection.Recv<RecommendPersonalRegion>(delegate(RecommendPersonalRegion request, PacketHeader header)
            {
                TamedIslandData.SelectTerrain(entityId, request.TemplateId);
                __instance.Send<PersonalRegion>(TamedIslandData.MakePersonalRegion(entityId), header.Seq);
            });
            connection.Recv<GetEstateLicenseById>(delegate(GetEstateLicenseById request, PacketHeader header)
            {
                if (TamedEstateState.IsDeclared(entityId, TamedIslandData.GetSelectedTerrain(entityId)))
                {
                    __instance.Send<EstateLicense>(TamedIslandData.MakeLicense(entityId), header.Seq);
                }
                else
                {
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });
            connection.Recv<DeclareEstate>(delegate(DeclareEstate request, PacketHeader header)
            {
                TamedEstateState.HandleDeclare(__instance, entityId, request, header);
            });
            connection.Recv<ExpandEstate>(delegate(ExpandEstate request, PacketHeader header)
            {
                TamedEstateState.HandleExpand(__instance, entityId, request, header);
            });
            connection.Recv<ShrinkEstate>(delegate(ShrinkEstate request, PacketHeader header)
            {
                TamedEstateState.HandleShrink(__instance, entityId, request, header);
            });
            connection.Recv<RemoveEstate>(delegate(RemoveEstate request, PacketHeader header)
            {
                TamedEstateState.HandleRemove(__instance, entityId, request);
            });
            connection.Recv<VisitEstate>(delegate(VisitEstate request, PacketHeader header)
            {
                if (request.OwnerType != OwnerType.PersonalPlayer) return;
                __instance.Send<OK>(default(OK), header.Seq);
                if (!HarborIslandApi.IsAtTamedHome(__instance))
                {
                    HarborIslandApi.SailToTamedIsland(__instance);
                }
            });
            connection.Recv<ReturnToEstate>(delegate(ReturnToEstate request, PacketHeader header)
            {
                if (request.OwnerType != OwnerType.PersonalPlayer) return;
                __instance.Send<OK>(default(OK), header.Seq);
                if (!HarborIslandApi.IsAtTamedHome(__instance))
                {
                    HarborIslandApi.SailToTamedIsland(__instance);
                }
            });

            TamedIslandRestorationPlugin.Log.LogInfo(
                "Registered Tamed Island backend for player " + entityId);
        }
    }
}
