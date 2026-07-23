namespace BaoX.DurangoOriginal.PlayerProgressionMod
{
    public static class PlayerProgressionApi
    {
        public static bool AddExperience(int amount, out string response)
        {
            return OfflineProgressionHandlers.TryAddExperience(amount, out response);
        }

        public static bool ModifyExperience(string operation, int amount, out string response)
        {
            return OfflineProgressionHandlers.TryModifyExperience(operation, amount, out response);
        }
    }
}
