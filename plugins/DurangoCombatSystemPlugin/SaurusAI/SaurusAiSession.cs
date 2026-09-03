using System;
using System.Collections.Generic;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Baominix.DurangoOriginal.CombatSystem.Damage;
using Baominix.DurangoOriginal.CombatSystem.Runtime;
using Durango.Utils;
using Shared.Animal;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal sealed class SaurusAiSession : IDisposable
    {
        private const double ReconcileIntervalSeconds = 0.75;

        private readonly int _generation;
        private readonly Dictionary<string, SaurusAnimalController>
            _controllers =
                new Dictionary<string, SaurusAnimalController>(
                    StringComparer.Ordinal);
        private readonly SaurusCoreTuning _tuning;
        private AnimalManager _manager;
        private double _nextReconcileAt;
        private bool _disposed;

        internal event SaurusAttackCommittedHandler AttackCommitted;
        internal event SaurusGroggyGaugeChangedHandler GroggyGaugeChanged;
        internal event SaurusAnimalStatusChangedHandler StatusChanged;

        internal SaurusAiSession(int generation)
        {
            _generation = generation;
            _tuning = SaurusCoreTuning.CreateDefault();
            string validation = _tuning.Validate();
            if (!string.IsNullOrEmpty(validation))
            {
                throw new InvalidOperationException(validation);
            }
        }

        internal void Process(double now)
        {
            if (_disposed)
            {
                return;
            }
            TryBindManager();
            if (now >= _nextReconcileAt)
            {
                Reconcile(now);
                _nextReconcileAt = now + ReconcileIntervalSeconds;
            }

            PlayerBehavior player = PlayerBehavior.LocalPlayer;
            List<string> stale = null;
            foreach (KeyValuePair<string, SaurusAnimalController> pair in
                _controllers)
            {
                SaurusAnimalController controller = pair.Value;
                if (!IsControllerCurrent(controller))
                {
                    if (stale == null)
                    {
                        stale = new List<string>();
                    }
                    stale.Add(pair.Key);
                    continue;
                }
                controller.Process(now, player);
            }

            if (stale != null)
            {
                int i;
                for (i = 0; i < stale.Count; i++)
                {
                    RemoveController(stale[i]);
                }
            }
        }

        internal void NotifyPlayerAttack(
            AnimalCombatTarget target,
            long actionInstanceId,
            string actionKey,
            double now)
        {
            if (_disposed || target == null || target.Animal == null)
            {
                return;
            }
            SaurusAnimalController controller = GetOrCreate(
                target.Animal,
                now);
            if (controller != null)
            {
                controller.EngagePlayer(
                    now,
                    actionInstanceId,
                    actionKey);
            }
        }

        internal void NotifyPlayerHit(
            AnimalCombatTarget target,
            ResolvedPlayerHit hit,
            float remainingLife,
            long actionInstanceId,
            string actionKey,
            double now)
        {
            if (_disposed || target == null || target.Animal == null ||
                hit == null)
            {
                return;
            }
            SaurusAnimalController controller = GetOrCreate(
                target.Animal,
                now);
            if (controller != null)
            {
                controller.ApplyPlayerHit(
                    hit,
                    remainingLife,
                    target.MaximumLife,
                    now,
                    actionInstanceId,
                    actionKey);
            }
        }

        internal bool TryGetContextReport(
            string selector,
            out string[] lines)
        {
            lines = null;
            if (_disposed)
            {
                lines = new string[] { "Saurus AI session is disposed." };
                return false;
            }
            if (_controllers.Count == 0)
            {
                lines = new string[] { "No Saurus controllers are active." };
                return false;
            }

            string requested = string.IsNullOrEmpty(selector)
                ? "nearest"
                : selector.Trim();
            if (string.Equals(
                requested,
                "all",
                StringComparison.OrdinalIgnoreCase))
            {
                List<string> summaries = new List<string>();
                summaries.Add(
                    "Saurus contexts generation=" + _generation +
                    " count=" + _controllers.Count + ".");
                foreach (KeyValuePair<string, SaurusAnimalController> pair in
                    _controllers)
                {
                    SaurusCombatContext context = pair.Value.LatestContext;
                    summaries.Add(context == null
                        ? "entity=" + pair.Key + " context=pending."
                        : context.ToSummaryLine());
                }
                lines = summaries.ToArray();
                return true;
            }

            SaurusAnimalController selected = null;
            if (!string.Equals(
                requested,
                "nearest",
                StringComparison.OrdinalIgnoreCase))
            {
                _controllers.TryGetValue(requested, out selected);
            }
            else
            {
                PlayerBehavior player = PlayerBehavior.LocalPlayer;
                float nearestDistance = float.MaxValue;
                foreach (KeyValuePair<string, SaurusAnimalController> pair in
                    _controllers)
                {
                    SaurusAnimalController candidate = pair.Value;
                    if (candidate == null || candidate.LatestContext == null)
                    {
                        continue;
                    }
                    float distance = player == null
                        ? candidate.LatestContext.CenterDistance
                        : Vector3.Distance(
                            player.CurrentPosition,
                            candidate.LatestContext.ActorPosition);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        selected = candidate;
                    }
                }
            }

            if (selected == null)
            {
                lines = new string[]
                {
                    "Saurus context not found for selector: " + requested + "."
                };
                return false;
            }
            if (selected.LatestContext == null)
            {
                lines = new string[]
                {
                    "Saurus context is waiting for its first process tick: " +
                        selected.EntityId + "."
                };
                return false;
            }

            string[] contextLines =
                selected.LatestContext.ToDiagnosticLines();
            string[] eventLines = selected.GetMemoryDiagnosticLines();
            List<string> report = new List<string>(
                contextLines.Length + eventLines.Length);
            report.AddRange(contextLines);
            report.AddRange(eventLines);
            lines = report.ToArray();
            return true;
        }

        internal bool TryGetIntentReport(
            string selector,
            out string[] lines)
        {
            lines = null;
            if (_disposed)
            {
                lines = new string[] { "Saurus AI session is disposed." };
                return false;
            }
            if (_controllers.Count == 0)
            {
                lines = new string[] { "No Saurus controllers are active." };
                return false;
            }

            string requested = string.IsNullOrEmpty(selector)
                ? "nearest"
                : selector.Trim();
            if (string.Equals(
                requested,
                "all",
                StringComparison.OrdinalIgnoreCase))
            {
                List<string> summaries = new List<string>();
                summaries.Add(
                    "Saurus shadow intents generation=" + _generation +
                    " count=" + _controllers.Count + ".");
                foreach (KeyValuePair<string, SaurusAnimalController> pair in
                    _controllers)
                {
                    SaurusShadowIntentDecision decision =
                        pair.Value.GetDiagnosticShadowDecision();
                    summaries.Add(decision == null
                        ? "entity=" + pair.Key + " shadow=pending."
                        : decision.ToSummaryLine(
                            pair.Key,
                            pair.Value.Profile.EntityTypeId));
                }
                lines = summaries.ToArray();
                return true;
            }

            SaurusAnimalController selected = null;
            if (!string.Equals(
                requested,
                "nearest",
                StringComparison.OrdinalIgnoreCase))
            {
                _controllers.TryGetValue(requested, out selected);
            }
            else
            {
                PlayerBehavior player = PlayerBehavior.LocalPlayer;
                float nearestDistance = float.MaxValue;
                foreach (KeyValuePair<string, SaurusAnimalController> pair in
                    _controllers)
                {
                    SaurusAnimalController candidate = pair.Value;
                    if (candidate == null || candidate.LatestContext == null)
                    {
                        continue;
                    }
                    float distance = player == null
                        ? candidate.LatestContext.CenterDistance
                        : Vector3.Distance(
                            player.CurrentPosition,
                            candidate.LatestContext.ActorPosition);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        selected = candidate;
                    }
                }
            }

            if (selected == null)
            {
                lines = new string[]
                {
                    "Saurus intent not found for selector: " + requested + "."
                };
                return false;
            }
            SaurusShadowIntentDecision selectedDecision =
                selected.GetDiagnosticShadowDecision();
            if (selectedDecision == null)
            {
                lines = new string[]
                {
                    "Saurus shadow intent is waiting for its first process " +
                        "tick: " + selected.EntityId + "."
                };
                return false;
            }

            lines = selected.GetIntentDiagnosticLines();
            return true;
        }

        internal void NotifyAnimalDied(
            string entityId,
            double now)
        {
            SaurusAnimalController controller;
            if (!_disposed && !string.IsNullOrEmpty(entityId) &&
                _controllers.TryGetValue(entityId, out controller))
            {
                controller.MarkDead(now);
            }
        }

        private void TryBindManager()
        {
            if (_manager != null ||
                !Singleton<AnimalManager>.HasInstance())
            {
                return;
            }
            _manager = Singleton<AnimalManager>.Instance();
            if (_manager == null)
            {
                return;
            }
            _manager.AnimalAppeared += OnAnimalAppeared;
            _manager.AnimalDisappeared += OnAnimalDisappeared;
            _nextReconcileAt = 0.0;
        }

        private void Reconcile(double now)
        {
            if (_manager == null)
            {
                return;
            }
            AnimalBehavior[] animals =
                UnityEngine.Object.FindObjectsOfType<AnimalBehavior>();
            int i;
            for (i = 0; i < animals.Length; i++)
            {
                AnimalBehavior animal = animals[i];
                if (IsEligible(animal))
                {
                    GetOrCreate(animal, now);
                }
            }
        }

        private void OnAnimalAppeared(AnimalBehavior animal)
        {
            if (!_disposed && IsEligible(animal))
            {
                GetOrCreate(animal, Durango.Utils.Times.UnixTimeNow());
            }
        }

        private void OnAnimalDisappeared(AnimalBehavior animal)
        {
            if (animal != null)
            {
                RemoveController(animal.EntityId);
            }
        }

        private SaurusAnimalController GetOrCreate(
            AnimalBehavior animal,
            double now)
        {
            if (!IsEligible(animal))
            {
                return null;
            }

            SaurusAnimalController existing;
            if (_controllers.TryGetValue(animal.EntityId, out existing))
            {
                if (existing.ObjectInstanceId ==
                    animal.gameObject.GetInstanceID())
                {
                    return existing;
                }
                RemoveController(animal.EntityId);
            }

            AnimalCombatProfile profile;
            if (!CombatDataRegistry.TryGetProfile(
                animal.EntityTypeId,
                out profile))
            {
                return null;
            }
            SaurusAnimalController created =
                new SaurusAnimalController(
                    animal,
                    profile,
                    _tuning,
                    _generation,
                    now);
            created.AttackCommitted += OnAttackCommitted;
            created.GroggyGaugeChanged += OnGroggyGaugeChanged;
            created.StatusChanged += OnStatusChanged;
            _controllers[animal.EntityId] = created;
            return created;
        }

        private bool IsEligible(AnimalBehavior animal)
        {
            if (_disposed || _manager == null || animal == null ||
                string.IsNullOrEmpty(animal.EntityId) ||
                !animal.IsAlive ||
                !ObjectIdentifier.IsTargetableEnemy(
                    animal.gameObject,
                    false) ||
                ObjectIdentifier.IsAlly(animal.gameObject))
            {
                return false;
            }

            AnimalCombatProfile ignored;
            if (!CombatDataRegistry.TryGetProfile(
                animal.EntityTypeId,
                out ignored))
            {
                return false;
            }

            SaurusSpeciesProfile species;
            if (!SaurusSpeciesProfiles.TryGet(
                animal.EntityTypeId,
                out species))
            {
                return false;
            }

            AnimalBehavior indexed = _manager.GetAnimal(animal.EntityId);
            return object.ReferenceEquals(indexed, animal);
        }

        private bool IsControllerCurrent(
            SaurusAnimalController controller)
        {
            return controller != null && !controller.IsDisposed &&
                IsEligible(controller.Animal) &&
                controller.ObjectInstanceId ==
                    controller.Animal.gameObject.GetInstanceID();
        }

        private void OnAttackCommitted(
            SaurusAnimalController controller,
            SaurusActionPlan plan)
        {
            AnimalAttackDefinition attack = plan == null
                ? null
                : plan.Attack;
            if (controller == null || attack == null)
            {
                return;
            }
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Saurus attack intent generation=" + _generation +
                " entity=" + controller.EntityId +
                " action=" + attack.Key +
                " hits=" + attack.Hits.Length +
                " plan=" + plan.ActionInstanceId +
                " alignment=" + plan.AlignmentPolicy + ".");

            SaurusAttackCommittedHandler committed = AttackCommitted;
            if (committed != null)
            {
                committed(controller, plan);
            }
        }

        private void OnGroggyGaugeChanged(
            SaurusAnimalController controller,
            Gauge gauge,
            string section)
        {
            SaurusGroggyGaugeChangedHandler changed = GroggyGaugeChanged;
            if (changed != null)
            {
                changed(controller, gauge, section);
            }
        }

        private void OnStatusChanged(
            SaurusAnimalController controller,
            AnimalStatus status)
        {
            SaurusAnimalStatusChangedHandler changed = StatusChanged;
            if (changed != null)
            {
                changed(controller, status);
            }
        }

        private void RemoveController(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                return;
            }
            SaurusAnimalController controller;
            if (!_controllers.TryGetValue(entityId, out controller))
            {
                return;
            }
            _controllers.Remove(entityId);
            controller.AttackCommitted -= OnAttackCommitted;
            controller.GroggyGaugeChanged -= OnGroggyGaugeChanged;
            controller.StatusChanged -= OnStatusChanged;
            controller.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_manager != null)
            {
                _manager.AnimalAppeared -= OnAnimalAppeared;
                _manager.AnimalDisappeared -= OnAnimalDisappeared;
                _manager = null;
            }
            List<SaurusAnimalController> controllers =
                new List<SaurusAnimalController>(_controllers.Values);
            _controllers.Clear();
            int i;
            for (i = 0; i < controllers.Count; i++)
            {
                controllers[i].AttackCommitted -= OnAttackCommitted;
                controllers[i].GroggyGaugeChanged -= OnGroggyGaugeChanged;
                controllers[i].StatusChanged -= OnStatusChanged;
                controllers[i].Dispose();
            }
            AttackCommitted = null;
            GroggyGaugeChanged = null;
            StatusChanged = null;
        }
    }
}
