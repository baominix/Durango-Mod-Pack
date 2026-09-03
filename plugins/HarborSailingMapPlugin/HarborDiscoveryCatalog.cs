using System;
using System.Collections.Generic;
using Messages;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    // DiscoveryInfo is server-owned in the live game. The offline backend
    // rebuilds it from the same original region_templates resource used by
    // world simulation. Terrain .bytes files contain geometry, not fauna.
    internal static class HarborDiscoveryCatalog
    {
        private sealed class Record
        {
            internal Pair<string, bool>[] Biocoms;
            internal Pair<ushort, bool>[] Animals;
        }

        private static readonly Dictionary<string, Record> Records =
            new Dictionary<string, Record>();
        private static bool _loaded;

        internal static bool TryCreate(string templateId, out DiscoveryInfo info)
        {
            EnsureLoaded();
            Record record;
            if (string.IsNullOrEmpty(templateId) ||
                !Records.TryGetValue(templateId, out record))
            {
                info = default(DiscoveryInfo);
                return false;
            }

            info = new DiscoveryInfo
            {
                TemplateId = templateId,
                BiocomNames = record.Biocoms,
                AnimalTypes = record.Animals
            };
            return true;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            try
            {
                TextAsset asset =
                    Resources.Load("offline/assets/region_templates") as TextAsset;
                if (asset == null)
                {
                    HarborSailingMapPlugin.Log.LogError(
                        "Cannot load region_templates for Harbor discovery info");
                    return;
                }

                JObject root = JObject.Parse(asset.text);
                for (int i = 0; i < HarborRoutes.Targets.Length; i++)
                {
                    SailTarget target = HarborRoutes.Targets[i];
                    if (target.Kind != HarborIslandKind.Unstable ||
                        Records.ContainsKey(target.RegionTemplateId))
                    {
                        continue;
                    }

                    JToken region = root[target.RegionTemplateId];
                    if (region == null)
                    {
                        HarborSailingMapPlugin.Log.LogWarning(
                            "No discovery source for region template " +
                            target.RegionTemplateId);
                        continue;
                    }
                    Records[target.RegionTemplateId] = BuildRecord(region);
                }

                HarborSailingMapPlugin.Log.LogInfo(
                    "Loaded original discovery fauna for " + Records.Count +
                    " Harbor region templates");
            }
            catch (Exception exception)
            {
                HarborSailingMapPlugin.Log.LogError(
                    "Harbor discovery catalog load failed: " + exception);
            }
        }

        private static Record BuildRecord(JToken region)
        {
            int closedCraterHerd = Integer(region["closed_crater_herd_type"]);
            List<ushort> animals = new List<ushort>();
            JToken herds = region["herds"];
            JObject herdGroups = herds as JObject;
            if (herdGroups != null)
            {
                foreach (JProperty group in herdGroups.Properties())
                {
                    JArray spawns = group.Value["spawns"] as JArray;
                    if (spawns == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < spawns.Count; i++)
                    {
                        int herdType = Integer(spawns[i]);
                        if (herdType <= 0 || herdType == closedCraterHerd)
                        {
                            continue;
                        }

                        int entityType = herdType / 100;
                        if (entityType <= 0 || entityType > ushort.MaxValue)
                        {
                            continue;
                        }
                        ushort value = (ushort)entityType;
                        if (!animals.Contains(value))
                        {
                            animals.Add(value);
                        }
                    }
                }
            }

            Pair<ushort, bool>[] animalPairs =
                new Pair<ushort, bool>[animals.Count];
            for (int i = 0; i < animals.Count; i++)
            {
                animalPairs[i] = new Pair<ushort, bool>(animals[i], true);
            }

            Pair<string, bool>[] biocoms = closedCraterHerd > 0
                ? new Pair<string, bool>[]
                {
                    new Pair<string, bool>("Crater", true)
                }
                : new Pair<string, bool>[0];

            return new Record
            {
                Biocoms = biocoms,
                Animals = animalPairs
            };
        }

        private static int Integer(JToken token)
        {
            int value;
            return token != null && int.TryParse(token.ToString(), out value)
                ? value
                : 0;
        }
    }
}
