using System.Reflection;
using Durango.UI;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatMode
{
    internal sealed class CombatModeControlPosition : MonoBehaviour
    {
        private const float OffsetY = -40f;

        private Transform[] _controls;
        private Vector3[] _lastApplied;

        internal void Initialize(BattleActionButtons buttons)
        {
            _controls = new Transform[]
            {
                GetTransform(buttons, "_autoButton"),
                GetTransform(buttons, "_battleModeLockButton"),
                GetTransform(buttons, "_leaveButton")
            };
            _lastApplied = new Vector3[_controls.Length];

            for (int i = 0; i < _controls.Length; i++)
            {
                ApplyFromCurrent(i);
            }
        }

        private void LateUpdate()
        {
            if (_controls == null)
            {
                return;
            }

            for (int i = 0; i < _controls.Length; i++)
            {
                Transform control = _controls[i];
                if (control == null)
                {
                    continue;
                }

                if ((control.localPosition - _lastApplied[i]).sqrMagnitude > 0.01f)
                {
                    ApplyFromCurrent(i);
                }
            }
        }

        private void ApplyFromCurrent(int index)
        {
            Transform control = _controls[index];
            if (control == null)
            {
                return;
            }

            Vector3 position = control.localPosition;
            position.y += OffsetY;
            control.localPosition = position;
            _lastApplied[index] = position;
        }

        private static Transform GetTransform(BattleActionButtons buttons, string fieldName)
        {
            FieldInfo field = typeof(BattleActionButtons).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            object value = field == null ? null : field.GetValue(buttons);

            Component component = value as Component;
            if (component != null)
            {
                return component.transform;
            }

            GameObject gameObject = value as GameObject;
            return gameObject == null ? null : gameObject.transform;
        }
    }

    [HarmonyPatch(typeof(BattleActionButtons), "Start")]
    internal static class CombatModeControlPositionPatch
    {
        private static void Postfix(BattleActionButtons __instance)
        {
            CombatModeControlPosition keeper = __instance.GetComponent<CombatModeControlPosition>();
            if (keeper == null)
            {
                keeper = __instance.gameObject.AddComponent<CombatModeControlPosition>();
            }
            keeper.Initialize(__instance);
        }
    }
}
