using System;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Baominix.DurangoOriginal.CombatSystem.Damage;
using Baominix.DurangoOriginal.CombatSystem.Presentation;
using Shared.Animal;
using Shared.Battle;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal delegate void SaurusAttackCommittedHandler(
        SaurusAnimalController controller,
        SaurusActionPlan plan);

    internal delegate void SaurusGroggyGaugeChangedHandler(
        SaurusAnimalController controller,
        Gauge gauge,
        string section);

    internal delegate void SaurusAnimalStatusChangedHandler(
        SaurusAnimalController controller,
        AnimalStatus status);

    internal sealed class SaurusAnimalController : IDisposable
    {
        private enum ReactionKind
        {
            None = 0,
            Damage = 1,
            KnockBack = 2,
            Blow = 3,
            Groggy = 4,
            KnockDown = 5
        }

        private enum KnockDownPhase
        {
            None = 0,
            Begin = 1,
            During = 2,
            End = 3
        }

        private const double ContextCaptureIntervalSeconds = 0.10;
        private const int ZebraceratopsEntityTypeId = 2027;
        private const int ElephantulusEntityTypeId = 2037;
        private const int DeinonychusEntityTypeId = 2039;
        private const int RaptorEntityTypeId = 2001;

        private readonly AnimalBehavior _animal;
        private readonly AnimalCombatProfile _profile;
        private readonly SaurusSpeciesProfile _species;
        private readonly SaurusCoreTuning _tuning;
        private readonly SaurusMotionAdapter _motion;
        private readonly SaurusRandom _random;
        private readonly SaurusCombatMemory _memory;
        private readonly Vector3 _homePosition;
        private readonly float _attackEnterDistance;
        private readonly int _objectInstanceId;
        private readonly int _generation;

        private SaurusAiState _state;
        private double _stateEnteredAt;
        private double _stateUntil;
        private double _nextAttackAt;
        private double _lastProcessedAt;
        private double _blockedSince;
        private bool _engaged;
        private bool _disposed;
        private Vector3 _roamDestination;
        private AnimalAttackDefinition _activeAttack;
        private SaurusActionPlan _activeActionPlan;
        private AnimalAttackDefinition _preparedAttack;
        private SaurusAttackProfile _preparedAttackProfile;
        private SaurusAlignmentPolicy _preparedAlignmentPolicy;
        private double _preparationDeadline;
        private DamageDirection _reactionDirection;
        private SaurusEvadeRoute _evadeRoute;
        private bool _reactionIsBlow;
        private ReactionKind _reactionKind;
        private ReactionKind _pendingReactionKind;
        private KnockDownPhase _knockDownPhase;
        private double _knockDownLoopUntil;
        private float _groggyMaximum;
        private float _groggyCurrent;
        private float _groggyThreshold;
        private float _groggyWeakThreshold;
        private AnimalStatus _publishedStatus = AnimalStatus.Invalid;
        private bool _retreatPending;
        private bool _retreatAttempted;
        private bool _escapeStrikePlayed;
        private bool _repositionUntilFront;
        private bool _roamTurnPending;
        private long _nextControllerActionInstanceId;
        private long _activeActionInstanceId;
        private long _nextEngagementId;
        private long _engagementId;
        private long _nextContextSequence;
        private long _nextShadowDecisionSequence;
        private double _nextContextCaptureAt;
        private double _previousContextAt;
        private Vector3 _previousContextActorPosition;
        private Vector3 _previousContextTargetPosition;
        private bool _hasPreviousContextSample;
        private bool _hasPreviousTargetSample;
        private SaurusTargetSector _lastTargetSector;
        private string _lastStateReason = "registered";

        internal event SaurusAttackCommittedHandler AttackCommitted;
        internal event SaurusGroggyGaugeChangedHandler GroggyGaugeChanged;
        internal event SaurusAnimalStatusChangedHandler StatusChanged;

        internal SaurusAnimalController(
            AnimalBehavior animal,
            AnimalCombatProfile profile,
            SaurusCoreTuning tuning,
            int generation,
            double now)
        {
            _animal = animal;
            _profile = profile;
            if (!SaurusSpeciesProfiles.TryGet(
                profile.EntityTypeId,
                out _species))
            {
                throw new InvalidOperationException(
                    "Missing Saurus species profile for entity type " +
                    profile.EntityTypeId + ".");
            }
            _tuning = tuning;
            _generation = generation;
            _homePosition = animal.CurrentPosition;
            _objectInstanceId = animal.gameObject.GetInstanceID();
            _random = new SaurusRandom(animal.EntityId);
            _memory = new SaurusCombatMemory(generation, animal.EntityId);
            _motion = new SaurusMotionAdapter(animal);
            InitializeGroggyGauge();
            float frameworkReach =
                SaurusAttackSelector.GetMaximumReach(profile, _species);
            _attackEnterDistance = Mathf.Max(
                80f,
                frameworkReach + GetAnimalRadius());
            EnterState(SaurusAiState.Idle, now, "registered");
        }

        internal string EntityId
        {
            get { return _animal == null ? null : _animal.EntityId; }
        }

        internal int ObjectInstanceId
        {
            get { return _objectInstanceId; }
        }

        internal AnimalBehavior Animal
        {
            get { return _animal; }
        }

        internal AnimalCombatProfile Profile
        {
            get { return _profile; }
        }

        internal SaurusSpeciesProfile SpeciesProfile
        {
            get { return _species; }
        }

        internal SaurusAiState State
        {
            get { return _state; }
        }

        internal bool IsDisposed
        {
            get { return _disposed; }
        }

        internal SaurusCombatContext LatestContext { get; private set; }
        internal SaurusShadowIntentDecision LatestShadowDecision
        {
            get;
            private set;
        }
        internal SaurusShadowIntentDecision LastSelectionShadowDecision
        {
            get;
            private set;
        }

        internal string[] GetMemoryDiagnosticLines()
        {
            return _memory.ToDiagnosticLines(
                LatestContext == null
                    ? Durango.Utils.Times.UnixTimeNow()
                    : LatestContext.CapturedAt,
                8);
        }

        internal SaurusShadowIntentDecision GetDiagnosticShadowDecision()
        {
            if (LatestContext != null)
            {
                LatestShadowDecision = SaurusShadowIntentResolver.Resolve(
                    ++_nextShadowDecisionSequence,
                    LatestContext,
                    _memory,
                    _profile);
            }
            return LatestShadowDecision;
        }

        internal string[] GetIntentDiagnosticLines()
        {
            SaurusShadowIntentDecision current =
                GetDiagnosticShadowDecision();
            if (current == null)
            {
                return new string[0];
            }
            string[] currentLines = current.ToDiagnosticLines(
                EntityId,
                _profile.EntityTypeId);
            if (LastSelectionShadowDecision == null ||
                LastSelectionShadowDecision.EngagementId != _engagementId)
            {
                return currentLines;
            }
            string[] result = new string[currentLines.Length + 1];
            Array.Copy(currentLines, result, currentLines.Length);
            result[result.Length - 1] =
                "LastSelection " +
                LastSelectionShadowDecision.ToSummaryLine(
                    EntityId,
                    _profile.EntityTypeId);
            return result;
        }

        internal void EngagePlayer(
            double now,
            long sourceActionInstanceId,
            string sourceActionKey)
        {
            if (_disposed || _animal == null || !_animal.IsAlive)
            {
                return;
            }
            if (!_engaged)
            {
                _engagementId = ++_nextEngagementId;
                _memory.BeginEngagement(_engagementId);
                LatestShadowDecision = null;
                LastSelectionShadowDecision = null;
                _retreatAttempted = false;
                _retreatPending = false;
                _escapeStrikePlayed = false;
                _lastTargetSector = SaurusTargetSector.None;
                _memory.Record(
                    SaurusCombatEventType.Engaged,
                    now,
                    GetPlayerEntityId(),
                    sourceActionInstanceId,
                    sourceActionKey);
            }
            _engaged = true;
            if (_state == SaurusAiState.Idle ||
                _state == SaurusAiState.Roam ||
                _state == SaurusAiState.ReturnHome)
            {
                EnterState(SaurusAiState.Alert, now, "player-threat");
            }
        }

        internal void ApplyPlayerHit(
            ResolvedPlayerHit hit,
            float remainingLife,
            float maximumLife,
            double now,
            long sourceActionInstanceId,
            string sourceActionKey)
        {
            if (_disposed || hit == null || _animal == null ||
                !_animal.IsAlive || _state == SaurusAiState.Dead)
            {
                return;
            }

            EngagePlayer(now, sourceActionInstanceId, sourceActionKey);
            if (hit.IsBowOrCrossbow)
            {
                _memory.Record(
                    SaurusCombatEventType.PlayerBowOrCrossbowAttack,
                    now,
                    GetPlayerEntityId(),
                    sourceActionInstanceId,
                    sourceActionKey);
            }
            if (hit.Result == DamageResult.Dodged)
            {
                _evadeRoute = SelectEvadeRoute(hit.Direction);
                _memory.Record(
                    SaurusCombatEventType.AnimalDodgedPlayerAttack,
                    now,
                    GetPlayerEntityId(),
                    sourceActionInstanceId,
                    sourceActionKey);
                // Dodge remains effective during an attack, but Evade must
                // never interrupt an attack animation already in progress.
                if (_state != SaurusAiState.Attack &&
                    _state != SaurusAiState.Groggy &&
                    _state != SaurusAiState.KnockDown)
                {
                    PlayerBehavior player = PlayerBehavior.LocalPlayer;
                    if (HasValidPlayer(player))
                    {
                        // The incoming side and evade route were captured
                        // before this snap. Face the attacker immediately,
                        // then evade relative to that combat-facing basis.
                        _motion.FacePosition(player.CurrentPosition, true);
                    }
                    EnterState(SaurusAiState.Evade, now, "player-hit-dodged");
                }
                return;
            }
            if (hit.Result == DamageResult.Missed)
            {
                _memory.Record(
                    SaurusCombatEventType.PlayerAttackMissed,
                    now,
                    GetPlayerEntityId(),
                    sourceActionInstanceId,
                    sourceActionKey);
                return;
            }
            if (hit.Result != DamageResult.Hit || hit.Value <= 0)
            {
                return;
            }

            _memory.Record(
                SaurusCombatEventType.DamagedByPlayer,
                now,
                GetPlayerEntityId(),
                sourceActionInstanceId,
                sourceActionKey);
            if ((hit.Effects &
                    (DamageEffects.Blow | DamageEffects.KnockBack)) != 0)
            {
                _memory.Record(
                    SaurusCombatEventType.BlownOrKnockedBack,
                    now,
                    GetPlayerEntityId(),
                    sourceActionInstanceId,
                    sourceActionKey);
            }

            if (!_retreatAttempted && maximumLife > 0f &&
                remainingLife / maximumLife <= _species.RetreatLifeRatio)
            {
                _retreatAttempted = true;
                _retreatPending = _random.Range(0f, 1f) <
                    _species.RetreatChance;
                _memory.Record(
                    SaurusCombatEventType.LowHealthThresholdCrossed,
                    now,
                    GetPlayerEntityId(),
                    sourceActionInstanceId,
                    sourceActionKey);
            }

            _reactionDirection = hit.Direction;
            ReactionKind requested = ResolveReaction(hit, now);
            _reactionIsBlow = requested == ReactionKind.Blow;
            if (_state == SaurusAiState.Attack)
            {
                QueueReaction(requested);
                return;
            }
            if (_state == SaurusAiState.KnockDown ||
                _state == SaurusAiState.Groggy)
            {
                if (requested == ReactionKind.KnockDown &&
                    _state != SaurusAiState.KnockDown)
                {
                    BeginReaction(requested, now, "groggy-depleted");
                }
                return;
            }
            BeginReaction(requested, now, "player-hit-landed");
        }

        private ReactionKind ResolveReaction(
            ResolvedPlayerHit hit,
            double now)
        {
            ReactionKind result = ReactionKind.Damage;
            if ((hit.Effects & DamageEffects.KnockBack) != 0)
            {
                result = ReactionKind.KnockBack;
            }
            else if ((hit.Effects & DamageEffects.Blow) != 0)
            {
                result = ReactionKind.Blow;
            }

            if (_groggyMaximum <= 0f || hit.GroggyDamage <= 0f)
            {
                return result;
            }

            float previous = _groggyCurrent;
            _groggyCurrent = Mathf.Clamp(
                _groggyCurrent - hit.GroggyDamage,
                0f,
                _groggyMaximum);
            string section = GetGroggySection(_groggyCurrent);
            PublishGroggyGauge(now, section);
            DurangoCombatSystemPlugin.Log.LogInfo(
                "Saurus groggy impact entity=" + EntityId +
                " amount=" + hit.GroggyDamage +
                " gauge=" + _groggyCurrent + "/" + _groggyMaximum +
                " section=" + section + ".");

            if (_groggyCurrent <= 0f)
            {
                return ReactionKind.KnockDown;
            }
            if (previous > _groggyThreshold &&
                _groggyCurrent <= _groggyThreshold)
            {
                return ReactionKind.Groggy;
            }
            return result;
        }

        private void QueueReaction(ReactionKind requested)
        {
            // Framework attack metadata has no interruptible flag. Treat the
            // authored attack clip as atomic and retain only the strongest
            // pending reaction for the first safe boundary after it.
            if (GetReactionPriority(requested) >
                GetReactionPriority(_pendingReactionKind))
            {
                _pendingReactionKind = requested;
            }
        }

        private void BeginReaction(
            ReactionKind requested,
            double now,
            string reason)
        {
            _reactionKind = requested;
            _pendingReactionKind = ReactionKind.None;
            if (requested == ReactionKind.KnockDown)
            {
                PublishStatus(AnimalStatus.KnockDown);
                EnterState(SaurusAiState.KnockDown, now, reason);
            }
            else if (requested == ReactionKind.Groggy)
            {
                PublishStatus(AnimalStatus.Groggy);
                EnterState(SaurusAiState.Groggy, now, reason);
            }
            else
            {
                _reactionIsBlow = requested == ReactionKind.Blow;
                if (requested == ReactionKind.Blow ||
                    requested == ReactionKind.KnockBack)
                {
                    PublishStatus(AnimalStatus.Blow);
                }
                EnterState(SaurusAiState.Reaction, now, reason);
            }
        }

        private static int GetReactionPriority(ReactionKind reaction)
        {
            return (int)reaction;
        }

        internal void MarkDead(double now)
        {
            if (!_disposed)
            {
                EnterState(SaurusAiState.Dead, now, "life-depleted");
            }
        }

        internal void Process(double now, PlayerBehavior player)
        {
            if (_disposed || _animal == null)
            {
                return;
            }
            if (!_animal.IsAlive)
            {
                if (_state != SaurusAiState.Dead)
                {
                    EnterState(SaurusAiState.Dead, now, "animal-dead");
                }
                return;
            }

            float deltaSeconds = _lastProcessedAt <= 0.0
                ? 0f
                : Mathf.Clamp((float)(now - _lastProcessedAt), 0f, 0.1f);
            _lastProcessedAt = now;

            if (now >= _nextContextCaptureAt)
            {
                CaptureContext(now, player);
                _nextContextCaptureAt = now +
                    ContextCaptureIntervalSeconds;
            }

            if (_engaged && !HasValidPlayer(player))
            {
                _engaged = false;
                EnterState(SaurusAiState.ReturnHome, now, "target-lost");
            }

            if (_engaged && player != null &&
                ShouldReturnHome(player.CurrentPosition))
            {
                _engaged = false;
                EnterState(SaurusAiState.ReturnHome, now, "leash");
            }

            switch (_state)
            {
                case SaurusAiState.Idle:
                    ProcessIdle(now);
                    break;
                case SaurusAiState.Roam:
                    ProcessRoam(now, deltaSeconds);
                    break;
                case SaurusAiState.Alert:
                    ProcessAlert(now, player);
                    break;
                case SaurusAiState.Approach:
                    ProcessApproach(now, deltaSeconds, player);
                    break;
                case SaurusAiState.Face:
                    ProcessFace(now, player);
                    break;
                case SaurusAiState.PrepareAttack:
                    ProcessPrepareAttack(now, deltaSeconds, player);
                    break;
                case SaurusAiState.PrepareEscape:
                    ProcessPrepareEscape(now, player);
                    break;
                case SaurusAiState.Attack:
                    ProcessAttack(now, player);
                    break;
                case SaurusAiState.Recover:
                    ProcessRecover(now, player);
                    break;
                case SaurusAiState.Evade:
                case SaurusAiState.Reaction:
                    ProcessReaction(now, player);
                    break;
                case SaurusAiState.Groggy:
                    ProcessGroggy(now, player);
                    break;
                case SaurusAiState.KnockDown:
                    ProcessKnockDown(now, player);
                    break;
                case SaurusAiState.Retreat:
                    ProcessRetreat(now, deltaSeconds, player);
                    break;
                case SaurusAiState.ReturnHome:
                    ProcessReturnHome(now, deltaSeconds);
                    break;
            }

            SaurusDebugBubble.Publish(
                _animal,
                _state + " | " + _lastStateReason + "\n" +
                    _motion.GetDebugText(),
                now);
        }

        private void ProcessIdle(double now)
        {
            if (_engaged)
            {
                EnterState(SaurusAiState.Alert, now, "engaged-from-idle");
                return;
            }
            if (now >= _stateUntil)
            {
                float angle = _random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = _random.Range(
                    _tuning.RoamRadius * 0.35f,
                    _tuning.RoamRadius);
                _roamDestination = _homePosition + new Vector3(
                    Mathf.Sin(angle) * distance,
                    0f,
                    Mathf.Cos(angle) * distance);
                _roamDestination.y = _homePosition.y;
                EnterState(SaurusAiState.Roam, now, "idle-complete");
            }
        }

        private void ProcessRoam(double now, float deltaSeconds)
        {
            if (_engaged)
            {
                EnterState(SaurusAiState.Alert, now, "engaged-from-roam");
                return;
            }

            if (_roamTurnPending)
            {
                float yawError =
                    _motion.FacePositionWithTurnAnimation(
                        _roamDestination,
                        _profile,
                        _tuning.FaceToleranceDegrees);
                if (yawError > _tuning.FaceToleranceDegrees)
                {
                    return;
                }

                _roamTurnPending = false;
                _motion.PlayMove();
                // Roam duration measures actual walking time; the authored
                // CW/CCW preparation is not deducted from it.
                _stateUntil = now + _random.Range(
                    _tuning.RoamSecondsMin,
                    _tuning.RoamSecondsMax);
            }

            float movedRatio;
            bool arrived = _motion.MoveToward(
                _roamDestination,
                _tuning.RoamStopDistance,
                deltaSeconds,
                out movedRatio);
            if (arrived || now >= _stateUntil ||
                movedRatio < 0.15f && deltaSeconds > 0f)
            {
                EnterState(SaurusAiState.Idle, now, "roam-complete");
            }
        }

        private void ProcessAlert(double now, PlayerBehavior player)
        {
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(SaurusAiState.ReturnHome, now, "alert-target-lost");
                return;
            }
            FaceCombatTarget(player);
            if (now >= _stateUntil)
            {
                EnterRangeState(now, player, "alert-complete");
            }
        }

        private void ProcessApproach(
            double now,
            float deltaSeconds,
            PlayerBehavior player)
        {
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(SaurusAiState.ReturnHome, now, "approach-target-lost");
                return;
            }

            float movedRatio;
            bool arrived = _motion.MoveToward(
                player.CurrentPosition,
                _attackEnterDistance,
                deltaSeconds,
                out movedRatio);
            if (arrived || DistanceTo(player.CurrentPosition) <=
                _attackEnterDistance)
            {
                _blockedSince = 0.0;
                EnterState(SaurusAiState.Face, now, "attack-range-entered");
                return;
            }

            if (movedRatio >= 0.15f || deltaSeconds <= 0f)
            {
                _blockedSince = 0.0;
                return;
            }
            if (_blockedSince <= 0.0)
            {
                _blockedSince = now;
                _memory.Record(
                    SaurusCombatEventType.PathBlocked,
                    now,
                    GetPlayerEntityId(),
                    _activeActionInstanceId,
                    _activeAttack == null ? null : _activeAttack.Key);
            }
            else if (now >= _blockedSince +
                _tuning.BlockedSecondsBeforeReturn)
            {
                _engaged = false;
                EnterState(SaurusAiState.ReturnHome, now, "approach-blocked");
            }
        }

        private void ProcessFace(double now, PlayerBehavior player)
        {
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(SaurusAiState.ReturnHome, now, "face-target-lost");
                return;
            }
            float distance = DistanceTo(player.CurrentPosition);
            if (distance > _attackEnterDistance +
                _tuning.ApproachHysteresis)
            {
                EnterState(SaurusAiState.Approach, now, "outside-hysteresis");
                return;
            }

            bool intentExecution = UsesIntentExecution();
            if (intentExecution && _repositionUntilFront)
            {
                CaptureContext(now, player);
                if (LatestContext != null &&
                    LatestContext.TargetSector ==
                        SaurusTargetSector.Front)
                {
                    _repositionUntilFront = false;
                    _motion.Stop(_profile, true);
                    _stateUntil = now + _tuning.FaceSettleSeconds;
                    return;
                }
                FaceCombatTarget(player);
                return;
            }
            if (intentExecution && _preparedAttack == null &&
                now >= _stateUntil)
            {
                CaptureContext(now, player);
                SaurusShadowIntentDecision decision =
                    SaurusShadowIntentResolver.Resolve(
                        ++_nextShadowDecisionSequence,
                        LatestContext,
                        _memory,
                        _profile);
                AnimalAttackDefinition intentAttack =
                    SaurusAttackSelector.FindDefinition(
                        _profile,
                        decision.ActionKey);
                LastSelectionShadowDecision =
                    decision.WithLegacySelection(
                        intentAttack == null ? null : intentAttack.Key);
                LatestShadowDecision = LastSelectionShadowDecision;
                if (intentAttack == null)
                {
                    if (decision.Intent ==
                        SaurusCombatIntent.Reposition)
                    {
                        // Reposition is an alignment step here, not an
                        // approach step. MoveToward intentionally does not
                        // rotate when already inside its stop distance, which
                        // would otherwise loop forever for a flank target
                        // just outside tricera_turn reach.
                        _repositionUntilFront = true;
                        FaceCombatTarget(player);
                        return;
                    }
                    if (decision.Intent == SaurusCombatIntent.Stand &&
                        now < _nextAttackAt)
                    {
                        // Recover is a fixed one-second pose. Any remaining
                        // species cooldown is an attack lock, not another
                        // Recover state: keep facing while normal attacks wait.
                        FaceCombatTarget(player);
                        return;
                    }
                    EnterState(
                        decision.Intent == SaurusCombatIntent.Approach
                            ? SaurusAiState.Approach
                            : SaurusAiState.Recover,
                        now,
                        "intent-" + decision.Intent);
                    return;
                }
                _preparedAttack = intentAttack;
                _species.TryGetAttack(
                    intentAttack.Key,
                    out _preparedAttackProfile);
                _preparedAlignmentPolicy = decision.Alignment;
            }

            // Preserve the pre-alignment sector until the intent boundary.
            // During Face settle a legacy FacePosition call would rotate the
            // Zebra early and collapse Flank/Rear into Front before the
            // resolver could select tricera_turn.
            if (intentExecution && _preparedAttack == null)
            {
                return;
            }

            float yawError = intentExecution &&
                _preparedAttack != null &&
                _preparedAlignmentPolicy ==
                    SaurusAlignmentPolicy.KeepCurrentFacing
                    ? 0f
                    : FaceCombatTarget(player);
            if (yawError <= _tuning.FaceToleranceDegrees &&
                now >= _stateUntil &&
                (intentExecution && _preparedAttack != null ||
                    now >= _nextAttackAt))
            {
                float combinedBounds =
                    GetAnimalRadius() + GetPlayerRadius(player);
                AnimalAttackDefinition selected = _preparedAttack;
                SaurusAttackProfile selectedProfile =
                    _preparedAttackProfile;
                if (selected == null)
                {
                    SaurusShadowIntentDecision shadowAtBoundary =
                        SaurusShadowIntentResolver.Resolve(
                            ++_nextShadowDecisionSequence,
                            LatestContext,
                            _memory,
                            _profile);
                    selected = SaurusAttackSelector.Select(
                        _profile,
                        _species,
                        distance,
                        combinedBounds,
                        _random.Range(0f, 1f));
                    LastSelectionShadowDecision =
                        shadowAtBoundary.WithLegacySelection(
                            selected == null ? null : selected.Key);
                    LatestShadowDecision = LastSelectionShadowDecision;
                    if (selected == null)
                    {
                        EnterState(
                            SaurusAiState.Approach,
                            now,
                            "no-attack-in-range");
                        return;
                    }
                    _species.TryGetAttack(
                        selected.Key,
                        out selectedProfile);
                    if (selectedProfile != null &&
                        selectedProfile.NeedsReposition(
                            distance,
                            combinedBounds))
                    {
                        _preparedAttack = selected;
                        _preparedAttackProfile = selectedProfile;
                        _preparationDeadline = now +
                            selectedProfile.RepositionSeconds;
                        EnterState(
                            SaurusAiState.PrepareAttack,
                            now,
                            "attack-spacing-required");
                        return;
                    }
                }
                else if (selectedProfile != null &&
                    _preparationDeadline <= 0.0 &&
                    selectedProfile.NeedsReposition(
                        distance,
                        combinedBounds))
                {
                    _preparationDeadline = now +
                        selectedProfile.RepositionSeconds;
                    EnterState(
                        SaurusAiState.PrepareAttack,
                        now,
                        "intent-attack-spacing-required");
                    return;
                }
                else if (selectedProfile == null ||
                    !selectedProfile.IsInRange(distance, combinedBounds))
                {
                    ClearPreparedAttack();
                    EnterState(
                        SaurusAiState.Approach,
                        now,
                        "prepared-attack-out-of-range");
                    return;
                }
                CommitAttack(selected, now, player);
            }
            else if (!intentExecution && now < _nextAttackAt)
            {
                // Stay in Face after the fixed Recover pose. Cooldown blocks
                // attack commit but does not force another standing wait.
                return;
            }
        }

        private void ProcessPrepareAttack(
            double now,
            float deltaSeconds,
            PlayerBehavior player)
        {
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(
                    SaurusAiState.ReturnHome,
                    now,
                    "prepare-target-lost");
                return;
            }
            if (_preparedAttack == null ||
                _preparedAttackProfile == null)
            {
                EnterState(
                    SaurusAiState.Face,
                    now,
                    "prepare-action-missing");
                return;
            }

            float combinedBounds =
                GetAnimalRadius() + GetPlayerRadius(player);
            float stopDistance = combinedBounds +
                _preparedAttackProfile.PreferredCommitDistance;
            float movedRatio;
            bool reached = _motion.MoveBackwardFromUntil(
                player.CurrentPosition,
                stopDistance,
                _preparedAttackProfile.RepositionSpeedMultiplier,
                deltaSeconds,
                out movedRatio);
            bool blocked = deltaSeconds > 0f && movedRatio < 0.15f;
            if (reached || blocked || now >= _preparationDeadline)
            {
                EnterState(
                    SaurusAiState.Face,
                    now,
                    reached
                        ? "attack-spacing-reached"
                        : blocked
                            ? "attack-spacing-blocked"
                            : "attack-spacing-timeout");
            }
        }

        private void CommitAttack(
            AnimalAttackDefinition selected,
            double now,
            PlayerBehavior player)
        {
            _repositionUntilFront = false;
            ClearPreparedAttack();
            _activeAttack = selected;
            _activeActionInstanceId =
                ++_nextControllerActionInstanceId;
            _activeActionPlan = SaurusActionPlan.Create(
                _generation,
                _engagementId,
                _activeActionInstanceId,
                selected,
                now,
                _animal.CurrentPosition,
                _animal.CurrentYaw,
                player.CurrentPosition,
                GetSpatialScale());
            EnterState(SaurusAiState.Attack, now, "attack-committed");
        }

        private void ProcessPrepareEscape(
            double now,
            PlayerBehavior player)
        {
            if (!_retreatPending || !_engaged || !HasValidPlayer(player))
            {
                EnterState(
                    SaurusAiState.ReturnHome,
                    now,
                    "escape-target-lost");
                return;
            }
            if (_preparedAttack == null)
            {
                EnterState(
                    SaurusAiState.Retreat,
                    now,
                    "escape-action-missing");
                return;
            }

            float yawError = _motion.FacePosition(
                player.CurrentPosition,
                false);
            if (yawError <= _tuning.FaceToleranceDegrees &&
                now >= _stateUntil)
            {
                AnimalAttackDefinition escape = _preparedAttack;
                _escapeStrikePlayed = true;
                CommitAttack(escape, now, player);
            }
        }

        private void BeginRetreatSequence(
            double now,
            PlayerBehavior player,
            string reason)
        {
            if (!_retreatPending)
            {
                EnterState(SaurusAiState.Recover, now, reason);
                return;
            }
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(
                    SaurusAiState.ReturnHome,
                    now,
                    "retreat-target-lost");
                return;
            }
            if (!_escapeStrikePlayed &&
                !string.IsNullOrEmpty(_species.EscapeAttackKey))
            {
                AnimalAttackDefinition escape =
                    SaurusAttackSelector.FindDefinition(
                        _profile,
                        _species.EscapeAttackKey);
                if (escape != null && escape.Hits.Length > 0 &&
                    !string.IsNullOrEmpty(escape.Motion))
                {
                    _preparedAttack = escape;
                    _preparedAttackProfile = null;
                    EnterState(
                        SaurusAiState.PrepareEscape,
                        now,
                        "escape-strike-before-retreat");
                    return;
                }
            }
            EnterState(SaurusAiState.Retreat, now, reason);
        }

        private void ClearPreparedAttack()
        {
            _preparedAttack = null;
            _preparedAttackProfile = null;
            _preparedAlignmentPolicy =
                SaurusAlignmentPolicy.FaceTargetBeforeCommit;
            _preparationDeadline = 0.0;
        }

        private void ProcessAttack(double now, PlayerBehavior player)
        {
            _motion.ProcessRootMotion(now);
            if (now < _stateUntil)
            {
                return;
            }
            _nextAttackAt = now + Math.Max(
                0f,
                _profile.AttackCooltime +
                _species.CooldownBonusSeconds);
            string completedAction = _activeAttack == null
                ? null
                : _activeAttack.Key;
            _memory.Record(
                SaurusCombatEventType.LastActionCompleted,
                now,
                GetPlayerEntityId(),
                _activeActionInstanceId,
                completedAction);
            _activeAttack = null;
            _activeActionPlan = null;
            _activeActionInstanceId = 0L;
            if (_pendingReactionKind != ReactionKind.None)
            {
                ReactionKind pending = _pendingReactionKind;
                _pendingReactionKind = ReactionKind.None;
                BeginReaction(pending, now, "queued-after-attack");
                return;
            }
            if (_retreatPending)
            {
                BeginRetreatSequence(
                    now,
                    player,
                    "low-life-retreat");
            }
            else
            {
                EnterState(
                    SaurusAiState.Recover,
                    now,
                    "attack-finished");
            }
        }

        private void ProcessRecover(double now, PlayerBehavior player)
        {
            if (_retreatPending)
            {
                BeginRetreatSequence(
                    now,
                    player,
                    "low-life-retreat");
                return;
            }
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(SaurusAiState.ReturnHome, now, "recover-target-lost");
                return;
            }
            if (now < _stateUntil)
            {
                return;
            }
            float distance = DistanceTo(player.CurrentPosition);
            if (distance > _attackEnterDistance +
                _tuning.ApproachHysteresis)
            {
                EnterState(SaurusAiState.Approach, now, "recover-target-moved");
                return;
            }
            EnterRangeState(now, player, "recover-complete");
        }

        private void ProcessReaction(double now, PlayerBehavior player)
        {
            _motion.ProcessRootMotion(now);
            if (now < _stateUntil)
            {
                return;
            }
            ClearReactionStatus();
            if (_retreatPending)
            {
                BeginRetreatSequence(
                    now,
                    player,
                    "low-life-retreat");
                return;
            }
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(SaurusAiState.ReturnHome, now, "reaction-target-lost");
                return;
            }
            EnterRangeState(now, player, "reaction-complete");
        }

        private void ProcessGroggy(double now, PlayerBehavior player)
        {
            if (now < _stateUntil)
            {
                return;
            }
            ResetGroggyGauge(now);
            ClearReactionStatus();
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(
                    SaurusAiState.ReturnHome,
                    now,
                    "groggy-target-lost");
                return;
            }
            EnterRangeState(now, player, "groggy-complete");
        }

        private void ProcessKnockDown(double now, PlayerBehavior player)
        {
            _motion.ProcessRootMotion(now);
            if (now < _stateUntil)
            {
                return;
            }
            if (_knockDownPhase == KnockDownPhase.Begin)
            {
                _knockDownPhase = KnockDownPhase.During;
                _motion.PlayKnockDownDuring(_profile);
                _stateUntil = Math.Max(now + 0.1, _knockDownLoopUntil);
                return;
            }
            if (_knockDownPhase == KnockDownPhase.During)
            {
                _knockDownPhase = KnockDownPhase.End;
                _stateUntil = now + Mathf.Max(
                    0.1f,
                    _motion.PlayKnockDownEnd(_profile, now));
                return;
            }

            _knockDownPhase = KnockDownPhase.None;
            ResetGroggyGauge(now);
            ClearReactionStatus();
            if (!_engaged || !HasValidPlayer(player))
            {
                EnterState(
                    SaurusAiState.ReturnHome,
                    now,
                    "knockdown-target-lost");
                return;
            }
            EnterRangeState(now, player, "knockdown-complete");
        }

        private void ProcessRetreat(
            double now,
            float deltaSeconds,
            PlayerBehavior player)
        {
            if (!_engaged || !HasValidPlayer(player))
            {
                _retreatPending = false;
                EnterState(SaurusAiState.ReturnHome, now, "retreat-target-lost");
                return;
            }
            if (now >= _stateUntil)
            {
                _retreatPending = false;
                EnterRangeState(now, player, "retreat-complete");
                return;
            }
            _motion.MoveAwayFrom(
                player.CurrentPosition,
                _species.RetreatSpeedMultiplier,
                deltaSeconds);
        }

        private void ProcessReturnHome(double now, float deltaSeconds)
        {
            float movedRatio;
            bool arrived = _motion.MoveToward(
                _homePosition,
                _tuning.ReturnStopDistance,
                deltaSeconds,
                out movedRatio);
            if (arrived)
            {
                _animal.CurrentPosition = _homePosition;
                EnterState(SaurusAiState.Idle, now, "home-reached");
            }
        }

        private void EnterRangeState(
            double now,
            PlayerBehavior player,
            string reason)
        {
            float distance = DistanceTo(player.CurrentPosition);
            EnterState(
                distance <= _attackEnterDistance
                    ? SaurusAiState.Face
                    : SaurusAiState.Approach,
                now,
                reason);
        }

        private void EnterState(
            SaurusAiState next,
            double now,
            string reason)
        {
            if (_disposed ||
                _state == next && _stateEnteredAt > 0.0 &&
                next != SaurusAiState.Reaction &&
                next != SaurusAiState.Evade)
            {
                return;
            }

            SaurusAiState previous = _state;
            if (next != SaurusAiState.PrepareAttack &&
                next != SaurusAiState.PrepareEscape &&
                next != SaurusAiState.Face &&
                next != SaurusAiState.Attack)
            {
                ClearPreparedAttack();
            }
            _state = next;
            _lastStateReason = string.IsNullOrEmpty(reason)
                ? "-"
                : reason;
            _stateEnteredAt = now;
            _stateUntil = now;
            _blockedSince = 0.0;

            switch (next)
            {
                case SaurusAiState.Idle:
                    _repositionUntilFront = false;
                    _roamTurnPending = false;
                    _motion.Stop(_profile, false);
                    _stateUntil = now + _random.Range(
                        _tuning.IdleSecondsMin,
                        _tuning.IdleSecondsMax);
                    break;
                case SaurusAiState.Roam:
                    // First face the newly selected roam destination. Every
                    // correction outside FaceTolerance uses Rotate_CW/CCW or
                    // the rear one-shot Turn. Stand begins only after facing
                    // has settled; walking starts after that boundary.
                    _roamTurnPending = true;
                    _motion.Stop(_profile, false);
                    _stateUntil = 0.0;
                    break;
                case SaurusAiState.Alert:
                    _motion.Stop(_profile, true);
                    _stateUntil = now + _tuning.AlertSeconds;
                    break;
                case SaurusAiState.Approach:
                    _motion.PlayMove();
                    break;
                case SaurusAiState.Face:
                    _motion.Stop(_profile, true);
                    _stateUntil = now + _tuning.FaceSettleSeconds;
                    break;
                case SaurusAiState.PrepareAttack:
                    break;
                case SaurusAiState.PrepareEscape:
                    _motion.Stop(_profile, true);
                    _stateUntil = now + _tuning.FaceSettleSeconds;
                    break;
                case SaurusAiState.Attack:
                    float duration = _motion.PlayAttack(
                        _activeActionPlan,
                        now);
                    _stateUntil = now + Mathf.Max(0.1f, duration);
                    SaurusAttackCommittedHandler committed = AttackCommitted;
                    if (committed != null && _activeActionPlan != null)
                    {
                        committed(this, _activeActionPlan);
                    }
                    break;
                case SaurusAiState.Recover:
                    _motion.Stop(_profile, true);
                    _stateUntil = now + _tuning.RecoverStandSeconds;
                    break;
                case SaurusAiState.Evade:
                    _stateUntil = now + Mathf.Max(
                        0.1f,
                        _motion.PlayEvade(_profile, _evadeRoute, now));
                    break;
                case SaurusAiState.Reaction:
                    _stateUntil = now + Mathf.Max(
                        0.1f,
                        _reactionIsBlow
                            ? _motion.PlayBlow(_profile, now)
                            : _motion.PlayDamage(
                                _profile,
                                _reactionDirection,
                                now));
                    break;
                case SaurusAiState.Groggy:
                    _motion.PlayGroggy(_profile);
                    _stateUntil = now + EvaluateDuration(
                        _profile.GroggyDurationFormula,
                        "groggy_duration");
                    break;
                case SaurusAiState.KnockDown:
                    _knockDownPhase = KnockDownPhase.Begin;
                    float beginLength = Mathf.Max(
                        0.1f,
                        _motion.PlayKnockDownBegin(_profile, now));
                    _stateUntil = now + beginLength;
                    _knockDownLoopUntil = _stateUntil + EvaluateDuration(
                        _profile.KnockDownDurationFormula,
                        "knock_down_duration");
                    break;
                case SaurusAiState.Retreat:
                    _retreatPending = false;
                    _motion.PlayMove();
                    _stateUntil = now + _species.RetreatSeconds;
                    break;
                case SaurusAiState.ReturnHome:
                    _repositionUntilFront = false;
                    _motion.PlayMove();
                    break;
                case SaurusAiState.Dead:
                    _repositionUntilFront = false;
                    _roamTurnPending = false;
                    _engaged = false;
                    _activeAttack = null;
                    _activeActionPlan = null;
                    _activeActionInstanceId = 0L;
                    _pendingReactionKind = ReactionKind.None;
                    PublishStatus(AnimalStatus.Dead);
                    break;
            }

            DurangoCombatSystemPlugin.Log.LogInfo(
                "Saurus AI state entity=" + EntityId +
                " type=" + _profile.EntityTypeId +
                " " + previous + "->" + next +
                " reason=" + reason + ".");
        }

        private void InitializeGroggyGauge()
        {
            bool exactMaximum;
            _groggyMaximum = AnimalFormulaEvaluator.Evaluate(
                _profile.GroggyMaxFormula,
                Math.Max(1, _animal.Level),
                1f,
                0f,
                out exactMaximum);
            bool exactThreshold = false;
            _groggyThreshold = _profile.GroggySectionFormulas == null ||
                _profile.GroggySectionFormulas.Length < 3
                ? 0f
                : AnimalFormulaEvaluator.Evaluate(
                    _profile.GroggySectionFormulas[2],
                    Math.Max(1, _animal.Level),
                    1f,
                    0f,
                    out exactThreshold);
            bool exactWeakThreshold = false;
            _groggyWeakThreshold =
                _profile.GroggySectionFormulas == null ||
                _profile.GroggySectionFormulas.Length < 2
                ? 0f
                : AnimalFormulaEvaluator.Evaluate(
                    _profile.GroggySectionFormulas[1],
                    Math.Max(1, _animal.Level),
                    1f,
                    0f,
                    out exactWeakThreshold);
            if (!exactMaximum || !exactThreshold ||
                !exactWeakThreshold ||
                _groggyMaximum <= 0f || _groggyThreshold <= 0f ||
                _groggyWeakThreshold <= _groggyThreshold ||
                _groggyWeakThreshold >= _groggyMaximum)
            {
                _groggyMaximum = 0f;
                _groggyThreshold = 0f;
                _groggyWeakThreshold = 0f;
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Saurus groggy gauge disabled because original formulas " +
                    "could not be evaluated: entity=" + EntityId + ".");
            }
            _groggyCurrent = _groggyMaximum;
        }

        private float EvaluateDuration(string formula, string field)
        {
            bool exact;
            float duration = AnimalFormulaEvaluator.Evaluate(
                formula,
                Math.Max(1, _animal.Level),
                1f,
                0f,
                out exact);
            if (!exact || duration <= 0f)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Saurus reaction duration unavailable: entity=" +
                    EntityId + " field=" + field + ".");
                return 0.1f;
            }
            return duration;
        }

        private string GetGroggySection(float current)
        {
            if (current <= 0f)
            {
                return "KnockDown";
            }
            if (current <= _groggyThreshold)
            {
                return "Groggy";
            }
            return current <= _groggyWeakThreshold ? "Weak" : "Super";
        }

        private void ResetGroggyGauge(double now)
        {
            if (_groggyMaximum <= 0f)
            {
                return;
            }
            _groggyCurrent = _groggyMaximum;
            PublishGroggyGauge(now, "Super");
        }

        private void PublishGroggyGauge(double now, string section)
        {
            SaurusGroggyGaugeChangedHandler changed = GroggyGaugeChanged;
            if (changed == null || _groggyMaximum <= 0f)
            {
                return;
            }
            Gauge gauge = new Gauge(
                _groggyMaximum,
                0f,
                new GaugeNode[]
                {
                    new GaugeNode(now, _groggyCurrent)
                });
            changed(this, gauge, section);
        }

        private void PublishStatus(AnimalStatus status)
        {
            if (_publishedStatus == status)
            {
                return;
            }
            _publishedStatus = status;
            SaurusAnimalStatusChangedHandler changed = StatusChanged;
            if (changed != null)
            {
                changed(this, status);
            }
        }

        private void ClearReactionStatus()
        {
            if (_publishedStatus == AnimalStatus.Blow ||
                _publishedStatus == AnimalStatus.Groggy ||
                _publishedStatus == AnimalStatus.KnockDown)
            {
                // Battle has no floating emoji in the original client and
                // therefore acts as the protocol-level clear for a finished
                // combat reaction.
                PublishStatus(AnimalStatus.Battle);
            }
        }

        private SaurusEvadeRoute SelectEvadeRoute(
            DamageDirection incomingDirection)
        {
            bool first = _random.Range(0f, 1f) < 0.5f;
            switch (incomingDirection)
            {
                case DamageDirection.Left:
                case DamageDirection.Right:
                    return first
                        ? SaurusEvadeRoute.Forward
                        : SaurusEvadeRoute.Backward;
                default:
                    return first
                        ? SaurusEvadeRoute.Left
                        : SaurusEvadeRoute.Right;
            }
        }

        private bool UsesIntentExecution()
        {
            return _profile != null &&
                (_profile.EntityTypeId == ZebraceratopsEntityTypeId ||
                    _profile.EntityTypeId == ElephantulusEntityTypeId ||
                    _profile.EntityTypeId == DeinonychusEntityTypeId ||
                    _profile.EntityTypeId == RaptorEntityTypeId);
        }

        private float FaceCombatTarget(PlayerBehavior player)
        {
            if (_profile != null &&
                _profile.EntityTypeId == ZebraceratopsEntityTypeId)
            {
                return _motion.FacePositionWithTurnAnimation(
                    player.CurrentPosition,
                    _profile,
                    _tuning.FaceToleranceDegrees);
            }
            return _motion.FacePosition(player.CurrentPosition, false);
        }

        private bool ShouldReturnHome(Vector3 playerPosition)
        {
            return Distance2D(_homePosition, _animal.CurrentPosition) >
                    _tuning.LeashDistance ||
                DistanceTo(playerPosition) >
                    _tuning.MaximumPursuitDistance;
        }

        private float DistanceTo(Vector3 position)
        {
            return Distance2D(_animal.CurrentPosition, position);
        }

        private static float Distance2D(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private float GetAnimalRadius()
        {
            float scale = GetSpatialScale();
            return Mathf.Max(_animal.XRadius, _animal.YRadius) *
                Mathf.Max(0.01f, scale);
        }

        private float GetSpatialScale()
        {
            if (_animal == null)
            {
                return 1f;
            }
            return Mathf.Max(
                0.01f,
                Mathf.Max(
                    Mathf.Abs(_animal.transform.lossyScale.x),
                    Mathf.Abs(_animal.transform.lossyScale.z)));
        }

        private static float GetPlayerRadius(PlayerBehavior player)
        {
            if (player == null)
            {
                return 0f;
            }
            float scale = Mathf.Max(
                Mathf.Abs(player.transform.lossyScale.x),
                Mathf.Abs(player.transform.lossyScale.z));
            return Mathf.Max(player.XRadius, player.YRadius) *
                Mathf.Max(0.01f, scale);
        }

        private static bool HasValidPlayer(PlayerBehavior player)
        {
            return player != null && player.IsAlive;
        }

        private void CaptureContext(double now, PlayerBehavior player)
        {
            Vector3 actorPosition = _animal.CurrentPosition;
            Vector3 targetPosition = Vector3.zero;
            bool hasTarget = HasValidPlayer(player);
            if (hasTarget)
            {
                targetPosition = player.CurrentPosition;
            }

            float sampleSeconds = _previousContextAt <= 0.0
                ? 0f
                : Mathf.Max(0f, (float)(now - _previousContextAt));
            Vector3 actorVelocity = _hasPreviousContextSample &&
                sampleSeconds > 0.0001f
                    ? (actorPosition - _previousContextActorPosition) /
                        sampleSeconds
                    : Vector3.zero;
            Vector3 targetVelocity = hasTarget &&
                _hasPreviousTargetSample && sampleSeconds > 0.0001f
                    ? (targetPosition - _previousContextTargetPosition) /
                        sampleSeconds
                    : Vector3.zero;
            actorVelocity.y = 0f;
            targetVelocity.y = 0f;

            float actorRadius = GetAnimalRadius();
            float targetRadius = hasTarget ? GetPlayerRadius(player) : 0f;
            float centerDistance = hasTarget
                ? Distance2D(actorPosition, targetPosition)
                : 0f;
            float surfaceDistance = hasTarget
                ? Mathf.Max(0f, centerDistance - actorRadius - targetRadius)
                : 0f;
            float bearing = _animal.CurrentYaw;
            float relative = 0f;
            SaurusTargetSector sector = SaurusTargetSector.None;
            if (hasTarget)
            {
                Vector3 toTarget = targetPosition - actorPosition;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    bearing = Mathf.Atan2(toTarget.x, toTarget.z) *
                        Mathf.Rad2Deg;
                    relative = Mathf.DeltaAngle(
                        _animal.CurrentYaw,
                        bearing);
                    sector = SaurusCombatContext.ResolveSector(relative);
                }
            }

            if (_engaged && sector != _lastTargetSector)
            {
                if (sector == SaurusTargetSector.LeftFlank ||
                    sector == SaurusTargetSector.RightFlank)
                {
                    _memory.Record(
                        SaurusCombatEventType.TargetEnteredFlank,
                        now,
                        hasTarget ? player.EntityId : null,
                        _activeActionInstanceId,
                        _activeAttack == null ? null : _activeAttack.Key);
                }
                else if (sector == SaurusTargetSector.Rear)
                {
                    _memory.Record(
                        SaurusCombatEventType.TargetEnteredRear,
                        now,
                        hasTarget ? player.EntityId : null,
                        _activeActionInstanceId,
                        _activeAttack == null ? null : _activeAttack.Key);
                }
            }
            _lastTargetSector = sector;

            float actorLife;
            float actorLifeMaximum;
            ReadLife(_animal, now, out actorLife, out actorLifeMaximum);
            float targetLife;
            float targetLifeMaximum;
            ReadLife(player, now, out targetLife, out targetLifeMaximum);
            bool actionLocked =
                (_state == SaurusAiState.Attack ||
                    _state == SaurusAiState.Evade ||
                    _state == SaurusAiState.Reaction) &&
                now < _stateUntil;

            LatestContext = new SaurusCombatContext(
                ++_nextContextSequence,
                _generation,
                _engagementId,
                now,
                EntityId,
                _profile.EntityTypeId,
                _objectInstanceId,
                actorPosition,
                _animal.CurrentYaw,
                actorRadius,
                actorVelocity,
                actorLife,
                actorLifeMaximum,
                _state,
                _engaged,
                hasTarget ? player.EntityId : null,
                hasTarget,
                targetPosition,
                hasTarget ? player.CurrentYaw : 0f,
                targetRadius,
                targetVelocity,
                targetLife,
                targetLifeMaximum,
                centerDistance,
                surfaceDistance,
                bearing,
                relative,
                sector,
                SaurusObservationState.Unknown,
                _blockedSince > 0.0
                    ? SaurusObservationState.Blocked
                    : SaurusObservationState.Unknown,
                actionLocked,
                _activeAttack == null ? null : _activeAttack.Key,
                _activeActionInstanceId,
                Mathf.Max(0f, (float)(_nextAttackAt - now)),
                Mathf.Max(0f, (float)(_stateUntil - now)),
                _memory.LatestEvent,
                _memory.EventCount);
            _previousContextActorPosition = actorPosition;
            _previousContextAt = now;
            _hasPreviousContextSample = true;
            if (hasTarget)
            {
                _previousContextTargetPosition = targetPosition;
                _hasPreviousTargetSample = true;
            }
            else
            {
                _hasPreviousTargetSample = false;
            }
        }

        private static void ReadLife(
            CharacterBehavior character,
            double now,
            out float current,
            out float maximum)
        {
            current = 0f;
            maximum = 0f;
            if (character == null || character.Life == null)
            {
                return;
            }
            current = character.Life.Get(now);
            maximum = character.Life.Max(now);
        }

        private static string GetPlayerEntityId()
        {
            return PlayerBehavior.LocalPlayer == null
                ? null
                : PlayerBehavior.LocalPlayer.EntityId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            LatestContext = null;
            LatestShadowDecision = null;
            LastSelectionShadowDecision = null;
            _memory.Clear();
            AttackCommitted = null;
            GroggyGaugeChanged = null;
            StatusChanged = null;
            SaurusDebugBubble.Hide(EntityId);
            _motion.Dispose();
        }
    }
}
