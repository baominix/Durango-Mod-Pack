using System;
using System.Collections.Generic;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    internal static class HarborLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static HarborLocalization()
        {
            Add("open_personal_failed",
                "Could not open the personal island.",
                "개인 섬을 열 수 없습니다.",
                "No se pudo abrir la isla personal.",
                "Não foi possível abrir a ilha pessoal.",
                "Tidak dapat membuka pulau pribadi.",
                "Не удалось открыть личный остров.",
                "ไม่สามารถเปิดเกาะส่วนตัวได้",
                "Die persönliche Insel konnte nicht geöffnet werden.",
                "Impossible d'ouvrir l'île personnelle.",
                "無法開啟個人島嶼。");
            Add("no_unstable",
                "No unstable island is available to return to.",
                "돌아갈 수 있는 불안정 섬이 없습니다.",
                "No hay ninguna isla inestable a la que regresar.",
                "Não há ilha instável disponível para retornar.",
                "Tidak ada pulau tidak stabil yang dapat dikunjungi kembali.",
                "Нет нестабильного острова, куда можно вернуться.",
                "ไม่มีเกาะไม่เสถียรให้กลับไป",
                "Es ist keine instabile Insel zur Rückkehr verfügbar.",
                "Aucune île instable n'est disponible pour le retour.",
                "沒有可返回的不穩定島嶼。");
            Add("return_warning",
                "<alert_icon/> Return to the most recent unstable island and continue exploring.",
                "<alert_icon/> 가장 최근의 불안정 섬으로 돌아가 탐험을 계속합니다.",
                "<alert_icon/> Regresa a la isla inestable más reciente y continúa explorando.",
                "<alert_icon/> Retorne à ilha instável mais recente e continue explorando.",
                "<alert_icon/> Kembali ke pulau tidak stabil terbaru dan lanjutkan eksplorasi.",
                "<alert_icon/> Вернуться на последний нестабильный остров и продолжить исследование.",
                "<alert_icon/> กลับไปยังเกาะไม่เสถียรล่าสุดและสำรวจต่อ",
                "<alert_icon/> Zur zuletzt besuchten instabilen Insel zurückkehren und weiter erkunden.",
                "<alert_icon/> Retourner sur l'île instable la plus récente et poursuivre l'exploration.",
                "<alert_icon/> 返回最近的不穩定島嶼並繼續探索。");
            Add("restore_failed",
                "Could not restore the unstable-island snapshot.",
                "불안정 섬 스냅샷을 복원할 수 없습니다.",
                "No se pudo restaurar el estado de la isla inestable.",
                "Não foi possível restaurar o estado da ilha instável.",
                "Tidak dapat memulihkan snapshot pulau tidak stabil.",
                "Не удалось восстановить снимок нестабильного острова.",
                "ไม่สามารถกู้สถานะเกาะไม่เสถียรได้",
                "Der Zustand der instabilen Insel konnte nicht wiederhergestellt werden.",
                "Impossible de restaurer l'état de l'île instable.",
                "無法還原不穩定島嶼快照。");
            Add("return",
                "Return", "돌아가기", "Regresar", "Retornar", "Kembali",
                "Вернуться", "กลับ", "Zurück", "Retour", "返回");
            Add("cancel",
                "Cancel", "취소", "Cancelar", "Cancelar", "Batal",
                "Отмена", "ยกเลิก", "Abbrechen", "Annuler", "取消");
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
