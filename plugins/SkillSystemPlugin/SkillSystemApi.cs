namespace BaoX.DurangoOriginal.SkillSystemMod
{
    public static class SkillSystemApi
    {
        public static bool GetCraftBuildUnlockState(
            out string[] allRecipeIds,
            out string[] unlockedRecipeIds,
            out string[] allBlueprintIds,
            out string[] unlockedBlueprintIds)
        {
            return OfflineSkillHandlers.TryGetCraftBuildUnlockState(
                out allRecipeIds,
                out unlockedRecipeIds,
                out allBlueprintIds,
                out unlockedBlueprintIds);
        }

        public static bool ModifyCategoryExperience(string category, string operation, int amount, out string response)
        {
            return OfflineSkillHandlers.TryModifyCategoryExperience(category, operation, amount, out response);
        }

        public static bool AddCategoryExperienceFromGameplay(string category, double amount, out string response)
        {
            return OfflineSkillHandlers.TryAddGameplayCategoryExperience(category, amount, out response);
        }

        public static bool ModifyAllCategoryExperience(string operation, int amount, out string response)
        {
            return OfflineSkillHandlers.TryModifyAllCategoryExperience(operation, amount, out response);
        }

        public static bool RefreshForCharacterLevel(out string response)
        {
            return OfflineSkillHandlers.RefreshForCharacterLevel(out response);
        }

        public static bool SetCategoryLevelForContext(Durango.Offline.PlayerContext context, string category, int level, out string response)
        {
            return OfflineSkillHandlers.TrySetCategoryLevel(context, category, level, out response);
        }
    }
}
