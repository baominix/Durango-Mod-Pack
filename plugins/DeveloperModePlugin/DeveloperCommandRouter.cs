using System;
using Durango.UI;
using HarmonyLib;
using Shared.Chat;

namespace Baominix.DurangoOriginal.DeveloperMode
{
    internal static class DeveloperCommandRouter
    {
        internal static bool TryExecute(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            string[] parts = message.Trim().Split(
                new char[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            string command = parts[0].ToLowerInvariant();
            if (command == "/dev" || command == "/developermode")
            {
                ExecuteDeveloperMode(parts);
                return true;
            }

            if (!CombatTestCommands.IsCommand(command))
            {
                return false;
            }
            if (!DeveloperModePlugin.IsEnabled)
            {
                Reply(DeveloperModeLocalization.Get("disabled"));
                return true;
            }

            try
            {
                CombatTestCommands.Execute(command, parts);
            }
            catch (Exception exception)
            {
                if (DeveloperModePlugin.Log != null)
                {
                    DeveloperModePlugin.Log.LogWarning(
                        "Developer command failed: " + exception);
                }
                Reply(DeveloperModeLocalization.Get("command_failed", exception.Message));
            }
            return true;
        }

        private static void ExecuteDeveloperMode(string[] parts)
        {
            if (parts.Length == 1 || Is(parts[1], "status"))
            {
                ReplyStatus();
                return;
            }
            if (Is(parts[1], "help"))
            {
                ReplyHelp();
                return;
            }
            if (Is(parts[1], "on") || Is(parts[1], "off") ||
                Is(parts[1], "toggle"))
            {
                bool enabled = Is(parts[1], "toggle")
                    ? !DeveloperModePlugin.IsEnabled
                    : Is(parts[1], "on");
                DeveloperModePlugin.SetEnabled(enabled);
                Reply(DeveloperModeLocalization.Get("mode_status", OnOff(enabled)));
                Reply(DeveloperModeLocalization.Get("attack_alert",
                    OnOff(global::CombatSystem.AttackAlertEnabled)));
                return;
            }
            if (Is(parts[1], "reset"))
            {
                DeveloperModePlugin.ResetToggles();
                Reply(DeveloperModeLocalization.Get("toggles_reset"));
                ReplyStatus();
                return;
            }
            if (Is(parts[1], "attackalert"))
            {
                ExecuteAttackAlert(parts);
                return;
            }
            if (Is(parts[1], "animalbubble"))
            {
                ExecuteAnimalBubble(parts);
                return;
            }

            ReplyHelp();
        }

        private static void ExecuteAttackAlert(string[] parts)
        {
            if (parts.Length == 2 || Is(parts[2], "status"))
            {
                Reply(DeveloperModeLocalization.Get("attack_config_runtime",
                    OnOff(DeveloperModePlugin.AttackAlert.Value),
                    OnOff(global::CombatSystem.AttackAlertEnabled)));
                return;
            }
            if (!DeveloperModePlugin.IsEnabled)
            {
                Reply(DeveloperModeLocalization.Get("disabled"));
                return;
            }
            if (!Is(parts[2], "on") && !Is(parts[2], "off") &&
                !Is(parts[2], "toggle"))
            {
                Reply(DeveloperModeLocalization.Get("usage", "/dev attackalert <on|off|toggle|status>"));
                return;
            }

            bool value = Is(parts[2], "toggle")
                ? !DeveloperModePlugin.AttackAlert.Value
                : Is(parts[2], "on");
            DeveloperModePlugin.SetAttackAlert(value);
            Reply(DeveloperModeLocalization.Get("attack_alert",
                OnOff(global::CombatSystem.AttackAlertEnabled)));
        }

        private static void ExecuteAnimalBubble(string[] parts)
        {
            if (parts.Length == 2 || Is(parts[2], "status"))
            {
                ReplyAnimalBubbleStatus();
                return;
            }
            if (!DeveloperModePlugin.IsEnabled)
            {
                Reply(DeveloperModeLocalization.Get("disabled"));
                return;
            }
            if (!Is(parts[2], "on") && !Is(parts[2], "off") &&
                !Is(parts[2], "toggle"))
            {
                Reply(DeveloperModeLocalization.Get("usage", "/dev animalbubble <on|off|toggle|status>"));
                return;
            }

            bool value = Is(parts[2], "toggle")
                ? !DeveloperModePlugin.AnimalBubble.Value
                : Is(parts[2], "on");
            DeveloperModePlugin.SetAnimalBubble(value);
            ReplyAnimalBubbleStatus();
        }

        private static void ReplyAnimalBubbleStatus()
        {
            Reply(DeveloperModeLocalization.Get("animal_config_runtime",
                OnOff(DeveloperModePlugin.AnimalBubble.Value),
                OnOff(DeveloperModePlugin.AnimalBubbleRuntimeActive),
                DeveloperModePlugin.AnimalBubbleRuntimeAvailable
                    ? "."
                    : DeveloperModeLocalization.Get("combat_unavailable_suffix")));
        }

        private static void ReplyStatus()
        {
            Reply(DeveloperModeLocalization.Get("mode_status",
                OnOff(DeveloperModePlugin.IsEnabled)));
            Reply(DeveloperModeLocalization.Get("attack_config_status",
                OnOff(DeveloperModePlugin.AttackAlert.Value),
                OnOff(global::CombatSystem.AttackAlertEnabled)));
            ReplyAnimalBubbleStatus();
        }

        private static void ReplyHelp()
        {
            Reply(DeveloperModeLocalization.Get("dev_commands"));
            Reply("/dev <on|off|toggle|status|reset>");
            Reply("/dev attackalert <on|off|toggle|status>");
            Reply("/dev animalbubble <on|off|toggle|status>");
            Reply("/hp <amount>, /sp <amount>");
            Reply(
                "/combatspawn [type] [level] " +
                "[rows columns] [spacing]");
            Reply("/combatwave [type] [level] [count] [spacing]");
            Reply(
                "/combatstatus, " +
                "/combatcontext [nearest|all|entityId]");
            Reply(
                "/combatintent [nearest|all|entityId], /combathelp");
        }

        private static bool Is(string value, string expected)
        {
            return string.Equals(
                value,
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string OnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }

        internal static void Reply(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (GameSystem<SocialSystem>.HasInstance())
            {
                GameSystem<SocialSystem>.Instance().AddSystemChat(
                    text,
                    DeveloperModeLocalization.Get("speaker"),
                    false,
                    ChannelType.System);
            }
            else
            {
                UIManager.SystemMsg(DeveloperModeLocalization.Get("speaker"), text, 4f);
            }
        }
    }

    [HarmonyPatch(
        typeof(SocialSystem),
        "Say",
        new Type[] { typeof(string), typeof(bool) })]
    internal static class DeveloperModeSayCommandPatch
    {
        private static bool Prefix(string message)
        {
            return !DeveloperCommandRouter.TryExecute(message);
        }
    }

    [HarmonyPatch(
        typeof(SocialSystem),
        "Say",
        new Type[] { typeof(string), typeof(string), typeof(bool) })]
    internal static class DeveloperModeConversationCommandPatch
    {
        private static bool Prefix(string message)
        {
            return !DeveloperCommandRouter.TryExecute(message);
        }
    }
}
