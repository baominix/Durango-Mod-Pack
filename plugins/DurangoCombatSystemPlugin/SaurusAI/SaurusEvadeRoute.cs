namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    // Evade route is deliberately independent from BodyPart. BodyPart says
    // what was hit; DamageDirection says where the attacker is relative to
    // the animal and therefore owns defensive movement selection.
    internal enum SaurusEvadeRoute
    {
        Left = 0,
        Right = 1,
        Forward = 2,
        Backward = 3
    }
}
