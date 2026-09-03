namespace Baominix.DurangoOriginal.CombatSystem.Runtime
{
    internal sealed class AcceptedPlayerAction
    {
        internal readonly long InstanceId;
        internal readonly int Generation;
        internal readonly uint PacketSequence;
        internal readonly string ActorEntityId;
        internal readonly string ActionId;
        internal readonly double AcceptedAt;
        internal readonly double ClientStartAt;
        internal readonly string TargetEntityId;

        internal AcceptedPlayerAction(
            long instanceId,
            int generation,
            uint packetSequence,
            string actorEntityId,
            string actionId,
            double acceptedAt,
            double clientStartAt,
            string targetEntityId)
        {
            InstanceId = instanceId;
            Generation = generation;
            PacketSequence = packetSequence;
            ActorEntityId = actorEntityId;
            ActionId = actionId;
            AcceptedAt = acceptedAt;
            ClientStartAt = clientStartAt;
            TargetEntityId = targetEntityId;
        }
    }
}
