using System;
using System.Collections.Generic;

namespace BaoX.DurangoOriginal.TamedIslandRestoration
{
    internal static class TamedIslandLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static TamedIslandLocalization()
        {
            Add("default_island_name",
                "Tamed Grassland Island",
                "길들인 초원 섬",
                "Isla de pradera domesticada",
                "Ilha de pradaria domesticada",
                "Pulau Padang Rumput Jinak",
                "Прирученный луговой остров",
                "เกาะทุ่งหญ้าเชื่อง",
                "Gezähmte Graslandinsel",
                "Île de prairie apprivoisée",
                "馴化草原島");
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

        internal static string ResolveIslandName(string value)
        {
            return string.Equals(value, "Tamed Grassland Island", StringComparison.Ordinal)
                ? Get("default_island_name")
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
