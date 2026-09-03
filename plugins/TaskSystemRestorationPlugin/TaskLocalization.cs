using System;
using System.Collections.Generic;

namespace BaoX.DurangoOriginal.TaskSystemRestoration
{
    internal static class TaskLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static TaskLocalization()
        {
            Add("permanent", "Permanent Tasks", "상시 과제", "Tareas permanentes", "Tarefas permanentes",
                "Tugas Permanen", "Постоянные задания", "ภารกิจถาวร",
                "Dauerhafte Aufgaben", "Tâches permanentes", "永久任務");
            Add("daily", "Daily Tasks", "일일 과제", "Tareas diarias", "Tarefas diárias",
                "Tugas Harian", "Ежедневные задания", "ภารกิจรายวัน",
                "Tägliche Aufgaben", "Tâches quotidiennes", "每日任務");
            Add("weekly", "Weekly Tasks", "주간 과제", "Tareas semanales", "Tarefas semanais",
                "Tugas Mingguan", "Еженедельные задания", "ภารกิจรายสัปดาห์",
                "Wöchentliche Aufgaben", "Tâches hebdomadaires", "每週任務");
            Add("story", "Story", "스토리", "Historia", "História", "Cerita",
                "История", "เนื้อเรื่อง", "Story", "Histoire", "故事");
        }

        private static void Add(string key, params string[] values) { Texts[key] = values; }

        internal static string Get(string key)
        {
            string[] values;
            if (!Texts.TryGetValue(key, out values) || values == null || values.Length == 0) return key;
            int i = LocaleIndex(LocalizeSystem.Locale);
            return i >= 0 && i < values.Length && !string.IsNullOrEmpty(values[i]) ? values[i] : values[0];
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
