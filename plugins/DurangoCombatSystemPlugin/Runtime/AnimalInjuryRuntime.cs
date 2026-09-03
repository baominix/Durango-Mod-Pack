using System;
using System.Collections.Generic;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Messages;
using Shared.Battle;
using Shared.StatusEffect;
using UnityEngine;
using Yaml;

namespace Baominix.DurangoOriginal.CombatSystem.Runtime
{
    internal sealed class AnimalPartDamageResult
    {
        internal string EntityId;
        internal BodyPart Part;
        internal float Remaining;
        internal float Maximum;
        internal bool IsTracked;
        internal bool Broke;
        internal Messages.StatusEffect[] ActiveStatusEffects =
            new Messages.StatusEffect[0];
        internal HashSet<string> ManagedStatusEffectIds =
            new HashSet<string>(StringComparer.Ordinal);
    }

    internal sealed class AnimalInjuryModifiers
    {
        internal float DamageBonus;
        internal float DodgePlus;
        internal float HitRatePlus;
        internal float LifePerSecond;

        internal bool IsEmpty
        {
            get
            {
                return Mathf.Abs(DamageBonus) <= 0.0001f &&
                    Mathf.Abs(DodgePlus) <= 0.0001f &&
                    Mathf.Abs(HitRatePlus) <= 0.0001f &&
                    Mathf.Abs(LifePerSecond) <= 0.0001f;
            }
        }
    }

    internal sealed class AnimalInjuryRuntime
    {
        private sealed class PartState
        {
            internal float Current;
            internal float Maximum;
            internal bool Broken;
        }

        private sealed class ActiveStatus
        {
            internal string Id;
            internal int Level;
            internal double Since;
        }

        private sealed class InjuryState
        {
            internal int ObjectInstanceId;
            internal AnimalCombatProfile Profile;
            internal readonly Dictionary<BodyPart, PartState> Parts =
                new Dictionary<BodyPart, PartState>();
            internal readonly List<ActiveStatus> ActiveStatuses =
                new List<ActiveStatus>();
            internal readonly HashSet<string> ActiveStatusKeys =
                new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> ManagedStatusEffectIds =
                new HashSet<string>(StringComparer.Ordinal);
            internal AnimalInjuryModifiers Modifiers =
                new AnimalInjuryModifiers();
        }

        private readonly Dictionary<string, InjuryState> _states =
            new Dictionary<string, InjuryState>(StringComparer.Ordinal);

        internal AnimalPartDamageResult ApplyDamage(
            AnimalCombatTarget target,
            BodyPart requestedPart,
            int damage,
            double now)
        {
            AnimalPartDamageResult result = new AnimalPartDamageResult();
            result.EntityId = target == null ? null : target.EntityId;
            result.Part = requestedPart;
            if (target == null || target.Animal == null ||
                target.Profile == null || damage <= 0)
            {
                return result;
            }

            InjuryState state = GetOrCreateState(target);
            result.ManagedStatusEffectIds = CopySet(
                state.ManagedStatusEffectIds);

            PartState part;
            AnimalBodyPartProfile partProfile;
            if (!TryResolvePart(
                state,
                requestedPart,
                out partProfile,
                out part))
            {
                result.ActiveStatusEffects = BuildStatusEffects(
                    target.EntityId,
                    state);
                return result;
            }

            result.Part = partProfile.Part;
            result.IsTracked = true;
            if (!part.Broken)
            {
                part.Current = Mathf.Max(0f, part.Current - damage);
                if (part.Current <= 0f)
                {
                    part.Broken = true;
                    result.Broke = true;
                    AddBreakStatuses(state, partProfile, now);
                    state.Modifiers = CalculateModifiers(
                        target.EntityId,
                        state);
                }
            }

            result.Remaining = part.Current;
            result.Maximum = part.Maximum;
            result.ActiveStatusEffects = BuildStatusEffects(
                target.EntityId,
                state);
            return result;
        }

        internal bool TryGetStatusSnapshot(
            string entityId,
            out Messages.StatusEffect[] active,
            out HashSet<string> managedIds)
        {
            active = new Messages.StatusEffect[0];
            managedIds = new HashSet<string>(StringComparer.Ordinal);
            InjuryState state;
            if (string.IsNullOrEmpty(entityId) ||
                !_states.TryGetValue(entityId, out state))
            {
                return false;
            }
            active = BuildStatusEffects(entityId, state);
            managedIds = CopySet(state.ManagedStatusEffectIds);
            return true;
        }

        internal AnimalInjuryModifiers GetModifiers(
            AnimalCombatTarget target)
        {
            if (target == null || target.Animal == null)
            {
                return new AnimalInjuryModifiers();
            }
            return GetModifiers(
                target.EntityId,
                target.Animal.gameObject.GetInstanceID());
        }

        internal AnimalInjuryModifiers GetModifiers(
            string entityId,
            int objectInstanceId)
        {
            InjuryState state;
            if (string.IsNullOrEmpty(entityId) ||
                !_states.TryGetValue(entityId, out state) ||
                state.ObjectInstanceId != objectInstanceId)
            {
                return new AnimalInjuryModifiers();
            }
            return CopyModifiers(state.Modifiers);
        }

        internal List<string> GetDegeneratingEntityIds()
        {
            List<string> result = new List<string>();
            foreach (KeyValuePair<string, InjuryState> pair in _states)
            {
                if (pair.Value.Modifiers.LifePerSecond < -0.0001f)
                {
                    result.Add(pair.Key);
                }
            }
            return result;
        }

        internal void Remove(string entityId)
        {
            if (!string.IsNullOrEmpty(entityId))
            {
                _states.Remove(entityId);
            }
        }

        internal void Clear()
        {
            _states.Clear();
        }

        private InjuryState GetOrCreateState(AnimalCombatTarget target)
        {
            InjuryState state;
            int objectInstanceId =
                target.Animal.gameObject.GetInstanceID();
            if (_states.TryGetValue(target.EntityId, out state) &&
                state.ObjectInstanceId == objectInstanceId)
            {
                return state;
            }

            state = new InjuryState();
            state.ObjectInstanceId = objectInstanceId;
            state.Profile = target.Profile;
            if (target.Profile.BodyParts != null)
            {
                foreach (KeyValuePair<BodyPart, AnimalBodyPartProfile> pair
                    in target.Profile.BodyParts)
                {
                    AnimalBodyPartProfile profile = pair.Value;
                    if (profile == null || profile.HpRatio <= 0f)
                    {
                        continue;
                    }
                    float maximum = Mathf.Max(
                        1f,
                        target.MaximumLife * profile.HpRatio);
                    state.Parts[pair.Key] = new PartState
                    {
                        Current = maximum,
                        Maximum = maximum
                    };
                    int i;
                    for (i = 0; i < profile.StatusEffects.Count; i++)
                    {
                        AnimalBreakStatus status =
                            profile.StatusEffects[i];
                        if (status != null &&
                            !string.IsNullOrEmpty(status.Id))
                        {
                            state.ManagedStatusEffectIds.Add(status.Id);
                        }
                    }
                }
            }
            _states[target.EntityId] = state;
            return state;
        }

        private static bool TryResolvePart(
            InjuryState state,
            BodyPart requested,
            out AnimalBodyPartProfile profile,
            out PartState part)
        {
            profile = null;
            part = null;
            if (state == null || state.Profile == null ||
                state.Profile.BodyParts == null)
            {
                return false;
            }

            if (state.Profile.BodyParts.TryGetValue(
                    requested,
                    out profile) &&
                state.Parts.TryGetValue(requested, out part))
            {
                return true;
            }

            // A malformed or older hit packet must not create a synthetic
            // body part.  Body is the only source-backed fallback used by the
            // original client protocol and by PlayerHitResolver.
            if (state.Profile.BodyParts.TryGetValue(
                    BodyPart.Body,
                    out profile) &&
                state.Parts.TryGetValue(BodyPart.Body, out part))
            {
                return true;
            }
            profile = null;
            part = null;
            return false;
        }

        private static void AddBreakStatuses(
            InjuryState state,
            AnimalBodyPartProfile part,
            double now)
        {
            int i;
            for (i = 0; i < part.StatusEffects.Count; i++)
            {
                AnimalBreakStatus source = part.StatusEffects[i];
                if (source == null || string.IsNullOrEmpty(source.Id))
                {
                    continue;
                }
                string key = source.Id + ":" + source.Level;
                if (!state.ActiveStatusKeys.Add(key))
                {
                    continue;
                }
                state.ActiveStatuses.Add(new ActiveStatus
                {
                    Id = source.Id,
                    Level = Math.Max(1, source.Level),
                    Since = now
                });
            }
        }

        private static Messages.StatusEffect[] BuildStatusEffects(
            string entityId,
            InjuryState state)
        {
            List<Messages.StatusEffect> result =
                new List<Messages.StatusEffect>();
            int i;
            for (i = 0; i < state.ActiveStatuses.Count; i++)
            {
                ActiveStatus active = state.ActiveStatuses[i];
                StatusEffectTemplate template =
                    StatusEffectTemplateYaml.GetStatusEffectTemplate(
                        active.Id,
                        active.Level);
                if (template == null)
                {
                    DurangoCombatSystemPlugin.Log.LogWarning(
                        "Could not create animal injury status because " +
                        "the original template is unavailable: " +
                        active.Id + " level=" + active.Level + ".");
                    continue;
                }

                List<Messages.EffectDetail> details =
                    new List<Messages.EffectDetail>();
                if (template.Effects != null)
                {
                    int j;
                    for (j = 0; j < template.Effects.Length; j++)
                    {
                        Yaml.EffectDetail source = template.Effects[j];
                        if (source == null)
                        {
                            continue;
                        }
                        details.Add(new Messages.EffectDetail
                        {
                            Type = source.Type,
                            Key = source.Key,
                            Value = source.GetValue(active.Level)
                        });
                    }
                }

                Messages.StatusEffect message =
                    default(Messages.StatusEffect);
                message.Id = entityId + ":injury:" + active.Id + ":" +
                    active.Level;
                message.EffectId = active.Id;
                message.Level = active.Level;
                message.Since = active.Since;
                message.Until = 0.0;
                message.Stacked = 1;
                message.DurationHidden = true;
                message.NameGettext = null;
                message.Effects = details.ToArray();
                message.DailyContents = null;
                result.Add(message);
            }
            return result.ToArray();
        }

        private static AnimalInjuryModifiers CalculateModifiers(
            string entityId,
            InjuryState state)
        {
            AnimalInjuryModifiers result =
                new AnimalInjuryModifiers();
            Messages.StatusEffect[] statuses = BuildStatusEffects(
                entityId,
                state);
            int i;
            for (i = 0; i < statuses.Length; i++)
            {
                Messages.EffectDetail[] details = statuses[i].Effects;
                if (details == null)
                {
                    continue;
                }
                int j;
                for (j = 0; j < details.Length; j++)
                {
                    Messages.EffectDetail detail = details[j];
                    if (detail.Type == EffectType.Survival &&
                        string.Equals(
                            detail.Key,
                            "life",
                            StringComparison.Ordinal))
                    {
                        result.LifePerSecond += detail.Value;
                    }
                    else if (detail.Type == EffectType.Modifier)
                    {
                        if (string.Equals(
                            detail.Key,
                            "damage_bonus",
                            StringComparison.Ordinal))
                        {
                            result.DamageBonus += detail.Value;
                        }
                        else if (string.Equals(
                            detail.Key,
                            "dodge_plus",
                            StringComparison.Ordinal))
                        {
                            result.DodgePlus += detail.Value;
                        }
                        else if (string.Equals(
                            detail.Key,
                            "hit_rate_plus",
                            StringComparison.Ordinal))
                        {
                            result.HitRatePlus += detail.Value;
                        }
                    }
                }
            }
            return result;
        }

        private static AnimalInjuryModifiers CopyModifiers(
            AnimalInjuryModifiers source)
        {
            if (source == null)
            {
                return new AnimalInjuryModifiers();
            }
            return new AnimalInjuryModifiers
            {
                DamageBonus = source.DamageBonus,
                DodgePlus = source.DodgePlus,
                HitRatePlus = source.HitRatePlus,
                LifePerSecond = source.LifePerSecond
            };
        }

        private static HashSet<string> CopySet(HashSet<string> source)
        {
            HashSet<string> result =
                new HashSet<string>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (string value in source)
                {
                    result.Add(value);
                }
            }
            return result;
        }
    }
}
