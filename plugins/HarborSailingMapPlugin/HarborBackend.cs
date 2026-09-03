using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Offline;
using Durango.UI;
using HarmonyLib;
using InteractionData;
using L10N;
using Messages;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;
using PacketHeader = Durango.Network.PacketHeader;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string), typeof(OfflineConnection), typeof(World), typeof(PlayerContext), typeof(bool)
    })]
    internal static class HarborPlayerBackendPatch
    {
        private static void Postfix(OfflinePlayer __instance, string entityId, OfflineConnection connection, World world, PlayerContext context, bool isLocalPlayer)
        {
            if (connection == null || !HarborSailingMapPlugin.Enabled.Value) return;
            if (isLocalPlayer) HarborRuntime.BindLocalPlayer(__instance);

            HarborMapMarkers.Register(__instance, connection, world);

            connection.Recv<GetRoutes>(delegate(GetRoutes msg, PacketHeader header)
            {
                __instance.Send<Routes>(HarborRoutes.MakeRoutes(), header.Seq);
            });
            connection.Recv<GetRegion>(delegate(GetRegion msg, PacketHeader header)
            {
                Messages.Region region;
                if (HarborRoutes.TryMakeRegion(msg.RegionId, out region)) __instance.Send<Messages.Region>(region, header.Seq);
            });
            connection.Recv<GetArchipelago>(delegate(GetArchipelago msg, PacketHeader header)
            {
                Messages.Archipelago archipelago;
                if (HarborRoutes.TryMakeArchipelago(msg.ArchipelagoId, out archipelago)) __instance.Send<Messages.Archipelago>(archipelago, header.Seq);
            });
            connection.Recv<GetDiscoveryInfo>(delegate(GetDiscoveryInfo msg, PacketHeader header)
            {
                if (!HarborRoutes.IsKnownRegionTemplate(msg.TemplateId)) return;
                Messages.DiscoveryInfo info;
                if (HarborDiscoveryCatalog.TryCreate(msg.TemplateId, out info))
                {
                    __instance.Send<Messages.DiscoveryInfo>(info, header.Seq);
                    return;
                }
                __instance.Send<Messages.DiscoveryInfo>(new Messages.DiscoveryInfo
                {
                    TemplateId = msg.TemplateId,
                    BiocomNames = new Pair<string, bool>[0],
                    AnimalTypes = new Pair<ushort, bool>[0]
                }, header.Seq);
            });
            connection.Recv<TravelByRegion>(delegate(TravelByRegion msg, PacketHeader header)
            {
                Travel(__instance, msg.RegionId);
            });
            connection.Recv<TravelByRegionInArchipelago>(delegate(TravelByRegionInArchipelago msg, PacketHeader header)
            {
                Travel(__instance, msg.RegionId);
            });
        }

        private static void Travel(OfflinePlayer player, string regionId)
        {
            SailTarget target;
            if (HarborRoutes.TryGetTravelTarget(regionId, out target)) HarborRuntime.Sail(player, target);
        }
    }

    internal static class HarborInteraction
    {
        public static void OpenPersonalIsland(InteractionObject target)
        {
            OfflinePlayer player = HarborRuntime.GetLocalPlayer();
            if (player != null && HarborIslandApi.IsAtTamedHome(player))
            {
                return;
            }
            if (player == null || !HarborIslandApi.SailToTamedIsland(player))
            {
                UIManager.SystemMsg(HarborLocalization.Get("open_personal_failed"), 4f);
            }
        }

        public static void OpenReturnToExploring(InteractionObject target)
        {
            OfflinePlayer player = HarborRuntime.GetLocalPlayer();
            if (player == null || !HarborRuntime.CanReturnToExploring(player))
            {
                UIManager.SystemMsg(HarborLocalization.Get("no_unstable"), 4f);
                return;
            }
            HarborSailingSelector.ConfirmReturnToExploring(player);
        }
    }

    // Original's method accidentally overwrites registered handlers. Bind the
    // three restored Harbor actions directly to their offline implementations.
    [HarmonyPatch(typeof(InteractionSystem), "GetInteractionHandler")]
    internal static class HarborInteractionHandlerPatch
    {
        private static readonly FieldInfo HandlersField =
            AccessTools.Field(typeof(InteractionSystem), "_interactionHandlers");

        private static void Postfix(InteractionSystem __instance, InteractionMenuData menu, ref InteractionSystem.InteractionHandler __result)
        {
            if (menu.Action == Interaction.SailingRoutes && HandlersField != null)
            {
                Dictionary<int, InteractionSystem.InteractionHandler> handlers =
                    HandlersField.GetValue(__instance) as
                    Dictionary<int, InteractionSystem.InteractionHandler>;
                InteractionSystem.InteractionHandler handler;
                if (handlers != null &&
                    handlers.TryGetValue((int)Interaction.SailingRoutes, out handler) &&
                    handler != null)
                {
                    __result = handler;
                }
                return;
            }
            if (menu.Action == Interaction.SailingPersonalRegion)
            {
                __result = HarborInteraction.OpenPersonalIsland;
                return;
            }
            if (menu.Action == Interaction.SailingBack)
            {
                __result = HarborInteraction.OpenReturnToExploring;
            }
        }
    }

    internal static class HarborSailingSelector
    {
        private static string ReturnWarning
        {
            get { return HarborLocalization.Get("return_warning"); }
        }

        internal static void ConfirmReturnToExploring(OfflinePlayer player)
        {
            UIManager.MessageBox.Show(Interaction.SailingBack.GetName(), ReturnWarning, delegate(bool ok)
            {
                if (ok && !HarborRuntime.ReturnToExploring(player))
                {
                    UIManager.SystemMsg(HarborLocalization.Get("restore_failed"), 4f);
                }
            }, HarborLocalization.Get("return"), HarborLocalization.Get("cancel"));
        }
    }

    // SailingRoutes is the real route-map action. Reuse SailingExplore's
    // original localized label so the button reads "Set Sail" in every locale.
    [HarmonyPatch(typeof(T), "GetName", new Type[] { typeof(Enum) })]
    internal static class HarborInteractionNamePatch
    {
        private static void Postfix(Enum source, ref string __result)
        {
            if (source is Interaction &&
                (Interaction)source == Interaction.SailingRoutes)
            {
                __result = Interaction.SailingExplore.GetName();
            }
        }
    }
}
