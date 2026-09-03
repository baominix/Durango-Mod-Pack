using System;
using System.Collections.Generic;
using System.Text;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    internal static class ChatCommandLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static ChatCommandLocalization()
        {
            Add("command_failed",
                "Command failed: {0}",
                "명령 실행 실패: {0}",
                "Error al ejecutar el comando: {0}",
                "Falha ao executar o comando: {0}",
                "Perintah gagal: {0}",
                "Ошибка команды: {0}",
                "คำสั่งทำงานไม่สำเร็จ: {0}",
                "Befehl fehlgeschlagen: {0}",
                "Échec de la commande : {0}",
                "指令執行失敗：{0}");
            Add("usage_prefix",
                "Usage: {0}", "사용법: {0}", "Uso: {0}", "Uso: {0}", "Penggunaan: {0}",
                "Использование: {0}", "วิธีใช้: {0}", "Verwendung: {0}", "Utilisation : {0}", "用法：{0}");
            Add("example_prefix",
                "Example: {0}", "예: {0}", "Ejemplo: {0}", "Exemplo: {0}", "Contoh: {0}",
                "Пример: {0}", "ตัวอย่าง: {0}", "Beispiel: {0}", "Exemple : {0}", "範例：{0}");
            Add("also_supported",
                "Also supported: {0}", "추가 지원: {0}", "También compatible: {0}",
                "Também compatível: {0}", "Juga didukung: {0}", "Также поддерживается: {0}",
                "รองรับด้วย: {0}", "Ebenfalls unterstützt: {0}", "Également pris en charge : {0}",
                "亦支援：{0}");
            Add("available_commands",
                "Available commands:", "사용 가능한 명령어:", "Comandos disponibles:",
                "Comandos disponíveis:", "Perintah yang tersedia:", "Доступные команды:",
                "คำสั่งที่ใช้ได้:", "Verfügbare Befehle:", "Commandes disponibles :",
                "可用指令：");
            Add("show_commands",
                "Show available commands.", "사용 가능한 명령어를 표시합니다.",
                "Muestra los comandos disponibles.", "Mostra os comandos disponíveis.",
                "Tampilkan perintah yang tersedia.", "Показать доступные команды.",
                "แสดงคำสั่งที่ใช้ได้", "Verfügbare Befehle anzeigen.",
                "Afficher les commandes disponibles.", "顯示可用指令。");
            Add("add_character_xp",
                "Add character XP.", "캐릭터 경험치를 추가합니다.", "Añade EXP de personaje.",
                "Adiciona EXP do personagem.", "Tambahkan XP karakter.",
                "Добавить опыт персонажа.", "เพิ่ม XP ตัวละคร.",
                "Charakter-EP hinzufügen.", "Ajouter de l'EXP de personnage.",
                "增加角色經驗值。");
            Add("character_xp",
                "Character XP.", "캐릭터 경험치.", "EXP de personaje.", "EXP do personagem.",
                "XP karakter.", "Опыт персонажа.", "XP ตัวละคร.", "Charakter-EP.",
                "EXP de personnage.", "角色經驗值。");
            Add("skill_category_xp",
                "Skill category XP.", "스킬 카테고리 경험치.", "EXP de categoría de habilidad.",
                "EXP da categoria de habilidade.", "XP kategori skill.", "Опыт категории навыка.",
                "XP หมวดสกิล.", "EP der Fertigkeitskategorie.",
                "EXP de catégorie de compétence.", "技能分類經驗值。");
            Add("all_skill_categories",
                "All skill categories.", "모든 스킬 카테고리.", "Todas las categorías de habilidad.",
                "Todas as categorias de habilidade.", "Semua kategori skill.",
                "Все категории навыков.", "ทุกหมวดสกิล.", "Alle Fertigkeitskategorien.",
                "Toutes les catégories de compétences.", "所有技能分類。");
            Add("show_combat_stats",
                "Show combat stats and modifiers.", "전투 능력치와 보정치를 표시합니다.",
                "Muestra estadísticas y modificadores de combate.",
                "Mostra atributos e modificadores de combate.",
                "Tampilkan statistik dan modifier pertempuran.",
                "Показать боевые параметры и модификаторы.",
                "แสดงค่าสถานะและตัวปรับแต่งการต่อสู้",
                "Kampfwerte und Modifikatoren anzeigen.",
                "Afficher les statistiques et modificateurs de combat.",
                "顯示戰鬥能力值與修正值。");
            Add("give_pioneer_desc",
                "Give Pioneer Material test items.", "개척 재료 속성이 있는 테스트 아이템을 지급합니다.",
                "Entrega objetos de prueba con el atributo Pioneer Material.",
                "Entrega itens de teste com o atributo Pioneer Material.",
                "Berikan item uji dengan atribut Pioneer Material.",
                "Выдать тестовые предметы с атрибутом Pioneer Material.",
                "ให้ไอเทมทดสอบที่มีคุณสมบัติ Pioneer Material",
                "Testgegenstände mit dem Attribut Pioneer Material geben.",
                "Donner des objets de test avec l'attribut Pioneer Material.",
                "給予帶有 Pioneer Material 屬性的測試道具。");
            Add("walk_desc",
                "Set walk speed multiplier (1 = default, range 0.1-10).",
                "걷기 속도 배율을 설정합니다 (1 = 기본값, 범위 0.1-10).",
                "Ajusta el multiplicador de velocidad al caminar (1 = predeterminado, rango 0.1-10).",
                "Define o multiplicador da velocidade de caminhada (1 = padrão, faixa 0.1-10).",
                "Atur pengali kecepatan berjalan (1 = default, rentang 0.1-10).",
                "Установить множитель скорости ходьбы (1 = по умолчанию, диапазон 0.1-10).",
                "ตั้งค่าตัวคูณความเร็วเดิน (1 = ค่าเริ่มต้น, ช่วง 0.1-10)",
                "Multiplikator für Gehgeschwindigkeit festlegen (1 = Standard, Bereich 0.1-10).",
                "Définir le multiplicateur de vitesse de marche (1 = défaut, plage 0.1-10).",
                "設定步行速度倍率（1 = 預設，範圍 0.1-10）。");
            Add("walk_variants",
                "Walk speed variants.", "걷기 속도 명령 형식.", "Variantes de velocidad al caminar.",
                "Variações da velocidade de caminhada.", "Varian kecepatan berjalan.",
                "Варианты команды скорости ходьбы.", "รูปแบบคำสั่งความเร็วเดิน",
                "Varianten der Gehgeschwindigkeit.", "Variantes de vitesse de marche.",
                "步行速度指令格式。");
            Add("kill_desc",
                "Kill the local player.", "현재 플레이어를 사망시킵니다.", "Mata al jugador local.",
                "Mata o jogador local.", "Bunuh pemain lokal.", "Убить локального игрока.",
                "ทำให้ผู้เล่นปัจจุบันตาย", "Lokalen Spieler töten.",
                "Tuer le joueur local.", "使本機玩家死亡。");
            Add("gamemode_desc",
                "Change the offline game mode.", "오프라인 게임 모드를 변경합니다.",
                "Cambia el modo de juego sin conexión.", "Altera o modo de jogo offline.",
                "Ubah mode game offline.", "Изменить автономный режим игры.",
                "เปลี่ยนโหมดเกมออฟไลน์", "Offline-Spielmodus ändern.",
                "Changer le mode de jeu hors ligne.", "變更離線遊戲模式。");
            Add("xp_categories",
                "XP categories:", "XP 카테고리:", "Categorías de EXP:", "Categorias de EXP:",
                "Kategori XP:", "Категории опыта:", "หมวด XP:", "EP-Kategorien:",
                "Catégories d'EXP :", "XP 分類：");
            Add("developer_mode_desc",
                "Developer mode.", "개발자 모드.", "Modo desarrollador.", "Modo de desenvolvedor.",
                "Mode pengembang.", "Режим разработчика.", "โหมดนักพัฒนา.",
                "Entwicklermodus.", "Mode développeur.", "開發者模式。");
            Add("attack_alert_desc",
                "Original attack-area visualizer.", "원본 공격 범위 시각화를 표시합니다.",
                "Visualizador original del área de ataque.", "Visualizador original da área de ataque.",
                "Visualisasi area serangan asli.", "Исходная визуализация области атаки.",
                "แสดงพื้นที่โจมตีแบบดั้งเดิมของเกม", "Originale Angriffsbereichsanzeige.",
                "Visualiseur original de zone d'attaque.", "原版攻擊範圍顯示。");
            Add("animal_bubble_desc",
                "Saurus debug bubble.", "사우루스 디버그 버블.", "Burbuja de depuración de Saurus.",
                "Balão de depuração do Saurus.", "Bubble debug Saurus.", "Отладочный пузырь Saurus.",
                "บับเบิลดีบัก Saurus", "Saurus-Debug-Blase.", "Bulle de débogage Saurus.",
                "Saurus 除錯泡泡。");
            Add("hp_desc",
                "Add player life for combat testing.", "전투 테스트용 플레이어 체력을 추가합니다.",
                "Añade vida al jugador para pruebas de combate.",
                "Adiciona vida ao jogador para testes de combate.",
                "Tambahkan nyawa pemain untuk pengujian pertempuran.",
                "Добавить здоровье игроку для боевого теста.",
                "เพิ่มพลังชีวิตผู้เล่นสำหรับทดสอบการต่อสู้",
                "Spieler-Lebenspunkte für Kampftests hinzufügen.",
                "Ajouter des PV au joueur pour les tests de combat.",
                "增加玩家生命值以進行戰鬥測試。");
            Add("sp_desc",
                "Add player stamina for combat testing.", "전투 테스트용 플레이어 스태미나를 추가합니다.",
                "Añade resistencia al jugador para pruebas de combate.",
                "Adiciona stamina ao jogador para testes de combate.",
                "Tambahkan stamina pemain untuk pengujian pertempuran.",
                "Добавить выносливость игроку для боевого теста.",
                "เพิ่มสตามินาผู้เล่นสำหรับทดสอบการต่อสู้",
                "Spieler-Ausdauer für Kampftests hinzufügen.",
                "Ajouter de l'endurance au joueur pour les tests de combat.",
                "增加玩家耐力以進行戰鬥測試。");
            Add("combatspawn_desc",
                "Spawn combat test animals.", "전투 테스트 동물을 생성합니다.",
                "Genera animales de prueba de combate.", "Gera animais de teste de combate.",
                "Munculkan hewan uji pertempuran.", "Создать животных для боевого теста.",
                "สร้างสัตว์สำหรับทดสอบการต่อสู้", "Kampftest-Tiere erzeugen.",
                "Faire apparaître des animaux de test de combat.", "生成戰鬥測試動物。");
            Add("combatwave_desc",
                "Spawn a combat test wave.", "전투 테스트 웨이브를 생성합니다.",
                "Genera una oleada de prueba de combate.", "Gera uma onda de teste de combate.",
                "Munculkan gelombang uji pertempuran.", "Создать волну для боевого теста.",
                "สร้างเวฟสำหรับทดสอบการต่อสู้", "Eine Kampftest-Welle erzeugen.",
                "Faire apparaître une vague de test de combat.", "生成戰鬥測試波次。");
            Add("combatstatus_desc",
                "Show current offline combat status.", "현재 오프라인 전투 상태를 표시합니다.",
                "Muestra el estado actual del combate offline.", "Mostra o estado atual do combate offline.",
                "Tampilkan status pertempuran offline saat ini.", "Показать текущее состояние автономного боя.",
                "แสดงสถานะการต่อสู้ออฟไลน์ปัจจุบัน", "Aktuellen Offline-Kampfstatus anzeigen.",
                "Afficher l'état actuel du combat hors ligne.", "顯示目前離線戰鬥狀態。");
            Add("combatcontext_desc",
                "Show Saurus combat context.", "사우루스 전투 컨텍스트를 표시합니다.",
                "Muestra el contexto de combate de Saurus.", "Mostra o contexto de combate do Saurus.",
                "Tampilkan konteks pertempuran Saurus.", "Показать боевой контекст Saurus.",
                "แสดงบริบทการต่อสู้ของ Saurus", "Saurus-Kampfkontext anzeigen.",
                "Afficher le contexte de combat de Saurus.", "顯示 Saurus 戰鬥內容。");
            Add("combatintent_desc",
                "Show Saurus combat intent.", "사우루스 전투 의도를 표시합니다.",
                "Muestra la intención de combate de Saurus.", "Mostra a intenção de combate do Saurus.",
                "Tampilkan intent pertempuran Saurus.", "Показать боевое намерение Saurus.",
                "แสดงเจตนาการต่อสู้ของ Saurus", "Saurus-Kampfabsicht anzeigen.",
                "Afficher l'intention de combat de Saurus.", "顯示 Saurus 戰鬥意圖。");
            Add("combathelp_desc",
                "Show combat developer commands.", "전투 개발자 명령어를 표시합니다.",
                "Muestra los comandos de desarrollo de combate.", "Mostra os comandos de desenvolvimento de combate.",
                "Tampilkan perintah developer pertempuran.", "Показать команды разработчика для боя.",
                "แสดงคำสั่งนักพัฒนาสำหรับการต่อสู้", "Kampf-Entwicklerbefehle anzeigen.",
                "Afficher les commandes développeur de combat.", "顯示戰鬥開發者指令。");
            Add("or_prefix", "Or: {0}", "또는: {0}", "O: {0}", "Ou: {0}", "Atau: {0}",
                "Или: {0}", "หรือ: {0}", "Oder: {0}", "Ou : {0}", "或：{0}");
            Add("plugin_unavailable",
                "{0} is not available.", "{0}을(를) 사용할 수 없습니다.", "{0} no está disponible.",
                "{0} não está disponível.", "{0} tidak tersedia.", "{0} недоступен.",
                "ไม่พบ {0}", "{0} ist nicht verfügbar.", "{0} n'est pas disponible.",
                "{0} 無法使用。");
            Add("xp_updated", "XP updated.", "XP가 변경되었습니다.", "EXP actualizada.",
                "EXP atualizada.", "XP diperbarui.", "Опыт обновлён.", "อัปเดต XP แล้ว",
                "EP aktualisiert.", "EXP mise à jour.", "XP 已更新。");
            Add("xp_update_failed", "Unable to update XP.", "XP를 변경할 수 없습니다.",
                "No se pudo actualizar la EXP.", "Não foi possível atualizar a EXP.",
                "Tidak dapat memperbarui XP.", "Не удалось обновить опыт.",
                "ไม่สามารถอัปเดต XP ได้", "EP konnten nicht aktualisiert werden.",
                "Impossible de mettre à jour l'EXP.", "無法更新 XP。");
            Add("walk_enter", "Enter a character before using /walk.",
                "/walk 명령을 사용하기 전에 캐릭터로 입장하세요.",
                "Entra con un personaje antes de usar /walk.",
                "Entre com um personagem antes de usar /walk.",
                "Masuk dengan karakter sebelum menggunakan /walk.",
                "Войдите за персонажа перед использованием /walk.",
                "เข้าเล่นด้วยตัวละครก่อนใช้ /walk",
                "Betritt das Spiel mit einem Charakter, bevor du /walk verwendest.",
                "Entrez avec un personnage avant d'utiliser /walk.",
                "請先進入角色後再使用 /walk。");
            Add("walk_current", "Current walk speed: {0}x (1 = default).",
                "현재 걷기 속도: {0}x (1 = 기본값).",
                "Velocidad actual al caminar: {0}x (1 = predeterminado).",
                "Velocidade atual de caminhada: {0}x (1 = padrão).",
                "Kecepatan berjalan saat ini: {0}x (1 = default).",
                "Текущая скорость ходьбы: {0}x (1 = по умолчанию).",
                "ความเร็วเดินปัจจุบัน: {0}x (1 = ค่าเริ่มต้น)",
                "Aktuelle Gehgeschwindigkeit: {0}x (1 = Standard).",
                "Vitesse de marche actuelle : {0}x (1 = défaut).",
                "目前步行速度：{0}x（1 = 預設）。");
            Add("walk_invalid", "Walk speed must be a number from 0.1 to 10.",
                "걷기 속도는 0.1에서 10 사이의 숫자여야 합니다.",
                "La velocidad debe ser un número entre 0.1 y 10.",
                "A velocidade deve ser um número entre 0.1 e 10.",
                "Kecepatan harus berupa angka dari 0.1 hingga 10.",
                "Скорость должна быть числом от 0.1 до 10.",
                "ความเร็วเดินต้องเป็นตัวเลขตั้งแต่ 0.1 ถึง 10",
                "Die Gehgeschwindigkeit muss eine Zahl zwischen 0.1 und 10 sein.",
                "La vitesse doit être un nombre compris entre 0.1 et 10.",
                "步行速度必須是 0.1 到 10 之間的數字。");
            Add("walk_set", "Walk speed set to {0}x (1 = default).",
                "걷기 속도를 {0}x로 설정했습니다 (1 = 기본값).",
                "Velocidad al caminar establecida en {0}x (1 = predeterminado).",
                "Velocidade de caminhada definida como {0}x (1 = padrão).",
                "Kecepatan berjalan diatur ke {0}x (1 = default).",
                "Скорость ходьбы установлена на {0}x (1 = по умолчанию).",
                "ตั้งค่าความเร็วเดินเป็น {0}x (1 = ค่าเริ่มต้น)",
                "Gehgeschwindigkeit auf {0}x gesetzt (1 = Standard).",
                "Vitesse de marche définie sur {0}x (1 = défaut).",
                "步行速度已設為 {0}x（1 = 預設）。");
            Add("walk_range", "Range: 0.1 to 10. Use /walk 1 or /walk reset for default speed.",
                "범위: 0.1-10. 기본 속도는 /walk 1 또는 /walk reset을 사용하세요.",
                "Rango: 0.1 a 10. Usa /walk 1 o /walk reset para la velocidad predeterminada.",
                "Faixa: 0.1 a 10. Use /walk 1 ou /walk reset para a velocidade padrão.",
                "Rentang: 0.1 hingga 10. Gunakan /walk 1 atau /walk reset untuk kecepatan default.",
                "Диапазон: 0.1-10. Для скорости по умолчанию используйте /walk 1 или /walk reset.",
                "ช่วง: 0.1 ถึง 10 ใช้ /walk 1 หรือ /walk reset เพื่อคืนความเร็วเริ่มต้น",
                "Bereich: 0.1 bis 10. Für Standardgeschwindigkeit /walk 1 oder /walk reset verwenden.",
                "Plage : 0.1 à 10. Utilisez /walk 1 ou /walk reset pour la vitesse par défaut.",
                "範圍：0.1 到 10。使用 /walk 1 或 /walk reset 恢復預設速度。");
            Add("pioneer_enter", "Enter an offline character before using /givepioneer.",
                "/givepioneer를 사용하기 전에 오프라인 캐릭터로 입장하세요.",
                "Entra con un personaje offline antes de usar /givepioneer.",
                "Entre com um personagem offline antes de usar /givepioneer.",
                "Masuk dengan karakter offline sebelum menggunakan /givepioneer.",
                "Войдите за автономного персонажа перед использованием /givepioneer.",
                "เข้าเล่นด้วยตัวละครออฟไลน์ก่อนใช้ /givepioneer",
                "Betritt das Spiel mit einem Offline-Charakter, bevor du /givepioneer verwendest.",
                "Entrez avec un personnage hors ligne avant d'utiliser /givepioneer.",
                "請先進入離線角色後再使用 /givepioneer。");
            Add("pioneer_requested", "Requested {0} x {1} Lv.{2} with Pioneer Material Lv.{3}.",
                "Pioneer Material Lv.{3} 속성의 {1} Lv.{2} x {0} 지급을 요청했습니다.",
                "Solicitados {0} x {1} Nv.{2} con Pioneer Material Nv.{3}.",
                "Solicitados {0} x {1} Nv.{2} com Pioneer Material Nv.{3}.",
                "Meminta {0} x {1} Lv.{2} dengan Pioneer Material Lv.{3}.",
                "Запрошено {0} x {1} ур.{2} с Pioneer Material ур.{3}.",
                "ร้องขอ {0} x {1} Lv.{2} พร้อม Pioneer Material Lv.{3}",
                "{0} x {1} Lv.{2} mit Pioneer Material Lv.{3} angefordert.",
                "{0} x {1} niv.{2} avec Pioneer Material niv.{3} demandés.",
                "已要求 {0} x {1} Lv.{2}，Pioneer Material Lv.{3}。");
            Add("pioneer_noargs", "No arguments gives 10 x clam_product Lv.1 / Pioneer Material Lv.1.",
                "인자가 없으면 clam_product Lv.1 10개 / Pioneer Material Lv.1을 지급합니다.",
                "Sin argumentos entrega 10 x clam_product Nv.1 / Pioneer Material Nv.1.",
                "Sem argumentos entrega 10 x clam_product Nv.1 / Pioneer Material Nv.1.",
                "Tanpa argumen memberi 10 x clam_product Lv.1 / Pioneer Material Lv.1.",
                "Без аргументов выдаётся 10 x clam_product ур.1 / Pioneer Material ур.1.",
                "หากไม่ใส่อาร์กิวเมนต์ จะให้ clam_product Lv.1 จำนวน 10 ชิ้น / Pioneer Material Lv.1",
                "Ohne Argumente werden 10 x clam_product Lv.1 / Pioneer Material Lv.1 gegeben.",
                "Sans argument, donne 10 x clam_product niv.1 / Pioneer Material niv.1.",
                "不帶參數時給予 10 x clam_product Lv.1 / Pioneer Material Lv.1。");
            Add("kill_enter", "Enter a character before using /kill.",
                "/kill을 사용하기 전에 캐릭터로 입장하세요.", "Entra con un personaje antes de usar /kill.",
                "Entre com um personagem antes de usar /kill.", "Masuk dengan karakter sebelum menggunakan /kill.",
                "Войдите за персонажа перед использованием /kill.", "เข้าเล่นด้วยตัวละครก่อนใช้ /kill",
                "Betritt das Spiel mit einem Charakter, bevor du /kill verwendest.",
                "Entrez avec un personnage avant d'utiliser /kill.", "請先進入角色後再使用 /kill。");
            Add("already_dead", "You are already dead.", "이미 사망한 상태입니다.", "Ya estás muerto.",
                "Você já está morto.", "Kamu sudah mati.", "Персонаж уже мёртв.", "ตัวละครตายอยู่แล้ว",
                "Du bist bereits tot.", "Vous êtes déjà mort.", "你已經死亡。");
            Add("killed_player", "Killed local player.", "현재 플레이어를 사망시켰습니다.",
                "Jugador local eliminado.", "Jogador local morto.", "Pemain lokal dibunuh.",
                "Локальный игрок убит.", "ทำให้ผู้เล่นปัจจุบันตายแล้ว", "Lokaler Spieler getötet.",
                "Joueur local tué.", "已使本機玩家死亡。");
            Add("gamemode_unknown", "Unknown game mode: {0}", "알 수 없는 게임 모드: {0}",
                "Modo de juego desconocido: {0}", "Modo de jogo desconhecido: {0}",
                "Mode game tidak dikenal: {0}", "Неизвестный режим игры: {0}",
                "ไม่รู้จักโหมดเกม: {0}", "Unbekannter Spielmodus: {0}",
                "Mode de jeu inconnu : {0}", "未知的遊戲模式：{0}");
            Add("gamemode_use", "Use survival/creative, s/c, or 0/1.",
                "survival/creative, s/c 또는 0/1을 사용하세요.",
                "Usa survival/creative, s/c o 0/1.", "Use survival/creative, s/c ou 0/1.",
                "Gunakan survival/creative, s/c, atau 0/1.", "Используйте survival/creative, s/c или 0/1.",
                "ใช้ survival/creative, s/c หรือ 0/1", "Verwende survival/creative, s/c oder 0/1.",
                "Utilisez survival/creative, s/c ou 0/1.", "請使用 survival/creative、s/c 或 0/1。");
            Add("creative_name", "Creative (1)", "크리에이티브 (1)", "Creativo (1)", "Criativo (1)",
                "Creative (1)", "Творческий (1)", "สร้างสรรค์ (1)", "Kreativ (1)", "Créatif (1)", "創造 (1)");
            Add("survival_name", "Survival (0)", "서바이벌 (0)", "Supervivencia (0)", "Sobrevivência (0)",
                "Survival (0)", "Выживание (0)", "เอาชีวิตรอด (0)", "Überleben (0)", "Survie (0)", "生存 (0)");
            Add("gamemode_changed", "Game mode changed to {0}.", "게임 모드를 {0}(으)로 변경했습니다.",
                "Modo de juego cambiado a {0}.", "Modo de jogo alterado para {0}.",
                "Mode game diubah ke {0}.", "Режим игры изменён на {0}.",
                "เปลี่ยนโหมดเกมเป็น {0} แล้ว", "Spielmodus auf {0} geändert.",
                "Mode de jeu changé en {0}.", "遊戲模式已變更為 {0}。");
            Add("craft_free", "Crafting and building no longer consume materials.",
                "제작과 건설 시 재료를 더 이상 소모하지 않습니다.",
                "La fabricación y construcción ya no consumen materiales.",
                "Criação e construção não consomem mais materiais.",
                "Crafting dan pembangunan tidak lagi menghabiskan material.",
                "Создание и строительство больше не расходуют материалы.",
                "การคราฟต์และการก่อสร้างจะไม่ใช้วัสดุอีกต่อไป",
                "Herstellung und Bau verbrauchen keine Materialien mehr.",
                "La fabrication et la construction ne consomment plus de matériaux.",
                "製作與建造不再消耗材料。");
            Add("craft_survival", "Crafting and building now require and consume materials.",
                "제작과 건설에 재료가 필요하며 소모됩니다.",
                "La fabricación y construcción ahora requieren y consumen materiales.",
                "Criação e construção agora exigem e consomem materiais.",
                "Crafting dan pembangunan sekarang membutuhkan dan menghabiskan material.",
                "Создание и строительство теперь требуют и расходуют материалы.",
                "การคราฟต์และการก่อสร้างจะต้องใช้และสิ้นเปลืองวัสดุ",
                "Herstellung und Bau benötigen und verbrauchen nun Materialien.",
                "La fabrication et la construction nécessitent et consomment désormais des matériaux.",
                "製作與建造現在需要並會消耗材料。");
            Add("gamemode_current", "Current game mode: {0}", "현재 게임 모드: {0}",
                "Modo de juego actual: {0}", "Modo de jogo atual: {0}", "Mode game saat ini: {0}",
                "Текущий режим игры: {0}", "โหมดเกมปัจจุบัน: {0}", "Aktueller Spielmodus: {0}",
                "Mode de jeu actuel : {0}", "目前遊戲模式：{0}");
            Add("stats_not_ready", "StatisticsSystem is not ready.", "StatisticsSystem이 아직 준비되지 않았습니다.",
                "StatisticsSystem aún no está listo.", "StatisticsSystem ainda não está pronto.",
                "StatisticsSystem belum siap.", "StatisticsSystem ещё не готов.",
                "StatisticsSystem ยังไม่พร้อม", "StatisticsSystem ist noch nicht bereit.",
                "StatisticsSystem n'est pas encore prêt.", "StatisticsSystem 尚未準備完成。");
            Add("combat_stat", "Combat Stat", "전투 능력치", "Estadísticas de combate", "Atributos de combate",
                "Statistik Pertempuran", "Боевые параметры", "ค่าสถานะการต่อสู้",
                "Kampfwerte", "Statistiques de combat", "戰鬥能力值");
            Add("attack", "Attack", "공격력", "Ataque", "Ataque", "Serangan", "Атака", "พลังโจมตี", "Angriff", "Attaque", "攻擊力");
            Add("accuracy", "Accuracy", "명중", "Precisión", "Precisão", "Akurasi", "Точность", "ความแม่นยำ", "Genauigkeit", "Précision", "命中");
            Add("evasion", "Evasion", "회피", "Evasión", "Evasão", "Hindaran", "Уклонение", "หลบหลีก", "Ausweichen", "Esquive", "閃避");
            Add("crit_rate", "Lethality/Crit Rate", "치명/치명타 확률", "Letalidad/Prob. crítico", "Letalidade/Taxa crítica",
                "Lethality/Crit Rate", "Летальность/Шанс крита", "ความรุนแรง/อัตราคริติคอล",
                "Tödlichkeit/Krit-Rate", "Létalité/Taux critique", "致命/暴擊率");
            Add("attack_rating", "AttackRating/Pen", "공격 등급/관통", "Índice de ataque/Pen.", "Índice de ataque/Pen.",
                "AttackRating/Pen", "Рейтинг атаки/Пробивание", "AttackRating/เจาะเกราะ",
                "Angriffswertung/Durchdr.", "Indice d'attaque/Pénétration", "攻擊評級/穿透");
            Add("defense", "Defense", "방어력", "Defensa", "Defesa", "Pertahanan", "Защита", "พลังป้องกัน", "Verteidigung", "Défense", "防禦力");
            Add("active_actions", "Active Actions", "활성 액션", "Acciones activas", "Ações ativas",
                "Action aktif", "Активные действия", "แอ็กชันที่ใช้งาน", "Aktive Aktionen",
                "Actions actives", "啟用中的動作");
            Add("weapon", "Weapon", "무기", "Arma", "Arma", "Senjata", "Оружие", "อาวุธ", "Waffe", "Arme", "武器");
            Add("action", "Action", "액션", "Acción", "Ação", "Action", "Действие", "แอ็กชัน", "Aktion", "Action", "動作");
            Add("melee_enhanced", "Melee Enhanced Action Type", "근접 강화 액션 유형",
                "Tipo de acción cuerpo a cuerpo mejorada", "Tipo de ação corpo a corpo aprimorada",
                "Tipe action melee yang ditingkatkan", "Тип усиленного действия ближнего боя",
                "ประเภทแอ็กชันประชิดแบบเสริม", "Verbesserter Nahkampf-Aktionstyp",
                "Type d'action de mêlée améliorée", "近戰強化動作類型");
            Add("melee_type", "Melee Type", "근접 무기 유형", "Tipo cuerpo a cuerpo", "Tipo corpo a corpo",
                "Tipe melee", "Тип ближнего боя", "ประเภทประชิด", "Nahkampftyp", "Type de mêlée", "近戰類型");
            Add("ranged_enhanced", "Ranged Enhanced Action Type", "원거리 강화 액션 유형",
                "Tipo de acción a distancia mejorada", "Tipo de ação à distância aprimorada",
                "Tipe action ranged yang ditingkatkan", "Тип усиленного дальнего действия",
                "ประเภทแอ็กชันระยะไกลแบบเสริม", "Verbesserter Fernkampf-Aktionstyp",
                "Type d'action à distance améliorée", "遠程強化動作類型");
            Add("ranged_type", "Ranged Type", "원거리 무기 유형", "Tipo a distancia", "Tipo à distância",
                "Tipe ranged", "Тип дальнего боя", "ประเภทระยะไกล", "Fernkampftyp", "Type à distance", "遠程類型");
            Add("none", "none", "없음", "ninguno", "nenhum", "tidak ada", "нет", "ไม่มี", "keine", "aucun", "無");
            Add("not_ready", "not ready", "준비되지 않음", "no listo", "não pronto", "belum siap",
                "не готово", "ยังไม่พร้อม", "nicht bereit", "pas prêt", "尚未準備");
            Add("unknown", "Unknown", "알 수 없음", "Desconocido", "Desconhecido", "Tidak diketahui",
                "Неизвестно", "ไม่ทราบ", "Unbekannt", "Inconnu", "未知");
            Add("sword", "Sword", "검", "Espada", "Espada", "Pedang", "Меч", "ดาบ", "Schwert", "Épée", "劍");
            Add("axe", "Axe", "도끼", "Hacha", "Machado", "Kapak", "Топор", "ขวาน", "Axt", "Hache", "斧");
            Add("blunt", "Blunt", "둔기", "Contundente", "Contundente", "Tumpul", "Дробящее", "อาวุธทุบ", "Stumpf", "Contondant", "鈍器");
            Add("lance", "Lance", "창", "Lanza", "Lança", "Tombak", "Копьё", "หอก", "Lanze", "Lance", "長槍");
            Add("bow", "Bow", "활", "Arco", "Arco", "Busur", "Лук", "ธนู", "Bogen", "Arc", "弓");
            Add("crossbow", "Crossbow", "석궁", "Ballesta", "Besta", "Crossbow", "Арбалет", "หน้าไม้", "Armbrust", "Arbalète", "弩");
            Add("level_up", "Level Up", "레벨 업", "Subida de nivel", "Subiu de nível", "Naik Level",
                "Повышение уровня", "เลเวลอัป", "Stufenaufstieg", "Niveau supérieur", "升級");
            Add("now_level", "You are now Lv. {0}", "현재 Lv. {0}입니다.", "Ahora eres Nv. {0}",
                "Agora você está no Nv. {0}", "Sekarang Lv. {0}", "Теперь ур. {0}",
                "ตอนนี้คุณมี Lv. {0}", "Du bist jetzt Lv. {0}", "Vous êtes maintenant niv. {0}",
                "目前為 Lv. {0}");
            Add("skill_point", "Skill Point", "스킬 포인트", "Punto de habilidad", "Ponto de habilidade",
                "Skill Point", "Очко навыка", "แต้มสกิล", "Fertigkeitspunkt", "Point de compétence", "技能點");
            Add("strength", "Strength", "힘", "Fuerza", "Força", "Strength", "Сила", "พละกำลัง", "Stärke", "Force", "力量");
            Add("charisma", "Charisma", "매력", "Carisma", "Carisma", "Charisma", "Харизма", "เสน่ห์", "Charisma", "Charisme", "魅力");
            Add("dexterity", "Dexterity", "손재주", "Destreza", "Destreza", "Dexterity", "Ловкость рук", "ความชำนาญ", "Geschick", "Dextérité", "靈巧");
            Add("agility", "Agility", "민첩", "Agilidad", "Agilidade", "Agility", "Проворство", "ความว่องไว", "Beweglichkeit", "Agilité", "敏捷");
            Add("endurance", "Endurance", "지구력", "Resistencia", "Resistência", "Endurance", "Выносливость", "ความอดทน", "Ausdauer", "Endurance", "耐力");
            Add("will", "Will", "의지", "Voluntad", "Vontade", "Will", "Воля", "จิตใจ", "Wille", "Volonté", "意志");
            Add("intelligence", "Intelligence", "지능", "Inteligencia", "Inteligência", "Intelligence", "Интеллект", "สติปัญญา", "Intelligenz", "Intelligence", "智力");
            Add("perception", "Perception", "지각", "Percepción", "Percepção", "Perception", "Восприятие", "การรับรู้", "Wahrnehmung", "Perception", "感知");
            Add("progression_inactive", "Player progression is not active in this game mode.",
                "이 게임 모드에서는 플레이어 성장 시스템이 활성화되어 있지 않습니다.",
                "La progresión del jugador no está activa en este modo de juego.",
                "A progressão do jogador não está ativa neste modo de jogo.",
                "Progres pemain tidak aktif dalam mode game ini.",
                "Прогресс персонажа не активен в этом режиме игры.",
                "ระบบความก้าวหน้าของผู้เล่นไม่ได้เปิดใช้ในโหมดเกมนี้",
                "Der Spielerfortschritt ist in diesem Spielmodus nicht aktiv.",
                "La progression du joueur n'est pas active dans ce mode de jeu.",
                "此遊戲模式未啟用玩家成長系統。");
            Add("max_level", "Character is already at maximum level (Lv.{0}). XP unchanged.",
                "캐릭터가 이미 최고 레벨(Lv.{0})입니다. XP는 변경되지 않았습니다.",
                "El personaje ya está en el nivel máximo (Nv.{0}). La EXP no cambió.",
                "O personagem já está no nível máximo (Nv.{0}). A EXP não foi alterada.",
                "Karakter sudah mencapai level maksimum (Lv.{0}). XP tidak berubah.",
                "Персонаж уже максимального уровня (ур.{0}). Опыт не изменён.",
                "ตัวละครมีเลเวลสูงสุดแล้ว (Lv.{0}) XP ไม่เปลี่ยนแปลง",
                "Der Charakter hat bereits die Maximalstufe (Lv.{0}). EP unverändert.",
                "Le personnage est déjà au niveau maximum (niv.{0}). EXP inchangée.",
                "角色已達最高等級（Lv.{0}），XP 未變更。");
            Add("skill_inactive", "SkillSystemPlugin is not active.", "SkillSystemPlugin이 활성화되어 있지 않습니다.",
                "SkillSystemPlugin no está activo.", "SkillSystemPlugin não está ativo.",
                "SkillSystemPlugin tidak aktif.", "SkillSystemPlugin не активен.",
                "SkillSystemPlugin ไม่ได้ทำงาน", "SkillSystemPlugin ist nicht aktiv.",
                "SkillSystemPlugin n'est pas actif.", "SkillSystemPlugin 未啟用。");
            Add("unknown_category", "Unknown skill category: {0}", "알 수 없는 스킬 카테고리: {0}",
                "Categoría de habilidad desconocida: {0}", "Categoria de habilidade desconhecida: {0}",
                "Kategori skill tidak dikenal: {0}", "Неизвестная категория навыка: {0}",
                "ไม่รู้จักหมวดสกิล: {0}", "Unbekannte Fertigkeitskategorie: {0}",
                "Catégorie de compétence inconnue : {0}", "未知的技能分類：{0}");
            Add("category_xp_result", "{0} XP {1} {2} | Lv.{3} | XP {4}",
                "{0} XP {1} {2} | Lv.{3} | XP {4}", "{0} EXP {1} {2} | Nv.{3} | EXP {4}",
                "{0} EXP {1} {2} | Nv.{3} | EXP {4}", "{0} XP {1} {2} | Lv.{3} | XP {4}",
                "{0} опыт {1} {2} | ур.{3} | опыт {4}", "{0} XP {1} {2} | Lv.{3} | XP {4}",
                "{0} EP {1} {2} | Lv.{3} | EP {4}", "{0} EXP {1} {2} | niv.{3} | EXP {4}",
                "{0} XP {1} {2} | Lv.{3} | XP {4}");
            Add("all_category_result", "All category XP {0} {1} | Updated {2}",
                "모든 카테고리 XP {0} {1} | 업데이트 {2}",
                "EXP de todas las categorías {0} {1} | Actualizadas {2}",
                "EXP de todas as categorias {0} {1} | Atualizadas {2}",
                "XP semua kategori {0} {1} | Diperbarui {2}",
                "Опыт всех категорий {0} {1} | Обновлено {2}",
                "XP ทุกหมวด {0} {1} | อัปเดต {2}",
                "EP aller Kategorien {0} {1} | Aktualisiert {2}",
                "EXP de toutes les catégories {0} {1} | Mises à jour {2}",
                "所有分類 XP {0} {1} | 已更新 {2}");
            Add("character_xp_result", "Character XP {0} {1} | Lv.{2} | XP {3} | HP {4} | Stamina {5}",
                "캐릭터 XP {0} {1} | Lv.{2} | XP {3} | HP {4} | 스태미나 {5}",
                "EXP de personaje {0} {1} | Nv.{2} | EXP {3} | PV {4} | Resistencia {5}",
                "EXP do personagem {0} {1} | Nv.{2} | EXP {3} | HP {4} | Stamina {5}",
                "XP karakter {0} {1} | Lv.{2} | XP {3} | HP {4} | Stamina {5}",
                "Опыт персонажа {0} {1} | ур.{2} | опыт {3} | HP {4} | Выносливость {5}",
                "XP ตัวละคร {0} {1} | Lv.{2} | XP {3} | HP {4} | สตามินา {5}",
                "Charakter-EP {0} {1} | Lv.{2} | EP {3} | HP {4} | Ausdauer {5}",
                "EXP de personnage {0} {1} | niv.{2} | EXP {3} | PV {4} | Endurance {5}",
                "角色 XP {0} {1} | Lv.{2} | XP {3} | HP {4} | 耐力 {5}");
        }

        private static void Add(string key, params string[] values)
        {
            Texts[key] = values;
        }

        internal static string Get(string key, params object[] args)
        {
            string[] values;
            if (!Texts.TryGetValue(key, out values) || values == null || values.Length == 0)
            {
                return key;
            }

            int index = LocaleIndex(LocalizeSystem.Locale);
            string format = index >= 0 && index < values.Length && !string.IsNullOrEmpty(values[index])
                ? values[index]
                : values[0];
            if (args == null || args.Length == 0)
            {
                return format;
            }
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        internal static string DisplayWeaponType(string type)
        {
            if (string.Equals(type, "Sword", StringComparison.Ordinal)) return Get("sword");
            if (string.Equals(type, "Axe", StringComparison.Ordinal)) return Get("axe");
            if (string.Equals(type, "Blunt", StringComparison.Ordinal)) return Get("blunt");
            if (string.Equals(type, "Lance", StringComparison.Ordinal)) return Get("lance");
            if (string.Equals(type, "Bow", StringComparison.Ordinal)) return Get("bow");
            if (string.Equals(type, "Crossbow", StringComparison.Ordinal)) return Get("crossbow");
            if (string.Equals(type, "Unknown", StringComparison.Ordinal)) return Get("unknown");
            return type ?? string.Empty;
        }

        internal static string TranslateExternalResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return response;
            }

            if (response == "Player progression is not active in this game mode.")
                return Get("progression_inactive");
            if (response == "SkillSystemPlugin is not active.")
                return Get("skill_inactive");
            if (response == "Usage: /xp level <add|set> <amount>")
                return Get("usage_prefix", "/xp level <add|set> <amount>");
            if (response == "Usage: /xp <category> <add|set> <amount>")
                return Get("usage_prefix", "/xp <category> <add|set> <amount>");
            if (response == "Usage: /xp category all <add|set> <amount>")
                return Get("usage_prefix", "/xp category all <add|set> <amount>");

            const string maxPrefix = "Character is already at maximum level (Lv.";
            const string maxSuffix = "). XP unchanged.";
            if (response.StartsWith(maxPrefix, StringComparison.Ordinal) &&
                response.EndsWith(maxSuffix, StringComparison.Ordinal))
            {
                string level = response.Substring(
                    maxPrefix.Length,
                    response.Length - maxPrefix.Length - maxSuffix.Length);
                return Get("max_level", level);
            }

            const string unknownCategory = "Unknown skill category: ";
            if (response.StartsWith(unknownCategory, StringComparison.Ordinal))
                return Get("unknown_category", response.Substring(unknownCategory.Length));

            if (response.StartsWith("All category XP ", StringComparison.Ordinal))
            {
                string[] parts = response.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int marker = response.IndexOf(" | Updated ", StringComparison.Ordinal);
                if (parts.Length >= 5 && marker >= 0)
                {
                    string updated = response.Substring(marker + " | Updated ".Length);
                    return Get("all_category_result", parts[3], parts[4], updated);
                }
            }

            if (response.StartsWith("Character XP ", StringComparison.Ordinal))
            {
                string[] pieces = response.Split(new string[] { " | " }, StringSplitOptions.None);
                if (pieces.Length >= 5)
                {
                    string[] first = pieces[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string level = AfterPrefix(pieces[1], "Lv.");
                    string xp = AfterPrefix(pieces[2], "XP ");
                    string hp = AfterPrefix(pieces[3], "HP ");
                    string stamina = AfterPrefix(pieces[4], "Stamina ");
                    if (first.Length >= 4)
                        return Get("character_xp_result", first[2], first[3], level, xp, hp, stamina);
                }
            }

            int categoryMarker = response.IndexOf(" XP ", StringComparison.Ordinal);
            if (categoryMarker > 0 && response.IndexOf(" | Lv.", StringComparison.Ordinal) > categoryMarker)
            {
                string[] pieces = response.Split(new string[] { " | " }, StringSplitOptions.None);
                if (pieces.Length >= 3)
                {
                    string prefix = pieces[0];
                    int xpAt = prefix.IndexOf(" XP ", StringComparison.Ordinal);
                    string category = prefix.Substring(0, xpAt);
                    string[] tail = prefix.Substring(xpAt + 4).Split(
                        new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tail.Length >= 2)
                    {
                        return Get("category_xp_result", category, tail[0], tail[1],
                            AfterPrefix(pieces[1], "Lv."), AfterPrefix(pieces[2], "XP "));
                    }
                }
            }

            if (response.StartsWith("Level Up", StringComparison.Ordinal))
            {
                string[] lines = response.Replace("\r", string.Empty).Split('\n');
                StringBuilder result = new StringBuilder();
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line == "Level Up") line = Get("level_up");
                    else if (line.StartsWith("You are now Lv. ", StringComparison.Ordinal))
                        line = Get("now_level", line.Substring("You are now Lv. ".Length));
                    else line = TranslatePlusLine(line);
                    if (result.Length > 0) result.AppendLine();
                    result.Append(line);
                }
                return result.ToString();
            }

            return response;
        }

        private static string TranslatePlusLine(string line)
        {
            string[] labels = new string[]
            {
                "Skill Point", "Strength", "Charisma", "Dexterity", "Agility",
                "Endurance", "Will", "Intelligence", "Perception"
            };
            string[] keys = new string[]
            {
                "skill_point", "strength", "charisma", "dexterity", "agility",
                "endurance", "will", "intelligence", "perception"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                if (line.StartsWith(labels[i] + " +", StringComparison.Ordinal))
                {
                    return Get(keys[i]) + line.Substring(labels[i].Length);
                }
            }
            return line;
        }

        private static string AfterPrefix(string value, string prefix)
        {
            return value != null && value.StartsWith(prefix, StringComparison.Ordinal)
                ? value.Substring(prefix.Length)
                : value;
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
