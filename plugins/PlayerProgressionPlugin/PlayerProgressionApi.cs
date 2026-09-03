namespace BaoX.DurangoOriginal.PlayerProgressionMod
{
    public static class PlayerProgressionApi
    {
        public static bool TryGetProgression(
            Durango.Offline.PlayerContext context,
            out int level,
            out int experience)
        {
            level = 1;
            experience = 0;
            if (context == null ||
                !ProgressionPersistence.IsProgressionMode(context))
            {
                return false;
            }

            PlayerProgressionState state =
                ProgressionPersistence.Get(context);
            if (state == null)
            {
                return false;
            }

            level = state.Level;
            experience = state.Experience;
            return true;
        }

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
