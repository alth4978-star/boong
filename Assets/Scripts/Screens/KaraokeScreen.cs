using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 노래방 — 30~60초짜리 간단한 박자 입력 이벤트 게임.
    /// ♥ (F / 왼쪽 버튼), ★ (J / 오른쪽 버튼). 성공하면 특별 대사와 호감도 보너스.
    /// </summary>
    public class KaraokeScreen : ScreenBase
    {
        const float Travel = 1.7f;          // 노트가 화면을 가로지르는 시간
        const float PerfectWin = 0.07f, GoodWin = 0.15f;
        const float HitX = 96, SpawnX = 628, LaneY0 = 282, LaneY1 = 312;

        class Note
        {
            public float time;
            public int lane;
            public RectTransform rt;
            public bool done;
        }

        HeroineDef h;
        HeroineView view;
        DialogueBox dlg;
        readonly List<Note> notes = new List<Note>();
        RectTransform track;
        Text judgeText, comboText, lyricText, scoreText;
        Image[] hitRings = new Image[2];
        int score, combo, maxCombo, perfect, good, miss;
        bool playing, finished;
        float songLen, beat, clock, lastNote;
        int lyricIndex = -1;

        public override void Build()
        {
            h = GameData.Rina;
            Sfx.Bgm(null);
            UIKit.Sprite(Root, "Backgrounds/karaoke").rectTransform.Fill();
            UIKit.Rect(Root, new Color(0.1f, 0.02f, 0.2f, 0.2f)).rectTransform.Fill();
            view = HeroineView.Create(Root, h, 320, 262, 200);

            var lyricBox = UIKit.Frame(Root, "neon_cyan");
            lyricBox.rectTransform.At(150, 36, 340, 30);
            lyricText = UIKit.Label(lyricBox.transform, "♪ " + h.name + "의 18번 — 「첫눈 오는 밤」 ♪", 11, Color.white, TextAnchor.MiddleCenter, false, true);
            lyricText.rectTransform.Fill(6, 0, 6, 0);
            var hud = Hud.Create(Root, HudStyle.Neon, 340);
            hud.timeOverride = "23:10";
            hud.extra = "KARAOKE ♪";

            // 박자 트랙
            track = UIKit.New("Track", Root).At(0, 262, 640, 70);
            var tbg = UIKit.Frame(track, "neon");
            tbg.rectTransform.Fill(10, 0, 10, 0);
            for (int l = 0; l < 2; l++)
            {
                var lane = UIKit.Rect(track, new Color(1, 1, 1, 0.07f), "Lane");
                lane.rectTransform.At(20, (l == 0 ? LaneY0 : LaneY1) - 262 - 10, 600, 20);
                var ring = UIKit.Img(track, Art.Get("Frames/circle"), l == 0 ? Pal.A(Pal.Pink, 0.5f) : Pal.A(Pal.Teal, 0.5f), "HitRing");
                ring.rectTransform.Mid(HitX, (l == 0 ? LaneY0 : LaneY1) - 262, 24, 24);
                hitRings[l] = ring;
                var key = UIKit.Label(track, l == 0 ? "F" : "J", 8, Color.white, TextAnchor.MiddleCenter, true, true);
                key.rectTransform.Mid(HitX, (l == 0 ? LaneY0 : LaneY1) - 262, 24, 24);
            }
            judgeText = UIKit.Label(Root, "", 13, Pal.Perfect, TextAnchor.MiddleCenter, true, true);
            judgeText.rectTransform.Mid(HitX + 10, 244, 140, 20);
            comboText = UIKit.Label(Root, "", 10, Color.white, TextAnchor.MiddleLeft, true, true);
            comboText.rectTransform.At(180, 236, 120, 18);
            scoreText = UIKit.Label(Root, "", 10, Pal.Teal, TextAnchor.MiddleRight, true, true);
            scoreText.rectTransform.At(480, 236, 140, 18);

            var b1 = UIKit.Button(Root, "♥  (F)", () => Hit(0), "button_neon", 12);
            ((RectTransform)b1.transform).At(160, 334, 150, 24);
            var b2 = UIKit.Button(Root, "★  (J)", () => Hit(1), "neon_cyan", 12);
            ((RectTransform)b2.transform).At(330, 334, 150, 24);

            dlg = DialogueBox.Create(Root, "wine", GameData.Hex(h.themeAccent, Pal.Gold), Pal.Cream, h, 150, 80);
            dlg.onLine = l => { if (l.who == h.id) view.SetExpression(l.expr); };

            BuildChart();
            view.SetSway(2f, Mathf.PI / beat);
            dlg.Play(new[]
            {
                new Line { who = h.id, text = "자, 내 18번! 박자 맞춰서 탬버린 쳐 줘야 해~", expr = "smile" },
                new Line { who = "", text = "노트가 원에 겹칠 때 ♥ 는 F(왼쪽 버튼), ★ 는 J(오른쪽 버튼)!" },
            }, () => StartCoroutine(Countdown()));
        }

        void BuildChart()
        {
            var song = Sfx.KaraokeChart;
            float step = 60f / song.bpm / 2f;
            beat = step * 2f;
            songLen = step * 8 * song.bars;
            int prev = 0, lane = 0;
            for (int bar = 0; bar < song.bars; bar++)
            {
                var m = song.melody[bar % song.melody.Length].Split(' ');
                for (int i = 0; i < m.Length; i++)
                {
                    int note;
                    if (!int.TryParse(m[i], out note)) continue;
                    if (i % 2 == 1) continue;   // 정박(4분음표)만 — 간단한 박자 입력
                    if (prev != 0) lane = note > prev ? 1 : note < prev ? 0 : lane;
                    prev = note;
                    notes.Add(new Note { time = (bar * 8 + i) * step, lane = lane });
                    lastNote = (bar * 8 + i) * step;
                }
            }
        }

        IEnumerator Countdown()
        {
            var t = UIKit.Label(Root, "", 30, Color.white, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Mid(320, 150, 200, 50);
            for (int i = 3; i >= 1; i--)
            {
                t.text = i.ToString();
                Sfx.Play("hit");
                Tw.Run(t, Tw.Pop(t.transform, 0.3f, 1.3f));
                yield return new WaitForSeconds(0.6f);
            }
            t.text = "START!";
            Destroy(t.gameObject, 0.6f);
            Sfx.Bgm("song", true);
            clock = 0;
            playing = true;
        }

        float SongTime { get { return clock; } }

        void Update()
        {
            if (!playing || finished) return;
            var src = Sfx.BgmSource;
            if (src != null && src.isPlaying) clock = src.time;
            else clock += Time.deltaTime;
            float t = clock;

            foreach (var n in notes)
            {
                if (n.done) continue;
                float dt = n.time - t;
                if (dt > Travel) continue;
                if (n.rt == null)
                {
                    var img = UIKit.Label(track, n.lane == 0 ? "♥" : "★", 16, n.lane == 0 ? Pal.Pink : Pal.Teal, TextAnchor.MiddleCenter, true, true);
                    n.rt = img.rectTransform;
                    n.rt.Mid(SpawnX, (n.lane == 0 ? LaneY0 : LaneY1) - 262, 24, 24);
                }
                float x = HitX + (SpawnX - HitX) * (dt / Travel);
                n.rt.anchoredPosition = new Vector2(x, n.rt.anchoredPosition.y);
                if (dt < -GoodWin) Judge(n, 0);
            }

            // 가사 & 히로인 리듬
            if (h.lyrics != null && h.lyrics.Length > 0)
            {
                int idx = Mathf.Clamp((int)((t - beat * 8) / (beat * 8)), -1, h.lyrics.Length - 1);
                if (idx != lyricIndex && idx >= 0) { lyricIndex = idx; lyricText.text = "♪ " + h.lyrics[idx] + " ♪"; }
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.LeftArrow)) Hit(0);
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.RightArrow)) Hit(1);
#endif
            if (t >= songLen || (AllDone() && t > lastNote + 0.6f)) StartCoroutine(Finish());
        }

        bool AllDone()
        {
            foreach (var n in notes) if (!n.done) return false;
            return true;
        }

        void Hit(int lane)
        {
            if (!playing || finished) return;
            Tw.Run(this, Tw.Pop(hitRings[lane].transform, 0.12f, 1.3f));
            float t = SongTime;
            Note best = null;
            foreach (var n in notes)
            {
                if (n.done || n.lane != lane) continue;
                if (Mathf.Abs(n.time - t) <= GoodWin && (best == null || Mathf.Abs(n.time - t) < Mathf.Abs(best.time - t))) best = n;
            }
            if (best == null) { Sfx.Play("click", 0.3f, 0.8f); return; }
            float d = Mathf.Abs(best.time - t);
            Judge(best, d <= PerfectWin ? 2 : 1);
        }

        void Judge(Note n, int grade)
        {
            n.done = true;
            if (n.rt != null) Destroy(n.rt.gameObject);
            if (grade == 2) { perfect++; score += 2; combo++; judgeText.text = "PERFECT"; judgeText.color = Pal.Perfect; Sfx.Play("hit", 0.6f, 1.2f); }
            else if (grade == 1) { good++; score += 1; combo++; judgeText.text = "GOOD"; judgeText.color = Pal.Good; Sfx.Play("hit", 0.5f); }
            else { miss++; combo = 0; judgeText.text = "MISS"; judgeText.color = Pal.Bad; }
            maxCombo = Mathf.Max(maxCombo, combo);
            comboText.text = combo >= 3 ? combo + " COMBO" : "";
            scoreText.text = "SCORE " + score * 50;
            Tw.Run(this, Tw.Pop(judgeText.transform, 0.15f, 1.2f));
            if (combo == 10) view.SetExpression("smile");
            if (combo == 25) view.SetExpression("love");
        }

        IEnumerator Finish()
        {
            if (finished) yield break;
            finished = true;
            playing = false;
            foreach (var n in notes) if (!n.done) { n.done = true; miss++; if (n.rt != null) Destroy(n.rt.gameObject); }
            yield return new WaitForSeconds(0.8f);
            Sfx.Bgm("weekend");

            float rate = notes.Count == 0 ? 1f : score / (2f * notes.Count);
            string rank = rate >= 0.85f ? "S" : rate >= 0.7f ? "A" : rate >= 0.45f ? "B" : "C";
            var m = Modal.Open("RESULT", 260, 150, "neon", false);
            var t = UIKit.Label(m.Content, "RANK  <size=26>" + rank + "</size>", 14, Pal.Teal, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.At(0, 0, 240, 40);
            var d = UIKit.Label(m.Content, string.Format("PERFECT {0}   GOOD {1}   MISS {2}\n최대 콤보 {3}", perfect, good, miss, maxCombo), 10, Color.white, TextAnchor.MiddleCenter, false, true);
            d.rectTransform.At(0, 44, 240, 36);
            bool closed = false;
            var b = UIKit.Button(m.Content, "계속", () => { m.Close(); closed = true; }, "button_neon", 11);
            ((RectTransform)b.transform).At(70, 88, 100, 24);
            while (!closed) yield return null;

            int bonus = rate >= 0.7f ? 10 : rate >= 0.45f ? 6 : 2;
            S.AddAffection(bonus);
            S.SetWeek("karaoke_done");
            S.Set("memory_karaoke");
            Sfx.Play("heart");
            var lines = rate >= 0.7f ? h.karaokeGreat : rate >= 0.45f ? h.karaokeOk : h.karaokeBad;
            dlg.Play(lines, () => Game.I.EnterNight());
        }

        public override void OnLeave()
        {
            Sfx.Bgm(null);
        }
    }

    /// <summary>
    /// 일요일 데이트 — 겨울 축제. 정말 중요한 장면에서만 풀 일러스트 CG로 전환한다.
    /// </summary>
    public class DateScreen : ScreenBase
    {
        HeroineDef h;
        HeroineView view;
        DialogueBox dlg;
        Image cg;
        CanvasGroup cgGroup;

        public override void Build()
        {
            h = GameData.Rina;
            Sfx.Bgm("date");
            UIKit.Sprite(Root, "Backgrounds/stall_day").rectTransform.Fill();
            UIKit.Rect(Root, new Color(1f, 0.85f, 0.7f, 0.08f)).rectTransform.Fill();
            UIKit.New("Snow", Root).Fill().gameObject.AddComponent<SnowFall>();
            view = HeroineView.Create(Root, h, 420, 330, 210);
            var hero = UIKit.Sprite(Root, "Characters/hero");
            hero.rectTransform.Foot(200, 340, 110, 150);
            var hud = Hud.Create(Root, HudStyle.Day, 340);
            hud.timeOverride = "15:00";
            hud.extra = "♥ 겨울 축제";

            // CG 레이어
            var cgRoot = UIKit.New("CG", Root).Fill();
            cgGroup = cgRoot.gameObject.AddComponent<CanvasGroup>();
            cgGroup.alpha = 0;
            cgGroup.blocksRaycasts = false;
            UIKit.Rect(cgRoot, Color.black).rectTransform.Fill();
            cg = UIKit.Img(cgRoot, Art.Get("CG/cg_first_taiyaki"), Color.white, "CGImage");
            cg.preserveAspect = true;
            cg.rectTransform.Fill();
            UIKit.New("Snow", cgRoot).Fill().gameObject.AddComponent<SnowFall>().count = 30;

            dlg = DialogueBox.Create(Root, "wine", GameData.Hex(h.themeAccent, Pal.Gold), Pal.Cream, h, 262, 90);
            dlg.onLine = l => { if (l.who == h.id) view.SetExpression(l.expr); };
            dlg.Play(h.dateIntro, ChooseTaiyaki);
        }

        void ChooseTaiyaki()
        {
            var labels = new List<string>();
            var ids = new List<string>();
            foreach (var f in GameData.Root.fillings)
                if (S.IsUnlocked(f.id)) { labels.Add(f.name + " 붕어빵을 구워 준다"); ids.Add(f.id); }
            dlg.Options("축제 노점의 철판을 잠깐 빌렸다. 어떤 붕어빵을 구워 줄까?", labels.ToArray(), i =>
            {
                string id = ids[i];
                // 그녀의 추억 루트: 팥 (딸기 크림 힌트를 들었다면 딸기도 기뻐한다)
                bool fav = id == "red" || id == "strawberry";
                S.AddAffection(fav ? 8 : 5);
                Sfx.Play("sizzle");
                StartCoroutine(ShowCg());
            });
        }

        IEnumerator ShowCg()
        {
            yield return Tw.Fade(cgGroup, 0, 1, 1.2f);
            Sfx.Play("heart");
            dlg.Play(h.dateCg, () => StartCoroutine(EndCg()));
        }

        IEnumerator EndCg()
        {
            yield return Tw.Fade(cgGroup, 1, 0, 0.8f);
            view.SetExpression("love");
            dlg.Play(h.dateOutro, () =>
            {
                S.SetWeek("date_done");
                S.Set("memory_date");
                S.Unset("date_accepted");
                Game.I.EnterNight();
            });
        }
    }
}
