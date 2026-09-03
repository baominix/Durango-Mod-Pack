using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Durango.Logic;
using Durango.Network;
using Messages;
using UnityEngine;

namespace Baominix.DurangoOriginal.DeveloperMode
{
    internal static class CombatTestCommands
    {
        private static readonly Regex FirstNumber = new Regex(
            @"-?\d+(?:\.\d+)?",
            RegexOptions.CultureInvariant);

        internal static bool IsCommand(string command)
        {
            return command == "/hp" || command == "/sp" ||
                command == "/combatspawn" || command == "/combatwave" ||
                command == "/combatstatus" ||
                command == "/combatcontext" ||
                command == "/combatintent" ||
                command == "/combathelp";
        }

        internal static void Execute(string command, string[] parts)
        {
            if (command == "/hp" || command == "/sp")
            {
                ExecuteGauge(command, parts);
            }
            else if (command == "/combatspawn")
            {
                ExecuteSpawn(parts);
            }
            else if (command == "/combatwave")
            {
                ExecuteWave(parts);
            }
            else if (command == "/combatstatus")
            {
                ExecuteStatus();
            }
            else if (command == "/combatcontext")
            {
                ExecuteRuntimeReport(
                    parts,
                    "/combatcontext",
                    "TryGetSaurusContextReport");
            }
            else if (command == "/combatintent")
            {
                ExecuteRuntimeReport(
                    parts,
                    "/combatintent",
                    "TryGetSaurusIntentReport");
            }
            else
            {
                ReplyHelp();
            }
        }

        private static void ExecuteGauge(string command, string[] parts)
        {
            float amount;
            if (parts.Length != 2 || !float.TryParse(
                parts[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out amount))
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("usage",
                        command + " <amount>"));
                return;
            }

            Type runtime = FindType(
                "Baominix.DurangoOriginal.CombatSystem.Runtime.CombatRuntime");
            MethodInfo method = runtime == null
                ? null
                : runtime.GetMethod(
                    "TryAddPlayerGauge",
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic);
            if (method == null)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("combat_plugin_unavailable"));
                return;
            }

            object[] parameters = new object[]
            {
                command == "/hp" ? "life" : "stamina",
                amount,
                null
            };
            bool success = (bool)method.Invoke(null, parameters);
            string response = parameters[2] as string;
            DeveloperCommandRouter.Reply(
                string.IsNullOrEmpty(response)
                    ? DeveloperModeLocalization.Get(
                        success ? "gauge_updated" : "gauge_failed")
                    : DeveloperModeLocalization.TranslateCombatResponse(response));
        }

        private static void ExecuteSpawn(string[] parts)
        {
            int entityType = 2039;
            int level = 10;
            int rows = 1;
            int columns = 1;
            int spacing = 160;
            if ((parts.Length > 1 &&
                    !int.TryParse(parts[1], out entityType)) ||
                (parts.Length > 2 &&
                    !int.TryParse(parts[2], out level)) ||
                (parts.Length > 3 &&
                    !int.TryParse(parts[3], out rows)) ||
                (parts.Length > 4 &&
                    !int.TryParse(parts[4], out columns)) ||
                (parts.Length > 5 &&
                    !int.TryParse(parts[5], out spacing)) ||
                parts.Length == 4 || parts.Length > 6)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("usage",
                        "/combatspawn [type] [level] [rows columns] [spacing]"));
                return;
            }

            object profile;
            if (!TryGetCombatProfile(entityType, out profile) ||
                !HasLocalWorld())
            {
                return;
            }

            level = Mathf.Clamp(level, 1, 100);
            rows = Mathf.Clamp(rows, 1, 6);
            columns = Mathf.Clamp(columns, 1, 8);
            spacing = Mathf.Clamp(spacing, 50, 500);
            if (rows * columns > 24)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("spawn_limit"));
                return;
            }

            Vector3 center =
                PlayerBehavior.LocalPlayer.CurrentPosition +
                PlayerBehavior.LocalPlayer.transform.forward * 700f;
            Vector3 forward =
                PlayerBehavior.LocalPlayer.transform.forward;
            Vector3 right = PlayerBehavior.LocalPlayer.transform.right;
            int row;
            int column;
            for (row = 0; row < rows; row++)
            {
                float depth =
                    (row - (rows - 1) * 0.5f) * spacing;
                for (column = 0; column < columns; column++)
                {
                    float side =
                        (column - (columns - 1) * 0.5f) * spacing;
                    SpawnAt(
                        entityType,
                        level,
                        center + forward * depth + right * side,
                        profile);
                }
            }

            int total = rows * columns;
            if (total == 1)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("spawned_one",
                        entityType, level));
            }
            else
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("spawned_grid",
                        entityType, level, rows, columns, spacing, total));
            }
        }

        private static void ExecuteWave(string[] parts)
        {
            int entityType = 2039;
            int level = 10;
            int count = 3;
            int spacing = 160;
            if ((parts.Length > 1 &&
                    !int.TryParse(parts[1], out entityType)) ||
                (parts.Length > 2 &&
                    !int.TryParse(parts[2], out level)) ||
                (parts.Length > 3 &&
                    !int.TryParse(parts[3], out count)) ||
                (parts.Length > 4 &&
                    !int.TryParse(parts[4], out spacing)) ||
                parts.Length > 5)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("usage",
                        "/combatwave [type] [level] [count] [spacing]"));
                return;
            }

            object profile;
            if (!TryGetCombatProfile(entityType, out profile) ||
                !HasLocalWorld())
            {
                return;
            }

            level = Mathf.Clamp(level, 1, 100);
            count = Mathf.Clamp(count, 1, 12);
            spacing = Mathf.Clamp(spacing, 50, 500);
            Vector3 center =
                PlayerBehavior.LocalPlayer.CurrentPosition +
                PlayerBehavior.LocalPlayer.transform.forward * 700f;
            Vector3 right = PlayerBehavior.LocalPlayer.transform.right;
            int i;
            for (i = 0; i < count; i++)
            {
                int step = (i + 1) / 2;
                float side = i == 0
                    ? 0f
                    : (i % 2 == 1 ? -step : step) * spacing;
                SpawnAt(
                    entityType,
                    level,
                    center + right * side,
                    profile);
            }
            DeveloperCommandRouter.Reply(
                DeveloperModeLocalization.Get("spawned_wave",
                    entityType, level, count));
        }

        private static void ExecuteStatus()
        {
            int actionCount = 0;
            bool combatMode = false;
            if (GameSystem<global::CombatSystem>.HasInstance())
            {
                global::CombatSystem combat =
                    GameSystem<global::CombatSystem>.Instance();
                combatMode = combat.CombatMode;
                foreach (Durango.Logic.Combat.BattleAction action in
                    combat.GetCurrentBattleActions())
                {
                    if (action != null)
                    {
                        actionCount++;
                    }
                }
            }
            DeveloperCommandRouter.Reply(
                DeveloperModeLocalization.Get("combat_status",
                    actionCount, combatMode));
        }

        private static void ExecuteRuntimeReport(
            string[] parts,
            string command,
            string methodName)
        {
            if (parts.Length > 2)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("usage",
                        command + " [nearest|all|entityId]"));
                return;
            }

            string selector = parts.Length == 2
                ? parts[1]
                : "nearest";
            Type runtime = FindType(
                "Baominix.DurangoOriginal.CombatSystem.Runtime.CombatRuntime");
            MethodInfo method = runtime == null
                ? null
                : runtime.GetMethod(
                    methodName,
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic);
            if (method == null)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("combat_plugin_unavailable"));
                return;
            }

            object[] parameters = new object[] { selector, null };
            bool success = (bool)method.Invoke(null, parameters);
            string[] lines = parameters[1] as string[];
            if (lines == null || lines.Length == 0)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get(
                        success ? "no_report" : "report_failed"));
                return;
            }

            int i;
            for (i = 0; i < lines.Length; i++)
            {
                DeveloperCommandRouter.Reply(lines[i]);
            }
        }

        private static bool HasLocalWorld()
        {
            if (PlayerBehavior.LocalPlayer != null &&
                Connections.Frontend != null)
            {
                return true;
            }
            DeveloperCommandRouter.Reply(
                DeveloperModeLocalization.Get("enter_world"));
            return false;
        }

        private static bool TryGetCombatProfile(
            int entityType,
            out object profile)
        {
            profile = null;
            Type registry = FindType(
                "Baominix.DurangoOriginal.CombatSystem.Data.CombatDataRegistry");
            MethodInfo method = registry == null
                ? null
                : registry.GetMethod(
                    "TryGetProfile",
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic);
            if (method == null)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("combat_plugin_unavailable"));
                return false;
            }

            object[] parameters = new object[] { entityType, null };
            bool success = (bool)method.Invoke(null, parameters);
            profile = parameters[1];
            if (!success || profile == null)
            {
                DeveloperCommandRouter.Reply(
                    DeveloperModeLocalization.Get("unsupported_type",
                        entityType));
                return false;
            }
            return true;
        }

        private static void SpawnAt(
            int entityType,
            int level,
            Vector3 spawnPosition,
            object profile)
        {
            WorldPosition worldPosition = default(WorldPosition);
            worldPosition.SetFromClientPosition(spawnPosition);
            string entityId =
                "local-combat-" + Guid.NewGuid().ToString("N");
            float lifeMaximum = EvaluateLifeMaximum(profile, level);
            float representScale = ReadPositiveProfileSingle(
                profile,
                "RepresentScale",
                1f);

            AppearAnimal animal = default(AppearAnimal);
            animal.EntityId = entityId;
            animal.EntityType = (ushort)entityType;
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
                Life = new Gauge(
                    lifeMaximum,
                    0f,
                    new GaugeNode[]
                    {
                        new GaugeNode(0.0, lifeMaximum)
                    }),
                Gauges = new Dictionary<string, Gauge>()
            };
            animal.Display = new AnimalDisplay
            {
                EntityId = entityId,
                BaseScale = representScale
            };

            Connections.Frontend.PushPacket(animal, 0U);
        }

        private static float EvaluateLifeMaximum(object profile, int level)
        {
            float coefficient = 1f;
            FieldInfo field = profile.GetType().GetField(
                "LifeMaxFormula",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            string formula = field == null
                ? null
                : field.GetValue(profile) as string;
            if (!string.IsNullOrEmpty(formula))
            {
                Match match = FirstNumber.Match(formula);
                float parsed;
                if (match.Success && float.TryParse(
                    match.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed) && parsed > 0f)
                {
                    coefficient = parsed;
                }
            }

            float adjustedLevel = Mathf.Max(1, level) + 24f;
            return Mathf.Max(
                1f,
                coefficient * adjustedLevel * adjustedLevel);
        }

        private static float ReadPositiveProfileSingle(
            object profile,
            string fieldName,
            float fallback)
        {
            if (profile == null || string.IsNullOrEmpty(fieldName))
            {
                return fallback;
            }
            FieldInfo field = profile.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (field == null)
            {
                return fallback;
            }
            object raw = field.GetValue(profile);
            if (raw is float)
            {
                float value = (float)raw;
                return value > 0f ? value : fallback;
            }
            return fallback;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int i;
            for (i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static void ReplyHelp()
        {
            DeveloperCommandRouter.Reply(DeveloperModeLocalization.Get("combat_commands"));
            DeveloperCommandRouter.Reply("/hp <amount>, /sp <amount>");
            DeveloperCommandRouter.Reply(
                "/combatspawn [type] [level] " +
                "[rows columns] [spacing]");
            DeveloperCommandRouter.Reply(
                "/combatwave [type] [level] [count] [spacing]");
            DeveloperCommandRouter.Reply(
                "/combatstatus, " +
                "/combatcontext [nearest|all|entityId]");
            DeveloperCommandRouter.Reply(
                "/combatintent [nearest|all|entityId]");
        }
    }
}
