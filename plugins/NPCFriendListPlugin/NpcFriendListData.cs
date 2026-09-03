using System.Collections.Generic;
using Messages;

namespace NPCFriendListPlugin
{
	internal static class NpcFriendListData
	{
		internal const string KEntityId = "npc_k";
		internal const string KName = "K";
		internal const int KLevel = 60;
		internal const int KFreq = 1234;
		internal const string KDefaultPortraitPreset = "todo_icon_npc_TheFirm";

		internal const string CharlieEntityId = "npc_charlie";
		internal const string CharlieName = "Charlie";
		internal const int CharlieLevel = 60;
		internal const int CharlieFreq = 5678;
		internal const string CharliePortraitPreset = "todo_icon_npc_Optimistic";

		internal static bool IsNpcFriend(string entityId)
		{
			return entityId == KEntityId || entityId == CharlieEntityId;
		}

		internal static void EnsureSocialArrays(ref Social social)
		{
			if (social.FollowingEntityIds == null) social.FollowingEntityIds = new string[0];
			if (social.FriendEntities == null) social.FriendEntities = new Dictionary<string, Shared.Player.FriendType>();
			if (social.ReceivedFriendRequests == null) social.ReceivedFriendRequests = new string[0];
			if (social.SentFriendRequests == null) social.SentFriendRequests = new string[0];
			if (social.BlockedEntityIds == null) social.BlockedEntityIds = new string[0];
			if (social.FavoriteRegionOwners == null) social.FavoriteRegionOwners = new string[0];
		}

		internal static void InjectNpcFriends(ref Social social)
		{
			EnsureSocialArrays(ref social);

			List<string> following = new List<string>(social.FollowingEntityIds);
			if (!following.Contains(KEntityId)) following.Add(KEntityId);
			if (!following.Contains(CharlieEntityId)) following.Add(CharlieEntityId);
			social.FollowingEntityIds = following.ToArray();

			if (!social.FriendEntities.ContainsKey(KEntityId))
			{
				social.FriendEntities.Add(KEntityId, Shared.Player.FriendType.JustFriend);
			}

			if (!social.FriendEntities.ContainsKey(CharlieEntityId))
			{
				social.FriendEntities.Add(CharlieEntityId, Shared.Player.FriendType.JustFriend);
			}
		}
	}
}
