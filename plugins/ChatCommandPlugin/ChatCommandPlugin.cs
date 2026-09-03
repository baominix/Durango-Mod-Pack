using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class ChatCommandPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.chatcommand";
        public const string PluginName = "Chat Command Plugin";
        public const string PluginVersion = "0.4.24";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            ChatCommandRegistry.RegisterDefaults();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("ChatCommandPlugin loaded");
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
