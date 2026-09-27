using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 원룸 HOME. 창밖 날씨/시간, 최소 정보, 그리고 방 안 물건으로 하루를 시작한다.
    /// 스마트폰 → 메시지 / 냉장고 → 재료 / 책상 → 연구·업그레이드 / 침대 → 하루 종료 / 현관 → 외출
    /// 게임이 진행될수록 선물·사진·트로피가 방에 쌓인다 (홈 화면 = 플레이 기록).
    /// </summary>
    public class HomeScreen : ScreenBase
    {
        Hud hud;
        Text badge;
        Image badgeBg;
        Text goalText, hintText;
        Image goalFill;

        public override void Build()
        {
            bool night = S.IsNight || S.day == 5;
            Sfx.Bgm(night ? "night" : "weekday");

            UIKit.Sprite(Root, "Backgrounds/room").rectTransform.Fill();

            // 창밖 눈
            var window = UIKit.New("WindowSnow", Root).At(300, 6, 160, 145);
            window.gameObject.AddComponent<RectMask2D>();
            var snow = UIKit.New("Snow", window).Fill().gameObject.AddComponent<SnowFall>();
            snow.width = 160; snow.height = 145; snow.count = 22;
            if (!night)
            {
                var sky = UIKit.Rect(window, new Color(0.75f, 0.85f, 1f, 0.28f), "DaySky");
                sky.rectTransform.Fill();
                sky.transform.SetAsFirstSibling();
            }

            // 시간대 조명
            var light = UIKit.Rect(Root, night ? new Color(0.04f, 0.06f, 0.2f, 0.28f) : new Color(1f, 0.86f, 0.62f, 0.16f), "Light");
            light.rectTransform.Fill();

            BuildDecor();

            // 주인공
            var hero = UIKit.Sprite(Root, "Characters/hero");
            hero.rectTransform.Foot(505, 356, 110, 150);
            hero.gameObject.AddComponent<Bob>().amp = 0.8f;

            // 스마트폰 (책상 위)
            var phone = UIKit.Frame(Root, "phone", null, 4f);
            phone.rectTransform.Mid(236, 300, 16, 26);
            phone.rectTransform.localRotation = Quaternion.Euler(0, 0, -12);
            var screen = UIKit.Rect(phone.transform, new Color(0.6f, 0.85f, 1f, 0.9f));
            screen.rectTransform.Fill(3, 3, 3, 5);
            badge = UIKit.Label(Root, "!", 12, Color.white, TextAnchor.MiddleCenter, true, true);
            badgeBg = UIKit.Img(Root, Art.Get("Frames/circle"), new Color(0.9f, 0.2f, 0.3f), "Badge");
            badgeBg.rectTransform.Mid(248, 286, 14, 14);
            badge.rectTransform.Mid(248, 286, 14, 14);
            badge.transform.SetAsLastSibling();
            badgeBg.gameObject.AddComponent<Blink>();

            Hotspot("스마트폰", new Rect(200, 272, 70, 52), 236, 270, OpenPhone);
            Hotspot("책상 · 연구", new Rect(20, 130, 180, 110), 100, 190, () => DeskPopup.Open(this));
            Hotspot("냉장고 · 재료", new Rect(585, 30, 55, 150), 592, 110, () => MarketPopup.Open(false, Refresh));
            Hotspot("침대", new Rect(440, 150, 100, 60), 490, 176, OnBed);
            Hotspot("현관 · 외출", new Rect(560, 190, 80, 160), 596, 262, OnDoor);

            hud = Hud.Create(Root, night ? HudStyle.Night : HudStyle.Day);

            // 오늘 일정 · 이번 주 매출 · 목표
            var info = UIKit.Frame(Root, "wood");
            info.rectTransform.At(482, 5, 152, 64);
            var today = UIKit.Label(info.transform, "", 10, Pal.Gold, TextAnchor.MiddleLeft, true, true);
            today.rectTransform.At(8, 4, 140, 16);
            today.text = "오늘 일정 : " + TodayLabel();
            var sales = UIKit.Label(info.transform, "이번 주 매출 " + UIKit.Yen(S.weekSales), 9, Pal.Cream, TextAnchor.MiddleLeft, false, true);
            sales.rectTransform.At(8, 20, 140, 14);
            goalText = UIKit.Label(info.transform, "", 8, new Color(1f, 0.75f, 0.85f), TextAnchor.MiddleLeft, false, true);
            goalText.rectTransform.At(8, 34, 140, 12);
            var barBg = UIKit.Rect(info.transform, new Color(0, 0, 0, 0.5f));
            barBg.rectTransform.At(8, 48, 136, 7);
            goalFill = UIKit.Rect(barBg.transform, new Color(1f, 0.5f, 0.65f));
            goalFill.rectTransform.At(0, 0, 0, 7);

            ShowHint();
            Refresh();
        }

        string TodayLabel()
        {
            if (S.IsNight) return "하루 끝";
            switch (S.Today)
            {
                case DayPlan.Work: return "장사";
                case DayPlan.Rest: return "휴식";
                case DayPlan.Club: return "♥ 클럽";
                default: return S.Has("date_accepted") ? "♥ 데이트" : "자유";
            }
        }

        void ShowHint()
        {
            var bar = UIKit.Frame(Root, "dark");
            bar.rectTransform.At(150, 336, 340, 20);
            hintText = UIKit.Label(bar.transform, "", 9, Pal.Cream, TextAnchor.MiddleCenter, false, true);
            hintText.rectTransform.Fill(4, 0, 4, 0);
        }

        string HintMessage()
        {
            string msg;
            if (S.IsNight) msg = S.UnreadCount > 0 ? "스마트폰에 메시지가 와 있다." : "오늘 하루도 수고했다. 침대에서 쉬자.";
            else if (S.Today == DayPlan.Work && S.stamina <= 0) msg = "몸이 무겁다… 오늘은 쉬어야 할 것 같다. (침대)";
            else if (S.Today == DayPlan.Work && S.BakeableCount < GameState.DailyStockTarget) msg = "재료가 " + S.BakeableCount + "개 분량뿐이다. 냉장고에서 먼저 주문하자!";
            else if (S.Today == DayPlan.Work) msg = "오늘은 장사하는 날. 현관으로 나가 노점으로 가자.";
            else if (S.Today == DayPlan.Rest) msg = "오늘은 쉬는 날. 침대에서 쉬거나, 쇼핑거리에 다녀와도 좋다.";
            else if (S.Today == DayPlan.Club) msg = "토요일 밤. " + GameData.Rina.name + "가 기다리는 번화가로 가자!";
            else msg = S.Has("date_accepted") ? "약속한 겨울 축제에 가자! (현관)" : "일요일. 푹 쉬며 다음 주를 준비하자.";
            return msg;
        }

        void Refresh()
        {
            if (hud != null) hud.Refresh();
            hintText.text = HintMessage();
            int unread = S.UnreadCount;
            badgeBg.gameObject.SetActive(unread > 0);
            badge.gameObject.SetActive(unread > 0);
            float r = Mathf.Clamp01((float)S.money / GameData.Eco.weeklyGoal);
            goalText.text = "♥ 토요일 목표 " + UIKit.Yen(S.money) + " / " + UIKit.Yen(GameData.Eco.weeklyGoal);
            goalFill.rectTransform.sizeDelta = new Vector2(136 * r, 7);
        }

        void Hotspot(string label, Rect area, float lx, float ly, System.Action onClick)
        {
            var hs = UIKit.New("Hotspot_" + label, Root).At(area.x, area.y, area.width, area.height);
            var img = hs.gameObject.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0);
            var glow = UIKit.Frame(hs, "round", new Color(1f, 0.9f, 0.6f, 0f), 4f);
            glow.rectTransform.Fill();
            UIKit.Clickable(hs, onClick, false);
            var trigger = hs.gameObject.AddComponent<HotspotGlow>();
            trigger.glow = glow;

            var plank = UIKit.Frame(Root, "wood");
            float w = label.Length * 9 + 16;
            plank.rectTransform.Mid(lx, ly, w, 18);
            var t = UIKit.Label(plank.transform, label, 9, Pal.Cream, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Fill();
            trigger.label = plank.transform;
            UIKit.Clickable(plank.rectTransform, onClick, false);
        }

        void OpenPhone()
        {
            PhonePopup.Open(GameData.Rina, Refresh);
        }

        void OnBed()
        {
            if (S.IsNight)
            {
                Modal.Confirm("오늘은 이만 잘까?", "잔다", "아직", () => Game.I.Sleep());
                return;
            }
            switch (S.Today)
            {
                case DayPlan.Rest:
                    Modal.Confirm("오늘은 푹 쉬자.\n(체력 +" + GameData.Eco.restStaminaGain + ")", "쉰다", "취소", Rest);
                    break;
                case DayPlan.Work:
                    Modal.Confirm(S.stamina <= 0 ? "너무 피곤하다… 오늘 장사는 쉴까?\n(계획을 휴식으로 바꿉니다)" : "오늘은 장사하는 날이다.\n그래도 쉴까? (계획을 휴식으로 바꿉니다)", "쉰다", "취소", () =>
                    {
                        while (S.plan.Count < 5) S.plan.Add("work");
                        S.plan[S.day] = "rest";
                        Rest();
                    });
                    break;
                case DayPlan.Club:
                    Modal.Confirm("오늘 밤은 " + GameData.Rina.name + "와의 약속이 있다.\n정말 집에서 쉴까?", "쉰다", "취소", Rest);
                    break;
                default:
                    Modal.Confirm("일요일은 느긋하게 쉬자.\n(체력 +" + GameData.Eco.restStaminaGain + ")", "쉰다", "취소", Rest);
                    break;
            }
        }

        void Rest()
        {
            S.stamina = Mathf.Min(GameData.Eco.maxStamina, S.stamina + GameData.Eco.restStaminaGain);
            Game.Toast("푹 쉬었다. 체력 " + UIKit.Hearts(S.stamina, GameData.Eco.maxStamina));
            Game.I.EnterNight();
        }

        void OnDoor()
        {
            if (S.IsNight)
            {
                Game.Toast("밤이 늦었다. 오늘은 쉬자.");
                return;
            }
            if (S.Today == DayPlan.Work && S.stamina <= 0)
            {
                Modal.Message("체력이 바닥이다. 오늘은 장사를 못 하겠다…\n(침대에서 쉬어 주세요)");
                return;
            }
            if (S.Today == DayPlan.Work && S.BakeableCount < GameState.DailyStockTarget)
            {
                // 장사 중엔 집에 못 돌아오니, 나가기 전에 한 번 알려 준다
                Modal.Confirm("재료가 붕어빵 " + S.BakeableCount + "개 분량뿐이다.\n(하루 장사엔 " + GameState.DailyStockTarget + "개 이상 필요)",
                    "냉장고 열기", "그냥 출근", () => MarketPopup.Open(false, Refresh), GoOut);
                return;
            }
            GoOut();
        }

        void GoOut()
        {
            Sfx.Play("door");
            Game.Go<MapScreen>();
        }

        // 방에 쌓이는 추억 — 플래그에 따라 소품이 늘어난다.
        void BuildDecor()
        {
            if (S.Has("memory_first_day"))
            {
                var crate = UIKit.Sprite(Root, "Sprites/crate");
                crate.rectTransform.Foot(420, 352, 90, 48);
            }
            if (S.Has("memory_combo10"))
            {
                var lantern = UIKit.Sprite(Root, "Sprites/lantern");
                lantern.rectTransform.At(250, 20, 22, 56);
                lantern.gameObject.AddComponent<Bob>().amp = 0.6f;
            }
            if (S.Has("memory_gift"))
            {
                var tray = UIKit.Sprite(Root, "Sprites/tray_taiyaki");
                tray.rectTransform.At(28, 88, 56, 28);
            }
            if (S.Has("memory_karaoke"))
            {
                var sign = UIKit.Sprite(Root, "UI/hanging_sign");
                sign.rectTransform.At(505, 60, 46, 42);
                var t = UIKit.Label(sign.transform, "노래방\n♪", 7, Pal.Cream, TextAnchor.MiddleCenter, true, true);
                t.rectTransform.Fill(0, 10, 0, 0);
            }
            if (S.Has("memory_date"))
            {
                var frame = UIKit.Frame(Root, "paper", null, 4f);
                frame.rectTransform.At(98, 18, 62, 40);
                frame.rectTransform.localRotation = Quaternion.Euler(0, 0, 4);
                var photo = UIKit.Img(frame.transform, Art.Get("CG/cg_first_taiyaki"), Color.white, "Photo");
                photo.rectTransform.Fill(3, 3, 3, 3);
            }
            if (S.molds >= 3)
            {
                var trophy = UIKit.Sprite(Root, "Sprites/grill6");
                trophy.rectTransform.At(420, 92, 40, 16);
            }
        }
    }

    /// <summary>핫스팟 마우스 오버 시 살짝 빛나게</summary>
    public class HotspotGlow : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public Image glow;
        public Transform label;
        float target;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { target = 0.18f; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { target = 0f; }

        void Update()
        {
            if (glow == null) return;
            var c = glow.color;
            c.a = Mathf.Lerp(c.a, target, Time.unscaledDeltaTime * 12f);
            glow.color = c;
            if (label != null) label.localScale = Vector3.one * (1f + c.a * 0.5f);
        }
    }

    // ───────────────────────────── 냉장고 / 재료 시장 ─────────────────────────────
    public static class MarketPopup
    {
        public static void Open(bool market, System.Action onChange)
        {
            var S = Game.S;
            var m = Modal.Open(market ? "쇼핑거리 · 재료 가게" : "냉장고 · 재료 (배달 주문)", 360, 250, "paper", true, onChange);
            var c = m.Content;
            var money = UIKit.Label(c, "", 11, Pal.Brown, TextAnchor.MiddleRight, true);
            money.rectTransform.At(170, 4, 150, 16);
            System.Action refreshMoney = () => money.text = "보유금 " + UIKit.Yen(S.money);
            refreshMoney();

            float y = 26;
            foreach (var ing in GameData.Root.ingredients)
            {
                if (ing.id != "batter" && !S.IsUnlocked(ing.id)) continue;
                var row = UIKit.Frame(c, "dark");
                row.rectTransform.At(0, y, 340, 40);
                var icon = UIKit.Sprite(row.transform, ing.sprite);
                var f = GameData.Filling(ing.id);
                if (f != null && !string.IsNullOrEmpty(f.tint)) icon.color = GameData.Hex(f.tint, Color.white);
                icon.rectTransform.At(6, 4, 40, 32);
                var name = UIKit.Label(row.transform, ing.name, 11, Pal.Cream, TextAnchor.MiddleLeft, true, true);
                name.rectTransform.At(52, 4, 110, 16);
                var stock = UIKit.Label(row.transform, "", 9, Pal.Sky, TextAnchor.MiddleLeft, false, true);
                stock.rectTransform.At(52, 20, 110, 14);
                string id = ing.id;
                System.Action refreshStock = () => stock.text = "재고 " + S.Stock(id) + "개";
                refreshStock();
                var ingDef = ing;
                var buy = UIKit.Button(row.transform, ing.packSize + "개  " + UIKit.Yen(ing.packPrice), () =>
                {
                    if (!S.Spend(ingDef.packPrice)) { Sfx.Play("fail"); Game.Toast("돈이 부족하다…", Pal.Bad); return; }
                    S.AddStock(id, ingDef.packSize);
                    Sfx.Play("coin");
                    refreshStock();
                    refreshMoney();
                }, "button", 10);
                ((RectTransform)buy.transform).At(214, 7, 118, 26);
                y += 44;
            }
            var hint = UIKit.Label(c, "붕어빵 1개 = 반죽 1 + 속 재료 1.  하루 장사에 20~30개 정도 팔린다.", 9, new Color(0.45f, 0.3f, 0.2f), TextAnchor.MiddleLeft);
            hint.rectTransform.At(0, y + 2, 340, 16);
        }
    }

    // ───────────────────────────── 책상: 업그레이드 / 레시피 연구 ─────────────────────────────
    public static class DeskPopup
    {
        public static void Open(MonoBehaviour host)
        {
            var S = Game.S;
            var m = Modal.Open("책상 · 가게 업그레이드 / 레시피 연구", 480, 290, "paper");
            var c = m.Content;

            // 가게 업그레이드 — 노점 그림 자체가 변화한다
            var left = UIKit.Frame(c, "dark");
            left.rectTransform.At(0, 6, 225, 250);
            var lt = UIKit.Label(left.transform, "가게 LEVEL " + S.stallLevel, 12, Pal.Gold, TextAnchor.MiddleLeft, true, true);
            lt.rectTransform.At(10, 6, 200, 18);
            var stall = UIKit.Sprite(left.transform, "Sprites/stall");
            stall.rectTransform.At(40, 26, 140, 90);
            if (S.stallLevel < 2) stall.color = new Color(0.75f, 0.7f, 0.65f);
            float y = 120;
            foreach (var lv in GameData.Root.stallLevels)
            {
                bool cur = lv.level == S.stallLevel;
                var t = UIKit.Label(left.transform, (cur ? "▶ " : "   ") + "LV" + lv.level + "  " + lv.desc, 8,
                    lv.level <= S.stallLevel ? Pal.Cream : new Color(0.6f, 0.55f, 0.5f), TextAnchor.MiddleLeft, false, true);
                t.rectTransform.At(8, y, 212, 13);
                y += 13;
            }
            foreach (var up in GameData.Root.upgrades)
            {
                bool owned = S.molds >= up.molds;
                var upDef = up;
                var b = UIKit.Button(left.transform, owned ? up.name + " (완료)" : up.name + "  " + UIKit.Yen(up.price), () =>
                {
                    if (S.molds >= upDef.molds) return;
                    Modal.Confirm(upDef.desc + "\n\n" + UIKit.Yen(upDef.price) + "을(를) 투자할까?\n(주말 데이트 자금이 줄어든다)", "투자한다", "고민해본다", () =>
                    {
                        if (!S.Spend(upDef.price)) { Sfx.Play("fail"); Game.Toast("돈이 부족하다…", Pal.Bad); return; }
                        S.molds = upDef.molds;
                        S.stallLevel = Mathf.Max(S.stallLevel, upDef.level);
                        Sfx.Play("perfect");
                        Game.Toast("붕어빵 틀이 " + S.molds + "개가 되었다!", Pal.Gold);
                        m.Close();
                    });
                }, owned ? "dark" : "button", 10);
                b.interactable = !owned;
                ((RectTransform)b.transform).At(10, 214, 205, 26);
            }

            // 레시피 연구 — 연애 대화가 게임 플레이 정보가 된다
            var right = UIKit.Frame(c, "dark");
            right.rectTransform.At(235, 6, 225, 250);
            var rt = UIKit.Label(right.transform, "레시피 연구", 12, Pal.Gold, TextAnchor.MiddleLeft, true, true);
            rt.rectTransform.At(10, 6, 200, 18);
            y = 30;
            foreach (var f in GameData.Root.fillings)
            {
                var row = UIKit.Frame(right.transform, "wood");
                row.rectTransform.At(8, y, 209, 41);
                var icon = UIKit.Sprite(row.transform, "Sprites/bowl_" + f.sprite);
                if (!string.IsNullOrEmpty(f.tint)) icon.color = GameData.Hex(f.tint, Color.white);
                icon.rectTransform.At(6, 5, 32, 31);
                bool unlocked = S.IsUnlocked(f.id);
                bool hinted = string.IsNullOrEmpty(f.requireFlag) || S.Has(f.requireFlag);
                bool comingSoon = f.researchPrice <= 0 && !f.startUnlocked;
                string nm = unlocked || hinted ? f.name : "???";
                var name = UIKit.Label(row.transform, nm + (unlocked ? "  <color=#8be06a>판매 중</color>" : ""), 10, Pal.Cream, TextAnchor.MiddleLeft, true, true);
                name.rectTransform.At(44, 3, 160, 15);
                string desc = unlocked ? UIKit.Yen(f.price) + " / 개" : comingSoon ? "추후 업데이트" : hinted ? f.researchDesc : "누군가의 이야기에서 힌트를 얻을 수 있을지도…";
                var d = UIKit.Label(row.transform, desc, 8, new Color(0.9f, 0.85f, 0.75f), TextAnchor.UpperLeft, false, true);
                d.rectTransform.At(44, 18, 160, 22);
                if (!unlocked && hinted && !comingSoon)
                {
                    icon.color = Color.white;
                    var fd = f;
                    var b = UIKit.Button(row.transform, "연구 " + UIKit.Yen(f.researchPrice), () =>
                    {
                        if (!S.Spend(fd.researchPrice)) { Sfx.Play("fail"); Game.Toast("돈이 부족하다…", Pal.Bad); return; }
                        S.unlockedFillings.Add(fd.id);
                        S.AddStock(fd.id, 6);
                        Sfx.Play("perfect");
                        Game.Toast("신메뉴 「" + fd.name + "」 완성! 재료 6개 증정", Pal.Gold);
                        m.Close();
                    }, "button", 9);
                    ((RectTransform)b.transform).At(126, 18, 78, 19);
                    d.rectTransform.sizeDelta = new Vector2(80, 22);
                }
                else if (!unlocked) icon.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                y += 44;
            }
        }
    }
}
