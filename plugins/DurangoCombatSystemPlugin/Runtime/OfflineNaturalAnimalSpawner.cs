using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Network;
using Durango.Utils;
using Messages;
using UnityEngine;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal static class OfflineNaturalAnimalSpawner
    {
        internal static readonly bool Enabled = false;
        private const string EntityPrefix = "local-natural-";
        private const int TargetPopulation = 8;
        private const float ScanInterval = 3f;
        private const float SpawnInterval = 8f;
        private const float DespawnDistance = 4200f;
        private const float DeadDespawnDistance = 2600f;

        private static readonly HashSet<string> SpawnedIds = new HashSet<string>();
        private static readonly Dictionary<string, float> SpawnedAt =
            new Dictionary<string, float>();
        private static float _nextScanAt;
        private static float _nextSpawnAt;
        private static bool _readyLogged;

        internal static void Tick()
        {
            if (!Enabled)
            {
                return;
            }

            if (Time.unscaledTime < _nextScanAt)
            {
                return;
            }
            _nextScanAt = Time.unscaledTime + ScanInterval;

            if (!IsReady())
            {
                return;
            }

            if (!_readyLogged)
            {
                _readyLogged = true;
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Offline natural animal spawner ready (session only)");
            }

            int liveCount = CleanupAndCount();
            if (liveCount >= TargetPopulation ||
                Time.unscaledTime < _nextSpawnAt)
            {
                return;
            }

            int toSpawn = Mathf.Clamp(TargetPopulation - liveCount, 1, 2);
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnOne(false);
            }
            _nextSpawnAt = Time.unscaledTime + SpawnInterval;
        }

        internal static void Reset()
        {
            SpawnedIds.Clear();
            SpawnedAt.Clear();
            _nextScanAt = 0f;
            _nextSpawnAt = 0f;
            _readyLogged = false;
        }

        internal static void ForceSpawn(int count, out string response)
        {
            if (!Enabled)
            {
                response = "Natural animal spawner is disabled.";
                return;
            }

            if (!IsReady())
            {
                response = "Enter an offline world before using /naturalspawn.";
                return;
            }

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (SpawnOne(true))
                {
                    spawned++;
                }
            }
            _nextSpawnAt = Time.unscaledTime + SpawnInterval;
            response = "Natural animals spawned: " + spawned +
                " active=" + CleanupAndCount();
        }

        internal static string Status()
        {
            if (!Enabled)
            {
                return "Natural animal spawner: disabled.";
            }

            if (!IsReady())
            {
                return "Natural animal spawner: waiting for offline world.";
            }

            return "Natural animal spawner: active=" + CleanupAndCount() +
                " target=" + TargetPopulation +
                " session-only";
        }

        private static bool IsReady()
        {
            return GameManager.IsMainScene &&
                PlayerBehavior.LocalPlayer != null &&
                Connections.Frontend != null &&
                Singleton<AnimalManager>.HasInstance();
        }

        private static int CleanupAndCount()
        {
            int liveCount = 0;
            List<string> remove = new List<string>();
            foreach (string id in SpawnedIds)
            {
                AnimalBehavior animal = Singleton<AnimalManager>.Instance().GetAnimal(id);
                if (animal == null)
                {
                    float createdAt;
                    if (SpawnedAt.TryGetValue(id, out createdAt) &&
                        Time.unscaledTime - createdAt < 30f)
                    {
                        continue;
                    }
                    remove.Add(id);
                    continue;
                }

                float distance = HorizontalDistance(
                    PlayerBehavior.LocalPlayer.CurrentPosition,
                    animal.CurrentPosition);
                bool alive = animal.IsAlive && (animal.Life == null || animal.Life.Get() > 0f);
                if ((!alive && distance > DeadDespawnDistance) ||
                    distance > DespawnDistance)
                {
                    animal.Disappear();
                    remove.Add(id);
                    OfflineCombatBackendPlugin.Log.LogInfo(
                        "Natural animal despawned entity=" + id +
                        " distance=" + distance.ToString("F0") +
                        " alive=" + alive);
                    continue;
                }

                if (alive)
                {
                    liveCount++;
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                SpawnedIds.Remove(remove[i]);
                SpawnedAt.Remove(remove[i]);
            }
            return liveCount;
        }

        private static bool SpawnOne(bool nearFront)
        {
            AnimalCombatProfile profile = PickProfile();
            if (profile == null)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Natural animal spawn skipped: no spawnable profiles");
                return false;
            }

            Vector3 spawnPosition = PickSpawnPosition(profile, nearFront);
            WorldPosition worldPosition = default(WorldPosition);
            worldPosition.SetFromClientPosition(spawnPosition);

            string entityId = EntityPrefix + Guid.NewGuid().ToString("N");
            int level = PickLevel();
            float lifeMax = PickLife(profile, level);

            AppearAnimal animal = default(AppearAnimal);
            animal.EntityId = entityId;
            animal.EntityType = (ushort)Mathf.Clamp(profile.EntityTypeId, 1, ushort.MaxValue);
            animal.IsAlive = true;
            animal.Level = level;
            animal.Role = "local_natural_wild";
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

            SpawnedIds.Add(entityId);
            SpawnedAt[entityId] = Time.unscaledTime;
            Connections.Frontend.PushPacket<AppearAnimal>(animal, 0U);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Natural animal spawned entity=" + entityId +
                " type=" + profile.EntityTypeId +
                " name=" + profile.Name +
                " kind=" + profile.AnimalType +
                " level=" + level +
                " life=" + lifeMax.ToString("F0"));
            return true;
        }

        private static AnimalCombatProfile PickProfile()
        {
            bool proactive = UnityEngine.Random.value < 0.35f;
            List<AnimalCombatProfile> candidates =
                AnimalCombatProfiles.GetSpawnCandidates(proactive);
            if (candidates.Count == 0 && proactive)
            {
                candidates = AnimalCombatProfiles.GetSpawnCandidates(false);
            }
            if (candidates.Count == 0)
            {
                return AnimalCombatProfiles.Get(2006);
            }

            for (int tries = 0; tries < 12; tries++)
            {
                AnimalCombatProfile profile = candidates[
                    UnityEngine.Random.Range(0, candidates.Count)];
                if (profile.SizeLevel <= 4 || UnityEngine.Random.value < 0.18f)
                {
                    return profile;
                }
            }
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private static Vector3 PickSpawnPosition(AnimalCombatProfile profile, bool nearFront)
        {
            Vector3 origin = PlayerBehavior.LocalPlayer.CurrentPosition;
            Vector3 direction;
            if (nearFront)
            {
                direction = Quaternion.Euler(
                    0f,
                    UnityEngine.Random.Range(-35f, 35f),
                    0f) * PlayerBehavior.LocalPlayer.transform.forward;
            }
            else
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            float minDistance = profile.IsProactive
                ? Mathf.Max(1500f, profile.AggroRange + 260f)
                : 850f;
            float maxDistance = nearFront
                ? minDistance + 450f
                : minDistance + 900f;
            float distance = UnityEngine.Random.Range(minDistance, maxDistance);
            Vector3 spawn = origin + direction.normalized * distance;
            spawn.y = origin.y;
            return spawn;
        }

        private static int PickLevel()
        {
            int playerLevel = 10;
            if (GameSystem<StatisticsSystem>.HasInstance())
            {
                playerLevel = Mathf.Max(1, GameSystem<StatisticsSystem>.Instance().Level);
            }

            int low = Mathf.Max(1, playerLevel - 8);
            int high = Mathf.Max(low + 1, playerLevel + 3);
            return Mathf.Clamp(UnityEngine.Random.Range(low, high), 1, 100);
        }

        private static float PickLife(AnimalCombatProfile profile, int level)
        {
            return profile.LifeMaxAt(level, 1f);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
