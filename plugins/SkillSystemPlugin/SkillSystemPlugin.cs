using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BaoX.DurangoOriginal.SkillSystemMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SkillSystemPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.skillsystem";
        public const string PluginName = "Skill System Plugin";
        public const string PluginVersion = "0.5.32";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("SkillSystemPlugin loaded: original offline backend");
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
