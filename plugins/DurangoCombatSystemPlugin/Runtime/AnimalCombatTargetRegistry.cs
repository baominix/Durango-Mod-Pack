using System;
using System.Collections.Generic;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Durango.Terrain;
using Durango.Utils;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.Runtime
{
    internal sealed class AnimalCombatTarget
    {
        internal readonly AnimalBehavior Animal;
        internal readonly AnimalCombatProfile Profile;
        internal readonly string EntityId;
        internal readonly int EntityTypeId;
        internal readonly int Level;
        internal readonly Vector2 Position;
        internal readonly float Yaw;
        internal readonly float Radius;
        internal readonly float CurrentLife;
        internal readonly float MaximumLife;
        internal readonly bool IsAlive;

        internal AnimalCombatTarget(
            AnimalBehavior animal,
            AnimalCombatProfile profile,
            float currentLife,
            float maximumLife)
        {
            Animal = animal;
            Profile = profile;
            EntityId = animal.EntityId;
            EntityTypeId = animal.EntityTypeId;
            Level = Math.Max(1, animal.Level);
            Vector3 world = Util.ClientPositionToWorldPosition(
                animal.CurrentPosition);
            Position = new Vector2(world.x, world.z);
            Yaw = animal.CurrentYaw;
            // Match Durango.Logic.Combat.UsingAction exactly: the client
            // stops auto-approach at entity bound_radius * localScale.x plus
            // action.meta.use_range.  CharacterBehavior.XRadius/YRadius are
            // collider dimensions and are not interchangeable with the YAML
            // navigation/combat bound (Zebraceratops is the clearest case:
            // 100 * 0.42 versus bound_radius 200 * 0.42).  Using the collider
            // value here made the client stop walking before this offline
            // runtime considered a selected target to be in range.
            float scale = Mathf.Abs(animal.transform.localScale.x);
            Radius = Mathf.Max(
                1f,
                ObjectManager.GetBoundRadius(animal.EntityTypeId)) *
                Mathf.Max(0.01f, scale);
            CurrentLife = currentLife;
            MaximumLife = maximumLife;
            IsAlive = animal.IsAlive && currentLife > 0f;
        }
    }

    internal sealed class AnimalCombatTargetRegistry
    {
        private sealed class LifeState
        {
            internal int ObjectInstanceId;
            internal float Current;
            internal float Maximum;
            internal float Velocity;
            internal double LastUpdatedAt;
        }

        private readonly Dictionary<string, LifeState> _lifeStates =
            new Dictionary<string, LifeState>(StringComparer.Ordinal);

        internal bool TryGet(
            string entityId,
            double now,
            out AnimalCombatTarget target)
        {
            target = null;
            if (string.IsNullOrEmpty(entityId))
            {
                return false;
            }

            // The game already keeps animals indexed by entity id.  Use that
            // index on the hot path; the scene scan remains only as a fallback
            // for locally-created objects that have not entered the manager.
            if (Singleton<AnimalManager>.HasInstance())
            {
                AnimalBehavior indexed =
                    Singleton<AnimalManager>.Instance().GetAnimal(entityId);
                if (indexed != null)
                {
                    return TryCreate(indexed, now, out target);
                }
            }

            AnimalBehavior[] animals =
                UnityEngine.Object.FindObjectsOfType<AnimalBehavior>();
            int i;
            for (i = 0; i < animals.Length; i++)
            {
                AnimalBehavior animal = animals[i];
                if (animal != null &&
                    string.Equals(
                        animal.EntityId,
                        entityId,
                        StringComparison.Ordinal))
                {
                    return TryCreate(animal, now, out target);
                }
            }
            return false;
        }

        internal List<AnimalCombatTarget> GetEnemyCandidates(double now)
        {
            List<AnimalCombatTarget> result =
                new List<AnimalCombatTarget>();
            AnimalBehavior[] animals =
                UnityEngine.Object.FindObjectsOfType<AnimalBehavior>();
            int i;
            for (i = 0; i < animals.Length; i++)
            {
                AnimalBehavior animal = animals[i];
                if (animal == null || ObjectIdentifier.IsAlly(animal.gameObject))
                {
                    continue;
                }

                AnimalCombatTarget target;
                if (TryCreate(animal, now, out target) && target.IsAlive)
                {
                    result.Add(target);
                }
            }
            return result;
        }

        internal Gauge ApplyDamage(
            AnimalCombatTarget target,
            int damage,
            double at,
            out float remainingLife)
        {
            LifeState state = GetOrCreateState(target.Animal, at);
            Synchronize(state, at);
            state.Current = Mathf.Clamp(
                state.Current - Math.Max(0, damage),
                0f,
                state.Maximum);
            if (state.Current <= 0f)
            {
                state.Velocity = 0f;
            }
            remainingLife = state.Current;
            return BuildGauge(state, at);
        }

        internal Gauge SetLifeVelocity(
            AnimalCombatTarget target,
            float velocity,
            double at,
            out float remainingLife)
        {
            LifeState state = GetOrCreateState(target.Animal, at);
            Synchronize(state, at);
            state.Velocity = state.Current <= 0f ? 0f : velocity;
            remainingLife = state.Current;
            return BuildGauge(state, at);
        }

        internal void Clear()
        {
            _lifeStates.Clear();
        }

        private bool TryCreate(
            AnimalBehavior animal,
            double now,
            out AnimalCombatTarget target)
        {
            target = null;
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return false;
            }
            if (ObjectIdentifier.IsAlly(animal.gameObject))
            {
                return false;
            }

            AnimalCombatProfile profile;
            if (!CombatDataRegistry.TryGetProfile(
                animal.EntityTypeId,
                out profile))
            {
                return false;
            }

            LifeState state = GetOrCreateState(animal, now);
            target = new AnimalCombatTarget(
                animal,
                profile,
                state.Current,
                state.Maximum);
            return true;
        }

        private LifeState GetOrCreateState(
            AnimalBehavior animal,
            double now)
        {
            LifeState state;
            int objectId = animal.gameObject.GetInstanceID();
            if (_lifeStates.TryGetValue(animal.EntityId, out state) &&
                state.ObjectInstanceId == objectId)
            {
                Synchronize(state, now);
                return state;
            }

            Gauge life = animal.Life;
            float current = life == null ? 1f : life.Get(now);
            float maximum = life == null ? current : life.Max(now);
            if (maximum <= 0f)
            {
                maximum = Mathf.Max(1f, current);
            }
            current = Mathf.Clamp(current, 0f, maximum);
            state = new LifeState();
            state.ObjectInstanceId = objectId;
            state.Current = current;
            state.Maximum = maximum;
            state.Velocity = 0f;
            state.LastUpdatedAt = now;
            _lifeStates[animal.EntityId] = state;
            return state;
        }

        private static void Synchronize(LifeState state, double at)
        {
            if (state == null)
            {
                return;
            }
            if (state.LastUpdatedAt <= 0.0)
            {
                state.LastUpdatedAt = at;
                return;
            }
            double elapsed = Math.Max(0.0, at - state.LastUpdatedAt);
            if (elapsed > 0.0 && Math.Abs(state.Velocity) > 0.0001f)
            {
                state.Current = Mathf.Clamp(
                    state.Current + state.Velocity * (float)elapsed,
                    0f,
                    state.Maximum);
            }
            state.LastUpdatedAt = at;
            if (state.Current <= 0f && state.Velocity < 0f)
            {
                state.Velocity = 0f;
            }
        }

        private static Gauge BuildGauge(LifeState state, double at)
        {
            if (state.Velocity < -0.0001f && state.Current > 0f)
            {
                double reachesMinimumAt =
                    at + state.Current / -state.Velocity;
                return new Gauge(
                    state.Maximum,
                    0f,
                    new GaugeNode[]
                    {
                        new GaugeNode(at, state.Current),
                        new GaugeNode(reachesMinimumAt, 0f)
                    });
            }
            return new Gauge(
                state.Maximum,
                0f,
                new GaugeNode[] { new GaugeNode(at, state.Current) });
        }
    }
}
