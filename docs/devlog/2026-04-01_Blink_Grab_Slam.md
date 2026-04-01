# 2026-04-01 데브로그: FSM 전투입력, Boss FrameWork

# Project — Blink·Grab·Slam Combat Framework

---

## 🎮 Core Gameplay Loop

세 가지 메카닉이 유기적으로 연결되는 전투 루프입니다.

| 메카닉 | 설명 |
|---|---|
| **Blink** (순간이동) | 투사체 패링 시 즉시 충전되는 초고속 이동기 |
| **Grab** (제압) | 적의 약점을 낚아채어 물리 소유권을 강제 획득 |
| **Slam** (강타) | 적을 지면에 메쳐 광역 데미지와 아머를 파괴하는 피니셔 |

---

## 🏗️ Technical Architecture (v2.1)

### Boss Framework (Scalable)
- **`IGrabbable` Interface** — 모든 적 유닛과 보스 약점 부위를 하나의 인터페이스로 통합, 저결합도(Low Coupling) 설계 달성
- **ArmorGauge System** — 일반 공격은 차단하고 Blink/Slam으로만 파괴 가능한 가드 시스템 및 자동 재생 로직

### Defensive Engineering
- **HitStop Management** — 다중 시간 정지 요청을 병합하는 전역 싱글톤 매니저로 레이스 컨디션 해결
- **Physics Safety** — `OverlapBox` 기반 85% 축소 영역 검사로 거대 콜라이더의 벽 뚫림(Clipping) 현상 방지

---

## 🗺️ Boss Roadmap — The 5 Trials

| 단계 | 보스명 | 핵심 기믹 | 서사적 키워드 |
|:---:|---|---|---|
| 01 | **하운드 (Hound)** | 블링크 입문 / 유압 피스톤 그랩 | 분노 |
| 02 | **서류 (Paper)** | 패리 + 블링크 (탄막 대응) | 무력감 |
| 03 | **형 (Brother)** | 단검 박고 강제 블링크 탈출 | 혼란 |
| 04 | **그림자 (Shadow)** | 비전투 퍼즐 / 잔상 스위칭 | 수용 |
| 05 | **설계자 (Designer)** | 군단 소환 / 역패리 대응 | 선택 |

---

## 📊 Project Status

- [x] **Core System** — 플레이어 FSM 안정화 및 입력 가드 시스템 완료
- [x] **Framework** — 보스 전용 확장형 아키텍처 및 `IGrabbable` 계약 완료
- [x] **Boss 1** — 하운드(Hound) AI 뼈대 및 페이즈 로직 구현
- [ ] **Juice** — SlamState 타격감 연동 (카메라 쉐이크, 이펙트)
- [ ] **Visuals** — 애니메이터(Animator) 및 VFX 실전 연동
- [ ] **UI/Sound** — 보스 체력바 시각화 및 타격 사운드 시스템

---

## 📋 Devlog

### 2026-04-01 — FSM 안정화 및 전투 입력 무결성 확보

> **목적**: `Blink-Grab-Slam` 루프 구현 중 발견된 런타임 예외 및 조작 간섭 해결  
> **핵심 키워드**: 소프트 락 해제, 입력 가드(Input Guard), 상태 전이 안전성

이번 업데이트는 단순 기능 추가가 아닌, **시스템의 척추(Core)를 바로잡는 리팩토링**에 집중했습니다.

#### 🐛 주요 수정 내역 (Bug Report)

**A. FSM 논리 결함 수정**

- **BUG-01 · GrabState 소프트 락 방어**  
  타겟 유실(`_target == null`) 시 별도 전이 없이 무한 대기하던 로직을 수정하여, 즉시 `IdleState`로 복귀하는 탈출로를 확보했습니다.

- **BUG-02 · SlamState 초기화 안정화**  
  `_hasImpacted` 플래그 초기화 시점을 `Enter()` 최상단으로 이동하여, 조기 리턴 시에도 물리 로직이 오발사되지 않도록 수정했습니다. `FixedTick` 내 `_rb` 및 `_target` null 가드를 강화했습니다.

**B. 조작 배타성 강화**

- **BUG-03 · 입력 가드 시스템 (Input Guard)**  
  `Grab` 및 `Slam` 상태 진행 중에는 `PlayerBlinkController2D`의 모든 입력(단검 투척, 블링크)을 차단합니다.  
  블링크 중복 발사로 인한 '자기 전이(Self-Transition)' 버그를 차단하고, 그랩 액션의 연출 집중도를 향상시킵니다.

#### ✅ 방어적 설계 체크리스트 (v2.1)

- [x] **Early Return** — 상태 진입 및 물리 연산 전 `null` 가드 처리 완료
- [x] **Input Exclusivity** — 특정 상태에서의 입력 간섭 차단 완료
- [x] **State Integrity** — 비정상 상태 시 `Idle` 복귀 로직 추가

#### ⚠️ 설정 주의사항 (Setup Warning)

> **Ground Mask 동기화**  
> `PlayerMovement2D`와 `PlayerStateMachine`의 레이어 마스크 설정이 이중화되어 있습니다.  
> 인스펙터에서 두 항목이 동일한 `Ground` 레이어를 바라보는지 반드시 확인하십시오.  
> *(차후 전역 상수로 통합 예정)*

#### 🔜 향후 과제

- **SlamState 타격감 연출** — 카메라 쉐이크 및 히트스톱 이벤트 연결
- **Enemy AI 연동** — `OnGrabbable` 이벤트를 활용한 적 유닛 피격 연출(반짝임 등) 추가
