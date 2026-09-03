using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.Logic.Clusters;
using Durango.System;
using Durango.UI;
using Durango.UI.Control;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.PCCurrencyGroupRestoration
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.baominix.durango.original.cashshoprestoration",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class PCCurrencyGroupRestorationPlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "com.baominix.durango.original.pccurrencygrouprestoration";
        public const string PluginName =
            "PC Currency Group Restoration Plugin";
        public const string PluginVersion = "1.9.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> CloseButtonGap;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Keep the original mobile T-Stone and Warp Gem widgets active in Bag only.");
            CloseButtonGap = Config.Bind("Layout", "CloseButtonGap", 10f,
                "Gap in UI units between the right currency widget and the Bag Close button.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(PluginName + " loaded.");
        }

        private void OnDestroy()
        {
            if (_harmony == null) return;
            _harmony.UnpatchSelf();
            _harmony = null;
        }
    }

    // This is the original approach which previously made the mobile currency
    // strip appear on every PC page. CurrencyWidgetTweakerForPC disables the
    // retained mobile widgets during Awake. Skip that Awake only when the
    // tweaker belongs to InventoryGroup; every other PC page keeps the retail
    // behavior. CashShopRestorationPlugin's offline CurrencyWidget.MakeComponent
    // patch then builds the original PresetCurrencyWidget visuals normally.
    [HarmonyPatch(typeof(CurrencyWidgetTweakerForPC), "Awake")]
    internal static class BagOnlyMobileCurrencyPatch
    {
        private static bool _reported;

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(CurrencyWidgetTweakerForPC __instance)
        {
            if (!PCCurrencyGroupRestorationPlugin.Enabled.Value ||
                GameManager.ClusterMode == Mode.Online || __instance == null)
                return true;

            InventoryGroup bag = __instance.GetComponentInParent<InventoryGroup>();
            if (bag == null) return true;

            if (!_reported)
            {
                _reported = true;
                PCCurrencyGroupRestorationPlugin.Log.LogInfo(
                    "Kept original mobile currency widgets active for Bag only.");
            }
            return false;
        }
    }

    // InventoryGroup_PC keeps the mobile currency holders below the nested PC
    // title prefab. They therefore survive the PC tweaker bypass but sit over
    // the Bag tab row. Keep their existing horizontal layout and continuously
    // align only their vertical centre with the real UITitleWidget_PC. This
    // makes the holders follow the title bar on window/anchor changes without
    // cloning or re-parenting the live currency components.
    internal static class BagCurrencyTitleBarBinding
    {
        internal static void Ensure(InventoryGroup bag)
        {
            if (!PCCurrencyGroupRestorationPlugin.Enabled.Value ||
                GameManager.ClusterMode == Mode.Online || bag == null ||
                bag.GetComponent<BagCurrencyTitleBarBinder>() != null)
                return;

            BagCurrencyTitleBarBinder binder =
                bag.gameObject.AddComponent<BagCurrencyTitleBarBinder>();
            binder.Initialize(bag);
        }
    }

    // Open runs after the nested title and the Bag contents have initialized.
    // This is the primary binding point for an already-created Bag.
    [HarmonyPatch(typeof(InventoryGroup), "Open")]
    internal static class BagCurrencyTitleBarOpenPatch
    {
        private static void Postfix(InventoryGroup __instance)
        {
            BagCurrencyTitleBarBinding.Ensure(__instance);
        }
    }

    // CashShopRestoration supplies the offline MakeComponent implementation.
    // Its postfix still reaches this hook, so a late-created currency widget
    // can also install the binder without relying on Unity Start order.
    [HarmonyPatch(typeof(CurrencyWidget), "MakeComponent")]
    internal static class BagCurrencyComponentCreatedPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CurrencyWidget __instance)
        {
            if (__instance == null) return;
            BagCurrencyTitleBarBinding.Ensure(
                __instance.GetComponentInParent<InventoryGroup>());
        }
    }

    internal sealed class BagCurrencyTitleBarBinder : MonoBehaviour
    {
        private static readonly FieldInfo TitleWidgetField =
            AccessTools.Field(typeof(InventoryGroup), "_titleWidget");
        private static readonly FieldInfo CloseButtonField =
            AccessTools.Field(typeof(UITitleWidget), "_closeButton");

        private InventoryGroup _bag;
        private UITitleWidget _title;
        private CurrencyWidget _first;
        private CurrencyWidget _second;
        private UIWidget _closeButton;
        private bool _reported;

        internal void Initialize(InventoryGroup bag)
        {
            _bag = bag;
            ResolveObjects();
            AlignToTitleBar();
        }

        private void LateUpdate()
        {
            AlignToTitleBar();
        }

        private void AlignToTitleBar()
        {
            ResolveObjects();
            if (_title == null || _first == null || _second == null ||
                _bag == null)
                return;

            float currencyCenterY =
                (_first.transform.position.y + _second.transform.position.y) * 0.5f;
            float deltaY = _title.transform.position.y - currencyCenterY;

            Vector3 firstLocal =
                _title.transform.InverseTransformPoint(_first.transform.position);
            Vector3 secondLocal =
                _title.transform.InverseTransformPoint(_second.transform.position);

            firstLocal.y = 0f;
            secondLocal.y = 0f;
            _first.transform.position =
                _title.transform.TransformPoint(firstLocal);
            _second.transform.position =
                _title.transform.TransformPoint(secondLocal);

            AlignBeforeCloseButton();

            if (_reported) return;
            _reported = true;
            PCCurrencyGroupRestorationPlugin.Log.LogInfo(
                "Anchored visible Bag currencies to the PC title bar. deltaY=" +
                deltaY.ToString("F6") + ", closeGap=" +
                PCCurrencyGroupRestorationPlugin.CloseButtonGap.Value);
        }

        private void AlignBeforeCloseButton()
        {
            if (_closeButton == null)
                _closeButton = CloseButtonField.GetValue(_title) as UIWidget;
            if (_closeButton == null) return;

            UIWidget firstWidget = _first.GetComponent<UIWidget>();
            UIWidget secondWidget = _second.GetComponent<UIWidget>();
            if (firstWidget == null || secondWidget == null) return;

            float closeLeft = MinX(_closeButton.worldCorners);
            float currencyRight = Mathf.Max(
                MaxX(firstWidget.worldCorners), MaxX(secondWidget.worldCorners));
            float oneUiUnit = Mathf.Abs(
                _title.transform.TransformPoint(Vector3.right).x -
                _title.transform.TransformPoint(Vector3.zero).x);
            float desiredRight = closeLeft -
                PCCurrencyGroupRestorationPlugin.CloseButtonGap.Value * oneUiUnit;
            float deltaX = desiredRight - currencyRight;
            if (Mathf.Abs(deltaX) <= 0.00001f) return;

            Vector3 shift = Vector3.right * deltaX;
            _first.transform.position += shift;
            _second.transform.position += shift;
        }

        private static float MinX(Vector3[] corners)
        {
            float value = corners[0].x;
            for (int i = 1; i < corners.Length; i++)
                value = Mathf.Min(value, corners[i].x);
            return value;
        }

        private static float MaxX(Vector3[] corners)
        {
            float value = corners[0].x;
            for (int i = 1; i < corners.Length; i++)
                value = Mathf.Max(value, corners[i].x);
            return value;
        }

        private void ResolveObjects()
        {
            if (_bag == null) return;

            if (_title == null)
            {
                UITitle titleLink = TitleWidgetField.GetValue(_bag) as UITitle;
                if (titleLink != null) _title = titleLink.Object;
            }

            if (_first != null && _second != null) return;

            CurrencyWidget[] widgets =
                _bag.GetComponentsInChildren<CurrencyWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                CurrencyWidget widget = widgets[i];
                if (widget == null) continue;
                if (_first == null) _first = widget;
                else if (widget != _first)
                {
                    _second = widget;
                    break;
                }
            }
        }
    }
}
