using System;
using HarmonyLib;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    [HarmonyPatch(typeof(SocialSystem), "Say", new Type[] { typeof(string), typeof(bool) })]
    internal static class SocialSystemSayChatCommandPatch
    {
        private static bool Prefix(string message, bool isDictation)
        {
            return !ChatCommandRegistry.TryExecute(message);
        }
    }

    [HarmonyPatch(typeof(SocialSystem), "Say", new Type[] { typeof(string), typeof(string), typeof(bool) })]
    internal static class SocialSystemConversationChatCommandPatch
    {
        private static bool Prefix(string conversationId, string message, bool isDictation)
        {
            return !ChatCommandRegistry.TryExecute(message);
        }
    }
}
