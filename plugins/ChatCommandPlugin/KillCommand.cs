using System;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    internal static class KillCommand
    {
        internal static void Execute(string[] args)
        {
            if (!IsSelfTarget(args))
            {
                ShowUsage();
                return;
            }

            PlayerBehavior player = PlayerBehavior.LocalPlayer;
            if (player == null)
            {
                ChatCommandRegistry.Reply("Enter a character before using /kill.");
                return;
            }

            if (!player.IsAlive)
            {
                ChatCommandRegistry.Reply("You are already dead.");
                return;
            }

            float maxLife = GetSafeMaxLife(player);
            Gauge zeroLife = new Gauge(maxLife, 0f, new GaugeNode[]
            {
                new GaugeNode(Gauge.CurrentTime, 0f)
            });

            player.SetSurvivalGauge(zeroLife, null);
            player.SetAlive(false, false);

            ChatCommandRegistry.Reply("Killed local player.");
        }

        private static bool IsSelfTarget(string[] args)
        {
            if (args == null || args.Length != 1)
            {
                return false;
            }

            return string.Equals(args[0], "me", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "myself", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetSafeMaxLife(PlayerBehavior player)
        {
            try
            {
                if (player != null && player.Life != null)
                {
                    float max = player.Life.RealMax();
                    if (max > 0f)
                    {
                        return max;
                    }

                    max = player.Life.Max();
                    if (max > 0f)
                    {
                        return max;
                    }
                }
            }
            catch (Exception exception)
            {
                ChatCommandPlugin.Log.LogWarning("Unable to read player max life: " + exception.Message);
            }

            return 100f;
        }

        private static void ShowUsage()
        {
            ChatCommandRegistry.Reply("Usage: /kill me");
            ChatCommandRegistry.Reply("Also supported: /kill myself");
        }
    }
}
