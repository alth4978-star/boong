using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    public enum HudStyle { Day, Night, Neon }

    /// <summary>화면 상단 최소 정보 바: DAY / 요일 · 시간 · 보유금 · 체력</summary>
    public class Hud : MonoBehaviour
    {
        Text dayText, timeText, moneyText, extraText;
        public string timeOverride;
        public string extra;

        public static Hud Create(Transform parent, HudStyle style, float width = 470)
        {
            var frame = UIKit.Frame(parent, style == HudStyle.Neon ? "neon" : "wood");
            frame.name = "HUD";
            frame.rectTransform.At(6, 5, width, 24);
            var hud = frame.gameObject.AddComponent<Hud>();
            Color main = style == HudStyle.Neon ? Pal.Teal : Pal.Gold;
            Color sub = style == HudStyle.Neon ? new Color(1f, 0.8f, 0.97f) : Pal.Cream;
            hud.dayText = UIKit.Label(frame.transform, "", 10, main, TextAnchor.MiddleLeft, true, true);
            hud.dayText.rectTransform.At(8, 0, 100, 24);
            hud.timeText = UIKit.Label(frame.transform, "", 10, sub, TextAnchor.MiddleLeft, true, true);
            hud.timeText.rectTransform.At(104, 0, 60, 24);
            hud.moneyText = UIKit.Label(frame.transform, "", 10, sub, TextAnchor.MiddleLeft, true, true);
            hud.moneyText.rectTransform.At(160, 0, 90, 24);
            hud.extraText = UIKit.Label(frame.transform, "", 10, new Color(1f, 0.55f, 0.6f), TextAnchor.MiddleLeft, true, true);
            hud.extraText.rectTransform.At(250, 0, width - 256, 24);
            hud.Refresh();
            return hud;
        }

        public void Refresh()
        {
            var s = Game.S;
            if (s == null) return;
            dayText.text = string.Format("DAY {0:00} · {1}", s.DayNumber, GameState.DayEn[s.day]);
            timeText.text = timeOverride ?? (s.IsNight ? "21:30" : (s.day == 5 ? "19:30" : "09:00"));
            moneyText.text = UIKit.Yen(s.money);
            extraText.text = extra ?? ("체력 " + UIKit.Hearts(s.stamina, GameData.Eco.maxStamina));
        }

        void Update()
        {
            if (Time.frameCount % 10 == 0) Refresh();
        }
    }

    /// <summary>모달 팝업</summary>
    public class Modal : MonoBehaviour
    {
        public RectTransform Content;
        public Action onClose;

        public static Modal Open(string title, float w, float h, string frame = "paper", bool closable = true, Action onClose = null)
        {
            var layer = Game.I.PopupLayer;
            var root = UIKit.New("Modal_" + title, layer).Fill();
            var modal = root.gameObject.AddComponent<Modal>();
            modal.onClose = onClose;
            var dim = UIKit.Blocker(root, new Color(0.02f, 0.01f, 0.03f, 0.55f));
            dim.name = "Dim";

            var panel = UIKit.Frame(root, frame);
            panel.rectTransform.Mid(Game.W / 2, Game.H / 2, w, h);
            panel.raycastTarget = true;
            bool dark = frame == "neon" || frame == "dark" || frame == "wood" || frame == "wine" || frame == "neon_cyan";
            if (!string.IsNullOrEmpty(title))
            {
                var tag = UIKit.Frame(panel.transform, dark ? "button_neon" : "button");
                if (frame == "wine") tag.color = new Color(1f, 0.85f, 0.6f);
                tag.rectTransform.At(10, -10, Mathf.Max(90, title.Length * 11 + 24), 20);
                var tt = UIKit.Label(tag.transform, title, 10, Pal.Cream, TextAnchor.MiddleCenter, true, true);
                tt.rectTransform.Fill();
            }
            if (closable)
            {
                var x = UIKit.Button(panel.transform, "×", modal.Close, dark ? "button_neon" : "button", 10);
                ((RectTransform)x.transform).At(w - 26, 6, 20, 18);
            }
            modal.Content = UIKit.New("Content", panel.transform).Fill(10, 14, 10, 10);
            Tw.Run(modal, Tw.Pop(panel.transform, 0.2f, 1.05f));
            return modal;
        }

        public void Close()
        {
            if (this == null) return;
            var cb = onClose;
            Destroy(gameObject);
            if (cb != null) cb();
        }

        public static Modal Confirm(string text, string yes, string no, Action onYes, Action onNo = null, string frame = "paper")
        {
            var m = Open("", 300, 130, frame, false);
            bool dark = frame != "paper";
            var t = UIKit.Label(m.Content, text, 11, dark ? Pal.Cream : Pal.Ink, TextAnchor.MiddleCenter);
            t.rectTransform.At(0, 0, 280, 70);
            string bf = dark ? "button_neon" : "button";
            if (no != null)
            {
                var b1 = UIKit.Button(m.Content, yes, () => { m.onClose = null; m.Close(); if (onYes != null) onYes(); }, bf);
                ((RectTransform)b1.transform).At(30, 76, 100, 24);
                var b2 = UIKit.Button(m.Content, no, () => { m.onClose = null; m.Close(); if (onNo != null) onNo(); }, bf);
                ((RectTransform)b2.transform).At(150, 76, 100, 24);
            }
            else
            {
                var b1 = UIKit.Button(m.Content, yes, () => { m.onClose = null; m.Close(); if (onYes != null) onYes(); }, bf);
                ((RectTransform)b1.transform).At(90, 76, 100, 24);
            }
            return m;
        }

        public static Modal Message(string text, Action ok = null, string frame = "paper")
        {
            return Confirm(text, "확인", null, ok, null, frame);
        }
    }

    /// <summary>
    /// 비주얼노벨식 대화창. 클릭/Space로 진행, 타자기 효과, 선택지 지원.
    /// 히로인별 테마(테두리 색)만 바꿔서 제작량을 줄인다.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        public Action<Line> onLine;
        public HeroineDef heroine;
        Image box, nameTag;
        Text nameText, bodyText, arrow;
        Image clickCatcher;
        RectTransform choiceRoot;
        Queue<Line> queue = new Queue<Line>();
        Action onDone;
        Coroutine typing;
        bool isTyping, waitingChoice;
        string fullText;
        Color bodyColor;

        public bool Busy { get { return gameObject.activeSelf && (queue.Count > 0 || isTyping || waitingChoice || onDone != null); } }

        public static DialogueBox Create(Transform parent, string frame, Color nameColor, Color textColor, HeroineDef heroine = null, float y = 262, float h = 90)
        {
            var root = UIKit.New("Dialogue", parent).Fill();
            var d = root.gameObject.AddComponent<DialogueBox>();
            d.heroine = heroine;
            d.clickCatcher = UIKit.Blocker(root, new Color(0, 0, 0, 0));
            UIKit.Clickable(d.clickCatcher.rectTransform, d.Advance, false, false);

            d.box = UIKit.Frame(root, frame);
            d.box.rectTransform.At(20, y, 600, h);
            d.box.raycastTarget = true;
            UIKit.Clickable(d.box.rectTransform, d.Advance, false, false);

            d.nameTag = UIKit.Frame(root, frame == "paper" ? "button" : frame);
            d.nameTag.rectTransform.At(34, y - 16, 110, 22);
            d.nameText = UIKit.Label(d.nameTag.transform, "", 11, nameColor, TextAnchor.MiddleCenter, true, true);
            d.nameText.rectTransform.Fill();

            d.bodyColor = textColor;
            d.bodyText = UIKit.Label(d.box.transform, "", 13, textColor, TextAnchor.UpperLeft);
            d.bodyText.rectTransform.Fill(18, 14, 18, 10);

            d.arrow = UIKit.Label(d.box.transform, "▼", 9, nameColor, TextAnchor.MiddleCenter, true);
            d.arrow.rectTransform.At(572, h - 20, 16, 14);
            d.arrow.gameObject.AddComponent<Blink>();

            d.choiceRoot = UIKit.New("Choices", root).Fill();
            root.gameObject.SetActive(false);
            return d;
        }

        public void SetTheme(string frame, Color nameColor)
        {
            box.sprite = Art.Frame(frame);
            nameTag.sprite = Art.Frame(frame == "paper" ? "button" : frame);
            nameText.color = nameColor;
            arrow.color = nameColor;
        }

        public void Play(Line[] lines, Action done)
        {
            if (lines == null || lines.Length == 0) { if (done != null) done(); return; }
            foreach (var l in lines) queue.Enqueue(l);
            onDone = done;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Next();
        }

        public void Say(string who, string text, string expr, Action done)
        {
            Play(new[] { new Line { who = who, text = text, expr = expr } }, done);
        }

        public string NameOf(string who)
        {
            if (string.IsNullOrEmpty(who)) return "";
            if (who == "hero") return "나";
            if (heroine != null && who == heroine.id) return heroine.name;
            return who;
        }

        public string Fmt(string s)
        {
            if (s == null) return "";
            if (heroine != null) s = s.Replace("{name}", heroine.name);
            return s;
        }

        void Next()
        {
            if (queue.Count == 0)
            {
                var cb = onDone;
                onDone = null;
                gameObject.SetActive(false);
                if (cb != null) cb();
                return;
            }
            var line = queue.Dequeue();
            string nm = NameOf(line.who);
            nameTag.gameObject.SetActive(nm.Length > 0);
            nameText.text = nm;
            bodyText.color = string.IsNullOrEmpty(line.who) ? Color.Lerp(bodyColor, new Color(0.6f, 0.6f, 0.7f), 0.35f) : bodyColor;
            bodyText.fontStyle = string.IsNullOrEmpty(line.who) ? FontStyle.Italic : FontStyle.Normal;
            if (onLine != null) onLine(line);
            fullText = Fmt(line.text);
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(Type(fullText));
        }

        IEnumerator Type(string s)
        {
            isTyping = true;
            arrow.enabled = false;
            for (int i = 0; i <= s.Length; i++)
            {
                bodyText.text = s.Substring(0, i);
                if (i % 3 == 0 && i < s.Length && s[i] != ' ') Sfx.Play("click", 0.15f, 1.6f);
                yield return new WaitForSecondsRealtime(0.022f);
            }
            isTyping = false;
            arrow.enabled = true;
        }

        public void Advance()
        {
            if (waitingChoice) return;
            if (isTyping)
            {
                if (typing != null) StopCoroutine(typing);
                bodyText.text = fullText;
                isTyping = false;
                arrow.enabled = true;
                return;
            }
            Next();
        }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!waitingChoice && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) Advance();
#endif
        }

        /// <summary>선택지 표시 (대화창에 질문을 띄운 채)</summary>
        public void Options(string prompt, string[] labels, Action<int> picked, string buttonFrame = "button")
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            waitingChoice = true;
            if (!string.IsNullOrEmpty(prompt))
            {
                nameTag.gameObject.SetActive(false);
                bodyText.text = Fmt(prompt);
                bodyText.fontStyle = FontStyle.Normal;
                bodyText.color = bodyColor;
            }
            arrow.enabled = false;
            for (int i = choiceRoot.childCount - 1; i >= 0; i--) Destroy(choiceRoot.GetChild(i).gameObject);
            float bh = 24, gap = 6;
            float top = box.rectTransform.anchoredPosition.y * -1 - labels.Length * (bh + gap) - 8;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = UIKit.Button(choiceRoot, Fmt(labels[i]), () =>
                {
                    waitingChoice = false;
                    for (int k = choiceRoot.childCount - 1; k >= 0; k--) Destroy(choiceRoot.GetChild(k).gameObject);
                    gameObject.SetActive(false);
                    picked(idx);
                }, buttonFrame, 11, null, false);
                ((RectTransform)b.transform).Mid(Game.W / 2, top + i * (bh + gap) + bh / 2, 360, bh);
                Tw.Run(b, Tw.Pop(b.transform, 0.18f, 1.06f));
            }
        }

        public void Choose(string prompt, Choice[] choices, Action<Choice> picked, string buttonFrame = "button")
        {
            var labels = new string[choices.Length];
            for (int i = 0; i < choices.Length; i++) labels[i] = choices[i].text;
            Options(prompt, labels, i => picked(choices[i]), buttonFrame);
        }
    }

    /// <summary>
    /// 히로인 스탠딩. 표정 스프라이트(Characters/{id}_{expr}.png)가 있으면 교체하고,
    /// 없으면 기본 스프라이트 + 감정 아이콘 + 볼터치 + 자세(기울기)로 표현한다.
    /// </summary>
    public class HeroineView : MonoBehaviour
    {
        public HeroineDef def;
        Image body;
        Image blushL, blushR;
        Text emote;
        Bob bob;
        Sprite baseSprite;
        int drunk;
        string expr = "normal";

        public static HeroineView Create(Transform parent, HeroineDef def, float cx, float bottomY, float height)
        {
            var root = UIKit.New("Heroine_" + def.id, parent);
            var sprite = Art.Get(def.sprite);
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 0.45f;
            root.Foot(cx, bottomY, height * aspect, height);
            var hv = root.gameObject.AddComponent<HeroineView>();
            hv.def = def;
            hv.baseSprite = sprite;
            hv.body = UIKit.Img(root, sprite, Color.white, "Body");
            hv.body.rectTransform.Fill();
            hv.body.preserveAspect = true;
            // 볼터치 (얼굴 위치는 스프라이트 비율 기준)
            hv.blushL = UIKit.Img(root, Art.Get("Frames/circle"), new Color(1f, 0.35f, 0.45f, 0f), "BlushL");
            hv.blushL.rectTransform.Mid(height * aspect * 0.60f, height * 0.125f, height * 0.035f, height * 0.018f);
            hv.blushR = UIKit.Img(root, Art.Get("Frames/circle"), new Color(1f, 0.35f, 0.45f, 0f), "BlushR");
            hv.blushR.rectTransform.Mid(height * aspect * 0.76f, height * 0.125f, height * 0.035f, height * 0.018f);
            hv.emote = UIKit.Label(root, "", 18, Color.white, TextAnchor.MiddleCenter, true, true);
            hv.emote.rectTransform.Mid(height * aspect * 1.05f, height * 0.03f, 80, 30);
            hv.bob = root.gameObject.AddComponent<Bob>();
            hv.bob.amp = 1.2f;
            hv.bob.speed = 1.6f;
            return hv;
        }

        public void SetExpression(string e)
        {
            if (string.IsNullOrEmpty(e)) return;
            expr = e;
            var s = Art.TryGet(def.sprite + "_" + e);
            body.sprite = s != null ? s : baseSprite;
            string icon = "";
            Color c = Color.white;
            switch (e)
            {
                case "smile": icon = "♪"; c = new Color(1f, 0.8f, 0.4f); break;
                case "embarrassed": icon = "!!"; c = new Color(1f, 0.5f, 0.6f); break;
                case "drunk": icon = "~♥"; c = new Color(1f, 0.55f, 0.75f); break;
                case "pout": icon = "흥"; c = new Color(0.7f, 0.8f, 1f); break;
                case "serious": icon = "…"; c = new Color(0.8f, 0.8f, 0.9f); break;
                case "love": icon = "♥"; c = new Color(1f, 0.4f, 0.6f); break;
                case "surprised": icon = "!?"; c = new Color(1f, 0.9f, 0.5f); break;
            }
            emote.text = icon;
            emote.color = c;
            if (icon.Length > 0) Tw.Run(this, Tw.Pop(emote.transform, 0.25f, 1.3f));
            UpdateBlush();
        }

        /// <summary>0 멀쩡 · 1 살짝 취함 · 2 취함 · 3 위험 — 수치 대신 자세/볼터치로 보여준다</summary>
        public void SetDrunk(int level)
        {
            drunk = level;
            bob.tilt = level <= 0 ? 0 : level == 1 ? 1.2f : level == 2 ? 3f : 5f;
            bob.tiltSpeed = 1.1f + level * 0.2f;
            bob.amp = 1.2f + level * 0.8f;
            UpdateBlush();
        }

        void UpdateBlush()
        {
            float a = Mathf.Clamp01(drunk * 0.28f + (expr == "embarrassed" || expr == "love" ? 0.45f : 0f));
            var c = blushL.color; c.a = a; blushL.color = c; blushR.color = c;
        }

        public void Wave()
        {
            Tw.Run(this, WaveCo());
        }

        IEnumerator WaveCo()
        {
            emote.text = "♪ 여기야~";
            emote.fontSize = 12;
            emote.color = new Color(1f, 0.85f, 0.5f);
            SetSway(3f, 16f);
            yield return Tw.Wait(0.9f);
            SetDrunk(drunk);
            yield return Tw.Wait(0.8f);
            emote.fontSize = 18;
            emote.text = "";
        }

        /// <summary>좌우 흔들림 직접 지정 (노래 박자 등)</summary>
        public void SetSway(float tilt, float speed)
        {
            bob.tilt = tilt;
            bob.tiltSpeed = speed;
        }
    }
}
