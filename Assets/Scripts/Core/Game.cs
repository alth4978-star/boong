using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Taiyaki
{
    /// <summary>
    /// 게임 진입점. 씬에 아무것도 없어도 플레이 버튼을 누르면 자동으로 생성된다.
    /// 640×360 스테이지를 화면에 맞춰 확대(레터박스)하고, 화면(Screen) 전환·세이브·하루 진행을 담당한다.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public static GameState S { get { return I != null ? I.state : null; } }

        public const float W = 640f, H = 360f;

        public GameState state;
        public RectTransform Stage { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform PopupLayer { get; private set; }
        public RectTransform ToastLayer { get; private set; }
        Image fader;
        ScreenBase current;
        bool transitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<Game>() != null) return;
            var go = new GameObject("TaiyakiRomance");
            go.AddComponent<Game>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            GameData.Load();
            Art.Init();
            Sfx.Init(gameObject);
            BuildCanvas();
        }

        void Start()
        {
            Show<TitleScreen>(false);
        }

        void BuildCanvas()
        {
            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.cullingMask = 0;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // 640×360 고정 스테이지 (남는 영역은 검은 레터박스)
            Stage = UIKit.New("Stage", canvasGo.transform);
            Stage.anchorMin = Stage.anchorMax = new Vector2(0.5f, 0.5f);
            Stage.pivot = new Vector2(0.5f, 0.5f);
            Stage.sizeDelta = new Vector2(W, H);
            Stage.anchoredPosition = Vector2.zero;
            Stage.gameObject.AddComponent<RectMask2D>();

            ScreenLayer = UIKit.New("Screens", Stage).Fill();
            PopupLayer = UIKit.New("Popups", Stage).Fill();
            ToastLayer = UIKit.New("Toasts", Stage).Fill();
            fader = UIKit.Rect(Stage, Color.black, "Fader");
            fader.rectTransform.Fill();
            fader.raycastTarget = false;
            fader.color = new Color(0, 0, 0, 0);
        }

        // ───────────────────────── 화면 전환 ─────────────────────────

        public void Show<T>(bool fade = true) where T : ScreenBase
        {
            if (transitioning) return;
            if (fade) StartCoroutine(FadeSwap(typeof(T)));
            else Swap(typeof(T));
        }

        public static void Go<T>() where T : ScreenBase { I.Show<T>(); }

        void Swap(Type t)
        {
            ClearPopups();
            if (current != null)
            {
                current.OnLeave();
                Destroy(current.gameObject);
            }
            var rt = UIKit.New(t.Name, ScreenLayer).Fill();
            current = (ScreenBase)rt.gameObject.AddComponent(t);
            current.Build();
        }

        IEnumerator FadeSwap(Type t)
        {
            transitioning = true;
            fader.raycastTarget = true;
            yield return Tw.FadeGraphic(fader, 1f, 0.25f);
            Swap(t);
            yield return null;
            yield return Tw.FadeGraphic(fader, 0f, 0.3f);
            fader.raycastTarget = false;
            transitioning = false;
        }

        public void ClearPopups()
        {
            for (int i = PopupLayer.childCount - 1; i >= 0; i--) Destroy(PopupLayer.GetChild(i).gameObject);
        }

        /// <summary>화면 상단에 잠깐 떴다 사라지는 알림</summary>
        public static void Toast(string msg, Color? color = null, float dur = 2.2f)
        {
            if (I == null) return;
            I.StartCoroutine(I.ToastCo(msg, color ?? Pal.Cream, dur));
        }

        IEnumerator ToastCo(string msg, Color color, float dur)
        {
            int idx = ToastLayer.childCount;
            var bg = UIKit.Frame(ToastLayer, S != null && S.IsWeekend && S.day == 5 ? "neon" : "dark");
            bg.rectTransform.Mid(W / 2, 52 + idx * 26, 300, 24);
            var t = UIKit.Label(bg.transform, msg, 10, color, TextAnchor.MiddleCenter, false, true);
            t.rectTransform.Fill(6, 2, 6, 2);
            var cg = bg.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            yield return Tw.Fade(cg, 0, 1, 0.15f);
            yield return Tw.Wait(dur);
            yield return Tw.Fade(cg, 1, 0, 0.3f);
            if (bg != null) Destroy(bg.gameObject);
        }

        // ───────────────────────── 게임 진행 ─────────────────────────

        public void NewGame()
        {
            state = GameState.NewGame();
            state.Deliver("rina", "mon_morning");
            Show<PrologueScreen>();
        }

        public void Continue()
        {
            state = SaveSystem.Load();
            if (state == null) { NewGame(); return; }
            GoMorning();
        }

        /// <summary>현재 요일 아침 화면으로 (월요일이면 주간 계획부터)</summary>
        public void GoMorning()
        {
            SaveSystem.Save(state);
            if (state.day == 0 && state.phase == 0 && !state.Has("plan_done_w" + state.week)) Show<WeekPlanScreen>();
            else Show<HomeScreen>();
        }

        /// <summary>하루 활동을 끝내고 밤으로</summary>
        public void EnterNight()
        {
            state.phase = 1;
            DeliverNightMessages();
            Show<HomeScreen>();
        }

        /// <summary>침대: 다음 날 아침으로</summary>
        public void Sleep()
        {
            if (state.day == 6)
            {
                Show<WeekSummaryScreen>();
                return;
            }
            state.day++;
            state.phase = 0;
            DeliverMorningMessages();
            GoMorning();
        }

        /// <summary>주간 결산 후 다음 주 월요일</summary>
        public void StartNextWeek()
        {
            state.week++;
            state.day = 0;
            state.phase = 0;
            state.weekSales = 0;
            state.weekSpent = 0;
            state.weekStartMoney = state.money;
            state.weekFlags.Clear();
            state.heroineVisitDay = -1;
            state.stamina = Mathf.Min(GameData.Eco.maxStamina, state.stamina + 2);
            state.Deliver("rina", state.affection >= 25 ? "mon_morning_close" : "mon_morning2");
            GoMorning();
        }

        void DeliverMorningMessages()
        {
            var s = state;
            // 일요일 아침: 데이트 신청 or 짧은 인사
            if (s.day == 6)
            {
                if (s.Has("club_done") && !s.Has("rina_went_home_drunk") && s.affection >= 22) s.Deliver("rina", "sun_invite");
                else if (s.Has("club_done")) s.Deliver("rina", "sun_hello");
            }
        }

        void DeliverNightMessages()
        {
            var s = state;
            var plan = s.Today;
            if (plan == DayPlan.Rest && !s.Has("msg_rest_w" + s.week))
            {
                s.SetWeek("msg_rest_w" + s.week);
                s.Deliver("rina", "rest_day");
            }
            else if (s.day == 1 && plan == DayPlan.Work) s.Deliver("rina", "tue_night");
            else if (s.day == 4) s.Deliver("rina", "fri_night");
            else if (s.day == 5 && s.Has("club_done"))
            {
                if (s.Has("rina_went_home_drunk")) s.Deliver("rina", "sat_night_drunk");
                else if (s.Has("karaoke_done")) s.Deliver("rina", "sat_night_karaoke");
                else s.Deliver("rina", "sat_night");
            }
        }
    }

    /// <summary>모든 화면의 기반 클래스</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected RectTransform Root { get { return (RectTransform)transform; } }
        protected GameState S { get { return Game.S; } }
        public abstract void Build();
        public virtual void OnLeave() { }
    }
}
