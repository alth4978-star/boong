using System;
using System.Collections.Generic;
using UnityEngine;

namespace Taiyaki
{
    public enum DayPlan { Work, Rest, Club, Free }

    [Serializable]
    public class InboxMsg
    {
        public string heroine;
        public string id;
        public int week;
        public int day;
        public bool read;
        public int answer = -1;    // -1 답장 대기 · -2 답장 안 함(다음 메시지가 옴) · 0~ 고른 답장

        public const int Pending = -1, Skipped = -2;
    }

    [Serializable]
    public class DayResult
    {
        public int sold;
        public int perfect;
        public int raw;
        public int burnt;
        public int missed;
        public int revenue;
        public int tips;
        public int bestCombo;
        public int wasted;
        public bool heroineVisited;
        public bool heroineServed;
        public bool packedGift;
    }

    [Serializable]
    public class GameState
    {
        public int week = 1;
        public int day;            // 0=MON ... 6=SUN
        public int phase;          // 0=아침/낮  1=밤(활동 종료)
        public int money;
        public int weekStartMoney;
        public int weekSales;
        public int weekSpent;
        public int totalSold;
        public int stamina;
        public int molds;
        public int stallLevel = 1;

        public int batter, red, cream, strawberry;

        public List<string> plan = new List<string>();
        public List<string> flags = new List<string>();
        public List<string> weekFlags = new List<string>();
        public List<string> unlockedFillings = new List<string>();
        public List<InboxMsg> inbox = new List<InboxMsg>();

        public int affection;      // 숨겨진 호감도 0~100 (UI에는 관계 단계만 표시)
        public int heroineVisitDay = -1;  // 이번 주 리나가 노점에 오는 요일
        public int giftTaiyaki;    // 포장해 둔 직접 구운 붕어빵
        public bool giftTaiyakiPerfect;
        public string giftTaiyakiFilling = "red";
        public List<string> items = new List<string>();  // 쇼핑거리에서 산 선물

        public int bestCombo;
        public DayResult lastResult = new DayResult();

        // ───────── 달력 ─────────
        public static readonly string[] DayEn = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
        public static readonly string[] DayKo = { "월", "화", "수", "목", "금", "토", "일" };

        public int DayNumber { get { return (week - 1) * 7 + day + 1; } }
        public bool IsWeekend { get { return day >= 5; } }
        public bool IsNight { get { return phase == 1; } }

        public DayPlan PlanOf(int d)
        {
            if (d == 5) return DayPlan.Club;
            if (d == 6) return DayPlan.Free;
            if (plan == null || d >= plan.Count) return DayPlan.Work;
            return plan[d] == "rest" ? DayPlan.Rest : DayPlan.Work;
        }

        public DayPlan Today { get { return PlanOf(day); } }

        public static string PlanLabel(DayPlan p)
        {
            switch (p)
            {
                case DayPlan.Work: return "장사";
                case DayPlan.Rest: return "휴식";
                case DayPlan.Club: return "클럽";
                default: return "자유";
            }
        }

        // ───────── 플래그 ─────────
        public bool Has(string f) { return !string.IsNullOrEmpty(f) && (flags.Contains(f) || weekFlags.Contains(f)); }
        public void Set(string f) { if (!string.IsNullOrEmpty(f) && !flags.Contains(f)) flags.Add(f); }
        public void SetWeek(string f) { if (!string.IsNullOrEmpty(f) && !weekFlags.Contains(f)) weekFlags.Add(f); }
        public void Unset(string f) { flags.Remove(f); weekFlags.Remove(f); }

        // ───────── 재고 ─────────
        public int Stock(string id)
        {
            switch (id)
            {
                case "batter": return batter;
                case "red": return red;
                case "cream": return cream;
                case "strawberry": return strawberry;
            }
            return 0;
        }

        public void AddStock(string id, int n)
        {
            switch (id)
            {
                case "batter": batter = Mathf.Max(0, batter + n); break;
                case "red": red = Mathf.Max(0, red + n); break;
                case "cream": cream = Mathf.Max(0, cream + n); break;
                case "strawberry": strawberry = Mathf.Max(0, strawberry + n); break;
            }
        }

        public bool IsUnlocked(string fillingId) { return unlockedFillings.Contains(fillingId); }

        /// <summary>지금 재고로 구울 수 있는 붕어빵 개수 (반죽 1 + 속 1)</summary>
        public int BakeableCount
        {
            get
            {
                int fillings = 0;
                foreach (var f in unlockedFillings) fillings += Stock(f);
                return Math.Min(batter, fillings);
            }
        }

        public const int DailyStockTarget = 20;   // 하루 장사에 20~30개 정도 팔린다

        // ───────── 돈 ─────────
        public bool Spend(int amount)
        {
            if (money < amount) return false;
            money -= amount;
            weekSpent += amount;
            return true;
        }

        public void Earn(int amount)
        {
            money += amount;
            weekSales += amount;
        }

        // ───────── 관계 ─────────
        public int Stage(HeroineDef h)
        {
            int s = 0;
            if (h.stageThresholds == null) return 0;
            for (int i = 0; i < h.stageThresholds.Length; i++)
                if (affection >= h.stageThresholds[i]) s = i;
            return s;
        }

        public string StageName(HeroineDef h)
        {
            int s = Stage(h);
            if (h.stageNames == null || h.stageNames.Length == 0) return "";
            return h.stageNames[Mathf.Clamp(s, 0, h.stageNames.Length - 1)];
        }

        public void AddAffection(int v) { affection = Mathf.Clamp(affection + v, 0, 100); }

        // ───────── 메시지 ─────────
        public InboxMsg Deliver(string heroine, string id)
        {
            foreach (var m in inbox) if (m.id == id && m.week == week) return m;
            // 새 메시지가 오면 답장하지 않은 이전 메시지는 지나간 대화가 된다 (답장 불가 → 알림에서 제외)
            foreach (var m in inbox) if (m.heroine == heroine && m.answer == InboxMsg.Pending) m.answer = InboxMsg.Skipped;
            var msg = new InboxMsg { heroine = heroine, id = id, week = week, day = day };
            inbox.Add(msg);
            return msg;
        }

        public int UnreadCount
        {
            get
            {
                int n = 0;
                foreach (var m in inbox)
                {
                    // 데이터에서 사라진 메시지(옛 세이브)나 선택지 없는 메시지는 폰에 안 뜨거나 답장할 수 없으니 배지에서 뺀다
                    var def = GameData.Message(GameData.Heroine(m.heroine), m.id);
                    if (def == null) continue;
                    bool canAnswer = m.answer == InboxMsg.Pending && def.choices != null && def.choices.Length > 0;
                    if (!m.read || canAnswer) n++;
                }
                return n;
            }
        }

        public static GameState NewGame()
        {
            var e = GameData.Eco;
            var s = new GameState
            {
                money = e.startMoney,
                weekStartMoney = e.startMoney,
                stamina = e.maxStamina,
                batter = e.startBatter,
                red = e.startRed,
                cream = e.startCream,
                molds = e.startMolds,
            };
            s.plan = new List<string> { "work", "work", "work", "rest", "work" };
            foreach (var f in GameData.Root.fillings) if (f.startUnlocked) s.unlockedFillings.Add(f.id);
            return s;
        }
    }

    public static class SaveSystem
    {
        const string Key = "taiyaki_romance_save_v1";

        public static bool HasSave { get { return PlayerPrefs.HasKey(Key); } }

        public static void Save(GameState s)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        public static GameState Load()
        {
            if (!HasSave) return null;
            try { return JsonUtility.FromJson<GameState>(PlayerPrefs.GetString(Key)); }
            catch (Exception e) { Debug.LogWarning("세이브 로드 실패: " + e.Message); return null; }
        }

        public static void Delete() { PlayerPrefs.DeleteKey(Key); }
    }
}
