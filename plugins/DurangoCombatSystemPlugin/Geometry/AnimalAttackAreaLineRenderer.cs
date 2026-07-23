using System;
using System.Collections.Generic;
using Durango.Logic;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatSystemMod.Geometry
{
    internal static class AnimalAttackAreaLineRenderer
    {
        private const float VisibleSeconds = 1.1f;

        private sealed class Entry
        {
            internal GameObject Object;
            internal LineRenderer Line;
            internal Material Material;
            internal float HideAt;
        }

        private static readonly Dictionary<string, Entry> Entries =
            new Dictionary<string, Entry>();
        private static bool _enabled = true;

        internal static bool Enabled
        {
            get { return _enabled; }
        }

        internal static void SetEnabled(bool value)
        {
            _enabled = value;
            if (_enabled)
            {
                return;
            }

            foreach (Entry entry in Entries.Values)
            {
                if (entry != null && entry.Object != null)
                {
                    entry.Object.SetActive(false);
                }
            }
        }

        internal static void Show(AnimalBehavior animal, AnimalAttackArea area)
        {
            Show(animal, area, VisibleSeconds);
        }

        internal static void Show(
            AnimalBehavior animal,
            AnimalAttackArea area,
            float visibleSeconds)
        {
            if (!_enabled || animal == null || string.IsNullOrEmpty(animal.EntityId))
            {
                return;
            }

            try
            {
                Entry entry = GetOrCreate(animal.EntityId);
                if (entry == null || entry.Line == null)
                {
                    return;
                }

                Vector3 origin = area.Origin;
                origin.y += 8f;
                Vector3 forward = area.Forward;
                forward.y = 0f;
                if (forward.sqrMagnitude <= 0.001f)
                {
                    forward = Vector3.forward;
                }
                forward.Normalize();
                Vector3 right = new Vector3(forward.z, 0f, -forward.x);

                if (area.Shape == AnimalAttackShape.Circle)
                {
                    DrawCircle(entry.Line, origin, forward, right, area.Radius);
                }
                else if (area.Shape == AnimalAttackShape.HalfCircle)
                {
                    DrawHalfCircle(entry.Line, origin, forward, right, area.Radius);
                }
                else if (area.Shape == AnimalAttackShape.Rectangle)
                {
                    DrawRectangle(
                        entry.Line,
                        origin,
                        forward,
                        right,
                        area.Length,
                        area.HalfWidth);
                }
                else
                {
                    DrawSector(
                        entry.Line,
                        origin,
                        forward,
                        right,
                        area.Radius,
                        area.ArcStart,
                        area.ArcEnd);
                }

                entry.Object.SetActive(true);
                entry.HideAt = Time.realtimeSinceStartup +
                    Mathf.Max(0.1f, visibleSeconds);
            }
            catch (Exception exception)
            {
                BaoX.DurangoOriginal.OfflineCombat.OfflineCombatBackendPlugin.Log.LogWarning(
                    "Animal attack-area line render failed: " + exception.Message);
            }
        }

        internal static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            foreach (Entry entry in Entries.Values)
            {
                if (entry != null && entry.Object != null &&
                    entry.Object.activeSelf && now >= entry.HideAt)
                {
                    entry.Object.SetActive(false);
                }
            }
        }

        internal static void Reset()
        {
            foreach (Entry entry in Entries.Values)
            {
                if (entry == null)
                {
                    continue;
                }
                if (entry.Object != null)
                {
                    UnityEngine.Object.Destroy(entry.Object);
                }
                if (entry.Material != null)
                {
                    UnityEngine.Object.Destroy(entry.Material);
                }
            }
            Entries.Clear();
        }

        private static Entry GetOrCreate(string entityId)
        {
            Entry entry;
            if (Entries.TryGetValue(entityId, out entry) &&
                entry != null && entry.Object != null && entry.Line != null)
            {
                return entry;
            }

            GameObject obj = new GameObject("DurangoCombat_AnimalAttackArea_" + entityId);
            UnityEngine.Object.DontDestroyOnLoad(obj);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            Material material = CreateMaterial();
            line.material = material;
            line.startColor = Color.red;
            line.endColor = Color.red;
            line.startWidth = 8f;
            line.endWidth = 8f;
            obj.SetActive(false);

            entry = new Entry
            {
                Object = obj,
                Line = line,
                Material = material,
                HideAt = 0f
            };
            Entries[entityId] = entry;
            return entry;
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
            Color color = new Color(1f, 0f, 0f, 1f);
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

        private static void DrawCircle(
            LineRenderer line,
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float radius)
        {
            const int segments = 64;
            line.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                line.SetPosition(
                    i,
                    origin + forward * Mathf.Cos(angle) * radius +
                    right * Mathf.Sin(angle) * radius);
            }
        }

        private static void DrawHalfCircle(
            LineRenderer line,
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float radius)
        {
            const int segments = 64;
            line.positionCount = segments + 2;
            Vector3 first = origin - right * radius;
            for (int i = 0; i <= segments; i++)
            {
                float angle = -Mathf.PI * 0.5f + Mathf.PI * i / segments;
                line.SetPosition(
                    i,
                    origin + forward * Mathf.Cos(angle) * radius +
                    right * Mathf.Sin(angle) * radius);
            }
            line.SetPosition(segments + 1, first);
        }

        private static void DrawRectangle(
            LineRenderer line,
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float length,
            float halfWidth)
        {
            line.positionCount = 5;
            Vector3 p0 = origin - right * halfWidth;
            Vector3 p1 = origin + right * halfWidth;
            Vector3 p2 = origin + forward * length + right * halfWidth;
            Vector3 p3 = origin + forward * length - right * halfWidth;
            line.SetPosition(0, p0);
            line.SetPosition(1, p1);
            line.SetPosition(2, p2);
            line.SetPosition(3, p3);
            line.SetPosition(4, p0);
        }

        private static void DrawSector(
            LineRenderer line,
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float radius,
            float arcStart,
            float arcEnd)
        {
            const int segments = 48;
            float start = AnimalAttackGeometry.NormalizeAngle(arcStart);
            float end = AnimalAttackGeometry.NormalizeAngle(arcEnd);
            if (end < start)
            {
                end += 360f;
            }

            line.positionCount = segments + 3;
            line.SetPosition(0, origin);
            for (int i = 0; i <= segments; i++)
            {
                float degrees = start + (end - start) * i / segments;
                float angle = degrees * Mathf.Deg2Rad;
                line.SetPosition(
                    i + 1,
                    origin + forward * Mathf.Cos(angle) * radius +
                    right * Mathf.Sin(angle) * radius);
            }
            line.SetPosition(segments + 2, origin);
        }
    }
}
