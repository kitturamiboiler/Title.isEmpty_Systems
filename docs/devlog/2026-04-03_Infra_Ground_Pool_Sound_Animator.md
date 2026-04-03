# 📋 Devlog — 2026-04-03 · 인프라 (Ground / 풀 / 사운드 / Animator)

> **목적**: 데모·스팀 출시 전 **코드만으로** 바로 이득 나는 작업 묶음  
> **핵심 키워드**: `Layers` 단일 폴백, 단검 풀링, `SoundManager` 훅, Animator 플레이스홀더

---

## 개요

에셋 없이도 적용 가능한 방어·GC·연출 뼈대를 한 번에 넣었다.

---

## 1. Ground 마스크 (`Layers.PlayerPhysicsGroundMask`)

| 항목 | 내용 |
|------|------|
| 추가 | `Layers.PlayerPhysicsGroundMask` — Ground 레이어 단일 비트 |
| 적용 | `PlayerMovement2D`, `PlayerStateMachine` → `SlamState`, `PlayerBlinkController2D` |
| 동작 | 인스펙터 `groundMask`가 **비어 있으면** 위 폴백 사용 → 이중 설정 불일치 완화 |

**주의**: Ground 외 전용 플랫폼 레이어만 쓰는 씬은 인스펙터에 마스크를 **직접** 채워야 한다.

---

## 2. 단검 오브젝트 풀 (선택)

| 항목 | 내용 |
|------|------|
| 컴포넌트 | `PlayerBlinkController2D._daggerPool` → `SimpleGameObjectPool` |
| 조건 | 풀의 프리팹 == `WeaponData.daggerProjectilePrefab` 일 때만 `Get` / `Release` |
| 폴백 | 불일치 시 `LogWarning` 후 `Instantiate` |
| 투사체 | `DaggerProjectile2D.SetReleasePool`, `ReleaseToPoolOrDestroy()` — 최대 사거리·블링크 회수 시 반환 |

`SimpleGameObjectPool`에 `PooledPrefab` 프로퍼티 추가(검증용).

---

## 3. 사운드 훅 (`SoundManager`)

| API | 연결 지점 |
|-----|-----------|
| `PlayBlink()` | `PlayerBlinkController2D.TryBlinkToDagger` 성공 시 |
| `PlayGrab()` | `GrabState.Enter` |
| `PlaySlam()` | `SlamState.ExecuteImpact` |
| `PlayParry()` | `PlayerParryController2D.OnParrySuccess` |
| `PlayDeath()` | `PlayerHealth.Die` (`_sfxDeath` 슬롯 추가) |

클립 미할당 시 기존 `PlaySFX` 가드로 **무음 스킵**.

---

## 4. Animator 플레이스홀더

### 플레이어

- `PlayerAnimHashes` — `Player_Idle`, `Player_Run`, `Player_BlinkCombat`, `Player_Grab`, `Player_Slam`, `Player_Bound`
- `PlayerStateMachine` — `[SerializeField] Animator`, `NotifyPlayerAnim(int)`
- 각 State `Enter()`에서 대응 트리거 발행

### 보스

- `BossAnimHashes` — Int `BossPhase`, Trigger `BossGrabbed`, `BossVulnerable`
- `BossStateMachine._bossAnimator` — 페이즈 전환 시 `SetInteger`, `BossGrabbedState` / `BossVulnerableState` 진입 시 `SetTrigger`

Animator 미할당 시 **동작 변화 없음**.

---

## 5. 체크리스트 (씬 작업)

- [ ] 단검 풀 사용 시: 풀 프리팹 = `WeaponData` 단검 프리팹
- [ ] 플레이어 / 보스 Animator 슬롯 연결 시 컨트롤러 파라미터 이름을 위 해시와 일치
- [ ] `SoundManager`에 SFX 클립 순차 연결

---

## 6. 관련 문서

- 같은 날짜 컴파일 정리: [`2026-04-03_Compile_Errors_Fix.md`](./2026-04-03_Compile_Errors_Fix.md)
