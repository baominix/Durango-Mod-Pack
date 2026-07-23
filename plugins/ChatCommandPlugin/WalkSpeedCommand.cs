using System;
using System.Globalization;
using Durango.Utils;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    internal static class WalkSpeedCommand
    {
        private const float MinimumMultiplier = 0.1f;
        private const float MaximumMultiplier = 10f;

        internal static void Execute(string[] args)
        {
            string valueText;
            if (!TryGetValueText(args, out valueText))
            {
                ShowUsage();
                return;
            }

            PlayerController controller = GetController();
            if (controller == null)
            {
                ChatCommandRegistry.Reply("Enter a character before using /walk.");
                return;
            }

            if (string.IsNullOrEmpty(valueText))
            {
                ChatCommandRegistry.Reply(
                    "Current walk speed: " + Format(controller.CheatMoveSpeedMultiply) + "x (1 = default).");
                ShowUsage();
                return;
            }

            float multiplier;
            if (string.Equals(valueText, "reset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valueText, "default", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1f;
            }
            else if (!TryParseMultiplier(valueText, out multiplier))
            {
                ChatCommandRegistry.Reply("Walk speed must be a number from 0.1 to 10.");
                ShowUsage();
                return;
            }

            controller.CheatMoveSpeedMultiply = multiplier;
            ChatCommandRegistry.Reply(
                "Walk speed set to " + Format(multiplier) + "x (1 = default).");
        }

        private static bool TryGetValueText(string[] args, out string valueText)
        {
            valueText = null;
            if (args == null || args.Length == 0)
            {
                return true;
            }
            if (args.Length == 1)
            {
                if (!string.Equals(args[0], "speed", StringComparison.OrdinalIgnoreCase))
                {
                    valueText = args[0];
                }
                return true;
            }
            if (args.Length == 2 && string.Equals(args[0], "speed", StringComparison.OrdinalIgnoreCase))
            {
                valueText = args[1];
                return true;
            }
            return false;
        }

        private static bool TryParseMultiplier(string text, out float multiplier)
        {
            bool parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out multiplier);
            return parsed
                && !float.IsNaN(multiplier)
                && !float.IsInfinity(multiplier)
                && multiplier >= MinimumMultiplier
                && multiplier <= MaximumMultiplier;
        }

        private static PlayerController GetController()
        {
            if (!Singleton<PlayerController>.HasInstance() || PlayerBehavior.LocalPlayer == null)
            {
                return null;
            }
            return Singleton<PlayerController>.Instance();
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static void ShowUsage()
        {
            ChatCommandRegistry.Reply("Usage: /walk <speed> or /walk speed <value>");
            ChatCommandRegistry.Reply("Range: 0.1 to 10. Use /walk 1 or /walk reset for default speed.");
        }
    }
}
