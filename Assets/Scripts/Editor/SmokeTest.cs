using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Taiyaki.EditorTools
{
    /// <summary>
    /// Play 모드 자동 스모크 테스트.
    /// 봇이 실제 버튼을 눌러 게임 속 3주를 진행하면서 런타임 예외 / [Art] 경고 / 약속 대사 / 화면 밖·넘치는 텍스트를 검사하고
    /// 화면별 스크린샷을 남긴다. 결과는 Logs/SmokeTest/report.txt 와 *.png.
    /// - 에디터: Taiyaki/Run Smoke Test
    /// - 배치:  Unity -batchmode -projectPath . -executeMethod Taiyaki.EditorTools.SmokeTest.RunBatch -logFile smoke.log
    ///          (종료 코드 0 = 통과, 1 = 실패)
    /// 세이브 데이터는 시작 전에 백업했다가 끝나면 되돌린다.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeTest
    {
        const string KeyRunning = "Taiyaki.Smoke.Running", KeyBatch = "Taiyaki.Smoke.Batch";
        const string KeyHadSave = "Taiyaki.Smoke.HadSave", KeySave = "Taiyaki.Smoke.Save", KeyFailed = "Taiyaki.Smoke.Failed";
        const string SaveKey = "taiyaki_romance_save_v1";
        const double TimeLimit = 25 * 60;
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static string OutDir { get { return Path.GetFullPath("Logs/SmokeTest"); } }

        // ───────── 진행 ─────────
        class Until
        {
            public Func<bool> cond; public float timeout; public string desc;
            public Until(Func<bool> c, float t, string d) { cond = c; timeout = t; desc = d; }
        }

        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static double waitUntil, startTime, condDeadline;
        static Func<bool> waitCond;
        static string condDesc, step = "시작";
        static bool finishing;

        // ───────── 결과 ─────────
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly List<string> warnings = new List<string>();
        static readonly List<string> layout = new List<string>();
        static readonly HashSet<string> layoutSeen = new HashSet<string>();
        static readonly List<string> notes = new List<string>();
        static readonly List<string> balance = new List<string>();
        static readonly List<string> transcript = new List<string>();
        static readonly HashSet<string> spoken = new HashSet<string>();
        static readonly HashSet<Transform> seenSpeech = new HashSet<Transform>();
        static int failCount, shotIndex;
        static string lastLine, lastHeroineOrder;
        static bool settlementShot;

        static SmokeTest()
        {
            if (!SessionState.GetBool(KeyRunning, false)) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // 도메인 리로드 직후 = Play 모드 진입 중. Game.Awake 로그부터 잡는다.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
            }
        }

        [MenuItem("Taiyaki/Run Smoke Test")]
        static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Begin(false);
        }

        public static void RunBatch() { Begin(true); }

        static void Begin(bool batch)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Smoke] Play 모드를 끈 뒤 실행하세요."); return; }
            SessionState.SetBool(KeyBatch, batch);
            SessionState.SetBool(KeyHadSave, PlayerPrefs.HasKey(SaveKey));
            SessionState.SetString(KeySave, PlayerPrefs.GetString(SaveKey, ""));
            PlayerPrefs.DeleteKey(SaveKey);
            SessionState.SetBool(KeyRunning, true);
            SessionState.SetInt(KeyFailed, -1);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorSceneManager.OpenScene(TaiyakiMenu.MainScene);
            EditorApplication.delayCall += EditorApplication.EnterPlaymode;
        }

        static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (!SessionState.GetBool(KeyRunning, false)) return;
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                startTime = EditorApplication.timeSinceStartup;
                finishing = false;
                stack.Clear();
                stack.Push(Scenario());
            }
            else if (s == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                SessionState.SetBool(KeyRunning, false);
                if (SessionState.GetBool(KeyHadSave, false)) PlayerPrefs.SetString(SaveKey, SessionState.GetString(KeySave, ""));
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
                int failed = SessionState.GetInt(KeyFailed, -1);
                if (SessionState.GetBool(KeyBatch, false)) EditorApplication.Exit(failed == 0 ? 0 : 1);
                else if (failed == 0) Debug.Log("[Smoke] 통과 — " + OutDir);
                else Debug.LogError("[Smoke] 실패 " + failed + "건 — " + OutDir + "/report.txt");
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || finishing) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - startTime > TimeLimit) { Fail("전체 시간 초과"); Finish(); return; }
            if (now < waitUntil) return;
            if (waitCond != null)
            {
                bool ok;
                try { ok = waitCond(); }
                catch (Exception e) { Fail("대기 조건 예외 (" + condDesc + "): " + e.Message); Finish(); return; }
                if (!ok)
                {
                    if (now > condDeadline) { Fail("대기 시간 초과: " + condDesc); Finish(); }
                    return;
                }
                waitCond = null;
            }
            for (int guard = 0; guard < 100; guard++)
            {
                if (stack.Count == 0) { Finish(); return; }
                var top = stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e) { Fail("시나리오 예외: " + e); Finish(); return; }
                if (!more) { stack.Pop(); continue; }
                var y = top.Current;
                if (y is IEnumerator sub) { stack.Push(sub); continue; }
                if (y is float f) { waitUntil = now + f; return; }
                if (y is Until u) { waitCond = u.cond; condDeadline = now + u.timeout; condDesc = u.desc; return; }
                return;
            }
        }

        static void Finish()
        {
            if (finishing) return;
            finishing = true;
            Time.timeScale = 1f;
            int failed = failCount + errors.Count;
            try { WriteReport(failed); }
            catch (Exception e) { Debug.LogError("[Smoke] 리포트 저장 실패: " + e); failed++; }
            SessionState.SetInt(KeyFailed, failed);
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }

        static void OnLog(string msg, string stackTrace, LogType type)
        {
            if (msg.StartsWith("[Smoke]")) return;
            string where = " @" + step + " / " + CurName;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                errors.Add("[" + type + "] " + msg + where + "\n      " + FirstLines(stackTrace, 5).Replace("\n", "\n      "));
            else if (type == LogType.Warning)
            {
                if (msg.Contains("[Art]")) errors.Add("[Art] " + msg + where);
                else warnings.Add(msg + where);
            }
        }

        // ───────────────────────── 시나리오 ─────────────────────────

        static IEnumerator Scenario()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var f in Directory.GetFiles(OutDir)) File.Delete(f);
            yield return new Until(() => Game.I != null && Cur is TitleScreen, 30f, "타이틀 화면");
            yield return 0.6f;
            UnityEngine.Random.InitState(20260927);

            Step("타이틀");
            yield return Shot("title");
            Click("새 게임");
            yield return WaitScreen<PrologueScreen>();
            yield return 0.8f;
            yield return Shot("prologue");
            Click("건너뛰기 ▶▶");
            yield return WaitScreen<WeekPlanScreen>();
            yield return Shot("week_plan");
            Click("이번 주 시작");
            yield return WaitScreen<HomeScreen>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-smokeQuick") >= 0) { yield return Shot("home_quick"); yield break; }

            yield return Week1();
            yield return Week2();
            yield return Week3();
        }

        // ── 1주차: 사람 속도 봇 (밸런스 기록), 재료 경고 · 스마트폰 배지 · 토요일→노래방→일요일
        static IEnumerator Week1()
        {
            var human = new BotOpts { speed = 4f, actionGap = 0.45f, human = true };

            Step("W1 월 아침");
            Check("월요일 아침: 재료 부족 안내 문구", HomeHint.Contains("재료가 " + S.BakeableCount + "개 분량뿐"), HomeHint);
            Check("월요일 아침: 첫 메시지 알림 배지 켜짐", BadgeOn && S.UnreadCount == 1, "unread=" + S.UnreadCount);
            yield return Shot("home_mon_morning");
            Click("현관 · 외출");
            yield return 0.4f;
            Check("현관: [냉장고 열기 / 그냥 출근] 경고창", FindButton("냉장고 열기") != null && FindButton("그냥 출근") != null);
            yield return Shot("home_stock_warning");
            Click("냉장고 열기");
            yield return 0.4f;
            yield return Shot("fridge");
            int b0 = S.batter, r0 = S.red;
            Click(PackLabel("batter"));
            Click(PackLabel("red"));
            Check("냉장고: 반죽/팥 구매 반영", S.batter == b0 + 10 && S.red == r0 + 10, "batter " + S.batter + ", red " + S.red);
            Click("×");
            yield return 0.3f;
            Check("냉장고 구매 후 안내 문구 갱신", HomeHint.Contains("장사하는 날"), HomeHint);

            yield return Phone("꼭 갈게", "phone_mon");
            Check("월요일 답장: promise_sat", S.Has("promise_sat"));
            Check("답장 후 알림 배지 꺼짐", !BadgeOn && S.UnreadCount == 0, "unread=" + S.UnreadCount);

            yield return WorkDay(human);
            yield return Night(null);

            Step("W1 화");
            yield return WorkDay(new BotOpts { speed = 4f, actionGap = 0.45f, human = true, testPause = true, shotName = "shop_busy" });
            Step("W1 화 밤");
            Check("화요일 밤: 새 메시지 배지", BadgeOn && S.UnreadCount == 1, "unread=" + S.UnreadCount);
            yield return Sleep();   // tue_night 는 일부러 답장하지 않는다

            Step("W1 수");
            yield return WorkDay(human);
            Check("수요일 밤: 답장 안 한 화요일 메시지는 아직 답장 가능 (배지 유지)", BadgeOn && S.UnreadCount == 1, "unread=" + S.UnreadCount);
            yield return Sleep();

            Step("W1 목 (휴식)");
            Check("휴식일 안내 문구", HomeHint.Contains("쉬는 날"), HomeHint);
            yield return RestDay();
            var tue = S.inbox.Find(m => m.id == "tue_night");
            Check("새 메시지가 오면 옛 미답장 메시지는 '답장 안 함' 처리", tue != null && tue.answer == InboxMsg.Skipped, tue != null ? "answer=" + tue.answer : "없음");
            Check("휴식일 밤: 배지 켜짐 (새 메시지 + 아직 안 읽은 화요일 메시지)", BadgeOn && S.UnreadCount == 2, "unread=" + S.UnreadCount);
            Click("스마트폰");
            yield return 0.4f;
            Check("폰 화면에 '(답장하지 않았다)' 표시", FindText("(답장하지 않았다)") != null);
            yield return Shot("phone_skipped");
            Click("내일 와");
            yield return 0.3f;
            Click("×");
            yield return 0.3f;
            Check("휴식일 답장: promise_visit", S.Has("promise_visit"));
            Check("답장 후 배지 꺼짐 (옛 메시지 때문에 켜져 있지 않음)", !BadgeOn && S.UnreadCount == 0, "unread=" + S.UnreadCount);
            yield return Sleep();

            Step("W1 금");
            yield return WorkDay(human);
            Check("금요일: '내일 와' 약속으로 리나 방문", S.lastResult.heroineVisited, "served=" + S.lastResult.heroineServed);
            yield return Night("당연하지!");

            Step("W1 토");
            Check("토요일 안내 문구", HomeHint.Contains("토요일 밤"), HomeHint);
            yield return ClubEntrance(5000 + 2500 + 4000, true);
            Check("클럽: promise_sat 회수 대사", Said(Rina.promiseSatKept));
            Check("클럽: promise_sat 플래그 소모", !S.Has("promise_sat"));
            Check("클럽: 노점 방문 기억 대사", Said(Rina.memoryPerfect) || Said(Rina.memoryVisited) || Said(Rina.memoryMissed));
            yield return Drink("딸기 칵테일");
            yield return Talk("붕어빵 장사 이야기", "많을 땐 서른 개 넘게 구워");
            yield return Talk("겨울 좋아해?", "눈 오는 밤은 좋아");
            yield return Talk("여기서 일하는 거", "피곤할 땐 붕어빵 먹으러 와");
            yield return GiftTaiyaki();
            yield return KaraokeOrHome(true);
            Step("W1 토 밤");
            Check("토요일 밤 메시지", S.inbox.Exists(m => m.week == 1 && m.id.StartsWith("sat_night")));
            yield return Night("*");

            yield return Sunday(true);
        }

        // ── 2주차: 빠른 봇, 약속(딸기/팥/꿈) 만들기
        static IEnumerator Week2()
        {
            var expert = new BotOpts { speed = 6f };
            Step("W2 월");
            Check("2주차 월요일 메시지 배지", BadgeOn);
            yield return Phone("*");
            yield return WorkDay(expert);
            yield return Night(null);
            Step("W2 화"); yield return WorkDay(expert); yield return Night("*");
            Step("W2 수"); yield return WorkDay(expert); yield return Night(null);
            Step("W2 목"); yield return RestDay(); yield return Night("오늘 쉬었어");
            Step("W2 금"); yield return WorkDay(expert); yield return Night("*");

            Step("W2 토");
            yield return ClubEntrance(5000 + 2500 + 2000 + 4500 + 4000, false);
            yield return Drink("딸기 칵테일");
            yield return Talk("좋아하는 맛", "만들어 볼게. 제일 먼저 먹게 해 줄게");
            Check("딸기 약속 + 레시피 힌트", S.Has("promise_strawberry") && S.Has("hint_strawberry"));
            yield return Drink("하이볼");
            yield return Talk("어릴 적 겨울 이야기", "다음엔 내가 구운 팥붕 가져올게");
            Check("팥붕 약속 플래그", S.Has("promise_red"));
            if (S.giftTaiyaki == 0 || S.giftTaiyakiFilling != "red")
            {
                Manip("선물 붕어빵을 팥으로 고정 (원래 " + (S.giftTaiyaki == 0 ? "없음" : S.giftTaiyakiFilling) + ")");
                S.giftTaiyaki = 1; S.giftTaiyakiFilling = "red";
            }
            yield return GiftTaiyaki();
            Check("클럽: 팥 붕어빵 선물 → promiseRedKept 대사", Said(Rina.promiseRedKept));
            Check("promise_red 플래그 소모", !S.Has("promise_red"));
            yield return Drink("위스키 온더락");
            Check("취기 경고 대사 (drunk = tolerance-1)", Said(Rina.drunkWarn));
            yield return Talk("리나의 비밀", "그럼 붕어빵 메뉴도 넣자");
            Check("dream_partner 플래그", S.Has("dream_partner"));
            yield return KaraokeOrHome(false);
            Step("W2 토 밤");
            yield return Night("*");
            yield return Sunday(false);
        }

        // ── 3주차: 약속 회수(노점 딸기 · 폐점 놓침 · 꿈 · 선물 딸기), 그냥 출근 경로
        static IEnumerator Week3()
        {
            var expert = new BotOpts { speed = 6f };
            Step("W3 월");
            yield return Phone("*");
            Click("책상 · 연구");
            yield return 0.4f;
            yield return Shot("desk");
            var straw = GameData.Filling("strawberry");
            if (S.money < straw.researchPrice) { Manip("연구비 부족 → 보유금 " + S.money + " → " + straw.researchPrice); S.money = straw.researchPrice; }
            Check("책상: 딸기 크림 연구 버튼", FindButton("연구 " + UIKit.Yen(straw.researchPrice)) != null);
            Click("연구 " + UIKit.Yen(straw.researchPrice));
            yield return 0.5f;
            Check("딸기 크림 연구 완료", S.IsUnlocked("strawberry") && S.strawberry >= 6, "stock " + S.strawberry);
            Manip("리나 방문일을 오늘(월)로 지정");
            S.heroineVisitDay = 0;
            lastHeroineOrder = null;
            yield return WorkDay(expert);
            Check("노점 리나: 딸기 크림 주문", lastHeroineOrder == "strawberry", lastHeroineOrder);
            Check("노점 리나: 딸기 약속 대사(shopPromiseStrawberry)", Said(Rina.shopPromiseStrawberry));
            Check("promise_strawberry 플래그 소모", !S.Has("promise_strawberry"));
            yield return Night(null);

            Step("W3 화");
            Manip("promise_visit 설정 + 리나 대기 중 폐점");
            S.SetWeek("promise_visit");
            yield return WorkDay(new BotOpts { speed = 6f, ignoreHeroine = true, closeWhenHeroineWaits = true });
            Check("폐점 때 기다리던 리나 → rina_missed", S.Has("rina_missed") && S.lastResult.heroineVisited && !S.lastResult.heroineServed && S.lastResult.missed >= 1,
                "missed=" + S.lastResult.missed);
            Check("폐점 때 리나 대사(shopLeft)", Said(Rina.shopLeft));
            yield return Night("*");

            Step("W3 수");
            Manip("반죽 5개로 줄여 경고창 → [그냥 출근]");
            S.batter = Mathf.Min(S.batter, 5);
            Call(Cur, "Refresh");
            Check("재료 부족 안내 문구 (수요일)", HomeHint.Contains("재료가 " + S.BakeableCount + "개 분량뿐"), HomeHint);
            Click("현관 · 외출");
            yield return 0.4f;
            Click("그냥 출근");
            yield return WaitScreen<MapScreen>();
            Check("[그냥 출근] → 지도", Cur is MapScreen);
            Click("붕어빵 노점");
            yield return ShopBot(new BotOpts { speed = 6f, fast = true });
            yield return AfterShop();
            yield return Night(null);

            Step("W3 목"); yield return RestDay(); yield return Night("*");
            Step("W3 금"); yield return WorkDay(new BotOpts { speed = 6f, fast = true }); yield return Night("*");

            Step("W3 토");
            yield return ClubEntrance(5000 + 2500 + 4000, false);
            Check("클럽: 동업 얘기 회상(dreamRecall)", Said(Rina.dreamRecall));
            Check("dream_recalled 플래그", S.Has("dream_recalled"));
            Manip("딸기 약속 다시 설정 + 선물 붕어빵을 딸기 크림으로");
            S.Set("promise_strawberry");
            S.giftTaiyaki = 1; S.giftTaiyakiFilling = "strawberry"; S.giftTaiyakiPerfect = true;
            yield return GiftTaiyaki();
            Check("클럽: 딸기 붕어빵 선물 → promiseStrawberryKept 대사", Said(Rina.promiseStrawberryKept));
            yield return Drink("딸기 칵테일");
            yield return KaraokeOrHome(false);
            yield return Night("*");
            Step("W3 일");
            yield return Sunday(false, true);
        }

        // ───────────────────────── 하루 단위 ─────────────────────────

        class BotOpts
        {
            public float speed = 4f, actionGap;
            public bool human, ignoreHeroine, fast, closeWhenHeroineWaits, testPause;
            public string shotName;
        }

        static IEnumerator WorkDay(BotOpts o)
        {
            int spent0 = S.weekSpent;
            yield return BuyStock(30);
            dayBuy = S.weekSpent - spent0;
            Click("현관 · 외출");
            yield return 0.3f;
            if (FindButton("그냥 출근") != null) { Fail("재료를 채웠는데 경고창이 뜸 (bakeable " + S.BakeableCount + ")"); Click("그냥 출근"); }
            yield return WaitScreen<MapScreen>();
            if (S.week == 1 && S.day == 0) yield return Shot("map_day");
            Click("붕어빵 노점");
            yield return ShopBot(o);
            yield return AfterShop();
        }

        static int dayBuy;

        static IEnumerator AfterShop()
        {
            yield return WaitScreen<SettlementScreen>();
            var r = S.lastResult;
            balance.Add(string.Format("W{0} {1} | 판매 {2,2}개 (PERFECT {3,2}, 설익 {4}, 탐 {5}) 놓친 손님 {6}, 버림 {7} | 수입 {8,7} (팁 {9}) | 재료 구입 {10,6} | 보유금 {11,7} | 남은 재고 반죽{12}/팥{13}/크림{14}",
                S.week, GameState.DayKo[S.day], r.sold, r.perfect, r.raw, r.burnt, r.missed, r.wasted, UIKit.Yen(r.revenue + r.tips), UIKit.Yen(r.tips),
                UIKit.Yen(dayBuy), UIKit.Yen(S.money), S.batter, S.red, S.cream));
            dayBuy = 0;
            if (!settlementShot) { settlementShot = true; yield return Shot("settlement"); }
            Click("집으로 돌아가기");
            yield return WaitScreen<HomeScreen>();
        }

        static IEnumerator BuyStock(int target)
        {
            if (S.BakeableCount >= target && (!S.IsUnlocked("strawberry") || S.strawberry >= 10)) yield break;
            Click("냉장고 · 재료");
            yield return 0.3f;
            if (S.IsUnlocked("strawberry") && S.strawberry < 10 && S.money >= GameData.Ingredient("strawberry").packPrice) Click(PackLabel("strawberry"));
            for (int guard = 0; guard < 20 && S.BakeableCount < target; guard++)
            {
                int fill = 0;
                foreach (var f in S.unlockedFillings) fill += S.Stock(f);
                string id = S.batter <= fill ? "batter" : S.red <= S.cream * 1.4f ? "red" : "cream";
                if (S.money < GameData.Ingredient(id).packPrice) { Note("재료 살 돈 부족 (보유금 " + UIKit.Yen(S.money) + ")"); break; }
                Click(PackLabel(id));
            }
            Click("×");
            yield return 0.3f;
        }

        static IEnumerator RestDay()
        {
            var prev = Cur;
            Click("침대");
            yield return 0.3f;
            Click("쉰다");
            yield return WaitScreen<HomeScreen>(prev);
        }

        static IEnumerator Night(string answer)
        {
            if (answer != null && BadgeOn) yield return Phone(answer);
            yield return Sleep();
        }

        static IEnumerator Sleep()
        {
            var prev = Cur;
            Click("침대");
            yield return 0.3f;
            Click("잔다");
            yield return new Until(() => Cur != prev && Cur != null && !Transitioning, 20f, "잠자기 후 화면");
            yield return 0.45f;
        }

        /// <summary>answer: 선택지 글자 / "*" = 첫 번째 선택지 / null = 읽기만</summary>
        static IEnumerator Phone(string answer, string shot = null)
        {
            Click("스마트폰");
            yield return 0.4f;
            if (shot != null) yield return Shot(shot);
            if (answer == "*")
            {
                var b = FirstPhoneChoice();
                if (b != null) { b.onClick.Invoke(); yield return 0.3f; }
            }
            else if (answer != null)
            {
                if (FindButton(answer) != null) { Click(answer); yield return 0.3f; }
                else Fail("답장 선택지 없음: " + answer);
            }
            Click("×");
            yield return 0.3f;
        }

        static IEnumerator ClubEntrance(int budget, bool shots)
        {
            if (S.money < budget)
            {
                Manip("토요일 자금 부족: 보유금 " + UIKit.Yen(S.money) + " → 테스트용 " + UIKit.Yen(budget));
                S.money = budget;
            }
            else Note("토요일 도착 시 보유금 " + UIKit.Yen(S.money) + " (필요 예산 " + UIKit.Yen(budget) + ")");
            Click("현관 · 외출");
            yield return WaitScreen<MapScreen>();
            if (shots) { yield return 2.0f; yield return Shot("map_saturday"); }
            Click("번화가");
            yield return WaitScreen<StreetScreen>();
            if (shots) { yield return 1.8f; yield return Shot("street"); }
            Click("입장");
            yield return 0.3f;
            Click("입장");
            yield return WaitScreen<ClubScreen>();
            yield return 0.6f;
            if (shots) yield return Shot("club_intro");
            yield return Pump();
            yield return new Until(() => FindButton("술 주문") != null, 10f, "클럽 메뉴");
            if (shots) yield return Shot("club_menu");
        }

        static IEnumerator ClubMenu(string label)
        {
            yield return new Until(() => FindButton(label) != null, 10f, "클럽 메뉴 " + label);
            Click(label);
            yield return 0.25f;
        }

        static IEnumerator Drink(string name)
        {
            yield return ClubMenu("술 주문");
            yield return Pick(name);
        }

        static IEnumerator Talk(string topic, string choice)
        {
            yield return ClubMenu("대화");
            if (!WaitingChoice) { Note("대화 주제 없음 (" + topic + ")"); yield return Pump(); yield break; }
            if (FindButton(topic) == null)
            {
                Fail("대화 주제가 목록에 없음: " + topic + " (호감도 " + S.affection + ")");
                Click("그만두기");
                yield return 0.2f;
                yield break;
            }
            yield return Pick(topic);
            yield return Pick(choice);
        }

        static IEnumerator GiftTaiyaki()
        {
            yield return ClubMenu("선물");
            if (FindButton("직접 구운") != null) yield return Pick("직접 구운");
            else
            {
                Note("선물할 붕어빵 없음");
                if (WaitingChoice) Click("그만두기"); else yield return Pump();
                yield return 0.2f;
            }
        }

        static IEnumerator KaraokeOrHome(bool shots)
        {
            yield return new Until(() => FindButton("술 주문") != null, 10f, "클럽 메뉴");
            if (FindButton("2차 갈래") != null)
            {
                Click("2차 갈래");
                yield return 0.25f;
                yield return Pick("“2차 갈래?”");
                yield return KaraokeBot(shots);
            }
            else
            {
                Note("노래방 버튼 안 나옴 (호감도 " + S.affection + ")");
                yield return ClubMenu("귀가");
                yield return Pick("응, 들어가자");
            }
            yield return WaitScreen<HomeScreen>();
        }

        static IEnumerator Sunday(bool shots, bool last = false)
        {
            Step("W" + S.week + " 일");
            if (BadgeOn)
            {
                var invite = S.inbox.Exists(m => m.week == S.week && m.id == "sun_invite");
                yield return Phone(invite ? "좋아! 바로 갈게" : "*", shots ? "phone_sunday" : null);
            }
            if (S.Has("date_accepted"))
            {
                Check("일요일 데이트 안내 문구", HomeHint.Contains("겨울 축제"), HomeHint);
                Click("현관 · 외출");
                yield return WaitScreen<MapScreen>();
                Click("쇼핑거리 · 겨울 축제");
                yield return WaitScreen<DateScreen>();
                yield return 0.6f;
                if (shots) yield return Shot("date");
                yield return Pick("팥 붕어빵을 구워 준다");
                yield return 1.6f;
                if (shots) yield return Shot("date_cg");
                yield return Pump();
                yield return 1.2f;
                yield return Pump();
                yield return WaitScreen<HomeScreen>();
                Check("데이트 완료", S.Has("date_done") && S.Has("memory_date"));
            }
            else
            {
                Note("일요일 데이트 신청 없음 (호감도 " + S.affection + ")");
                yield return RestDay();
            }
            yield return Sleep();
            if (!(Cur is WeekSummaryScreen)) { Fail("일요일 밤 → 주간 결산이 아님: " + CurName); yield break; }
            yield return Shot("week" + S.week + "_summary");
            if (last) yield break;
            Click("다음 주 월요일 ▶");
            yield return WaitScreen<WeekPlanScreen>();
            Click("이번 주 시작");
            yield return WaitScreen<HomeScreen>();
        }

        // ───────────────────────── 장사 봇 ─────────────────────────

        static IEnumerator ShopBot(BotOpts o)
        {
            yield return WaitScreen<ShopScreen>();
            var shop = Cur;
            float dayLen = (float)Get(shop, "dayLen");
            if (o.fast) Set(shop, "time", dayLen - 0.3f);
            Time.timeScale = o.speed;
            var rnd = new System.Random(S.DayNumber * 31);
            var targets = new Dictionary<object, float>();
            float nextAction = 0;
            bool shot = false, paused = false;
            while (Cur == shop && !(bool)Get(shop, "finished"))
            {
                float time = (float)Get(shop, "time");
                if (!(bool)Get(shop, "closing"))
                {
                    if (o.actionGap <= 0 || time >= nextAction)
                        if (BotAct(shop, o, rnd, targets)) nextAction = time + o.actionGap;
                    if (o.closeWhenHeroineWaits && (bool)Get(shop, "heroineSpawned") && time < dayLen - 3f)
                        Set(shop, "time", dayLen - 2.5f);
                }
                WatchCustomers(shop);
                if (o.shotName != null && !shot && time > dayLen * 0.45f && (Customers(shop) >= 3 || time > dayLen * 0.7f))
                {
                    shot = true;
                    yield return Shot(o.shotName);
                }
                if (o.testPause && !paused && time > dayLen * 0.6f)
                {
                    paused = true;
                    yield return TestPause(shop, o.speed);
                }
                yield return null;
            }
            Time.timeScale = 1f;
        }

        static IEnumerator TestPause(ScreenBase shop, float speed)
        {
            Call(shop, "Pause");
            yield return 0.35f;
            Check("일시정지: timeScale 0", Time.timeScale == 0f && (bool)Get(shop, "paused"));
            float t0 = (float)Get(shop, "time");
            yield return 0.4f;
            Check("일시정지 중 영업 시간 정지", Mathf.Approximately(t0, (float)Get(shop, "time")));
            Call(shop, "ClickMold", ((IList)Get(shop, "molds"))[0]);
            yield return Shot("shop_pause");
            Click("계속하기");
            yield return 0.1f;
            Check("계속하기 후 재개", Time.timeScale == 1f && !(bool)Get(shop, "paused"));
            Time.timeScale = speed;
        }

        static bool BotAct(ScreenBase shop, BotOpts o, System.Random rnd, Dictionary<object, float> targets)
        {
            var E = GameData.Eco;
            var molds = (IList)Get(shop, "molds");
            var rack = (IList)Get(shop, "rack");
            var slots = (IList)Get(shop, "slots");
            bool one = o.actionGap > 0, acted = false;

            // 1) 굽기 타이밍 — 급한 것부터
            foreach (var m in molds)
            {
                string st = Get(m, "state").ToString();
                if (st != "SideB") targets.Remove(m);
                if (st == "SideB")
                {
                    float target;
                    if (!targets.TryGetValue(m, out target))
                        targets[m] = target = o.human ? 0.8f + (float)rnd.NextDouble() * 0.38f : 0.95f;
                    if ((float)Get(m, "b") >= target && rack.Count < E.rackCapacity) { Call(shop, "ClickMold", m); acted = true; if (one) return true; }
                }
                else if ((st == "SideA" && (float)Get(m, "a") >= 1.05f) || st == "Burnt")
                {
                    Call(shop, "ClickMold", m); acted = true; if (one) return true;
                }
            }

            // 2) 판매
            foreach (var c in slots)
            {
                if (c == null || (bool)Get(c, "leaving")) continue;
                if (o.ignoreHeroine && (bool)Get(c, "heroine")) continue;
                if (CanServe(c, rack)) { Call(shop, "Serve", c); acted = true; if (one) return true; }
            }

            // 3) 속 넣기 · 반죽 붓기
            var need = new Dictionary<string, int>();
            foreach (var c in slots)
            {
                if (c == null || (bool)Get(c, "leaving")) continue;
                if (o.ignoreHeroine && (bool)Get(c, "heroine")) continue;
                foreach (var kv in (Dictionary<string, int>)Get(c, "order")) { int v; need.TryGetValue(kv.Key, out v); need[kv.Key] = v + kv.Value; }
            }
            foreach (var it in rack) { string f = (string)Get(it, "filling"); if (need.ContainsKey(f)) need[f]--; }
            int busy = 0;
            foreach (var m in molds)
            {
                string st = Get(m, "state").ToString();
                if (st == "Batter" || st == "SideA" || st == "SideB") busy++;
                if ((st == "SideA" || st == "SideB") && need.ContainsKey((string)Get(m, "filling"))) need[(string)Get(m, "filling")]--;
            }
            foreach (var m in molds)
            {
                string st = Get(m, "state").ToString();
                if (st == "Batter")
                {
                    string f = ChooseFilling(need, rack.Count + busy <= E.rackCapacity, rnd);
                    if (f == null) continue;
                    if ((string)Get(shop, "selected") != f) Call(shop, "Select", f);
                    Call(shop, "ClickMold", m);
                    if (need.ContainsKey(f)) need[f]--;
                    acted = true; if (one) return true;
                }
                else if (st == "Empty" && S.batter > 0 && rack.Count + busy < E.rackCapacity && ChooseFilling(need, true, rnd) != null)
                {
                    Call(shop, "ClickMold", m);
                    busy++;
                    acted = true; if (one) return true;
                }
            }
            return acted;
        }

        static string ChooseFilling(Dictionary<string, int> need, bool prebake, System.Random rnd)
        {
            string best = null;
            foreach (var kv in need) if (kv.Value > 0 && S.Stock(kv.Key) > 0 && (best == null || kv.Value > need[best])) best = kv.Key;
            if (best != null || !prebake) return best;
            int total = 0;
            foreach (var f in GameData.Root.fillings) if (S.IsUnlocked(f.id) && S.Stock(f.id) > 0) total += f.weight;
            if (total == 0) return null;
            int r = rnd.Next(total);
            foreach (var f in GameData.Root.fillings)
            {
                if (!S.IsUnlocked(f.id) || S.Stock(f.id) <= 0) continue;
                r -= f.weight;
                if (r < 0) return f.id;
            }
            return null;
        }

        static bool CanServe(object c, IList rack)
        {
            foreach (var kv in (Dictionary<string, int>)Get(c, "order"))
            {
                int have = 0;
                foreach (var it in rack) if ((string)Get(it, "filling") == kv.Key) have++;
                if (have < kv.Value) return false;
            }
            return true;
        }

        static int Customers(ScreenBase shop)
        {
            int n = 0;
            foreach (var c in (IList)Get(shop, "slots")) if (c != null) n++;
            return n;
        }

        /// <summary>손님 말풍선 위치 검사 + 리나 말풍선 대사 기록</summary>
        static void WatchCustomers(ScreenBase shop)
        {
            var stage = Game.I.Stage;
            foreach (var c in (IList)Get(shop, "slots"))
            {
                if (c == null) continue;
                bool heroine = (bool)Get(c, "heroine");
                if (heroine) lastHeroineOrder = string.Join(",", new List<string>(((Dictionary<string, int>)Get(c, "order")).Keys).ToArray());
                var rt = (RectTransform)Get(c, "rt");
                if (rt == null) continue;
                var bubble = (RectTransform)Get(c, "bubble");
                if (bubble != null) CheckInside("장사", bubble, stage, 30f, heroine ? "리나 주문 말풍선" : "손님 주문 말풍선");
                var speech = rt.Find("Speech");
                if (speech != null)
                {
                    var t = speech.GetComponentInChildren<Text>();
                    if (t != null && seenSpeech.Add(speech)) Record(t.text);
                    CheckInside("장사", (RectTransform)speech, stage, 0f, "손님 대사 말풍선");
                }
            }
        }

        static void CheckInside(string shot, RectTransform rt, RectTransform stage, float minTop, string what)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 a = stage.InverseTransformPoint(corners[0]), b = stage.InverseTransformPoint(corners[2]);
            var r = stage.rect;
            float left = a.x - r.xMin, right = b.x - r.xMin, top = r.yMax - b.y, bottom = r.yMax - a.y;
            if (left < -1 || right > Game.W + 1 || top < minTop - 1 || bottom > Game.H + 1)
                AddLayout(shot, what + (top < minTop ? " (상단 HUD 영역 침범)" : " (화면 밖)"), PathOf(rt, stage), "",
                    string.Format("x {0:0}~{1:0}, y {2:0}~{3:0}", left, right, top, bottom));
        }

        // ───────────────────────── 노래방 봇 ─────────────────────────

        static IEnumerator KaraokeBot(bool shots)
        {
            yield return WaitScreen<KaraokeScreen>();
            var k = Cur;
            yield return Pump();
            yield return new Until(() => (bool)Get(k, "playing"), 10f, "노래 시작");
            var notesList = (IList)Get(k, "notes");
            var rnd = new System.Random(5);
            var skip = new HashSet<object>();
            bool shot = !shots;
            while (!(bool)Get(k, "finished"))
            {
                float clock = (float)Get(k, "clock");
                foreach (var n in notesList)
                {
                    if ((bool)Get(n, "done") || skip.Contains(n)) continue;
                    float dt = (float)Get(n, "time") - clock;
                    if (dt > 0.03f || dt < -0.12f) continue;
                    if (rnd.NextDouble() < 0.9) Call(k, "Hit", (int)Get(n, "lane"));
                    else skip.Add(n);
                }
                if (!shot && clock > 8f) { shot = true; yield return Shot("karaoke"); }
                yield return null;
            }
            yield return new Until(() => FindButton("계속") != null, 10f, "노래방 결과창");
            if (shots) yield return Shot("karaoke_result");
            Click("계속");
            yield return 0.3f;
            yield return Pump();
        }

        // ───────────────────────── 대화 ─────────────────────────

        static bool WaitingChoice
        {
            get
            {
                foreach (var d in Game.I.Stage.GetComponentsInChildren<DialogueBox>(false))
                    if ((bool)Get(d, "waitingChoice")) return true;
                return false;
            }
        }

        /// <summary>대화창을 끝까지 넘긴다 (선택지가 나오면 멈춤)</summary>
        static IEnumerator Pump(float timeout = 60f)
        {
            double end = EditorApplication.timeSinceStartup + timeout;
            while (EditorApplication.timeSinceStartup < end)
            {
                bool busy = false;
                foreach (var d in Game.I.Stage.GetComponentsInChildren<DialogueBox>(false))
                {
                    Record(Get(d, "fullText") as string);
                    if ((bool)Get(d, "waitingChoice")) yield break;
                    if (d.Busy) { busy = true; d.Advance(); }
                }
                if (!busy) yield break;
                yield return 0.04f;
            }
            Fail("대화 진행 시간 초과");
        }

        static IEnumerator Pick(string label)
        {
            yield return Pump();
            yield return new Until(() => FindButton(label) != null, 8f, "선택지 " + label);
            Click(label);
            yield return 0.2f;
            yield return Pump();
        }

        static void Record(string text)
        {
            if (string.IsNullOrEmpty(text) || text == lastLine) return;
            lastLine = text;
            spoken.Add(text);
            transcript.Add(step + " | " + text);
        }

        static bool Said(Line[] lines)
        {
            if (lines == null || lines.Length == 0) return false;
            foreach (var l in lines) if (!spoken.Contains(l.text.Replace("{name}", Rina.name))) return false;
            return true;
        }

        // ───────────────────────── UI 조작 ─────────────────────────

        static GameState S { get { return Game.S; } }
        static HeroineDef Rina { get { return GameData.Rina; } }
        static ScreenBase Cur { get { return Game.I == null ? null : Get(Game.I, "current") as ScreenBase; } }
        static string CurName { get { var c = Cur; return c != null ? c.GetType().Name : "-"; } }
        static bool Transitioning { get { return (bool)Get(Game.I, "transitioning"); } }
        static string HomeHint { get { var t = Cur is HomeScreen ? Get(Cur, "hintText") as Text : null; return t != null ? t.text : "(홈 화면 아님: " + CurName + ")"; } }
        static bool BadgeOn { get { var i = Cur is HomeScreen ? Get(Cur, "badgeBg") as Image : null; return i != null && i.gameObject.activeSelf; } }

        static string PackLabel(string id)
        {
            var ing = GameData.Ingredient(id);
            return ing.packSize + "개  " + UIKit.Yen(ing.packPrice);
        }

        static IEnumerator WaitScreen<T>(ScreenBase notThis = null) where T : ScreenBase
        {
            yield return new Until(() => Cur is T && Cur != notThis && !Transitioning, 30f, typeof(T).Name + " 화면 (현재 " + CurName + ")");
            yield return 0.45f;
        }

        static bool Click(string label)
        {
            var b = FindButton(label);
            if (b == null) { Fail("버튼 없음: '" + label + "' (화면 " + CurName + ")"); return false; }
            b.onClick.Invoke();
            return true;
        }

        /// <summary>UIKit.Button("Button_라벨") 또는 글자가 그대로 적힌 클릭 영역(핫스팟 팻말, 지도 노드). 위에 뜬 팝업부터 찾는다.</summary>
        static Button FindButton(string label)
        {
            var all = Game.I.Stage.GetComponentsInChildren<Button>(false);
            for (int pass = 0; pass < 3; pass++)
                for (int i = all.Length - 1; i >= 0; i--)
                {
                    var b = all[i];
                    if (!b.isActiveAndEnabled || !b.interactable) continue;
                    string n = b.gameObject.name;
                    if (pass == 0 && n == "Button_" + label) return b;
                    if (pass == 1 && n.StartsWith("Button_") && n.Contains(label)) return b;
                    if (pass == 2 && !n.StartsWith("Button_"))
                        for (int c = 0; c < b.transform.childCount; c++)
                        {
                            var t = b.transform.GetChild(c).GetComponent<Text>();
                            if (t != null && t.text == label) return b;
                        }
                }
            return null;
        }

        static Button FirstPhoneChoice()
        {
            foreach (var b in Game.I.PopupLayer.GetComponentsInChildren<Button>(false))
                if (b.gameObject.name.StartsWith("Button_[")) return b;
            return null;
        }

        static Text FindText(string text)
        {
            foreach (var t in Game.I.Stage.GetComponentsInChildren<Text>(false)) if (t.text == text) return t;
            return null;
        }

        // ───────────────────────── 스크린샷 · 레이아웃 검사 ─────────────────────────

        static IEnumerator Shot(string name)
        {
            string file = string.Format("{0:00}_{1}.png", ++shotIndex, name);
            var canvas = Game.I.Stage.GetComponentInParent<Canvas>().rootCanvas;
            var scaler = canvas.GetComponent<CanvasScaler>();
            var go = new GameObject("SmokeShotCam");
            var cam = go.AddComponent<Camera>();   // 꺼진 카메라에 붙은 캔버스는 그려지지 않으므로 켜 둔 채 RT 로만 렌더
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = ~0;   // 코드로 만든 Canvas 오브젝트는 Default 레이어
            cam.orthographic = true;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10;
            canvas.scaleFactor = 2f;
            Canvas.ForceUpdateCanvases();
            yield return 0.12f;
            try
            {
                canvas.scaleFactor = 2f;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                scaler.enabled = true;
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(go);
            }
            Audit(file);
        }

        static void Audit(string shot)
        {
            var stage = Game.I.Stage;
            foreach (var t in stage.GetComponentsInChildren<Text>(false))
            {
                if (!t.enabled || string.IsNullOrEmpty(t.text) || t.color.a < 0.1f) continue;
                if (t.GetComponentInParent<FloatUp>() != null || t.GetComponentInParent<RectMask2D>() != stage.GetComponent<RectMask2D>()) continue;
                var rt = t.rectTransform;
                float ph = t.preferredHeight, h = rt.rect.height;
                if (ph > h + 4 && ph > h * 1.3f)
                    AddLayout(shot, "텍스트 넘침(세로)", PathOf(rt, stage), t.text, string.Format("필요 {0:0}px / 칸 {1:0}px", ph, h));
                CheckInside(shot, rt, stage, 0f, "텍스트");
            }
            foreach (var b in stage.GetComponentsInChildren<Button>(false))
                if (b.gameObject.name.StartsWith("Button_")) CheckInside(shot, (RectTransform)b.transform, stage, 0f, "버튼");
        }

        static void AddLayout(string shot, string kind, string path, string text, string detail)
        {
            string key = kind + path + text;
            if (!layoutSeen.Add(key)) return;
            layout.Add(string.Format("{0} · {1} · {2}{3} · {4}", shot, kind, path, string.IsNullOrEmpty(text) ? "" : " \"" + Snip(text) + "\"", detail));
        }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var p = t; p != null && p != root; p = p.parent) parts.Insert(0, p.name);
            return string.Join("/", parts.ToArray());
        }

        // ───────────────────────── 기록 ─────────────────────────

        static void Step(string s) { step = s; }

        static void Check(string name, bool ok, string detail = null)
        {
            checks.Add((ok ? "PASS  " : "FAIL  ") + name + (detail != null ? "   [" + Snip(detail) + "]" : ""));
            if (!ok) failCount++;
        }

        static void Fail(string msg)
        {
            checks.Add("FAIL  " + msg + "   @" + step);
            failCount++;
        }

        static void Note(string msg) { notes.Add(step + " | " + msg); }
        static void Manip(string msg) { notes.Add(step + " | [테스트 조작] " + msg); }

        static void WriteReport(int failed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("붕어빵 연애 타이쿤 — Play 모드 스모크 테스트");
            sb.AppendLine("Unity " + Application.unityVersion + " · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · " + (EditorApplication.timeSinceStartup - startTime).ToString("0") + "초");
            sb.AppendLine(failed == 0 ? "결과: 통과" : "결과: 실패 " + failed + "건 (체크 실패 " + failCount + ", 에러/[Art] 로그 " + errors.Count + ")");
            if (S != null) sb.AppendLine(string.Format("마지막 상태: W{0} {1} · {2} · 보유금 {3} · 호감도 {4} ({5})", S.week, GameState.DayKo[S.day], CurName, UIKit.Yen(S.money), S.affection, S.StageName(Rina)));
            Section(sb, "체크", checks);
            Section(sb, "에러 · 예외 · [Art] 경고 로그", errors);
            Section(sb, "기타 경고 로그", warnings);
            Section(sb, "레이아웃 (640×360 밖 / 넘치는 텍스트)", layout);
            Section(sb, "하루 장사 기록 (W1 = 사람 속도 봇, W2~ = 빠른 봇)", balance);
            Section(sb, "메모", notes);
            Section(sb, "대사 기록", transcript);
            File.WriteAllText(Path.Combine(OutDir, "report.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Smoke] " + (failed == 0 ? "통과" : "실패 " + failed + "건") + " — " + Path.Combine(OutDir, "report.txt") + "\n" + string.Join("\n", checks.ToArray()));
        }

        static void Section(StringBuilder sb, string title, List<string> lines)
        {
            sb.AppendLine().AppendLine("── " + title + " (" + lines.Count + ")");
            foreach (var l in lines) sb.AppendLine("  " + l);
        }

        static string Snip(string s)
        {
            s = s.Replace("\n", "⏎");
            return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
        }

        static string FirstLines(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var lines = s.Split('\n');
            return string.Join("\n", lines, 0, Math.Min(n, lines.Length)).TrimEnd();
        }

        // ───────────────────────── 리플렉션 ─────────────────────────

        static FieldInfo Field(Type t, string name)
        {
            for (; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BF);
                if (f != null) return f;
            }
            throw new MissingFieldException(name);
        }

        static object Get(object o, string name) { return Field(o.GetType(), name).GetValue(o); }
        static void Set(object o, string name, object v) { Field(o.GetType(), name).SetValue(o, v); }

        static object Call(object o, string name, params object[] args)
        {
            var m = o.GetType().GetMethod(name, BF);
            if (m == null) throw new MissingMethodException(o.GetType().Name, name);
            return m.Invoke(o, args);
        }
    }
}
