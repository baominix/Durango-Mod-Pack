using System;
using Durango.Network;
using Messages;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    internal static class PioneerItemCommand
    {
        internal static void Execute(string[] args)
        {
            if (args.Length > 4)
            {
                Usage();
                return;
            }

            string prototype = args.Length >= 1 ? args[0] : "clam_product";
            int level = 1;
            int count = 10;
            int tagLevel = 1;
            if ((args.Length >= 2 && !int.TryParse(args[1], out level)) ||
                (args.Length >= 3 && !int.TryParse(args[2], out count)) ||
                (args.Length >= 4 && !int.TryParse(args[3], out tagLevel)) ||
                string.IsNullOrEmpty(prototype) || level < 1 || level > 100 ||
                count < 1 || count > 200 || tagLevel < 1 || tagLevel > 8)
            {
                Usage();
                return;
            }

            if (Connections.Frontend == null)
            {
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("pioneer_enter"));
                return;
            }

            Connections.Frontend.Send<Cheat>(new Cheat
            {
                _Cheat = "pioneer_it " + prototype + " " + level + " " + count + " " + tagLevel
            }, false, 0U);
            ChatCommandRegistry.Reply(
                ChatCommandLocalization.Get("pioneer_requested",
                    count, prototype, level, tagLevel));
        }

        private static void Usage()
        {
            ChatCommandRegistry.Reply(
                ChatCommandLocalization.Get("usage_prefix",
                    "/givepioneer [prototype] [level 1-100] [count 1-200] [tagLevel 1-8]"));
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("example_prefix", "/givepioneer flax 15 10 1"));
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("pioneer_noargs"));
        }
    }
}
