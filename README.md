# 붕어빵 팔아서 그녀 보러 간다 — TAIYAKI ROMANCE TYCOON

> 평일에는 붕어빵 장사, 주말에는 네온 번화가에서 만나는 연애 타이쿤
> **Unity 프로토타입 v0.1 — 게임 속 1주일**

기획서 「PIXEL GAME SCREEN DESIGN」의 **19. 첫 프로토타입 범위**를 그대로 구현했습니다.

```
월요일 주간 계획 → 붕어빵 장사 3일 → 하루 휴식 → 금요일 장사
→ 토요일 클럽 (히로인 1명과 술자리) → 노래방 → 일요일 데이트 이벤트 → 주간 결산 → 다음 주 월요일
```

- 히로인 **1명 (리나)**, 붕어빵 **팥 / 슈크림 2종**, 업그레이드 **붕어빵 틀 +1** 하나
- 1주가 끝나도 같은 사이클로 계속 플레이할 수 있습니다 (2주차부터 딸기 크림 연구 가능).

---

## 실행 방법

1. **Unity Hub**에서 `Add → Add project from disk`로 이 폴더를 추가합니다.
   - 권장 버전: **Unity 2022.3 LTS** (Unity 6에서도 열립니다. 버전이 다르면 Hub가 업그레이드를 물어봅니다.)
2. 처음 열면 `Assets/Scenes/Main.unity`가 자동으로 열립니다. (안 열리면 메뉴 **Taiyaki → Open Main Scene**)
3. **▶ Play**를 누르면 끝! 씬에 아무것도 배치할 필요가 없습니다 — 게임이 코드로 스스로 화면을 구성합니다.
4. 빌드: `File → Build Settings`에 Main 씬이 이미 등록되어 있습니다.

> 이미지가 흐리거나 뭉개져 보이면 메뉴 **Taiyaki → Reimport Art** 를 한 번 실행하세요.
> 세이브 초기화: **Taiyaki → Delete Save Data**

### 자동 스모크 테스트

메뉴 **Taiyaki → Run Smoke Test** 를 누르면 Play 모드에서 봇이 실제 버튼을 눌러 게임 속 3주를 진행합니다 (약 10분).
런타임 예외 · `[Art] 이미지 없음` 경고 · 약속 대사 회수 · 640×360 밖으로 나가거나 넘치는 텍스트를 검사하고,
결과를 `Logs/SmokeTest/report.txt`와 화면별 스크린샷(`*.png`)으로 남깁니다. 세이브 데이터는 끝나면 원래대로 돌려놓습니다.

```
Unity.exe -batchmode -projectPath <이 폴더> -executeMethod Taiyaki.EditorTools.SmokeTest.RunBatch -logFile smoke.log
```

배치 모드 종료 코드: 0 = 통과, 1 = 실패. 사람이 직접 봐야 하는 항목은 [PLAYTEST.md](PLAYTEST.md)에 있습니다.

## 조작

| 화면 | 조작 |
|---|---|
| 공통 | 마우스 클릭. 대화는 클릭 / `Space` / `Enter`로 넘김 |
| 붕어빵 장사 | 틀 클릭 = **반죽 붓기 → 속 넣기 → 뒤집기 → 꺼내기**, 손님 클릭 = 판매 |
| | 단축키: `1` `2` `3` 틀 · `Q` `W` `E` 속 선택 · `Space` 가장 급한 손님에게 판매 · `Esc` 일시정지 |
| 노래방 | ♥ = `F`(또는 왼쪽 버튼) · ★ = `J`(또는 오른쪽 버튼) |

### 붕어빵 굽기 판정
- 속을 넣으면 아랫면이 익기 시작 → 막대가 **초록**이 되면 뒤집기 (너무 오래 두면 탐)
- 뒤집은 뒤 막대: 회색(설익음) → 연두(GOOD) → **금색(PERFECT, 판매가 보너스)** → 주황(진함) → 빨강(탐)
- 꺼낸 붕어빵은 **보온대**에 쌓이고, 손님을 누르면 주문만큼 꺼내 팝니다 (PERFECT 우선).
- 연속 판매 시 **COMBO** 팁. 설익은 걸 팔면 콤보가 끊깁니다. 보온대의 붕어빵은 눌러서 버릴 수 있어요.
- 장사가 끝나면 남은 붕어빵 중 가장 잘 구운 하나를 **포장** → 주말에 그녀에게 선물할 수 있습니다.

---

## 구현된 화면 (기획서 핵심 화면 6장 + α)

| 기획서 | 구현 | 파일 |
|---|---|---|
| 1. 메인 홈 (원룸) | 스마트폰(메시지) / 냉장고(재료) / 책상(연구·업그레이드) / 침대(하루 종료) / 현관(외출). 창밖 눈, 낮·밤 조명, **플레이 기록이 방에 쌓임**(재료상자·등불·노래방 간판·데이트 사진) | `Screens/HomeScreen.cs` |
| 2. 주간 일정 | 월~금 장사/휴식 토글, 예상 체력, 이번 주 목표·필요 예상금액 | `Screens/MetaScreens.cs` |
| 3. 붕어빵 장사 | 틀 2개(업그레이드 3개), 판정, 보온대, 콤보, 저녁이 되면 **조명이 하나씩 켜짐**, 지붕에 **눈이 쌓임**, 김·반짝이·연기 연출 | `Screens/ShopScreen.cs` |
| 4. 손님 시스템 | 학생·회사원·아이와 부모·술 취한 아저씨·커플·단골 할머니 + **히로인이 사복으로 방문** | `ShopScreen.cs`, `Data/game.json` |
| 5. 재료 / 메뉴 | 팥·슈크림 + 대화에서 발견하는 **딸기 크림** 레시피 연구, 초코·치즈는 LOCK | `HomeScreen.cs (DeskPopup)` |
| 6. 가게 업그레이드 | 붕어빵 틀 +1 (LV1→LV2), 이후 레벨은 “준비 중” 표시 | `HomeScreen.cs (DeskPopup)` |
| 7. 도시 지도 | 집·역앞·노점·쇼핑거리·번화가·클럽·노래방. 낮/평일 밤/주말 네온 조명이 모두 다름, 네온이 순서대로 켜짐 | `Screens/MapScreen.cs` |
| 8. 주말 번화가 | 네온 UI + BGM 전환, 히로인 선택 (RINA 출근 / MIKA / YUU 휴무) | `MapScreen.cs (StreetScreen)` |
| 9. 클럽 내부 | 대형 도트 히로인, 비주얼노벨 대화창, 테이블 메뉴(술·안주·선물·대화·게임·2차·귀가) | `Screens/ClubScreen.cs` |
| 10. 술 시스템 | 히로인별 주량·취향, 수치 대신 **표정·볼터치·자세 흐트러짐**으로 멀쩡→살짝 취함→취함→위험, 과음 시 먼저 귀가 | `ClubScreen.cs`, `UI/Widgets.cs (HeroineView)` |
| 11. 노래방 | 약 40초 박자 입력 미니게임, 가사, 랭크에 따라 특별 대사 + 호감도 | `Screens/KaraokeScreen.cs` |
| 12. 데이트 이벤트 | 일요일 겨울 축제 → **풀 일러스트 CG 전환** (그녀가 주인공의 붕어빵을 처음 먹는 장면) | `KaraokeScreen.cs (DateScreen)` |
| 13. 메시지 | 스마트폰 채팅, 답장 → 호감도 + **다음 약속** (“내일 와” → 다음 장사 날 방문, 일요일 축제 약속) | `UI/PhonePopup.cs` |
| 14. 호감도 UI | 숫자 없이 관계 단계만: 처음 본 손님 → 낯익은 손님 → 친한 손님 → 친구 → 특별한 사람 → 연인 | `GameState.cs` |
| 15. 화면 전환 흐름 | HOME → 장사 → 정산 → 업그레이드/쇼핑 → 토요일 밤 → 클럽 → 노래방 → 일요일 데이트 → 월요일 | `Core/Game.cs` |
| 16. 히로인별 UI 테마 | 리나 = 와인 레드 + 금색 테두리 (대화창/메시지 헤더) | `Data/heroine_rina.json` |
| 18. 살리고 싶은 연출 | 김, 눈 쌓임, 조명 점등, 네온 순차 점등 + BGM 전환, 취하면 자세 흐트러짐, 호감도 높으면 **먼저 손 흔들기**, 친해지면 **노점에 더 자주 찾아옴** | 전반 |

**타이쿤 ↔ 연애가 서로 기억합니다**
- 장사 중 리나에게 PERFECT 붕어빵을 팔면 → 클럽에서 “지난번 붕어빵 맛있었어.”
- 클럽에서 살짝 취한 리나가 “딸기 크림 붕어빵 같은 거 있으면 귀엽겠다.” → 책상에서 **딸기 크림 레시피 연구** 해금
- 쉬는 날 “오늘 붕어빵 먹으러 갔는데 너 없더라 ㅋㅋ” → “내일 와” 라고 답하면 다음 장사 날 찾아옴

---

## 프로젝트 구조

```
Assets/
  Scenes/Main.unity              빈 씬 (Game.cs가 자동 부팅)
  Scripts/
    Core/  Game.cs               부팅·640×360 스테이지·화면 전환·하루 진행·메시지 배달
           GameState.cs          세이브 데이터(돈·재고·체력·호감도·플래그) + PlayerPrefs 저장
           GameData.cs           JSON 데이터 정의/로더
           Art.cs                스프라이트/폰트 로딩, 팔레트
           UIKit.cs              코드로 UGUI 조립하는 헬퍼 + 연출 컴포넌트(눈, 흔들림, 떠오르는 텍스트…)
           Sfx.cs                효과음·BGM 코드 합성 (평일/밤/주말/노래방/데이트)
    UI/    Widgets.cs            HUD, 모달, 비주얼노벨 대화창, 히로인 스탠딩
           PhonePopup.cs         스마트폰 메시지
    Screens/                     각 화면
    Editor/ArtImportSettings.cs  이미지 임포트 자동 설정 + Taiyaki 메뉴
           SmokeTest.cs          Play 모드 자동 스모크 테스트 (Taiyaki → Run Smoke Test)
  Resources/
    Data/game.json               경제 수치·메뉴·재료·손님·업그레이드·선물·프롤로그
    Data/heroine_rina.json       리나의 모든 대사·술/안주·대화 주제·메시지·가사
    Art/                         배경·스프라이트·UI·캐릭터 (첨부 이미지에서 잘라낸 것)
    Fonts/                       Pretendard(본문), Galmuri11(도트 제목/숫자) — 둘 다 OFL
```

해상도: 내부 **640×360** (16:9) 고정 스테이지를 화면에 맞춰 확대, 남는 영역은 레터박스.

## 확장 가이드

- **밸런스 조정**: `game.json`의 `economy` (시작 자금, 장사 시간, 굽는 속도 `cookSideA/B`, 목표 금액, 입장료…)
- **메뉴 추가**: `fillings` + `ingredients`에 항목 추가. `requireFlag`로 대화 힌트와 연결.
- **손님 추가**: `customers`에 추가하고 `Art/Customers/{sprite}.png` 넣기.
- **대사 수정**: `heroine_rina.json`. `expr`는 `normal smile embarrassed drunk pout serious love surprised`.
- **히로인 추가**: `heroine_{id}.json`을 만들고 `GameData.Load()`에 등록 (구조는 리나와 동일).
- **표정 스프라이트**: `Art/Characters/rina_smile.png`처럼 `{sprite}_{expr}.png`를 넣으면 자동으로 교체됩니다.
  없으면 기본 스프라이트 + 감정 아이콘 + 볼터치 + 자세로 표현합니다.
- **효과음/BGM 교체**: `Resources/Audio/{이름}`에 파일을 넣으면 합성음 대신 사용
  (`click pour fill flip sizzle perfect good burn coin fail notify heart clink door hit neon` / BGM `weekday night weekend song date`).

## 아트 리소스 출처

- 첨부된 도트 시트(노점 소품, 주인공, 굽기 단계/UI, UI 킷, 노점 카운터 배경)를 잘라서 사용했습니다.
- 기획서(PPT)의 일러스트는 640×360으로 축소·감색해서 도트풍 배경으로 사용했고, 히로인 이미지는 배경을 제거 후 도트화했습니다.
- 손님 NPC와 UI 프레임은 임시 도트로 새로 그렸습니다 → 직접 제작한 아트로 같은 파일명으로 교체하면 됩니다.
- 폰트: [Pretendard](https://github.com/orioncactus/pretendard), [Galmuri](https://github.com/quiple/galmuri) — SIL Open Font License 1.1

## 알려진 한계

- Unity 6 (6000.5.4f1) 에디터에서 임포트 · 컴파일 · 자동 스모크 테스트(3주 진행)를 통과했습니다.
  권장 버전인 2022.3 LTS 에디터에서는 아직 직접 열어 보지 못했습니다.
- 히로인 표정은 스프라이트 1장 + 연출로 대신합니다 (표정 스프라이트를 추가하면 자동 적용).
- MIKA / YUU 는 선택 화면에만 표시됩니다 (다음 단계에서 콘텐츠 추가).
