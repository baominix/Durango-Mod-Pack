using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Offline;
using Durango.UI;
using HarmonyLib;
using Shared.Skill;
using PlayerJob = Shared.Player.Job;
using Yaml.Util;

namespace BaoX.DurangoOriginal.CharacterCreationFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CharacterCreationFixPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.charactercreationfix";
        public const string PluginName = "Character Creation Fix Plugin";
        public const string PluginVersion = "0.1.7";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("CharacterCreationFixPlugin loaded");
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

    internal static class CharacterCreationRuntime
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string SkillApiTypeName = "BaoX.DurangoOriginal.SkillSystemMod.SkillSystemApi";

        internal static void InstallPlayersRouteWrapper(Gateway gateway)
        {
            if (gateway == null)
            {
                return;
            }

            FieldInfo webServerField = typeof(Gateway).GetField("_webServer", Flags);
            FieldInfo playerContextField = typeof(Gateway).GetField("_playerCtx", Flags);
            WebServer webServer = webServerField == null ? null : webServerField.GetValue(gateway) as WebServer;
            PlayerContext playerContext = playerContextField == null ? null : playerContextField.GetValue(gateway) as PlayerContext;
            if (webServer == null || webServer.PostRoute == null || playerContext == null)
            {
                return;
            }

            WebServer.RouteFunction original;
            if (!webServer.PostRoute.TryGetValue("/players", out original) || original == null)
            {
                return;
            }

            webServer.PostRoute["/players"] = delegate(HttpListenerRequest request, Dictionary<string, string> postData)
            {
                WebServer.Response response = original(request, postData);
                ApplyProfessionSkill(playerContext, postData);
                return response;
            };

            if (CharacterCreationFixPlugin.Log != null)
            {
                CharacterCreationFixPlugin.Log.LogInfo("Wrapped offline /players route for profession category Lv.20");
            }
        }

        internal static void ApplyProfessionSkill(PlayerContext context, Dictionary<string, string> postData)
        {
            if (context == null || postData == null)
            {
                return;
            }

            string jobText;
            if (!postData.TryGetValue("job", out jobText))
            {
                return;
            }

            int jobValue;
            if (!int.TryParse(jobText, out jobValue))
            {
                return;
            }

            // Gateway's original /players route clamps the submitted job to
            // the eight retail professions before selecting starter clothes.
            // Keep profession skill initialization on that exact same value.
            jobValue = Math.Max((int)PlayerJob.Engineer,
                Math.Min((int)PlayerJob.Jobless, jobValue));
            PlayerJob job = (PlayerJob)jobValue;
            Yaml.Job jobData = SingletonDict<PlayerJob, Yaml.Job>.Get(job, null);
            if (jobData == null || jobData.category_levels == null || jobData.category_levels.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Category, int> pair in jobData.category_levels)
            {
                SetCategoryLevel(context, pair.Key, pair.Value);
            }
        }

        private static void SetCategoryLevel(PlayerContext context, Category category, int level)
        {
            Type apiType = FindType(SkillApiTypeName);
            MethodInfo method = apiType == null ? null : apiType.GetMethod("SetCategoryLevelForContext", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                if (CharacterCreationFixPlugin.Log != null)
                {
                    CharacterCreationFixPlugin.Log.LogWarning("SkillSystemApi.SetCategoryLevelForContext is not available.");
                }
                return;
            }

            object[] args = new object[] { context, category.ToString(), level, null };
            bool success = false;
            try
            {
                success = (bool)method.Invoke(null, args);
            }
            catch (Exception exception)
            {
                if (CharacterCreationFixPlugin.Log != null)
                {
                    CharacterCreationFixPlugin.Log.LogWarning("Set profession category failed: " + exception.Message);
                }
                return;
            }

            if (CharacterCreationFixPlugin.Log != null)
            {
                CharacterCreationFixPlugin.Log.LogInfo("Set profession category: " + args[3] + " success=" + success);
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }
    }

    [HarmonyPatch]
    internal static class GatewayConstructorPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Constructor(typeof(Gateway), new Type[]
            {
                typeof(GameServer),
                typeof(WorldContext),
                typeof(PlayerContext)
            }, false);
        }

        private static void Postfix(Gateway __instance)
        {
            CharacterCreationRuntime.InstallPlayersRouteWrapper(__instance);
        }
    }

    [HarmonyPatch(typeof(FullScreenMovieGroupBase), "Play")]
    internal static class FullScreenMovieGroupPlayPatch
    {
        private const string OpeningMovieRelativePath = "Movie/PC/Durango-Wild-Lands-Opening-Movie.asset";
        private static UITexture _openingMovieTexture;
        private static UIBasicSprite.Fit _previousFit;
        private static float _previousFitAspectRatio;
        private static bool _coverApplied;

        private static bool Prefix(ref string url, bool once, Action onFinished)
        {
            if (string.IsNullOrEmpty(url) || url.IndexOf("prologue_movie", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            string localPath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, OpeningMovieRelativePath);
            if (System.IO.File.Exists(localPath))
            {
                if (CharacterCreationFixPlugin.Log != null)
                {
                    CharacterCreationFixPlugin.Log.LogInfo("Redirecting prologue movie to: " + localPath);
                }
                url = OpeningMovieRelativePath;
                ApplyOpeningMovieCover();
                return true;
            }

            if (CharacterCreationFixPlugin.Log != null)
            {
                CharacterCreationFixPlugin.Log.LogWarning("Local prologue movie not found at: " + localPath + ". Skipping prologue movie.");
            }
            if (onFinished != null)
            {
                onFinished();
            }
            return false;
        }

        internal static void ApplyOpeningMovieCover()
        {
            try
            {
                FullScreenMovieGroupBase movieGroup = UIManager.FindScript<FullScreenMovieGroupBase>();
                if (movieGroup == null)
                {
                    LogLayoutWarning("FullScreenMovieGroup was not found; keeping the original movie layout.");
                    return;
                }

                UITexture[] textures = movieGroup.GetComponentsInChildren<UITexture>(true);
                for (int i = 0; i < textures.Length; i++)
                {
                    UITexture texture = textures[i];
                    if (texture == null || !string.Equals(texture.gameObject.name, "MovieTexture", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!_coverApplied)
                    {
                        _openingMovieTexture = texture;
                        _previousFit = texture.fit;
                        _previousFitAspectRatio = texture.fitAcpectRatio;
                        _coverApplied = true;
                    }

                    // FitOutside is NGUI's cover mode: it preserves the source
                    // aspect ratio, fills the complete widget and crops overflow.
                    texture.fitAcpectRatio = 0f;
                    texture.fit = UIBasicSprite.Fit.FitOutside;
                    if (CharacterCreationFixPlugin.Log != null)
                    {
                        CharacterCreationFixPlugin.Log.LogInfo("Opening movie layout set to Cover (FitOutside).");
                    }
                    return;
                }

                LogLayoutWarning("MovieTexture was not found; keeping the original movie layout.");
            }
            catch (Exception exception)
            {
                LogLayoutWarning("Could not apply opening movie Cover layout: " + exception.Message);
            }
        }

        internal static void RestoreMovieLayout()
        {
            if (!_coverApplied)
            {
                return;
            }

            try
            {
                if (_openingMovieTexture != null)
                {
                    _openingMovieTexture.fitAcpectRatio = _previousFitAspectRatio;
                    _openingMovieTexture.fit = _previousFit;
                }
            }
            catch (Exception exception)
            {
                LogLayoutWarning("Could not restore the original movie layout: " + exception.Message);
            }
            finally
            {
                _openingMovieTexture = null;
                _coverApplied = false;
            }
        }

        private static void LogLayoutWarning(string message)
        {
            if (CharacterCreationFixPlugin.Log != null)
            {
                CharacterCreationFixPlugin.Log.LogWarning(message);
            }
        }
    }

    [HarmonyPatch(typeof(FullScreenMovieGroupBase), "Stop")]
    internal static class FullScreenMovieGroupStopPatch
    {
        private static void Postfix()
        {
            FullScreenMovieGroupPlayPatch.RestoreMovieLayout();
        }
    }
}
