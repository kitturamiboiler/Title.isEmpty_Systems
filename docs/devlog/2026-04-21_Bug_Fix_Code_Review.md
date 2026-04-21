# Devlog — 2026-04-21 · 코드 리뷰 기반 버그 수정 & 리팩터링

> **맥락**: 전체 코드 리뷰로 발견한 🔴버그위험 / 🟡설계취약 / 🟢최적화 이슈를 우선순위대로 수정.  
> 핵심: 암전 잔상 방어, Confirm 스킵 플래그 분리, 트리거 검증 강화, JSON 파싱 경고, 무적 레이어 복구 버그, 패리 버퍼 확장, 블링크 재개 억제, 로드 로직 중복 제거.

---

## 1. 수정 완료 ✅

### 🔴 버그 위험

#### 1-1. `CutscenePlayer.cs` — 암전 잔상 조건 오류
- **문제**: `PrepareStoryUiForResume`에서 `_fadeOverlay.alpha > 0.99f`만 체크 → 페이드가 중간(0.01~0.98)에서 멈추면 화면이 어둡게 남음.
- **수정**: 기준을 **`> 0.01f`** 로 변경 — 조금이라도 암전 흔적이 있으면 즉시 제거.

#### 1-2. `CutscenePlayer.cs` — `_skipCurrentLine` 플래그 이중 소비
- **문제**: `Update`(HoldWithSkip용)와 `ShowConfirm`(WaitUntil용)이 같은 `_skipCurrentLine`을 공유 → 라인 이중 소비·타이밍 꼬임 가능.
- **수정**: `ShowConfirm` 전용 **`_confirmWaitInputReady`** 플래그 분리.

#### 1-3. `StoryEventTrigger.cs` — Rigidbody2D 미전제
- **문제**: `OnTriggerEnter2D`는 양쪽 중 하나에 RB가 있어야 발생 — 플레이어 RB 없으면 트리거 자체가 무음 실패.
- **수정**: `Awake`에서 **RB 미할당 경고** 추가. 검증 로직을 `IsPlayer()` 메서드로 분리, **Tag + Layer 이중 검증** 적용.

#### 1-4. `StoryJsonManager.cs` — JSON type 오타 시 조용한 폴백
- **문제**: `ParseLineType` 기본값이 항상 `Narration` → JSON에 오타가 있어도 경고 없이 잘못된 연출 실행.
- **수정**: 알 수 없는 타입 시 **`Debug.LogWarning`** 출력 후 폴백.

#### 1-5. `PlayerBlinkController2D.cs` — 무적 레이어 복구 오류
- **문제**: `PerformBlinkInvincibility`에서 `origLayer = Layers.Player` 고정 → 상태 전이 중 레이어가 달라지면 복구 레이어가 틀림.
- **수정**: **`origLayer = gameObject.layer`** (호출 시점 실제 레이어)로 변경 + 이미 무적 레이어인 경우 중복 진입 방어.

#### 1-6. `PlayerParryController2D.cs` — 패리 버퍼 한도
- **문제**: `_overlapBuffer` 크기 8 → 밀집 구간에서 콜라이더 8개 초과 시 대상 누락.
- **수정**: **16**으로 확장.

### 🟡 설계 취약

#### 1-7. `BlinkState.cs` — 재개 입력 억제 게이트 누락
- **문제**: 패리(`PlayerParryController2D`)는 `IsResumeInputSuppressActive`를 체크하는데, Shift 블링크(`BlinkState`)는 미적용 → 재개 직후 블링크가 뚫릴 수 있음.
- **수정**: `Tick()`에 **`IsResumeInputSuppressActive`** 게이트 추가.

#### 1-8. `GameInitializer.cs` + `ChapterStoryLoader.cs` — 중복 switch
- **문제**: 동일한 `StoryKey` → `List<CutsceneLine>` 분기가 두 파일에 복제 → 한쪽만 수정 시 불일치.
- **수정**: **`Assets/Dialogue/StoryLineLoader.cs`** (신규 static 클래스) 로 단일화. 두 파일 모두 `StoryLineLoader.Load(key)` 위임.

---

## 2. 미수정 — 추후 대응 필요 ❌

### 🔴 버그 위험 (코드만으로 불가 — 씬 구조 확인 필요)

| 이슈 | 내용 | 확인 방법 |
|------|------|-----------|
| **FadeManager 암전 잔상** | `PrepareStoryUiForResume`은 `_fadeOverlay`(로컬)만 클리어. FadeManager가 별도 전용 오버레이를 쓰면 Resume 후에도 검정 화면 남음. | `FadeManager` 인스턴스가 관리하는 CanvasGroup을 확인하고, Resume 시 FadeManager에도 Fade-In 요청 추가 필요. |

### 🟡 설계 취약 (플레이 검증 후 수정)

| 이슈 | 파일 | 내용 |
|------|------|------|
| `walkOnlyNoJump` 공중 속도 미리셋 | `PlayerMovement2D.cs` | 트리거 직전 공중에 있으면 의도치 않은 이동 가능 |
| `SlamState` 직접 타입 체크 | `PlayerMovement2D.cs` | 새 "이동 막는 상태" 추가 시 매번 분기 수동 추가 필요 |
| `_playerMovement null` Raw 폴백 | `PlayerStateMachine.cs` | `GetMovementHorizontalAxis`에서 컷신 가상 입력 미반영 |
| 데드존 상수 중복 | `IdleState.cs` / `RunState.cs` | `inputDeadZone` 두 곳 — 한쪽만 바뀌면 애니·이동 불일치 |
| 인스펙터 라인 무조건 덮어씀 | `ChapterStoryLoader.cs` | Awake에서 수동 입력 라인 덮어쓰기 — 디버그 어려움 |
| 공중 블링크 제한 주석 처리 | `PlayerBlinkController2D.cs` | `maxAirBlinkCount` 변수는 있으나 실제 제한 로직 비활성 — 기획·코드 불일치 |

### 🟢 성능 최적화 (여유 있을 때)

| 이슈 | 파일 | 내용 |
|------|------|------|
| FixedUpdate 물리 쿼리 과다 | `PlayerMovement2D.cs` | BoxCast + 보조 레이 2개 + 벽 프로브 레이 6개 |
| 블링크마다 GameObject 동적 생성 | `PlayerBlinkController2D.cs` | LineRenderer 등 매 블링크마다 생성·파괴 → 풀링 권장 |
| 미사용 메서드 | `PlayerBlinkController2D.cs` | `PlayBlinkSoundSafe` 호출처 없음 — 삭제 가능 |
| `ForceMeshUpdate(true)` 과호출 | `CutscenePlayer.cs` | 라인마다 호출 — 불필요한 경우도 포함 |

---

## 3. 신규 생성 파일

| 파일 | 역할 |
|------|------|
| `Assets/Dialogue/StoryLineLoader.cs` | `StoryKey` → `List<CutsceneLine>` 단일 로드 진입점. `ChapterStoryLoader`, `GameInitializer` 공용. |

---

## 4. 관련 경로 (빠른 점프)

| 영역 | 경로 |
|------|------|
| 컷신 재생기 | `Assets/Ending/CutscenePlayer.cs` |
| 스토리 트리거 | `Assets/Story/StoryEventTrigger.cs` |
| JSON 로더 | `Assets/Dialogue/StoryJsonManager.cs` |
| 공통 라인 로더 | `Assets/Dialogue/StoryLineLoader.cs` |
| 플레이어 이동 | `Assets/Player/PlayerMovement2D.cs` |
| 블링크 컨트롤러 | `Assets/Player/PlayerBlinkController2D.cs` |
| 패리 컨트롤러 | `Assets/Player/PlayerParryController2D.cs` |
| FSM 블링크 상태 | `Assets/Player/States/BlinkState.cs` |
