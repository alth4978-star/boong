using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    // ───────────────────────────── 타이틀 ─────────────────────────────
    public class TitleScreen : ScreenBase
    {
        public override void Build()
        {
            Sfx.Bgm("night");
            UIKit.Sprite(Root, "Backgrounds/title").rectTransform.Fill();
            var shade = UIKit.Rect(Root, new Color(0.03f, 0.01f, 0.05f, 0.35f));
            shade.rectTransform.Fill();
            var snow = UIKit.New("Snow", Root).Fill();
            snow.gameObject.AddComponent<SnowFall>();

            var small = UIKit.Label(Root, "가칭", 9, new Color(1f, 0.8f, 0.6f), TextAnchor.MiddleLeft, true, true);
            small.rectTransform.At(36, 70, 100, 14);
            var title = UIKit.Label(Root, "붕어빵 팔아서\n그녀 보러 간다", 28, Pal.Cream, TextAnchor.UpperLeft, true, true);
            title.rectTransform.At(34, 86, 300, 80);
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.6f, 0.1f, 0.3f, 0.8f);
            shadow.effectDistance = new Vector2(2, -2);
            var en = UIKit.Label(Root, "TAIYAKI ROMANCE TYCOON", 11, Pal.Gold, TextAnchor.MiddleLeft, true, true);
            en.rectTransform.At(36, 168, 300, 16);
            var desc = UIKit.Label(Root, "평일에는 붕어빵 장사, 주말에는 네온 번화가에서 만나는 연애 타이쿤", 9, new Color(0.95f, 0.88f, 0.95f), TextAnchor.MiddleLeft, false, true);
            desc.rectTransform.At(36, 186, 320, 16);

            var b1 = UIKit.Button(Root, "새 게임", () =>
            {
                if (SaveSystem.HasSave)
                    Modal.Confirm("저장된 게임이 있어요.\n처음부터 다시 시작할까요?", "새로 시작", "취소", () => Game.I.NewGame());
                else Game.I.NewGame();
            }, "button", 12);
            ((RectTransform)b1.transform).At(36, 222, 140, 28);
            if (SaveSystem.HasSave)
            {
                var b2 = UIKit.Button(Root, "이어하기", () => Game.I.Continue(), "button", 12);
                ((RectTransform)b2.transform).At(36, 256, 140, 28);
            }
            var foot = UIKit.Label(Root, "프로토타입 v0.1 — 게임 속 1주일 · 히로인 1명 · 팥/크림", 8, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, false, true);
            foot.rectTransform.At(36, 334, 400, 14);
        }
    }

    // ───────────────────────────── 프롤로그 ─────────────────────────────
    public class PrologueScreen : ScreenBase
    {
        public override void Build()
        {
            Sfx.Bgm("night");
            UIKit.Sprite(Root, "Backgrounds/street_night").rectTransform.Fill();
            UIKit.Rect(Root, new Color(0, 0, 0.05f, 0.55f)).rectTransform.Fill();
            UIKit.New("Snow", Root).Fill().gameObject.AddComponent<SnowFall>();
            var dlg = DialogueBox.Create(Root, "dark", Pal.Gold, Pal.Cream, GameData.Rina);
            var lines = new List<Line>();
            foreach (var t in GameData.Root.prologue)
            {
                bool hero = t.StartsWith("나:");
                lines.Add(new Line { who = hero ? "hero" : "", text = hero ? t.Substring(2).Trim() : t });
            }
            dlg.Play(lines.ToArray(), () => Game.I.GoMorning());
            var skip = UIKit.Button(Root, "건너뛰기 ▶▶", () => Game.I.GoMorning(), "dark", 9);
            ((RectTransform)skip.transform).At(546, 10, 84, 20);
        }
    }

    // ───────────────────────────── 주간 일정 ─────────────────────────────
    public class WeekPlanScreen : ScreenBase
    {
        readonly List<Text> planTexts = new List<Text>();
        readonly List<Image> planCards = new List<Image>();
        Text staminaText;

        public override void Build()
        {
            Sfx.Bgm("weekday");
            UIKit.Sprite(Root, "Backgrounds/room").rectTransform.Fill();
            UIKit.Rect(Root, new Color(1f, 0.85f, 0.6f, 0.12f)).rectTransform.Fill();
            UIKit.Rect(Root, new Color(0, 0, 0, 0.35f)).rectTransform.Fill();

            var panel = UIKit.Frame(Root, "paper");
            panel.rectTransform.Mid(320, 184, 560, 300);
            var p = panel.transform;

            var wk = UIKit.Label(p, string.Format("WEEK {0:00}", S.week), 18, Pal.Brown, TextAnchor.MiddleLeft, true);
            wk.rectTransform.At(20, 14, 200, 26);
            var money = UIKit.Label(p, "현재 자금  " + UIKit.Yen(S.money), 13, Pal.Ink, TextAnchor.MiddleRight, true);
            money.rectTransform.At(300, 14, 240, 26);
            var help = UIKit.Label(p, "평일(월~금)을 눌러 장사 / 휴식을 정하세요.  장사: 체력 -" + GameData.Eco.workStaminaCost + "   휴식: 체력 +" + GameData.Eco.restStaminaGain, 9, new Color(0.45f, 0.3f, 0.2f), TextAnchor.MiddleLeft);
            help.rectTransform.At(20, 42, 520, 14);

            for (int d = 0; d < 7; d++)
            {
                int day = d;
                var card = UIKit.Frame(p, d >= 5 ? "neon" : "wood");
                card.rectTransform.At(20 + d * 75, 62, 68, 76);
                var dn = UIKit.Label(card.transform, GameState.DayEn[d], 11, d >= 5 ? Pal.Teal : Pal.Gold, TextAnchor.MiddleCenter, true, true);
                dn.rectTransform.At(0, 6, 68, 18);
                var pl = UIKit.Label(card.transform, "", 13, Pal.Cream, TextAnchor.MiddleCenter, true, true);
                pl.rectTransform.At(0, 28, 68, 24);
                var sub = UIKit.Label(card.transform, d == 5 ? "♥ 약속" : d == 6 ? "?" : "", 9, new Color(1f, 0.7f, 0.85f), TextAnchor.MiddleCenter, true, true);
                sub.rectTransform.At(0, 52, 68, 16);
                planTexts.Add(pl);
                planCards.Add(card);
                if (d < 5) UIKit.Clickable(card.rectTransform, () => Toggle(day));
            }

            staminaText = UIKit.Label(p, "", 9, new Color(0.45f, 0.3f, 0.2f), TextAnchor.MiddleLeft, true);
            staminaText.rectTransform.At(20, 142, 520, 14);

            var goalBox = UIKit.Frame(p, "dark");
            goalBox.rectTransform.At(20, 162, 340, 96);
            var gt = UIKit.Label(goalBox.transform, "이번 주 목표", 10, Pal.Gold, TextAnchor.MiddleLeft, true, true);
            gt.rectTransform.At(12, 8, 200, 16);
            var g1 = UIKit.Label(goalBox.transform, "♥ " + GameData.Rina.name + "와(과) 토요일 밤 클럽 약속", 12, new Color(1f, 0.7f, 0.8f), TextAnchor.MiddleLeft, false, true);
            g1.rectTransform.At(12, 28, 320, 20);
            var g2 = UIKit.Label(goalBox.transform, "필요 예상금액  " + UIKit.Yen(GameData.Eco.weeklyGoal), 12, Pal.Cream, TextAnchor.MiddleLeft, true, true);
            g2.rectTransform.At(12, 50, 320, 18);
            int lack = GameData.Eco.weeklyGoal - S.money;
            var g3 = UIKit.Label(goalBox.transform, lack > 0 ? "앞으로 " + UIKit.Yen(lack) + " 더 벌어야 한다." : "돈은 충분하다. 가게에 투자해 볼까?", 9, new Color(0.9f, 0.85f, 0.75f), TextAnchor.MiddleLeft, false, true);
            g3.rectTransform.At(12, 70, 320, 16);

            var tip = UIKit.Label(p, "“토요일에 그녀를 만나려면\n 이번 주에 돈을 벌어야 한다.”", 11, Pal.Brown, TextAnchor.MiddleCenter);
            tip.rectTransform.At(370, 168, 170, 50);
            var start = UIKit.Button(p, "이번 주 시작", () =>
            {
                S.Set("plan_done_w" + S.week);
                Game.I.GoMorning();
            }, "button", 13);
            ((RectTransform)start.transform).At(380, 226, 150, 32);
            Refresh();
        }

        void Toggle(int d)
        {
            while (S.plan.Count < 5) S.plan.Add("work");
            S.plan[d] = S.plan[d] == "work" ? "rest" : "work";
            Tw.Run(this, Tw.Pop(planCards[d].transform, 0.15f, 1.08f));
            Refresh();
        }

        void Refresh()
        {
            int st = S.stamina;
            var sb = new System.Text.StringBuilder("예상 체력  ");
            for (int d = 0; d < 7; d++)
            {
                var plan = S.PlanOf(d);
                planTexts[d].text = GameState.PlanLabel(plan);
                planTexts[d].color = plan == DayPlan.Rest ? Pal.Sky : plan == DayPlan.Work ? Pal.Cream : new Color(1f, 0.6f, 0.9f);
                if (d < 5)
                {
                    if (plan == DayPlan.Work) st -= GameData.Eco.workStaminaCost;
                    else st = Mathf.Min(GameData.Eco.maxStamina, st + GameData.Eco.restStaminaGain);
                    sb.Append(GameState.DayKo[d]).Append(' ').Append(st < 0 ? "<color=#c03020>X</color>" : st.ToString()).Append("   ");
                }
            }
            if (st < 0) sb.Append("<color=#c03020>체력이 모자라요! 휴식을 넣어 주세요.</color>");
            staminaText.text = sb.ToString();
        }
    }

    // ───────────────────────────── 주간 결산 ─────────────────────────────
    public class WeekSummaryScreen : ScreenBase
    {
        public override void Build()
        {
            Sfx.Bgm("night");
            var h = GameData.Rina;
            UIKit.Sprite(Root, "Backgrounds/room").rectTransform.Fill();
            UIKit.Rect(Root, new Color(0.03f, 0.04f, 0.12f, 0.55f)).rectTransform.Fill();

            var panel = UIKit.Frame(Root, "paper");
            panel.rectTransform.Mid(320, 180, 520, 320);
            var p = panel.transform;
            var title = UIKit.Label(p, string.Format("WEEK {0:00}  결산", S.week), 18, Pal.Brown, TextAnchor.MiddleLeft, true);
            title.rectTransform.At(20, 12, 300, 26);

            string[] rows =
            {
                "이번 주 매출", UIKit.Yen(S.weekSales),
                "이번 주 지출", UIKit.Yen(S.weekSpent),
                "현재 자금", UIKit.Yen(S.money),
                "누적 판매", S.totalSold + "개",
                "최고 콤보", "×" + S.bestCombo,
            };
            for (int i = 0; i < rows.Length; i += 2)
            {
                var a = UIKit.Label(p, rows[i], 11, Pal.Ink, TextAnchor.MiddleLeft);
                a.rectTransform.At(28, 48 + i * 11, 120, 20);
                var b = UIKit.Label(p, rows[i + 1], 12, Pal.Brown, TextAnchor.MiddleRight, true);
                b.rectTransform.At(130, 48 + i * 11, 110, 20);
            }

            var rel = UIKit.Frame(p, "wine");
            rel.rectTransform.At(262, 46, 238, 106);
            var face = UIKit.Img(rel.transform, Art.Get(h.smallSprite), Color.white, "face");
            face.preserveAspect = true;
            face.rectTransform.At(10, 10, 34, 86);
            var rn = UIKit.Label(rel.transform, "♡ " + h.name, 12, GameData.Hex(h.themeAccent, Pal.Gold), TextAnchor.MiddleLeft, true, true);
            rn.rectTransform.At(52, 10, 180, 18);
            var rs = UIKit.Label(rel.transform, "관계:  " + S.StageName(h), 12, Pal.Cream, TextAnchor.MiddleLeft, false, true);
            rs.rectTransform.At(52, 32, 180, 18);
            string mood = S.Has("date_done") ? "일요일의 붕어빵을 오래 기억할 것 같다." :
                          S.Has("karaoke_done") ? "노래방에서 같이 부른 노래가 귀에 맴돈다." :
                          S.Has("club_done") ? "토요일 밤 이야기가 즐거웠다." : "이번 주는 만나지 못했다.";
            var rm = UIKit.Label(rel.transform, mood, 9, new Color(1f, 0.85f, 0.9f), TextAnchor.UpperLeft, false, true);
            rm.rectTransform.At(52, 54, 180, 44);

            var hints = new List<string>();
            foreach (var f in GameData.Root.fillings)
                if (!S.IsUnlocked(f.id) && !string.IsNullOrEmpty(f.requireFlag) && S.Has(f.requireFlag))
                    hints.Add("★ 신메뉴 힌트: <b>" + f.name + "</b> — 책상에서 레시피 연구 가능");
            if (hints.Count == 0) hints.Add("신메뉴 힌트: 아직 없음 (그녀의 이야기에 귀 기울여 보자)");
            var ht = UIKit.Label(p, string.Join("\n", hints.ToArray()), 10, new Color(0.55f, 0.15f, 0.3f), TextAnchor.UpperLeft);
            ht.rectTransform.At(28, 170, 470, 34);

            var next = UIKit.Label(p, "다음 주 목표:  " + UIKit.Yen(GameData.Eco.weeklyGoal) + " 모아서 " + h.name + " 만나러 가기" +
                (S.molds < 3 ? "   /   가게: 붕어빵 틀 +1 업그레이드" : ""), 10, Pal.Ink, TextAnchor.MiddleLeft);
            next.rectTransform.At(28, 206, 470, 18);

            var proto = UIKit.Label(p, S.week == 1 ? "— 프로토타입의 1주일이 끝났습니다. 같은 사이클로 계속 플레이할 수 있어요. —" : "", 9, new Color(0.5f, 0.4f, 0.35f), TextAnchor.MiddleCenter);
            proto.rectTransform.At(10, 232, 500, 16);

            var b1 = UIKit.Button(p, "다음 주 월요일 ▶", () => Game.I.StartNextWeek(), "button", 12);
            ((RectTransform)b1.transform).At(300, 262, 190, 30);
            var b2 = UIKit.Button(p, "타이틀로", () => { SaveSystem.Save(S); Game.Go<TitleScreen>(); }, "dark", 10);
            ((RectTransform)b2.transform).At(28, 266, 100, 24);
        }
    }
}
