using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal sealed class SaurusRootMotionKey
    {
        internal float Time;
        internal float X;
        internal float Z;
        internal float InX;
        internal float InZ;
        internal float OutX;
        internal float OutZ;
    }

    internal sealed class SaurusRootYawKey
    {
        internal float Time;
        internal float DeltaYaw;
    }

    internal sealed class SaurusRootMotionCurve
    {
        private readonly SaurusRootMotionKey[] _keys;
        private readonly SaurusRootYawKey[] _yawKeys;
        private readonly Vector2 _origin;

        internal SaurusRootMotionCurve(
            string motion,
            float duration,
            SaurusRootMotionKey[] keys,
            SaurusRootYawKey[] yawKeys)
        {
            Motion = motion;
            Duration = Mathf.Max(0f, duration);
            _keys = keys ?? new SaurusRootMotionKey[0];
            _yawKeys = yawKeys ?? new SaurusRootYawKey[0];
            _origin = _keys.Length == 0
                ? Vector2.zero
                : new Vector2(_keys[0].X, _keys[0].Z);
        }

        internal string Motion { get; private set; }

        internal float Duration { get; private set; }

        internal Vector2 GetLocalDelta(float time)
        {
            if (_keys.Length == 0)
            {
                return Vector2.zero;
            }
            if (time <= _keys[0].Time)
            {
                return Vector2.zero;
            }
            if (time >= _keys[_keys.Length - 1].Time)
            {
                SaurusRootMotionKey last = _keys[_keys.Length - 1];
                return new Vector2(last.X, last.Z) - _origin;
            }

            int i;
            for (i = 0; i < _keys.Length - 1; i++)
            {
                SaurusRootMotionKey left = _keys[i];
                SaurusRootMotionKey right = _keys[i + 1];
                if (time > right.Time)
                {
                    continue;
                }

                float span = Mathf.Max(0.0001f, right.Time - left.Time);
                float u = Mathf.Clamp01((time - left.Time) / span);
                float u2 = u * u;
                float u3 = u2 * u;
                float h00 = 2f * u3 - 3f * u2 + 1f;
                float h10 = u3 - 2f * u2 + u;
                float h01 = -2f * u3 + 3f * u2;
                float h11 = u3 - u2;
                float x = h00 * left.X + h10 * span * left.OutX +
                    h01 * right.X + h11 * span * right.InX;
                float z = h00 * left.Z + h10 * span * left.OutZ +
                    h01 * right.Z + h11 * span * right.InZ;
                return new Vector2(x, z) - _origin;
            }
            return Vector2.zero;
        }

        internal Vector2 GetLocalDeltaAtFrame(int frame, float frameRate)
        {
            return frameRate <= 0f
                ? Vector2.zero
                : GetLocalDelta(frame / frameRate);
        }

        internal float GetLocalYawDelta(float time)
        {
            if (_yawKeys.Length == 0 || time <= _yawKeys[0].Time)
            {
                return 0f;
            }
            if (time >= _yawKeys[_yawKeys.Length - 1].Time)
            {
                return _yawKeys[_yawKeys.Length - 1].DeltaYaw;
            }

            int i;
            for (i = 0; i < _yawKeys.Length - 1; i++)
            {
                SaurusRootYawKey left = _yawKeys[i];
                SaurusRootYawKey right = _yawKeys[i + 1];
                if (time > right.Time)
                {
                    continue;
                }
                float span = Mathf.Max(0.0001f, right.Time - left.Time);
                float u = Mathf.Clamp01((time - left.Time) / span);
                return Mathf.Lerp(left.DeltaYaw, right.DeltaYaw, u);
            }
            return 0f;
        }

        internal float GetLocalYawDeltaAtFrame(int frame, float frameRate)
        {
            return frameRate <= 0f
                ? 0f
                : GetLocalYawDelta(frame / frameRate);
        }
    }

    internal static class SaurusRootMotionData
    {
        private static Dictionary<string, SaurusRootMotionCurve> _curves =
            new Dictionary<string, SaurusRootMotionCurve>(
                StringComparer.OrdinalIgnoreCase);

        internal static bool Load(
            string json,
            CombatDataLoadReport report)
        {
            Reset();
            if (string.IsNullOrEmpty(json))
            {
                report.Errors.Add("Embedded Saurus root-motion data is empty.");
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    "Embedded Saurus root-motion data could not be parsed: " +
                    exception.Message);
                return false;
            }

            JObject clips = root["clips"] as JObject;
            if (clips == null)
            {
                report.Errors.Add("Saurus root-motion data has no clips object.");
                return false;
            }

            foreach (JProperty property in clips.Properties())
            {
                JObject source = property.Value as JObject;
                JArray sourceKeys = source == null
                    ? null
                    : source["keys"] as JArray;
                float duration;
                if (source == null || sourceKeys == null ||
                    !TrySingle(source["duration"], out duration))
                {
                    report.Errors.Add(
                        "Invalid Saurus root-motion clip: " + property.Name);
                    continue;
                }

                List<SaurusRootMotionKey> keys =
                    new List<SaurusRootMotionKey>();
                int i;
                for (i = 0; i < sourceKeys.Count; i++)
                {
                    JArray values = sourceKeys[i] as JArray;
                    if (values == null || values.Count != 7)
                    {
                        report.Errors.Add(
                            "Invalid root-motion key " + i + " in " +
                            property.Name + ".");
                        continue;
                    }
                    float[] parsed = new float[7];
                    int j;
                    bool valid = true;
                    for (j = 0; j < parsed.Length; j++)
                    {
                        if (!TrySingle(values[j], out parsed[j]))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid)
                    {
                        report.Errors.Add(
                            "Non-numeric root-motion key " + i + " in " +
                            property.Name + ".");
                        continue;
                    }
                    keys.Add(new SaurusRootMotionKey
                    {
                        Time = parsed[0],
                        X = parsed[1],
                        Z = parsed[2],
                        InX = parsed[3],
                        InZ = parsed[4],
                        OutX = parsed[5],
                        OutZ = parsed[6]
                    });
                }
                List<SaurusRootYawKey> yawKeys =
                    new List<SaurusRootYawKey>();
                JArray sourceYawKeys = source["yawKeys"] as JArray;
                if (sourceYawKeys != null)
                {
                    for (i = 0; i < sourceYawKeys.Count; i++)
                    {
                        JArray values = sourceYawKeys[i] as JArray;
                        float time;
                        float deltaYaw;
                        if (values == null || values.Count != 2 ||
                            !TrySingle(values[0], out time) ||
                            !TrySingle(values[1], out deltaYaw))
                        {
                            report.Errors.Add(
                                "Invalid root-yaw key " + i + " in " +
                                property.Name + ".");
                            continue;
                        }
                        yawKeys.Add(new SaurusRootYawKey
                        {
                            Time = time,
                            DeltaYaw = deltaYaw
                        });
                    }
                }
                if (keys.Count >= 2)
                {
                    _curves[property.Name] = new SaurusRootMotionCurve(
                        property.Name,
                        duration,
                        keys.ToArray(),
                        yawKeys.ToArray());
                }
            }
            return _curves.Count > 0 && report.IsValid;
        }

        internal static bool TryGet(
            string motion,
            out SaurusRootMotionCurve curve)
        {
            curve = null;
            return !string.IsNullOrEmpty(motion) &&
                _curves.TryGetValue(motion, out curve);
        }

        internal static void Reset()
        {
            _curves = new Dictionary<string, SaurusRootMotionCurve>(
                StringComparer.OrdinalIgnoreCase);
        }

        private static bool TrySingle(JToken token, out float value)
        {
            value = 0f;
            return token != null && float.TryParse(
                token.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
