using System;
using System.Collections.Generic;

namespace Baominix.DurangoOriginal.DeveloperMode
{
    internal static class DeveloperModeLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static DeveloperModeLocalization()
        {
            Add("speaker", "Developer", "개발자", "Desarrollador", "Desenvolvedor", "Developer",
                "Разработчик", "นักพัฒนา", "Entwickler", "Développeur", "開發者");
            Add("disabled", "Developer mode is disabled. Use /dev on first.",
                "개발자 모드가 꺼져 있습니다. 먼저 /dev on을 사용하세요.",
                "El modo desarrollador está desactivado. Usa primero /dev on.",
                "O modo de desenvolvedor está desativado. Use /dev on primeiro.",
                "Mode pengembang nonaktif. Gunakan /dev on terlebih dahulu.",
                "Режим разработчика отключён. Сначала используйте /dev on.",
                "โหมดนักพัฒนาปิดอยู่ ให้ใช้ /dev on ก่อน",
                "Der Entwicklermodus ist deaktiviert. Verwende zuerst /dev on.",
                "Le mode développeur est désactivé. Utilisez d'abord /dev on.",
                "開發者模式已關閉，請先使用 /dev on。");
            Add("command_failed", "Developer command failed: {0}", "개발자 명령 실행 실패: {0}",
                "Error al ejecutar el comando de desarrollador: {0}",
                "Falha no comando de desenvolvedor: {0}", "Perintah developer gagal: {0}",
                "Ошибка команды разработчика: {0}", "คำสั่งนักพัฒนาทำงานไม่สำเร็จ: {0}",
                "Entwicklerbefehl fehlgeschlagen: {0}", "Échec de la commande développeur : {0}",
                "開發者指令執行失敗：{0}");
            Add("mode_status", "Developer mode: {0}.", "개발자 모드: {0}.",
                "Modo desarrollador: {0}.", "Modo de desenvolvedor: {0}.",
                "Mode pengembang: {0}.", "Режим разработчика: {0}.",
                "โหมดนักพัฒนา: {0}", "Entwicklermodus: {0}.",
                "Mode développeur : {0}.", "開發者模式：{0}。");
            Add("attack_alert", "Original attack alert: {0}.", "원본 공격 경고: {0}.",
                "Alerta de ataque original: {0}.", "Alerta de ataque original: {0}.",
                "Peringatan serangan asli: {0}.", "Исходное предупреждение атаки: {0}.",
                "การแสดงเตือนการโจมตีแบบดั้งเดิม: {0}", "Original-Angriffswarnung: {0}.",
                "Alerte d'attaque originale : {0}.", "原版攻擊警示：{0}。");
            Add("toggles_reset", "Developer toggles reset to their default values.",
                "개발자 토글을 기본값으로 초기화했습니다.",
                "Los ajustes de desarrollador se restablecieron a sus valores predeterminados.",
                "As opções de desenvolvedor foram redefinidas para os valores padrão.",
                "Toggle developer direset ke nilai default.",
                "Переключатели разработчика сброшены к значениям по умолчанию.",
                "รีเซ็ตตัวเลือกนักพัฒนาเป็นค่าเริ่มต้นแล้ว",
                "Entwicklerschalter wurden auf Standardwerte zurückgesetzt.",
                "Les options développeur ont été réinitialisées.",
                "開發者選項已重設為預設值。");
            Add("attack_config_runtime", "Configured attack alert: {0}; runtime: {1}.",
                "설정된 공격 경고: {0}; 실행 상태: {1}.",
                "Alerta configurada: {0}; ejecución: {1}.",
                "Alerta configurada: {0}; execução: {1}.",
                "Attack alert config: {0}; runtime: {1}.",
                "Настройка предупреждения: {0}; выполнение: {1}.",
                "การตั้งค่าแจ้งเตือนโจมตี: {0}; ขณะทำงาน: {1}",
                "Konfigurierte Angriffswarnung: {0}; Laufzeit: {1}.",
                "Alerte configurée : {0} ; exécution : {1}.",
                "攻擊警示設定：{0}；執行狀態：{1}。");
            Add("animal_config_runtime", "AnimalBubble config/runtime: {0}/{1}{2}",
                "AnimalBubble 설정/실행: {0}/{1}{2}",
                "AnimalBubble config/ejecución: {0}/{1}{2}",
                "AnimalBubble config/execução: {0}/{1}{2}",
                "AnimalBubble config/runtime: {0}/{1}{2}",
                "AnimalBubble настройка/выполнение: {0}/{1}{2}",
                "AnimalBubble ตั้งค่า/ขณะทำงาน: {0}/{1}{2}",
                "AnimalBubble Konfig./Laufzeit: {0}/{1}{2}",
                "AnimalBubble config/exécution : {0}/{1}{2}",
                "AnimalBubble 設定/執行：{0}/{1}{2}");
            Add("combat_unavailable_suffix", " (DurangoCombatSystemPlugin unavailable).",
                " (DurangoCombatSystemPlugin을 사용할 수 없습니다).",
                " (DurangoCombatSystemPlugin no disponible).",
                " (DurangoCombatSystemPlugin indisponível).",
                " (DurangoCombatSystemPlugin tidak tersedia).",
                " (DurangoCombatSystemPlugin недоступен).",
                " (ไม่พบ DurangoCombatSystemPlugin)",
                " (DurangoCombatSystemPlugin nicht verfügbar).",
                " (DurangoCombatSystemPlugin indisponible).",
                "（DurangoCombatSystemPlugin 無法使用）。");
            Add("attack_config_status", "AttackAlert config/runtime: {0}/{1}.",
                "AttackAlert 설정/실행: {0}/{1}.", "AttackAlert config/ejecución: {0}/{1}.",
                "AttackAlert config/execução: {0}/{1}.", "AttackAlert config/runtime: {0}/{1}.",
                "AttackAlert настройка/выполнение: {0}/{1}.", "AttackAlert ตั้งค่า/ขณะทำงาน: {0}/{1}",
                "AttackAlert Konfig./Laufzeit: {0}/{1}.", "AttackAlert config/exécution : {0}/{1}.",
                "AttackAlert 設定/執行：{0}/{1}。");
            Add("dev_commands", "Developer mode commands:", "개발자 모드 명령어:",
                "Comandos del modo desarrollador:", "Comandos do modo de desenvolvedor:",
                "Perintah mode pengembang:", "Команды режима разработчика:",
                "คำสั่งโหมดนักพัฒนา:", "Entwicklermodus-Befehle:",
                "Commandes du mode développeur :", "開發者模式指令：");
            Add("combat_commands", "Combat developer commands:", "전투 개발자 명령어:",
                "Comandos de desarrollo de combate:", "Comandos de desenvolvimento de combate:",
                "Perintah developer pertempuran:", "Команды разработчика для боя:",
                "คำสั่งนักพัฒนาสำหรับการต่อสู้:", "Kampf-Entwicklerbefehle:",
                "Commandes développeur de combat :", "戰鬥開發者指令：");
            Add("usage", "Usage: {0}", "사용법: {0}", "Uso: {0}", "Uso: {0}", "Penggunaan: {0}",
                "Использование: {0}", "วิธีใช้: {0}", "Verwendung: {0}",
                "Utilisation : {0}", "用法：{0}");
            Add("combat_plugin_unavailable", "DurangoCombatSystemPlugin is not available.",
                "DurangoCombatSystemPlugin을 사용할 수 없습니다.",
                "DurangoCombatSystemPlugin no está disponible.",
                "DurangoCombatSystemPlugin não está disponível.",
                "DurangoCombatSystemPlugin tidak tersedia.",
                "DurangoCombatSystemPlugin недоступен.",
                "ไม่พบ DurangoCombatSystemPlugin",
                "DurangoCombatSystemPlugin ist nicht verfügbar.",
                "DurangoCombatSystemPlugin n'est pas disponible.",
                "DurangoCombatSystemPlugin 無法使用。");
            Add("gauge_updated", "Gauge updated.", "게이지가 변경되었습니다.",
                "Indicador actualizado.", "Medidor atualizado.", "Gauge diperbarui.",
                "Шкала обновлена.", "อัปเดตเกจแล้ว", "Anzeige aktualisiert.",
                "Jauge mise à jour.", "計量值已更新。");
            Add("gauge_failed", "Unable to update gauge.", "게이지를 변경할 수 없습니다.",
                "No se pudo actualizar el indicador.", "Não foi possível atualizar o medidor.",
                "Tidak dapat memperbarui gauge.", "Не удалось обновить шкалу.",
                "ไม่สามารถอัปเดตเกจได้", "Anzeige konnte nicht aktualisiert werden.",
                "Impossible de mettre à jour la jauge.", "無法更新計量值。");
            Add("spawn_limit", "Combat spawn grid is limited to 24 animals.",
                "전투 생성 그리드는 최대 24마리까지 가능합니다.",
                "La cuadrícula de aparición está limitada a 24 animales.",
                "A grade de geração é limitada a 24 animais.",
                "Grid spawn pertempuran dibatasi hingga 24 hewan.",
                "Сетка появления ограничена 24 животными.",
                "กริดสร้างสัตว์สำหรับการต่อสู้จำกัดสูงสุด 24 ตัว",
                "Das Kampfraster ist auf 24 Tiere begrenzt.",
                "La grille d'apparition est limitée à 24 animaux.",
                "戰鬥生成網格最多 24 隻動物。");
            Add("spawned_one", "Spawned local combat animal type={0} level={1}.",
                "로컬 전투 동물을 생성했습니다 type={0} level={1}.",
                "Animal de combate generado type={0} level={1}.",
                "Animal de combate gerado type={0} level={1}.",
                "Hewan tempur lokal dibuat type={0} level={1}.",
                "Создано локальное боевое животное type={0} level={1}.",
                "สร้างสัตว์ต่อสู้แล้ว type={0} level={1}",
                "Lokales Kampftier erzeugt type={0} level={1}.",
                "Animal de combat local généré type={0} level={1}.",
                "已生成本機戰鬥動物 type={0} level={1}。");
            Add("spawned_grid", "Spawned combat grid type={0} level={1} rows={2} columns={3} spacing={4} total={5}.",
                "전투 그리드 생성 type={0} level={1} rows={2} columns={3} spacing={4} total={5}.",
                "Cuadrícula generada type={0} level={1} filas={2} columnas={3} espacio={4} total={5}.",
                "Grade gerada type={0} level={1} linhas={2} colunas={3} espaço={4} total={5}.",
                "Grid dibuat type={0} level={1} rows={2} columns={3} spacing={4} total={5}.",
                "Создана сетка type={0} level={1} rows={2} columns={3} spacing={4} total={5}.",
                "สร้างกริดต่อสู้ type={0} level={1} rows={2} columns={3} spacing={4} total={5}",
                "Kampfraster erzeugt type={0} level={1} rows={2} columns={3} spacing={4} total={5}.",
                "Grille générée type={0} level={1} rows={2} columns={3} spacing={4} total={5}.",
                "已生成戰鬥網格 type={0} level={1} rows={2} columns={3} spacing={4} total={5}。");
            Add("spawned_wave", "Spawned combat wave type={0} level={1} count={2}.",
                "전투 웨이브 생성 type={0} level={1} count={2}.",
                "Oleada generada type={0} level={1} cantidad={2}.",
                "Onda gerada type={0} level={1} quantidade={2}.",
                "Gelombang dibuat type={0} level={1} count={2}.",
                "Создана волна type={0} level={1} count={2}.",
                "สร้างเวฟต่อสู้ type={0} level={1} count={2}",
                "Kampfwelle erzeugt type={0} level={1} count={2}.",
                "Vague générée type={0} level={1} count={2}.",
                "已生成戰鬥波次 type={0} level={1} count={2}。");
            Add("combat_status", "Offline combat: actions={0} active={1}.",
                "오프라인 전투: actions={0} active={1}.",
                "Combate offline: actions={0} active={1}.",
                "Combate offline: actions={0} active={1}.",
                "Pertempuran offline: actions={0} active={1}.",
                "Автономный бой: actions={0} active={1}.",
                "การต่อสู้ออฟไลน์: actions={0} active={1}",
                "Offline-Kampf: actions={0} active={1}.",
                "Combat hors ligne : actions={0} active={1}.",
                "離線戰鬥：actions={0} active={1}。");
            Add("no_report", "No Saurus diagnostic report is available.",
                "사용 가능한 사우루스 진단 보고서가 없습니다.",
                "No hay informe de diagnóstico de Saurus disponible.",
                "Nenhum relatório de diagnóstico do Saurus está disponível.",
                "Tidak ada laporan diagnostik Saurus.",
                "Диагностический отчёт Saurus отсутствует.",
                "ไม่มีรายงานวินิจฉัย Saurus",
                "Kein Saurus-Diagnosebericht verfügbar.",
                "Aucun rapport de diagnostic Saurus disponible.",
                "沒有可用的 Saurus 診斷報告。");
            Add("report_failed", "Unable to read Saurus diagnostic report.",
                "사우루스 진단 보고서를 읽을 수 없습니다.",
                "No se pudo leer el informe de diagnóstico de Saurus.",
                "Não foi possível ler o relatório de diagnóstico do Saurus.",
                "Tidak dapat membaca laporan diagnostik Saurus.",
                "Не удалось прочитать диагностический отчёт Saurus.",
                "ไม่สามารถอ่านรายงานวินิจฉัย Saurus ได้",
                "Saurus-Diagnosebericht konnte nicht gelesen werden.",
                "Impossible de lire le rapport de diagnostic Saurus.",
                "無法讀取 Saurus 診斷報告。");
            Add("enter_world", "Enter an offline world before using combat spawn commands.",
                "전투 생성 명령을 사용하기 전에 오프라인 월드로 입장하세요.",
                "Entra a un mundo offline antes de usar los comandos de aparición.",
                "Entre em um mundo offline antes de usar os comandos de geração.",
                "Masuk ke dunia offline sebelum memakai perintah spawn pertempuran.",
                "Войдите в автономный мир перед использованием команд появления.",
                "เข้าโลกออฟไลน์ก่อนใช้คำสั่งสร้างสัตว์ต่อสู้",
                "Betritt eine Offline-Welt, bevor du Kampf-Spawn-Befehle verwendest.",
                "Entrez dans un monde hors ligne avant d'utiliser les commandes d'apparition.",
                "請先進入離線世界再使用戰鬥生成指令。");
            Add("unsupported_type", "Unsupported combat animal type: {0}. Use 2027, 2037, 2039, or 2001.",
                "지원하지 않는 전투 동물 타입: {0}. 2027, 2037, 2039 또는 2001을 사용하세요.",
                "Tipo de animal no compatible: {0}. Usa 2027, 2037, 2039 o 2001.",
                "Tipo de animal não suportado: {0}. Use 2027, 2037, 2039 ou 2001.",
                "Tipe hewan tidak didukung: {0}. Gunakan 2027, 2037, 2039, atau 2001.",
                "Неподдерживаемый тип животного: {0}. Используйте 2027, 2037, 2039 или 2001.",
                "ไม่รองรับประเภทสัตว์ต่อสู้: {0} ใช้ 2027, 2037, 2039 หรือ 2001",
                "Nicht unterstützter Kampftiertyp: {0}. Verwende 2027, 2037, 2039 oder 2001.",
                "Type d'animal non pris en charge : {0}. Utilisez 2027, 2037, 2039 ou 2001.",
                "不支援的戰鬥動物類型：{0}。請使用 2027、2037、2039 或 2001。");
            Add("enter_offline_first", "Enter an offline world first.", "먼저 오프라인 월드로 입장하세요.",
                "Entra primero a un mundo offline.", "Entre primeiro em um mundo offline.",
                "Masuk ke dunia offline terlebih dahulu.", "Сначала войдите в автономный мир.",
                "เข้าโลกออฟไลน์ก่อน", "Betritt zuerst eine Offline-Welt.",
                "Entrez d'abord dans un monde hors ligne.", "請先進入離線世界。");
            Add("finite_amount", "Amount must be a finite number.", "값은 유한한 숫자여야 합니다.",
                "La cantidad debe ser un número finito.", "A quantidade deve ser um número finito.",
                "Jumlah harus berupa angka terbatas.", "Значение должно быть конечным числом.",
                "จำนวนต้องเป็นตัวเลขที่มีค่าจำกัด", "Der Wert muss eine endliche Zahl sein.",
                "La valeur doit être un nombre fini.", "數值必須是有限數字。");
            Add("unknown_gauge", "Unknown gauge: {0}", "알 수 없는 게이지: {0}",
                "Indicador desconocido: {0}", "Medidor desconhecido: {0}", "Gauge tidak dikenal: {0}",
                "Неизвестная шкала: {0}", "ไม่รู้จักเกจ: {0}", "Unbekannte Anzeige: {0}",
                "Jauge inconnue : {0}", "未知計量：{0}");
            Add("gauge_not_initialized", "{0} is not initialized yet.", "{0}가 아직 초기화되지 않았습니다.",
                "{0} aún no está inicializado.", "{0} ainda não foi inicializado.",
                "{0} belum diinisialisasi.", "{0} ещё не инициализирован.",
                "{0} ยังไม่ได้เริ่มต้น", "{0} ist noch nicht initialisiert.",
                "{0} n'est pas encore initialisé.", "{0} 尚未初始化。");
            Add("saved_suffix", "{0} (saved)", "{0} (저장됨)", "{0} (guardado)", "{0} (salvo)",
                "{0} (tersimpan)", "{0} (сохранено)", "{0} (บันทึกแล้ว)", "{0} (gespeichert)",
                "{0} (enregistré)", "{0}（已儲存）");
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

        internal static string OnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }

        internal static string TranslateCombatResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return response;
            if (response == "Enter an offline world first.") return Get("enter_offline_first");
            if (response == "Amount must be a finite number.") return Get("finite_amount");

            const string unknown = "Unknown gauge: ";
            if (response.StartsWith(unknown, StringComparison.Ordinal))
                return Get("unknown_gauge", response.Substring(unknown.Length));

            const string initSuffix = " is not initialized yet.";
            if (response.EndsWith(initSuffix, StringComparison.Ordinal))
                return Get("gauge_not_initialized",
                    response.Substring(0, response.Length - initSuffix.Length));

            const string saved = " (saved)";
            if (response.EndsWith(saved, StringComparison.Ordinal))
                return Get("saved_suffix",
                    response.Substring(0, response.Length - saved.Length));

            return response;
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
