using System.Collections;
using System.Reflection;
using Durango.Offline;

namespace Baominix.DurangoOriginal.CombatSystem.Protocol
{
    internal static class ConnectionHandlerInspector
    {
        private static readonly FieldInfo PacketHandlersField =
            typeof(Connection).GetField(
                "_packetHandlers",
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool HasHandler(Connection connection, uint typeCode)
        {
            if (connection == null || PacketHandlersField == null)
            {
                return false;
            }

            IDictionary handlers =
                PacketHandlersField.GetValue(connection) as IDictionary;
            return handlers != null && handlers.Contains(typeCode);
        }
    }
}
