using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Durango.Logic;
using Durango.Network;
using Durango.UI;
using HarmonyLib;
using Messages;
using UnityEngine;
using Yaml;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal static class OfflineCombatAnimalStatusIcons
    {
        private const string Prefix = "offline_brachio_";
        private static readonly bool SyntheticStatusIconsEnabled = false;
        private static readonly BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo MakeOrGetMethod =
            typeof(StatusEffectSystem).GetMethod("MakeOrGet", Flags);
        private static readonly FieldInfo TargetStatusEntityField =
            typeof(StatusEffectsControl).GetField("_target", Flags);
        private static readonly FieldInfo TargetStatusTypeField =
            typeof(StatusEffectsControl).GetField("_statusType", Flags);
        private static readonly MethodInfo RefreshTargetStatusMethod =
            typeof(StatusEffectsControl).GetMethod(
                "RefreshStatusEffect",
                Flags,
                null,
                new Type[] { typeof(bool) },
                null);
        private static readonly Dictionary<string, double> BlowUntil =
            new Dictionary<string, double>();
        private static readonly Dictionary<string, double> GroggyUntil =
            new Dictionary<string, double>();
        private static readonly Dictionary<string, double> IncapacitatedUntil =
            new Dictionary<string, double>();

        internal static void SetBrachioBattle(
            AnimalBehavior animal,
            string state,
            float seconds)
        {
            if (!SyntheticStatusIconsEnabled)
            {
                return;
            }

            SetBrachioStatusSet(
                animal,
                CreateBattleSpecs(
                    "Brachio: " + state,
                    "The Brachiosaurus is locked onto its local combat target.",
                    "icon_se_excited",
                    "negative",
                    false,
                    seconds));
            ApplyTargetMarker(animal, Mathf.Max(2f, seconds));
            ApplyTension(animal, new Color(1f, 0.45f, 0.25f, 1f));
        }

        internal static void SetBrachioPreview(AnimalBehavior animal)
        {
            if (!SyntheticStatusIconsEnabled)
            {
                RefreshDynamicCombatStatuses(animal);
                return;
            }

            SetBrachioStatusSet(
                animal,
                CreateBattleSpecs(
                    "Brachio: Alert",
                    "The alert-state status slot.",
                    "icon_se_excited",
                    "negative",
                    false,
                    0f));
        }

        internal static void SetBrachioTailPrep(
            AnimalBehavior animal,
            bool woundedTail,
            float seconds)
        {
            if (!SyntheticStatusIconsEnabled)
            {
                return;
            }

            StatusSpec[] specs = CreateBattleSpecs(
                woundedTail ? "Brachio: Wounded Tail" : "Brachio: Tail Sweep",
                woundedTail
                    ? "The animal is backing up and preparing a heavy wounded-tail sweep."
                    : "The animal is turning its tail toward the target.",
                "icon_se_tail_broken",
                "negative",
                true,
                seconds);
            ReplaceSpec(
                specs,
                "tail",
                animal,
                woundedTail ? "wounded_tail" : "tail",
                woundedTail ? "Brachio: Wounded Tail" : "Brachio: Tail Sweep",
                woundedTail
                    ? "The animal is backing up and preparing a heavy wounded-tail sweep."
                    : "The animal is turning its tail toward the target.",
                "icon_se_tail_broken",
                "negative",
                true,
                seconds);
            SetBrachioStatusSet(animal, specs);
            ApplyTargetMarker(animal, Mathf.Max(2f, seconds));
            ApplyTension(animal, woundedTail
                ? new Color(1f, 0.15f, 0.15f, 1f)
                : new Color(1f, 0.35f, 0.15f, 1f));
        }

        internal static void SetBrachioAttack(
            AnimalBehavior animal,
            bool woundedTail,
            bool tail,
            float seconds)
        {
            if (!SyntheticStatusIconsEnabled)
            {
                return;
            }

            StatusSpec[] specs = CreateBattleSpecs(
                woundedTail ? "Brachio: Wounded Tail Strike" :
                    (tail ? "Brachio: Tail Strike" : "Brachio: Stomp"),
                "Incoming Brachio attack. Dodge or move out of the hit area.",
                tail ? "icon_se_tail_broken" : "icon_se_attack",
                "negative",
                true,
                seconds);
            ReplaceSpec(
                specs,
                "attack",
                animal,
                woundedTail ? "wounded_tail_attack" :
                    (tail ? "tail_attack" : "stomp_attack"),
                woundedTail ? "Brachio: Wounded Tail Strike" :
                    (tail ? "Brachio: Tail Strike" : "Brachio: Stomp"),
                "Incoming Brachio attack. Dodge or move out of the hit area.",
                tail ? "icon_se_tail_broken" : "icon_se_attack",
                "negative",
                true,
                seconds);
            SetBrachioStatusSet(animal, specs);
            ApplyTargetMarker(animal, Mathf.Max(2f, seconds));
        }

        internal static void SetBrachioGroggy(AnimalBehavior animal, float seconds)
        {
            if (!SyntheticStatusIconsEnabled)
            {
                SetDynamicStatus(animal, GroggyUntil, seconds);
                return;
            }

            StatusSpec[] specs = CreateBattleSpecs(
                "Brachio: Groggy",
                "The animal is stunned and temporarily unable to attack.",
                "icon_se_mental_confusion",
                "negative",
                true,
                seconds);
            ReplaceSpec(
                specs,
                "alert",
                animal,
                "groggy",
                "Brachio: Groggy",
                "The animal is stunned and temporarily unable to attack.",
                "icon_se_mental_confusion",
                "negative",
                true,
                seconds);
            SetBrachioStatusSet(animal, specs);
            ApplyGroggyParticle(animal, true);
        }

        internal static void SetBrachioBlow(AnimalBehavior animal, float seconds)
        {
            if (SyntheticStatusIconsEnabled)
            {
                StatusSpec[] specs = CreateBattleSpecs(
                    "Brachio: Alert",
                    "The alert-state status slot.",
                    "icon_se_excited",
                    "negative",
                    false,
                    0f);
                ReplaceSpec(
                    specs,
                    "attack",
                    animal,
                    "blow",
                    "Blow",
                    "Blow resistance is broken. Successful hits can cause Blow.",
                    "icon_se_attack",
                    "negative",
                    true,
                    seconds);
                SetBrachioStatusSet(animal, specs);
                return;
            }

            SetDynamicStatus(animal, BlowUntil, seconds);
        }

        internal static void ClearBrachioBlow(AnimalBehavior animal)
        {
            if (SyntheticStatusIconsEnabled)
            {
                SetBrachioPreview(animal);
                return;
            }

            RemoveDynamicStatus(animal, BlowUntil);
        }

        internal static void ClearBrachioGroggy(AnimalBehavior animal)
        {
            RemoveDynamicStatus(animal, GroggyUntil);
        }

        internal static void SetBrachioIncapacitated(
            AnimalBehavior animal,
            float seconds)
        {
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            BlowUntil.Remove(animal.EntityId);
            GroggyUntil.Remove(animal.EntityId);
            IncapacitatedUntil[animal.EntityId] =
                Now() + Mathf.Max(1f, seconds);
            ApplyDynamicCombatStatuses(animal);
        }

        internal static void ClearBrachioIncapacitated(AnimalBehavior animal)
        {
            RemoveDynamicStatus(animal, IncapacitatedUntil);
        }

        internal static void Clear(AnimalBehavior animal)
        {
            if (animal == null)
            {
                return;
            }

            BlowUntil.Remove(animal.EntityId);
            GroggyUntil.Remove(animal.EntityId);
            IncapacitatedUntil.Remove(animal.EntityId);

            try
            {
                Durango.Logic.StatusEffects effects = GetOrCreate(animal.EntityId);
                if (effects == null)
                {
                    return;
                }

                List<Durango.Logic.StatusEffect> next =
                    CopyNonOfflineBrachioEffects(effects);
                effects.SetStatusEffects(next);
                ApplyGroggyParticle(animal, false);
                ClearTargetMarker(animal);
                animal.ApplyTensionColor(Color.gray);
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio status clear failed: " + exception.Message);
            }
        }

        private static StatusSpec[] CreateBattleSpecs(
            string alertName,
            string alertDescription,
            string alertIcon,
            string alertColor,
            bool alertTimed,
            float alertSeconds)
        {
            return new StatusSpec[]
            {
                new StatusSpec(
                    "alert",
                    alertName,
                    alertDescription,
                    alertIcon,
                    alertColor,
                    alertTimed,
                    alertSeconds),
                new StatusSpec(
                    "attack",
                    "Brachio: Heavy Body",
                    "Large-body attacks can blow the player away.",
                    "icon_se_attack",
                    "negative",
                    false,
                    0f),
                new StatusSpec(
                    "tail",
                    "Brachio: Tail Threat",
                    "The tail can sweep a wide area behind the animal.",
                    "icon_se_tail_broken",
                    "negative",
                    false,
                    0f),
                new StatusSpec(
                    "leg",
                    "Brachio: Slow Turning",
                    "A huge body that turns slowly but hits a broad area.",
                    "icon_se_leg_broken",
                    "negative",
                    false,
                    0f),
                new StatusSpec(
                    "defense",
                    "Brachio: Thick Hide",
                    "The animal is resistant to knockback and light hits.",
                    "icon_se_defense",
                    "negative",
                    false,
                    0f),
                new StatusSpec(
                    "life",
                    "Brachio: Vitality",
                    "High life and stamina while combat is active.",
                    "icon_se_life",
                    "negative",
                    false,
                    0f)
            };
        }

        private static void ReplaceSpec(
            StatusSpec[] specs,
            string oldSlot,
            AnimalBehavior animal,
            string id,
            string name,
            string description,
            string icon,
            string iconColor,
            bool timed,
            float seconds)
        {
            if (specs == null)
            {
                return;
            }

            for (int i = 0; i < specs.Length; i++)
            {
                if (specs[i].Slot == oldSlot)
                {
                    specs[i] = new StatusSpec(
                        id,
                        name,
                        description,
                        icon,
                        iconColor,
                        timed,
                        seconds);
                    return;
                }
            }
        }

        private static void SetBrachioStatusSet(
            AnimalBehavior animal,
            StatusSpec[] specs)
        {
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            try
            {
                Durango.Logic.StatusEffects effects = GetOrCreate(animal.EntityId);
                if (effects == null)
                {
                    return;
                }

                List<Durango.Logic.StatusEffect> next =
                    CopyNonOfflineBrachioEffects(effects);
                if (specs != null)
                {
                    for (int i = specs.Length - 1; i >= 0; i--)
                    {
                        StatusSpec spec = specs[i];
                        next.Add(CreateStatusEffect(
                            Prefix + spec.Slot,
                            spec.Name,
                            spec.Description,
                            spec.Icon,
                            spec.IconColor,
                            spec.Timed,
                            spec.Seconds));
                    }
                }
                effects.SetStatusEffects(next);
                ForceRefreshTargetStatus(animal.EntityId);
                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Brachio status icons applied entity=" + animal.EntityId +
                    " count=" + (specs == null ? 0 : specs.Length));
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio status set failed: " + exception.Message);
            }
        }

        private static Durango.Logic.StatusEffects GetOrCreate(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) ||
                !GameSystem<StatusEffectSystem>.HasInstance())
            {
                return null;
            }

            StatusEffectSystem system = GameSystem<StatusEffectSystem>.Instance();
            if (MakeOrGetMethod != null)
            {
                return MakeOrGetMethod.Invoke(system, new object[] { entityId }) as
                    Durango.Logic.StatusEffects;
            }
            return system.GetStatusEffects(entityId);
        }

        private static void ForceRefreshTargetStatus(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) ||
                TargetStatusEntityField == null ||
                TargetStatusTypeField == null ||
                RefreshTargetStatusMethod == null)
            {
                return;
            }

            int refreshed = 0;
            int activated = 0;
            UnityEngine.Object[] controls =
                Resources.FindObjectsOfTypeAll(typeof(StatusEffectsControl));
            for (int i = 0; i < controls.Length; i++)
            {
                StatusEffectsControl control = controls[i] as StatusEffectsControl;
                object statusType = control == null
                    ? null
                    : TargetStatusTypeField.GetValue(control);
                if (control == null || statusType == null ||
                    !string.Equals(
                        statusType.ToString(), "Target", StringComparison.Ordinal) ||
                    !control.gameObject.scene.IsValid())
                {
                    continue;
                }

                TargetStatusEntityField.SetValue(control, entityId);
                if (!control.gameObject.activeSelf)
                {
                    control.gameObject.SetActive(true);
                    activated++;
                }
                RefreshTargetStatusMethod.Invoke(control, new object[] { true });
                refreshed++;
            }

            OfflineCombatBackendPlugin.Log.LogInfo(
                "Brachio target status UI refreshed entity=" + entityId +
                " controls=" + refreshed +
                " activated=" + activated);
        }

        private static void SetTimedCombatStatus(
            AnimalBehavior animal,
            string slot,
            string name,
            string description,
            string icon,
            float seconds)
        {
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            try
            {
                Durango.Logic.StatusEffects effects = GetOrCreate(animal.EntityId);
                if (effects == null)
                {
                    return;
                }

                double now = Now();
                string id = Prefix + slot;
                List<Durango.Logic.StatusEffect> next =
                    CopyActiveCombatEffects(effects, id, now);
                next.Add(CreateStatusEffect(
                    id,
                    name,
                    description,
                    icon,
                    "negative",
                    true,
                    Mathf.Max(1f, seconds)));
                effects.SetStatusEffects(next);
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio combat status set failed: " + exception.Message);
            }
        }

        private static void RemoveCombatStatus(AnimalBehavior animal, string slot)
        {
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            try
            {
                Durango.Logic.StatusEffects effects = GetOrCreate(animal.EntityId);
                if (effects == null)
                {
                    return;
                }

                effects.SetStatusEffects(CopyActiveCombatEffects(
                    effects,
                    Prefix + slot,
                    Now()));
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio combat status remove failed: " + exception.Message);
            }
        }

        private static void RefreshDynamicCombatStatuses(AnimalBehavior animal)
        {
            ApplyDynamicCombatStatuses(animal);
        }

        private static void SetDynamicStatus(
            AnimalBehavior animal,
            Dictionary<string, double> statusUntil,
            float seconds)
        {
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            statusUntil[animal.EntityId] = Now() + Mathf.Max(1f, seconds);
            ApplyDynamicCombatStatuses(animal);
        }

        private static void RemoveDynamicStatus(
            AnimalBehavior animal,
            Dictionary<string, double> statusUntil)
        {
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            statusUntil.Remove(animal.EntityId);
            ApplyDynamicCombatStatuses(animal);
        }

        private static void ApplyDynamicCombatStatuses(AnimalBehavior animal)
        {
            if (animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            try
            {
                double now = Now();
                List<StatusSpec> specs = new List<StatusSpec>();
                double until;
                if (IncapacitatedUntil.TryGetValue(animal.EntityId, out until))
                {
                    if (until > now)
                    {
                        specs.Add(new StatusSpec(
                            "incapacitated",
                            "Brachio: Incapacitated",
                            "The animal has been knocked down and cannot move or attack.",
                            "icon_se_leg_broken",
                            "negative",
                            true,
                            Mathf.Max(1f, (float)(until - now))));
                    }
                    else
                    {
                        IncapacitatedUntil.Remove(animal.EntityId);
                    }
                }

                if (GroggyUntil.TryGetValue(animal.EntityId, out until))
                {
                    if (until > now)
                    {
                        specs.Add(new StatusSpec(
                            "groggy",
                            "Brachio: Groggy",
                            "The animal is stunned and temporarily unable to attack.",
                            "icon_se_mental_confusion",
                            "negative",
                            true,
                            Mathf.Max(1f, (float)(until - now))));
                    }
                    else
                    {
                        GroggyUntil.Remove(animal.EntityId);
                    }
                }

                if (BlowUntil.TryGetValue(animal.EntityId, out until))
                {
                    if (until > now)
                    {
                        specs.Add(new StatusSpec(
                            "blow",
                            "Blow",
                            "Blow resistance is broken. Successful hits can cause Blow.",
                            "icon_se_attack",
                            "negative",
                            true,
                            Mathf.Max(1f, (float)(until - now))));
                    }
                    else
                    {
                        BlowUntil.Remove(animal.EntityId);
                    }
                }

                SetBrachioStatusSet(animal, specs.ToArray());
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio dynamic status refresh failed: " + exception.Message);
            }
        }

        private static List<Durango.Logic.StatusEffect> CopyActiveCombatEffects(
            Durango.Logic.StatusEffects effects,
            string excludedId,
            double now)
        {
            List<Durango.Logic.StatusEffect> next =
                new List<Durango.Logic.StatusEffect>();
            if (effects == null || effects.List == null)
            {
                return next;
            }

            for (int i = 0; i < effects.List.Count; i++)
            {
                Durango.Logic.StatusEffect status = effects.List[i];
                if (status == null || string.Equals(
                    status.Id, excludedId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(status.Id) &&
                    status.Id.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    bool supported = string.Equals(
                        status.Id, Prefix + "blow", StringComparison.Ordinal) ||
                        string.Equals(
                            status.Id, Prefix + "groggy", StringComparison.Ordinal) ||
                        string.Equals(
                            status.Id,
                            Prefix + "incapacitated",
                            StringComparison.Ordinal);
                    if (!supported || (status.Until > 0.0 && status.Until <= now))
                    {
                        continue;
                    }
                }
                next.Add(status);
            }
            return next;
        }

        private static List<Durango.Logic.StatusEffect> CopyNonOfflineBrachioEffects(
            Durango.Logic.StatusEffects effects)
        {
            List<Durango.Logic.StatusEffect> next =
                new List<Durango.Logic.StatusEffect>();
            if (effects == null || effects.List == null)
            {
                return next;
            }

            for (int i = 0; i < effects.List.Count; i++)
            {
                Durango.Logic.StatusEffect status = effects.List[i];
                if (status == null ||
                    (!string.IsNullOrEmpty(status.Id) &&
                     status.Id.StartsWith(Prefix, StringComparison.Ordinal)))
                {
                    continue;
                }
                next.Add(status);
            }
            return next;
        }

        private static Durango.Logic.StatusEffect CreateStatusEffect(
            string id,
            string name,
            string description,
            string icon,
            string iconColor,
            bool timed,
            float seconds)
        {
            double now = Now();
            float duration = Mathf.Max(1f, seconds);
            StatusEffectTemplate template = new StatusEffectTemplate();
            template.MinLevel = 1;
            template.MaxLevel = 1;
            template.Name = name;
            template.Description = description;
            template.Icon = icon;
            template.IconColor = iconColor;
            template.UIGroup = string.Empty;
            template.Duration = timed
                ? duration.ToString(CultureInfo.InvariantCulture)
                : string.Empty;

            Messages.StatusEffect message = default(Messages.StatusEffect);
            message.Id = id;
            message.EffectId = id;
            message.Level = 1;
            message.Since = now;
            message.Until = timed ? now + duration : 0.0;
            message.Stacked = 1;
            message.DurationHidden = !timed;
            message.NameGettext = name;
            message.Effects = new Messages.EffectDetail[0];
            message.DailyContents = null;
            return new Durango.Logic.StatusEffect(message, template);
        }

        private static void ApplyTargetMarker(AnimalBehavior animal, float seconds)
        {
            if (animal == null || animal.Life == null)
            {
                return;
            }

            try
            {
                double now = Now();
                Dictionary<string, Gauge> gauges = new Dictionary<string, Gauge>();
                gauges["target_marker"] = new Gauge(1f, 0f, new GaugeNode[]
                {
                    new GaugeNode { Time = now, Value = 1f },
                    new GaugeNode { Time = now + Mathf.Max(1f, seconds), Value = 0f }
                });
                animal.SetSurvivalGauge(animal.Life, gauges);
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio target marker failed: " + exception.Message);
            }
        }

        private static void ClearTargetMarker(AnimalBehavior animal)
        {
            if (animal == null || animal.Life == null)
            {
                return;
            }

            try
            {
                double now = Now();
                Dictionary<string, Gauge> gauges = new Dictionary<string, Gauge>();
                gauges["target_marker"] = new Gauge(1f, 0f, new GaugeNode[]
                {
                    new GaugeNode { Time = now, Value = 0f }
                });
                animal.SetSurvivalGauge(animal.Life, gauges);
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio target marker clear failed: " + exception.Message);
            }
        }

        private static void ApplyGroggyParticle(AnimalBehavior animal, bool active)
        {
            if (animal == null || animal.Life == null)
            {
                return;
            }

            try
            {
                double now = Now();
                Dictionary<string, Gauge> gauges = new Dictionary<string, Gauge>();
                gauges["groggy"] = new Gauge(1f, 0f, new GaugeNode[]
                {
                    new GaugeNode { Time = now, Value = active ? 0f : 1f }
                });
                animal.SetSurvivalGauge(animal.Life, gauges);
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogWarning(
                    "Brachio groggy marker failed: " + exception.Message);
            }
        }

        private static void ApplyTension(AnimalBehavior animal, Color color)
        {
            try
            {
                if (animal != null)
                {
                    animal.ApplyTensionColor(color);
                }
            }
            catch
            {
            }
        }

        private static double Now()
        {
            return Connections.Frontend == null
                ? Time.unscaledTime
                : Connections.Frontend.GetPredictedServerTime();
        }

        private struct StatusSpec
        {
            public readonly string Slot;
            public readonly string Name;
            public readonly string Description;
            public readonly string Icon;
            public readonly string IconColor;
            public readonly bool Timed;
            public readonly float Seconds;

            public StatusSpec(
                string slot,
                string name,
                string description,
                string icon,
                string iconColor,
                bool timed,
                float seconds)
            {
                Slot = slot;
                Name = name;
                Description = description;
                Icon = icon;
                IconColor = iconColor;
                Timed = timed;
                Seconds = seconds;
            }
        }
    }

    [HarmonyPatch(typeof(StatusEffectsControl), "OnTargetChanged")]
    internal static class RefreshBrachioStatusIconsOnTargetChangedPatch
    {
        private static void Postfix(DamageableEntity target)
        {
            AnimalBehavior animal = target == null
                ? null
                : Durango.Utils.Singleton<AnimalManager>.Instance().GetAnimal(
                    target.GetEntityId());
            if (animal == null ||
                animal.EntityTypeId != 2004 ||
                !OfflineCombatAnimalTargets.IsCombatAnimal(animal))
            {
                return;
            }

            OfflineCombatAnimalStatusIcons.SetBrachioPreview(animal);
        }
    }
}
