using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Logic.Item;
using Durango.Offline;
using Durango.UI;
using Durango.UI.Control;
using HarmonyLib;
using L10N;
using Messages;
using Shared.Item;
using Yaml;

namespace Baominix.DurangoOriginal.CombatSystem.EquipmentPerformance
{
    internal static class EquipmentLevelRules
    {
        private static readonly MethodInfo SetOriginalLevel = AccessTools.PropertySetter(typeof(ItemData), "OriginalLevel");
        private static readonly List<Action> DeferredReplies = new List<Action>();

        internal static void PrepareMessage(ref Item item)
        {
            int originalLevel = Math.Max(item.Level, item.OriginalLevel);
            if (originalLevel <= 0)
            {
                return;
            }

            item.OriginalLevel = originalLevel;
            Prototype prototype = PrototypeYaml.GetItemPrototype(item.Prototype, originalLevel);
            if (!IsEquipment(item, prototype))
            {
                return;
            }

        }

        internal static bool CanEquip(ItemData item, out int requiredLevel, out int playerLevel)
        {
            requiredLevel = 1;
            playerLevel = 0;
            if (item == null || !TryGetPlayerLevel(out playerLevel))
            {
                return true;
            }

            Prototype prototype;
            if (!TryGetEquipmentPrototype(item, out prototype))
            {
                return true;
            }

            requiredLevel = Math.Max(1, prototype.MinLevel);
            return playerLevel >= requiredLevel;
        }

        internal static int GetPerformanceLevel(ItemData item)
        {
            if (item == null)
            {
                return 1;
            }

            int originalLevel = GetOriginalLevel(item);
            Prototype prototype;
            int playerLevel;
            if (!TryGetEquipmentPrototype(item, out prototype) || !TryGetPlayerLevel(out playerLevel))
            {
                return Math.Max(1, item.Level);
            }

            return GetEffectiveLevel(originalLevel, playerLevel);
        }

        internal static void RefreshForPlayerLevel()
        {
            if (!GameSystem<InventorySystem>.HasInstance())
            {
                return;
            }

            List<ItemData> items = GameSystem<InventorySystem>.Instance().PlayerItemList;
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                Prototype prototype;
                int playerLevel;
                if (!TryGetEquipmentPrototype(item, out prototype) || !TryGetPlayerLevel(out playerLevel))
                {
                    continue;
                }

                int originalLevel = GetOriginalLevel(item);
                if (item.OriginalLevel != originalLevel && SetOriginalLevel != null)
                {
                    SetOriginalLevel.Invoke(item, new object[] { originalLevel });
                    changed = true;
                }
                WeaponItemPerformance.Enrich(item);
            }

            if (changed && GameSystem<EquipSystem>.HasInstance())
            {
                EquipSystem equipSystem = GameSystem<EquipSystem>.Instance();
                GameSystem<InventorySystem>.Instance().UpdateEquipments(equipSystem.CurrentEquipPreset);
            }
        }

        internal static void NormalizeSavedItems(PlayerContext context)
        {
            if (context == null || context.InventoryItems == null)
            {
                return;
            }

            int normalized = 0;
            for (int i = 0; i < context.InventoryItems.Count; i++)
            {
                Item item = context.InventoryItems[i];
                if (item.OriginalLevel > 0 || item.Level <= 0)
                {
                    continue;
                }
                item.OriginalLevel = item.Level;
                context.InventoryItems[i] = item;
                normalized++;
            }

            if (normalized > 0 && DurangoCombatSystemPlugin.Log != null)
            {
                DurangoCombatSystemPlugin.Log.LogInfo("Normalized OriginalLevel for saved items: " + normalized);
            }
        }

        internal static void DeferReply(Action reply)
        {
            if (reply != null)
            {
                DeferredReplies.Add(reply);
            }
        }

        internal static void Tick()
        {
            if (DeferredReplies.Count == 0)
            {
                return;
            }

            Action[] replies = DeferredReplies.ToArray();
            DeferredReplies.Clear();
            for (int i = 0; i < replies.Length; i++)
            {
                try
                {
                    replies[i]();
                }
                catch (Exception exception)
                {
                    DurangoCombatSystemPlugin.Log.LogWarning("Deferred equip reply failed: " + exception.Message);
                }
            }
        }

        private static int GetOriginalLevel(ItemData item)
        {
            return Math.Max(1, Math.Max(item.Level, item.OriginalLevel));
        }

        private static int GetEffectiveLevel(int originalLevel, int playerLevel)
        {
            return Math.Max(1, Math.Min(originalLevel, playerLevel));
        }

        private static bool TryGetPlayerLevel(out int playerLevel)
        {
            playerLevel = 0;
            if (!GameSystem<StatisticsSystem>.HasInstance())
            {
                return false;
            }
            playerLevel = Math.Max(1, GameSystem<StatisticsSystem>.Instance().Level);
            return true;
        }

        private static bool TryGetEquipmentPrototype(ItemData item, out Prototype prototype)
        {
            prototype = null;
            if (item == null)
            {
                return false;
            }

            int originalLevel = GetOriginalLevel(item);
            prototype = PrototypeYaml.GetItemPrototype(item.PrototypeId, originalLevel) ?? item.Prototype;
            if (prototype == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(item.GetStringAttribute("slot")))
            {
                return true;
            }
            return IsEquipmentCategory(prototype.Category);
        }

        private static bool IsEquipment(Item item, Prototype prototype)
        {
            if (prototype == null)
            {
                return false;
            }

            if (item.Performance != null)
            {
                for (int i = 0; i < item.Performance.Length; i++)
                {
                    string id = item.Performance[i].Id;
                    if (id == "weapon" || id == "armor")
                    {
                        return true;
                    }
                }
            }
            return IsEquipmentCategory(prototype.Category);
        }

        private static bool IsEquipmentCategory(string category)
        {
            return string.Equals(category, "weapon/tool", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, "clothing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, "accessories", StringComparison.OrdinalIgnoreCase);
        }
    }

    [HarmonyPatch(typeof(ItemData), "Set", new Type[] { typeof(Item) })]
    internal static class ItemDataEquipmentLevelPatch
    {
        private static void Prefix(ref Item itemInfo)
        {
            EquipmentLevelRules.PrepareMessage(ref itemInfo);
        }
    }

    [HarmonyPatch(typeof(ItemInfoView), "Set", new Type[] { typeof(ItemData), typeof(string) })]
    internal static class ItemInfoViewEquipmentLevelReductionPatch
    {
        private static void Postfix(ItemInfoView __instance, ItemData item)
        {
            if (__instance == null || item == null)
            {
                return;
            }

            int originalLevel = Math.Max(item.Level, item.OriginalLevel);
            int effectiveLevel = EquipmentLevelRules.GetPerformanceLevel(item);
            if (effectiveLevel >= originalLevel)
            {
                return;
            }

            UIWidget modifierWidget = Traverse.Create(__instance).Field("_levelModifierWidget").GetValue<UIWidget>();
            UISpriteLabel modifierLabel = Traverse.Create(__instance).Field("_levelModifierLabel").GetValue<UISpriteLabel>();
            if (modifierWidget == null || modifierLabel == null)
            {
                return;
            }

            modifierWidget.gameObject.SetActive(true);
            modifierLabel.text = T._("[icon=img_pet_arrow_down] {0:lv:}", new object[]
            {
                originalLevel - effectiveLevel
            });
            Traverse.Create(__instance).Field("_isLevelDown").SetValue(true);
            Traverse.Create(__instance).Field("_isDirtyLayout").SetValue(true);
        }
    }

    [HarmonyPatch(typeof(EquipSystem), "EquipItem", new Type[]
    {
        typeof(EquipSlotType),
        typeof(string),
        typeof(ItemData),
        typeof(Action)
    })]
    internal static class EquipItemRequiredLevelPatch
    {
        private static bool Prefix(ItemData item, Action onReply)
        {
            int requiredLevel;
            int playerLevel;
            if (item == null || EquipmentLevelRules.CanEquip(item, out requiredLevel, out playerLevel))
            {
                return true;
            }

            UIManager.SystemMsg(
                T._("장비를 착용할 수 없습니다") + " (" + T._("착용 가능 레벨") + " " + requiredLevel + ")",
                3f);
            EquipmentLevelRules.DeferReply(onReply);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerContext), "Initialize")]
    internal static class PlayerContextEquipmentOriginalLevelPatch
    {
        private static void Postfix(PlayerContext __instance)
        {
            EquipmentLevelRules.NormalizeSavedItems(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "HandleEquipMsg")]
    internal static class OfflinePlayerRequiredLevelPatch
    {
        private static bool Prefix(Player __instance, Equip msg, uint headerSeq, PlayerContext ____context)
        {
            if (msg.Action != "equip" || ____context == null || ____context.InventoryItems == null)
            {
                return true;
            }

            int index = ____context.InventoryItems.FindIndex(delegate(Item candidate)
            {
                return candidate.Id == msg.ItemId;
            });
            if (index < 0)
            {
                return true;
            }

            Item item = ____context.InventoryItems[index];
            int originalLevel = Math.Max(item.Level, item.OriginalLevel);
            Prototype prototype = PrototypeYaml.GetItemPrototype(item.Prototype, originalLevel);
            int playerLevel = ____context.PlayerInfo == null ? 1 : Math.Max(1, ____context.PlayerInfo.PlayerLevel);
            if (prototype == null || playerLevel >= Math.Max(1, prototype.MinLevel))
            {
                return true;
            }

            __instance.Send<Messages.Error>(default(Messages.Error), headerSeq);
            if (DurangoCombatSystemPlugin.Log != null)
            {
                DurangoCombatSystemPlugin.Log.LogInfo(
                    "Rejected under-level equipment request: item=" + item.Prototype +
                    " required=" + prototype.MinLevel +
                    " player=" + playerLevel);
            }
            return false;
        }
    }
}
