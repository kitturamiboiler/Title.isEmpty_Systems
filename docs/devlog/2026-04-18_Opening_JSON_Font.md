# 📋 Devlog — 2026-04-18 · 오프닝 시퀀스 데이터 파이프라인 · 폰트 베이킹 · UI 방어

> **Role 맥락**: 15년 차 픽셀 아트 디렉터 겸 기술 블로그 에디터 톤으로, 오늘 수행한 **Unity 6 오프닝 시퀀스 개발**과 **폰트 최적화**를 한 장에 묶는다.  
> **핵심 키워드**: `StoryJsonManager`, `Chapter1_Opening.json`, `AutoStartSequence`, 선행 동작(Anticipation), TMP 베이킹, 누아르 물리 템포, `FadeManager`, 의성어 UI

---

## 1. 문제 상황 (Happy Path를 믿지 않기)

| 증상 | 가설 축 (디버깅 5원칙에 대응) |
|------|------------------------------|
| **씬 시작 직후 컷씬이 안 돌아감** | **선행 동작 부재**: `ChapterStoryLoader`가 `Awake`에서 JSON을 주입하지만, `TriggerCutscene`을 **즉시** 점화하는 코드가 먼저 돌면 리스트가 비어 있거나 레이스가 난다. |
| **한글 폰트(Apple SD Gothic Neo) 베이킹 시 에디터 프리징** | **타이밍/부하**: Dynamic/OS 기본 세트 전체를 한 번에 굽거나, 불필요한 글리프 범위로 **연산 폭발** → 체감 **약 2시간 지연** 수준의 멈춤. |

여기서 “코드가 틀렸다”보다 **데이터·파이프라인이 준비되기 전에 연출이 발화했다**는 점을 1순위로 봤다.

---

## 2. 기술적 해결 (수치·구조 중심)

### 2.1 JSON 스토리 데이터 외부화

- `Assets/Resources/StoryData/Chapter1_Opening.json` — `StoryDatabase.GetChapter1Lines()`를 **한글·줄바꿈 포함** 이전.
- `StoryJsonManager`: `Resources.Load<TextAsset>` + `JsonUtility`, 라인별 **공백 제외 26자** 초과 시 `LogError`로 디렉터에게 즉시 알림.
- **오프닝 구간만 재생**: `Chapter1_Opening`은 **line id 53**(FadeIn)까지만 리스트에 담고, 이후 라인은 **다음 씬**용으로 분리.
- `CutsceneLine.lineId`로 **id 43 의성어**, **id 53 암전 종료**를 코드에서 구분.

### 2.2 선행 동작 — `AutoStartSequence`

- `IEnumerator Start()`에서 **`0.1s`** 대기 후 `TriggerCutscene.Play()` 호출.
- 같은 프레임에 JSON 주입과 컷씬 점화가 붙지 않도록 **버퍼**를 둔 것이 핵심이다.
- `TriggerCutscene`에 **`Play()` → `ActivateTrigger()`** 공개 API 추가 (충돌 없이 강제 점화).

### 2.3 오프닝 물리 템포 (`PlayerMovement2D`)

- `BeginOpeningSequencePhysics` / `EndOpeningSequencePhysics` — 이동·점프·지상 가속을 오프닝 전용으로 주입.
- 초기 설계는 **느린 누아르 질감**(예: move **2.2**)에 가깝게 두었으나, **그레이박스 장애물을 넘기 어렵다**는 플레이 검증 후 **move 3.5 / jump 10** 등으로 조정(수치는 `OpeningSequencePhysicsDefaults`에 상수화).
- 복구 시 스냅샷이 비정상이면 **Follow-through**: `move 4` / `jump 12` 등 안전 기본값으로 회수.
- **걷기 전용 잠금**(`walkOnlyNoJump`): 스토리 구간에서 점프 연타로 트리거 스킵하는 것을 막기 위한 방어.

### 2.4 의성어·암전 연출

- **id 43「꼬르륵」**: `CombatDialogueUI.ShowAmbientOnomatopoeia` — 배경 알파 스케일·본문 색 톤 다운, TMP rich text 이탤릭.
- **id 53 FadeIn**: `FadeManager` — CanvasGroup으로 암전, **`blocksRaycasts`/`interactable`**로 입력 차단, **`FinalizeOpening`** 이벤트로 1장 시퀀스 종료.

### 2.5 폰트 베이킹 (TMP)

- 방식을 **Characters from File** (또는 필요 글리프만 제한)으로 전환.
- 체감: 연산을 **수 시간 → 약 449ms** 수준으로 줄였다는 결과를 기준으로 기록한다 (**99% 이상 단축**이라는 표현은 실측 환경 기준).

---

## 3. 결과 및 디버깅 5원칙 매핑

| 원칙 | 적용 |
|------|------|
| **선행 동작 (Anticipation)** | JSON 주입·`ChapterStoryLoader` `Awake` 이후를 고려한 **0.1s 대기**, 그리고 트리거 강제 점화 순서 고정. |
| **타이밍** | 컷씬 라인 duration·Fade **0.9s**와 `FadeManager` 기본 암전 시간 정렬. |
| **스미어 / 가독성 방어** | 의성어 라인에서 패널·텍스트 대비 조정, (별도) **Padding 5px** 등 아웃라인·테두리 가독성 실험은 **스미어 프레임** 사고로 UI에 이식. |
| **무게 중심** | UI 앵커 **(0,0)** 등 레이아웃이 연출과 겹치는지 확인 — **내일 수정 예약**으로 남김. |
| **Follow-through** | JSON/파싱 실패 시 Missing Data 대사, 물리 복구 실패 시 기본 수치, 암전 후 `FinalizeOpening`으로 제어권 정리. |

---

## 4. 마무리 — 한 줄과 내일

2시간에 가까운 연산을 **0.4초대(실측 449ms)**로 줄인 것은 단순한 “옵션 바꾸기”가 아니라, **필요 글리프만 조준한 데이터 설계**다. 타협이 아니라 **정밀 사격**에 가깝다.

**내일(또는 다음 세션) 예정**: 본문·JSON에 섞일 수 있는 **특수 따옴표(Unicode U+2018–U+201F)** 정규화·폰트 글리프 포함 여부 점검 — 잘못 들어가면 베이킹 범위가 다시 불어나거나, 줄바꿈·26자 검증과 충돌할 수 있다.

---

## 5. 관련 파일 (빠른 점프)

| 영역 | 경로 |
|------|------|
| JSON | `Assets/Resources/StoryData/Chapter1_Opening.json` |
| 로더 | `Assets/Dialogue/StoryJsonManager.cs`, `ChapterStoryLoader.cs` |
| 오토 스타트 | `Assets/Story/AutoStartSequence.cs`, `Assets/Dialogue/TriggerCutscene.cs` |
| 물리 | `Assets/Player/PlayerMovement2D.cs`, `Assets/Story/OpeningSequencePhysicsDefaults.cs` |
| UI | `Assets/Dialogue/CombatDialogueUI.cs` |
| 암전 | `Assets/System/FadeManager.cs`, `Assets/Ending/CutscenePlayer.cs` |

---

*작성일: 2026-04-18*
