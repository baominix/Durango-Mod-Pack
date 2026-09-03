using System;
using System.Collections;
using System.Reflection;
using Durango.UI;
using HarmonyLib;
using Messages;
using UnityEngine;

namespace NPCFriendListPlugin
{
	[HarmonyPatch(typeof(FriendFollowList), "Refresh", new Type[] { typeof(Social) })]
	internal static class FriendFollowListRefreshPatch
	{
		private static void Postfix(FriendFollowList __instance)
		{
			try
			{
				if (NPCFriendListPlugin.Instance != null)
				{
					NPCFriendListPlugin.Instance.StartCoroutine(ForceUIRebuild(__instance));
				}
			}
			catch (Exception ex)
			{
				if (NPCFriendListPlugin.Log != null)
				{
					NPCFriendListPlugin.Log.LogError("FriendFollowList refresh error: " + ex);
				}
			}
		}

		private static IEnumerator ForceUIRebuild(FriendFollowList instance)
		{
			yield return null;
			yield return null;

			try
			{
				FieldInfo scrollField = typeof(FriendFollowList).GetField("_scrollView", BindingFlags.NonPublic | BindingFlags.Instance);
				if (scrollField != null)
				{
					Component scrollView = scrollField.GetValue(instance) as Component;
					if (scrollView != null && scrollView.gameObject.activeInHierarchy)
					{
						MethodInfo updateMethod = scrollView.GetType().GetMethod("UpdateLayout", BindingFlags.Public | BindingFlags.Instance);
						if (updateMethod != null)
						{
							updateMethod.Invoke(scrollView, new object[] { false });
						}

						UIWidget[] widgets = scrollView.GetComponentsInChildren<UIWidget>(true);
						for (int i = 0; i < widgets.Length; i++)
						{
							widgets[i].MarkAsChanged();
						}
					}
				}

				SocialGroup socialGroup = instance.GetComponentInParent<SocialGroup>();
				if (socialGroup != null)
				{
					UIPanel panel = socialGroup.GetComponent<UIPanel>();
					if (panel != null && panel.alpha < 0.95f)
					{
						panel.alpha = 1f;
					}
				}
			}
			catch (Exception ex)
			{
				if (NPCFriendListPlugin.Log != null)
				{
					NPCFriendListPlugin.Log.LogError("ForceUIRebuild error: " + ex);
				}
			}
		}
	}
}
