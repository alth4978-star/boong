using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 코드로 UGUI를 조립하는 헬퍼.
    /// 모든 좌표는 640×360 스테이지 기준, 부모의 "왼쪽 위"가 (0,0)이다.
    /// </summary>
    public static class UIKit
    {
        public static RectTransform New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5; // UI
            return (RectTransform)go.transform;
        }

        /// <summary>왼쪽 위 기준 배치</summary>
        public static RectTransform At(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>중심 기준 배치 (cx, cy 는 왼쪽 위 기준 좌표)</summary>
        public static RectTransform Mid(this RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>발밑(아래 중앙) 기준 배치 — 캐릭터용</summary>
        public static RectTransform Foot(this RectTransform rt, float cx, float bottomY, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(cx, -bottomY);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Image Img(Transform parent, Sprite sprite, Color? color = null, string name = "Image")
        {
            var rt = New(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : Art.White;
            img.color = color ?? Color.white;
            img.raycastTarget = false;
            return img;
        }

        public static Image Sprite(Transform parent, string path, Color? color = null)
        {
            var s = Art.Get(path);
            var img = Img(parent, s, color, path);
            img.preserveAspect = true;
            if (s == null) img.color = new Color(1, 0, 1, 0.4f);
            return img;
        }

        public static Image Rect(Transform parent, Color color, string name = "Rect")
        {
            return Img(parent, Art.White, color, name);
        }

        /// <summary>9-slice 프레임 패널</summary>
        public static Image Frame(Transform parent, string frame, Color? tint = null, float ppuMultiplier = 2f)
        {
            var img = Img(parent, Art.Frame(frame), tint, "Frame_" + frame);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMultiplier;
            img.fillCenter = true;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, bool pixel = false, bool outline = false)
        {
            var rt = New("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = pixel ? Art.Pixel : Art.Body;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            t.raycastTarget = false;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.08f, 0.04f, 0.03f, 0.9f);
                o.effectDistance = new Vector2(0.8f, -0.8f);
            }
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, string frame = "button",
            int size = 11, Color? textColor = null, bool pixel = true)
        {
            var img = Frame(parent, frame);
            img.name = "Button_" + label;
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1.12f, 1.08f, 1f);
            cb.pressedColor = new Color(0.8f, 0.75f, 0.7f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.colorMultiplier = 1.2f;
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("click"); onClick(); });
            var t = Label(img.transform, label, size, textColor ?? Pal.Cream, TextAnchor.MiddleCenter, pixel, true);
            t.rectTransform.Fill(3, 2, 3, 2);
            img.gameObject.AddComponent<HoverScale>();
            return b;
        }

        /// <summary>임의의 RectTransform을 클릭 가능하게 만든다.</summary>
        public static UnityEngine.UI.Button Clickable(RectTransform rt, Action onClick, bool hover = true, bool sound = true)
        {
            var g = rt.GetComponent<Graphic>();
            if (g == null)
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
                g = img;
            }
            g.raycastTarget = true;
            var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = g;
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(() => { if (sound) Sfx.Play("click"); onClick(); });
            if (hover) rt.gameObject.AddComponent<HoverScale>();
            return b;
        }

        public static string Yen(int v) { return "¥" + v.ToString("N0"); }

        public static string Hearts(int cur, int max)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < max; i++) sb.Append(i < cur ? "♥" : "♡");
            return sb.ToString();
        }

        /// <summary>전체 화면 가림막 (입력 차단)</summary>
        public static Image Blocker(Transform parent, Color c)
        {
            var img = Rect(parent, c, "Blocker");
            img.rectTransform.Fill();
            img.raycastTarget = true;
            return img;
        }
    }

    // ───────────────────────── 작은 연출 컴포넌트 ─────────────────────────

    /// <summary>마우스 오버 시 살짝 커지는 버튼 연출</summary>
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.05f;
        float target = 1f;
        Vector3 baseScale = Vector3.one;
        bool init;

        void Start() { baseScale = transform.localScale; init = true; }

        public void OnPointerEnter(PointerEventData e) { target = hoverScale; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }
        public void OnPointerDown(PointerEventData e) { target = 0.95f; }
        public void OnPointerUp(PointerEventData e) { target = hoverScale; }

        void Update()
        {
            if (!init) return;
            var s = Mathf.Lerp(transform.localScale.x / Mathf.Max(0.0001f, baseScale.x), target, Time.unscaledDeltaTime * 18f);
            transform.localScale = baseScale * s;
        }
    }

    /// <summary>위로 떠오르며 사라지는 텍스트/이미지</summary>
    public class FloatUp : MonoBehaviour
    {
        public float speed = 22f;
        public float life = 1.1f;
        float t;
        Graphic[] graphics;
        Color[] baseColors;

        void Start()
        {
            graphics = GetComponentsInChildren<Graphic>();
            baseColors = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) baseColors[i] = graphics[i].color;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            var rt = (RectTransform)transform;
            rt.anchoredPosition += new Vector2(0, speed * Time.unscaledDeltaTime);
            float a = t < life * 0.6f ? 1f : Mathf.Clamp01(1f - (t - life * 0.6f) / (life * 0.4f));
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                var c = baseColors[i]; c.a *= a; graphics[i].color = c;
            }
            float pop = t < 0.12f ? Mathf.Lerp(0.6f, 1.15f, t / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.12f) / 0.1f));
            transform.localScale = Vector3.one * pop;
            if (t >= life) Destroy(gameObject);
        }

        public static Text Spawn(Transform parent, string text, Vector2 topLeftPos, Color color, int size = 12, float life = 1.1f)
        {
            var t = UIKit.Label(parent, text, size, color, TextAnchor.MiddleCenter, true, true);
            t.rectTransform.Mid(topLeftPos.x, topLeftPos.y, 160, 24);
            var f = t.gameObject.AddComponent<FloatUp>();
            f.life = life;
            return t;
        }
    }

    /// <summary>깜빡임</summary>
    public class Blink : MonoBehaviour
    {
        public float speed = 4f;
        public float min = 0.25f;
        Graphic g;
        Color c;
        void Start() { g = GetComponent<Graphic>(); if (g != null) c = g.color; }
        void Update()
        {
            if (g == null) return;
            var k = c; k.a = c.a * Mathf.Lerp(min, 1f, (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f);
            g.color = k;
        }
    }

    /// <summary>위아래로 둥실둥실 (캐릭터 호흡, 노래 박자 등)</summary>
    public class Bob : MonoBehaviour
    {
        public float amp = 1.5f;
        public float speed = 2f;
        public float phase;
        public float tilt;        // 취했을 때 흔들림(도)
        public float tiltSpeed = 1.3f;
        Vector2 basePos;
        bool init;

        public void Rebase() { basePos = ((RectTransform)transform).anchoredPosition; init = true; }
        void Start() { if (!init) Rebase(); }

        void Update()
        {
            var rt = (RectTransform)transform;
            float t = Time.time * speed + phase;
            rt.anchoredPosition = basePos + new Vector2(0, Mathf.Sin(t) * amp);
            rt.localRotation = Quaternion.Euler(0, 0, tilt == 0 ? 0 : Mathf.Sin(Time.time * tiltSpeed + phase) * tilt);
        }
    }

    /// <summary>스프라이트 프레임 애니메이션 (김, 연기, 반짝이)</summary>
    public class SpriteCycle : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 6f;
        public bool floatUp;
        Image img;
        float t;
        Vector2 basePos;

        void Start() { img = GetComponent<Image>(); basePos = ((RectTransform)transform).anchoredPosition; }

        void Update()
        {
            if (img == null || frames == null || frames.Length == 0) return;
            t += Time.deltaTime;
            img.sprite = frames[(int)(t * fps) % frames.Length];
            if (floatUp)
            {
                float k = (t * 0.8f) % 1f;
                ((RectTransform)transform).anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 3f) * 2f, k * 10f);
                var c = img.color; c.a = 1f - k; img.color = c;
            }
        }
    }

    /// <summary>화면 전체에 떨어지는 눈 (UI 파티클)</summary>
    public class SnowFall : MonoBehaviour
    {
        public int count = 40;
        public Color color = new Color(1, 1, 1, 0.85f);
        public float width = 640, height = 360;
        RectTransform[] flakes;
        Vector2[] vel;

        void Start()
        {
            flakes = new RectTransform[count];
            vel = new Vector2[count];
            var rnd = new System.Random(7);
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Img(transform, Art.Get("Frames/circle"), color, "flake");
                float s = 1.5f + (float)rnd.NextDouble() * 2.5f;
                img.rectTransform.Mid((float)rnd.NextDouble() * width, (float)rnd.NextDouble() * height, s, s);
                var c = color; c.a *= 0.5f + (float)rnd.NextDouble() * 0.5f; img.color = c;
                flakes[i] = img.rectTransform;
                vel[i] = new Vector2(-4f + (float)rnd.NextDouble() * 8f, -(8f + (float)rnd.NextDouble() * 16f));
            }
        }

        void Update()
        {
            if (flakes == null) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < flakes.Length; i++)
            {
                var p = flakes[i].anchoredPosition;
                p += new Vector2(vel[i].x + Mathf.Sin(Time.unscaledTime + i) * 3f, vel[i].y) * dt;
                if (p.y < -height - 5) { p.y = 5; p.x = UnityEngine.Random.Range(0f, width); }
                if (p.x < -5) p.x = width + 5;
                if (p.x > width + 5) p.x = -5;
                flakes[i].anchoredPosition = p;
            }
        }
    }

    /// <summary>코루틴 트윈 모음 — 호스트 오브젝트가 사라지면 같이 멈춘다.</summary>
    public static class Tw
    {
        class Runner : MonoBehaviour { }

        public static Coroutine Run(Component host, IEnumerator e)
        {
            if (host == null) return null;
            var r = host.GetComponent<Runner>();
            if (r == null) r = host.gameObject.AddComponent<Runner>();
            return r.StartCoroutine(e);
        }

        public static IEnumerator Fade(CanvasGroup cg, float from, float to, float dur)
        {
            float t = 0;
            while (t < dur)
            {
                if (cg == null) yield break;
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            if (cg != null) cg.alpha = to;
        }

        public static IEnumerator FadeGraphic(Graphic g, float to, float dur)
        {
            if (g == null) yield break;
            float from = g.color.a, t = 0;
            while (t < dur)
            {
                if (g == null) yield break;
                t += Time.unscaledDeltaTime;
                var c = g.color; c.a = Mathf.Lerp(from, to, t / dur); g.color = c;
                yield return null;
            }
            if (g != null) { var c = g.color; c.a = to; g.color = c; }
        }

        public static IEnumerator Pop(Transform tr, float dur = 0.25f, float over = 1.2f)
        {
            float t = 0;
            while (t < dur)
            {
                if (tr == null) yield break;
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                float s = k < 0.5f ? Mathf.Lerp(0.3f, over, k * 2f) : Mathf.Lerp(over, 1f, (k - 0.5f) * 2f);
                tr.localScale = Vector3.one * s;
                yield return null;
            }
            if (tr != null) tr.localScale = Vector3.one;
        }

        public static IEnumerator Shake(RectTransform rt, float dur = 0.3f, float mag = 3f)
        {
            if (rt == null) yield break;
            var basePos = rt.anchoredPosition;
            float t = 0;
            while (t < dur)
            {
                if (rt == null) yield break;
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition = basePos + new Vector2(UnityEngine.Random.Range(-mag, mag), UnityEngine.Random.Range(-mag, mag)) * (1f - t / dur);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = basePos;
        }

        public static IEnumerator Move(RectTransform rt, Vector2 to, float dur)
        {
            if (rt == null) yield break;
            var from = rt.anchoredPosition;
            float t = 0;
            while (t < dur)
            {
                if (rt == null) yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0, 1, t / dur);
                rt.anchoredPosition = Vector2.Lerp(from, to, k);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
        }

        public static IEnumerator Wait(float s)
        {
            float t = 0;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        public static IEnumerator Then(IEnumerator first, Action after)
        {
            yield return first;
            if (after != null) after();
        }
    }
}
