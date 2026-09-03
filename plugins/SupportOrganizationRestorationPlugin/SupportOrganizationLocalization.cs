using System;
using System.Collections.Generic;

namespace BaoX.DurangoOriginal.SupportOrganizationRestoration
{
    internal static class SupportOrganizationLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static SupportOrganizationLocalization()
        {
            Add("offline_support",
                "Offline Support: {0}",
                "오프라인 지원: {0}",
                "Apoyo sin conexión: {0}",
                "Suporte offline: {0}",
                "Dukungan Offline: {0}",
                "Автономная поддержка: {0}",
                "การสนับสนุนออฟไลน์: {0}",
                "Offline-Unterstützung: {0}",
                "Soutien hors ligne : {0}",
                "離線支援：{0}");
        }

        private static void Add(string key, params string[] values)
        {
            Texts[key] = values;
        }

        internal static string Get(string key, params object[] args)
        {
            string[] values;
            if (!Texts.TryGetValue(key, out values) || values == null || values.Length == 0)
                return key;

            int index = LocaleIndex(LocalizeSystem.Locale);
            string format = index >= 0 && index < values.Length &&
                !string.IsNullOrEmpty(values[index]) ? values[index] : values[0];
            if (args == null || args.Length == 0) return format;
            try { return string.Format(format, args); }
            catch { return format; }
        }

        private static int LocaleIndex(string locale)
        {
            if (string.Equals(locale, "en_US", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(locale, "ko_KR", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(locale, "es_MX", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(locale, "pt_BR", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(locale, "id_ID", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(locale, "ru_RU", StringComparison.OrdinalIgnoreCase)) return 5;
            if (string.Equals(locale, "th_TH", StringComparison.OrdinalIgnoreCase)) return 6;
            if (string.Equals(locale, "de_DE", StringComparison.OrdinalIgnoreCase)) return 7;
            if (string.Equals(locale, "fr_FR", StringComparison.OrdinalIgnoreCase)) return 8;
            if (string.Equals(locale, "zh_TW", StringComparison.OrdinalIgnoreCase)) return 9;
            return 0;
        }
    }
}
