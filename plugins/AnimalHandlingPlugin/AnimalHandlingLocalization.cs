using System;
using System.Collections.Generic;

namespace AnimalHandlingPlugin
{
    internal static class AnimalHandlingLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static AnimalHandlingLocalization()
        {
            Add("title",
                "Animal Handling", "동물 관리", "Manejo de animales", "Manejo de animais",
                "Penanganan Hewan", "Обращение с животными", "การดูแลสัตว์",
                "Tierhaltung", "Gestion des animaux", "動物管理");
            Add("no_taming_material",
                "Taming Material was not found.",
                "길들이기 재료를 찾을 수 없습니다.",
                "No se encontró material de domesticación.",
                "Material de domesticação não encontrado.",
                "Material penjinakan tidak ditemukan.",
                "Материал для приручения не найден.",
                "ไม่พบวัตถุดิบสำหรับฝึกสัตว์",
                "Kein Zähmungsmaterial gefunden.",
                "Aucun matériau d'apprivoisement trouvé.",
                "找不到馴服材料。");
            Add("no_animal_data",
                "This item has no animal data.",
                "이 아이템에는 동물 데이터가 없습니다.",
                "Este objeto no contiene datos de animal.",
                "Este item não possui dados de animal.",
                "Item ini tidak memiliki data hewan.",
                "В этом предмете нет данных животного.",
                "ไอเทมนี้ไม่มีข้อมูลสัตว์",
                "Dieser Gegenstand enthält keine Tierdaten.",
                "Cet objet ne contient aucune donnée d'animal.",
                "此道具沒有動物資料。");
            Add("slots_full",
                "The animal handling slots are full.",
                "동물 관리 슬롯이 가득 찼습니다.",
                "Los espacios de manejo de animales están llenos.",
                "Os espaços de manejo de animais estão cheios.",
                "Slot penanganan hewan penuh.",
                "Слоты содержания животных заполнены.",
                "ช่องจัดการสัตว์เต็มแล้ว",
                "Die Tierhaltungsplätze sind voll.",
                "Les emplacements de gestion des animaux sont pleins.",
                "動物管理欄位已滿。");
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
