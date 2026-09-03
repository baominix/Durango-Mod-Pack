using Baominix.DurangoOriginal.CombatSystem.Geometry;

namespace Baominix.DurangoOriginal.CombatSystem.Runtime
{
    internal sealed class ScheduledPlayerHit
    {
        internal readonly AttackSnapshot Attack;

        internal ScheduledPlayerHit(AttackSnapshot attack)
        {
            Attack = attack;
        }
    }
}
