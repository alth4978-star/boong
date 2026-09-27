using System;
using System.Collections.Generic;
using UnityEngine;

namespace Taiyaki
{
    // ─────────────────────────────────────────────────────────────
    //  데이터 정의 — Resources/Data/*.json 에서 불러온다.
    //  히로인 / 메뉴 / 손님 / 장비는 JSON만 추가·수정해서 확장할 수 있다.
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class EconomyDef
    {
        public int startMoney = 8000;
        public int startBatter = 12;
        public int startRed = 8;
        public int startCream = 8;
        public int perfectBonus = 100;
        public float rawPriceRate = 0.5f;
        public int maxStamina = 5;
        public int workStaminaCost = 1;
        public int restStaminaGain = 2;
        public float shopDaySeconds = 150f;
        public int shopOpenHour = 14;
        public int shopCloseHour = 20;
        public int weeklyGoal = 18000;
        public int clubEntry = 5000;
        public int karaokePrice = 4000;
        public int rackCapacity = 8;
        public int startMolds = 2;
        public int maxQueue = 4;
        public float cookSideA = 2.6f;   // 아랫면이 익는 데 걸리는 시간(초)
        public float cookSideB = 3.2f;   // 뒤집은 뒤 PERFECT 까지 걸리는 시간(초)
        public float tiredPatienceRate = 0.85f;
    }

    [Serializable]
    public class FillingDef
    {
        public string id;          // red, cream, strawberry ...
        public string name;        // 팥
        public int price;          // 판매가
        public string sprite;      // 스프라이트 접미사 (bowl_red, mold_red, bubble_red)
        public string tint;        // 스프라이트 틴트 (#RRGGBB, 빈 값이면 원색)
        public string dotColor;    // 보온대 표시용 색
        public bool startUnlocked;
        public int weight;         // 주문 빈도 가중치
        public int researchPrice;  // 연구 비용 (startUnlocked=false 일 때)
        public string requireFlag; // 연구 해금 조건 플래그 (히로인 대화 등)
        public string researchDesc;
    }

    [Serializable]
    public class IngredientDef
    {
        public string id;       // batter, red, cream, strawberry
        public string name;
        public int packSize;
        public int packPrice;
        public string sprite;
    }

    [Serializable]
    public class CustomerDef
    {
        public string id;
        public string name;
        public string sprite;
        public int minItems;
        public int maxItems;
        public float patience;
        public int weight;
        public float tipRate;
        public string[] orderLines;
        public string[] happyLines;
        public string[] angryLines;
    }

    [Serializable]
    public class UpgradeDef
    {
        public string id;
        public string name;
        public string desc;
        public int price;
        public int molds;      // 적용 후 틀 개수
        public int level;      // 가게 레벨
    }

    [Serializable]
    public class GiftDef
    {
        public string id;
        public string name;
        public string desc;
        public int price;
        public string sprite;
    }

    [Serializable]
    public class StallLevelDef
    {
        public int level;
        public string desc;
    }

    [Serializable]
    public class GameDataRoot
    {
        public EconomyDef economy;
        public FillingDef[] fillings;
        public IngredientDef[] ingredients;
        public CustomerDef[] customers;
        public UpgradeDef[] upgrades;
        public GiftDef[] gifts;
        public StallLevelDef[] stallLevels;
        public string[] prologue;
    }

    // ───────── 히로인 ─────────

    [Serializable]
    public class Line
    {
        public string who;   // "rina" / "hero" / "" (나레이션)
        public string text;
        public string expr;  // normal smile embarrassed drunk pout serious
    }

    [Serializable]
    public class Choice
    {
        public string text;
        public int affection;
        public string flag;
        public Line[] reply;
    }

    [Serializable]
    public class Topic
    {
        public string id;
        public string title;
        public int minDrunk;
        public int maxDrunk = 9;
        public int minAffection;
        public string requireFlag;
        public string grantFlag;
        public Line[] lines;
        public Choice[] choices;
    }

    [Serializable]
    public class DrinkDef
    {
        public string id;
        public string name;
        public string desc;
        public int price;
        public int drunk;
        public int affection;
        public Line[] lines;
    }

    [Serializable]
    public class PhoneMsg
    {
        public string id;
        public string[] texts;
        public Choice[] choices;
    }

    [Serializable]
    public class HeroineDef
    {
        public string id;
        public string name;
        public string nameEn;
        public string job;
        public string profile;
        public string themeMain;
        public string themeAccent;
        public string sprite;
        public string smallSprite;
        public int tolerance;          // 이 이상 취하면 '위험'
        public string[] stageNames;
        public int[] stageThresholds;
        public DrinkDef[] drinks;
        public DrinkDef[] snacks;
        public Topic[] topics;

        public Line[] firstMeet;
        public Line[] greetHigh;
        public Line[] greetNormal;
        public Line[] memoryPerfect;
        public Line[] memoryVisited;
        public Line[] memoryMissed;
        public Line[] promiseSatKept;        // 문자로 "꼭 갈게" 약속 후 클럽에 왔을 때
        public Line[] promiseRedKept;        // "팥붕 가져올게" 약속 후 팥 붕어빵을 선물했을 때
        public Line[] promiseStrawberryKept; // "제일 먼저 먹게 해 줄게" 약속 후 딸기 크림을 건넸을 때
        public Line[] shopPromiseStrawberry; // 같은 약속을 노점에서 지켰을 때 (짧은 말풍선용)
        public Line[] dreamRecall;           // 카페 동업 얘기를 기억해 둔 다음 주 클럽
        public Line[] shopVisitFirst;
        public Line[] shopVisitAgain;
        public Line[] shopServed;
        public Line[] shopServedPerfect;
        public Line[] shopLeft;
        public Line[] drunkWarn;
        public Line[] goHomeDrunk;
        public Line[] karaokeAsk;
        public Line[] karaokeGreat;
        public Line[] karaokeOk;
        public Line[] karaokeBad;
        public Line[] giftTaiyaki;
        public Line[] giftTaiyakiPerfect;
        public Line[] giftScarf;
        public Line[] giftFlower;
        public Line[] gameIntro;
        public Line[] gameWin;
        public Line[] gameLose;
        public Line[] timeUp;
        public Line[] farewell;
        public Line[] noMoney;
        public Line[] dateIntro;
        public Line[] dateCg;
        public Line[] dateOutro;
        public PhoneMsg[] messages;
        public string[] lyrics;
    }

    public static class GameData
    {
        public static GameDataRoot Root;
        public static HeroineDef Rina;
        static readonly Dictionary<string, HeroineDef> heroines = new Dictionary<string, HeroineDef>();

        public static EconomyDef Eco { get { return Root.economy; } }

        public static void Load()
        {
            if (Root != null) return;
            var ta = Resources.Load<TextAsset>("Data/game");
            Root = ta != null ? JsonUtility.FromJson<GameDataRoot>(ta.text) : new GameDataRoot();
            if (Root.economy == null) Root.economy = new EconomyDef();
            if (Root.fillings == null) Root.fillings = new FillingDef[0];
            if (Root.ingredients == null) Root.ingredients = new IngredientDef[0];
            if (Root.customers == null) Root.customers = new CustomerDef[0];
            if (Root.upgrades == null) Root.upgrades = new UpgradeDef[0];
            if (Root.gifts == null) Root.gifts = new GiftDef[0];
            if (Root.stallLevels == null) Root.stallLevels = new StallLevelDef[0];
            if (Root.prologue == null) Root.prologue = new string[0];

            var h = Resources.Load<TextAsset>("Data/heroine_rina");
            Rina = h != null ? JsonUtility.FromJson<HeroineDef>(h.text) : new HeroineDef { id = "rina", name = "리나" };
            heroines[Rina.id] = Rina;
        }

        public static HeroineDef Heroine(string id)
        {
            HeroineDef d;
            return heroines.TryGetValue(id, out d) ? d : Rina;
        }

        public static FillingDef Filling(string id)
        {
            foreach (var f in Root.fillings) if (f.id == id) return f;
            return null;
        }

        public static IngredientDef Ingredient(string id)
        {
            foreach (var i in Root.ingredients) if (i.id == id) return i;
            return null;
        }

        public static GiftDef Gift(string id)
        {
            foreach (var g in Root.gifts) if (g.id == id) return g;
            return null;
        }

        public static CustomerDef Customer(string id)
        {
            foreach (var c in Root.customers) if (c.id == id) return c;
            return null;
        }

        public static PhoneMsg Message(HeroineDef h, string id)
        {
            if (h.messages == null) return null;
            foreach (var m in h.messages) if (m.id == id) return m;
            return null;
        }

        public static Color Hex(string hex, Color fallback)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return fallback;
        }
    }
}
