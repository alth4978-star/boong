using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 스마트폰 메시지 화면. 답장은 호감도보다 "다음 약속"을 잡는 장치다.
    /// </summary>
    public class PhonePopup : MonoBehaviour
    {
        const float PW = 210, PH = 320;
        RectTransform content;
        ScrollRect scroll;
        RectTransform choiceArea;
        HeroineDef heroine;
        Action onClose;

        public static PhonePopup Open(HeroineDef h, Action onClose = null)
        {
            var root = UIKit.New("Phone", Game.I.PopupLayer).Fill();
            var dim = UIKit.Blocker(root, new Color(0, 0, 0, 0.55f));
            var p = root.gameObject.AddComponent<PhonePopup>();
            p.heroine = h;
            p.onClose = onClose;
            UIKit.Clickable(dim.rectTransform, p.Close, false, false);
            p.Build(root);
            Sfx.Play("notify", 0.5f);
            return p;
        }

        void Build(RectTransform root)
        {
            var phone = UIKit.Frame(root, "phone", null, 2f);
            phone.raycastTarget = true;
            phone.rectTransform.Mid(Game.W / 2, Game.H / 2 + 6, PW, PH);
            Tw.Run(this, Tw.Pop(phone.transform, 0.2f, 1.04f));

            // 상단 바
            var top = UIKit.Rect(phone.transform, GameData.Hex(heroine.themeMain, Pal.Brown), "Header");
            top.rectTransform.At(6, 6, PW - 12, 36);
            var name = UIKit.Label(top.transform, heroine.name + "  <size=8>" + heroine.nameEn + "</size>", 12, Color.white, TextAnchor.MiddleLeft, true, true);
            name.rectTransform.At(10, 2, 130, 20);
            var stage = UIKit.Label(top.transform, "♡ " + Game.S.StageName(heroine), 8, GameData.Hex(heroine.themeAccent, Pal.Gold), TextAnchor.MiddleLeft, true);
            stage.rectTransform.At(10, 20, 150, 14);
            var x = UIKit.Button(top.transform, "×", Close, "dark", 9);
            ((RectTransform)x.transform).At(PW - 38, 7, 22, 20);

            // 대화 스크롤
            var view = UIKit.New("Viewport", phone.transform).At(8, 44, PW - 16, PH - 44 - 84);
            view.gameObject.AddComponent<RectMask2D>();
            var viewImg = view.gameObject.AddComponent<Image>();
            viewImg.color = new Color(0.95f, 0.93f, 0.9f, 1f);
            content = UIKit.New("Content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 10);
            scroll = phone.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;

            choiceArea = UIKit.New("Choices", phone.transform).At(8, PH - 82, PW - 16, 74);
            Rebuild();
        }

        void Rebuild()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            for (int i = choiceArea.childCount - 1; i >= 0; i--) Destroy(choiceArea.GetChild(i).gameObject);

            var s = Game.S;
            float y = 8;
            float w = PW - 16;
            InboxMsg pending = null;
            foreach (var m in s.inbox)
            {
                if (m.heroine != heroine.id) continue;
                var def = GameData.Message(heroine, m.id);
                if (def == null) continue;
                string stamp = string.Format("WEEK {0} · {1}요일", m.week, GameState.DayKo[Mathf.Clamp(m.day, 0, 6)]);
                var st = UIKit.Label(content, stamp, 7, new Color(0.5f, 0.45f, 0.45f), TextAnchor.MiddleCenter, true);
                st.rectTransform.At(0, y, w, 12);
                y += 14;
                m.read = true;
                foreach (var t in def.texts) y = Bubble(t, false, y, w);
                if (m.answer >= 0 && def.choices != null && m.answer < def.choices.Length)
                {
                    var c = def.choices[m.answer];
                    y = Bubble(c.text, true, y, w);
                    if (c.reply != null) foreach (var r in c.reply) y = Bubble(r.text, r.who == "hero", y, w);
                }
                else if (m.answer == InboxMsg.Skipped)
                {
                    var skipped = UIKit.Label(content, "(답장하지 않았다)", 7, new Color(0.6f, 0.55f, 0.55f), TextAnchor.MiddleRight);
                    skipped.rectTransform.At(0, y, w - 8, 12);
                    y += 14;
                }
                else if (def.choices != null && def.choices.Length > 0) pending = m;
                y += 6;
            }
            if (y <= 8)
            {
                var none = UIKit.Label(content, "아직 메시지가 없다.", 9, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
                none.rectTransform.At(0, 40, w, 20);
                y = 80;
            }
            content.sizeDelta = new Vector2(0, y + 6);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;

            if (pending != null)
            {
                var def = GameData.Message(heroine, pending.id);
                float bh = Mathf.Min(22, 72f / def.choices.Length - 2);
                for (int i = 0; i < def.choices.Length; i++)
                {
                    int idx = i;
                    var msg = pending;
                    var b = UIKit.Button(choiceArea, "[" + def.choices[i].text + "]", () => Answer(msg, idx), "button", 9, null, false);
                    ((RectTransform)b.transform).At(0, i * (bh + 2), PW - 16, bh);
                }
            }
            else
            {
                var hint = UIKit.Label(choiceArea, "— 새 메시지 없음 —", 8, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter, true);
                hint.rectTransform.Fill();
            }
        }

        float Bubble(string text, bool mine, float y, float w)
        {
            text = text.Replace("{name}", heroine.name);
            float maxW = w * 0.72f;
            var probe = UIKit.Label(content, text, 9, Color.black, TextAnchor.UpperLeft);
            probe.rectTransform.At(0, 0, maxW - 12, 10);
            float tw = Mathf.Min(maxW - 12, probe.preferredWidth + 1);
            probe.rectTransform.sizeDelta = new Vector2(tw, 10);
            float th = probe.preferredHeight;
            Destroy(probe.gameObject);

            var bub = UIKit.Frame(content, mine ? "bubble_right" : "bubble_left", null, 3f);
            float bw = tw + 12, bh = th + 8;
            bub.rectTransform.At(mine ? w - bw - 6 : 6, y, bw, bh);
            var t = UIKit.Label(bub.transform, text, 9, mine ? new Color(0.25f, 0.13f, 0.05f) : new Color(0.15f, 0.12f, 0.12f), TextAnchor.UpperLeft);
            t.rectTransform.Fill(6, 4, 6, 4);
            return y + bh + 4;
        }

        void Answer(InboxMsg m, int idx)
        {
            var def = GameData.Message(heroine, m.id);
            var c = def.choices[idx];
            m.answer = idx;
            var s = Game.S;
            int before = s.Stage(heroine);
            s.AddAffection(c.affection);
            if (!string.IsNullOrEmpty(c.flag))
                foreach (var f in c.flag.Split(',')) s.SetWeek(f.Trim());
            if (c.affection > 0) Sfx.Play("heart", 0.6f);
            else Sfx.Play("notify", 0.4f);
            if (s.Stage(heroine) > before) Game.Toast("♥ 관계가 깊어졌다: " + s.StageName(heroine), new Color(1f, 0.7f, 0.8f));
            Rebuild();
        }

        public void Close()
        {
            Destroy(gameObject);
            if (onClose != null) onClose();
        }
    }
}
