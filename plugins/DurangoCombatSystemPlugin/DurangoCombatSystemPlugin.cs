using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatSystemMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DurangoCombatSystemPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.combatsystem";
        public const string PluginName = "Durango Combat System Plugin";
        public const string PluginVersion = "0.3.2";

        private Harmony _harmony;
        private float _nextShortcutScanAt;
        private float _nextSkillRefreshAt;

        private void Awake()
        {
            BaoX.DurangoOriginal.OfflineCombat.OfflineCombatBackendPlugin.Log = Logger;
            BaoX.DurangoOriginal.CombatMode.CombatModePlugin.Log = Logger;
            BaoX.DurangoOriginal.SkillCombatBridge.SkillCombatBridgePlugin.Log = Logger;
            BaoX.DurangoOriginal.WeaponStatisticsMod.WeaponStatisticsPlugin.Log = Logger;

            Logger.LogInfo("Initializing unified local combat system");
            BaoX.DurangoOriginal.OfflineCombat.CombatActionProfiles.EnsureLoaded();
            BaoX.DurangoOriginal.OfflineCombat.AnimalCombatProfiles.EnsureLoaded();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            BaoX.DurangoOriginal.SkillCombatBridge.SkillCombatActionBuilder.MarkDirty();
            Logger.LogInfo("Durango combat system loaded (single plugin, local player only)");
        }

        private void Update()
        {
            BaoX.DurangoOriginal.OfflineCombat.OfflineCombatRuntime.Tick();
            BaoX.DurangoOriginal.OfflineCombat.BrachioLootRuntime.Tick();
            BaoX.DurangoOriginal.CombatSystemMod.Geometry.AttackAreaLineRenderer.Tick();
            BaoX.DurangoOriginal.CombatSystemMod.Geometry.AnimalAttackAreaLineRenderer.Tick();

            if (Time.unscaledTime >= _nextShortcutScanAt)
            {
                _nextShortcutScanAt = Time.unscaledTime + 0.5f;
                BaoX.DurangoOriginal.CombatMode.CombatModeShortcutVisibility.HideEndCombatShortcut();
            }

            if (BaoX.DurangoOriginal.SkillCombatBridge.SkillCombatActionBuilder.IsDirty &&
                Time.unscaledTime >= _nextSkillRefreshAt)
            {
                _nextSkillRefreshAt = Time.unscaledTime + 0.25f;
                BaoX.DurangoOriginal.SkillCombatBridge.SkillCombatActionBuilder.TryRefresh();
            }
        }

        private void OnDestroy()
        {
            BaoX.DurangoOriginal.CombatSystemMod.Geometry.AttackAreaLineRenderer.Reset();
            BaoX.DurangoOriginal.CombatSystemMod.Geometry.AnimalAttackAreaLineRenderer.Reset();
            BaoX.DurangoOriginal.OfflineCombat.OfflineNaturalAnimalSpawner.Reset();
            BaoX.DurangoOriginal.OfflineCombat.OfflineCombatRuntime.Reset();
            BaoX.DurangoOriginal.OfflineCombat.BrachioLootRuntime.Reset();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }
}
