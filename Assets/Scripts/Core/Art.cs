using System.Collections.Generic;
using UnityEngine;

namespace Taiyaki
{
    /// <summary>
    /// Resources/Art 아래 이미지를 스프라이트로 불러오는 캐시.
    /// 텍스처 임포트 타입과 상관없이 동작하도록 Texture2D에서 직접 Sprite를 만든다.
    /// 도트(픽셀) 리소스는 Point 필터, 고해상도 도트풍 리소스는 Bilinear 필터를 쓴다.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Sprite white;
        public static Font Body;
        public static Font Pixel;

        static readonly string[] PointFilterPrefixes =
        {
            "Backgrounds/room", "Backgrounds/street_night", "Backgrounds/club", "Backgrounds/karaoke",
            "Backgrounds/title", "Backgrounds/stall_day", "Characters/rina", "Customers/", "Frames/"
        };

        public static void Init()
        {
            Body = Resources.Load<Font>("Fonts/Pretendard-Regular");
            Pixel = Resources.Load<Font>("Fonts/Galmuri11-Bold");
            Font builtin = null;
            try { builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (builtin == null) { try { builtin = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (Body == null) Body = Pixel != null ? Pixel : builtin;
            if (Pixel == null) Pixel = Body;
        }

        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                    t.SetPixels32(px);
                    t.Apply();
                    t.filterMode = FilterMode.Point;
                    white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
                }
                return white;
            }
        }

        /// <summary>path 예: "Sprites/taiyaki_perfect"</summary>
        public static Sprite Get(string path)
        {
            return Get(path, Vector4.zero);
        }

        public static Sprite Get(string path, Vector4 border)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key = path + "|" + border;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;

            var tex = Resources.Load<Texture2D>("Art/" + path);
            if (tex == null)
            {
                Debug.LogWarning("[Art] 이미지 없음: " + path);
                cache[key] = null;
                return null;
            }
            bool point = false;
            foreach (var p in PointFilterPrefixes) if (path.StartsWith(p)) { point = true; break; }
            tex.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = path;
            cache[key] = s;
            return s;
        }

        /// <summary>없으면 null (경고 로그 없이) — 선택적 리소스(예: 표정 스프라이트)용</summary>
        public static Sprite TryGet(string path)
        {
            if (Resources.Load<Texture2D>("Art/" + path) == null) return null;
            return Get(path);
        }

        /// <summary>9-slice 프레임 (Frames 폴더, 32x32 / 테두리 10px)</summary>
        public static Sprite Frame(string name)
        {
            return Get("Frames/" + name, new Vector4(10, 10, 10, 10));
        }
    }

    public static class Pal
    {
        // 평일 낮 — 크림/갈색/주황/겨울 하늘색
        public static readonly Color Cream = Hex("#F6EAD0");
        public static readonly Color Brown = Hex("#5C351C");
        public static readonly Color DarkBrown = Hex("#2B1D17");
        public static readonly Color Orange = Hex("#E8893A");
        public static readonly Color Gold = Hex("#F5C45A");
        public static readonly Color Sky = Hex("#9CC8E8");
        public static readonly Color Ink = Hex("#3A2418");
        // 평일 밤 — 남색/주황 가로등
        public static readonly Color Navy = Hex("#1B2346");
        // 주말 밤 — 핑크/보라/청록/네온
        public static readonly Color Pink = Hex("#FF5FD2");
        public static readonly Color Purple = Hex("#7A2A8C");
        public static readonly Color Teal = Hex("#4FF0FF");
        public static readonly Color NeonInk = Hex("#1B1030");
        // 판정
        public static readonly Color Perfect = Hex("#FFD84A");
        public static readonly Color Good = Hex("#8BE06A");
        public static readonly Color Bad = Hex("#FF6A5A");

        public static Color Hex(string h)
        {
            Color c;
            ColorUtility.TryParseHtmlString(h, out c);
            return c;
        }

        public static Color A(Color c, float a) { c.a = a; return c; }
    }
}
