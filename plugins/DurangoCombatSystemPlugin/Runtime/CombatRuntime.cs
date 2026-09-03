using Durango.Offline;
using Baominix.DurangoOriginal.CombatSystem.Presentation;

namespace Baominix.DurangoOriginal.CombatSystem.Runtime
{
    internal static class CombatRuntime
    {
        private static readonly object Sync = new object();
        private static OfflineCombatSession _current;
        private static int _generation;

        internal static OfflineCombatSession Bind(
            Player player,
            Connection connection,
            World world,
            PlayerContext context)
        {
            lock (Sync)
            {
                if (_current != null)
                {
                    _current.Dispose();
                }

                _generation++;
                _current = new OfflineCombatSession(
                    player,
                    connection,
                    world,
                    context,
                    _generation);
                return _current;
            }
        }

        internal static bool IsCurrent(OfflineCombatSession session)
        {
            lock (Sync)
            {
                return session != null &&
                    object.ReferenceEquals(_current, session) &&
                    !_current.IsDisposed;
            }
        }

        internal static void Release(OfflineCombatSession session)
        {
            lock (Sync)
            {
                if (!object.ReferenceEquals(_current, session))
                {
                    return;
                }
                _current = null;
            }
        }

        internal static void Reset()
        {
            lock (Sync)
            {
                if (_current != null)
                {
                    _current.Dispose();
                    _current = null;
                }
                _generation++;
            }
            SaurusDebugBubble.HideAll();
        }

        internal static bool SetSaurusBubbleDebug(
            bool enabled,
            out string response)
        {
            SaurusDebugBubble.SetEnabled(enabled);
            response = "Animal AI bubble debug: " +
                (SaurusDebugBubble.Enabled ? "ON" : "OFF") + ".";
            return true;
        }

        internal static bool IsSaurusBubbleDebugEnabled()
        {
            return SaurusDebugBubble.Enabled;
        }

        internal static void Process(double now)
        {
            OfflineCombatSession current;
            lock (Sync)
            {
                current = _current;
            }
            if (current != null)
            {
                current.Process(now);
            }
        }

        internal static bool TryAddPlayerGauge(
            string gaugeName,
            float amount,
            out string response)
        {
            OfflineCombatSession current;
            lock (Sync)
            {
                current = _current;
            }
            if (current == null || current.IsDisposed)
            {
                response = "Enter an offline world first.";
                return false;
            }
            return current.TryAddPlayerGauge(
                gaugeName,
                amount,
                out response);
        }

        internal static bool TryGetSaurusContextReport(
            string selector,
            out string[] lines)
        {
            OfflineCombatSession current;
            lock (Sync)
            {
                current = _current;
            }
            if (current == null || current.IsDisposed)
            {
                lines = new string[]
                {
                    "Enter an offline world first."
                };
                return false;
            }
            return current.TryGetSaurusContextReport(selector, out lines);
        }

        internal static bool TryGetSaurusIntentReport(
            string selector,
            out string[] lines)
        {
            OfflineCombatSession current;
            lock (Sync)
            {
                current = _current;
            }
            if (current == null || current.IsDisposed)
            {
                lines = new string[]
                {
                    "Enter an offline world first."
                };
                return false;
            }
            return current.TryGetSaurusIntentReport(selector, out lines);
        }
    }
}
