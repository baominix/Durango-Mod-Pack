using System;
using System.Collections;
using System.Reflection;
using Durango.UI;
using HarmonyLib;
using Messages;

namespace NPCFriendListPlugin
{
	// Owns only the fake friend-list source data. Party membership, follow,
	// invite handling, rescue and NPC state stay in PartySystemPlugin.
	[HarmonyPatch(typeof(SocialSystem), "GetSocial")]
	internal static class SocialSystemGetSocialPatch
	{
		private static bool Prefix(SocialSystem __instance, Action<Social> onSocial)
		{
			try
			{
				Social social = __instance.Social;
				NpcFriendListData.InjectNpcFriends(ref social);

				if (NPCFriendListPlugin.Instance != null)
				{
					NPCFriendListPlugin.Instance.StartCoroutine(DeliverSocialResponse(__instance, social, onSocial));
				}
				else
				{
					DeliverSocialResponseNow(__instance, social, onSocial);
				}
			}
			catch (Exception ex)
			{
				if (NPCFriendListPlugin.Log != null)
				{
					NPCFriendListPlugin.Log.LogError("SocialSystem.GetSocial error: " + ex);
				}
			}
			return false;
		}

		private static IEnumerator DeliverSocialResponse(SocialSystem socialSystem, Social social, Action<Social> onSocial)
		{
			// SocialGroup opens its child pages only after UIBase.TryOpen marks the
			// group as opened. Returning social in the same stack is too early and
			// leaves the Friend List background visible but empty.
			yield return null;
			yield return null;
			DeliverSocialResponseNow(socialSystem, social, onSocial);
		}

		private static void DeliverSocialResponseNow(SocialSystem socialSystem, Social social, Action<Social> onSocial)
		{
			try
			{
				MethodInfo setSocial = typeof(SocialSystem).GetMethod("SetSocial", BindingFlags.NonPublic | BindingFlags.Instance);
				if (setSocial == null)
				{
					throw new MissingMethodException(typeof(SocialSystem).FullName, "SetSocial");
				}

				setSocial.Invoke(socialSystem, new object[] { social, null });
				if (onSocial != null)
				{
					onSocial(social);
				}

				if (NPCFriendListPlugin.Log != null)
				{
					NPCFriendListPlugin.Log.LogInfo("NPC friend list delivered. Following="
						+ social.FollowingEntityIds.Length + ", friends=" + social.FriendEntities.Count);
				}
			}
			catch (Exception ex)
			{
				if (NPCFriendListPlugin.Log != null)
				{
					NPCFriendListPlugin.Log.LogError("DeliverSocialResponse error: " + ex);
				}
			}
		}
	}

	[HarmonyPatch(typeof(PlayerSearchResultList), "OnSocial")]
	internal static class PlayerSearchResultListOnSocialPatch
	{
		private static void Prefix(ref Social social)
		{
			NpcFriendListData.InjectNpcFriends(ref social);
		}
	}
}
