using System.Collections.Generic;
using Messages;

namespace Baominix.DurangoOriginal.CombatSystem.Actions
{
    internal sealed class CombatActionSnapshot
    {
        internal readonly ActionStatus[] Statuses;
        internal readonly HashSet<string> ActionIds;
        internal readonly string EquipmentSource;
        internal readonly bool EquipmentDataReady;
        internal readonly bool SkillDataReady;

        internal CombatActionSnapshot(
            ActionStatus[] statuses,
            HashSet<string> actionIds,
            string equipmentSource,
            bool equipmentDataReady,
            bool skillDataReady)
        {
            Statuses = statuses ?? new ActionStatus[0];
            ActionIds = actionIds ??
                new HashSet<string>(System.StringComparer.Ordinal);
            EquipmentSource = equipmentSource ?? "unknown";
            EquipmentDataReady = equipmentDataReady;
            SkillDataReady = skillDataReady;
        }
    }
}
