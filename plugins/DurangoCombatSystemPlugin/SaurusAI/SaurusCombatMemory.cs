using System.Collections.Generic;
using System.Globalization;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal enum SaurusCombatEventType
    {
        None = 0,
        Engaged = 1,
        DamagedByPlayer = 2,
        AnimalDodgedPlayerAttack = 3,
        PlayerAttackMissed = 4,
        BlownOrKnockedBack = 5,
        TargetEnteredFlank = 6,
        TargetEnteredRear = 7,
        PathBlocked = 8,
        LowHealthThresholdCrossed = 9,
        LastActionCompleted = 10,
        PlayerBowOrCrossbowAttack = 11
    }

    internal sealed class SaurusCombatEventSnapshot
    {
        internal SaurusCombatEventSnapshot(
            long sequence,
            SaurusCombatEventType type,
            double at,
            int generation,
            long engagementId,
            string actorEntityId,
            string targetEntityId,
            long actionInstanceId,
            string actionKey)
        {
            Sequence = sequence;
            Type = type;
            At = at;
            Generation = generation;
            EngagementId = engagementId;
            ActorEntityId = actorEntityId;
            TargetEntityId = targetEntityId;
            ActionInstanceId = actionInstanceId;
            ActionKey = actionKey;
        }

        internal long Sequence { get; private set; }
        internal SaurusCombatEventType Type { get; private set; }
        internal double At { get; private set; }
        internal int Generation { get; private set; }
        internal long EngagementId { get; private set; }
        internal string ActorEntityId { get; private set; }
        internal string TargetEntityId { get; private set; }
        internal long ActionInstanceId { get; private set; }
        internal string ActionKey { get; private set; }
    }

    internal sealed class SaurusCombatMemory
    {
        private const int MaximumRememberedEvents = 16;

        private readonly int _generation;
        private readonly string _actorEntityId;
        private readonly Queue<SaurusCombatEventSnapshot> _events =
            new Queue<SaurusCombatEventSnapshot>();
        private long _nextSequence;
        private long _currentEngagementId;

        internal SaurusCombatMemory(int generation, string actorEntityId)
        {
            _generation = generation;
            _actorEntityId = actorEntityId;
        }

        internal int EventCount
        {
            get { return _events.Count; }
        }

        internal SaurusCombatEventSnapshot LatestEvent { get; private set; }

        internal void BeginEngagement(long engagementId)
        {
            _currentEngagementId = engagementId;
        }

        internal void Record(
            SaurusCombatEventType type,
            double at,
            string targetEntityId,
            long actionInstanceId,
            string actionKey)
        {
            if (type == SaurusCombatEventType.None)
            {
                return;
            }

            SaurusCombatEventSnapshot recorded =
                new SaurusCombatEventSnapshot(
                    ++_nextSequence,
                    type,
                    at,
                    _generation,
                    _currentEngagementId,
                    _actorEntityId,
                    targetEntityId,
                    actionInstanceId,
                    actionKey);
            _events.Enqueue(recorded);
            LatestEvent = recorded;
            while (_events.Count > MaximumRememberedEvents)
            {
                _events.Dequeue();
            }
        }

        internal void Clear()
        {
            _events.Clear();
            LatestEvent = null;
        }

        internal bool HasRecent(
            SaurusCombatEventType type,
            double now,
            double maximumAgeSeconds,
            long engagementId)
        {
            if (type == SaurusCombatEventType.None ||
                maximumAgeSeconds < 0.0)
            {
                return false;
            }
            foreach (SaurusCombatEventSnapshot item in _events)
            {
                if (item.Type == type &&
                    item.Generation == _generation &&
                    item.EngagementId == engagementId &&
                    now >= item.At &&
                    now - item.At <= maximumAgeSeconds)
                {
                    return true;
                }
            }
            return false;
        }

        internal string[] ToDiagnosticLines(double now, int maximumLines)
        {
            if (_events.Count == 0 || maximumLines <= 0)
            {
                return new string[] { "RecentEvents none." };
            }

            SaurusCombatEventSnapshot[] snapshot = _events.ToArray();
            int first = snapshot.Length - maximumLines;
            if (first < 0)
            {
                first = 0;
            }
            List<string> lines = new List<string>();
            lines.Add(
                "RecentEvents showing=" + (snapshot.Length - first) +
                "/" + snapshot.Length + ".");
            int i;
            for (i = snapshot.Length - 1; i >= first; i--)
            {
                SaurusCombatEventSnapshot item = snapshot[i];
                lines.Add(
                    "event#" + item.Sequence + " " + item.Type +
                    " engagement=" + item.EngagementId +
                    " age=" + (now - item.At).ToString(
                        "0.##",
                        CultureInfo.InvariantCulture) + "s" +
                    (string.IsNullOrEmpty(item.ActionKey)
                        ? string.Empty
                        : " action=" + item.ActionKey + "@" +
                            item.ActionInstanceId) + ".");
            }
            return lines.ToArray();
        }
    }
}
