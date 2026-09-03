using System;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.System;
using Durango.Utils;
using HarmonyLib;

namespace BaoX.DurangoOriginal.GameMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class GameMenuPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.gamemenu";
        public const string PluginName = "Game Menu Plugin";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(GameMenuPlugin).Assembly);
            Logger.LogInfo("GameMenuPlugin loaded");
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

    internal static class GameMenuModeRules
    {
        private const string CreativeKey = "free_offline";
        private const string SingleMultiKey = "single_multi_offline";
        private const string SelectModePrefKey = "baox_select_game_mode";

        private static readonly MenuType[] SharedGameModeMenus = new MenuType[]
        {
            MenuType.Character,
            MenuType.CategoryCharacter,
            MenuType.PlayerSelection,
            MenuType.Music,
            MenuType.Skill,
            MenuType.Craft,
            MenuType.Inventory,
            MenuType.Connect,
            MenuType.Pet,
            MenuType.Market,
            MenuType.Estate,
            MenuType.Quest,
            MenuType.Faction,
            MenuType.PvpIsland,
            MenuType.Story,
            MenuType.Clan,
            MenuType.Social,
            MenuType.CategorySocial,
            MenuType.Party,
            MenuType.Encyclopedia,
            MenuType.WarpShop,
            MenuType.WorldMap,
            MenuType.Screenshot,
            MenuType.Mail,
            MenuType.Notice,
            MenuType.MoveToTitle,
            MenuType.Config
        };

        private static readonly MenuType[] HiddenInOnline = new MenuType[]
        {
            MenuType.Connect,
            MenuType.CharacterOnMenu,
            MenuType.MusicOnMenu,
            MenuType.StoryOnMenu,
            MenuType.MoveToTitle,
            MenuType.WarpShop
        };

        public static bool IsGameModeActive()
        {
            string clusterKey = GameManager.ClusterKey;
            if (clusterKey == CreativeKey || clusterKey == SingleMultiKey)
            {
                return true;
            }

            string selected = Preferences.GetString(SelectModePrefKey, string.Empty, Preferences.Level.Device);
            if (selected == CreativeKey || selected == SingleMultiKey)
            {
                return true;
            }

            return GameManager.ClusterMode == Mode.Editable || GameManager.ClusterMode == Mode.Offline;
        }

        public static bool ShouldHide(MenuType type)
        {
            if (!IsGameModeActive())
            {
                if (GameManager.ClusterMode == Mode.Online && Contains(HiddenInOnline, type))
                {
                    return true;
                }
                return false;
            }

            return !Contains(SharedGameModeMenus, type);
        }

        public static void Apply(MenuSystem menuSystem)
        {
            if (menuSystem == null || !IsGameModeActive())
            {
                return;
            }

            MenuType[] all = Enums<MenuType>.All();
            for (int i = 0; i < all.Length; i++)
            {
                MenuType type = all[i];
                menuSystem.EnableMenu(type, !ShouldHide(type), false);
            }

            if (GameMenuPlugin.Log != null)
            {
                GameMenuPlugin.Log.LogInfo("Game menu mode rules applied. cluster=" + GameManager.ClusterKey + " mode=" + GameManager.ClusterMode);
            }
        }

        private static bool Contains(MenuType[] list, MenuType type)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == type)
                {
                    return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "IsHiddenMenu")]
    internal static class MenuSystemIsHiddenMenuPatch
    {
        private static bool Prefix(MenuType type, ref bool __result)
        {
            __result = GameMenuModeRules.ShouldHide(type);
            return false;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "GameManager_MainSceneLoaded")]
    internal static class MenuSystemMainSceneLoadedPatch
    {
        private static void Postfix(MenuSystem __instance)
        {
            GameMenuModeRules.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "OnWelcome")]
    internal static class MenuSystemOnWelcomePatch
    {
        private static void Postfix(MenuSystem __instance)
        {
            GameMenuModeRules.Apply(__instance);
        }
    }
}
