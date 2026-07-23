namespace BaoX.DurangoOriginal.SkillSystemMod
{
    public static class SkillSystemApi
    {
        public static bool ModifyCategoryExperience(string category, string operation, int amount, out string response)
        {
            return OfflineSkillHandlers.TryModifyCategoryExperience(category, operation, amount, out response);
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
