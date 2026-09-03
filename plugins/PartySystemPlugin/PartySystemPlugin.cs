using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic;
using Durango.Render;
using Durango.UI;
using Durango.UI.Control;
using HarmonyLib;
using UnityEngine;

namespace PartySystemPlugin
{
	[BepInPlugin("com.baominix.durango.original.partysystem", "Party System Plugin", "2.1.2")]
	[BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
	public class PartySystemPlugin : BaseUnityPlugin
	{
		internal static PartySystemPlugin Instance;
		internal static ManualLogSource Log;
		private static readonly HashSet<int> WatchedPartyPreviews = new HashSet<int>();

		private void Awake()
		{
			Instance = this;
			Log = Logger;
			new Harmony("com.baominix.durango.original.partysystem").PatchAll(Assembly.GetExecutingAssembly());
			Logger.LogInfo("PartySystemPlugin v2.1.2 loaded. Original party menu/feature restore with party-preview layer isolation.");
		}

		private void Update()
		{
			TryEnablePartyMenu();
		}

		private static void TryEnablePartyMenu()
		{
			try
			{
				MenuSystem menuSystem = GameSystem<MenuSystem>.Instance();
				if (menuSystem != null && !menuSystem.IsEnabled(MenuType.Party))
				{
					menuSystem.EnableMenu(MenuType.Party, true, false);
				}
			}
			catch (Exception ex)
			{
				if (Log != null)
				{
					Log.LogWarning("Enable party menu failed: " + ex.Message);
				}
			}
		}

		// The original client hides Party in several local/offline contexts.
		// This plugin restores only availability; NPC friends/follow/rescue live
		// in NPCFriendListPlugin.
		[HarmonyPatch(typeof(MenuSystem), "IsHiddenMenu")]
		private static class MenuSystemIsHiddenMenuPatch
		{
			private static bool Prefix(MenuType type, ref bool __result)
			{
				if (type == MenuType.Party)
				{
					__result = false;
					return false;
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(MenuSystem), "EnableMenu")]
		private static class MenuSystemEnableMenuPatch
		{
			private static void Prefix(MenuType type, ref bool enable, ref bool checkHidden)
			{
				if (type == MenuType.Party)
				{
					enable = true;
					checkHidden = false;
				}
			}
		}

		[HarmonyPatch(typeof(MenuSystem), "IsEnabled")]
		private static class MenuSystemIsEnabledPatch
		{
			private static void Postfix(MenuType type, ref bool __result)
			{
				if (type == MenuType.Party)
				{
					__result = true;
				}
			}
		}

		// The restored Party page builds a large off-screen copy of the local
		// player for its member preview. Durango's PlaneShadows component clones
		// that copy onto world layer 0, producing the giant green silhouette seen
		// immediately after Create Party. Move every preview child (including the
		// cloned shadow mesh) back to UIModelRender layer 13. This preserves the
		// shadow inside Party Preview without leaking it into the game world.
		[HarmonyPatch(typeof(PartyPlayerInfoWidget), "SetPreviewModel")]
		private static class PartyPlayerInfoWidgetPreviewShadowPatch
		{
			private static void Postfix(PartyPlayerInfoWidget __instance)
			{
				if (Instance == null || __instance == null) return;
				int instanceId = __instance.GetInstanceID();
				if (!WatchedPartyPreviews.Add(instanceId)) return;
				Instance.StartCoroutine(KeepPartyPreviewIsolated(__instance, instanceId));
			}
		}

		private static IEnumerator KeepPartyPreviewIsolated(PartyPlayerInfoWidget widget, int instanceId)
		{
			FieldInfo previewField = typeof(PartyPlayerInfoWidget).GetField("_previewModel", BindingFlags.NonPublic | BindingFlags.Instance);
			while (widget != null && widget.gameObject.activeInHierarchy)
			{
				PlayerBehavior preview = (previewField != null) ? previewField.GetValue(widget) as PlayerBehavior : null;
				NormalizePartyPreviewLayer(preview);
				yield return new WaitForSeconds(0.2f);
			}
			WatchedPartyPreviews.Remove(instanceId);
		}

		private static void NormalizePartyPreviewLayer(PlayerBehavior preview)
		{
			if (preview == null) return;
			UIModelRender render = preview.GetComponentInParent<UIModelRender>();
			if (render == null) return;
			NGUITools.SetLayer(preview.gameObject, render.gameObject.layer);
		}

		[HarmonyPatch(typeof(PlaneShadows), "Add")]
		private static class PlaneShadowsPartyPreviewLayerPatch
		{
			private static void Postfix(PlaneShadows __instance)
			{
				if (__instance == null) return;
				UIModelRender render = __instance.GetComponentInParent<UIModelRender>();
				if (render == null) return;
				NGUITools.SetLayer(__instance.gameObject, render.gameObject.layer);
			}
		}
	}
}
