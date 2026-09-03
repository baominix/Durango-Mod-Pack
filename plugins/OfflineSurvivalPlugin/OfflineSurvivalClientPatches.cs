using System;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.Terrain;
using HarmonyLib;
using InteractionData;
using Shared.Region;
using Yaml;
using Yaml.Util;

namespace BaoX.DurangoOriginal.OfflineSurvivalMod
{
    /// <summary>
    /// Client-side patches that make the survival context icons appear in offline mode.
    ///
    /// The stock <see cref="InteractionSystem.BiomeContextAction"/> bails out immediately
    /// when <c>GameManager.ClusterMode != Mode.Online</c> (InteractionSystem.cs:762-765).
    /// That bails out is fine for the production server but it strips the
    /// WashBody / DrinkWater / SelectDrawContainer icons from the bottom-right action
    /// wheel in offline mode, so the player has no way to start the actions even after
    /// the server-side handlers are patched.
    ///
    /// The prefix replicates the original logic and lets the early return happen only
    /// when the cluster is actually online (i.e. the real game server is authoritative).
    /// </summary>
    [HarmonyPatch(typeof(InteractionSystem), "BiomeContextAction")]
    internal static class BiomeContextActionPatch
    {
        // Signature of the original method:
        //   private static void BiomeContextAction(List<InteractionMenuData> result)
        // The 'result' list is appended to by every registered context action finder,
        // so adding to it is the right way to surface more icons.

        [HarmonyPrefix]
        static bool Prefix(List<InteractionMenuData> result)
        {
            // Always run the original when we are actually online — the production
            // server is authoritative there, no need to duplicate work.
            if (GameManager.ClusterMode == Mode.Online) return true;

            try
            {
                PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
                if (localPlayer == null) return false;

                Biome biome = localPlayer.GetBiome();

                // --- Replicate the SelectDrawContainer loop from the original method ---
                // Adds the "act_scoopupwater" icon whenever any PutInContainerInfo
                // entry matches the player's current biome. This is what makes the
                // DrawWater button show up next to drinkable water.
                Dictionary<string, PutInContainerInfo> putInContainerInfos = Singleton<Constants>.Instance.PutInContainerInfos;
                if (putInContainerInfos != null)
                {
                    foreach (KeyValuePair<string, PutInContainerInfo> entry in putInContainerInfos)
                    {
                        PutInContainerInfo info = entry.Value;
                        if (info == null || info.Biomes == null) continue;
                        if (Array.IndexOf(info.Biomes, biome) < 0) continue;

                        // Avoid duplicating an entry the original may have already
                        // produced (defensive — should be a no-op in offline mode).
                        bool already = false;
                        for (int i = 0; i < result.Count; i++)
                        {
                            if (result[i].Id == entry.Key) { already = true; break; }
                        }
                        if (already) continue;

                        InteractionMenuData item = new InteractionMenuData(Interaction.SelectDrawContainer);
                        item.Id = entry.Key;
                        item.Icon = "act_scoopupwater";
                        result.Add(item);
                    }
                }

                // --- Replicate the water-depth + biome gate from the original method ---
                // WashBody / DrinkWater only show when the player is on ground floor
                // and at most waist-deep in water (waist-deep is the max the drink /
                // wash animation supports).
                TerrainWater.WaterDepthLevel waterDepthLevel = (TerrainWater.WaterDepthLevel)localPlayer.WaterDepthLevel;
                if ((byte)localPlayer.Floor != 0) return false;
                if (waterDepthLevel > TerrainWater.WaterDepthLevel.Waist) return false;

                if (Util.IsWater(biome))
                {
                    result.Add(Interaction.WashBody);
                }
                if (Util.IsDrinkable(biome))
                {
                    result.Add(Interaction.DrinkWater);
                }
            }
            catch (Exception ex)
            {
                OfflineSurvivalPlugin.Log.LogError("BiomeContextAction offline patch failed: " + ex);
                return true; // fall through to original if we somehow crash
            }

            return false; // skip the original — we already populated the list
        }
    }
}
