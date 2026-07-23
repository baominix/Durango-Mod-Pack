using System.Reflection;
using Durango.Logic.Clusters;

namespace BaoX.DurangoOriginal.CombatMode
{
    internal struct ClusterModePatchState
    {
        internal bool Changed;
        internal Mode OriginalMode;
    }

    internal static class CombatModeRuntime
    {
        private static readonly PropertyInfo ClusterModeProperty = typeof(GameManager).GetProperty(
            "ClusterMode",
            BindingFlags.Static | BindingFlags.Public);

        internal static bool SetClusterMode(Mode mode)
        {
            if (ClusterModeProperty == null)
            {
                return false;
            }

            MethodInfo setter = ClusterModeProperty.GetSetMethod(true);
            if (setter == null)
            {
                return false;
            }

            setter.Invoke(null, new object[] { mode });
            return true;
        }
    }
}
