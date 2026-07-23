using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.Logic.Market;
using Durango.Offline;
using Durango.System;
using Durango.UI;
using Durango.UI.Control;
using HarmonyLib;
using L10N;
using Messages;
using Shared.Economy;
using Shared.Market;
using UnityEngine;
using Yaml.Util;

namespace BaoX.DurangoOriginal.IslandMarketEnable
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class IslandMarketEnablePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.islandmarketenable";
        public const string PluginName = "Island Market Enable Plugin";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(IslandMarketEnablePlugin).Assembly);
            Logger.LogInfo("IslandMarketEnablePlugin loaded");
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

    internal static class IslandMarketRuntime
    {
        private const string GameModePrefKey = "baox_select_game_mode";
        private const string LastClusterPrefKey = "last_selected_cluster_key";
        private const string CreativeKey = "free_offline";
        private const string SingleMultiKey = "single_multi_offline";

        internal static bool ShouldForceMarket()
        {
            return GameManager.ClusterMode == Mode.Offline || GameManager.ClusterMode == Mode.Editable;
        }

        internal static bool IsSingleMultiMode()
        {
            string mode = Preferences.GetString(GameModePrefKey, string.Empty, Preferences.Level.Device);
            if (string.IsNullOrEmpty(mode))
            {
                mode = Preferences.GetString(LastClusterPrefKey, string.Empty, Preferences.Level.Device);
            }

            if (mode == SingleMultiKey)
            {
                return true;
            }
            if (mode == CreativeKey)
            {
                return false;
            }
            return GameManager.ClusterMode == Mode.Offline;
        }

        internal static void EnableMarket(MenuSystem menuSystem)
        {
            if (menuSystem == null || !ShouldForceMarket())
            {
                return;
            }

            menuSystem.EnableMenu(MenuType.Market, true, false);
        }

        internal static void RepairMarketTabs(MarketGroup marketGroup)
        {
            if (marketGroup == null || !ShouldForceMarket())
            {
                return;
            }

            try
            {
                IconTabList menuList = GetField<IconTabList>(marketGroup, "_menuList");
                if (menuList == null)
                {
                    return;
                }

                MonoBehaviour linker = GetField<MonoBehaviour>(marketGroup, "_menuLinker");
                if (linker != null)
                {
                    linker.gameObject.SetActive(true);
                }
                menuList.gameObject.SetActive(true);

                MarketGroup.Menu[] menus = (MarketGroup.Menu[])Enum.GetValues(typeof(MarketGroup.Menu));
                SetField(marketGroup, "_menus", menus);

                menuList.BeginLoad();
                for (int i = 0; i < menus.Length; i++)
                {
                    MarketGroup.Menu menu = menus[i];
                    menuList.Add(IconMap.Get(menu, null), menu.GetName());
                }
                menuList.EndLoad();

                MethodInfo method = typeof(MarketGroup).GetMethod("MenuSelected", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null)
                {
                    Action<int> handler = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), marketGroup, method);
                    menuList.Clicked -= handler;
                    menuList.Clicked += handler;
                }

                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogInfo("Island Market tabs repaired for offline/editable mode");
                }
            }
            catch (Exception ex)
            {
                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogWarning("RepairMarketTabs failed: " + ex);
                }
            }
        }

        internal static bool InitMarketCategoriesWithoutOfflineFilter(MarketCategoriesWidget widget)
        {
            if (widget == null || !ShouldForceMarket())
            {
                return true;
            }

            try
            {
                bool isInit = GetValue<bool>(widget, "_isInit");
                if (isInit)
                {
                    return false;
                }

                SetField(widget, "_isInit", true);
                Category[] categoryList = GameSystem<MarketSystem>.Instance().CategoryYamlData;
                SetField(widget, "_categoryList", categoryList);
                if (categoryList == null)
                {
                    SetField(widget, "_isInit", false);
                    return false;
                }

                KGridScrollView categories = GetField<KGridScrollView>(widget, "_categories");
                if (categories == null)
                {
                    return false;
                }

                MethodInfo onInitNodes = typeof(MarketCategoriesWidget).GetMethod("OnInitNodes", BindingFlags.Instance | BindingFlags.NonPublic);
                Action<GameObject> initializer = null;
                if (onInitNodes != null)
                {
                    initializer = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), widget, onInitNodes);
                }

                ListObjectPool nodes = categories.Nodes;
                nodes.Init(initializer, null);
                nodes.Set(categoryList.Length + 2);

                for (int i = 0; i < categoryList.Length; i++)
                {
                    GameObject go = nodes[i + 2];
                    Category category = categoryList[i];
                    go.transform.Find("name").GetComponent<UILabel>().text = category.MainCategory.Name;
                    go.transform.Find("icon").GetComponent<UISprite>().spriteName = category.MainCategory.Icon;
                }

                string viewAllText = GetValue<string>(widget, "_viewAllText");
                string searchText = GetValue<string>(widget, "_searchText");
                SpriteData viewAllIcon = GetValue<SpriteData>(widget, "_viewAllIcon");
                SpriteData searchIcon = GetValue<SpriteData>(widget, "_searchIcon");

                nodes[0].transform.Find("name").GetComponent<UILabel>().text = T._(viewAllText);
                nodes[0].transform.Find("icon").GetComponent<UISprite>().spriteName = viewAllIcon.sprite;
                nodes[1].transform.Find("name").GetComponent<UILabel>().text = T._(searchText);
                nodes[1].transform.Find("icon").GetComponent<UISprite>().spriteName = searchIcon.sprite;
                categories.ResetPosition();

                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogInfo("Island Market category filter disabled for offline/editable mode. categories=" + categoryList.Length);
                }
            }
            catch (Exception ex)
            {
                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogWarning("InitMarketCategoriesWithoutOfflineFilter failed: " + ex);
                }
            }

            return false;
        }

        internal static bool ConfirmOfflineBuy(CommodityListWidget widget)
        {
            if (widget == null || !ShouldForceMarket() || !IsSingleMultiMode())
            {
                return true;
            }

            try
            {
                CommodityList commodityList = GetField<CommodityList>(widget, "_commodityList");
                if (commodityList == null)
                {
                    return false;
                }

                Durango.Logic.Market.Commodity selected = commodityList.Selected;
                if (selected == null)
                {
                    return false;
                }

                ItemData item = selected.GetItem();
                if (item == null)
                {
                    return false;
                }

                MethodInfo boughtMethod = typeof(CommodityListWidget).GetMethod("CommodityBought", BindingFlags.Instance | BindingFlags.NonPublic);
                if (boughtMethod == null)
                {
                    return false;
                }

                Action<bool> bought = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), widget, boughtMethod);
                UIManager.MessageBox.Show(T._("<t_stone> {0:으로} <em>{1}</em>{1:-을} 구매합니다", new object[]
                {
                    selected.Price.ToString("N0", T.Culture),
                    item.Name
                }), bought, null, null);
            }
            catch (Exception ex)
            {
                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogWarning("ConfirmOfflineBuy failed: " + ex);
                }
            }

            return false;
        }

        internal static bool ForceCommodityListOnlineColumns(CommodityList list)
        {
            if (list == null || !ShouldForceMarket())
            {
                return true;
            }

            try
            {
                GameObject[] onlyOnline = GetField<GameObject[]>(list, "_onlyOnline");
                if (onlyOnline != null)
                {
                    for (int i = 0; i < onlyOnline.Length; i++)
                    {
                        if (onlyOnline[i] != null)
                        {
                            onlyOnline[i].SetActive(true);
                        }
                    }
                }

                RectLayoutComponent headerLayout = GetField<RectLayoutComponent>(list, "_headerLayout");
                if (headerLayout != null)
                {
                    headerLayout.UpdateLayout();
                    UIUtility.UpdateAnchors(headerLayout.transform);
                }
            }
            catch (Exception ex)
            {
                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogWarning("ForceCommodityListOnlineColumns failed: " + ex);
                }
            }

            return false;
        }

        internal static bool ForceCommodityNodeOnlineColumns(CommodityNode node)
        {
            if (node == null || !ShouldForceMarket())
            {
                return true;
            }

            try
            {
                GameObject[] onlyOnline = GetField<GameObject[]>(node, "_onlyOnline");
                if (onlyOnline != null)
                {
                    for (int i = 0; i < onlyOnline.Length; i++)
                    {
                        if (onlyOnline[i] != null)
                        {
                            onlyOnline[i].SetActive(true);
                        }
                    }
                }

                RectLayout layout = GetField<RectLayout>(node, "_layout");
                if (layout != null)
                {
                    layout.UpdateLayout();
                    UIUtility.UpdateAnchors(node.transform);
                }
            }
            catch (Exception ex)
            {
                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogWarning("ForceCommodityNodeOnlineColumns failed: " + ex);
                }
            }

            return false;
        }

        internal static void ForceCommodityBottomBarBuy(CommodityListBottomBar bottomBar, Commodity commodity, Action<Commodity> favoriteChanged)
        {
            if (bottomBar == null || commodity == null || !ShouldForceMarket() || !IsSingleMultiMode())
            {
                return;
            }

            try
            {
                SelectableButton buyButton = GetField<SelectableButton>(bottomBar, "_buyButton");
                if (buyButton != null)
                {
                    buyButton.Text = string.Format(T._("{0} 구매"), Durango.Logic.Item.Inventory.CurrencyFormat(commodity.Price, commodity.CurrencyType));
                    buyButton.Disabled = (commodity.Price > InventorySystem.Wallet.GetBalance(commodity.CurrencyType));
                }

                MarketFavoritesButton favoritesButton = GetField<MarketFavoritesButton>(bottomBar, "_favoritesButton");
                if (favoritesButton != null)
                {
                    favoritesButton.Set(commodity, favoriteChanged);
                }
            }
            catch (Exception ex)
            {
                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogWarning("ForceCommodityBottomBarBuy failed: " + ex);
                }
            }
        }

        internal static bool GetAllOfflineMarketProducts(MarketManager manager, ref Product[] __result)
        {
            if (manager == null || !ShouldForceMarket())
            {
                return true;
            }

            try
            {
                Product[] cached = GetField<Product[]>(manager, "_products");
                if (cached != null)
                {
                    __result = cached;
                    return false;
                }

                List<Product> products = new List<Product>();
                foreach (KeyValuePair<string, List<Yaml.Prototype>> pair in SingletonDict<string, List<Yaml.Prototype>>.Instance)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }
                    products.Add(MakeProduct(pair.Key));
                }

                Product[] productArray = products.ToArray();
                SetField(manager, "_products", productArray);
                __result = productArray;

                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogInfo("Island Market offline products rebuilt from all prototypes. count=" + productArray.Length);
                }
            }
            catch (Exception ex)
            {
                if (IslandMarketEnablePlugin.Log != null)
                {
                    IslandMarketEnablePlugin.Log.LogWarning("GetAllOfflineMarketProducts failed: " + ex);
                }
                return true;
            }

            return false;
        }

        private static Product MakeProduct(string prototypeId)
        {
            Product product = new Product
            {
                Id = Guid.NewGuid().ToString(),
                RegionId = "1",
                ListedAt = 0.0,
                ExpiresAt = 0.0,
                DeletesAt = 0.0,
                PurchasedAt = null,
                Price = 0L,
                Fee = 0L,
                Currency = Currency.TStone,
                State = ProductState.Registered,
                Level = 60,
                Durability = 10000f
            };

            Item? item = Cheats.MakeItem(prototypeId, product.Level);
            if (item != null)
            {
                product.Items = new Item[]
                {
                    item.Value
                };
            }
            return product;
        }

        private static T GetField<T>(object instance, string name) where T : class
        {
            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            return field == null ? null : field.GetValue(instance) as T;
        }

        private static T GetValue<T>(object instance, string name)
        {
            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            if (field == null)
            {
                return default(T);
            }
            object value = field.GetValue(instance);
            if (value is T)
            {
                return (T)value;
            }
            return default(T);
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            if (field != null)
            {
                field.SetValue(instance, value);
            }
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "IsHiddenMenu")]
    internal static class MenuSystemIsHiddenMenuPatch
    {
        private static void Postfix(MenuType type, ref bool __result)
        {
            if (type == MenuType.Market && IslandMarketRuntime.ShouldForceMarket())
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "GameManager_MainSceneLoaded")]
    internal static class MenuSystemMainSceneLoadedPatch
    {
        private static void Postfix(MenuSystem __instance)
        {
            IslandMarketRuntime.EnableMarket(__instance);
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "OnWelcome")]
    internal static class MenuSystemOnWelcomePatch
    {
        private static void Postfix(MenuSystem __instance)
        {
            IslandMarketRuntime.EnableMarket(__instance);
        }
    }

    [HarmonyPatch(typeof(MarketGroup), "Start")]
    internal static class MarketGroupStartPatch
    {
        private static void Postfix(MarketGroup __instance)
        {
            IslandMarketRuntime.RepairMarketTabs(__instance);
        }
    }

    [HarmonyPatch(typeof(MarketCategoriesWidget), "Init")]
    internal static class MarketCategoriesWidgetInitPatch
    {
        private static bool Prefix(MarketCategoriesWidget __instance)
        {
            return IslandMarketRuntime.InitMarketCategoriesWithoutOfflineFilter(__instance);
        }
    }

    [HarmonyPatch(typeof(CommodityListWidget), "OnBuyCommodity")]
    internal static class CommodityListWidgetOnBuyCommodityPatch
    {
        private static bool Prefix(CommodityListWidget __instance)
        {
            return IslandMarketRuntime.ConfirmOfflineBuy(__instance);
        }
    }

    [HarmonyPatch(typeof(CommodityList), "UpdateItemsOnOnline")]
    internal static class CommodityListUpdateItemsOnOnlinePatch
    {
        private static bool Prefix(CommodityList __instance)
        {
            return IslandMarketRuntime.ForceCommodityListOnlineColumns(__instance);
        }
    }

    [HarmonyPatch(typeof(CommodityNode), "UpdateItemsOnOnline")]
    internal static class CommodityNodeUpdateItemsOnOnlinePatch
    {
        private static bool Prefix(CommodityNode __instance)
        {
            return IslandMarketRuntime.ForceCommodityNodeOnlineColumns(__instance);
        }
    }

    [HarmonyPatch(typeof(CommodityListBottomBar), "Show")]
    internal static class CommodityListBottomBarShowPatch
    {
        private static void Postfix(CommodityListBottomBar __instance, Commodity commodity, Action<Commodity> favoriteChanged)
        {
            IslandMarketRuntime.ForceCommodityBottomBarBuy(__instance, commodity, favoriteChanged);
        }
    }

    [HarmonyPatch(typeof(MarketManager), "get_Products")]
    internal static class MarketManagerProductsPatch
    {
        private static bool Prefix(MarketManager __instance, ref Product[] __result)
        {
            return IslandMarketRuntime.GetAllOfflineMarketProducts(__instance, ref __result);
        }
    }
}
