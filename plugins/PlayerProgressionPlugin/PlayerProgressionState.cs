using System;
using System.Collections.Generic;
using Durango.Offline;
using Yaml;
using Yaml.Util;

namespace BaoX.DurangoOriginal.PlayerProgressionMod
{
    internal sealed class PlayerProgressionState
    {
        internal const float LevelOneHp = 300f;
        internal const float LevelOneInitialHp = 200f;
        internal const float HpPerLevel = 9f;
        internal const float LevelOneStamina = 100f;

        private readonly PlayerContext _context;

        internal PlayerProgressionState(PlayerContext context, int experience)
        {
            _context = context;
            Experience = Math.Max(0, experience);
        }

        internal int Experience { get; private set; }

        internal PlayerContext Context
        {
            get { return _context; }
        }

        internal int Level
        {
            get
            {
                if (_context.PlayerInfo == null)
                {
                    return 1;
                }
                return Math.Max(1, _context.PlayerInfo.PlayerLevel);
            }
        }

        internal static float GetMaxHp(int level)
        {
            return LevelOneHp + Math.Max(0, level - 1) * HpPerLevel;
        }

        internal static float GetMaxStamina(int level)
        {
            return LevelOneStamina + Math.Max(0, level / 2);
        }

        internal static int GetBasicAbility(int level)
        {
            return 4 + Math.Max(0, level - 1) * 2;
        }

        internal static int GetSkillPointReward(int level)
        {
            if (level <= 1)
            {
                return 9;
            }
            if (level <= 8)
            {
                return 6;
            }
            if (level == 9)
            {
                return 7;
            }
            return 8;
        }

        internal static int GetTotalSkillPoints(int level)
        {
            int total = GetSkillPointReward(1);
            for (int current = 2; current <= Math.Max(1, level); current++)
            {
                total += GetSkillPointReward(current);
            }
            return total;
        }

        internal void ApplyToContext(bool resetProgression)
        {
            int level = resetProgression ? 1 : CalculateLevel(Experience, Level);
            SetLevel(level);
            RebuildGauges(resetProgression);
        }

        internal int AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int previousLevel = Level;
            if (Experience > int.MaxValue - amount)
            {
                Experience = int.MaxValue;
            }
            else
            {
                Experience += amount;
            }

            int currentLevel = CalculateLevel(Experience, previousLevel);
            SetLevel(currentLevel);
            RebuildGauges(currentLevel > previousLevel);
            _context.Save();
            return currentLevel - previousLevel;
        }

        internal int SetExperience(int amount)
        {
            int previousLevel = Level;
            Experience = Math.Max(0, amount);
            int currentLevel = CalculateLevel(Experience, previousLevel);
            SetLevel(currentLevel);
            RebuildGauges(currentLevel != previousLevel);
            _context.Save();
            return currentLevel - previousLevel;
        }

        private void SetLevel(int level)
        {
            level = Math.Max(1, Math.Min(60, level));
            if (_context.PlayerInfo != null)
            {
                _context.PlayerInfo.PlayerLevel = level;
            }
            _context.AppearPlayer.Level = level;
        }

        private void RebuildGauges(bool fullRestore)
        {
            int level = Level;
            float maxHp = GetMaxHp(level);
            float maxStamina = GetMaxStamina(level);
            float currentHp = fullRestore ? GetRestoreHp(level, maxHp) : ReadCurrent(_context.AppearPlayer.Survival.Life, maxHp);

            if (_context.AppearPlayer.Survival.Gauges == null)
            {
                _context.AppearPlayer.Survival.Gauges = new Dictionary<string, Gauge>();
            }

            Gauge oldStamina;
            _context.AppearPlayer.Survival.Gauges.TryGetValue("stamina", out oldStamina);
            float currentStamina = fullRestore ? maxStamina : ReadCurrent(oldStamina, maxStamina);

            _context.AppearPlayer.Survival.Life = MakeGauge(maxHp, currentHp);
            _context.AppearPlayer.Survival.Gauges["stamina"] = MakeGauge(maxStamina, currentStamina);

            if (!_context.AppearPlayer.Survival.Gauges.ContainsKey("fatigue"))
            {
                _context.AppearPlayer.Survival.Gauges["fatigue"] = MakeGauge(100f, 0f);
            }

            if (fullRestore)
            {
                _context.AppearPlayer.IsAlive = true;
            }
        }

        private static float ReadCurrent(Gauge gauge, float max)
        {
            if (gauge == null)
            {
                return max;
            }

            return Math.Max(0f, Math.Min(max, gauge.Get()));
        }

        private float GetRestoreHp(int level, float maxHp)
        {
            if (Experience == 0 && level <= 1)
            {
                return Math.Max(0f, Math.Min(maxHp, LevelOneInitialHp));
            }
            return maxHp;
        }

        private static Gauge MakeGauge(float max, float current)
        {
            return new Gauge(max, 0f, new GaugeNode[]
            {
                new GaugeNode
                {
                    Time = 0.0,
                    Value = current
                }
            });
        }

        private static int CalculateLevel(int experience, int fallbackLevel)
        {
            try
            {
                PlayerStatistics statistics = Singleton<PlayerStatistics>.Instance;
                int[] thresholds = statistics == null ? null : statistics.level_thresholds;
                if (thresholds == null || thresholds.Length == 0)
                {
                    return Math.Max(1, Math.Min(60, fallbackLevel));
                }

                int level = 1;
                int maxThreshold = Math.Min(thresholds.Length, 59);
                while (level <= maxThreshold && experience >= thresholds[level - 1])
                {
                    level++;
                }
                return Math.Min(60, level);
            }
            catch
            {
                return Math.Max(1, Math.Min(60, fallbackLevel));
            }
        }
    }
}
