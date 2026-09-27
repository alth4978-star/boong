using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 붕어빵 장사 메인 화면 — 게임에서 가장 오래 보는 화면.
    /// 반죽 붓기 → 속 선택 → 뒤집기 → 꺼내기. 너무 빨리 꺼내면 설익고, 늦으면 탄다.
    /// </summary>
    public class ShopScreen : ScreenBase
    {
        // ─────────── 굽기 판정 구간 (뒤집은 뒤 진행도 기준) ───────────
        const float RawEnd = 0.55f, PerfectStart = 0.85f, PerfectEnd = 1.08f, BurnStart = 1.35f, BarMaxB = 1.6f;
        const float FlipReady = 1f, FlipBurn = 2.2f, BarMaxA = 2.4f;

        enum MoldState { Empty, Batter, SideA, SideB, Burnt }

        class Mold
        {
            public int index;
            public MoldState state;
            public string filling;
            public float a, b;
            public RectTransform rt;
            public Image baseImg, fishImg;
            public RectTransform barRoot;
            public Image needle;
            public Text hint;
            public Image fx;
            public SpriteCycle fxCycle;
            public bool warnedFlip, warnedTake;
        }

        class Item
        {
            public string filling;
            public int quality; // 0 설익음 1 좋음 2 PERFECT
        }

        class Customer
        {
            public CustomerDef def;
            public bool heroine;
            public Dictionary<string, int> order = new Dictionary<string, int>();
            public float patience, maxPatience;
            public RectTransform rt;
            public RectTransform bubble;
            public Image patienceFill;
            public int slot;
            public bool leaving;
        }

        readonly List<Mold> molds = new List<Mold>();
        readonly List<Item> rack = new List<Item>();
        readonly Customer[] slots = new Customer[4];
        readonly List<Image> rackSlots = new List<Image>();
        readonly List<Image> rackDots = new List<Image>();
        readonly Dictionary<string, RectTransform> cards = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Text> cardCounts = new Dictionary<string, Text>();
        readonly List<Image> lanterns = new List<Image>();
        readonly List<Image> lanternGlows = new List<Image>();

        Hud hud;
        RectTransform customerLayer, fxLayer;
        Image darkness, snowPile;
        Text orderText, comboText, rackTitle;
        Image comboFrame;
        string selected = "red";
        float time, dayLen, spawnTimer = 1.5f;
        int combo;
        bool closing, finished, paused, tired;
        bool heroineToday, heroineSpawned;
        DayResult result = new DayResult();
        EconomyDef E { get { return GameData.Eco; } }

        static readonly float[] SlotX = { 92, 206, 320, 434 };

        public override void Build()
        {
            Sfx.Bgm("weekday");
            Time.timeScale = 1f;
            dayLen = E.shopDaySeconds;
            tired = S.stamina <= 1;
            if (!S.IsUnlocked(selected) && S.unlockedFillings.Count > 0) selected = S.unlockedFillings[0];
            DecideHeroineVisit();

            // 배경 (거리) → 손님 → 카운터(배경 아랫부분을 한 번 더 그려서 손님 발을 가린다)
            var bgTex = Resources.Load<Texture2D>("Art/Backgrounds/shop");
            var bg = UIKit.Sprite(Root, "Backgrounds/shop");
            bg.preserveAspect = false;
            bg.rectTransform.Fill();
            darkness = UIKit.Rect(Root, new Color(0.05f, 0.06f, 0.22f, 0f), "Evening");
            darkness.rectTransform.At(0, 0, 640, 196);
            UIKit.New("Snow", Root).Fill().gameObject.AddComponent<SnowFall>().count = 45;
            BuildLanterns();
            snowPile = UIKit.Frame(Root, "round", new Color(1, 1, 1, 0.95f), 6f);
            snowPile.rectTransform.At(-10, -6, 660, 6);

            customerLayer = UIKit.New("Customers", Root).Fill();

            if (bgTex != null)
            {
                var counter = UIKit.New("Counter", Root).At(0, 192, 640, 168);
                var raw = counter.gameObject.AddComponent<RawImage>();
                raw.texture = bgTex;
                raw.uvRect = new Rect(0, 0, 1, 168f / 360f);
                raw.raycastTarget = false;
            }

            BuildMolds();
            BuildRack();
            BuildBottomBar();
            fxLayer = UIKit.New("FX", Root).Fill();

            hud = Hud.Create(Root, HudStyle.Day, 500);
            var pause = UIKit.Img(Root, Art.Get("UI/btn_pause"), Color.white, "Pause");
            pause.rectTransform.At(608, 4, 26, 26);
            UIKit.Clickable(pause.rectTransform, Pause);

            if (tired) Game.Toast("피곤해서 손님 응대가 느리다… (손님 인내심 감소)", Pal.Bad, 3f);
            else Game.Toast("영업 시작! 틀을 눌러 반죽을 부어요.", Pal.Gold, 2.5f);
            Sfx.Play("door");
            RefreshAll();
        }

        public override void OnLeave()
        {
            Time.timeScale = 1f;
        }

        void DecideHeroineVisit()
        {
            if (S.Has("promise_visit"))
            {
                heroineToday = true;
                S.Unset("promise_visit");
                return;
            }
            if (S.heroineVisitDay < 0)
            {
                var days = new List<int>();
                for (int d = Mathf.Max(1, S.day); d < 5; d++) if (S.PlanOf(d) == DayPlan.Work) days.Add(d);
                if (days.Count == 0) days.Add(S.day);
                S.heroineVisitDay = days[Random.Range(0, days.Count)];
            }
            heroineToday = S.heroineVisitDay == S.day && !S.Has("rina_shop_w" + S.week);
            // 후반부(친해질수록)에는 노점에 더 자주 찾아온다
            if (!heroineToday && S.affection >= 45 && Random.value < 0.5f) heroineToday = true;
        }

        // ───────────────────────── 레이아웃 ─────────────────────────

        void BuildLanterns()
        {
            float[] xs = { 20, 196, 400, 610 };
            for (int i = 0; i < xs.Length; i++)
            {
                var glow = UIKit.Img(Root, Art.Get("Frames/circle"), new Color(1f, 0.7f, 0.35f, 0f), "LanternGlow");
                glow.rectTransform.Mid(xs[i], 44, 70, 70);
                var l = UIKit.Sprite(Root, "Sprites/lantern", new Color(0.45f, 0.4f, 0.4f));
                l.rectTransform.At(xs[i] - 10, 8, 20, 48);
                l.gameObject.AddComponent<Bob>().amp = 0.5f;
                lanterns.Add(l);
                lanternGlows.Add(glow);
            }
        }

        void BuildMolds()
        {
            int n = Mathf.Clamp(S.molds, 1, 6);
            float w = 84, gap = 10;
            float total = n * w + (n - 1) * gap;
            float x0 = 215 - total / 2;
            if (x0 < 8) x0 = 8;
            for (int i = 0; i < n; i++)
            {
                var m = new Mold { index = i };
                m.rt = UIKit.New("Mold" + i, Root).At(x0 + i * (w + gap), 206, w, 96);
                var hl = UIKit.Frame(m.rt, "round", new Color(1f, 0.85f, 0.4f, 0f), 4f);
                hl.rectTransform.At(-2, 0, w + 4, 84);
                m.baseImg = UIKit.Sprite(m.rt, "Sprites/mold_empty");
                m.baseImg.rectTransform.At(0, 4, w, 76);
                m.fishImg = UIKit.Sprite(m.rt, "Sprites/taiyaki_raw");
                m.fishImg.rectTransform.At(12, 18, w - 24, 42);
                m.fishImg.enabled = false;
                m.fx = UIKit.Img(m.rt, Art.Get("Sprites/steam1"), Color.white, "Fx");
                m.fx.rectTransform.At(w / 2 - 14, -14, 28, 28);
                m.fx.preserveAspect = true;
                m.fx.enabled = false;
                m.fxCycle = m.fx.gameObject.AddComponent<SpriteCycle>();
                m.fxCycle.floatUp = true;

                m.barRoot = UIKit.New("Bar", m.rt).At(4, 82, w - 8, 7);
                m.needle = UIKit.Rect(m.barRoot, Color.white, "Needle");
                m.needle.rectTransform.At(0, -2, 2, 11);
                m.hint = UIKit.Label(m.rt, "", 8, Pal.Cream, TextAnchor.MiddleCenter, true, true);
                m.hint.rectTransform.At(-10, -6, w + 20, 14);

                var hit = UIKit.New("Hit", m.rt).At(0, 0, w, 90);
                var mold = m;
                UIKit.Clickable(hit, () => ClickMold(mold), false, false);
                hit.gameObject.AddComponent<HotspotGlow>().glow = hl;
                molds.Add(m);
                SetMoldVisual(m);
            }
        }

        void BuildRack()
        {
            var frame = UIKit.Frame(Root, "wood");
            frame.rectTransform.At(452, 206, 182, 94);
            rackTitle = UIKit.Label(frame.transform, "", 9, Pal.Gold, TextAnchor.MiddleLeft, true, true);
            rackTitle.rectTransform.At(8, 3, 170, 14);
            int cap = E.rackCapacity;
            for (int i = 0; i < cap; i++)
            {
                int col = i % 4, row = i / 4;
                var slot = UIKit.Frame(frame.transform, "dark", null, 3f);
                slot.rectTransform.At(8 + col * 42, 19 + row * 36, 40, 33);
                var fish = UIKit.Img(slot.transform, null, Color.white, "Fish");
                fish.preserveAspect = true;
                fish.rectTransform.Fill(3, 4, 3, 4);
                var dot = UIKit.Img(slot.transform, Art.Get("Frames/circle"), Color.white, "Dot");
                dot.rectTransform.At(29, 22, 8, 8);
                int idx = i;
                UIKit.Clickable(slot.rectTransform, () => Discard(idx), true, false);
                rackSlots.Add(fish);
                rackDots.Add(dot);
            }
        }

        void BuildBottomBar()
        {
            var bar = UIKit.Rect(Root, new Color(0.1f, 0.06f, 0.04f, 0.55f), "BottomBar");
            bar.rectTransform.At(0, 304, 640, 56);

            float x = 8;
            AddCard("batter", "반죽", "UI/card_batter", null, x, false); x += 50;
            foreach (var f in GameData.Root.fillings)
            {
                bool unlocked = S.IsUnlocked(f.id);
                string cardSprite = "UI/card_" + (f.sprite == "red" ? "red" : f.sprite);
                AddCard(f.id, f.name, cardSprite, f.tint, x, !unlocked);
                x += 50;
            }

            var info = UIKit.Frame(Root, "dark");
            info.rectTransform.At(x + 4, 306, 634 - x - 4, 50);
            orderText = UIKit.Label(info.transform, "", 10, Pal.Cream, TextAnchor.MiddleLeft, false, true);
            orderText.rectTransform.At(8, 4, 200, 42);
            comboFrame = UIKit.Frame(info.transform, "wine");
            comboFrame.rectTransform.At(634 - x - 4 - 92, 6, 86, 38);
            comboText = UIKit.Label(comboFrame.transform, "", 12, Pal.Orange, TextAnchor.MiddleCenter, true, true);
            comboText.rectTransform.Fill();
        }

        void AddCard(string id, string label, string sprite, string tint, float x, bool locked)
        {
            var root = UIKit.New("Card_" + id, Root).At(x, 305, 46, 54);
            var sel = UIKit.Frame(root, "neon_cyan", null, 3f);
            sel.name = "Selected";
            sel.rectTransform.Fill(-3, -3, -3, -3);
            sel.gameObject.SetActive(false);
            var img = UIKit.Sprite(root, sprite);
            img.rectTransform.Fill();
            img.preserveAspect = false;
            if (!string.IsNullOrEmpty(tint)) img.color = GameData.Hex(tint, Color.white);
            var name = UIKit.Label(root, label, 7, Pal.Cream, TextAnchor.MiddleCenter, true, true);
            name.rectTransform.At(2, 32, 42, 10);
            var cnt = UIKit.Label(root, "", 7, Pal.Gold, TextAnchor.MiddleCenter, true, true);
            cnt.rectTransform.At(26, 42, 20, 10);
            cardCounts[id] = cnt;
            cards[id] = root;
            if (locked)
            {
                img.color = new Color(0.3f, 0.3f, 0.3f);
                name.text = "LOCK";
                name.color = new Color(0.7f, 0.7f, 0.7f);
                cnt.text = "";
                UIKit.Clickable(root, () => Game.Toast("아직 모르는 레시피다. (책상에서 연구)"), true);
                cardCounts.Remove(id);
                return;
            }
            if (id != "batter") UIKit.Clickable(root, () => Select(id), true);
            else UIKit.Clickable(root, () => Game.Toast("반죽 재고 " + S.batter + "개. 빈 틀을 누르면 부어요."), true);
        }

        // ───────────────────────── 갱신 ─────────────────────────

        void Update()
        {
            if (finished) return;
            float dt = Time.deltaTime;
            if (!closing)
            {
                time += dt;
                if (time >= dayLen) StartCoroutine(Close());
            }
            UpdateClock();
            UpdateMolds(dt);
            UpdateCustomers(dt);
            if (!closing) UpdateSpawn(dt);
            HandleKeys();
        }

        void UpdateClock()
        {
            float k = Mathf.Clamp01(time / dayLen);
            float hours = Mathf.Lerp(E.shopOpenHour, E.shopCloseHour, k);
            int h = (int)hours, m = (int)((hours - h) * 60) / 10 * 10;
            hud.timeOverride = string.Format("{0:00}:{1:00}", h, m);
            // 저녁이 되면 거리가 어두워지고 조명이 하나씩 켜진다
            float dusk = Mathf.Clamp01((k - 0.5f) / 0.5f);
            darkness.color = new Color(0.05f, 0.06f, 0.22f, dusk * 0.45f);
            for (int i = 0; i < lanterns.Count; i++)
            {
                bool on = k > 0.5f + i * 0.07f;
                lanterns[i].color = Color.Lerp(lanterns[i].color, on ? Color.white : new Color(0.45f, 0.4f, 0.4f), Time.deltaTime * 4f);
                var g = lanternGlows[i].color;
                g.a = Mathf.Lerp(g.a, on ? 0.35f + Mathf.Sin(Time.time * 3 + i) * 0.05f : 0f, Time.deltaTime * 4f);
                lanternGlows[i].color = g;
            }
            // 눈이 오는 날엔 지붕에 눈이 쌓인다
            snowPile.rectTransform.sizeDelta = new Vector2(660, 4 + k * 8);
        }

        void UpdateSpawn(float dt)
        {
            spawnTimer -= dt;
            float k = time / dayLen;
            if (heroineToday && !heroineSpawned && k > 0.3f)
            {
                int free = FreeSlot();
                if (free >= 0) { SpawnCustomer(free, true); heroineSpawned = true; }
            }
            if (spawnTimer > 0) return;
            bool rush = k > 0.4f && k < 0.8f;
            spawnTimer = rush ? Random.Range(2.4f, 4.2f) : Random.Range(3.5f, 6.2f);
            int slot = FreeSlot();
            if (slot >= 0 && k < 0.95f) SpawnCustomer(slot, false);
        }

        int FreeSlot()
        {
            int max = Mathf.Min(E.maxQueue, slots.Length);
            var free = new List<int>();
            for (int i = 0; i < max; i++) if (slots[i] == null) free.Add(i);
            return free.Count == 0 ? -1 : free[Random.Range(0, free.Count)];
        }

        void UpdateMolds(float dt)
        {
            foreach (var m in molds)
            {
                if (m.state == MoldState.SideA)
                {
                    m.a += dt / E.cookSideA;
                    if (m.a >= FlipBurn) Burn(m);
                    else if (m.a >= FlipReady && !m.warnedFlip) { m.warnedFlip = true; Sfx.Play("sizzle", 0.4f); }
                }
                else if (m.state == MoldState.SideB)
                {
                    m.b += dt / E.cookSideB;
                    if (m.b >= BarMaxB) Burn(m);
                    else if (m.b >= PerfectStart && !m.warnedTake) { m.warnedTake = true; Sfx.Play("good", 0.35f); }
                }
                UpdateMoldVisual(m);
            }
        }

        void UpdateCustomers(float dt)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var c = slots[i];
                if (c == null || c.leaving) continue;
                c.patience -= dt;
                float r = Mathf.Clamp01(c.patience / c.maxPatience);
                c.patienceFill.rectTransform.sizeDelta = new Vector2(40 * r, 4);
                c.patienceFill.color = r > 0.5f ? Pal.Good : r > 0.25f ? Pal.Perfect : Pal.Bad;
                if (r < 0.25f && c.bubble != null) c.bubble.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 20f) * 3f);
                if (c.patience <= 0 && !closing) LeaveAngry(c);
            }
            RefreshOrderText();
        }

        void HandleKeys()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (paused) return;
            for (int i = 0; i < molds.Count && i < 6; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) ClickMold(molds[i]);
            var fs = S.unlockedFillings;
            KeyCode[] keys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
            for (int i = 0; i < fs.Count && i < keys.Length; i++)
                if (Input.GetKeyDown(keys[i])) Select(fs[i]);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Customer best = null;
                foreach (var c in slots) if (c != null && !c.leaving && (best == null || c.patience < best.patience)) best = c;
                if (best != null) Serve(best);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Pause();
#endif
        }

        // ───────────────────────── 굽기 ─────────────────────────

        void Select(string id)
        {
            selected = id;
            Sfx.Play("click");
            RefreshCards();
        }

        void ClickMold(Mold m)
        {
            if (closing || paused) return;
            switch (m.state)
            {
                case MoldState.Empty:
                    if (S.batter <= 0) { Warn(m, "반죽이 없어요!"); Game.Toast("반죽이 떨어졌다! (집 냉장고에서 주문)", Pal.Bad); return; }
                    S.batter--;
                    m.state = MoldState.Batter;
                    Sfx.Play("pour");
                    break;
                case MoldState.Batter:
                    if (S.Stock(selected) <= 0)
                    {
                        var f = GameData.Filling(selected);
                        Warn(m, (f != null ? f.name : selected) + " 없음!");
                        return;
                    }
                    S.AddStock(selected, -1);
                    m.filling = selected;
                    m.state = MoldState.SideA;
                    m.a = 0;
                    m.warnedFlip = false;
                    Sfx.Play("fill");
                    break;
                case MoldState.SideA:
                    if (m.a < FlipReady) { Warn(m, "아직 덜 익었어요"); return; }
                    m.state = MoldState.SideB;
                    m.b = 0;
                    m.warnedTake = false;
                    Sfx.Play("flip");
                    Tw.Run(this, Tw.Pop(m.baseImg.transform, 0.2f, 1.08f));
                    break;
                case MoldState.SideB:
                    TakeOut(m);
                    break;
                case MoldState.Burnt:
                    m.state = MoldState.Empty;
                    result.burnt++;
                    result.wasted++;
                    FloatUp.Spawn(fxLayer, "버렸다…", MoldCenter(m), new Color(0.7f, 0.7f, 0.7f), 10);
                    break;
            }
            SetMoldVisual(m);
            RefreshCards();
        }

        void TakeOut(Mold m)
        {
            if (rack.Count >= E.rackCapacity) { Warn(m, "보온대 꽉 참!"); Game.Toast("보온대가 꽉 찼다! 손님에게 먼저 팔자.", Pal.Bad); return; }
            int q = m.b < RawEnd ? 0 : (m.b >= PerfectStart && m.b < PerfectEnd) ? 2 : 1;
            rack.Add(new Item { filling = m.filling, quality = q });
            var pos = MoldCenter(m);
            if (q == 2)
            {
                Sfx.Play("perfect");
                FloatUp.Spawn(fxLayer, "PERFECT!", pos, Pal.Perfect, 14);
                Sparkle(pos);
            }
            else if (q == 1)
            {
                Sfx.Play("good");
                FloatUp.Spawn(fxLayer, m.b >= BurnStart - 0.1f ? "좀 진하게 GOOD" : "GOOD", pos, Pal.Good, 11);
            }
            else
            {
                Sfx.Play("fail", 0.6f);
                FloatUp.Spawn(fxLayer, "설익었다…", pos, new Color(0.9f, 0.85f, 0.7f), 11);
            }
            m.state = MoldState.Empty;
            SetMoldVisual(m);
            RefreshRack();
        }

        void Burn(Mold m)
        {
            m.state = MoldState.Burnt;
            Sfx.Play("burn");
            FloatUp.Spawn(fxLayer, "탔다!", MoldCenter(m), Pal.Bad, 12);
            SetMoldVisual(m);
        }

        void Warn(Mold m, string msg)
        {
            Sfx.Play("fail", 0.4f);
            FloatUp.Spawn(fxLayer, msg, MoldCenter(m), Pal.Bad, 9, 0.8f);
            Tw.Run(this, Tw.Shake(m.baseImg.rectTransform, 0.2f, 2f));
        }

        Vector2 MoldCenter(Mold m)
        {
            var p = m.rt.anchoredPosition;
            return new Vector2(p.x + 42, -p.y + 30);
        }

        void Sparkle(Vector2 pos)
        {
            var img = UIKit.Img(fxLayer, Art.Get("Sprites/sparkle2"), Color.white, "Sparkle");
            img.preserveAspect = true;
            img.rectTransform.Mid(pos.x, pos.y - 10, 34, 34);
            img.gameObject.AddComponent<FloatUp>().life = 0.7f;
        }

        void SetMoldVisual(Mold m)
        {
            string fs = FillingSprite(m.filling);
            var tint = FillingTint(m.filling);
            m.baseImg.color = Color.white;
            m.fishImg.enabled = false;
            m.fx.enabled = false;
            switch (m.state)
            {
                case MoldState.Empty:
                    m.baseImg.sprite = Art.Get("Sprites/mold_empty");
                    break;
                case MoldState.Batter:
                    m.baseImg.sprite = Art.Get("Sprites/mold_batter");
                    break;
                case MoldState.SideA:
                    m.baseImg.sprite = Art.Get("Sprites/mold_" + fs);
                    m.baseImg.color = tint;
                    break;
                case MoldState.SideB:
                case MoldState.Burnt:
                    m.baseImg.sprite = Art.Get("Sprites/mold_empty");
                    m.fishImg.enabled = true;
                    break;
            }
            m.barRoot.gameObject.SetActive(m.state == MoldState.SideA || m.state == MoldState.SideB);
            BuildBarSegments(m);
            UpdateMoldVisual(m);
        }

        void BuildBarSegments(Mold m)
        {
            for (int i = m.barRoot.childCount - 1; i >= 0; i--)
            {
                var ch = m.barRoot.GetChild(i);
                if (ch != m.needle.transform) Destroy(ch.gameObject);
            }
            if (!m.barRoot.gameObject.activeSelf) return;
            float w = m.barRoot.sizeDelta.x;
            System.Action<float, float, Color> seg = (from, to, c) =>
            {
                var s = UIKit.Rect(m.barRoot, c, "Seg");
                s.rectTransform.At(from * w, 0, (to - from) * w, 7);
                s.transform.SetAsFirstSibling();
            };
            if (m.state == MoldState.SideA)
            {
                seg(0, FlipReady / BarMaxA, new Color(0.3f, 0.45f, 0.7f));
                seg(FlipReady / BarMaxA, FlipBurn / BarMaxA, new Color(0.35f, 0.8f, 0.4f));
                seg(FlipBurn / BarMaxA, 1, new Color(0.8f, 0.2f, 0.2f));
            }
            else
            {
                seg(0, RawEnd / BarMaxB, new Color(0.55f, 0.52f, 0.45f));
                seg(RawEnd / BarMaxB, PerfectStart / BarMaxB, new Color(0.6f, 0.8f, 0.35f));
                seg(PerfectStart / BarMaxB, PerfectEnd / BarMaxB, new Color(1f, 0.82f, 0.25f));
                seg(PerfectEnd / BarMaxB, BurnStart / BarMaxB, new Color(0.85f, 0.55f, 0.25f));
                seg(BurnStart / BarMaxB, 1, new Color(0.8f, 0.2f, 0.2f));
            }
            m.needle.transform.SetAsLastSibling();
        }

        void UpdateMoldVisual(Mold m)
        {
            float w = m.barRoot.sizeDelta.x;
            string hint = "";
            Color hc = Pal.Cream;
            switch (m.state)
            {
                case MoldState.Empty:
                    hint = "▼ 반죽 붓기";
                    hc = new Color(1, 1, 1, 0.75f);
                    break;
                case MoldState.Batter:
                    var f = GameData.Filling(selected);
                    hint = "▼ " + (f != null ? f.name : "") + " 넣기";
                    hc = Pal.Gold;
                    break;
                case MoldState.SideA:
                    m.needle.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp01(m.a / BarMaxA) * w - 1, 2);
                    if (m.a < FlipReady) hint = "굽는 중…";
                    else { hint = "뒤집기!"; hc = m.a > FlipBurn - 0.4f ? Pal.Bad : Pal.Good; }
                    break;
                case MoldState.SideB:
                    m.needle.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp01(m.b / BarMaxB) * w - 1, 2);
                    m.fishImg.sprite = Art.Get(FishSprite(m.b));
                    m.fishImg.color = FillingTint(m.filling) == Color.white ? Color.white : Color.Lerp(Color.white, FillingTint(m.filling), 0.25f);
                    if (m.b < RawEnd) hint = "굽는 중…";
                    else if (m.b < PerfectStart) { hint = "거의 다 됐다"; hc = Pal.Good; }
                    else if (m.b < PerfectEnd) { hint = "지금! 꺼내기"; hc = Pal.Perfect; }
                    else { hint = "빨리 꺼내!"; hc = Pal.Bad; }
                    SetFx(m, m.b >= PerfectStart && m.b < PerfectEnd ? "sparkle" : m.b >= BurnStart ? "smoke" : m.b > 0.3f ? "steam" : null);
                    break;
                case MoldState.Burnt:
                    m.fishImg.sprite = Art.Get("Sprites/taiyaki_burnt");
                    hint = "탔다! 눌러서 버리기";
                    hc = Pal.Bad;
                    SetFx(m, "smoke");
                    break;
            }
            if (m.hint.text != hint) m.hint.text = hint;
            m.hint.color = hc;
        }

        void SetFx(Mold m, string kind)
        {
            if (kind == null) { m.fx.enabled = false; return; }
            m.fx.enabled = true;
            if (m.fxCycle.frames != null && m.fxCycle.frames.Length > 0 && m.fxCycle.frames[0].name.Contains(kind)) return;
            if (kind == "steam") m.fxCycle.frames = new[] { Art.Get("Sprites/steam1"), Art.Get("Sprites/steam2"), Art.Get("Sprites/steam3") };
            else if (kind == "smoke") m.fxCycle.frames = new[] { Art.Get("Sprites/smoke1"), Art.Get("Sprites/smoke2") };
            else m.fxCycle.frames = new[] { Art.Get("Sprites/sparkle1"), Art.Get("Sprites/sparkle2") };
            m.fx.sprite = m.fxCycle.frames[0];
        }

        static string FishSprite(float b)
        {
            if (b < 0.35f) return "Sprites/taiyaki_raw";
            if (b < RawEnd) return "Sprites/taiyaki_light";
            if (b < PerfectStart) return "Sprites/taiyaki_golden";
            if (b < PerfectEnd) return "Sprites/taiyaki_perfect";
            if (b < BurnStart) return "Sprites/taiyaki_brown";
            return "Sprites/taiyaki_burnt";
        }

        static string FillingSprite(string id)
        {
            var f = GameData.Filling(id);
            return f != null ? f.sprite : "red";
        }

        static Color FillingTint(string id)
        {
            var f = GameData.Filling(id);
            return f != null ? GameData.Hex(f.tint, Color.white) : Color.white;
        }

        void Discard(int idx)
        {
            if (idx >= rack.Count || closing) return;
            var it = rack[idx];
            rack.RemoveAt(idx);
            result.wasted++;
            Sfx.Play("fail", 0.4f);
            var slot = rackSlots[idx].rectTransform.parent as RectTransform;
            var p = slot.parent as RectTransform;
            FloatUp.Spawn(fxLayer, it.quality == 0 ? "설익은 건 버렸다" : "버렸다", new Vector2(p.anchoredPosition.x + slot.anchoredPosition.x + 20, -p.anchoredPosition.y - slot.anchoredPosition.y), new Color(0.8f, 0.8f, 0.8f), 9, 0.8f);
            RefreshRack();
        }

        // ───────────────────────── 손님 ─────────────────────────

        void SpawnCustomer(int slot, bool heroine)
        {
            var c = new Customer { slot = slot, heroine = heroine };
            string sprite;
            if (heroine)
            {
                c.def = new CustomerDef { id = "heroine", name = GameData.Rina.name, minItems = 1, maxItems = 1, patience = 55, tipRate = 1f };
                sprite = GameData.Rina.smallSprite;
                string fav = S.IsUnlocked("strawberry") ? "strawberry" : "red";
                c.order[fav] = 1;
            }
            else
            {
                c.def = PickCustomerDef();
                sprite = c.def.sprite;
                int n = Random.Range(c.def.minItems, c.def.maxItems + 1);
                for (int i = 0; i < n; i++)
                {
                    string f = PickFilling();
                    int v;
                    c.order.TryGetValue(f, out v);
                    c.order[f] = v + 1;
                }
            }
            c.maxPatience = c.patience = c.def.patience * (tired ? E.tiredPatienceRate : 1f) * Random.Range(0.9f, 1.1f);

            var spr = Art.Get(sprite);
            float scale = heroine ? 1.9f : 2.4f;
            float w = spr != null ? spr.rect.width * scale : 50, h = spr != null ? spr.rect.height * scale : 110;
            c.rt = UIKit.New("Customer_" + c.def.id, customerLayer).Foot(SlotX[slot], 206, w, h);
            var body = UIKit.Img(c.rt, spr, Color.white, "Body");
            body.rectTransform.Fill();
            body.preserveAspect = true;
            body.raycastTarget = true;
            var cust = c;
            UIKit.Clickable(body.rectTransform, () => Serve(cust), true, false);
            var bob = c.rt.gameObject.AddComponent<Bob>();
            bob.amp = 0.8f; bob.speed = 2.2f; bob.phase = slot;

            // 주문 말풍선
            int kinds = c.order.Count;
            float bw = 12 + kinds * 34;
            c.bubble = UIKit.New("Bubble", customerLayer).Mid(SlotX[slot], 206 - h - 22, bw, 30);
            var bubImg = UIKit.Frame(c.bubble, heroine ? "bubble_right" : "bubble_left", null, 3f);
            bubImg.rectTransform.Fill();
            bubImg.raycastTarget = true;
            UIKit.Clickable(c.bubble, () => Serve(cust), false, false);
            float x = 6;
            foreach (var kv in c.order)
            {
                var icon = UIKit.Sprite(c.bubble, "Sprites/bowl_" + FillingSprite(kv.Key));
                icon.color = FillingTint(kv.Key);
                icon.rectTransform.At(x, 3, 20, 18);
                var cnt = UIKit.Label(c.bubble, "×" + kv.Value, 9, Pal.Ink, TextAnchor.MiddleLeft, true);
                cnt.rectTransform.At(x + 19, 4, 16, 16);
                x += 34;
            }
            var pbg = UIKit.Rect(c.bubble, new Color(0, 0, 0, 0.35f), "PatienceBg");
            pbg.rectTransform.At(bw / 2 - 20, 23, 40, 4);
            c.patienceFill = UIKit.Rect(pbg.transform, Pal.Good, "Patience");
            c.patienceFill.rectTransform.At(0, 0, 40, 4);

            if (heroine)
            {
                var tag = UIKit.Frame(c.rt, "wine", null, 3f);
                tag.rectTransform.Mid(w / 2, h + 4, 44, 14);
                var tt = UIKit.Label(tag.transform, "♡ " + GameData.Rina.name, 8, Pal.Gold, TextAnchor.MiddleCenter, true, true);
                tt.rectTransform.Fill();
                var lines = S.Has("rina_shop_ever") ? GameData.Rina.shopVisitAgain : GameData.Rina.shopVisitFirst;
                StartCoroutine(SpeechSequence(c, lines));
                S.Set("rina_shop_ever");
                S.SetWeek("rina_shop_w" + S.week);
                result.heroineVisited = true;
                Sfx.Play("heart", 0.5f);
            }
            else
            {
                string line = Pick(c.def.orderLines);
                if (!string.IsNullOrEmpty(line) && Random.value < 0.5f) Speech(c, line, 1.8f);
            }

            slots[slot] = c;
            Tw.Run(c.rt, Tw.Pop(c.rt, 0.25f, 1.08f));
        }

        CustomerDef PickCustomerDef()
        {
            var list = GameData.Root.customers;
            if (list.Length == 0) return new CustomerDef { id = "student", name = "손님", sprite = "Customers/student", minItems = 1, maxItems = 2, patience = 25, tipRate = 1 };
            int total = 0;
            foreach (var c in list) total += Mathf.Max(0, c.weight);
            int r = Random.Range(0, Mathf.Max(1, total));
            foreach (var c in list)
            {
                r -= Mathf.Max(0, c.weight);
                if (r < 0) return c;
            }
            return list[0];
        }

        string PickFilling()
        {
            int total = 0;
            foreach (var f in GameData.Root.fillings) if (S.IsUnlocked(f.id)) total += f.weight;
            int r = Random.Range(0, Mathf.Max(1, total));
            foreach (var f in GameData.Root.fillings)
            {
                if (!S.IsUnlocked(f.id)) continue;
                r -= f.weight;
                if (r < 0) return f.id;
            }
            return "red";
        }

        static string Pick(string[] arr)
        {
            if (arr == null || arr.Length == 0) return null;
            return arr[Random.Range(0, arr.Length)];
        }

        void Serve(Customer c)
        {
            if (c == null || c.leaving || closing || paused) return;
            // 재고 확인
            var missing = new List<string>();
            foreach (var kv in c.order)
            {
                int have = 0;
                foreach (var it in rack) if (it.filling == kv.Key) have++;
                if (have < kv.Value)
                {
                    var f = GameData.Filling(kv.Key);
                    missing.Add((f != null ? f.name : kv.Key) + "×" + (kv.Value - have));
                }
            }
            if (missing.Count > 0)
            {
                Sfx.Play("fail", 0.5f);
                Tw.Run(this, Tw.Shake(c.bubble, 0.25f, 2.5f));
                FloatUp.Spawn(fxLayer, "보온대에 " + string.Join(", ", missing.ToArray()) + " 부족", new Vector2(SlotX[c.slot], -c.bubble.anchoredPosition.y - 24), Pal.Bad, 9, 1f);
                return;
            }
            // 판매: PERFECT 우선으로 꺼낸다
            int price = 0, perfect = 0, raw = 0, n = 0;
            foreach (var kv in c.order)
            {
                for (int k = 0; k < kv.Value; k++)
                {
                    int best = -1;
                    for (int i = 0; i < rack.Count; i++)
                        if (rack[i].filling == kv.Key && (best < 0 || rack[i].quality > rack[best].quality)) best = i;
                    var it = rack[best];
                    rack.RemoveAt(best);
                    var f = GameData.Filling(it.filling);
                    int p = f != null ? f.price : 300;
                    if (it.quality == 0) { p = Mathf.RoundToInt(p * E.rawPriceRate); raw++; }
                    if (it.quality == 2) { p += E.perfectBonus; perfect++; }
                    price += p;
                    n++;
                }
            }
            if (raw > 0) combo = 0; else combo++;
            int tip = combo >= 3 ? Mathf.RoundToInt(Mathf.Min(combo * 10, 150) * c.def.tipRate) : 0;
            S.Earn(price + tip);
            result.sold += n;
            result.perfect += perfect;
            result.raw += raw;
            result.revenue += price;
            result.tips += tip;
            result.bestCombo = Mathf.Max(result.bestCombo, combo);
            Sfx.Play("coin");
            var pos = new Vector2(SlotX[c.slot], -c.bubble.anchoredPosition.y);
            FloatUp.Spawn(fxLayer, "+" + UIKit.Yen(price + tip), pos, Pal.Gold, 13);
            if (perfect > 0) FloatUp.Spawn(fxLayer, "PERFECT ×" + perfect, pos + new Vector2(0, 16), Pal.Perfect, 10);
            if (combo >= 2) PopCombo();

            if (c.heroine)
            {
                result.heroineServed = true;
                S.SetWeek("rina_visited_shop");
                bool perf = perfect > 0;
                if (perf) S.SetWeek("rina_ate_perfect");
                S.AddAffection(perf ? 5 : 3);
                StartCoroutine(HeroineLeave(c, perf ? GameData.Rina.shopServedPerfect : GameData.Rina.shopServed));
            }
            else
            {
                string line = raw > 0 ? "음… 좀 덜 익었네" : Pick(c.def.happyLines);
                if (!string.IsNullOrEmpty(line)) Speech(c, line, 1.4f);
                Leave(c, false);
            }
            RefreshRack();
        }

        void LeaveAngry(Customer c)
        {
            combo = 0;
            result.missed++;
            Sfx.Play("fail", 0.6f);
            if (c.heroine)
            {
                S.SetWeek("rina_missed");
                S.AddAffection(-1);
                StartCoroutine(HeroineLeave(c, GameData.Rina.shopLeft));
                return;
            }
            string line = Pick(c.def.angryLines);
            Speech(c, string.IsNullOrEmpty(line) ? "…됐어요" : line, 1.4f);
            FloatUp.Spawn(fxLayer, "놓쳤다…", new Vector2(SlotX[c.slot], 120), Pal.Bad, 10);
            Leave(c, true);
            RefreshOrderText();
        }

        void Leave(Customer c, bool angry)
        {
            c.leaving = true;
            if (c.bubble != null) Destroy(c.bubble.gameObject);
            StartCoroutine(LeaveCo(c, angry));
        }

        IEnumerator LeaveCo(Customer c, bool angry)
        {
            var cg = c.rt.gameObject.AddComponent<CanvasGroup>();
            var bob = c.rt.GetComponent<Bob>();
            if (bob != null) Destroy(bob);
            var start = c.rt.anchoredPosition;
            var end = start + new Vector2(angry ? -40 : 40, 0);
            float t = 0;
            while (t < 0.6f)
            {
                if (c.rt == null) yield break;
                t += Time.deltaTime;
                c.rt.anchoredPosition = Vector2.Lerp(start, end, t / 0.6f) + new Vector2(0, Mathf.Abs(Mathf.Sin(t * 18f)) * 2f);
                cg.alpha = 1f - t / 0.6f;
                yield return null;
            }
            if (slots[c.slot] == c) slots[c.slot] = null;
            if (c.rt != null) Destroy(c.rt.gameObject);
        }

        IEnumerator HeroineLeave(Customer c, Line[] lines)
        {
            c.leaving = true;
            if (c.bubble != null) Destroy(c.bubble.gameObject);
            yield return SpeechSequence(c, lines);
            if (c.rt == null) yield break;
            yield return LeaveCo(c, false);
        }

        IEnumerator SpeechSequence(Customer c, Line[] lines)
        {
            if (lines == null) yield break;
            foreach (var l in lines)
            {
                if (c.rt == null) yield break;
                float d = 1.2f + l.text.Length * 0.06f;
                Speech(c, l.text.Replace("{name}", GameData.Rina.name), d);
                yield return new WaitForSeconds(d);
            }
        }

        void Speech(Customer c, string text, float dur)
        {
            if (c.rt == null) return;
            var old = c.rt.Find("Speech");
            if (old != null) Destroy(old.gameObject);
            var box = UIKit.Frame(c.rt, c.heroine ? "bubble_right" : "bubble_left", null, 3f);
            box.name = "Speech";
            float w = Mathf.Clamp(text.Length * 8.5f + 14, 50, 150);
            var h = c.rt.sizeDelta.y;
            box.rectTransform.Mid(c.rt.sizeDelta.x / 2 + (c.slot >= 2 ? -w / 2 : w / 2), h * 0.25f, w, text.Length > 16 ? 30 : 20);
            var t = UIKit.Label(box.transform, text, 8, Pal.Ink, TextAnchor.MiddleCenter);
            t.rectTransform.Fill(5, 2, 5, 2);
            Destroy(box.gameObject, dur);
        }

        // ───────────────────────── UI 갱신 ─────────────────────────

        void PopCombo()
        {
            comboText.text = "COMBO\n<size=15>×" + combo + "</size>";
            Tw.Run(this, Tw.Pop(comboFrame.transform, 0.25f, 1.25f));
            if (combo >= 10) S.Set("memory_combo10");
        }

        void RefreshOrderText()
        {
            if (orderText == null) return;
            Customer urgent = null;
            foreach (var c in slots) if (c != null && !c.leaving && (urgent == null || c.patience < urgent.patience)) urgent = c;
            if (urgent == null) { orderText.text = closing ? "영업 종료" : "손님을 기다리는 중…\n<size=8>틀을 미리 채워 두자</size>"; return; }
            var parts = new List<string>();
            foreach (var kv in urgent.order)
            {
                var f = GameData.Filling(kv.Key);
                parts.Add((f != null ? f.name : kv.Key) + "×" + kv.Value);
            }
            orderText.text = "주문: " + string.Join(" / ", parts.ToArray()) + "\n<size=8>손님을 눌러 판매 (Space)</size>";
            if (combo < 2) comboText.text = "COMBO\n<size=15>×" + combo + "</size>";
        }

        void RefreshAll()
        {
            RefreshCards();
            RefreshRack();
            RefreshOrderText();
        }

        void RefreshCards()
        {
            foreach (var kv in cards)
            {
                var sel = kv.Value.Find("Selected");
                if (sel != null) sel.gameObject.SetActive(kv.Key == selected);
            }
            foreach (var kv in cardCounts)
            {
                int n = S.Stock(kv.Key);
                kv.Value.text = n.ToString();
                kv.Value.color = n == 0 ? Pal.Bad : n <= 3 ? Pal.Orange : Pal.Gold;
            }
        }

        void RefreshRack()
        {
            for (int i = 0; i < rackSlots.Count; i++)
            {
                bool has = i < rack.Count;
                rackSlots[i].enabled = has;
                rackDots[i].enabled = has;
                if (!has) continue;
                var it = rack[i];
                rackSlots[i].sprite = Art.Get(it.quality == 2 ? "Sprites/taiyaki_perfect" : it.quality == 1 ? "Sprites/taiyaki_golden" : "Sprites/taiyaki_raw");
                var f = GameData.Filling(it.filling);
                rackDots[i].color = f != null ? GameData.Hex(f.dotColor, Color.white) : Color.white;
            }
            rackTitle.text = "보온대 " + rack.Count + "/" + E.rackCapacity + "  <size=7><color=#f6ead0>(눌러서 버리기)</color></size>";
            RefreshCards();
        }

        // ───────────────────────── 일시정지 / 종료 ─────────────────────────

        void Pause()
        {
            if (paused || closing) return;
            paused = true;
            Time.timeScale = 0f;
            var m = Modal.Open("일시정지", 260, 150, "paper", false);
            var help = UIKit.Label(m.Content, "틀 클릭: 반죽→속→뒤집기→꺼내기\n1~" + molds.Count + ": 틀   Q/W/E: 속 선택   Space: 판매", 9, Pal.Ink, TextAnchor.MiddleCenter);
            help.rectTransform.At(0, 4, 240, 40);
            var b1 = UIKit.Button(m.Content, "계속하기", () => { m.Close(); paused = false; Time.timeScale = 1f; }, "button", 11);
            ((RectTransform)b1.transform).At(20, 56, 200, 26);
            var b2 = UIKit.Button(m.Content, "오늘 장사 접기", () => { m.Close(); paused = false; Time.timeScale = 1f; time = dayLen; }, "dark", 10);
            ((RectTransform)b2.transform).At(20, 88, 200, 24);
        }

        IEnumerator Close()
        {
            closing = true;
            Sfx.Play("perfect", 0.6f);
            var banner = UIKit.Frame(fxLayer, "wine");
            banner.rectTransform.Mid(320, 150, 240, 44);
            var bt = UIKit.Label(banner.transform, "영업 종료!", 18, Pal.Gold, TextAnchor.MiddleCenter, true, true);
            bt.rectTransform.Fill();
            Tw.Run(banner, Tw.Pop(banner.transform, 0.3f, 1.15f));
            foreach (var c in slots) if (c != null && !c.leaving) { if (c.heroine) StartCoroutine(HeroineLeave(c, GameData.Rina.shopLeft)); else Leave(c, false); }
            yield return new WaitForSeconds(2f);
            Finish();
        }

        void Finish()
        {
            finished = true;
            foreach (var m in molds) if (m.state != MoldState.Empty) result.wasted++;
            // 남은 붕어빵 중 가장 좋은 하나는 포장해서 선물용으로 챙긴다
            if (rack.Count > 0 && S.giftTaiyaki == 0)
            {
                int best = 0;
                for (int i = 1; i < rack.Count; i++) if (rack[i].quality > rack[best].quality) best = i;
                if (rack[best].quality > 0)
                {
                    S.giftTaiyaki = 1;
                    S.giftTaiyakiPerfect = rack[best].quality == 2;
                    S.giftTaiyakiFilling = rack[best].filling;
                    result.packedGift = true;
                    rack.RemoveAt(best);
                }
            }
            result.wasted += rack.Count;
            S.totalSold += result.sold;
            S.bestCombo = Mathf.Max(S.bestCombo, result.bestCombo);
            S.stamina = Mathf.Max(0, S.stamina - E.workStaminaCost);
            S.lastResult = result;
            S.Set("memory_first_day");
            Game.Go<SettlementScreen>();
        }
    }

    // ───────────────────────────── 정산 ─────────────────────────────
    public class SettlementScreen : ScreenBase
    {
        public override void Build()
        {
            Sfx.Bgm("night");
            var r = S.lastResult;
            UIKit.Sprite(Root, "Backgrounds/stall_day").rectTransform.Fill();
            UIKit.Rect(Root, new Color(0.03f, 0.04f, 0.15f, 0.55f)).rectTransform.Fill();
            UIKit.New("Snow", Root).Fill().gameObject.AddComponent<SnowFall>();

            var note = UIKit.Sprite(Root, "UI/notebook");
            note.preserveAspect = false;
            note.rectTransform.Mid(250, 182, 320, 280);
            var p = note.transform;
            var title = UIKit.Label(p, "오늘의 장사  DAY " + S.DayNumber.ToString("00"), 14, Pal.Brown, TextAnchor.MiddleLeft, true);
            title.rectTransform.At(40, 22, 240, 22);
            string[] rows =
            {
                "판매한 붕어빵", r.sold + "개",
                "PERFECT", r.perfect + "개",
                "설익음 / 탐", r.raw + " / " + r.burnt,
                "놓친 손님", r.missed + "명",
                "최고 콤보", "×" + r.bestCombo,
                "버린 붕어빵", r.wasted + "개",
                "판매 금액", UIKit.Yen(r.revenue),
                "콤보 팁", UIKit.Yen(r.tips),
            };
            for (int i = 0; i < rows.Length; i += 2)
            {
                var a = UIKit.Label(p, rows[i], 11, Pal.Ink, TextAnchor.MiddleLeft);
                a.rectTransform.At(44, 52 + i * 12, 130, 20);
                var b = UIKit.Label(p, rows[i + 1], 12, Pal.Brown, TextAnchor.MiddleRight, true);
                b.rectTransform.At(160, 52 + i * 12, 110, 20);
                Tw.Run(a, Tw.Pop(a.transform, 0.2f + i * 0.03f, 1.1f));
            }
            var total = UIKit.Label(p, "오늘 수입  " + UIKit.Yen(r.revenue + r.tips), 15, new Color(0.7f, 0.25f, 0.1f), TextAnchor.MiddleRight, true);
            total.rectTransform.At(40, 250, 232, 24);

            var side = UIKit.Frame(Root, "wood");
            side.rectTransform.At(420, 40, 200, 280);
            var sp = side.transform;
            var money = UIKit.Label(sp, "보유금\n<size=18>" + UIKit.Yen(S.money) + "</size>", 11, Pal.Gold, TextAnchor.MiddleCenter, true, true);
            money.rectTransform.At(0, 10, 200, 50);
            int lack = GameData.Eco.weeklyGoal - S.money;
            var goal = UIKit.Label(sp, lack > 0 ? "토요일 목표까지\n" + UIKit.Yen(lack) : "토요일 목표 달성! ♥", 10, new Color(1f, 0.7f, 0.8f), TextAnchor.MiddleCenter, false, true);
            goal.rectTransform.At(0, 66, 200, 34);
            var st = UIKit.Label(sp, "체력 " + UIKit.Hearts(S.stamina, GameData.Eco.maxStamina), 11, Pal.Cream, TextAnchor.MiddleCenter, true, true);
            st.rectTransform.At(0, 104, 200, 18);

            var extra = new List<string>();
            if (r.packedGift) extra.Add("남은 붕어빵 하나를 포장했다." + (S.giftTaiyakiPerfect ? " (PERFECT!)" : "") + "\n누군가에게 선물할 수 있을 것 같다.");
            if (r.heroineVisited && r.heroineServed) extra.Add("♡ " + GameData.Rina.name + "가 붕어빵을 사 갔다.");
            else if (r.heroineVisited) extra.Add("♡ " + GameData.Rina.name + "가 왔었는데… 놓쳤다.");
            if (r.bestCombo >= 10) extra.Add("콤보 ×10 달성! 기념 등불을 방에 걸자.");
            var ex = UIKit.Label(sp, string.Join("\n\n", extra.ToArray()), 9, Pal.Cream, TextAnchor.UpperCenter, false, true);
            ex.rectTransform.At(10, 130, 180, 100);

            var b1 = UIKit.Button(sp, "집으로 돌아가기", () => Game.I.EnterNight(), "button", 11);
            ((RectTransform)b1.transform).At(20, 238, 160, 30);
        }
    }
}
