using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal static class EmbeddedCombatData
    {
        internal const string AnimalJson =
            "DurangoCombat.animal.json";
        internal const string RootMotionJson =
            "DurangoCombat.saurus_root_motion.json";
        internal const string PlayerBattleActionsJson =
            "DurangoCombat.player_battle_actions.json";
        internal const string TriceraFramework =
            "DurangoCombat.framework.tricera";
        internal const string PhenacodusFramework =
            "DurangoCombat.framework.phenacodus";
        internal const string RaptorFramework =
            "DurangoCombat.framework.raptor";

        internal static string ReadText(
            string resourceName,
            CombatDataLoadReport report)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                report.Errors.Add("Embedded combat resource name is empty.");
                return null;
            }

            Stream stream = null;
            try
            {
                stream = Assembly.GetExecutingAssembly().
                    GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    report.Errors.Add(
                        "Embedded combat resource not found: " +
                        resourceName);
                    return null;
                }

                using (StreamReader reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    stream = null;
                    return reader.ReadToEnd();
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    "Embedded combat resource could not be read: " +
                    resourceName + " (" + exception.Message + ")");
                return null;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }
        }

        internal static string FrameworkFor(string framework)
        {
            if (string.Equals(
                framework,
                "Tricera",
                StringComparison.OrdinalIgnoreCase))
            {
                return TriceraFramework;
            }

            if (string.Equals(
                framework,
                "Phenacodus",
                StringComparison.OrdinalIgnoreCase))
            {
                return PhenacodusFramework;
            }

            if (string.Equals(
                framework,
                "Raptor",
                StringComparison.OrdinalIgnoreCase))
            {
                return RaptorFramework;
            }

            return null;
        }
    }
}
