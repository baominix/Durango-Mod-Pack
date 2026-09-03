using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.UI;
using Durango.UI.Control;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.Presentation
{
    internal static class SaurusDebugBubble
    {
        private const double RefreshSeconds = 0.50;

        private sealed class BubbleState
        {
            internal double NextRefreshAt;
            internal ChatBubbleGroup Group;
            internal ChatBubble Bubble;
            internal string LastText;
        }

        private static readonly Dictionary<string, BubbleState> States =
            new Dictionary<string, BubbleState>(
                StringComparer.Ordinal);
        private static readonly MethodInfo GetBubbleMethod =
            typeof(ChatBubbleGroup).GetMethod(
                "Get",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(bool) },
                null);
        private static readonly MethodInfo UpdateLayoutMethod =
            typeof(ChatBubble).GetMethod(
                "UpdateLayout",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MainWidgetField =
            typeof(ChatBubble).GetField(
                "_mainWidget",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static double _nextFailureLogAt;

        internal static bool Enabled { get; private set; }

        internal static void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
            {
                return;
            }
            Enabled = enabled;
            if (!enabled)
            {
                HideAll();
            }
        }

        internal static void Publish(
            AnimalBehavior animal,
            string text,
            double now)
        {
            if (!Enabled || animal == null ||
                string.IsNullOrEmpty(animal.EntityId) ||
                string.IsNullOrEmpty(text))
            {
                return;
            }

            BubbleState state;
            if (!States.TryGetValue(animal.EntityId, out state))
            {
                state = new BubbleState();
                States.Add(animal.EntityId, state);
            }
            // Yaw changes every frame while turning. Throttle all updates,
            // including changed text, so this visual tracer cannot become a
            // source of combat lag when a spawn grid is being inspected.
            if (now < state.NextRefreshAt)
            {
                return;
            }

            state.NextRefreshAt = now + RefreshSeconds;

            try
            {
                ChatBubbleGroup group =
                    UIManager.FindScript<ChatBubbleGroup>();
                if (group == null || animal.ChatableBase == null)
                {
                    return;
                }
                ChatBubble bubble = state.Bubble;
                if (bubble == null || !bubble.gameObject.activeSelf ||
                    state.Group != group ||
                    !string.Equals(
                        bubble.Id,
                        animal.EntityId,
                        StringComparison.Ordinal))
                {
                    bubble = GetStableBubble(group, animal.EntityId);
                    if (bubble == null)
                    {
                        return;
                    }
                    state.Group = group;
                    state.Bubble = bubble;
                    state.LastText = null;
                }

                // ChatBubbleGroup.Show always resets TweenScale to zero and
                // replays its pop animation. Debug text updates the existing
                // bubble directly instead, so the panel stays readable.
                if (!string.Equals(
                    state.LastText,
                    text,
                    StringComparison.Ordinal))
                {
                    bubble.AlwaysInScreen = true;
                    bubble.Set(
                        animal.ChatableBase,
                        text,
                        new PortraitBuilder.Argument?(),
                        string.Empty,
                        Color.white,
                        new ChatBubble.TargetPivot?(
                            ChatBubble.TargetPivot.Up),
                        new Vector3?(Vector3.up * 80f),
                        false);
                    bubble.Align = ChatBubble.ChatBubbleAlign.Auto;
                    bubble.gameObject.SetActive(true);
                    bubble.Refresh();
                    if (UpdateLayoutMethod != null)
                    {
                        UpdateLayoutMethod.Invoke(bubble, null);
                    }
                    FreezeScaleTween(bubble);
                    state.LastText = text;
                }
            }
            catch (Exception exception)
            {
                if (now >= _nextFailureLogAt &&
                    DurangoCombatSystemPlugin.Log != null)
                {
                    _nextFailureLogAt = now + 5.0;
                    DurangoCombatSystemPlugin.Log.LogWarning(
                        "Saurus debug bubble failed: " +
                        exception.Message);
                }
            }
        }

        private static ChatBubble GetStableBubble(
            ChatBubbleGroup group,
            string entityId)
        {
            if (GetBubbleMethod == null)
            {
                return null;
            }
            ChatBubble bubble = GetBubbleMethod.Invoke(
                group,
                new object[] { entityId, true }) as ChatBubble;
            if (bubble != null)
            {
                FreezeScaleTween(bubble);
            }
            return bubble;
        }

        private static void FreezeScaleTween(ChatBubble bubble)
        {
            if (bubble == null)
            {
                return;
            }
            UIWidget mainWidget = MainWidgetField == null
                ? null
                : MainWidgetField.GetValue(bubble) as UIWidget;
            if (mainWidget == null)
            {
                return;
            }
            TweenScale tween = mainWidget.GetComponent<TweenScale>();
            if (tween != null)
            {
                tween.tweenFactor = 1f;
                tween.enabled = false;
            }
            mainWidget.transform.localScale = Vector3.one;
        }

        internal static void Hide(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                return;
            }
            States.Remove(entityId);
            try
            {
                ChatBubbleGroup group =
                    UIManager.FindScript<ChatBubbleGroup>();
                if (group != null)
                {
                    group.Hide(entityId);
                }
            }
            catch
            {
                // Best-effort developer UI cleanup only.
            }
        }

        internal static void HideAll()
        {
            string[] entityIds = new string[States.Count];
            States.Keys.CopyTo(entityIds, 0);
            States.Clear();
            try
            {
                ChatBubbleGroup group =
                    UIManager.FindScript<ChatBubbleGroup>();
                if (group == null)
                {
                    return;
                }
                int i;
                for (i = 0; i < entityIds.Length; i++)
                {
                    group.Hide(entityIds[i]);
                }
            }
            catch
            {
                // Best-effort developer UI cleanup only.
            }
        }
    }
}
