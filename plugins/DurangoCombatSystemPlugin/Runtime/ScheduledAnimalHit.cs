using Baominix.DurangoOriginal.CombatSystem.Geometry;

namespace Baominix.DurangoOriginal.CombatSystem.Runtime
{
    internal sealed class ScheduledAnimalHit
    {
        internal readonly AnimalAttackSnapshot Attack;

        internal ScheduledAnimalHit(AnimalAttackSnapshot attack)
        {
            Attack = attack;
        }
    }
}
