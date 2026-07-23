using System;
using System.Reflection;
using Durango.UI;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatMode
{
    internal static class CombatModeShortcutVisibility
    {
        private static GameObject _leaveButton;
        private static GameObject _escLabel;

        internal static void RegisterLeaveButton(BattleActionButtons buttons)
        {
            FieldInfo field = typeof(BattleActionButtons).GetField(
                "_leaveButton",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _leaveButton = field == null ? null : field.GetValue(buttons) as GameObject;
            _escLabel = null;
        }

        internal static void HideEndCombatShortcut()
        {
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(UILabel));
            for (int i = 0; i < objects.Length; i++)
            {
                UILabel label = objects[i] as UILabel;
                if (label == null || !label.gameObject.activeInHierarchy || !IsEndCombatText(label.text))
                {
                    continue;
                }

                GameObject container = FindShortcutContainer(label.transform);
                if (container != null && container.activeSelf)
                {
                    AttachEscLabel(container, label);
                    container.SetActive(false);
                }
            }
        }

        private static void AttachEscLabel(GameObject shortcutContainer, UILabel descriptionLabel)
        {
            if (_leaveButton == null || _escLabel != null)
            {
                return;
            }

            UILabel template = null;
            UILabel[] labels = shortcutContainer.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != descriptionLabel)
                {
                    template = labels[i];
                    break;
                }
            }

            if (template == null)
            {
                return;
            }

            _escLabel = UnityEngine.Object.Instantiate(template.gameObject) as GameObject;
            _escLabel.name = "CombatLeaveEscLabel";
            _escLabel.transform.parent = _leaveButton.transform;
            _escLabel.transform.localScale = Vector3.one;
            _escLabel.transform.localPosition = new Vector3(30f, 30f, -5f);

            UILabel label = _escLabel.GetComponent<UILabel>();
            if (label != null)
            {
                label.text = "ESC";
            }

            _escLabel.SetActive(true);
        }

        private static bool IsEndCombatText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string normalized = text.Replace(" ", string.Empty);
            return normalized.Equals("EndCombat", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("EndCombatMode", StringComparison.OrdinalIgnoreCase);
        }

        private static GameObject FindShortcutContainer(Transform labelTransform)
        {
            Transform parent = labelTransform.parent;
            if (parent != null && parent.GetComponentsInChildren<UILabel>(true).Length <= 3)
            {
                return parent.gameObject;
            }
            return labelTransform.gameObject;
        }
    }


    [HarmonyLib.HarmonyPatch(typeof(BattleActionButtons), "Start")]
    internal static class CombatModeLeaveButtonLabelPatch
    {
        private static void Postfix(BattleActionButtons __instance)
        {
            CombatModeShortcutVisibility.RegisterLeaveButton(__instance);
        }
    }
}
