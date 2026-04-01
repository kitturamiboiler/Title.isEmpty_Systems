# 2026-04-01 데브로그: 코드 리팩토링 및 방어적 설계(v2.0) 적용

## 1. 개요
- **목적**: `.cursorrules 2.0` 도입에 따른 전역 코드 감사 및 성능 최적화.
- **핵심 키워드**: GC(Garbage Collector) 할당 제거, 매직 넘버 에셋화, 레이어 타입 안정성 확보.

---

## 2. 주요 리팩토링 내역

### A. WeaponData & PlayerBlinkController2D
- **데이터 기반 설계**: 하드코딩된 모든 수치(쿨타임, 히트스톱, 오프셋 등)를 `WeaponData.cs`로 이관. 밸런싱 효율 증대.
- **레이어 참조 시스템 개선**: `string` 기반 레이어 비교 로직 완전 제거. `Layers.cs` 정적 상수를 사용하여 오타로 인한 물리 버그 원천 차단.
- **수명 관리 최적화**: 블링크 이펙트(`Instantiate`) 생성 시 `Destroy(go, duration)`를 명시하여 메모리 누수 방지.

### B. EnemyLineOfSight2D (GC 폭탄 해체)
- **머티리얼 캐싱**: `Update` 내에서 `.material` 접근 시 매 프레임 새로운 인스턴스가 생성되는 유니티의 고질적 문제 해결.
    - `Awake`에서 1회 캐싱 후 재사용.
    - `OnDestroy`에서 명시적으로 `Destroy(_cachedMaterial)` 호출하여 씬 전환 시 누수 방지.
- **방어적 참조 로직**: `playerTransform` 탐색 시 인스펙터 할당을 우선하고, 누락 시에만 `FindFirstObjectByType`을 호출하도록 설계하여 런타임 부하 최소화 및 경고 로그 추가.

---

## 3. 방어적 설계 체크리스트 (v2.0 준수)
- [x] **Early Return**: 모든 메서드 진입점에서 `null` 및 경계값 검사 수행.
- [x] **No Happy Path**: 컴포넌트 누락, 플레이어 미탐색 등 최악의 상황에 대한 `Debug.LogError/Warning` 처리.
- [x] **Memory Management**: 핫 루프(Update/FixedUpdate) 내 `new` 할당 및 문자열 생성 지양.

---

## 4. 향후 과제
- **PlayerStateMachine (FSM) 구축**: 현재 `Update` 내부에 흩어진 전투 로직을 상태 클래스로 분리하여 확장성 확보.
- **히트스톱 & 카메라 쉐이크 시스템**: `WeaponData`에 추가된 데이터를 기반으로 타격감 디테일 작업 진행.
- **오브젝트 풀링**: 투사체 및 이펙트 생성 시 발생하는 인스턴스 부하를 줄이기 위한 Pool 시스템 도입 검토.