using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 밤의 클럽. 테이블 위 메뉴로 술 / 안주 / 선물 / 대화 / 게임 / 노래방 제안.
    /// 호감도 숫자와 취기 수치는 보여주지 않는다 — 표정·자세·대사로 느끼게 한다.
    /// </summary>
    public class ClubScreen : ScreenBase
    {
        HeroineDef h;
        HeroineView view;
        DialogueBox dlg;
        Hud hud;
        RectTransform menu;
        Button karaokeButton;
        Text talkLabel;

        int drunk;          // 누적 취기 (숨김)
        int minutes = 20 * 60 + 30;
        int actions, talks;
        bool gamePlayed, karaokeAsked, ended, warned;
        readonly HashSet<string> giftsGiven = new HashSet<string>();
        const int MaxTalks = 3;

        public override void Build()
        {
            h = GameData.Rina;
            Sfx.Bgm("weekend");
            UIKit.Sprite(Root, "Backgrounds/club").rectTransform.Fill();
            UIKit.Rect(Root, new Color(0.25f, 0.02f, 0.2f, 0.12f)).rectTransform.Fill();

            view = HeroineView.Create(Root, h, 318, 336, 230);
            hud = Hud.Create(Root, HudStyle.Neon, 440);

            // 히로인별 UI 테마: 테두리 색과 작은 장식만 바꾼다 (리나 = 와인 레드 + 금색)
            dlg = DialogueBox.Create(Root, "wine", GameData.Hex(h.themeAccent, Pal.Gold), Pal.Cream, h, 262, 90);
            dlg.onLine = OnLine;

            BuildMenu();
            UpdateHud();
            menu.gameObject.SetActive(false);
            Intro();
        }

        // ───────────────────────── 흐름 ─────────────────────────

        void Intro()
        {
            var lines = new List<Line>();
            if (!S.Has("met_club_once"))
            {
                lines.AddRange(h.firstMeet);
                S.Set("met_club_once");
            }
            else if (S.affection >= 45)
            {
                view.Wave();   // 호감도가 높으면 먼저 손을 흔든다
                lines.AddRange(h.greetHigh);
            }
            else lines.AddRange(h.greetNormal);

            // 타이쿤 파트와 연애 파트가 서로 기억한다
            if (S.Has("rina_ate_perfect")) lines.AddRange(h.memoryPerfect);
            else if (S.Has("rina_visited_shop")) lines.AddRange(h.memoryVisited);
            else if (S.Has("rina_missed")) lines.AddRange(h.memoryMissed);

            // 지킨 약속은 그녀도 기억한다
            if (S.Has("promise_sat"))
            {
                S.Unset("promise_sat");
                S.AddAffection(2);
                lines.AddRange(h.promiseSatKept);
            }
            if (S.Has("dream_partner") && !S.Has("dream_recalled"))
            {
                S.Set("dream_recalled");
                S.AddAffection(2);
                lines.AddRange(h.dreamRecall);
            }
            Play(lines.ToArray(), ShowMenu);
        }

        void Play(Line[] lines, System.Action after)
        {
            menu.gameObject.SetActive(false);
            dlg.Play(lines, after);
        }

        void OnLine(Line l)
        {
            if (l.who == h.id) view.SetExpression(string.IsNullOrEmpty(l.expr) ? (DrunkLevel >= 2 ? "drunk" : "normal") : l.expr);
        }

        int DrunkLevel
        {
            get
            {
                // 0 멀쩡 · 1 살짝 취함 · 2 취함 · 3 위험
                if (drunk <= 0) return 0;
                if (drunk <= 2) return 1;
                if (drunk < h.tolerance) return 2;
                return 3;
            }
        }

        void ShowMenu()
        {
            if (ended) return;
            if (minutes >= 24 * 60)
            {
                Play(h.timeUp, EndNight);
                return;
            }
            UpdateHud();
            menu.gameObject.SetActive(true);
            talkLabel.text = "대화 " + (MaxTalks - talks) + "/" + MaxTalks;
            bool canKaraoke = !karaokeAsked && S.affection >= 18 && actions >= 3 && DrunkLevel <= 2;
            karaokeButton.gameObject.SetActive(canKaraoke);
            if (canKaraoke && !karaokeButton.GetComponent<Blink>()) karaokeButton.gameObject.AddComponent<Blink>().min = 0.6f;
        }

        void Spend(int mins)
        {
            minutes += mins;
            actions++;
            UpdateHud();
        }

        void UpdateHud()
        {
            hud.timeOverride = string.Format("{0:00}:{1:00}", (minutes / 60) % 24, minutes % 60);
            hud.extra = "♡ " + S.StageName(h);
            hud.Refresh();
            view.SetDrunk(DrunkLevel);
        }

        void Affection(int v)
        {
            int before = S.Stage(h);
            S.AddAffection(v);
            if (v > 0) Sfx.Play("heart", 0.5f);
            if (S.Stage(h) > before)
            {
                Game.Toast("♥ 관계가 깊어졌다: " + S.StageName(h), new Color(1f, 0.7f, 0.85f), 3f);
                view.SetExpression("love");
            }
            UpdateHud();
        }

        // ───────────────────────── 테이블 메뉴 ─────────────────────────

        void BuildMenu()
        {
            menu = UIKit.New("TableMenu", Root).At(20, 266, 600, 88);
            var bg = UIKit.Frame(menu, "wine");
            bg.rectTransform.Fill();
            var title = UIKit.Label(menu, "TABLE", 8, GameData.Hex(h.themeAccent, Pal.Gold), TextAnchor.MiddleLeft, true, true);
            title.rectTransform.At(12, 4, 100, 12);

            string[] labels = { "술 주문", "안주 주문", "선물", "대화", "게임", "귀가" };
            System.Action[] acts = { OrderDrink, OrderSnack, Gift, Talk, PlayGame, GoHome };
            for (int i = 0; i < labels.Length; i++)
            {
                int col = i % 3, row = i / 3;
                var b = UIKit.Button(menu, labels[i], acts[i], "button_neon", 11);
                ((RectTransform)b.transform).At(12 + col * 128, 18 + row * 33, 122, 28);
                if (labels[i] == "대화")
                {
                    talkLabel = UIKit.Label(b.transform, "", 7, Pal.Gold, TextAnchor.LowerRight, true, true);
                    talkLabel.rectTransform.Fill(0, 0, 6, 2);
                }
            }
            karaokeButton = UIKit.Button(menu, "♪ 2차 갈래? (노래방)", AskKaraoke, "neon_cyan", 11);
            ((RectTransform)karaokeButton.transform).At(400, 18, 188, 61);
        }

        void Options(string prompt, List<string> labels, System.Action<int> picked)
        {
            menu.gameObject.SetActive(false);
            labels.Add("그만두기");
            int cancel = labels.Count - 1;
            dlg.Options(prompt, labels.ToArray(), i =>
            {
                if (i == cancel) { ShowMenu(); return; }
                picked(i);
            }, "button_neon");
        }

        void OrderDrink()
        {
            var labels = new List<string>();
            foreach (var d in h.drinks) labels.Add(d.name + "  " + UIKit.Yen(d.price) + "  <size=9>" + d.desc + "</size>");
            Options("무엇을 주문할까?  (보유금 " + UIKit.Yen(S.money) + ")", labels, i => Consume(h.drinks[i], true));
        }

        void OrderSnack()
        {
            var labels = new List<string>();
            foreach (var d in h.snacks) labels.Add(d.name + "  " + UIKit.Yen(d.price) + "  <size=9>" + d.desc + "</size>");
            Options("안주를 시킬까?  (보유금 " + UIKit.Yen(S.money) + ")", labels, i => Consume(h.snacks[i], false));
        }

        void Consume(DrinkDef d, bool drink)
        {
            if (!S.Spend(d.price))
            {
                Sfx.Play("fail");
                Play(h.noMoney, ShowMenu);
                return;
            }
            Sfx.Play("clink");
            drunk = Mathf.Max(0, drunk + d.drunk);
            Spend(30);
            Affection(d.affection);
            Play(d.lines, AfterDrink);
        }

        void AfterDrink()
        {
            int lv = DrunkLevel;
            view.SetDrunk(lv);
            if (lv >= 3)
            {
                // 너무 많이 마시면 이벤트가 꼬이고 일찍 귀가한다
                S.SetWeek("rina_went_home_drunk");
                S.AddAffection(-5);
                Play(h.goHomeDrunk, EndNight);
                return;
            }
            if (drunk == h.tolerance - 1 && !warned)
            {
                warned = true;
                Play(h.drunkWarn, ShowMenu);
                return;
            }
            ShowMenu();
        }

        void Gift()
        {
            var labels = new List<string>();
            var ids = new List<string>();
            if (S.giftTaiyaki > 0 && !giftsGiven.Contains("taiyaki"))
            {
                var f = GameData.Filling(S.giftTaiyakiFilling);
                labels.Add("직접 구운 " + (f != null ? f.name : "") + " 붕어빵" + (S.giftTaiyakiPerfect ? " (PERFECT)" : ""));
                ids.Add("taiyaki");
            }
            foreach (var g in GameData.Root.gifts)
                if (S.items.Contains(g.id) && !giftsGiven.Contains(g.id)) { labels.Add(g.name); ids.Add(g.id); }
            if (ids.Count == 0)
            {
                menu.gameObject.SetActive(false);
                dlg.Say("", "건넬 선물이 없다… (장사 후 남은 붕어빵을 포장하거나, 쇼핑거리에서 선물을 살 수 있다)", "", ShowMenu);
                return;
            }
            Options("무엇을 건넬까?", labels, i =>
            {
                string id = ids[i];
                giftsGiven.Add(id);
                Spend(20);
                Sfx.Play("heart");
                S.Set("memory_gift");
                if (id == "taiyaki")
                {
                    bool perfect = S.giftTaiyakiPerfect;
                    S.giftTaiyaki = 0;
                    S.SetWeek("rina_gift_taiyaki");
                    var lines = new List<Line>(perfect ? h.giftTaiyakiPerfect : h.giftTaiyaki);
                    var kept = KeptPromise(S.giftTaiyakiFilling);
                    if (kept != null) lines.AddRange(kept);
                    Affection((perfect ? 12 : 8) + (kept != null ? 4 : 0));
                    Play(lines.ToArray(), ShowMenu);
                }
                else
                {
                    S.items.Remove(id);
                    Affection(id == "scarf" ? 10 : 6);
                    Play(id == "scarf" ? h.giftScarf : h.giftFlower, ShowMenu);
                }
            });
        }

        /// <summary>붕어빵 선물로 지켜진 약속이 있으면 그 대사 (약속은 한 번만)</summary>
        Line[] KeptPromise(string filling)
        {
            if (filling == "red" && S.Has("promise_red")) { S.Unset("promise_red"); return h.promiseRedKept; }
            if (filling == "strawberry" && S.Has("promise_strawberry")) { S.Unset("promise_strawberry"); return h.promiseStrawberryKept; }
            return null;
        }

        void Talk()
        {
            if (talks >= MaxTalks)
            {
                menu.gameObject.SetActive(false);
                dlg.Say(h.id, "오늘은 이야기 많이 했네~ 다른 거 하자!", "smile", ShowMenu);
                return;
            }
            var avail = new List<Topic>();
            foreach (var t in h.topics)
            {
                if (S.Has("topic_" + t.id)) continue;
                if (DrunkLevel < t.minDrunk || DrunkLevel > t.maxDrunk) continue;
                if (S.affection < t.minAffection) continue;
                if (!string.IsNullOrEmpty(t.requireFlag) && !S.Has(t.requireFlag)) continue;
                avail.Add(t);
                if (avail.Count >= 3) break;
            }
            if (avail.Count == 0)
            {
                talks++;
                Spend(30);
                Affection(1);
                Play(new[]
                {
                    new Line { who = "", text = "이런저런 이야기를 나눴다. 시간이 금방 간다." },
                    new Line { who = h.id, text = DrunkLevel >= 1 ? "헤헤… 오늘 기분 좋다~" : "너랑 얘기하면 편하네.", expr = "smile" }
                }, ShowMenu);
                return;
            }
            var labels = new List<string>();
            foreach (var t in avail) labels.Add(t.title);
            Options("무슨 이야기를 할까?", labels, i => RunTopic(avail[i]));
        }

        void RunTopic(Topic t)
        {
            talks++;
            Spend(30);
            S.Set("topic_" + t.id);
            if (!string.IsNullOrEmpty(t.grantFlag)) S.Set(t.grantFlag);
            dlg.Play(t.lines, () =>
            {
                if (t.choices == null || t.choices.Length == 0) { ShowMenu(); return; }
                dlg.Choose(null, t.choices, c =>
                {
                    Affection(c.affection);
                    if (!string.IsNullOrEmpty(c.flag)) foreach (var f in c.flag.Split(',')) S.Set(f.Trim());
                    Play(c.reply, () =>
                    {
                        if (!string.IsNullOrEmpty(t.grantFlag) && t.grantFlag.StartsWith("hint_"))
                            Game.Toast("★ 신메뉴 힌트를 얻었다! (다음에 책상에서 연구)", Pal.Gold, 3f);
                        ShowMenu();
                    });
                }, "button_neon");
            });
        }

        void PlayGame()
        {
            if (gamePlayed)
            {
                menu.gameObject.SetActive(false);
                dlg.Say(h.id, "또? 나 이번엔 안 봐줄 거야~", "smile", ShowMenu);
                return;
            }
            gamePlayed = true;
            Spend(30);
            dlg.Play(h.gameIntro, () =>
            {
                dlg.Options("그녀가 주먹 쥔 손 안의 사탕 개수… 홀일까, 짝일까?", new[] { "홀!", "짝!" }, i =>
                {
                    bool win = Random.value < 0.5f;
                    Sfx.Play(win ? "perfect" : "fail", 0.6f);
                    Affection(win ? 3 : 2);
                    Play(win ? h.gameWin : h.gameLose, ShowMenu);
                }, "button_neon");
            });
        }

        void AskKaraoke()
        {
            int price = GameData.Eco.karaokePrice;
            if (S.money < price)
            {
                menu.gameObject.SetActive(false);
                dlg.Say("", "노래방 갈 돈이 부족하다… (" + UIKit.Yen(price) + ")", "", ShowMenu);
                return;
            }
            menu.gameObject.SetActive(false);
            dlg.Options("노래방 비용 " + UIKit.Yen(price) + ". 2차로 노래방에 가자고 해 볼까?", new[] { "“2차 갈래?”", "아직은 그만두기" }, i =>
            {
                if (i == 1) { ShowMenu(); return; }
                karaokeAsked = true;
                S.Spend(price);
                S.SetWeek("club_done");
                Affection(2);
                Play(h.karaokeAsk, () => Game.Go<KaraokeScreen>());
            }, "button_neon");
        }

        void GoHome()
        {
            menu.gameObject.SetActive(false);
            dlg.Options("오늘은 이만 들어갈까?", new[] { "응, 들어가자", "조금 더 있을래" }, i =>
            {
                if (i == 1) { ShowMenu(); return; }
                Play(h.farewell, EndNight);
            }, "button_neon");
        }

        void EndNight()
        {
            if (ended) return;
            ended = true;
            S.SetWeek("club_done");
            S.AddAffection(2);
            Game.I.EnterNight();
        }
    }
}
