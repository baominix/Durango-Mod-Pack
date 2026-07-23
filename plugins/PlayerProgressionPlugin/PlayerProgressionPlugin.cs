using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BaoX.DurangoOriginal.PlayerProgressionMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PlayerProgressionPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.playerprogression";
        public const string PluginName = "Player Progression Plugin";
        public const string PluginVersion = "0.2.3";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("PlayerProgressionPlugin loaded");
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
}
