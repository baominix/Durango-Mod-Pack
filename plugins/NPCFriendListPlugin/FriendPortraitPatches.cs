using System;
using System.Reflection;
using Durango.UI;
using HarmonyLib;
using UnityEngine;

namespace NPCFriendListPlugin
{
	[HarmonyPatch(typeof(PlayerInfoWidget), "OnPlayer")]
	internal static class PlayerInfoWidgetOnPlayerPatch
	{
		private static void Postfix(PlayerInfoWidget __instance, Durango.Player.PlayerInfo player)
		{
			if (player == null) return;

			try
			{
				FieldInfo portraitField = typeof(PlayerInfoWidget).GetField("_portraitTexture", BindingFlags.NonPublic | BindingFlags.Instance);
				UITexture portrait = (portraitField != null) ? portraitField.GetValue(__instance) as UITexture : null;

				if (player.EntityId == NpcFriendListData.KEntityId)
				{
					NpcFriendPortraits.ApplyPresetPortrait(portrait, NpcPartyBridge.GetKPortraitPreset(), "K");
				}
				else if (player.EntityId == NpcFriendListData.CharlieEntityId)
				{
					NpcFriendPortraits.ApplyPresetPortrait(portrait, NpcFriendListData.CharliePortraitPreset, "Charlie");
				}
			}
			catch (Exception ex)
			{
				if (NPCFriendListPlugin.Log != null)
				{
					NPCFriendListPlugin.Log.LogWarning("PlayerInfoWidget portrait patch failed: " + ex.Message);
				}
			}
		}
	}

	internal static class NpcFriendPortraits
	{
		internal static void ApplyPresetPortrait(UITexture texture, string preset, string label)
		{
			if (texture == null) return;

			ResetCustomPortrait(texture);

			PortraitBuilder.Argument argument = new PortraitBuilder.Argument
			{
				Preset = preset
			};
			PortraitBuilder.Set(argument, texture);
			texture.MarkAsChanged();

			if (texture.mainTexture == null && texture.material == null && NPCFriendListPlugin.Log != null)
			{
				NPCFriendListPlugin.Log.LogWarning(label + " portrait preset was not found: " + preset);
			}
		}

		private static bool IsCustomPortraitMaterial(Material material)
		{
			return material != null
				&& (material.name.StartsWith("KPortrait_Runtime", StringComparison.Ordinal)
					|| material.name.StartsWith("CharliePortrait_Runtime", StringComparison.Ordinal));
		}

		private static void ResetCustomPortrait(UITexture texture)
		{
			if (texture == null) return;
			Material currentMaterial = texture.material;
			if (!IsCustomPortraitMaterial(currentMaterial)) return;

			texture.material = null;
			texture.mainTexture = null;
			texture.uvRect = new Rect(0f, 0f, 1f, 1f);
			texture.drawRegion = new Vector4(0f, 0f, 1f, 1f);
			texture.MarkAsChanged();
			UnityEngine.Object.Destroy(currentMaterial);
		}
	}
}
