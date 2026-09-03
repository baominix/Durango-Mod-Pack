using System;
using System.Collections.Generic;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal static class CombatDataRegistry
    {
        private static readonly int[] InitialAnimalTypes =
            new int[] { 2027, 2037, 2039, 2001 };

        private static Dictionary<int, AnimalCombatProfile> _profiles =
            new Dictionary<int, AnimalCombatProfile>();
        private static Dictionary<string, PlayerActionImpactProfile>
            _playerActionImpacts =
                new Dictionary<string, PlayerActionImpactProfile>(
                    StringComparer.Ordinal);

        internal static bool IsReady { get; private set; }

        internal static CombatDataLoadReport LoadEmbeddedData()
        {
            Reset();
            CombatDataLoadReport report = new CombatDataLoadReport();

            string animalJson = EmbeddedCombatData.ReadText(
                EmbeddedCombatData.AnimalJson,
                report);
            string rootMotionJson = EmbeddedCombatData.ReadText(
                EmbeddedCombatData.RootMotionJson,
                report);
            string playerBattleActionsJson = EmbeddedCombatData.ReadText(
                EmbeddedCombatData.PlayerBattleActionsJson,
                report);

            Dictionary<int, AnimalCombatProfile> loaded =
                OriginalAnimalDataLoader.Load(
                    animalJson,
                    InitialAnimalTypes,
                    report);

            SaurusRootMotionData.Load(
                rootMotionJson,
                report);
            _playerActionImpacts = PlayerActionImpactDataLoader.Load(
                playerBattleActionsJson,
                report);

            foreach (KeyValuePair<int, AnimalCombatProfile> pair in loaded)
            {
                AnimalCombatProfile profile = pair.Value;
                string resourceName =
                    EmbeddedCombatData.FrameworkFor(profile.Framework);
                if (string.IsNullOrEmpty(resourceName))
                {
                    report.Errors.Add(
                        "No embedded framework is registered for animal " +
                        profile.EntityTypeId + " (" +
                        (profile.Framework ?? string.Empty) + ").");
                    continue;
                }

                string frameworkText = EmbeddedCombatData.ReadText(
                    resourceName,
                    report);
                profile.FrameworkData = FrameworkReferenceReader.Read(
                    frameworkText,
                    resourceName,
                    report);
                ValidateExpectedAttacks(profile, report);
            }

            _profiles = loaded;
            report.ProfileCount = loaded.Count;
            foreach (AnimalCombatProfile profile in loaded.Values)
            {
                if (profile.FrameworkData != null)
                {
                    report.FrameworkCount++;
                }
            }

            IsReady =
                report.IsValid &&
                loaded.Count == InitialAnimalTypes.Length;
            return report;
        }

        internal static bool TryGetProfile(
            int entityTypeId,
            out AnimalCombatProfile profile)
        {
            return _profiles.TryGetValue(entityTypeId, out profile);
        }

        internal static bool TryGetPlayerActionImpact(
            string actionId,
            int hitIndex,
            out PlayerActionHitImpact impact)
        {
            impact = null;
            PlayerActionImpactProfile action;
            return !string.IsNullOrEmpty(actionId) && hitIndex >= 0 &&
                _playerActionImpacts.TryGetValue(actionId, out action) &&
                action != null && action.Hits != null &&
                hitIndex < action.Hits.Length &&
                (impact = action.Hits[hitIndex]) != null;
        }

        internal static void Reset()
        {
            _profiles = new Dictionary<int, AnimalCombatProfile>();
            _playerActionImpacts =
                new Dictionary<string, PlayerActionImpactProfile>(
                    StringComparer.Ordinal);
            SaurusRootMotionData.Reset();
            IsReady = false;
        }

        private static void ValidateExpectedAttacks(
            AnimalCombatProfile profile,
            CombatDataLoadReport report)
        {
            if (profile == null || profile.FrameworkData == null)
            {
                return;
            }

            string[] expected;
            switch (profile.EntityTypeId)
            {
                case 2027:
                    expected = new string[]
                    {
                        "tricera_dash", "tricera_once", "tricera_head"
                    };
                    break;
                case 2037:
                    expected = new string[]
                    {
                        "phenaco_jump", "phenaco_bite",
                        "phenaco_attack_escape"
                    };
                    break;
                case 2039:
                case 2001:
                    expected = new string[]
                    {
                        "raptor_dash", "raptor_jump", "raptor_attack"
                    };
                    break;
                default:
                    expected = new string[0];
                    break;
            }

            int i;
            for (i = 0; i < expected.Length; i++)
            {
                if (!ContainsIgnoreCase(
                    profile.FrameworkData.AttackKeys,
                    expected[i]))
                {
                    report.Errors.Add(
                        "Framework for animal " + profile.EntityTypeId +
                        " is missing expected attack '" + expected[i] + "'.");
                }
            }
        }

        private static bool ContainsIgnoreCase(
            List<string> values,
            string expected)
        {
            int i;
            for (i = 0; i < values.Count; i++)
            {
                if (string.Equals(
                    values[i],
                    expected,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
