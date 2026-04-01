# 2026-04-01 데브로그: FSM 안정화 및 전투 입력

## 1. 개요
- **목적**: `Blink-Grab-Slam` 루프 구현 중 발견된 런타임 예외 및 조작 간섭 해결.
- **핵심 키워드**: 소프트 락 해제, 입력 가드(Input Guard), 상태 전이 안전성.

---

## 2. 주요 수정 내역 (Debugging Report)

### A. FSM 논리 결함 수정 (BUG-1, BUG-2)
- **GrabState 소프트 락 방어**: 타겟 유실(`_target == null`) 시 별도 전이 없이 대기하던 로직을 수정하여 즉시 `IdleState`로 복귀하도록 탈출로 확보.
- **SlamState 초기화 안정화**: `_hasImpacted` 플래그 초기화 시점을 `Enter()` 최상단으로 이동하여, 조기 리턴 시에도 물리 로직이 오발사되지 않도록 수정. `FixedTick` 내 `_rb` 및 `_target` null 가드 강화.

### B. 조작 배타성 강화 (BUG-3)
- **입력 가드 시스템**: `Grab` 및 `Slam` 상태 진행 중에는 `PlayerBlinkController2D`의 모든 입력(단검 투척, 블링크)을 차단.
- **기대 효과**: 블링크 중복 발사로 인한 '자기 전이(Self-Transition)' 버그를 차단하고, 그랩 액션의 연출 집중도 향상.

---

## 3. 방어적 설계 체크리스트 (v2.1 준수)
- [x] **Early Return**: 상태 진입 및 물리 연산 전 `null` 가드 처리 완료.
- [x] **Input Exclusivity**: 특정 상태에서의 입력 간섭 차단 완료.
- [x] **State Integrity**: 비정상 상태 시 `Idle` 복귀 로직 추가.

---

## 4. 설정 주의사항 (Setup Warning)
- **Ground Mask 동기화**: `PlayerMovement2D`와 `PlayerStateMachine`의 레이어 마스크 설정이 이중화되어 있음. 인스펙터에서 두 항목이 동일한 `Ground` 레이어를 바라보는지 반드시 확인 필요. (차후 전역 상수로 통합 예정)

---

## 5. 향후 과제
- **SlamState 타격감 연출**: 카메라 쉐이크 및 히트스톱 이벤트 연결.
- **Enemy AI 연동**: `OnGrabbable` 이벤트를 활용한 적 유닛의 피격 연출(반짝임 등) 추가.