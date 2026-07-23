using System;
using System.Collections.Generic;
using System.Globalization;
using BaoX.DurangoOriginal.CombatSystemMod.Geometry;
using Durango.Logic;
using Durango.Network;
using Durango.UI;
using Durango.Utils;
using HarmonyLib;
using Messages;
using Shared.Chat;
using UnityEngine;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal static class OfflineCombatDebugCommands
    {
        private static readonly HashSet<string> TestAnimals = new HashSet<string>();
        private static readonly HashSet<string> AutoSelectAnimals = new HashSet<string>();
        private static readonly HashSet<string> PlayerInitiatedAnimals = new HashSet<string>();

        internal static bool TryExecute(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            string[] parts = message.Trim().Split(
                new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            if (string.Equals(parts[0], "/combatspawn", StringComparison.OrdinalIgnoreCase))
            {
                int entityType = 2005;
                int level = 10;
                if (parts.Length > 1 && !int.TryParse(parts[1], out entityType))
                {
                    Reply("Usage: /combatspawn [animalType] [level]");
                    return true;
                }
                if (parts.Length > 2 && !int.TryParse(parts[2], out level))
                {
                    Reply("Usage: /combatspawn [animalType] [level]");
                    return true;
                }

                Spawn(entityType, Mathf.Clamp(level, 1, 100));
                return true;
            }

            if (string.Equals(parts[0], "/brachio", StringComparison.OrdinalIgnoreCase))
            {
                int level = 60;
                if (parts.Length > 1 && !int.TryParse(parts[1], out level))
                {
                    Reply("Usage: /brachio [level]");
                    return true;
                }

                Spawn(2004, Mathf.Clamp(level, 1, 100));
                return true;
            }

            if (string.Equals(parts[0], "/combatwave", StringComparison.OrdinalIgnoreCase))
            {
                int entityType = 2005;
                int level = 10;
                int count = 5;
                int spacing = 130;
                if ((parts.Length > 1 && !int.TryParse(parts[1], out entityType)) ||
                    (parts.Length > 2 && !int.TryParse(parts[2], out level)) ||
                    (parts.Length > 3 && !int.TryParse(parts[3], out count)) ||
                    (parts.Length > 4 && !int.TryParse(parts[4], out spacing)))
                {
                    Reply("Usage: /combatwave [animalType] [level] [count] [spacing]");
                    return true;
                }

                SpawnWave(
                    entityType,
                    Mathf.Clamp(level, 1, 100),
                    Mathf.Clamp(count, 2, 12),
                    Mathf.Clamp(spacing, 50, 500));
                return true;
            }

            if (string.Equals(parts[0], "/combatstatus", StringComparison.OrdinalIgnoreCase))
            {
                int actionCount = 0;
                if (GameSystem<CombatSystem>.HasInstance())
                {
                    foreach (Durango.Logic.Combat.BattleAction action in
                        GameSystem<CombatSystem>.Instance().GetCurrentBattleActions())
                    {
                        if (action != null)
                        {
                            actionCount++;
                        }
                    }
                }
                Reply("Offline combat: actions=" + actionCount +
                    " active=" + (GameSystem<CombatSystem>.HasInstance() &&
                        GameSystem<CombatSystem>.Instance().CombatMode));
                return true;
            }

            if (string.Equals(parts[0], "/combatline", StringComparison.OrdinalIgnoreCase))
            {
                bool enabled = !AttackAreaLineRenderer.Enabled;
                if (parts.Length > 1)
                {
                    if (string.Equals(parts[1], "on", StringComparison.OrdinalIgnoreCase))
                    {
                        enabled = true;
                    }
                    else if (string.Equals(parts[1], "off", StringComparison.OrdinalIgnoreCase))
                    {
                        enabled = false;
                    }
                    else if (!string.Equals(parts[1], "toggle", StringComparison.OrdinalIgnoreCase))
                    {
                        Reply("Usage: /combatline [on|off|toggle]");
                        return true;
                    }
                }

                AttackAreaLineRenderer.SetEnabled(enabled);
                AnimalAttackAreaLineRenderer.SetEnabled(enabled);
                Reply("Combat attack-area lines (player + animals): " +
                    (enabled ? "ON" : "OFF"));
                return true;
            }

            if (string.Equals(parts[0], "/naturalspawn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parts[0], "/wildspawn", StringComparison.OrdinalIgnoreCase))
            {
                int count = 4;
                if (parts.Length > 1 && !int.TryParse(parts[1], out count))
                {
                    Reply("Usage: " + parts[0].ToLowerInvariant() + " [count]");
                    return true;
                }

                string response;
                OfflineNaturalAnimalSpawner.ForceSpawn(
                    Mathf.Clamp(count, 1, 12),
                    out response);
                Reply(response);
                return true;
            }

            if (string.Equals(parts[0], "/naturalstatus", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parts[0], "/wildstatus", StringComparison.OrdinalIgnoreCase))
            {
                Reply(OfflineNaturalAnimalSpawner.Status());
                return true;
            }

            if (string.Equals(parts[0], "/hp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parts[0], "/sp", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 2)
                {
                    Reply("Usage: " + parts[0].ToLowerInvariant() + " <amount>");
                    return true;
                }

                float amount;
                if (!float.TryParse(
                    parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
                {
                    Reply("Usage: " + parts[0].ToLowerInvariant() + " <amount>");
                    return true;
                }

                string response;
                OfflineCombatRuntime.AddPersistentGauge(
                    string.Equals(parts[0], "/hp", StringComparison.OrdinalIgnoreCase)
                        ? "life"
                        : "stamina",
                    amount,
                    out response);
                Reply(response);
                return true;
            }

            return false;
        }

        private static void Spawn(int entityType, int level)
        {
            if (PlayerBehavior.LocalPlayer == null || Connections.Frontend == null)
            {
                Reply("Enter an offline world before using /combatspawn.");
                return;
            }

            Vector3 spawnPosition = PlayerBehavior.LocalPlayer.CurrentPosition +
                PlayerBehavior.LocalPlayer.transform.forward * 700f;
            SpawnAt(entityType, level, spawnPosition, false, true);
            Reply("Spawning local combat animal type=" + entityType + " level=" + level);
        }

        private static void SpawnWave(int entityType, int level, int count, int spacing)
        {
            if (PlayerBehavior.LocalPlayer == null || Connections.Frontend == null)
            {
                Reply("Enter an offline world before using /combatwave.");
                return;
            }

            Vector3 center = PlayerBehavior.LocalPlayer.CurrentPosition +
                PlayerBehavior.LocalPlayer.transform.forward * 700f;
            Vector3 right = PlayerBehavior.LocalPlayer.transform.right;
            for (int i = 0; i < count; i++)
            {
                int step = (i + 1) / 2;
                float side = i == 0 ? 0f : (i % 2 == 1 ? -step : step) * spacing;
                SpawnAt(entityType, level, center + right * side, i == 0, false);
            }

            Reply("Spawning combat wave type=" + entityType +
                " level=" + level + " count=" + count + " spacing=" + spacing);
        }

        private static string SpawnAt(
            int entityType,
            int level,
            Vector3 spawnPosition,
            bool preferred,
            bool waitForPlayerAttack)
        {
            WorldPosition worldPosition = default(WorldPosition);
            worldPosition.SetFromClientPosition(spawnPosition);
            string entityId = "local-combat-" + Guid.NewGuid().ToString("N");
            if (preferred)
            {
                AutoSelectAnimals.Add(entityId);
            }
            if (waitForPlayerAttack)
            {
                PlayerInitiatedAnimals.Add(entityId);
            }
            AnimalCombatProfile combatProfile = AnimalCombatProfiles.Get(entityType);
            float lifeMax = combatProfile.LifeMaxAt(level, 1f);

            AppearAnimal animal = default(AppearAnimal);
            animal.EntityId = entityId;
            animal.EntityType = (ushort)Mathf.Clamp(entityType, 1, ushort.MaxValue);
            animal.IsAlive = true;
            animal.Level = level;
            animal.Role = "local_combat_test";
            animal.Move = new Move
            {
                EntityId = entityId,
                Movements = new Movement[]
                {
                    new Movement
                    {
                        Path = new Location[]
                        {
                            new Location { Position = worldPosition }
                        }
                    }
                }
            };
            animal.Survival = new Survival
            {
                EntityId = entityId,
                Life = new Gauge(lifeMax, 0f, new GaugeNode[]
                {
                    new GaugeNode { Time = 0.0, Value = lifeMax }
                }),
                Gauges = new Dictionary<string, Gauge>()
            };
            animal.Display = new AnimalDisplay
            {
                EntityId = entityId,
                BaseScale = 1f
            };

            TestAnimals.Add(entityId);
            Connections.Frontend.PushPacket<AppearAnimal>(animal, 0U);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Requested local combat animal entity=" + entityId +
                " type=" + entityType + " level=" + level);
            return entityId;
        }

        internal static bool WaitsForPlayerAttack(string entityId)
        {
            return !string.IsNullOrEmpty(entityId) && PlayerInitiatedAnimals.Contains(entityId);
        }

        internal static void ReleasePlayerInitiatedAnimal(string entityId)
        {
            if (!string.IsNullOrEmpty(entityId))
            {
                PlayerInitiatedAnimals.Remove(entityId);
            }
        }

        internal static void OnAnimalAppeared(AnimalBehavior animal)
        {
            if (animal == null || !TestAnimals.Remove(animal.EntityId))
            {
                return;
            }

            if (!AutoSelectAnimals.Remove(animal.EntityId))
            {
                return;
            }

            CombatGroup combatGroup = UIManager.FindScript<CombatGroup>();
            if (combatGroup != null)
            {
                combatGroup.SetBattleView(CombatGroup.BattleViewMode.Battle);
            }
            if (GameSystem<CombatSystem>.HasInstance())
            {
                GameSystem<CombatSystem>.Instance().SelectTarget(animal.EntityId);
            }
            Reply("Combat target ready: " + animal.GetName());
        }

        private static void Reply(string text)
        {
            if (GameSystem<SocialSystem>.HasInstance())
            {
                GameSystem<SocialSystem>.Instance().AddSystemChat(
                    text, "Combat", false, ChannelType.System);
            }
            else
            {
                UIManager.SystemMsg("Combat", text, 4f);
            }
        }
    }

    [HarmonyPatch(typeof(SocialSystem), "Say", new Type[] { typeof(string), typeof(bool) })]
    internal static class OfflineCombatSayCommandPatch
    {
        private static bool Prefix(string message)
        {
            return !OfflineCombatDebugCommands.TryExecute(message);
        }
    }

    [HarmonyPatch(
        typeof(SocialSystem),
        "Say",
        new Type[] { typeof(string), typeof(string), typeof(bool) })]
    internal static class OfflineCombatConversationCommandPatch
    {
        private static bool Prefix(string message)
        {
            return !OfflineCombatDebugCommands.TryExecute(message);
        }
    }

    [HarmonyPatch(typeof(AnimalManager), "OnAppearAnimal")]
    internal static class SelectLocalCombatTestAnimalPatch
    {
        private static void Postfix(AnimalBehavior animal)
        {
            OfflineCombatDebugCommands.OnAnimalAppeared(animal);
        }
    }
}
