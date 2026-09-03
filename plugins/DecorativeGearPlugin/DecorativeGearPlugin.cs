using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic.Item;
using Durango.Offline;
using Durango.UI;
using Durango.Utils.Extensions;
using HarmonyLib;
using Messages;
using Shared.Item;
using UnityEngine;
using Yaml;

namespace BaoMinix.Durango.DecorativeGear
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "com.baominix.durango.original.logcontrol",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DecorativeGearPlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "com.baominix.durango.original.decorativegear";
        public const string PluginName = "DecorativeGearPlugin";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(DecorativeGearPlugin).Assembly);
            Logger.LogInfo(
                "Offline decorative gear presets, appearance and persistence enabled.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }

    internal sealed class DecorativeGearState
    {
        public string HeadItemId = string.Empty;
        public string BodyItemId = string.Empty;
    }

    internal static class DecorativeGearRuntime
    {
        private const string StorageKey = "decorative_gear.avatar.v1";
        private const string AvatarTag = "equipment_avatar";

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly MethodInfo SendEquipmentsMethod =
            typeof(Player).GetMethod("SendEquipments", InstanceFlags);

        private static readonly MethodInfo ContextChangedMethod =
            typeof(Player).GetMethod("OnContextChanged", InstanceFlags);

        private static readonly FieldInfo WorldField =
            AccessTools.Field(typeof(Player), "_world");

        internal static DecorativeGearState Load(PlayerContext context)
        {
            DecorativeGearState state = new DecorativeGearState();
            if (context == null || context.Storage == null)
            {
                return state;
            }

            byte[] data;
            if (!context.Storage.TryGetValue(StorageKey, out data) ||
                data == null || data.Length == 0)
            {
                return state;
            }

            try
            {
                string[] values = Encoding.UTF8.GetString(data)
                    .Replace("\r", string.Empty)
                    .Split('\n');
                if (values.Length >= 3 && values[0] == "v1")
                {
                    state.HeadItemId = values[1] ?? string.Empty;
                    state.BodyItemId = values[2] ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                DecorativeGearPlugin.Log.LogWarning(
                    "Could not read decorative gear state: " + ex.Message);
            }
            return state;
        }

        internal static void Save(PlayerContext context,
            DecorativeGearState state)
        {
            if (context == null)
            {
                return;
            }
            if (context.Storage == null)
            {
                context.Storage = new Dictionary<string, byte[]>();
            }

            string text = "v1\n" +
                (state.HeadItemId ?? string.Empty) + "\n" +
                (state.BodyItemId ?? string.Empty);
            context.Storage[StorageKey] = Encoding.UTF8.GetBytes(text);
        }

        internal static bool TryFindAvatarItem(PlayerContext context,
            string itemId, out Item item, out string slot)
        {
            item = default(Item);
            slot = null;
            if (context == null || context.InventoryItems == null ||
                string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            int index = context.InventoryItems.FindIndex(delegate(Item candidate)
            {
                return candidate.Id == itemId;
            });
            if (index < 0)
            {
                return false;
            }

            item = context.InventoryItems[index];
            Prototype prototype = PrototypeYaml.GetItemPrototype(
                item.Prototype, Math.Max(1, Math.Max(item.Level, item.OriginalLevel))) ??
                PrototypeYaml.GetItemPrototype(item.Prototype);
            if (prototype == null || prototype.Tags == null ||
                !prototype.Tags.ContainsKey(AvatarTag))
            {
                return false;
            }

            PerformanceYaml.Armor armor =
                PerformanceYaml.GetArmor(item.Prototype);
            if (armor == null)
            {
                return false;
            }

            slot = NormalizeSlot(armor.Slot);
            return slot == "head" || slot == "body";
        }

        internal static bool HandleAvatarEquip(Player player,
            PlayerContext context, Equip message, uint replySequence)
        {
            string requestedSlot = NormalizeSlot(message.SlotName);
            if (requestedSlot != "head" && requestedSlot != "body")
            {
                SendError(player, replySequence);
                return false;
            }

            DecorativeGearState state = Load(context);
            if (message.Action == "equip")
            {
                Item item;
                string actualSlot;
                if (!TryFindAvatarItem(
                    context, message.ItemId, out item, out actualSlot) ||
                    actualSlot != requestedSlot)
                {
                    SendError(player, replySequence);
                    return false;
                }

                if (actualSlot == "head")
                {
                    state.HeadItemId = item.Id;
                }
                else
                {
                    state.BodyItemId = item.Id;
                }
            }
            else if (message.Action == "unequip")
            {
                if (requestedSlot == "head")
                {
                    state.HeadItemId = string.Empty;
                }
                else
                {
                    state.BodyItemId = string.Empty;
                }
            }
            else
            {
                SendError(player, replySequence);
                return false;
            }

            Save(context, state);
            SendEquipments(player, replySequence);
            BroadcastDisplay(player, context);
            NotifyContextChanged(player);
            return true;
        }

        internal static void AddAvatarPreset(PlayerContext context,
            ref Equipments equipments)
        {
            if (context == null)
            {
                return;
            }

            DecorativeGearState state = Load(context);
            bool changed = false;
            Dictionary<string, Item> avatarItems =
                new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

            Item head;
            string headSlot;
            if (!string.IsNullOrEmpty(state.HeadItemId))
            {
                if (TryFindAvatarItem(
                    context, state.HeadItemId, out head, out headSlot) &&
                    headSlot == "head")
                {
                    avatarItems["head"] = head;
                }
                else
                {
                    state.HeadItemId = string.Empty;
                    changed = true;
                }
            }

            Item body;
            string bodySlot;
            if (!string.IsNullOrEmpty(state.BodyItemId))
            {
                if (TryFindAvatarItem(
                    context, state.BodyItemId, out body, out bodySlot) &&
                    bodySlot == "body")
                {
                    avatarItems["body"] = body;
                }
                else
                {
                    state.BodyItemId = string.Empty;
                    changed = true;
                }
            }

            EquipmentSlot avatar = default(EquipmentSlot);
            avatar.ItemSlots = avatarItems;
            avatar.IsLocked = false;
            avatar.UnlockSince = null;
            avatar.UnlockUntil = null;
            avatar.TitleId = string.Empty;

            if (equipments.Presets == null)
            {
                equipments.Presets =
                    new Dictionary<EquipSlotType, EquipmentSlot>();
            }
            equipments.Presets[EquipSlotType.Avatar] = avatar;

            ApplyAppearance(context, avatarItems);

            if (changed)
            {
                Save(context, state);
                context.Save();
            }
        }

        private static void ApplyAppearance(PlayerContext context,
            Dictionary<string, Item> avatarItems)
        {
            PlayerDisplay display = context.AppearPlayer.Display;
            bool isMale = context.AppearPlayer.IsMale();

            Item body;
            if (avatarItems.TryGetValue("body", out body))
            {
                PerformanceYaml.Armor armor =
                    PerformanceYaml.GetArmor(body.Prototype);
                if (armor != null)
                {
                    display.Body = isMale ? armor.MaleModel : armor.FemaleModel;
                    display.BodyColor = MakeColors(body);
                }
            }

            Item head;
            if (avatarItems.TryGetValue("head", out head))
            {
                PerformanceYaml.Armor armor =
                    PerformanceYaml.GetArmor(head.Prototype);
                if (armor != null)
                {
                    display.Head = isMale ? armor.MaleModel : armor.FemaleModel;
                    display.HeadColor = MakeColors(head);
                }
            }

            context.AppearPlayer.Display = display;
        }

        private static string[] MakeColors(Item item)
        {
            return new string[3]
            {
                item.ColorR,
                item.ColorG,
                item.ColorB
            };
        }

        private static string NormalizeSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot))
            {
                return string.Empty;
            }
            slot = slot.Trim().ToLowerInvariant();
            return slot == "hoody" ? "body" : slot;
        }

        private static void SendEquipments(Player player, uint replySequence)
        {
            if (SendEquipmentsMethod != null)
            {
                SendEquipmentsMethod.Invoke(
                    player, new object[] { replySequence });
            }
            else
            {
                SendError(player, replySequence);
            }
        }

        private static void SendError(Player player, uint replySequence)
        {
            player.Send<Messages.Error>(
                default(Messages.Error), replySequence);
        }

        private static void BroadcastDisplay(Player player,
            PlayerContext context)
        {
            if (WorldField == null || context == null)
            {
                return;
            }
            World world = WorldField.GetValue(player) as World;
            if (world != null)
            {
                world.BroadCast(context.AppearPlayer.Display);
            }
        }

        private static void NotifyContextChanged(Player player)
        {
            if (ContextChangedMethod != null)
            {
                ContextChangedMethod.Invoke(player, null);
            }
        }
    }

    [HarmonyPatch(typeof(Player), "HandleEquipMsg")]
    internal static class OfflineAvatarEquipPatch
    {
        private static bool Prefix(Player __instance, Equip msg,
            uint headerSeq, PlayerContext ____context)
        {
            if (msg.SlotType != EquipSlotType.Avatar)
            {
                return true;
            }

            try
            {
                DecorativeGearRuntime.HandleAvatarEquip(
                    __instance, ____context, msg, headerSeq);
            }
            catch (Exception ex)
            {
                DecorativeGearPlugin.Log.LogError(
                    "Decorative gear request failed: " + ex);
                __instance.Send<Messages.Error>(
                    default(Messages.Error), headerSeq);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), "UpdateEquipments")]
    internal static class OfflineAvatarPresetPatch
    {
        private static void Postfix(PlayerContext ____context,
            ref Equipments __result)
        {
            try
            {
                DecorativeGearRuntime.AddAvatarPreset(
                    ____context, ref __result);
            }
            catch (Exception ex)
            {
                DecorativeGearPlugin.Log.LogError(
                    "Could not build decorative gear preset: " + ex);
            }
        }
    }

    // The retail client deliberately showed avatar items in the normal gear
    // list while running in its legacy Offline mode. Once the Avatar preset is
    // restored, keep the two equipment lists separate just as in Online mode.
    [HarmonyPatch(typeof(EquipWidgetBase), "RefreshItemList")]
    internal static class DecorativeGearListSeparationPatch
    {
        private static bool Prefix(EquipWidgetBase __instance,
            ItemList ____itemList, GameObject ____emptyItemList,
            bool ____waitForChangingPreset)
        {
            if (__instance.SelectedEquipPreset == EquipSlotType.Avatar)
            {
                return true;
            }

            EquipSlotType preset = __instance.SelectedEquipPreset;
            EquipSystem.Slot selectedSlot = __instance.SelectedSlot;
            bool usable = preset != EquipSlotType.Invalid &&
                selectedSlot != EquipSystem.Slot.Invalid &&
                !____waitForChangingPreset &&
                !GameSystem<EquipSystem>.Instance().IsLockedPreset(preset);
            if (!usable)
            {
                ____itemList.gameObject.SetActive(false);
                return false;
            }

            Predicate<ItemData> slotFilter;
            if (selectedSlot == EquipSystem.Slot.Main)
            {
                slotFilter = delegate(ItemData data)
                {
                    return data.HasAttribute("slot", "main") ||
                        data.HasAttribute("slot", "both");
                };
            }
            else if (selectedSlot == EquipSystem.Slot.Body)
            {
                slotFilter = delegate(ItemData data)
                {
                    return data.HasAttribute("slot", "body") ||
                        data.HasAttribute("slot", "hoody");
                };
            }
            else
            {
                string slot = selectedSlot.ToString().ToLowerInvariant();
                slotFilter = delegate(ItemData data)
                {
                    return data.HasAttribute("slot", slot);
                };
            }

            ____itemList.DeselectAllItems(false);
            ____itemList.SetItemList(
                GameSystem<InventorySystem>.Instance().PlayerItemList,
                delegate(ItemData data)
                {
                    return !data.HasTag("equipment_avatar") &&
                        slotFilter(data);
                },
                null,
                null);
            int usableCount = ____itemList.UsableCount;
            ____emptyItemList.SetActive(usableCount == 0);
            ____itemList.gameObject.SetActive(usableCount > 0);
            ____itemList.Reposition(false, false);
            return false;
        }
    }
}
