using System;
using System.Collections.Generic;

namespace NPCFriendListPlugin
{
    internal static class NpcFriendLocalization
    {
        private static readonly Dictionary<string, string[]> Texts =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static NpcFriendLocalization()
        {
            Add("joined_party",
                "{0} has joined the party.", "{0}님이 파티에 참가했습니다.",
                "{0} se unió al grupo.", "{0} entrou no grupo.", "{0} bergabung ke party.",
                "{0} присоединился к группе.", "{0} เข้าร่วมปาร์ตี้แล้ว",
                "{0} ist der Gruppe beigetreten.", "{0} a rejoint le groupe.",
                "{0} 已加入隊伍。");
            Add("online_again",
                "{0} is online again.", "{0}님이 다시 온라인 상태가 되었습니다.",
                "{0} vuelve a estar en línea.", "{0} está online novamente.",
                "{0} kembali online.", "{0} снова в сети.", "{0} กลับมาออนไลน์แล้ว",
                "{0} ist wieder online.", "{0} est de nouveau en ligne.",
                "{0} 已重新上線。");
            Add("offline_minute",
                "{0} went offline for 1 minute.", "{0}님이 1분 동안 오프라인 상태가 됩니다.",
                "{0} estará fuera de línea durante 1 minuto.",
                "{0} ficará offline por 1 minuto.", "{0} offline selama 1 menit.",
                "{0} будет не в сети 1 минуту.", "{0} จะออฟไลน์เป็นเวลา 1 นาที",
                "{0} ist für 1 Minute offline.", "{0} sera hors ligne pendant 1 minute.",
                "{0} 將離線 1 分鐘。");
            Add("rescue_in_progress",
                "NPC party rescue is already in progress.",
                "NPC 파티 구조가 이미 진행 중입니다.",
                "El rescate del grupo de NPC ya está en curso.",
                "O resgate pelo grupo de NPC já está em andamento.",
                "Penyelamatan oleh party NPC sedang berlangsung.",
                "Спасение группой NPC уже выполняется.",
                "การช่วยเหลือโดยปาร์ตี้ NPC กำลังดำเนินอยู่",
                "Die Rettung durch die NPC-Gruppe läuft bereits.",
                "Le sauvetage par le groupe de PNJ est déjà en cours.",
                "NPC 隊伍救援已在進行中。");
            Add("rescue_failed",
                "NPC party rescue failed.", "NPC 파티 구조에 실패했습니다.",
                "El rescate del grupo de NPC falló.", "O resgate pelo grupo de NPC falhou.",
                "Penyelamatan oleh party NPC gagal.", "Спасение группой NPC не удалось.",
                "การช่วยเหลือโดยปาร์ตี้ NPC ล้มเหลว", "Die Rettung durch die NPC-Gruppe ist fehlgeschlagen.",
                "Le sauvetage par le groupe de PNJ a échoué.", "NPC 隊伍救援失敗。");
            Add("no_local_player",
                "No local player found for NPC rescue.", "NPC 구조 대상 플레이어를 찾을 수 없습니다.",
                "No se encontró al jugador local para el rescate.",
                "Jogador local não encontrado para o resgate.",
                "Pemain lokal tidak ditemukan untuk penyelamatan.",
                "Локальный игрок для спасения не найден.",
                "ไม่พบผู้เล่นปัจจุบันสำหรับการช่วยเหลือ",
                "Kein lokaler Spieler für die Rettung gefunden.",
                "Aucun joueur local trouvé pour le sauvetage.",
                "找不到可供 NPC 救援的本機玩家。");
            Add("no_rescuer",
                "No NPC party member is available to rescue you.",
                "구조할 수 있는 NPC 파티원이 없습니다.",
                "No hay ningún miembro NPC del grupo disponible para rescatarte.",
                "Nenhum membro NPC do grupo está disponível para resgatar você.",
                "Tidak ada anggota party NPC yang tersedia untuk menyelamatkanmu.",
                "Нет доступного члена группы NPC для спасения.",
                "ไม่มีสมาชิกปาร์ตี้ NPC ที่พร้อมมาช่วยคุณ",
                "Kein NPC-Gruppenmitglied steht für die Rettung zur Verfügung.",
                "Aucun membre PNJ du groupe n'est disponible pour vous secourir.",
                "沒有可進行救援的 NPC 隊伍成員。");
            Add("rescuer_not_ready",
                "{0} is not ready to rescue you.", "{0}님은 아직 구조할 준비가 되지 않았습니다.",
                "{0} aún no está listo para rescatarte.", "{0} ainda não está pronto para resgatar você.",
                "{0} belum siap menyelamatkanmu.", "{0} ещё не готов вас спасать.",
                "{0} ยังไม่พร้อมที่จะมาช่วยคุณ", "{0} ist noch nicht bereit, dich zu retten.",
                "{0} n'est pas encore prêt à vous secourir.", "{0} 尚未準備好救援你。");
            Add("coming_to_rescue",
                "{0} is coming to rescue you.", "{0}님이 구조하러 오고 있습니다.",
                "{0} viene a rescatarte.", "{0} está vindo resgatar você.",
                "{0} sedang datang untuk menyelamatkanmu.", "{0} идёт вас спасать.",
                "{0} กำลังมาช่วยคุณ", "{0} kommt, um dich zu retten.",
                "{0} vient vous secourir.", "{0} 正在趕來救援你。");
            Add("rescued",
                "{0} rescued you.", "{0}님이 당신을 구조했습니다.",
                "{0} te rescató.", "{0} resgatou você.", "{0} menyelamatkanmu.",
                "{0} спас вас.", "{0} ช่วยชีวิตคุณแล้ว", "{0} hat dich gerettet.",
                "{0} vous a secouru.", "{0} 已救援你。");
            Add("invite_sent",
                "Party invitation sent to {0}.", "{0}님에게 파티 초대를 보냈습니다.",
                "Invitación de grupo enviada a {0}.", "Convite de grupo enviado para {0}.",
                "Undangan party dikirim ke {0}.", "Приглашение в группу отправлено {0}.",
                "ส่งคำเชิญเข้าปาร์ตี้ให้ {0} แล้ว", "Gruppeneinladung an {0} gesendet.",
                "Invitation de groupe envoyée à {0}.", "已向 {0} 發送隊伍邀請。");
            Add("kicked",
                "{0} has been kicked from the party.", "{0}님을 파티에서 추방했습니다.",
                "{0} fue expulsado del grupo.", "{0} foi removido do grupo.",
                "{0} dikeluarkan dari party.", "{0} исключён из группы.",
                "{0} ถูกนำออกจากปาร์ตี้แล้ว", "{0} wurde aus der Gruppe entfernt.",
                "{0} a été exclu du groupe.", "{0} 已被移出隊伍。");
            Add("rescue_menu",
                "Request Rescue With NPC Party", "NPC 파티에 구조 요청",
                "Pedir rescate al grupo de NPC", "Pedir resgate ao grupo de NPC",
                "Minta penyelamatan dari party NPC", "Попросить спасение у группы NPC",
                "ขอให้ปาร์ตี้ NPC ช่วยเหลือ", "Rettung durch NPC-Gruppe anfordern",
                "Demander le secours du groupe de PNJ", "請求 NPC 隊伍救援");

            Add("k_start_1",
                "Hold on. I'm coming.", "조금만 버텨요. 지금 갈게요.",
                "Aguanta. Ya voy.", "Aguente. Estou indo.", "Bertahanlah. Aku datang.",
                "Держитесь. Я иду.", "อดทนไว้นะ ฉันกำลังไป", "Halte durch. Ich komme.",
                "Tenez bon. J'arrive.", "撐住，我來了。");
            Add("k_start_2",
                "Stay still. I'll get you back up.", "가만히 있어요. 다시 일으켜 줄게요.",
                "Quédate quieto. Te pondré de pie.", "Fique parado. Vou colocar você de pé.",
                "Jangan bergerak. Aku akan membantumu berdiri.", "Не двигайтесь. Я помогу вам встать.",
                "อยู่นิ่ง ๆ ฉันจะช่วยให้คุณลุกขึ้น", "Bleib ruhig. Ich helfe dir wieder hoch.",
                "Ne bougez pas. Je vais vous relever.", "別動，我會讓你重新站起來。");
            Add("k_start_3",
                "Don't move. I can handle this.", "움직이지 마요. 제가 할 수 있어요.",
                "No te muevas. Yo me encargo.", "Não se mexa. Eu cuido disso.",
                "Jangan bergerak. Aku bisa menangani ini.", "Не двигайтесь. Я справлюсь.",
                "อย่าขยับ ฉันจัดการได้", "Nicht bewegen. Ich schaffe das.",
                "Ne bougez pas. Je m'en occupe.", "別動，我能處理。");
            Add("k_finish_1",
                "You're breathing again. Stay close.", "다시 숨을 쉬네요. 제 곁에 있어요.",
                "Vuelves a respirar. Quédate cerca.", "Você está respirando de novo. Fique perto.",
                "Kamu bernapas lagi. Tetap dekat.", "Вы снова дышите. Держитесь рядом.",
                "คุณกลับมาหายใจแล้ว อยู่ใกล้ ๆ ฉันไว้", "Du atmest wieder. Bleib in meiner Nähe.",
                "Vous respirez à nouveau. Restez près de moi.", "你又能呼吸了，待在我身邊。");
            Add("k_finish_2",
                "You're up. Don't make me do that twice.", "일어났네요. 두 번 하게 만들지는 마요.",
                "Ya estás de pie. No me hagas hacerlo dos veces.",
                "Você levantou. Não me faça fazer isso duas vezes.",
                "Kamu sudah bangun. Jangan buat aku melakukannya dua kali.",
                "Вы встали. Не заставляйте меня делать это дважды.",
                "ลุกขึ้นแล้วนะ อย่าให้ฉันต้องทำแบบนี้สองครั้ง",
                "Du bist wieder auf den Beinen. Lass mich das nicht zweimal machen.",
                "Vous êtes debout. Ne m'obligez pas à recommencer.",
                "你起來了，別讓我做第二次。");
            Add("k_finish_3",
                "Good. Now stay behind me for a moment.", "좋아요. 잠깐 제 뒤에 있어요.",
                "Bien. Ahora quédate detrás de mí un momento.",
                "Ótimo. Agora fique atrás de mim por um momento.",
                "Bagus. Sekarang tetap di belakangku sebentar.",
                "Хорошо. Теперь немного побудьте позади меня.",
                "ดี ตอนนี้อยู่ข้างหลังฉันสักครู่", "Gut. Bleib jetzt kurz hinter mir.",
                "Bien. Restez derrière moi un moment.", "很好，先在我身後待一會兒。");

            Add("charlie_start_1",
                "Don't worry. Think of this as a very short break.",
                "걱정 마요. 아주 짧은 휴식이라고 생각해요.",
                "No te preocupes. Piensa que es un descanso muy corto.",
                "Não se preocupe. Pense nisso como uma pausa bem curta.",
                "Jangan khawatir. Anggap saja ini istirahat yang sangat singkat.",
                "Не волнуйтесь. Считайте это очень коротким перерывом.",
                "ไม่ต้องห่วง คิดซะว่าเป็นการพักสั้น ๆ",
                "Keine Sorge. Betrachte es als sehr kurze Pause.",
                "Ne vous inquiétez pas. Voyez ça comme une très courte pause.",
                "別擔心，就當作是非常短暫的休息。");
            Add("charlie_start_2",
                "Resting is important, but this is a little too much.",
                "쉬는 건 중요하지만, 이건 조금 너무한데요.",
                "Descansar es importante, pero esto es demasiado.",
                "Descansar é importante, mas isso é um pouco demais.",
                "Istirahat itu penting, tapi ini agak berlebihan.",
                "Отдых важен, но это уже немного слишком.",
                "การพักสำคัญนะ แต่นี่มากไปหน่อย",
                "Ausruhen ist wichtig, aber das ist etwas zu viel.",
                "Se reposer est important, mais là c'est un peu trop.",
                "休息很重要，不過這有點太久了。");
            Add("charlie_start_3",
                "Charlie. Charlie. Rescue Charlie? No—rescuing you.",
                "찰리. 찰리. 찰리 구조? 아니—당신을 구조하는 거지.",
                "Charlie. Charlie. ¿Rescatar a Charlie? No, rescatarte a ti.",
                "Charlie. Charlie. Resgatar Charlie? Não—resgatar você.",
                "Charlie. Charlie. Menyelamatkan Charlie? Bukan—menyelamatkanmu.",
                "Чарли. Чарли. Спасти Чарли? Нет — спасаю вас.",
                "ชาร์ลี ชาร์ลี ช่วยชาร์ลีเหรอ? ไม่—ช่วยคุณต่างหาก",
                "Charlie. Charlie. Charlie retten? Nein—dich retten.",
                "Charlie. Charlie. Sauver Charlie ? Non — vous sauver.",
                "Charlie。Charlie。救 Charlie？不——是救你。");
            Add("charlie_finish_1",
                "There you go. Break time is over.", "됐어요. 휴식 시간은 끝났어요.",
                "Listo. Se acabó el descanso.", "Pronto. O intervalo acabou.",
                "Nah, selesai. Waktu istirahat sudah habis.", "Вот и всё. Перерыв окончен.",
                "เรียบร้อย หมดเวลาพักแล้ว", "So. Die Pause ist vorbei.",
                "Voilà. La pause est terminée.", "好了，休息時間結束。");
            Add("charlie_finish_2",
                "See? Knowing when to rest also means knowing when to get up.",
                "봐요? 쉴 때를 안다는 건 일어날 때도 안다는 뜻이에요.",
                "¿Ves? Saber cuándo descansar también es saber cuándo levantarse.",
                "Viu? Saber quando descansar também é saber quando levantar.",
                "Lihat? Tahu kapan istirahat berarti tahu kapan harus bangun.",
                "Видите? Знать, когда отдыхать, значит знать и когда вставать.",
                "เห็นไหม รู้ว่าเมื่อไหร่ควรพัก ก็ต้องรู้ว่าเมื่อไหร่ควรลุกด้วย",
                "Siehst du? Wer weiß, wann er ruhen muss, weiß auch, wann er aufstehen muss.",
                "Vous voyez ? Savoir quand se reposer, c'est aussi savoir quand se relever.",
                "看吧？知道何時休息，也代表知道何時該起來。");
            Add("charlie_finish_3",
                "You're back. Let's stay optimistic, okay?",
                "돌아왔네요. 계속 낙관적으로 가자고요, 알겠죠?",
                "Has vuelto. Sigamos siendo optimistas, ¿sí?",
                "Você voltou. Vamos continuar otimistas, certo?",
                "Kamu kembali. Tetap optimis, ya?",
                "Вы вернулись. Давайте сохранять оптимизм, хорошо?",
                "กลับมาแล้ว มองโลกในแง่ดีกันต่อ โอเคไหม",
                "Du bist zurück. Bleiben wir optimistisch, ja?",
                "Vous êtes de retour. Restons optimistes, d'accord ?",
                "你回來了。繼續保持樂觀，好嗎？");
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

        internal static string LocalizeOriginal(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            try { return LocalizeSystem.Get(text); }
            catch { return text; }
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
