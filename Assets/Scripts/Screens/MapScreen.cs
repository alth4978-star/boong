using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 작은 도트 도시 지도 한 장. 같은 지도지만 낮(눈 쌓인 겨울 도시) / 평일 밤 / 주말 밤(네온)에 따라 조명이 완전히 다르다.
    /// </summary>
    public class MapScreen : ScreenBase
    {
        enum Mood { Day, Night, Neon }

        class Node
        {
            public string id, label;
            public Vector2 pos;
            public RectTransform rt;
            public Image glow;
            public Image plate;
        }

        readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>();
        Mood mood;
        RectTransform marker;
        bool moving;

        public override void Build()
        {
            mood = S.day == 5 ? Mood.Neon : S.IsNight ? Mood.Night : Mood.Day;
            Sfx.Bgm(mood == Mood.Neon ? "weekend" : mood == Mood.Night ? "night" : "weekday");

            Color ground = mood == Mood.Day ? Pal.Hex("#DCE6EF") : mood == Mood.Night ? Pal.Hex("#1A2140") : Pal.Hex("#140C24");
            Color road = mood == Mood.Day ? Pal.Hex("#A9B4C2") : mood == Mood.Night ? Pal.Hex("#2E3760") : Pal.Hex("#2A1B44");
            Color block = mood == Mood.Day ? Pal.Hex("#F4F7FA") : mood == Mood.Night ? Pal.Hex("#252D52") : Pal.Hex("#20143A");
            UIKit.Rect(Root, ground, "Ground").rectTransform.Fill();

            // 건물 블록 (지붕 위에 눈)
            var rnd = new System.Random(3);
            for (int i = 0; i < 26; i++)
            {
                float x = (float)rnd.NextDouble() * 600 + 10, y = (float)rnd.NextDouble() * 300 + 40;
                float w = 30 + (float)rnd.NextDouble() * 40, h = 18 + (float)rnd.NextDouble() * 24;
                var b = UIKit.Frame(Root, "round", block, 6f);
                b.rectTransform.At(x, y, w, h);
                var roof = UIKit.Rect(b.transform, mood == Mood.Day ? Color.white : new Color(0.85f, 0.9f, 1f, 0.35f), "Snow");
                roof.rectTransform.At(2, 0, w - 4, 3);
                if (mood != Mood.Day)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var win = UIKit.Rect(b.transform, mood == Mood.Neon && rnd.NextDouble() < 0.4 ? new Color(1f, 0.4f, 0.85f, 0.9f) : new Color(1f, 0.8f, 0.45f, 0.85f), "Window");
                        win.rectTransform.At(5 + k * (w - 10) / 3f, 7, 4, 4);
                    }
                }
            }

            AddNode("home", "집", 92, 168);
            AddNode("station", "역앞", 228, 168);
            AddNode("stall", "붕어빵 노점", 372, 168);
            AddNode("shopping", S.day == 6 && S.Has("date_accepted") ? "쇼핑거리 · 겨울 축제" : "쇼핑거리", 372, 76);
            AddNode("downtown", "번화가", 372, 250);
            AddNode("club", "클럽", 292, 318);
            AddNode("karaoke", "노래방", 452, 318);

            Road("home", "station", road);
            Road("station", "stall", road);
            Road("stall", "shopping", road);
            Road("stall", "downtown", road);
            Road(new Vector2(292, 290), new Vector2(452, 290), road);
            Road("downtown", new Vector2(372, 290), road);
            Road(new Vector2(292, 290), nodes["club"].pos, road);
            Road(new Vector2(452, 290), nodes["karaoke"].pos, road);
            foreach (var n in nodes.Values) n.rt.SetAsLastSibling();

            if (mood == Mood.Day) UIKit.New("Snow", Root).Fill().gameObject.AddComponent<SnowFall>().count = 50;
            else UIKit.Rect(Root, new Color(0.02f, 0.02f, 0.1f, 0.15f)).rectTransform.Fill();

            // 내 위치 표시
            marker = UIKit.New("Me", Root).Foot(nodes["home"].pos.x, nodes["home"].pos.y - 10, 22, 30);
            var me = UIKit.Img(marker, Art.Get("Characters/hero"), Color.white, "Hero");
            me.preserveAspect = true;
            me.rectTransform.Fill();
            marker.gameObject.AddComponent<Bob>().amp = 1.5f;

            Hud.Create(Root, mood == Mood.Neon ? HudStyle.Neon : mood == Mood.Night ? HudStyle.Night : HudStyle.Day, 405);
            var tip = UIKit.Frame(Root, mood == Mood.Neon ? "neon" : "dark");
            tip.rectTransform.At(420, 5, 214, 40);
            var tt = UIKit.Label(tip.transform, Guide(), 9, Pal.Cream, TextAnchor.MiddleCenter, false, true);
            tt.rectTransform.Fill(6, 2, 6, 2);

            if (mood == Mood.Neon) StartCoroutine(NeonOn());
        }

        string Guide()
        {
            if (S.day == 5) return "토요일 밤. 번화가의 네온이 켜진다.\n♥ 번화가로 가자!";
            if (S.day == 6) return S.Has("date_accepted") ? "쇼핑거리의 겨울 축제에서\n" + GameData.Rina.name + "와 만나기로 했다." : "일요일. 쇼핑거리에 들러 볼까?";
            if (S.Today == DayPlan.Work) return "붕어빵 노점으로 출근하자.\n쇼핑거리에서 재료·선물도 살 수 있다.";
            return "오늘은 쉬는 날.\n쇼핑거리에서 선물을 골라 볼까?";
        }

        void AddNode(string id, string label, float x, float y)
        {
            var n = new Node { id = id, label = label, pos = new Vector2(x, y) };
            n.rt = UIKit.New("Node_" + id, Root).Mid(x, y, 110, 40);
            bool neonSpot = id == "downtown" || id == "club" || id == "karaoke";
            n.glow = UIKit.Img(n.rt, Art.Get("Frames/circle"), new Color(1, 1, 1, 0), "Glow");
            n.glow.rectTransform.Mid(55, 20, 120, 60);
            string frame = mood == Mood.Neon ? (neonSpot ? (id == "karaoke" ? "neon_cyan" : "neon") : "dark") : "wood";
            n.plate = UIKit.Frame(n.rt, frame);
            float w = Mathf.Max(56, label.Length * 10 + 18);
            n.plate.rectTransform.Mid(55, 20, w, 22);
            var t = UIKit.Label(n.plate.transform, label, 10, mood == Mood.Neon && neonSpot ? Color.white : Pal.Cream, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Fill();
            if (mood == Mood.Night && (id == "stall" || id == "station" || id == "home")) n.glow.color = new Color(1f, 0.7f, 0.35f, 0.25f);
            if (mood == Mood.Day && id == Highlight()) n.glow.color = new Color(1f, 0.9f, 0.4f, 0.45f);
            if (id == Highlight()) n.glow.gameObject.AddComponent<Blink>().min = 0.4f;
            UIKit.Clickable(n.plate.rectTransform, () => Visit(n));
            nodes[id] = n;
        }

        string Highlight()
        {
            if (S.day == 5) return "downtown";
            if (S.day == 6) return S.Has("date_accepted") ? "shopping" : "home";
            return S.Today == DayPlan.Work ? "stall" : "shopping";
        }

        void Road(string a, string b, Color c) { Road(nodes[a].pos, nodes[b].pos, c); }
        void Road(string a, Vector2 b, Color c) { Road(nodes[a].pos, b, c); }

        void Road(Vector2 a, Vector2 b, Color c)
        {
            var r = UIKit.Rect(Root, c, "Road");
            var d = b - a;
            r.rectTransform.Mid((a.x + b.x) / 2, (a.y + b.y) / 2, d.magnitude + 10, 10);
            r.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        IEnumerator NeonOn()
        {
            // 네온사인이 순서대로 켜진다
            string[] order = { "downtown", "club", "karaoke" };
            Color[] cols = { Pal.Pink, new Color(1f, 0.4f, 0.9f), Pal.Teal };
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < order.Length; i++)
            {
                var n = nodes[order[i]];
                for (int k = 0; k < 3; k++)
                {
                    n.glow.color = Pal.A(cols[i], 0.5f);
                    Sfx.Play("neon", 0.4f);
                    yield return new WaitForSeconds(0.07f);
                    n.glow.color = Pal.A(cols[i], 0.05f);
                    yield return new WaitForSeconds(0.09f);
                }
                n.glow.color = Pal.A(cols[i], 0.5f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        void Visit(Node n)
        {
            if (moving) return;
            StartCoroutine(MoveTo(n));
        }

        IEnumerator MoveTo(Node n)
        {
            moving = true;
            var bob = marker.GetComponent<Bob>();
            bob.enabled = false;
            var target = new Vector2(n.pos.x, -(n.pos.y - 10));
            yield return Tw.Move(marker, target, Mathf.Clamp((marker.anchoredPosition - target).magnitude / 300f, 0.15f, 0.6f));
            bob.Rebase();
            bob.enabled = true;
            moving = false;
            Arrive(n.id);
        }

        void Arrive(string id)
        {
            switch (id)
            {
                case "home":
                    Game.Go<HomeScreen>();
                    break;
                case "station":
                    Game.Toast(S.day >= 5 ? "역 앞은 주말이라 사람이 가득하다." : "퇴근길 사람들이 붕어빵 냄새에 발걸음을 멈춘다.");
                    break;
                case "stall":
                    if (S.Today == DayPlan.Work && !S.IsNight) Game.Go<ShopScreen>();
                    else if (S.day == 5) Game.Toast("오늘은 가게 문을 닫았다. 번화가로 가자!", Pal.Pink);
                    else Game.Toast("오늘은 장사 쉬는 날이다.");
                    break;
                case "shopping":
                    if (S.day == 6 && S.Has("date_accepted") && !S.Has("date_done")) Game.Go<DateScreen>();
                    else if (S.day == 5) Game.Toast("가게들은 이미 문을 닫았다.");
                    else ShoppingPopup.Open();
                    break;
                case "downtown":
                case "club":
                case "karaoke":
                    if (S.day == 5) Game.Go<StreetScreen>();
                    else Game.Toast("번화가는 주말 밤에 가자. 지금은 돈을 모을 때!", Pal.Pink);
                    break;
            }
        }
    }

    /// <summary>쇼핑거리: 선물 가게 + 재료 가게</summary>
    public static class ShoppingPopup
    {
        public static void Open()
        {
            var S = Game.S;
            var m = Modal.Open("쇼핑거리", 380, 250, "paper");
            var c = m.Content;
            var money = UIKit.Label(c, "보유금 " + UIKit.Yen(S.money), 11, Pal.Brown, TextAnchor.MiddleRight, true);
            money.rectTransform.At(200, 4, 150, 16);
            var sub = UIKit.Label(c, "선물 가게 — 주말에 건넬 선물을 고르자", 10, Pal.Ink, TextAnchor.MiddleLeft);
            sub.rectTransform.At(0, 24, 300, 16);
            float y = 44;
            foreach (var g in GameData.Root.gifts)
            {
                var row = UIKit.Frame(c, "dark");
                row.rectTransform.At(0, y, 360, 46);
                var icon = UIKit.Sprite(row.transform, g.sprite);
                icon.rectTransform.At(6, 5, 36, 36);
                var nm = UIKit.Label(row.transform, g.name + (S.items.Contains(g.id) ? "  <color=#8be06a>보유</color>" : ""), 11, Pal.Cream, TextAnchor.MiddleLeft, true, true);
                nm.rectTransform.At(48, 4, 180, 16);
                var ds = UIKit.Label(row.transform, g.desc, 8, new Color(0.9f, 0.85f, 0.75f), TextAnchor.UpperLeft, false, true);
                ds.rectTransform.At(48, 21, 190, 22);
                var gd = g;
                var b = UIKit.Button(row.transform, UIKit.Yen(g.price), () =>
                {
                    if (S.items.Contains(gd.id)) { Game.Toast("이미 가지고 있다."); return; }
                    Modal.Confirm(gd.name + "을(를) " + UIKit.Yen(gd.price) + "에 살까?", "산다", "그만둔다", () =>
                    {
                        if (!S.Spend(gd.price)) { Sfx.Play("fail"); Game.Toast("돈이 부족하다…", Pal.Bad); return; }
                        S.items.Add(gd.id);
                        Sfx.Play("coin");
                        Game.Toast(gd.name + "을(를) 샀다. 주말에 건네 보자.", Pal.Gold);
                        m.Close();
                    });
                }, "button", 10);
                ((RectTransform)b.transform).At(252, 10, 100, 26);
                y += 50;
            }
            var mk = UIKit.Button(c, "재료 가게 (반죽 · 속 재료)", () => { m.Close(); MarketPopup.Open(true, null); }, "button", 10);
            ((RectTransform)mk.transform).At(0, y + 4, 360, 26);
        }
    }

    /// <summary>
    /// 주말 번화가. BGM·UI가 보라/핑크 네온으로 바뀌고, 오늘 만날 히로인을 고른다.
    /// </summary>
    public class StreetScreen : ScreenBase
    {
        readonly List<Text> signs = new List<Text>();

        public override void Build()
        {
            Sfx.Bgm("weekend");
            UIKit.Sprite(Root, "Backgrounds/street_night").rectTransform.Fill();
            UIKit.Rect(Root, new Color(0.15f, 0.02f, 0.2f, 0.25f)).rectTransform.Fill();
            UIKit.New("Snow", Root).Fill().gameObject.AddComponent<SnowFall>().color = new Color(1f, 0.85f, 1f, 0.7f);

            Sign("CLUB  NEON MOON", 40, 60, Pal.Pink, 14);
            Sign("KARAOKE ♪", 470, 70, Pal.Teal, 13);
            Sign("BAR  ♥", 490, 130, new Color(1f, 0.5f, 0.3f), 12);
            Sign("OPEN", 70, 108, new Color(0.6f, 1f, 0.5f), 10);

            Hud.Create(Root, HudStyle.Neon);

            var panel = UIKit.Frame(Root, "neon");
            panel.rectTransform.Mid(320, 214, 300, 216);
            var p = panel.transform;
            var title = UIKit.Label(p, "SATURDAY  20:13", 15, Pal.Teal, TextAnchor.MiddleLeft, true, true);
            title.rectTransform.At(18, 12, 260, 22);

            var h = GameData.Rina;
            Row(p, 40, "♡ " + h.nameEn, "오늘 출근", Pal.Pink, true, h.smallSprite);
            Row(p, 82, "♡ MIKA", "오늘 출근 · 다음 업데이트", new Color(0.7f, 0.6f, 0.8f), false, null);
            Row(p, 118, "♡ YUU", "휴무", new Color(0.5f, 0.5f, 0.6f), false, null);

            var money = UIKit.Label(p, "보유금  " + UIKit.Yen(S.money), 11, Color.white, TextAnchor.MiddleLeft, true, true);
            money.rectTransform.At(18, 152, 170, 18);
            var fee = UIKit.Label(p, "입장료(자릿세) " + UIKit.Yen(GameData.Eco.clubEntry), 8, new Color(1f, 0.8f, 0.95f), TextAnchor.MiddleLeft, false, true);
            fee.rectTransform.At(18, 170, 170, 14);
            var enter = UIKit.Button(p, "입장", Enter, "button_neon", 13);
            ((RectTransform)enter.transform).At(190, 156, 92, 32);
            var back = UIKit.Button(Root, "◀ 돌아가기", () => Game.Go<MapScreen>(), "button_neon", 9);
            ((RectTransform)back.transform).At(8, 334, 80, 20);

            StartCoroutine(SignsOn());
        }

        void Row(Transform p, float y, string name, string status, Color c, bool active, string sprite)
        {
            var row = UIKit.Frame(p, active ? "neon" : "dark");
            row.rectTransform.At(14, y, 272, active ? 38 : 32);
            if (sprite != null)
            {
                var face = UIKit.Img(row.transform, Art.Get(sprite), Color.white, "Face");
                face.preserveAspect = true;
                face.rectTransform.At(6, 2, 20, 34);
            }
            var n = UIKit.Label(row.transform, name, 12, c, TextAnchor.MiddleLeft, true, true);
            n.rectTransform.At(32, 0, 110, active ? 38 : 32);
            var s = UIKit.Label(row.transform, status, 9, active ? Color.white : new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleRight, false, true);
            s.rectTransform.At(120, 0, 144, active ? 38 : 32);
            if (active)
            {
                var stage = UIKit.Label(row.transform, S.StageName(GameData.Rina), 7, Pal.Gold, TextAnchor.MiddleLeft, false, true);
                stage.rectTransform.At(88, 22, 120, 12);
            }
        }

        void Sign(string text, float x, float y, Color c, int size)
        {
            var t = UIKit.Label(Root, text, size, Pal.A(c, 0.08f), TextAnchor.MiddleLeft, true);
            t.rectTransform.At(x, y, 200, 24);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = Pal.A(c, 0.0f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            signs.Add(t);
        }

        IEnumerator SignsOn()
        {
            yield return new WaitForSeconds(0.4f);
            foreach (var t in signs)
            {
                var c = t.color; c.a = 1f;
                var o = t.GetComponent<Outline>();
                for (int k = 0; k < 3; k++)
                {
                    t.color = c; o.effectColor = Pal.A(c, 0.5f);
                    Sfx.Play("neon", 0.35f);
                    yield return new WaitForSeconds(0.06f);
                    t.color = Pal.A(c, 0.1f); o.effectColor = Pal.A(c, 0f);
                    yield return new WaitForSeconds(0.08f);
                }
                t.color = c; o.effectColor = Pal.A(c, 0.55f);
                t.gameObject.AddComponent<Blink>().min = 0.75f;
                yield return new WaitForSeconds(0.2f);
            }
        }

        void Enter()
        {
            int fee = GameData.Eco.clubEntry;
            if (S.money < fee)
            {
                Modal.Confirm("돈이 모자라서 들어갈 수 없다…\n(입장료 " + UIKit.Yen(fee) + ")", "집으로", "조금 더 있기", () =>
                {
                    S.SetWeek("club_skipped");
                    Game.I.EnterNight();
                }, null, "neon");
                return;
            }
            Modal.Confirm(GameData.Rina.name + "를 지명하고 입장할까?\n입장료 " + UIKit.Yen(fee), "입장", "잠깐만", () =>
            {
                S.Spend(fee);
                Sfx.Play("door");
                Game.Go<ClubScreen>();
            }, null, "neon");
        }
    }
}
