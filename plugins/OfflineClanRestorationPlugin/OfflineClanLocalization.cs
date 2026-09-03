using System;
using System.Collections.Generic;

namespace BaoX.DurangoOriginal.OfflineClanRestoration
{
    internal static class OfflineClanLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static OfflineClanLocalization()
        {
            Add("default_clan_name",
                "Durango Clan",
                "듀랑고 부족",
                "Clan Durango",
                "Clã Durango",
                "Clan Durango",
                "Клан Durango",
                "แคลน Durango",
                "Durango-Clan",
                "Clan Durango",
                "Durango 部族");
            Add("default_notice",
                "Offline clan restored.",
                "오프라인 부족이 복원되었습니다.",
                "Clan sin conexión restaurado.",
                "Clã offline restaurado.",
                "Clan offline dipulihkan.",
                "Автономный клан восстановлен.",
                "กู้คืนแคลนออฟไลน์แล้ว",
                "Offline-Clan wiederhergestellt.",
                "Clan hors ligne restauré.",
                "離線部族已恢復。");
            Add("default_intro",
                "A local offline clan.",
                "로컬 오프라인 부족입니다.",
                "Un clan local sin conexión.",
                "Um clã local offline.",
                "Clan offline lokal.",
                "Локальный автономный клан.",
                "แคลนออฟไลน์ภายในเครื่อง",
                "Ein lokaler Offline-Clan.",
                "Un clan local hors ligne.",
                "本機離線部族。");
            Add("leader",
                "Leader",
                "족장",
                "Líder",
                "Líder",
                "Pemimpin",
                "Лидер",
                "หัวหน้า",
                "Anführer",
                "Chef",
                "族長");
            Add("offline",
                "Offline",
                "오프라인",
                "Sin conexión",
                "Offline",
                "Offline",
                "Автономный",
                "ออฟไลน์",
                "Offline",
                "Hors ligne",
                "離線");
        }

        private static void Add(string key, params string[] values)
        {
            Texts[key] = values;
        }

        internal static string Get(string key)
        {
            string[] values;
            if (!Texts.TryGetValue(key, out values) || values == null || values.Length == 0)
                return key;
            int index = LocaleIndex(LocalizeSystem.Locale);
            return index >= 0 && index < values.Length && !string.IsNullOrEmpty(values[index])
                ? values[index]
                : values[0];
        }

        internal static string ResolveDefault(string value, string englishDefault, string key)
        {
            return string.Equals(value, englishDefault, StringComparison.Ordinal)
                ? Get(key)
                : (value ?? string.Empty);
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
