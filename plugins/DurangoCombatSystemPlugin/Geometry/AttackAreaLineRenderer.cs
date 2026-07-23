using System;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatSystemMod.Geometry
{
    internal static class AttackAreaLineRenderer
    {
        private const float VisibleSeconds = 1.1f;
        private static GameObject _object;
        private static LineRenderer _line;
        private static Material _material;
        private static float _hideAt;
        private static bool _enabled = true;

        internal static bool Enabled
        {
            get { return _enabled; }
        }

        internal static void SetEnabled(bool value)
        {
            _enabled = value;
            if (!_enabled && _object != null)
            {
                _object.SetActive(false);
            }
        }

        internal static void Show(PlayerAttackArea area)
        {
            if (!_enabled)
            {
                return;
            }

            try
            {
                EnsureRenderer();
                if (_line == null)
                {
                    return;
                }

                Vector3 origin = area.Origin;
                origin.y += 8f;
                Vector3 forward = area.Forward;
                Vector3 right = new Vector3(forward.z, 0f, -forward.x);
                WeaponSkillAoEProfile profile = area.Profile;

                if (string.Equals(profile.Shape, "circle", StringComparison.OrdinalIgnoreCase))
                {
                    DrawEllipse(origin, forward, right, profile.Length, profile.Length, true);
                }
                else if (string.Equals(profile.Shape, "half-circle", StringComparison.OrdinalIgnoreCase))
                {
                    DrawHalfEllipse(origin, forward, right, profile.Length, profile.Length);
                }
                else if (string.Equals(profile.Shape, "half-ellipse", StringComparison.OrdinalIgnoreCase))
                {
                    DrawHalfEllipse(
                        origin,
                        forward,
                        right,
                        Mathf.Max(1f, profile.Length),
                        Mathf.Max(1f, profile.HalfWidth));
                }
                else
                {
                    DrawRectangle(origin, forward, right, profile.Length, profile.HalfWidth);
                }

                _object.SetActive(true);
                _hideAt = Time.realtimeSinceStartup + VisibleSeconds;
            }
            catch (Exception exception)
            {
                BaoX.DurangoOriginal.OfflineCombat.OfflineCombatBackendPlugin.Log.LogWarning(
                    "Attack area line render failed: " + exception.Message);
            }
        }

        internal static void Tick()
        {
            if (_object != null && _object.activeSelf &&
                Time.realtimeSinceStartup >= _hideAt)
            {
                _object.SetActive(false);
            }
        }

        internal static void Reset()
        {
            if (_object != null)
            {
                UnityEngine.Object.Destroy(_object);
            }
            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
            }
            _object = null;
            _line = null;
            _material = null;
            _hideAt = 0f;
        }

        private static void EnsureRenderer()
        {
            if (_object != null && _line != null)
            {
                return;
            }

            _object = new GameObject("DurangoCombat_PlayerAttackArea");
            UnityEngine.Object.DontDestroyOnLoad(_object);
            _line = _object.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _material = CreateMaterial();
            _line.material = _material;
            _line.SetColors(Color.yellow, Color.yellow);
            _line.SetWidth(6f, 6f);
            _object.SetActive(false);
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Unlit/Transparent Colored");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            Material material = new Material(shader);
            Color color = new Color(1f, 0.88f, 0f, 1f);
            material.color = color;
            string[] names = new string[] { "_Color", "_TintColor", "_MainColor" };
            for (int i = 0; i < names.Length; i++)
            {
                if (material.HasProperty(names[i]))
                {
                    material.SetColor(names[i], color);
                }
            }
            return material;
        }

        private static void DrawRectangle(
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float length,
            float halfWidth)
        {
            _line.SetVertexCount(5);
            Vector3 p0 = origin - right * halfWidth;
            Vector3 p1 = origin + right * halfWidth;
            Vector3 p2 = origin + forward * length + right * halfWidth;
            Vector3 p3 = origin + forward * length - right * halfWidth;
            _line.SetPosition(0, p0);
            _line.SetPosition(1, p1);
            _line.SetPosition(2, p2);
            _line.SetPosition(3, p3);
            _line.SetPosition(4, p0);
        }

        private static void DrawEllipse(
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float forwardRadius,
            float sideRadius,
            bool full)
        {
            int segments = full ? 48 : 32;
            _line.SetVertexCount(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / (float)segments * Mathf.PI * 2f;
                _line.SetPosition(
                    i,
                    origin + forward * Mathf.Cos(angle) * forwardRadius +
                    right * Mathf.Sin(angle) * sideRadius);
            }
        }

        private static void DrawHalfEllipse(
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float forwardRadius,
            float sideRadius)
        {
            const int segments = 32;
            _line.SetVertexCount(segments + 2);
            Vector3 first = origin - right * sideRadius;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (-90f + 180f * (float)i / (float)segments) * Mathf.Deg2Rad;
                _line.SetPosition(
                    i,
                    origin + forward * Mathf.Cos(angle) * forwardRadius +
                    right * Mathf.Sin(angle) * sideRadius);
            }
            _line.SetPosition(segments + 1, first);
        }
    }
}
