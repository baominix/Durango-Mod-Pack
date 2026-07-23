using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Offline;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using HarmonyLib;
using InteractionData;
using L10N;
using Messages;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;
using PacketHeader = Durango.Network.PacketHeader;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    internal static class HarborRequestMode
    {
        private static bool _directSailing;
        public static void RequestDirectSailing() { _directSailing = true; }
        public static bool ConsumeDirectSailing() { bool value = _directSailing; _directSailing = false; return value; }
    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string), typeof(OfflineConnection), typeof(World), typeof(PlayerContext), typeof(bool)
    })]
    internal static class HarborPlayerBackendPatch
    {
        private static void Postfix(OfflinePlayer __instance, string entityId, OfflineConnection connection, World world, PlayerContext context, bool isLocalPlayer)
        {
            if (connection == null || !HarborSailingMapPlugin.Enabled.Value) return;

            HarborMapMarkers.Register(__instance, connection, world);

            connection.Recv<GetRoutes>(delegate(GetRoutes msg, PacketHeader header)
            {
                if (HarborRequestMode.ConsumeDirectSailing())
                {
                    HarborSailingSelector.Show(__instance, msg.Tile);
                }
                else
                {
                    __instance.Send<Routes>(HarborRoutes.MakeRoutes(), header.Seq);
                }
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
                bool restoredSavanna15 = msg.TemplateId == "ri15sa190710";
                __instance.Send<Messages.DiscoveryInfo>(new Messages.DiscoveryInfo
                {
                    TemplateId = msg.TemplateId,
                    BiocomNames = new Pair<string, bool>[]
                    {
                        new Pair<string, bool>(restoredSavanna15 ? "Crater" : "?", restoredSavanna15)
                    },
                    AnimalTypes = restoredSavanna15
                        ? new Pair<ushort, bool>[]
                        {
                            // ri15sa190710 land herds. 2042 is the closed-crater herd
                            // and is intentionally represented by the Crater entry.
                            new Pair<ushort, bool>(2037, true),
                            new Pair<ushort, bool>(2027, true),
                            new Pair<ushort, bool>(2039, true)
                        }
                        : new Pair<ushort, bool>[]
                        {
                            new Pair<ushort, bool>(0, false),
                            new Pair<ushort, bool>(1, false),
                            new Pair<ushort, bool>(2, false)
                        }
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
        public static void OpenDirectSailing(InteractionObject target)
        {
            if (target == null) return;
            HarborRequestMode.RequestDirectSailing();
            GameSystem<ExploreSystem>.Instance().RequestRoutes(target.EntityId, new Point2(target.Tile));
        }
    }

    // Restore the registered handler for Map; Original's method accidentally overwrites every registered handler.
    [HarmonyPatch(typeof(InteractionSystem), "GetInteractionHandler")]
    internal static class HarborInteractionHandlerPatch
    {
        private static readonly FieldInfo HandlersField = AccessTools.Field(typeof(InteractionSystem), "_interactionHandlers");

        private static void Postfix(InteractionSystem __instance, InteractionMenuData menu, ref InteractionSystem.InteractionHandler __result)
        {
            if (menu.Action == Interaction.SailingExplore)
            {
                __result = HarborInteraction.OpenDirectSailing;
                return;
            }
            if (menu.Action == Interaction.SailingRoutes && HandlersField != null)
            {
                Dictionary<int, InteractionSystem.InteractionHandler> handlers = HandlersField.GetValue(__instance) as Dictionary<int, InteractionSystem.InteractionHandler>;
                InteractionSystem.InteractionHandler handler;
                if (handlers != null && handlers.TryGetValue((int)Interaction.SailingRoutes, out handler) && handler != null)
                {
                    __result = handler;
                }
            }
        }
    }

    internal static class HarborSailingSelector
    {
        private const string Warning = "<alert_icon/> Harbor keeps a separate snapshot for every destination. The current home map is backed up before sailing.";

        public static void Show(OfflinePlayer player, Point2 portTile)
        {
            GenericSelector selector = UIManager.Popup.Tooltip<GenericSelector>();
            selector.ResetArguments();
            selector.SetTitle("Set Sail");
            List<Action> actions = new List<Action>();

            if (HarborRuntime.IsAwayFromHome(player))
            {
                selector.AddItem("Return to Home Island");
                actions.Add(delegate { ConfirmReturn(player); });
            }

            selector.AddItem("Tamed Islands");
            actions.Add(delegate { ShowTamedIslands(player); });
            selector.AddItem("Unstable Islands");
            actions.Add(delegate { ShowUnstableSeas(player); });

            selector.SetConfirmText("OK");
            selector.SetSelected(delegate(int index)
            {
                if (index >= 0 && index < actions.Count) actions[index]();
            });
            selector.Show();
        }

        private static void ShowTamedIslands(OfflinePlayer player)
        {
            ShowTargets(player, "Tamed Islands", HarborRoutes.GetTargetsForKind(HarborIslandKind.Tamed));
        }

        private static void ShowUnstableSeas(OfflinePlayer player)
        {
            GenericSelector selector = UIManager.Popup.Tooltip<GenericSelector>();
            selector.ResetArguments();
            selector.SetTitle("Unstable Islands");
            List<Action> actions = new List<Action>();
            string[] seas = HarborRoutes.GetSeaNames();
            for (int i = 0; i < seas.Length; i++)
            {
                string sea = seas[i];
                selector.AddItem(sea);
                actions.Add(delegate { ShowSea(player, sea); });
            }
            selector.SetConfirmText("OK");
            selector.SetSelected(delegate(int index)
            {
                if (index >= 0 && index < actions.Count) actions[index]();
            });
            selector.Show();
        }

        private static void ShowSea(OfflinePlayer player, string sea)
        {
            ShowTargets(player, sea, HarborRoutes.GetTargetsForSea(sea));
        }

        private static void ShowTargets(OfflinePlayer player, string title, SailTarget[] targets)
        {
            MessageBox.Button[] buttons = new MessageBox.Button[targets.Length + 1];
            for (int i = 0; i < targets.Length; i++)
            {
                buttons[i] = new MessageBox.Button("Sail to " + targets[i].Name, PresetButton.Style.Solid, null, false, PresetButton.Effect.None);
            }
            buttons[targets.Length] = "Cancel";
            UIManager.MessageBox.Show(title, Warning, delegate(int index)
            {
                if (index >= 0 && index < targets.Length && !HarborRuntime.Sail(player, targets[index]))
                {
                    UIManager.SystemMsg("Harbor travel failed. Check BepInEx/LogOutput.log.", 4f);
                }
            }, buttons);
        }

        private static void ConfirmReturn(OfflinePlayer player)
        {
            UIManager.MessageBox.Show("Return to Home Island", Warning, delegate(bool ok)
            {
                if (ok && !HarborRuntime.ReturnHome(player))
                {
                    UIManager.SystemMsg("Could not restore the Harbor home snapshot.", 4f);
                }
            }, "Return", "Cancel");
        }
    }

    [HarmonyPatch(typeof(T), "GetName", new Type[] { typeof(Enum) })]
    internal static class HarborInteractionNamePatch
    {
        private static void Postfix(Enum source, ref string __result)
        {
            if (!(source is Interaction)) return;
            Interaction interaction = (Interaction)source;
            if (interaction == Interaction.SailingExplore) __result = "Set Sail";
            else if (interaction == Interaction.SailingRoutes) __result = "Map";
        }
    }
}
