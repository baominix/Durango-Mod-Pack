using System;
using BaoX.DurangoOriginal.CombatSystemMod.Geometry;
using Durango.Network;
using Durango.Utils;
using HarmonyLib;
using Messages;
using Shared.Battle;
using UnityEngine;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    public sealed class LocalWildAnimalCombatAI : MonoBehaviour
    {
        private enum BrachioMode
        {
            Idle,
            Roaming,
            BattleBegin,
            Chase,
            SkillWait,
            TailPrep,
            WoundedTailPrep,
            Attack,
            Incapacitated,
            Dead
        }

        private const int BrachioEntityType = 2004;
        private const float BrachioWalkSpeed = 135f;
        private const float BrachioRunSpeed = 260f;
        private const float BrachioFrontAttackRange =
            BrachioAttackProfiles.AreaAttackDistance;
        private const float BrachioTailAttackRange =
            BrachioAttackProfiles.TailAreaDistance;
        private const float BrachioIdleMinSeconds = 2f;
        private const float BrachioIdleMaxSeconds = 5f;
        private const float BrachioRoamMinSeconds = 2f;
        private const float BrachioRoamMaxSeconds = 6f;
        private const float BrachioEngageDistance = BrachioAttackProfiles.EngageDistance;
        private const float BrachioAttackDistance = BrachioAttackProfiles.AttackDistance;
        private const float BrachioAttackCooldown = BrachioAttackProfiles.AttackCooldown;
        private const float BrachioAttackEndDelay = BrachioAttackProfiles.AttackEndDelay;
        private const float BrachioAttackHitBeforeEnd = BrachioAttackProfiles.AttackHitBeforeEnd;
        private const float BrachioTailAttackHitBeforeEnd =
            BrachioAttackProfiles.TailAttackHitBeforeEnd;
        private const float BrachioWoundedTailAttackHitBeforeEnd =
            BrachioAttackProfiles.WoundedTailAttackHitBeforeEnd;
        private const float BrachioTailAttackPrepSeconds =
            BrachioAttackProfiles.TailAttackPrepSeconds;
        private const float BrachioWoundedTailPrepSeconds =
            BrachioAttackProfiles.WoundedTailAttackPrepSeconds;
        private const float BrachioTailAttackChance = BrachioAttackProfiles.TailAttackChance;
        private const float BrachioWoundedTailAttackChance =
            BrachioAttackProfiles.WoundedTailAttackChance;
        private const float BrachioDefenseValue = 0.8f;
        private const float BrachioHealCooldown = 0.1f;
        private const float BrachioRegenAmount = 1f;
        private const float BlowActiveSeconds = 20f;
        private const float BrachioKnockDownSeconds = 6f;

        private AnimalBehavior _animal;
        private string _standMotion;
        private string _moveMotion;
        private string _attackMotion;
        private string _damagedMotion;
        private string _groggyMotion;
        private string _deadMotion;
        private string _walkMotion;
        private string _runMotion;
        private string _runStopMotion;
        private string _turnMotion;
        private string _limpMotion;
        private string _battleIdleMotion;
        private string _eatMotion;
        private string _eatLowerMotion;
        private string _activeMotion;
        private string _sleepBeginMotion;
        private string _sleepLoopMotion;
        private string _sleepEndMotion;
        private string _damageFrontMotion;
        private string _damageBackMotion;
        private string _damageLeftMotion;
        private string _damageRightMotion;
        private string _knockDownBeginMotion;
        private string _knockDownLoopMotion;
        private string _knockDownEndMotion;
        private float _moveSpeed = 350f;
        private float _attackRange = 450f;
        private float _attackCooldown = 2.2f;
        private float _nextAttackAt;
        private float _damageAt;
        private float _motionLockedUntil;
        private float _nextPassiveMotionAt;
        private float _sleepUntil;
        private float _pendingAttackRange;
        private float _pendingAttackArcStart;
        private float _pendingAttackArcEnd;
        private float _pendingAttackDamageScale = 1f;
        private float _brachioStateUntil;
        private float _brachioAttackReadyAt;
        private float _brachioChaseStartedAt;
        private float _brachioTailPrepEndAt;
        private float _brachioWoundedTailPrepEndAt;
        private float _brachioCurrentAttackHitBeforeEnd;
        private float _brachioStuckStartedAt;
        private float _brachioNextRegenAt;
        private Vector3 _brachioLastPosition;
        private bool _damagePending;
        private bool _aggressive;
        private bool _returningHome;
        private bool _dead;
        private bool _isBrachio;
        private bool _sleeping;
        private bool _brachioTailAttack;
        private bool _brachioWoundedTailAttack;
        private BrachioMode _brachioMode;
        private DamageDirection _pendingAttackDirection = DamageDirection.Front;
        private DamageEffects _pendingAttackEffects = DamageEffects.None;
        private AnimalAttackArea _pendingAttackArea;
        private string _pendingAttackId;
        private string _mode;
        private float _groggyAccumulated;
        private float _groggyStatusUntil;
        private float _blowAccumulated;
        private float _blowActiveUntil;
        private float _incapacitateAccumulated;
        private float _incapacitateLoopAt;
        private float _incapacitateUntil;
        private float _incapacitateEndAt;
        private bool _incapacitateEnding;
        private Vector3 _homePosition;
        private Vector3 _brachioRoamDirection;
        private Vector3 _brachioAttackDirection;
        private Vector3 _brachioAreaDirection;
        private TweenPosition _brachioAttackTween;
        private AnimalCombatProfile _profile;

        internal static LocalWildAnimalCombatAI Attach(AnimalBehavior animal)
        {
            if (!OfflineCombatAnimalTargets.IsCombatAnimal(animal))
            {
                return null;
            }

            LocalWildAnimalCombatAI ai = animal.GetComponent<LocalWildAnimalCombatAI>();
            if (ai == null)
            {
                ai = animal.gameObject.AddComponent<LocalWildAnimalCombatAI>();
            }
            ai.Initialize(animal);
            return ai;
        }

        internal void Initialize(AnimalBehavior animal)
        {
            if (_animal == animal && _animal != null)
            {
                return;
            }

            _animal = animal;
            _profile = AnimalCombatProfiles.Get(animal.EntityTypeId);
            _isBrachio = animal.EntityTypeId == BrachioEntityType;
            _homePosition = animal.CurrentPosition;
            _attackRange = Mathf.Max(
                300f, Mathf.Max(animal.XRadius, _profile.BoundRadius) + 250f);
            _attackCooldown = _profile.AttackCooldown;
            _moveSpeed = Mathf.Clamp(260f + animal.Level * 4f, 300f, 650f);
            ResolveMotions();
            if (_isBrachio)
            {
                ApplyBrachioDefaults();
                _animal.ResetRootMotionOffset();
                _animal.SetActivateRootMotion(false);
            }
            _moveSpeed = Mathf.Clamp(
                _moveSpeed * _profile.MoveSpeedMultiplier, 220f, 780f);
            if (_isBrachio)
            {
                _moveSpeed = BrachioRunSpeed;
            }
            _damagePending = false;
            _dead = false;
            _sleeping = false;
            _blowAccumulated = 0f;
            _blowActiveUntil = 0f;
            _incapacitateAccumulated = 0f;
            _incapacitateLoopAt = 0f;
            _incapacitateUntil = 0f;
            _incapacitateEndAt = 0f;
            _incapacitateEnding = false;
            _groggyStatusUntil = 0f;
            _pendingAttackId = null;
            _nextPassiveMotionAt = Time.time + UnityEngine.Random.Range(1.5f, 4f);
            if (_isBrachio)
            {
                _brachioAttackReadyAt = 0f;
                _brachioNextRegenAt = Time.time + BrachioHealCooldown;
                _brachioTailAttack = false;
                _brachioWoundedTailAttack = false;
                SwitchBrachioIdle("initialize");
            }
        }

        internal void ActivateCombat(string reason)
        {
            if (_animal == null || _dead || !_animal.IsAlive)
            {
                return;
            }

            SetAggressive(reason);
            _returningHome = false;
            _nextAttackAt = Mathf.Min(_nextAttackAt, Time.time + 0.35f);
        }

        internal void NotifyDamaged(Damage damage, float groggyPower, float blowPower)
        {
            if (_animal == null || _dead)
            {
                return;
            }

            if (_isBrachio && _brachioMode == BrachioMode.Incapacitated)
            {
                return;
            }

            OfflineCombatDebugCommands.ReleasePlayerInitiatedAnimal(_animal.EntityId);
            SetAggressive("damaged");
            _returningHome = false;
            _nextAttackAt = Mathf.Min(_nextAttackAt, Time.time + 0.35f);
            if ((damage.Effects & DamageEffects.KnockBack) != DamageEffects.None)
            {
                ApplyKnockBack(blowPower);
            }

            _groggyAccumulated += Mathf.Max(0f, groggyPower);
            float groggyThreshold = Mathf.Clamp(4f + _animal.Level * 0.06f, 4f, 12f);
            if (_groggyAccumulated >= groggyThreshold)
            {
                _groggyAccumulated = 0f;
                EnterGroggy();
                return;
            }

            string damagedMotion = _isBrachio
                ? GetBrachioDamageMotion(damage.Direction)
                : _damagedMotion;
            if (!string.IsNullOrEmpty(damagedMotion) &&
                ((damage.Effects & DamageEffects.Blow) != DamageEffects.None ||
                 (damage.Effects & DamageEffects.Critical) != DamageEffects.None ||
                 (damage.Effects & DamageEffects.KnockBack) != DamageEffects.None))
            {
                StopBrachioAttackMovement();
                float duration = _animal.CrossFade(damagedMotion, 0.08f, false, 0f, 1f);
                _motionLockedUntil = Time.time + Mathf.Max(0.15f, duration * 0.65f);
                _mode = _isBrachio ? "brachio_damaged" : "damaged";
            }
        }

        internal bool RegisterBlowImpact(float blowPower, out bool incapacitate)
        {
            incapacitate = false;
            if (_animal == null || _dead)
            {
                return false;
            }

            if (_isBrachio && _brachioMode == BrachioMode.Incapacitated)
            {
                return false;
            }

            float now = Time.time;
            if (_blowActiveUntil > 0f)
            {
                if (now < _blowActiveUntil)
                {
                    if (_isBrachio)
                    {
                        float incapacitateThreshold = Mathf.Max(1f, _profile.BlowResistance);
                        _incapacitateAccumulated += Mathf.Max(0f, blowPower);
                        if (_incapacitateAccumulated >= incapacitateThreshold)
                        {
                            _blowActiveUntil = 0f;
                            _blowAccumulated = 0f;
                            _incapacitateAccumulated = 0f;
                            incapacitate = true;
                            EnterBrachioIncapacitated();
                            return false;
                        }

                        OfflineCombatBackendPlugin.Log.LogInfo(
                                "Animal incapacitate charge entity=" + _animal.EntityId +
                                " value=" + _incapacitateAccumulated.ToString("F0") +
                                "/" + incapacitateThreshold.ToString("F0"));

                        OfflineCombatAnimalStatusIcons.SetBrachioBlow(
                            _animal,
                            Mathf.Max(1f, _blowActiveUntil - now));
                    }
                    return true;
                }

                _blowActiveUntil = 0f;
                _blowAccumulated = 0f;
                _incapacitateAccumulated = 0f;
                if (_isBrachio)
                {
                    OfflineCombatAnimalStatusIcons.ClearBrachioBlow(_animal);
                }
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Animal blow window ended entity=" + _animal.EntityId);
            }

            float threshold = Mathf.Max(1f, _profile.BlowResistance);
            _blowAccumulated += Mathf.Max(0f, blowPower);
            if (_blowAccumulated < threshold)
            {
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Animal blow charge entity=" + _animal.EntityId +
                    " value=" + _blowAccumulated.ToString("F0") +
                    "/" + threshold.ToString("F0"));
                return false;
            }

            _blowAccumulated = 0f;
            _blowActiveUntil = now + BlowActiveSeconds;
            _incapacitateAccumulated = 0f;
            if (_isBrachio)
            {
                OfflineCombatAnimalStatusIcons.SetBrachioBlow(
                    _animal,
                    BlowActiveSeconds);
            }
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Animal blow window started entity=" + _animal.EntityId +
                " duration=" + BlowActiveSeconds.ToString("F0") +
                " threshold=" + threshold.ToString("F0"));
            // The threshold hit arms the Blow state. A later successful hit
            // during this window applies DamageEffects.Blow, so the status is
            // always visible before the reaction can occur.
            return false;
        }

        private void ApplyKnockBack(float blowPower)
        {
            if (PlayerBehavior.LocalPlayer == null)
            {
                return;
            }

            StopBrachioAttackMovement();
            Vector3 direction = _animal.CurrentPosition - PlayerBehavior.LocalPlayer.CurrentPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = -_animal.transform.forward;
            }
            direction.Normalize();
            float resistanceScale = Mathf.Clamp(
                1f - _profile.KnockBackResistance / 1000f, 0.25f, 1f);
            float distance = Mathf.Clamp(blowPower * 0.65f * resistanceScale, 40f, 280f);
            if (_isBrachio)
            {
                distance = Mathf.Clamp(distance * 0.22f, 12f, 55f);
            }
            Vector3 next = _animal.CurrentPosition + direction * distance;
            next.y = _animal.ProcessWaterDepth(next);
            _animal.CurrentPosition = next;
            _motionLockedUntil = Mathf.Max(_motionLockedUntil, Time.time + 0.65f);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Animal knockback entity=" + _animal.EntityId + " distance=" + distance);
        }

        private void EnterGroggy()
        {
            if (_isBrachio && _brachioMode == BrachioMode.Incapacitated)
            {
                return;
            }

            StopBrachioAttackMovement();
            string motion = !string.IsNullOrEmpty(_groggyMotion)
                ? _groggyMotion
                : _damagedMotion;
            float duration = Mathf.Max(0.5f, _profile.GroggyDuration);
            if (!string.IsNullOrEmpty(motion))
            {
                duration = Mathf.Max(duration, _animal.CrossFade(motion, 0.08f, false, 0f, 1f));
            }
            _damagePending = false;
            _motionLockedUntil = Time.time + duration;
            _nextAttackAt = _motionLockedUntil + 0.5f;
            _mode = "groggy";
            if (_isBrachio)
            {
                _groggyStatusUntil = Time.time + duration;
                OfflineCombatAnimalStatusIcons.SetBrachioGroggy(
                    _animal,
                    Mathf.Max(1f, duration));
            }
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Animal groggy entity=" + _animal.EntityId + " duration=" + duration);
        }

        private void EnterBrachioIncapacitated()
        {
            if (!_isBrachio || _animal == null || _dead)
            {
                return;
            }

            StopBrachioAttackMovement();
            _brachioMode = BrachioMode.Incapacitated;
            _damagePending = false;
            _sleeping = false;
            _groggyAccumulated = 0f;
            _groggyStatusUntil = 0f;
            _incapacitateEnding = false;
            float beginDuration = 0.8f;
            if (!string.IsNullOrEmpty(_knockDownBeginMotion))
            {
                beginDuration = Mathf.Max(
                    0.35f,
                    _animal.CrossFade(
                        _knockDownBeginMotion, 0.08f, false, 0f, 1f));
            }
            _incapacitateLoopAt = Time.time + beginDuration;
            _incapacitateUntil = Time.time + BrachioKnockDownSeconds;
            _incapacitateEndAt = 0f;
            _motionLockedUntil = _incapacitateUntil;
            _nextAttackAt = _incapacitateUntil + _attackCooldown;
            _mode = "brachio_incapacitate_begin";
            OfflineCombatAnimalStatusIcons.SetBrachioIncapacitated(
                _animal,
                BrachioKnockDownSeconds);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Animal incapacitated entity=" + _animal.EntityId +
                " duration=" + BrachioKnockDownSeconds.ToString("F0"));
        }

        internal void NotifyDead()
        {
            StopBrachioAttackMovement();
            if (_animal != null)
            {
                OfflineCombatDebugCommands.ReleasePlayerInitiatedAnimal(_animal.EntityId);
            }
            _dead = true;
            _aggressive = false;
            _returningHome = false;
            _damagePending = false;
            _sleeping = false;
            _blowAccumulated = 0f;
            _blowActiveUntil = 0f;
            _incapacitateAccumulated = 0f;
            _incapacitateUntil = 0f;
            _incapacitateEndAt = 0f;
            _groggyStatusUntil = 0f;
            if (_isBrachio)
            {
                _brachioMode = BrachioMode.Dead;
                OfflineCombatAnimalStatusIcons.Clear(_animal);
            }
            if (_animal != null && !string.IsNullOrEmpty(_deadMotion))
            {
                _animal.CrossFade(_deadMotion, 0.1f, false, 0f, 1f);
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Animal death motion started entity=" + _animal.EntityId +
                    " motion=" + _deadMotion);
            }
            enabled = false;
        }

        private void UpdateTimedCombatStatuses()
        {
            float now = Time.time;
            if (_blowActiveUntil > 0f && now >= _blowActiveUntil)
            {
                _blowActiveUntil = 0f;
                _blowAccumulated = 0f;
                if (_isBrachio)
                {
                    OfflineCombatAnimalStatusIcons.ClearBrachioBlow(_animal);
                }
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Animal blow window ended entity=" + _animal.EntityId);
            }

            if (_groggyStatusUntil > 0f && now >= _groggyStatusUntil)
            {
                _groggyStatusUntil = 0f;
                if (_isBrachio)
                {
                    OfflineCombatAnimalStatusIcons.ClearBrachioGroggy(_animal);
                }
            }
        }

        private void Update()
        {
            if (_animal == null || _dead || !_animal.IsAlive ||
                PlayerBehavior.LocalPlayer == null)
            {
                return;
            }

            UpdateTimedCombatStatuses();

            if (!PlayerBehavior.LocalPlayer.IsAlive)
            {
                _sleeping = false;
                PlayStand();
                return;
            }

            Vector3 playerPosition = PlayerBehavior.LocalPlayer.CurrentPosition;
            float playerDistance = HorizontalDistance(
                playerPosition, _animal.CurrentPosition);
            if (_isBrachio)
            {
                UpdateBrachioRegen();
                if (_brachioMode == BrachioMode.Incapacitated)
                {
                    UpdateBrachioIncapacitated();
                    return;
                }
            }

            if (!_aggressive)
            {
                if (_returningHome)
                {
                    ReturnHome();
                    return;
                }

                if (_profile.IsProactive &&
                    !OfflineCombatDebugCommands.WaitsForPlayerAttack(_animal.EntityId) &&
                    playerDistance <= _profile.AggroRange)
                {
                    SetAggressive("proximity");
                }
                else
                {
                    if (_isBrachio)
                    {
                        UpdateBrachioPassiveState();
                    }
                    return;
                }
            }

            float combatExitDistance = _isBrachio
                ? BrachioAttackProfiles.CombatExitDistance
                : _profile.LeashRange;
            if (HorizontalDistance(playerPosition, _homePosition) > combatExitDistance ||
                playerDistance > combatExitDistance)
            {
                Disengage();
                ReturnHome();
                return;
            }

            if (_damagePending && Time.time >= _damageAt)
            {
                _damagePending = false;
                if (_isBrachio)
                {
                    OfflineCombatRuntime.ApplyAnimalAttack(
                        _animal,
                        _pendingAttackRange,
                        _pendingAttackArcStart,
                        _pendingAttackArcEnd,
                        _pendingAttackDamageScale,
                        _pendingAttackDirection,
                        _pendingAttackEffects,
                        _pendingAttackId,
                        _brachioAreaDirection,
                        _pendingAttackArea);
                }
                else
                {
                    OfflineCombatRuntime.ApplyAnimalAttack(_animal);
                }
            }

            if (_isBrachio)
            {
                UpdateBrachioState(playerPosition, playerDistance);
                return;
            }

            if (Time.time < _motionLockedUntil)
            {
                return;
            }

            Vector3 delta = playerPosition - _animal.CurrentPosition;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > _attackRange)
            {
                Chase(playerPosition);
                return;
            }

            PlayStand();
            if (Time.time >= _nextAttackAt)
            {
                StartAttack(playerPosition);
            }
        }

        private void SetAggressive(string reason)
        {
            if (_aggressive)
            {
                return;
            }
            _aggressive = true;
            _returningHome = false;
            _sleeping = false;
            _nextAttackAt = Time.time + 0.35f;
            OfflineCombatRuntime.BeginCombat(_animal.EntityId);
            if (_isBrachio)
            {
                SwitchBrachioBattleBegin(reason);
            }
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Animal aggro entity=" + _animal.EntityId +
                " profile=" + _profile.Name +
                " type=" + _profile.AnimalType +
                " reason=" + reason +
                " range=" + _profile.AggroRange);
        }

        private void Disengage()
        {
            if (!_aggressive)
            {
                return;
            }
            StopBrachioAttackMovement();
            _aggressive = false;
            _returningHome = true;
            _damagePending = false;
            _sleeping = false;
            if (_isBrachio)
            {
                OfflineCombatAnimalStatusIcons.SetBrachioPreview(_animal);
                _brachioMode = BrachioMode.Idle;
            }
            OfflineCombatRuntime.NotifyAnimalDisengaged(_animal);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Animal disengaged entity=" + _animal.EntityId +
                " leash=" + (_isBrachio
                    ? BrachioAttackProfiles.CombatExitDistance
                    : _profile.LeashRange));
        }

        private void ReturnHome()
        {
            float distance = HorizontalDistance(_animal.CurrentPosition, _homePosition);
            if (distance <= 30f)
            {
                _animal.CurrentPosition = _homePosition;
                _returningHome = false;
                PlayStand();
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Animal returned home entity=" + _animal.EntityId);
                return;
            }
            if (_isBrachio)
            {
                ChaseBrachio(_homePosition, distance);
            }
            else
            {
                Chase(_homePosition);
            }
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private void Chase(Vector3 playerPosition)
        {
            float yaw = Maths.CalcYawWithTarget(playerPosition, _animal.CurrentPosition);
            _animal.TurnToYaw(yaw, false);
            if (_mode != "move" && !string.IsNullOrEmpty(_moveMotion))
            {
                _animal.CrossFade(_moveMotion, 0.1f, true, 0f, 1f);
                _mode = "move";
            }

            Vector3 next = Vector3.MoveTowards(
                _animal.CurrentPosition,
                playerPosition,
                _moveSpeed * Time.deltaTime);
            next.y = _animal.ProcessWaterDepth(next);
            _animal.CurrentPosition = next;
        }

        private void ApplyBrachioDefaults()
        {
            _standMotion = "Brachio_Stand";
            _battleIdleMotion = "Brachio_BattleStand";
            _moveMotion = "Brachio_Walk";
            _walkMotion = "Brachio_Walk";
            _runMotion = "Brachio_Run";
            _runStopMotion = "Brachio_Run_Stop";
            _turnMotion = "Brachio_Turn";
            _limpMotion = "Brachio_Limp";
            _attackMotion = "Brachio_Attack";
            _damagedMotion = "Brachio_Damage_S";
            _groggyMotion = "Brachio_Groggy";
            _deadMotion = "Brachio_Die";
            _eatMotion = "Brachio_Eat";
            _eatLowerMotion = "Brachio_Eat_Lower";
            _activeMotion = "Brachio_Active_Default";
            _sleepBeginMotion = "Brachio_Sleep_Begin";
            _sleepLoopMotion = "Brachio_Sleep_Looping";
            _sleepEndMotion = "Brachio_Sleep_End";
            _damageFrontMotion = "Brachio_Damage_S";
            _damageBackMotion = "Brachio_Damage_N";
            _damageLeftMotion = "Brachio_Damage_E";
            _damageRightMotion = "Brachio_Damage_W";
            _knockDownBeginMotion = "Brachio_Damage_Blow_Begin";
            _knockDownLoopMotion = "Brachio_Damage_Blow_Looping";
            _knockDownEndMotion = "Brachio_Damage_Blow_End";
            _attackRange = BrachioTailAttackRange;
            _attackCooldown = Mathf.Max(BrachioAttackCooldown, _profile.AttackCooldown);
            _moveSpeed = BrachioRunSpeed;
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Brachio local AI configured entity=" + _animal.EntityId +
                " level=" + _animal.Level +
                " cooldown=" + _attackCooldown +
                " defense=" + BrachioDefenseValue +
                " healCooldown=" + BrachioHealCooldown);
        }

        private void UpdateBrachioRegen()
        {
            if (!_isBrachio ||
                _animal == null ||
                _animal.Life == null ||
                Time.time < _brachioNextRegenAt)
            {
                return;
            }

            _brachioNextRegenAt = Time.time + BrachioHealCooldown;
            float current = _animal.Life.Get();
            float max = _animal.Life.Max();
            if (current <= 0f || current >= max)
            {
                return;
            }

            float nextLife = Mathf.Min(max, current + BrachioRegenAmount);
            Gauge life = new Gauge(max, 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = nextLife }
            });
            _animal.SetSurvivalGauge(life, null);
        }

        private void UpdateBrachioPassiveState()
        {
            if (_brachioMode == BrachioMode.Roaming)
            {
                PlayBrachioLoop(_walkMotion, "brachio_roam");
                TurnToDirection(_brachioRoamDirection);
                MoveBrachio(_brachioRoamDirection, BrachioWalkSpeed);
                if (Time.time >= _brachioStateUntil)
                {
                    SwitchBrachioIdle("roam-timeout");
                }
                return;
            }

            if (_sleeping)
            {
                if (Time.time >= _sleepUntil)
                {
                    float duration = 0.8f;
                    if (!string.IsNullOrEmpty(_sleepEndMotion))
                    {
                        duration = _animal.CrossFade(_sleepEndMotion, 0.12f, false, 0f, 1f);
                    }
                    _sleeping = false;
                    _mode = "brachio_sleep_end";
                    _motionLockedUntil = Time.time + Mathf.Min(Mathf.Max(0.4f, duration), 1.2f);
                    _nextPassiveMotionAt = Time.time + Mathf.Max(0.8f, duration) +
                        UnityEngine.Random.Range(2f, 4f);
                    return;
                }

                if (Time.time >= _motionLockedUntil &&
                    _mode != "brachio_sleep_loop" &&
                    !string.IsNullOrEmpty(_sleepLoopMotion))
                {
                    _animal.CrossFade(_sleepLoopMotion, 0.12f, true, 0f, 1f);
                    _mode = "brachio_sleep_loop";
                }
                return;
            }

            if (Time.time >= _brachioStateUntil)
            {
                SwitchBrachioRoaming("idle-timeout");
                return;
            }

            if (Time.time < _nextPassiveMotionAt)
            {
                PlayBrachioLoop(_standMotion, "brachio_idle");
                return;
            }

            int roll = UnityEngine.Random.Range(0, 12);
            if (roll == 0 && !string.IsNullOrEmpty(_sleepBeginMotion))
            {
                float duration = _animal.CrossFade(_sleepBeginMotion, 0.15f, false, 0f, 1f);
                duration = Mathf.Max(0.8f, duration);
                _sleeping = true;
                _sleepUntil = Time.time + duration + UnityEngine.Random.Range(5f, 10f);
                _motionLockedUntil = Time.time + duration;
                _mode = "brachio_sleep_begin";
                return;
            }

            string motion = _standMotion;
            bool loop = true;
            if (roll <= 4 && !string.IsNullOrEmpty(_eatLowerMotion))
            {
                motion = _eatLowerMotion;
                loop = false;
            }
            else if (roll <= 8 && !string.IsNullOrEmpty(_eatMotion))
            {
                motion = _eatMotion;
                loop = false;
            }

            float played = 1.5f;
            if (!string.IsNullOrEmpty(motion))
            {
                played = _animal.CrossFade(motion, 0.15f, loop, 0f, 1f);
            }
            _mode = "brachio_idle_detail";
            _nextPassiveMotionAt = Time.time + Mathf.Max(1.5f, played) +
                UnityEngine.Random.Range(1.5f, 3.5f);
        }

        private void UpdateBrachioState(Vector3 playerPosition, float playerDistance)
        {
            if ((_mode == "brachio_damaged" || _mode == "groggy") &&
                Time.time < _motionLockedUntil)
            {
                return;
            }
            if (_mode == "brachio_run_stop" && Time.time < _motionLockedUntil)
            {
                return;
            }

            if (_brachioMode == BrachioMode.BattleBegin)
            {
                Face(playerPosition);
                if (Time.time >= _brachioStateUntil)
                {
                    SwitchBrachioChase("battle-begin-done");
                }
                return;
            }

            if (_brachioMode == BrachioMode.Chase)
            {
                Face(playerPosition);
                if (playerDistance <= BrachioAttackDistance)
                {
                    SwitchBrachioSkillWait("in-range");
                    return;
                }

                if (IsBrachioStuck())
                {
                    Debug.Log("[OfflineCombat] Brachio stuck detected. Disengaging.");
                    Disengage();
                    ReturnHome();
                    return;
                }

                ChaseBrachio(playerPosition, playerDistance);
                return;
            }

            if (_brachioMode == BrachioMode.SkillWait)
            {
                if (playerDistance > BrachioAttackDistance)
                {
                    SwitchBrachioChase("skill-wait-target-out-range");
                    return;
                }

                PlayBrachioBattleStand();
                if (Time.time >= _brachioAttackReadyAt)
                {
                    SwitchBrachioAttack(playerPosition, "skill-wait-done");
                }
                return;
            }

            if (_brachioMode == BrachioMode.TailPrep)
            {
                Vector3 targetDirection = DirectionTo(playerPosition);
                if (targetDirection.sqrMagnitude > 0.001f)
                {
                    _brachioAreaDirection = targetDirection;
                    _brachioAttackDirection = -targetDirection;
                }
                TurnToDirection(_brachioAttackDirection);
                PlayBrachioBattleStand();
                if (Time.time >= _brachioTailPrepEndAt)
                {
                    BeginBrachioAttack(
                        "Brachio_Attack_Tail",
                        BrachioAttackProfiles.TailAttackId,
                        BrachioTailAttackRange,
                        75f,
                        290f,
                        1.05f,
                        DamageDirection.Front,
                        DamageEffects.Blow,
                        true,
                        false,
                        BrachioTailAttackHitBeforeEnd);
                }
                return;
            }

            if (_brachioMode == BrachioMode.WoundedTailPrep)
            {
                Vector3 targetDirection = DirectionTo(playerPosition);
                if (targetDirection.sqrMagnitude > 0.001f)
                {
                    _brachioAreaDirection = targetDirection;
                    _brachioAttackDirection = -targetDirection;
                }
                TurnToDirection(_brachioAttackDirection);
                PlayBrachioLoop(_walkMotion, "brachio_wounded_tail_backstep");
                MoveBrachio(_brachioAttackDirection, BrachioRunSpeed);
                if (Time.time >= _brachioWoundedTailPrepEndAt)
                {
                    BeginBrachioAttack(
                        "Brachio_Attack_WoundedTail",
                        BrachioAttackProfiles.WoundedTailAttackId,
                        BrachioTailAttackRange,
                        120f,
                        240f,
                        1.18f,
                        DamageDirection.Back,
                        DamageEffects.Blow,
                        true,
                        true,
                        BrachioWoundedTailAttackHitBeforeEnd);
                }
                return;
            }

            if (_brachioMode == BrachioMode.Attack)
            {
                TurnToDirection(_brachioAttackDirection);
                if (Time.time >= _motionLockedUntil)
                {
                    SwitchBrachioChase("attack-done");
                }
                return;
            }

            SwitchBrachioChase("battle-state-fallback");
        }

        private void UpdateBrachioIncapacitated()
        {
            float now = Time.time;
            if (!_incapacitateEnding)
            {
                if (now >= _incapacitateUntil)
                {
                    _incapacitateEnding = true;
                    OfflineCombatAnimalStatusIcons.ClearBrachioIncapacitated(_animal);
                    float endDuration = 0.8f;
                    if (!string.IsNullOrEmpty(_knockDownEndMotion))
                    {
                        endDuration = Mathf.Max(
                            0.35f,
                            _animal.CrossFade(
                                _knockDownEndMotion, 0.08f, false, 0f, 1f));
                    }
                    _incapacitateEndAt = now + endDuration;
                    _motionLockedUntil = _incapacitateEndAt;
                    _mode = "brachio_incapacitate_end";
                    return;
                }

                if (now >= _incapacitateLoopAt &&
                    _mode != "brachio_incapacitate_loop")
                {
                    if (!string.IsNullOrEmpty(_knockDownLoopMotion))
                    {
                        _animal.CrossFade(
                            _knockDownLoopMotion, 0.08f, true, 0f, 1f);
                    }
                    _mode = "brachio_incapacitate_loop";
                }
                return;
            }

            if (now < _incapacitateEndAt)
            {
                return;
            }

            _incapacitateUntil = 0f;
            _incapacitateEndAt = 0f;
            _incapacitateEnding = false;
            _motionLockedUntil = 0f;
            if (_aggressive)
            {
                SwitchBrachioChase("incapacitate-recovered");
            }
            else
            {
                SwitchBrachioIdle("incapacitate-recovered");
            }
        }

        private void SwitchBrachioIdle(string reason)
        {
            _brachioMode = BrachioMode.Idle;
            _brachioStateUntil = Time.time + UnityEngine.Random.Range(
                BrachioIdleMinSeconds,
                BrachioIdleMaxSeconds);
            _sleeping = false;
            OfflineCombatAnimalStatusIcons.SetBrachioPreview(_animal);
            PlayBrachioLoop(_standMotion, "brachio_idle");
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> Idle entity=" + _animal.EntityId + " reason=" + reason);
        }

        private void SwitchBrachioRoaming(string reason)
        {
            _brachioMode = BrachioMode.Roaming;
            _brachioStateUntil = Time.time + UnityEngine.Random.Range(
                BrachioRoamMinSeconds,
                BrachioRoamMaxSeconds);
            float yaw = UnityEngine.Random.Range(0f, 360f);
            _brachioRoamDirection = new Vector3(
                Mathf.Sin(yaw * Mathf.Deg2Rad),
                0f,
                Mathf.Cos(yaw * Mathf.Deg2Rad)).normalized;
            PlayBrachioLoop(_walkMotion, "brachio_roam");
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> Roaming entity=" + _animal.EntityId + " reason=" + reason);
        }

        private void SwitchBrachioBattleBegin(string reason)
        {
            _brachioMode = BrachioMode.BattleBegin;
            _sleeping = false;
            _brachioStateUntil = Time.time + 0.7f;
            _brachioAttackReadyAt = Time.time;
            if (!string.IsNullOrEmpty(_activeMotion))
            {
                _animal.CrossFade(_activeMotion, 0.08f, false, 0f, 1f);
            }
            _mode = "brachio_active";
            OfflineCombatAnimalStatusIcons.SetBrachioBattle(_animal, "Battle Begin", 3f);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> BattleBegin entity=" + _animal.EntityId + " reason=" + reason);
        }

        private void SwitchBrachioChase(string reason)
        {
            StopBrachioAttackMovement();
            _brachioMode = BrachioMode.Chase;
            _brachioChaseStartedAt = Time.time;
            OfflineCombatAnimalStatusIcons.SetBrachioBattle(_animal, "Chasing", 6f);
            PlayBrachioLoop(_runMotion, "brachio_run");
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> Chase entity=" + _animal.EntityId + " reason=" + reason);
        }

        private void SwitchBrachioSkillWait(string reason)
        {
            _brachioMode = BrachioMode.SkillWait;
            if (PlayerBehavior.LocalPlayer != null)
            {
                TurnToDirection(DirectionTo(
                    PlayerBehavior.LocalPlayer.CurrentPosition));
            }
            OfflineCombatAnimalStatusIcons.SetBrachioBattle(
                _animal,
                Time.time >= _brachioAttackReadyAt ? "Attack Ready" : "Recovering",
                4f);
            PlayBrachioBattleStand();
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> SkillWait entity=" + _animal.EntityId +
                " readyIn=" + Mathf.Max(0f, _brachioAttackReadyAt - Time.time) +
                " reason=" + reason);
        }

        private void SwitchBrachioAttack(Vector3 playerPosition, string reason)
        {
            Vector3 targetDirection = DirectionTo(playerPosition);
            if (targetDirection.sqrMagnitude <= 0.001f)
            {
                targetDirection = _animal.transform.forward;
            }

            float roll = UnityEngine.Random.value;
            if (roll <= BrachioWoundedTailAttackChance)
            {
                SwitchBrachioWoundedTailPrep(targetDirection, reason);
                return;
            }

            if (roll <= BrachioWoundedTailAttackChance + BrachioTailAttackChance)
            {
                SwitchBrachioTailPrep(targetDirection, reason);
                return;
            }

            _brachioAreaDirection = targetDirection;
            _brachioAttackDirection = targetDirection;
            BeginBrachioAttack(
                "Brachio_Attack",
                BrachioAttackProfiles.FrontAttackId,
                BrachioFrontAttackRange,
                320f,
                40f,
                1.08f,
                DamageDirection.Front,
                DamageEffects.Blow,
                false,
                false,
                BrachioAttackHitBeforeEnd);
        }

        private void SwitchBrachioTailPrep(Vector3 targetDirection, string reason)
        {
            _brachioMode = BrachioMode.TailPrep;
            _brachioAttackReadyAt = Time.time + _attackCooldown;
            _brachioTailAttack = true;
            _brachioWoundedTailAttack = false;
            _brachioAreaDirection = targetDirection;
            _brachioAttackDirection = targetDirection.sqrMagnitude <= 0.001f
                ? -_animal.transform.forward
                : -targetDirection;
            _brachioTailPrepEndAt = Time.time + BrachioTailAttackPrepSeconds;
            TurnToDirection(_brachioAttackDirection);
            OfflineCombatAnimalStatusIcons.SetBrachioTailPrep(_animal, false, 3f);
            PlayBrachioBattleStand();
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> TailPrep entity=" + _animal.EntityId + " reason=" + reason);
        }

        private void SwitchBrachioWoundedTailPrep(Vector3 targetDirection, string reason)
        {
            _brachioMode = BrachioMode.WoundedTailPrep;
            _brachioAttackReadyAt = Time.time + _attackCooldown;
            _brachioTailAttack = true;
            _brachioWoundedTailAttack = true;
            _brachioAreaDirection = targetDirection;
            _brachioAttackDirection = targetDirection.sqrMagnitude <= 0.001f
                ? -_animal.transform.forward
                : -targetDirection;
            _brachioWoundedTailPrepEndAt = Time.time + BrachioWoundedTailPrepSeconds;
            TurnToDirection(_brachioAttackDirection);
            OfflineCombatAnimalStatusIcons.SetBrachioTailPrep(_animal, true, 4f);
            PlayBrachioLoop(_walkMotion, "brachio_wounded_tail_backstep");
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> WoundedTailPrep entity=" + _animal.EntityId + " reason=" + reason);
        }

        private void BeginBrachioAttack(
            string motion,
            string attackId,
            float range,
            float arcStart,
            float arcEnd,
            float damageScale,
            DamageDirection direction,
            DamageEffects effects,
            bool tailAttack,
            bool woundedTailAttack,
            float hitBeforeEnd)
        {
            _brachioMode = BrachioMode.Attack;
            _brachioTailAttack = tailAttack;
            _brachioWoundedTailAttack = woundedTailAttack;
            _brachioCurrentAttackHitBeforeEnd = hitBeforeEnd;
            TurnToDirection(_brachioAttackDirection);

            float clipDuration = 0f;
            if (!string.IsNullOrEmpty(motion))
            {
                if (!tailAttack)
                {
                    // Match SaurusAICore: leave the AnimalBehavior transform at
                    // the locked attack origin while allowing the attack clip's
                    // root bone to move only the visible mesh forward.
                    _animal.ResetRootMotionOffset();
                    clipDuration = _animal.CrossFade(
                        motion, 0.1f, false, 0f, 1f);
                }
                else
                {
                    clipDuration = _animal.CrossFade(
                        motion, 0.1f, false, 0f, 1f);
                }
            }

            // SaurusAICorePlugin intentionally uses a fixed 4.5-second action
            // window. CrossFade's clip length must not move the damage frame.
            float duration = BrachioAttackEndDelay;
            // Keep the animal's world position fixed throughout the front attack.
            // The animation still plays, but neither root motion nor a transform
            // tween is allowed to advance Brachio toward the player.
            _pendingAttackRange = range;
            _pendingAttackArcStart = arcStart;
            _pendingAttackArcEnd = arcEnd;
            _pendingAttackDamageScale = damageScale;
            _pendingAttackDirection = direction;
            _pendingAttackEffects = effects;
            _pendingAttackId = attackId;
            _damageAt = Time.time + Mathf.Max(
                0f,
                BrachioAttackEndDelay - _brachioCurrentAttackHitBeforeEnd);
            _damagePending = true;
            float attackStartedAt = Time.time;
            _motionLockedUntil = attackStartedAt + BrachioAttackEndDelay;
            _nextAttackAt = attackStartedAt + _attackCooldown;
            _brachioAttackReadyAt = _nextAttackAt;
            _mode = "attack:" + attackId;
            PlayBrachioAttackNotice();
            OfflineCombatAnimalStatusIcons.SetBrachioAttack(
                _animal,
                woundedTailAttack,
                tailAttack,
                Mathf.Max(1f, duration));
            AnimalAttackArea attackArea = AnimalAttackGeometry.Create(
                _animal,
                range,
                arcStart,
                arcEnd,
                attackId,
                _brachioAreaDirection);
            _pendingAttackArea = attackArea;
            AnimalAttackAreaLineRenderer.Show(
                _animal,
                attackArea,
                Mathf.Max(1f, duration));
            OfflineCombatBackendPlugin.Log.LogInfo(
                "BrachioAI -> Attack entity=" + _animal.EntityId +
                " attack=" + attackId +
                " range=" + range +
                " arc=" + arcStart + ".." + arcEnd +
                " hitIn=" + (_damageAt - Time.time) +
                " duration=" + duration +
                " clipDuration=" + clipDuration +
                " advance=" + (!tailAttack
                    ? BrachioAttackProfiles.FrontAdvanceDistance
                    : 0f));
        }

        private void StopBrachioAttackMovement()
        {
            if (_brachioAttackTween != null)
            {
                _brachioAttackTween.enabled = false;
                _brachioAttackTween = null;
            }

            if (_isBrachio && _animal != null)
            {
                _animal.ResetRootMotionOffset();
            }
        }

        private void PlayBrachioAttackNotice()
        {
            try
            {
                if (_animal != null && Connections.Frontend != null)
                {
                    _animal.AttackNotice(Connections.Frontend.GetBufferedServerTime() + 2.0);
                }
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio attack notice failed: " + exception.Message);
            }
        }

        private void ChaseBrachio(Vector3 targetPosition, float distance)
        {
            Face(targetPosition);

            bool run = distance > BrachioEngageDistance;
            string motion = run ? _runMotion : _walkMotion;
            float speed = run ? BrachioRunSpeed : BrachioWalkSpeed;
            string mode = run ? "brachio_run" : "brachio_walk";
            if (!run && IsBrachioLowLife() && !string.IsNullOrEmpty(_limpMotion))
            {
                motion = _limpMotion;
                speed = BrachioWalkSpeed * 0.75f;
                mode = "brachio_limp";
            }

            if (_mode != mode && !string.IsNullOrEmpty(motion))
            {
                _animal.CrossFade(motion, 0.14f, true, 0f, 1f);
                _mode = mode;
            }

            Vector3 direction = DirectionTo(targetPosition);
            MoveBrachio(direction, speed);
        }

        private bool IsBrachioStuck()
        {
            if (_brachioLastPosition == Vector3.zero)
            {
                _brachioLastPosition = _animal.CurrentPosition;
                return false;
            }

            float distance = Vector3.Distance(_brachioLastPosition, _animal.CurrentPosition);
            if (distance > 20f) // StuckMoveDistance
            {
                _brachioLastPosition = _animal.CurrentPosition;
                _brachioStuckStartedAt = 0f;
                return false;
            }

            if (_brachioStuckStartedAt == 0f)
            {
                _brachioStuckStartedAt = Time.time;
                return false;
            }

            return Time.time - _brachioStuckStartedAt >= 2f; // StuckTimeoutSeconds
        }

        private void MoveBrachio(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector3 next = _animal.CurrentPosition +
                direction.normalized * speed * Time.deltaTime;
            next.y = _animal.ProcessWaterDepth(next);
            _animal.CurrentPosition = next;
            _animal.transform.position = next;
        }

        private void Face(Vector3 targetPosition)
        {
            TurnToDirection(DirectionTo(targetPosition));
        }

        private void TurnToDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float currentYaw = _animal.transform.rotation.eulerAngles.y;
            if (_isBrachio &&
                Mathf.Abs(Mathf.DeltaAngle(currentYaw, yaw)) > 70f &&
                !string.IsNullOrEmpty(_turnMotion) &&
                _mode != "brachio_turn" &&
                (_brachioMode == BrachioMode.Idle || _brachioMode == BrachioMode.BattleBegin || _brachioMode == BrachioMode.SkillWait))
            {
                _animal.CrossFade(_turnMotion, 0.08f, true, 0f, 1f);
                _mode = "brachio_turn";
            }
            _animal.TurnToYaw(yaw, false);
        }

        private Vector3 DirectionTo(Vector3 targetPosition)
        {
            Vector3 delta = targetPosition - _animal.CurrentPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= 0.001f ? Vector3.zero : delta.normalized;
        }

        private void PlayBrachioLoop(string motion, string mode)
        {
            if (_mode == mode)
            {
                return;
            }
            if (!string.IsNullOrEmpty(motion))
            {
                _animal.CrossFade(motion, 0.1f, true, 0f, 1f);
            }
            _mode = mode;
        }

        private bool IsBrachioLowLife()
        {
            return _animal != null &&
                _animal.Life != null &&
                _animal.Life.Max() > 0f &&
                _animal.Life.Get() <= _animal.Life.Max() * 0.45f;
        }

        private float BrachioAngleTo(Vector3 targetPosition)
        {
            Vector3 delta = targetPosition - _animal.CurrentPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f)
            {
                return 0f;
            }
            delta.Normalize();
            Vector3 forward = _animal.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            Vector3 right = _animal.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            float angle = Vector3.Angle(forward, delta);
            if (Vector3.Dot(right, delta) < 0f)
            {
                angle = 360f - angle;
            }
            return NormalizeAngle(angle);
        }

        private static bool IsAngleInArc(float angle, float arcStart, float arcEnd)
        {
            angle = NormalizeAngle(angle);
            arcStart = NormalizeAngle(arcStart);
            arcEnd = NormalizeAngle(arcEnd);
            if (Mathf.Abs(arcStart - arcEnd) < 0.01f)
            {
                return true;
            }
            if (arcStart <= arcEnd)
            {
                return angle >= arcStart && angle <= arcEnd;
            }
            return angle >= arcStart || angle <= arcEnd;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle < 0f)
            {
                angle += 360f;
            }
            while (angle >= 360f)
            {
                angle -= 360f;
            }
            return angle;
        }

        private string GetBrachioDamageMotion(DamageDirection direction)
        {
            if (!_isBrachio)
            {
                return _damagedMotion;
            }

            switch (direction)
            {
                case DamageDirection.Front:
                    return BrachioDamageMotionOrDefault(_damageFrontMotion);
                case DamageDirection.Back:
                    return BrachioDamageMotionOrDefault(_damageBackMotion);
                case DamageDirection.Left:
                    return BrachioDamageMotionOrDefault(_damageLeftMotion);
                case DamageDirection.Right:
                    return BrachioDamageMotionOrDefault(_damageRightMotion);
            }

            if (PlayerBehavior.LocalPlayer == null)
            {
                return _damagedMotion;
            }
            float angle = BrachioAngleTo(PlayerBehavior.LocalPlayer.CurrentPosition);
            if (angle <= 45f || angle >= 315f)
            {
                return BrachioDamageMotionOrDefault(_damageFrontMotion);
            }
            if (angle >= 135f && angle <= 225f)
            {
                return BrachioDamageMotionOrDefault(_damageBackMotion);
            }
            if (angle > 45f && angle < 135f)
            {
                return BrachioDamageMotionOrDefault(_damageRightMotion);
            }
            return BrachioDamageMotionOrDefault(_damageLeftMotion);
        }

        private string BrachioDamageMotionOrDefault(string motion)
        {
            return !string.IsNullOrEmpty(motion)
                ? motion
                : _damagedMotion;
        }

        private void PlayBrachioBattleStand()
        {
            if (_mode == "brachio_run" && !string.IsNullOrEmpty(_runStopMotion))
            {
                float duration = _animal.CrossFade(_runStopMotion, 0.08f, false, 0f, 1f);
                _motionLockedUntil = Time.time + Mathf.Min(Mathf.Max(0.25f, duration), 0.75f);
                _mode = "brachio_run_stop";
                return;
            }
            PlayBrachioLoop(
                !string.IsNullOrEmpty(_battleIdleMotion) ? _battleIdleMotion : _standMotion,
                "brachio_battle_stand");
        }

        private void StartAttack(Vector3 playerPosition)
        {
            float yaw = Maths.CalcYawWithTarget(playerPosition, _animal.CurrentPosition);
            _animal.TurnToYaw(yaw, false);
            float duration = 1f;
            if (!string.IsNullOrEmpty(_attackMotion))
            {
                duration = _animal.CrossFade(_attackMotion, 0.08f, false, 0f, 1f);
            }

            duration = Mathf.Max(0.6f, duration);
            _damageAt = Time.time + Mathf.Min(0.65f, duration * 0.55f);
            _damagePending = true;
            _motionLockedUntil = Time.time + duration;
            _nextAttackAt = Time.time + duration + _attackCooldown;
            _mode = "attack";
        }

        private void PlayStand()
        {
            if (_mode == "stand")
            {
                return;
            }
            if (!string.IsNullOrEmpty(_standMotion))
            {
                _animal.CrossFade(_standMotion, 0.1f, true, 0f, 1f);
            }
            _mode = "stand";
        }

        private void ResolveMotions()
        {
            if (_animal.AnimalFrameworkResource == null)
            {
                return;
            }

            _standMotion = GetSimpleMotion("battle_stand");
            if (string.IsNullOrEmpty(_standMotion))
            {
                _standMotion = GetSimpleMotion("stand");
            }

            _damagedMotion = GetSimpleMotion("blow");
            _groggyMotion = GetSimpleMotion("groggy");

            AnimationElem3State knockDown =
                _animal.AnimalFrameworkResource.GetAnimationElements(
                    "knock_down_motions") as AnimationElem3State;
            if (knockDown != null)
            {
                _knockDownBeginMotion = knockDown.begin;
                _knockDownLoopMotion = knockDown.during;
                _knockDownEndMotion = knockDown.end;
            }

            AnimationElemBase dead = _animal.AnimalFrameworkResource.GetAnimationElements("dead");
            AnimationSequenceClip deadClip;
            if (dead != null && dead.TryMoveNext(0, out deadClip))
            {
                _deadMotion = deadClip.Clip;
            }

            AnimationElemMoveSet moveSet = _animal.AnimalFrameworkResource.GetAnimationElements(
                "move_motion_sets") as AnimationElemMoveSet;
            if (moveSet != null && moveSet.elems != null && moveSet.elems.Count > 0)
            {
                MoveMotionInfo motion = moveSet.elems[0].GetMoveMotion(_moveSpeed);
                if (motion != null)
                {
                    _moveMotion = motion.motion;
                    if (motion.base_move_speed > 0f)
                    {
                        _moveSpeed = Mathf.Clamp(motion.base_move_speed, 250f, 700f);
                    }
                }
            }

            AnimationElemAttack attack = _animal.AnimalFrameworkResource.GetAnimationElements(
                "attack_normal") as AnimationElemAttack;
            if (attack == null)
            {
                attack = _animal.AnimalFrameworkResource.GetAnimationElements(
                    "attack_strong") as AnimationElemAttack;
            }
            if (attack != null && attack.meta != null)
            {
                _attackMotion = attack.meta.motion;
            }
        }

        private string GetSimpleMotion(string key)
        {
            AnimationElem element = _animal.AnimalFrameworkResource.GetAnimationElements(key) as AnimationElem;
            return element == null ? null : element.motion;
        }
    }

    [HarmonyPatch(typeof(AnimalManager), "OnAppearAnimal")]
    internal static class AttachLocalWildAnimalCombatAIPatch
    {
        private static void Postfix(AnimalBehavior animal)
        {
            LocalWildAnimalCombatAI.Attach(animal);
        }
    }
}
