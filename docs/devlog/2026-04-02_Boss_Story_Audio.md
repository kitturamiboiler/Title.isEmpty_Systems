# 📋 Devlog

---

## 2026-04-01 — FSM 안정화 및 전투 입력 무결성 확보

> **목적**: `Blink-Grab-Slam` 루프 구현 중 발견된 런타임 예외 및 조작 간섭 해결  
> **핵심 키워드**: 소프트 락 해제, 입력 가드(Input Guard), 상태 전이 안전성

이번 업데이트는 단순 기능 추가가 아닌, **시스템의 척추(Core)를 바로잡는 리팩토링**에 집중했습니다.

### 🐛 주요 수정 내역 (Bug Report)

**A. FSM 논리 결함 수정**

- **BUG-01 · GrabState 소프트 락 방어**  
  타겟 유실(`_target == null`) 시 별도 전이 없이 무한 대기하던 로직을 수정하여, 즉시 `IdleState`로 복귀하는 탈출로를 확보했습니다.

- **BUG-02 · SlamState 초기화 안정화**  
  `_hasImpacted` 플래그 초기화 시점을 `Enter()` 최상단으로 이동하여, 조기 리턴 시에도 물리 로직이 오발사되지 않도록 수정했습니다. `FixedTick` 내 `_rb` 및 `_target` null 가드를 강화했습니다.

**B. 조작 배타성 강화**

- **BUG-03 · 입력 가드 시스템 (Input Guard)**  
  `Grab` 및 `Slam` 상태 진행 중에는 `PlayerBlinkController2D`의 모든 입력(단검 투척, 블링크)을 차단합니다.  
  블링크 중복 발사로 인한 '자기 전이(Self-Transition)' 버그를 차단하고, 그랩 액션의 연출 집중도를 향상시킵니다.

### ✅ 방어적 설계 체크리스트 (v2.1)

- [x] **Early Return** — 상태 진입 및 물리 연산 전 `null` 가드 처리 완료
- [x] **Input Exclusivity** — 특정 상태에서의 입력 간섭 차단 완료
- [x] **State Integrity** — 비정상 상태 시 `Idle` 복귀 로직 추가

### ⚠️ 설정 주의사항 (Setup Warning)

> **Ground Mask 동기화**  
> `PlayerMovement2D`와 `PlayerStateMachine`의 레이어 마스크 설정이 이중화되어 있습니다.  
> 인스펙터에서 두 항목이 동일한 `Ground` 레이어를 바라보는지 반드시 확인하십시오.  
> *(차후 전역 상수로 통합 예정)*

### 🔜 향후 과제

- **SlamState 타격감 연출** — 카메라 쉐이크 및 히트스톱 이벤트 연결
- **Enemy AI 연동** — `OnGrabbable` 이벤트를 활용한 적 유닛 피격 연출(반짝임 등) 추가

---

## 2026-04-02 — Boss 2~5 전체 구현 및 엔딩·시스템 인프라 완성

> **목적**: Paper(서류) 이후 전 보스 구현, 엔딩 시스템, 대화 시스템, 핵심 인프라 완성  
> **핵심 키워드**: 보스 AI FSM, 컷씬 엔진, 대화 우선순위, 회차 추적, 방어적 설계

---

### 👾 Boss 2 — Paper / 서류

#### 구현 내역

| 클래스 / 상태 | 설명 |
|---|---|
| `EnemyBossPaper` | 드론 방패 + 도장 패턴 허브 |
| `PaperDrone` | 패리 유도 드론 |
| `PaperDroneShieldState` | 드론 전멸 시 `PaperOverclockState`로 전이 |
| `PaperStampState` | 지면 Raycast 방어 포함 |
| `PaperOverclockState` (Phase 2) | 4발 바라지 + 각도 퍼짐 |

#### 🛡️ 방어 설계 (Rule 1)

- **H1** — `_hasParried` 플래그로 다중 패리 오버플로우 차단
- **H2** — `BossParryableProjectile2D` 편향탄 팀 ID 오류 방어
- **H3** — `PaperStampState` 지면 미감지 시 스탬프 취소

---

### 👾 Boss 3 — Brother / 형

#### 구현 내역

| 클래스 / 상태 | 설명 |
|---|---|
| `EnemyBossBrother` | `LastPlayerBlinkPos` 추적 |
| `BrotherBindProjectile2D` | 구속 와이어 |
| `BrotherCounterState` (Phase 2) | 블링크 위치로 텔레포트 후 즉각 슬래시 |
| `PlayerBoundState` | 전면 재작성 |

#### 🛡️ 방어 설계 (Rule 1)

- **H1** — 단검 분실 시 `DAGGER_LOST_PENALTY` 타이머 가속 (Soft Lock 방어)
- **H2** — 구속 중 블링크 시도 시 `TriggerRejectFlash` 빨간 플래시
- **H3** — 타임아웃 데미지 → `IsDead` 체크 → 사망과 탈출 충돌 방어

---

### 👾 Boss 4 — Shadow / 그림자

#### 구현 내역

| 클래스 / 상태 | 설명 |
|---|---|
| `EnemyBossShadow` | LRU 마커 관리 + Bind/Unbind 오버라이드 |
| `BlinkGhostMarker` | Pause/Resume + 즉각 겹침 감지 |
| `GhostSwitch` | HashSet 추적 + `IsFake` (Phase 3 가짜 스위치) |
| `ShadowGhostPhaseState` | 퍼즐 타임아웃 |
| `ShadowHauntState` | 절대 타이머 + `FadeAndWarpRoutine` |

> **Phase 2**: `GetCurrentMarkerLifetime()` Phase별 수명 감소 적용

#### 🛡️ 방어 설계 (Rule 1)

- **H1** — 마커 벽 클리핑 → `Physics2D.OverlapPoint` 스폰 차단
- **H2** — `HauntState` 무한 루프 → 절대 타이머 + 강제 워프
- **H3** — Bind 중 마커 혼란 → `PauseAllMarkers` / `ResumeAllMarkers`
- **H4** — 페이드 중 플레이어 사망 → `playerHealth.IsDead` yield 체크
- **H5** — 가짜 스위치 마커 과부하 → LRU + Phase별 수명 단축

#### 📁 파일 구조 재정비

```
Enemy/Boss/
├── Hound/   └── States/
├── Paper/   └── States/
├── Brother/ └── States/
└── Shadow/  └── States/
```

각 보스별 독립 폴더로 재구조화하여 결합도 최소화.

---

### 👾 Boss 5 — Designer / 설계자

#### 구현 내역

| 클래스 / 상태 | 설명 |
|---|---|
| `EnemyBossDesigner` | Wave 13 + Phase 12 허브 |
| `DesignerUmbrella` | 단검 반사 Anti-Blink (핵심 메카닉) |
| `ArmJoint` | 관절 파괴 (원본 disabled + 분리 프리팹) |
| `EliteAgent` / `NullCyborg` / `EliteZeroX01` | Wave 적 3종 |
| `DesignerWaveState` | Wave 1→2→3 시퀀서 |
| `DesignerUmbrellaPhaseState` | 패리→기절→블링크 루프 |
| `DesignerTrueFormState` | 역패리 + Struggle 트리거 |
| `DesignerStruggleState` | 동시 대치 + `ForcedReleaseTimer` |
| `StruggleUI` | Space 연타 진행도 바 |

#### `DaggerProjectile2D` 수정

- `IsReflected` 프로퍼티 추가
- `Reflect(Vector2)` 메서드 추가
- `PlayerBlinkController2D.TryBlinkToDagger()` — `IsReflected` 가드 적용

#### 🛡️ 방어 설계 (Rule 1)

- **H1** — 관절 분리 물리 오류 → `enabled=false` + 분리 프리팹 독립 스폰
- **H2** — 동시 잡기 무한 루프 → `ForcedReleaseTimer` 5초 강제 해제
- **H3** — USB 습득 누락 → 카메라 포커스 + 자동 프롬프트

---

### 🎬 엔딩 시스템

#### 구현 내역

| 클래스 | 설명 |
|---|---|
| `CutscenePlayer` | 라인바이라인 나레이션 엔진 (FadeIn/Out, Narration, Dialogue, Pause, Confirm) |
| `EndingChoiceUI` | `[연서가 막는다]` / `[연서가 누른다]` 선택지 |
| `EndingUSBItem` | 공통 프리 컷씬 + Ending A/B 전체 대사 내장 |

#### 엔딩 분기 요약

| | Ending A | Ending B |
|---|---|---|
| **선택** | 연서가 USB를 막는다 | 연서가 USB를 직접 누른다 |
| **결말** | 빨간 우산 아래 둘이 서있음 → *"현아."* | 우산이 바닥에, 혼자 빗속 |
| **톤** | 희망 | 비극 |

---

### 💬 대화 / 스토리 시스템

#### 구현 내역

| 클래스 | 설명 |
|---|---|
| `CombatDialogueUI` | 비블로킹 전투 자막 싱글턴 |
| `BossCombatDialogue` | 보스 오브젝트 컴포넌트 — Phase 전환/패리/그랩/취약 진입 자동 트리거 |
| `TriggerCutscene` | 맵 배치 대화 트리거 존 (WalkAndTalk / BlockingCutscene 모드) |

**대화 우선순위 3단계**

| 레벨 | 종류 | 동작 |
|:---:|---|---|
| 1 | Ambient | 낮은 우선순위, 큐 드롭 허용 |
| 2 | Combat | 전투 상황 자막 |
| 3 | Story | 강제 재생, 드롭 불가 |

#### `BossStateMachine` 수정

- `TransitionToPhase()` → `BossCombatDialogue.TriggerPhase()` 자동 호출
- `BossGrabbedState` → 그랩 반응 대사 자동 호출
- `BossVulnerableState` → 취약 반응 대사 자동 호출
- `BossParryableProjectile2D.Deflect()` → 패리 반응 대사 자동 호출

#### `OnBlinkExecuted` 시그니처 변경

```csharp
// Before
Action<Vector2>
// After
Action<Vector2 from, Vector2 to>
```

구독자 (`EnemyBossBrother`, `EnemyBossShadow`) 시그니처 업데이트 완료.

#### 🛡️ 방어 설계

- **H2** — `TriggerCutscene` Linecast 블링크 스킵 방어
- **H3** — `ResetOnDeath` 사망 리셋

---

### ⚙️ 시스템 인프라

#### 구현 내역

| 클래스 | 설명 |
|---|---|
| `PlaythroughTracker` | 회차 추적 (`PlayerPrefs`) |
| `SoundManager` | 오디오 뼈대 (`ignoreListenerPause`, BGM/SFX 슬롯) |
| `CheckpointManager` | 체크포인트 저장/복구 뼈대 |

#### 1장 스킵 불가 처리

- `CutscenePlayer._allowSkip` 플래그 추가
- `HoldWithSkip()` 코루틴 — `false`이면 Space 무반응
- `Confirm` 라인 타입 — 무조건 Space 확인 요구
- `TriggerCutscene._freezeTimeScale` — `Time.timeScale = 0` 플레이어 완전 정지

#### 2회차 자동 스킵

- `TriggerCutscene` + `PlaythroughTracker.HasCompletedChapter()` 연동
- 2회차 이상 진입 시 `CutscenePlayer.AllowSkip = true` 자동 설정

---

### 📂 신규 생성 파일 총계

```
Boss/Paper/        PaperOverclockState.cs
Boss/Brother/      BrotherCounterState.cs       (재작성)
                   PlayerBoundState.cs           (전면 재작성)
Boss/Shadow/       (Phase 2 + Rule 1 전체 수정)
Boss/Designer/     EnemyBossDesigner.cs
                   DesignerUmbrella.cs
                   ArmJoint.cs
                   Enemies/                      × 3
                   States/                       × 4
Dialogue/          CombatDialogueUI.cs
                   BossCombatDialogue.cs
                   TriggerCutscene.cs
Ending/            CutscenePlayer.cs
                   EndingChoiceUI.cs
                   EndingUSBItem.cs
System/            PlaythroughTracker.cs
                   SoundManager.cs
                   CheckpointManager.cs
UI/                StruggleUI.cs
```

> **신규/수정 합계: 약 25개 파일**
