using System;
using System.Reflection;

namespace NPCFriendListPlugin
{
	internal static class NpcPartyBridge
	{
		private const string PartySystemTypeName = "NPCFriendListPlugin.NPCFriendListPlugin, NPCFriendListPlugin";
		private static Type _partySystemType;
		private static bool _partySystemResolved;

		internal static bool IsPartySystemAvailable()
		{
			return GetPartySystemType() != null;
		}

		internal static string GetKPortraitPreset()
		{
			try
			{
				Type type = GetPartySystemType();
				if (type == null) return NpcFriendListData.KDefaultPortraitPreset;

				MethodInfo method = type.GetMethod("GetSelectedKPortraitPreset", BindingFlags.NonPublic | BindingFlags.Static);
				if (method == null) return NpcFriendListData.KDefaultPortraitPreset;

				string preset = method.Invoke(null, new object[0]) as string;
				return string.IsNullOrEmpty(preset) ? NpcFriendListData.KDefaultPortraitPreset : preset;
			}
			catch
			{
				return NpcFriendListData.KDefaultPortraitPreset;
			}
		}

		private static Type GetPartySystemType()
		{
			if (!_partySystemResolved)
			{
				_partySystemType = Type.GetType(PartySystemTypeName, false);
				_partySystemResolved = true;
			}
			return _partySystemType;
		}
	}
}
