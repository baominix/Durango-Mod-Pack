using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Offline;
using Durango.Utils;
using Messages;
using Shared.Skill;
using Yaml.Util;
using LogicBundle = Durango.Logic.Skill.Bundle;
using LogicSkill = Durango.Logic.Skill.Skill;

namespace BaoX.DurangoOriginal.SkillSystemMod
{
    internal sealed class OfflineSkillState
    {
        private readonly PlayerContext _context;
        private SkillSaveData _data;

        internal OfflineSkillState(PlayerContext context)
        {
            _context = context;
            _data = SkillPlayerDataPersistence.Load(context);
        }

        internal bool Learn(LearnSkill request, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(request.SkillId) || string.IsNullOrEmpty(request.SubId) || request.Level <= 0)
            {
                error = "invalid request";
                return false;
            }

            Durango.Logic.Skill.Node node = FindNode(request.SkillId, request.SubId, request.Level);
            if (node == null)
            {
                error = "skill node not found";
                return false;
            }

            SkillBundleSaveData bundle = GetBundle(request.SkillId, true);
            bundle.Category = (int)node.Category;

            SkillLevelSaveData saved = GetLevel(bundle, request.SubId, true);
            int expectedLevel = saved.Level + 1;
            if (request.Level == saved.Level)
            {
                return true;
            }
            if (request.Level != expectedLevel)
            {
                error = "expected level " + expectedLevel + ", saved level " + saved.Level;
                return false;
            }

            saved.Level = request.Level;
            Save();
            return true;
        }

        internal bool Untrain(UntrainSkill request)
        {
            SkillBundleSaveData bundle = GetBundle(request.SkillId, false);
            SkillLevelSaveData saved = GetLevel(bundle, request.SubId, false);
            if (saved == null || saved.Level <= 0)
            {
                return false;
            }

            int currentLevel = saved.Level;
            if (request.Level > 0 && request.Level != currentLevel)
            {
                return false;
            }

            int nextLevel = saved.Level - 1;
            if (HasDependentBranch(bundle, saved.SubId, nextLevel))
            {
                return false;
            }

            saved.Level = nextLevel;
            if (saved.Level <= 0)
            {
                bundle.Levels.Remove(saved);
            }
            if (bundle.Levels.Count == 0)
            {
                _data.Bundles.Remove(bundle);
            }

            Save();
            return true;
        }

        internal bool HasDependentBranch(string skillId, string parentSubId, int parentLevelAfterUntrain)
        {
            SkillBundleSaveData bundle = GetBundle(skillId, false);
            return HasDependentBranch(bundle, parentSubId, parentLevelAfterUntrain);
        }

        private static bool HasDependentBranch(SkillBundleSaveData bundle, string parentSubId, int parentLevelAfterUntrain)
        {
            if (bundle == null || bundle.Levels == null || string.IsNullOrEmpty(parentSubId))
            {
                return false;
            }

            bool parentIsBase = IsBaseSubSkill(bundle.SkillId, parentSubId);
            for (int i = 0; i < bundle.Levels.Count; i++)
            {
                SkillLevelSaveData child = bundle.Levels[i];
                if (child == null || child.Level <= 0 || string.IsNullOrEmpty(child.SubId))
                {
                    continue;
                }

                if (string.Equals(child.SubId, parentSubId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (parentIsBase)
                {
                    if (parentLevelAfterUntrain <= 0)
                    {
                        return true;
                    }
                    continue;
                }

                if (IsDependentSubSkill(parentSubId, child.SubId) && child.Level > parentLevelAfterUntrain)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBaseSubSkill(string skillId, string subId)
        {
            if (string.Equals(subId, "__base__", StringComparison.Ordinal))
            {
                return true;
            }

            if (!GameSystem<SkillSystem>.HasInstance() || string.IsNullOrEmpty(skillId))
            {
                return false;
            }

            LogicBundle bundle = GameSystem<SkillSystem>.Instance().FindSkill(skillId);
            return bundle != null && bundle.Base != null && string.Equals(bundle.Base.SubId, subId, StringComparison.Ordinal);
        }

        private static bool IsDependentSubSkill(string parentSubId, string childSubId)
        {
            if (string.Equals(parentSubId, "__base__", StringComparison.Ordinal))
            {
                return true;
            }

            return childSubId.StartsWith(parentSubId + "_", StringComparison.Ordinal);
        }

        internal Skills CreateMessage()
        {
            int autoLearned = EnsureAutoSkills();
            if (autoLearned > 0)
            {
                Save();
                LogAutoSkills(autoLearned);
            }
            EnsureSkillPoints();

            List<SkillBundle> bundles = new List<SkillBundle>();
            List<Messages.Skill> learned = new List<Messages.Skill>();
            for (int i = 0; i < _data.Bundles.Count; i++)
            {
                SkillBundleSaveData savedBundle = _data.Bundles[i];
                Dictionary<string, int> levels = new Dictionary<string, int>(StringComparer.Ordinal);

                for (int j = 0; j < savedBundle.Levels.Count; j++)
                {
                    SkillLevelSaveData savedLevel = savedBundle.Levels[j];
                    if (savedLevel.Level <= 0 || string.IsNullOrEmpty(savedLevel.SubId))
                    {
                        continue;
                    }

                    levels[savedLevel.SubId] = savedLevel.Level;

                    Messages.Skill learnedSkill = new Messages.Skill();
                    learnedSkill.SkillId = savedBundle.SkillId;
                    learnedSkill.SubId = savedLevel.SubId;
                    learnedSkill.Level = savedLevel.Level;
                    learned.Add(learnedSkill);
                }

                if (levels.Count > 0)
                {
                    SkillBundle bundle = new SkillBundle();
                    bundle.Category = (Category)savedBundle.Category;
                    bundle.SkillId = savedBundle.SkillId;
                    bundle.Levels = levels;
                    bundles.Add(bundle);
                }
            }

            Skills result = new Skills();
            result.SkillList = bundles.ToArray();
            result.SkillPoint = _data.SkillPoints;
            result.Categories = CreateCategories();
            result.UntrainedCount = 0;
            result.AdvisedSkills = learned.ToArray();
            result.AdvisedSkillCategories = new Dictionary<Category, int>();
            return result;
        }

        private Dictionary<Category, SkillCategory> CreateCategories()
        {
            Dictionary<Category, SkillCategory> result = new Dictionary<Category, SkillCategory>();
            for (int i = 0; i < _data.Categories.Count; i++)
            {
                SkillCategorySaveData saved = _data.Categories[i];
                Category type = (Category)saved.Category;
                SkillCategory category = new SkillCategory();
                category.Level = saved.Level;
                category.Exp = saved.Exp;
                category.ResearchTime = null;
                category.Researching = null;
                result[type] = category;
            }
            return result;
        }

        internal void EnsureSkillPoints()
        {
            int playerLevel = _context.PlayerInfo == null ? 1 : Math.Max(1, _context.PlayerInfo.PlayerLevel);
            int total = GetTotalSkillPoints(playerLevel);
            if (_data.SkillPoints != total)
            {
                _data.SkillPoints = total;
                Save();
            }
        }

        internal bool ModifyCategoryExperience(Category category, string operation, int amount, out int previousLevel, out int currentLevel, out int currentExp)
        {
            previousLevel = 0;
            currentLevel = 0;
            currentExp = 0;
            SkillCategorySaveData saved = GetCategory(category);
            if (saved == null || amount < 0)
            {
                return false;
            }

            previousLevel = saved.Level;
            if (string.Equals(operation, "set", StringComparison.OrdinalIgnoreCase))
            {
                // "set" means total category XP, so rebuild the level from the beginning.
                saved.Level = 1;
                saved.Exp = amount;
                saved.GameplayExpRemainder = 0.0;
            }
            else if (string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase))
            {
                if (saved.Exp > int.MaxValue - amount)
                {
                    saved.Exp = int.MaxValue;
                }
                else
                {
                    saved.Exp += amount;
                }
            }
            else
            {
                return false;
            }

            while (saved.Level < 60)
            {
                int needed = GetCategoryExpNeeded(category, saved.Level);
                if (needed <= 0 || saved.Exp < needed)
                {
                    break;
                }
                saved.Exp -= needed;
                saved.Level++;
            }

            currentLevel = saved.Level;
            currentExp = saved.Exp;
            int autoLearned = currentLevel != previousLevel
                ? EnsureAutoSkills()
                : 0;
            SaveDeferred();
            LogAutoSkills(autoLearned);
            return true;
        }

        internal bool AddCategoryExperienceFromGameplay(
            Category category,
            double amount,
            out int previousLevel,
            out int currentLevel,
            out int currentExp,
            out int appliedExp,
            out double remainder)
        {
            previousLevel = 0;
            currentLevel = 0;
            currentExp = 0;
            appliedExp = 0;
            remainder = 0.0;

            SkillCategorySaveData saved = GetCategory(category);
            if (saved == null ||
                amount <= 0.0 ||
                Double.IsNaN(amount) ||
                Double.IsInfinity(amount))
            {
                return false;
            }

            previousLevel = saved.Level;
            int playerLevel = _context.PlayerInfo == null
                ? 1
                : Math.Max(1, Math.Min(60, _context.PlayerInfo.PlayerLevel));
            if (saved.Level >= playerLevel || saved.Level >= 60)
            {
                currentLevel = saved.Level;
                currentExp = saved.Exp;
                remainder = saved.GameplayExpRemainder;
                return true;
            }

            double accumulated = saved.GameplayExpRemainder + amount;
            long wholeExp = accumulated >= Int32.MaxValue
                ? Int32.MaxValue
                : (long)Math.Floor(accumulated + 0.000000001);
            saved.GameplayExpRemainder = Math.Max(
                0.0,
                Math.Min(0.999999999, accumulated - wholeExp));

            long capacity = GetGameplayExperienceCapacity(
                category,
                saved,
                playerLevel);
            appliedExp = (int)Math.Min(wholeExp, capacity);

            if (appliedExp > 0)
            {
                saved.Exp += appliedExp;
                while (saved.Level < playerLevel && saved.Level < 60)
                {
                    int needed = GetCategoryExpNeeded(category, saved.Level);
                    if (needed <= 0 || saved.Exp < needed)
                    {
                        break;
                    }

                    saved.Exp -= needed;
                    saved.Level++;
                }
            }

            currentLevel = saved.Level;
            currentExp = saved.Exp;
            remainder = saved.GameplayExpRemainder;
            int autoLearned = currentLevel > previousLevel
                ? EnsureAutoSkills()
                : 0;
            SaveDeferred();
            LogAutoSkills(autoLearned);
            return true;
        }

        internal int SkillPoints
        {
            get
            {
                EnsureSkillPoints();
                return _data.SkillPoints;
            }
        }

        internal PlayerContext Context
        {
            get { return _context; }
        }

        internal int GetCategoryLevel(Category category)
        {
            SkillCategorySaveData saved = GetCategory(category);
            return saved == null ? 1 : Math.Max(1, saved.Level);
        }

        internal bool SetCategoryLevel(Category category, int level, out int currentLevel)
        {
            currentLevel = 0;
            SkillCategorySaveData saved = GetCategory(category);
            if (saved == null)
            {
                return false;
            }

            saved.Level = Math.Max(1, Math.Min(60, level));
            saved.Exp = 0;
            saved.GameplayExpRemainder = 0.0;
            currentLevel = saved.Level;
            int autoLearned = EnsureAutoSkills();
            SaveDeferred();
            LogAutoSkills(autoLearned);
            return true;
        }

        private int EnsureAutoSkills()
        {
            if (!GameSystem<SkillSystem>.HasInstance())
            {
                return 0;
            }

            List<LogicBundle> skills = GameSystem<SkillSystem>.Instance().Skills;
            if (skills == null || skills.Count == 0)
            {
                return 0;
            }

            int learned = 0;
            for (int i = 0; i < skills.Count; i++)
            {
                LogicBundle logicBundle = skills[i];
                if (logicBundle == null || string.IsNullOrEmpty(logicBundle.Id))
                {
                    continue;
                }

                SkillBundleSaveData savedBundle = GetBundle(logicBundle.Id, false);
                learned += EnsureAutoSkill(logicBundle, logicBundle.Base, ref savedBundle);

                if (logicBundle.Sub == null)
                {
                    continue;
                }

                for (int j = 0; j < logicBundle.Sub.Length; j++)
                {
                    learned += EnsureAutoSkill(logicBundle, logicBundle.Sub[j], ref savedBundle);
                }
            }

            return learned;
        }

        private int EnsureAutoSkill(LogicBundle logicBundle, LogicSkill logicSkill, ref SkillBundleSaveData savedBundle)
        {
            if (logicSkill == null || string.IsNullOrEmpty(logicSkill.SubId))
            {
                return 0;
            }

            if (logicSkill != logicBundle.Base && GetSavedLevel(savedBundle, logicBundle.Base) <= 0)
            {
                return 0;
            }

            SkillLevelSaveData savedLevel = GetLevel(savedBundle, logicSkill.SubId, false);
            int currentLevel = savedLevel == null ? 0 : Math.Max(0, savedLevel.Level);
            int learned = 0;

            while (currentLevel < logicSkill.MaxLevel)
            {
                Durango.Logic.Skill.Node node = logicSkill.Get(currentLevel + 1);
                if (node == null || node.SkillPoints > 0 || node.CategoryLevel > GetCategoryLevel(node.Category))
                {
                    break;
                }

                if (savedBundle == null)
                {
                    savedBundle = GetBundle(logicBundle.Id, true);
                }
                savedBundle.Category = (int)node.Category;

                if (savedLevel == null)
                {
                    savedLevel = GetLevel(savedBundle, logicSkill.SubId, true);
                }

                currentLevel++;
                savedLevel.Level = currentLevel;
                learned++;
            }

            return learned;
        }

        private static int GetSavedLevel(SkillBundleSaveData savedBundle, LogicSkill logicSkill)
        {
            if (savedBundle == null || logicSkill == null)
            {
                return 0;
            }

            SkillLevelSaveData savedLevel = GetLevel(savedBundle, logicSkill.SubId, false);
            return savedLevel == null ? 0 : Math.Max(0, savedLevel.Level);
        }

        private static void LogAutoSkills(int count)
        {
            if (count > 0 && SkillSystemPlugin.Log != null)
            {
                SkillSystemPlugin.Log.LogInfo("Auto-learned " + count + " skill node(s) for available category levels.");
            }
        }

        internal IEnumerable<Durango.Logic.Skill.Node> EnumerateLearnedNodes()
        {
            for (int i = 0; i < _data.Bundles.Count; i++)
            {
                SkillBundleSaveData bundle = _data.Bundles[i];
                for (int j = 0; j < bundle.Levels.Count; j++)
                {
                    SkillLevelSaveData saved = bundle.Levels[j];
                    for (int level = 1; level <= saved.Level; level++)
                    {
                        Durango.Logic.Skill.Node node = FindNode(bundle.SkillId, saved.SubId, level);
                        if (node != null)
                        {
                            yield return node;
                        }
                    }
                }
            }
        }

        private SkillCategorySaveData GetCategory(Category category)
        {
            for (int i = 0; i < _data.Categories.Count; i++)
            {
                if (_data.Categories[i].Category == (int)category)
                {
                    return _data.Categories[i];
                }
            }
            return null;
        }

        private static int GetCategoryExpNeeded(Category category, int level)
        {
            Yaml.SkillCategory data = SingletonDict<Category, Yaml.SkillCategory>.Get(category, null);
            return data == null || data.ExpNeeded == null ? -1 : data.ExpNeeded.Get(level, -1);
        }

        private static long GetGameplayExperienceCapacity(
            Category category,
            SkillCategorySaveData saved,
            int levelCap)
        {
            if (saved == null || saved.Level > levelCap || saved.Level >= 60)
            {
                return 0L;
            }

            long capacity = 0L;
            for (int level = saved.Level; level < levelCap && level < 60; level++)
            {
                int needed = GetCategoryExpNeeded(category, level);
                if (needed <= 0)
                {
                    return capacity;
                }

                capacity += level == saved.Level
                    ? Math.Max(0, needed - saved.Exp)
                    : needed;
            }

            return Math.Min(Int32.MaxValue, capacity);
        }

        private static int GetTotalSkillPoints(int level)
        {
            int total = 9;
            for (int current = 2; current <= level; current++)
            {
                total += (current <= 8) ? 6 : ((current == 9) ? 7 : 8);
            }
            return total;
        }

        private static Durango.Logic.Skill.Node FindNode(string skillId, string subId, int level)
        {
            if (!GameSystem<SkillSystem>.HasInstance() || string.IsNullOrEmpty(skillId) || string.IsNullOrEmpty(subId) || level <= 0)
            {
                return null;
            }

            return GameSystem<SkillSystem>.Instance().FindSkill(skillId, subId, level);
        }

        private SkillBundleSaveData GetBundle(string skillId, bool create)
        {
            for (int i = 0; i < _data.Bundles.Count; i++)
            {
                if (_data.Bundles[i].SkillId == skillId)
                {
                    return _data.Bundles[i];
                }
            }

            if (!create)
            {
                return null;
            }

            SkillBundleSaveData bundle = new SkillBundleSaveData();
            bundle.SkillId = skillId;
            bundle.Normalize();
            _data.Bundles.Add(bundle);
            return bundle;
        }

        private static SkillLevelSaveData GetLevel(SkillBundleSaveData bundle, string subId, bool create)
        {
            if (bundle == null)
            {
                return null;
            }

            for (int i = 0; i < bundle.Levels.Count; i++)
            {
                if (bundle.Levels[i].SubId == subId)
                {
                    return bundle.Levels[i];
                }
            }

            if (!create)
            {
                return null;
            }

            SkillLevelSaveData level = new SkillLevelSaveData();
            level.SubId = subId;
            bundle.Levels.Add(level);
            return level;
        }

        private void Save()
        {
            _data.Normalize();
            SkillPlayerDataPersistence.SaveNow(_context, _data);
        }

        private void SaveDeferred()
        {
            _data.Normalize();
            SkillPlayerDataPersistence.MarkDirty(_context, _data);
        }
    }

    [Serializable]
    internal sealed class SkillSaveData
    {
        public int Version = 3;
        public int SkillPoints;
        public List<SkillBundleSaveData> Bundles;
        public List<SkillCategorySaveData> Categories;

        internal void Normalize()
        {
            Version = 3;
            if (Bundles == null)
            {
                Bundles = new List<SkillBundleSaveData>();
            }
            for (int i = 0; i < Bundles.Count; i++)
            {
                Bundles[i].Normalize();
            }
            if (Categories == null)
            {
                Categories = new List<SkillCategorySaveData>();
            }
            Category[] types = Enums<Category>.Greater(Category.Invalid);
            for (int i = 0; i < types.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < Categories.Count; j++)
                {
                    if (Categories[j].Category == (int)types[i])
                    {
                        Categories[j].Level = Math.Max(1, Categories[j].Level);
                        Categories[j].Exp = Math.Max(0, Categories[j].Exp);
                        Categories[j].GameplayExpRemainder = Math.Max(
                            0.0,
                            Math.Min(0.999999999, Categories[j].GameplayExpRemainder));
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Categories.Add(new SkillCategorySaveData { Category = (int)types[i], Level = 1, Exp = 0 });
                }
            }
        }
    }

    [Serializable]
    internal sealed class SkillCategorySaveData
    {
        public int Category;
        public int Level = 1;
        public int Exp;
        public double GameplayExpRemainder;
    }

    [Serializable]
    internal sealed class SkillBundleSaveData
    {
        public int Category = (int)Shared.Skill.Category.Invalid;
        public string SkillId;
        public List<SkillLevelSaveData> Levels;

        internal void Normalize()
        {
            if (Levels == null)
            {
                Levels = new List<SkillLevelSaveData>();
            }
        }
    }

    [Serializable]
    internal sealed class SkillLevelSaveData
    {
        public string SubId;
        public int Level;
    }
}
