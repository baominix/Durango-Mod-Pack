using System;
using System.Collections.Generic;
using System.Reflection;
using Shared.Chat;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    internal delegate void ChatCommandHandler(string[] args);

    internal sealed class ChatCommandDefinition
    {
        internal string Name;
        internal string Usage;
        internal string Description;
        internal ChatCommandHandler Handler;
    }

    internal static class ChatCommandRegistry
    {
        private static readonly Dictionary<string, ChatCommandDefinition> Commands = new Dictionary<string, ChatCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        private static bool _registered;

        internal static void RegisterDefaults()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;

            Register("help", "/help", "Show available commands.", ShowHelp);
            Register("helps", "/helps", "Show available commands.", ShowHelp);
            Register("xp", "/xp <target> <add|set> <amount>", "Modify character or skill category XP.", ModifyExperience);
            Register("combatstat", "/combatstat", "Show current combat formula values.", CombatStatCommand.Execute);
            Register("cstat", "/cstat", "Show current combat formula values.", CombatStatCommand.Execute);
            Register("givepioneer", "/givepioneer [prototype] [level] [count] [tagLevel]", "Give test items with the Pioneer Material attribute.", PioneerItemCommand.Execute);
            Register("gpioneer", "/gpioneer [prototype] [level] [count] [tagLevel]", "Give test items with the Pioneer Material attribute.", PioneerItemCommand.Execute);
            Register("walk", "/walk <speed>", "Set player walk speed multiplier (1 = default).", WalkSpeedCommand.Execute);
            Register("walkspeed", "/walkspeed <speed>", "Set player walk speed multiplier (1 = default).", WalkSpeedCommand.Execute);
            Register("kill", "/kill me|myself", "Kill the local player.", KillCommand.Execute);
            Register("gamemode", "/gamemode <survival|creative>", "Change the offline game mode.", GameModeCommand.Execute);
            Register("gm", "/gm <s|c|0|1>", "Change the offline game mode.", GameModeCommand.Execute);
        }

        private static void Register(string name, string usage, string description, ChatCommandHandler handler)
        {
            Commands[name] = new ChatCommandDefinition
            {
                Name = name,
                Usage = usage,
                Description = description,
                Handler = handler
            };
        }

        internal static bool TryExecute(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            string text = message.Trim();
            if (!text.StartsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = text.Substring(1).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            ChatCommandDefinition command;
            if (!Commands.TryGetValue(parts[0], out command))
            {
                return false;
            }

            string[] args = new string[Math.Max(0, parts.Length - 1)];
            if (args.Length > 0)
            {
                Array.Copy(parts, 1, args, 0, args.Length);
            }

            try
            {
                command.Handler(args);
            }
            catch (Exception exception)
            {
                ChatCommandPlugin.Log.LogWarning("Command /" + command.Name + " failed: " + exception);
                Reply(ChatCommandLocalization.Get("command_failed", exception.Message));
            }
            return true;
        }

        private static void ShowHelp(string[] args)
        {
            Reply(ChatCommandLocalization.Get("available_commands"));

            Reply("ChatCommandPlugin:");
            Reply(Yellow("/help") + ", " + Yellow("/helps") +
                " - " + ChatCommandLocalization.Get("show_commands"));
            Reply(Yellow("/xp") + " <amount> - " +
                ChatCommandLocalization.Get("add_character_xp"));
            Reply(Yellow("/xp") +
                " level <add|set> <amount> - " +
                ChatCommandLocalization.Get("character_xp"));
            Reply(Yellow("/xp") +
                " <category> <add|set> <amount> - " +
                ChatCommandLocalization.Get("skill_category_xp"));
            Reply(Yellow("/xp") +
                " category all <add|set> <amount> - " +
                ChatCommandLocalization.Get("all_skill_categories"));
            Reply(Yellow("/combatstat") + ", " + Yellow("/cstat") +
                " - " + ChatCommandLocalization.Get("show_combat_stats"));
            Reply(Yellow("/givepioneer") + ", " + Yellow("/gpioneer") +
                " [prototype] [level] [count] [tagLevel] - " +
                ChatCommandLocalization.Get("give_pioneer_desc"));
            Reply(Yellow("/walk") +
                " <speed> - " + ChatCommandLocalization.Get("walk_desc"));
            Reply(Yellow("/walk") + " speed <value>, " +
                Yellow("/walkspeed") + " <speed>, " +
                Yellow("/walk") + " reset - " +
                ChatCommandLocalization.Get("walk_variants"));
            Reply(Yellow("/kill") +
                " me|myself - " + ChatCommandLocalization.Get("kill_desc"));
            Reply(Yellow("/gamemode") + " <survival|creative>, " +
                Yellow("/gm") +
                " <s|c|0|1> - " + ChatCommandLocalization.Get("gamemode_desc"));
            Reply(ChatCommandLocalization.Get("xp_categories") +
                " survival, melee, ranged, defense, butchery, gathering, cooking, weapon, tailoring, construction, farming, processing.");

            if (FindType(
                "Baominix.DurangoOriginal.DeveloperMode.DeveloperCommandRouter") != null)
            {
                Reply("DeveloperModePlugin:");
                Reply(Yellow("/dev") + ", " + Yellow("/developermode") +
                    " <on|off|toggle|status|reset|help> - " +
                    ChatCommandLocalization.Get("developer_mode_desc"));
                Reply(Yellow("/dev") +
                    " attackalert <on|off|toggle|status> - " +
                    ChatCommandLocalization.Get("attack_alert_desc"));
                Reply(Yellow("/dev") +
                    " animalbubble <on|off|toggle|status> - " +
                    ChatCommandLocalization.Get("animal_bubble_desc"));
                Reply(Yellow("/hp") +
                    " <amount> - " + ChatCommandLocalization.Get("hp_desc"));
                Reply(Yellow("/sp") +
                    " <amount> - " + ChatCommandLocalization.Get("sp_desc"));
                Reply(Yellow("/combatspawn") +
                    " [type] [level] [rows columns] [spacing] - " +
                    ChatCommandLocalization.Get("combatspawn_desc"));
                Reply(Yellow("/combatwave") +
                    " [type] [level] [count] [spacing] - " +
                    ChatCommandLocalization.Get("combatwave_desc"));
                Reply(Yellow("/combatstatus") +
                    " - " + ChatCommandLocalization.Get("combatstatus_desc"));
                Reply(Yellow("/combatcontext") +
                    " [nearest|all|entityId] - " +
                    ChatCommandLocalization.Get("combatcontext_desc"));
                Reply(Yellow("/combatintent") +
                    " [nearest|all|entityId] - " +
                    ChatCommandLocalization.Get("combatintent_desc"));
                Reply(Yellow("/combathelp") +
                    " - " + ChatCommandLocalization.Get("combathelp_desc"));
            }
        }

        private static string Yellow(string command)
        {
            return command;
        }

        private static void ModifyExperience(string[] args)
        {
            string target;
            string operation;
            int amount;
            bool allCategories = false;
            if (args.Length == 1 && int.TryParse(args[0], out amount) && amount > 0)
            {
                target = "level";
                operation = "add";
            }
            else if (args.Length == 3 && int.TryParse(args[2], out amount) && amount >= 0)
            {
                target = args[0];
                operation = args[1];
                allCategories = string.Equals(target, "all", StringComparison.OrdinalIgnoreCase);
            }
            else if (args.Length == 4
                && string.Equals(args[0], "category", StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[1], "all", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[3], out amount) && amount >= 0)
            {
                target = "all";
                operation = args[2];
                allCategories = true;
            }
            else
            {
                Reply(ChatCommandLocalization.Get("usage_prefix", "/xp level <add|set> <amount>"));
                Reply(ChatCommandLocalization.Get("or_prefix",
                    "/xp <category> <add|set> <amount>"));
                Reply(ChatCommandLocalization.Get("or_prefix",
                    "/xp category all <add|set> <amount>"));
                return;
            }

            bool isCharacter = string.Equals(target, "level", StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, "lv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, "character", StringComparison.OrdinalIgnoreCase);

            string typeName = isCharacter
                ? "BaoX.DurangoOriginal.PlayerProgressionMod.PlayerProgressionApi"
                : "BaoX.DurangoOriginal.SkillSystemMod.SkillSystemApi";
            string methodName = isCharacter ? "ModifyExperience" : (allCategories ? "ModifyAllCategoryExperience" : "ModifyCategoryExperience");
            object[] parameters = isCharacter
                ? new object[] { operation, amount, null }
                : (allCategories
                    ? new object[] { operation, amount, null }
                    : new object[] { target, operation, amount, null });

            Type apiType = FindType(typeName);
            MethodInfo method = apiType == null ? null : apiType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                Reply(ChatCommandLocalization.Get("plugin_unavailable", isCharacter ? "PlayerProgressionPlugin" : "SkillSystemPlugin"));
                return;
            }

            bool success = (bool)method.Invoke(null, parameters);
            string response = parameters[parameters.Length - 1] as string;
            Reply(string.IsNullOrEmpty(response)
                ? ChatCommandLocalization.Get(success ? "xp_updated" : "xp_update_failed")
                : ChatCommandLocalization.TranslateExternalResponse(response));
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        internal static void Reply(string text)
        {
            SocialSystem social = GameSystem<SocialSystem>.Instance();
            if (social != null)
            {
                social.AddSystemChat(text, string.Empty, false, ChannelType.System);
            }
        }
    }
}
