using System;
using System.Collections.Generic;

namespace BaoX.DurangoOriginal.MobilePCUISwitch
{
    internal static class MobileUiLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static MobileUiLocalization()
        {
            Add("ui_mode", "UI Mode", "UI 모드", "Modo de UI", "Modo da UI", "Mode UI",
                "Режим интерфейса", "รูปแบบ UI", "UI-Modus", "Mode d'interface", "UI 模式");
            Add("mobile", "Mobile", "모바일", "Móvil", "Mobile", "Mobile",
                "Мобильный", "มือถือ", "Mobil", "Mobile", "行動版");
            Add("pc", "PC", "PC", "PC", "PC", "PC", "ПК", "พีซี", "PC", "PC", "PC");
            Add("confirm_title", "Change UI Mode to {0}?", "UI 모드를 {0}(으)로 변경하시겠습니까?",
                "¿Cambiar el modo de UI a {0}?", "Alterar o modo da UI para {0}?",
                "Ubah mode UI ke {0}?", "Изменить режим интерфейса на {0}?",
                "เปลี่ยนรูปแบบ UI เป็น {0}?", "UI-Modus zu {0} ändern?",
                "Changer le mode d'interface en {0} ?", "將 UI 模式變更為 {0}？");
            Add("confirm_detail", "The current scene will reload to replace the UI set.",
                "UI를 교체하기 위해 현재 장면을 다시 불러옵니다.",
                "La escena actual se recargará para reemplazar el conjunto de UI.",
                "A cena atual será recarregada para substituir o conjunto de UI.",
                "Scene saat ini akan dimuat ulang untuk mengganti set UI.",
                "Текущая сцена будет перезагружена для смены интерфейса.",
                "เกมจะโหลดฉากปัจจุบันใหม่เพื่อเปลี่ยนชุด UI",
                "Die aktuelle Szene wird neu geladen, um das UI-Set zu wechseln.",
                "La scène actuelle sera rechargée pour remplacer l'interface.",
                "目前場景將重新載入以切換 UI 組。");
            Add("confirm", "Confirm", "확인", "Confirmar", "Confirmar", "Konfirmasi",
                "Подтвердить", "ยืนยัน", "Bestätigen", "Confirmer", "確認");
            Add("cancel", "Cancel", "취소", "Cancelar", "Cancelar", "Batal",
                "Отмена", "ยกเลิก", "Abbrechen", "Annuler", "取消");
            Add("status_ready", "UI mode ready: {0}", "UI 모드 준비 완료: {0}",
                "Modo de UI listo: {0}", "Modo da UI pronto: {0}", "Mode UI siap: {0}",
                "Режим интерфейса готов: {0}", "รูปแบบ UI พร้อมใช้งาน: {0}",
                "UI-Modus bereit: {0}", "Mode d'interface prêt : {0}", "UI 模式已準備：{0}");
            Add("status_changed", "UI mode changed to {0}. Rebuild UI to replace loaded prefabs.",
                "UI 모드가 {0}(으)로 변경되었습니다. 로드된 프리팹을 교체하려면 UI를 다시 빌드하세요.",
                "Modo de UI cambiado a {0}. Reconstruye la UI para reemplazar los prefabs cargados.",
                "Modo da UI alterado para {0}. Reconstrua a UI para substituir os prefabs carregados.",
                "Mode UI diubah ke {0}. Rebuild UI untuk mengganti prefab yang sudah dimuat.",
                "Режим интерфейса изменён на {0}. Перестройте UI, чтобы заменить загруженные префабы.",
                "เปลี่ยนรูปแบบ UI เป็น {0} แล้ว ให้สร้าง UI ใหม่เพื่อแทนที่ prefab ที่โหลดไว้",
                "UI-Modus auf {0} geändert. UI neu aufbauen, um geladene Prefabs zu ersetzen.",
                "Mode d'interface changé en {0}. Reconstruisez l'UI pour remplacer les prefabs chargés.",
                "UI 模式已變更為 {0}。請重建 UI 以替換已載入的 prefab。");
            Add("panel_title", "Durango Mobile / PC UI", "Durango 모바일 / PC UI",
                "UI Móvil / PC de Durango", "UI Mobile / PC do Durango",
                "UI Mobile / PC Durango", "Durango: мобильный / ПК интерфейс",
                "Durango UI มือถือ / พีซี", "Durango Mobil-/PC-UI",
                "UI Mobile / PC de Durango", "Durango 行動版 / PC UI");
            Add("requested_mode", "Requested mode: {0}", "요청 모드: {0}",
                "Modo solicitado: {0}", "Modo solicitado: {0}", "Mode diminta: {0}",
                "Запрошенный режим: {0}", "รูปแบบที่เลือก: {0}", "Angeforderter Modus: {0}",
                "Mode demandé : {0}", "要求的模式：{0}");
            Add("active_scene", "Active scene: {0}", "현재 장면: {0}", "Escena activa: {0}",
                "Cena ativa: {0}", "Scene aktif: {0}", "Текущая сцена: {0}",
                "ฉากปัจจุบัน: {0}", "Aktive Szene: {0}", "Scène active : {0}", "目前場景：{0}");
            Add("apply_rebuild", "Apply and rebuild UI", "적용하고 UI 다시 빌드",
                "Aplicar y reconstruir UI", "Aplicar e reconstruir UI",
                "Terapkan dan rebuild UI", "Применить и перестроить UI",
                "ใช้ค่าและสร้าง UI ใหม่", "Anwenden und UI neu aufbauen",
                "Appliquer et reconstruire l'UI", "套用並重建 UI");
            Add("rebuild_after_setting", "Rebuild after changing the native setting",
                "기본 설정 변경 후 다시 빌드", "Reconstruir tras cambiar el ajuste nativo",
                "Reconstruir após alterar a configuração nativa",
                "Rebuild setelah mengubah pengaturan native",
                "Перестраивать после изменения системной настройки",
                "สร้างใหม่หลังเปลี่ยนการตั้งค่าของเกม",
                "Nach Änderung der nativen Einstellung neu aufbauen",
                "Reconstruire après modification du réglage natif",
                "變更原生設定後重建");
            Add("pc_size_only", "UI size options apply to PC UI only.",
                "UI 크기 옵션은 PC UI에만 적용됩니다.",
                "Las opciones de tamaño de UI solo se aplican a la UI de PC.",
                "As opções de tamanho da UI se aplicam apenas à UI de PC.",
                "Opsi ukuran UI hanya berlaku untuk UI PC.",
                "Параметры размера UI применяются только к интерфейсу ПК.",
                "ตัวเลือกขนาด UI ใช้กับ UI แบบพีซีเท่านั้น",
                "UI-Größenoptionen gelten nur für die PC-UI.",
                "Les options de taille s'appliquent uniquement à l'UI PC.",
                "UI 大小選項僅套用於 PC UI。");
            Add("hotkey_hint", "F6: panel    F7: Mobile / PC", "F6: 패널    F7: 모바일 / PC",
                "F6: panel    F7: Móvil / PC", "F6: painel    F7: Mobile / PC",
                "F6: panel    F7: Mobile / PC", "F6: панель    F7: Мобильный / ПК",
                "F6: แผง    F7: มือถือ / พีซี", "F6: Panel    F7: Mobil / PC",
                "F6 : panneau    F7 : Mobile / PC", "F6：面板    F7：行動版 / PC");
            Add("status_mode_rebuild", "UI mode: {0} - rebuilding...", "UI 모드: {0} - 다시 빌드 중...",
                "Modo de UI: {0} - reconstruyendo...", "Modo da UI: {0} - reconstruindo...",
                "Mode UI: {0} - rebuild...", "Режим UI: {0} — перестроение...",
                "รูปแบบ UI: {0} - กำลังสร้างใหม่...", "UI-Modus: {0} - wird neu aufgebaut...",
                "Mode UI : {0} - reconstruction...", "UI 模式：{0} - 正在重建...");
            Add("status_mode_wait", "UI mode: {0} - rebuild when ready", "UI 모드: {0} - 준비되면 다시 빌드",
                "Modo de UI: {0} - reconstruir cuando esté listo",
                "Modo da UI: {0} - reconstruir quando estiver pronto",
                "Mode UI: {0} - rebuild saat siap", "Режим UI: {0} — перестроить, когда будет готово",
                "รูปแบบ UI: {0} - สร้างใหม่เมื่อพร้อม", "UI-Modus: {0} - neu aufbauen, sobald bereit",
                "Mode UI : {0} - reconstruire une fois prêt", "UI 模式：{0} - 準備完成後重建");
            Add("cannot_rebuild", "Cannot rebuild: active scene is not ready.",
                "다시 빌드할 수 없습니다. 현재 장면이 준비되지 않았습니다.",
                "No se puede reconstruir: la escena activa no está lista.",
                "Não é possível reconstruir: a cena ativa não está pronta.",
                "Tidak dapat rebuild: scene aktif belum siap.",
                "Невозможно перестроить: текущая сцена не готова.",
                "ไม่สามารถสร้างใหม่ได้: ฉากปัจจุบันยังไม่พร้อม",
                "Neuaufbau nicht möglich: aktive Szene ist nicht bereit.",
                "Impossible de reconstruire : la scène active n'est pas prête.",
                "無法重建：目前場景尚未準備完成。");
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
            string format = index >= 0 && index < values.Length && !string.IsNullOrEmpty(values[index])
                ? values[index]
                : values[0];
            if (args == null || args.Length == 0) return format;
            try { return string.Format(format, args); }
            catch { return format; }
        }

        internal static string ModeName(DurangoUIMode mode)
        {
            return Get(mode == DurangoUIMode.Mobile ? "mobile" : "pc");
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
