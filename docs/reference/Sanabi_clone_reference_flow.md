# 산나비 모작 프로젝트 — 카메라·씬 플로우 (참고용)

코드 이식 대상이 아니라, **흐름 설계**를 짤 때만 참고하면 됩니다.  
실제 구현은 KnifeSystem(Unity 6, Cinemachine 유무)에 맞춰 새로 짜는 것을 권장합니다.

## 카메라 연출 (모작에서의 구조)

- **Cinemachine Virtual Camera**를 여러 개 두고 **Priority**로 활성 카메라를 바꿈.  
  - 예: `Main`(추적) vs `Zoom`(연출/사망 등) vs 이벤트용 `CutScene` 전용 VC.
- **CinemachineBrain**의 `Default Blend > Time`으로 전환 길이 조절 (급전환 vs 부드러운 블렌드).
- **CinemachineConfiner2D**로 `Bounding Shape`에 플레이어 시야 제한 → 방/구역마다 콜라이더를 바꿔 **맵 경계**를 맞춤.
- **CinemachineImpulse Source** 등으로 훅/피격 시 카메라 쉐이크 (Knife에는 별도 `CameraShaker` 등이 있으면 역할이 겹침).

### Knife에 옮길 때 체크할 것

- 프로젝트에 **Cinemachine 패키지**가 없으면 먼저 패키지 추가 후, `CameraFollow`와 **역할 분담** 정리 (추적은 CM, 연출만 커스텀 등).
- Unity 6과 모작(2021 LTS)의 Cinemachine **API/버전 차이**는 공식 마이그레이션 문서를 본 뒤 적용.

## 씬·게임 플로우 (모작에서의 흐름)

대략적인 **덩어리**만 참고하면 됩니다.

| 개념 | 모작에서의 역할 |
|------|----------------|
| **Title** | 타이틀 BGM, 커서 표시, 다음 씬 로딩 루틴 진입점 |
| **GameFlow** | 씬 안에 Confiner(경계 콜라이더) 리스트를 두고, 진행에 따라 **다음 구역 경계**로 교체 |
| **GameChapterController** | 챕터를 `GameObject` 배열로 두고, 클리어 시 다음 챕터 활성화 + 세이브 포인트 갱신 |
| **SavePoint** | 진행 단계(챕터 인덱스 등) 저장 |
| **BaseScene** | 씬 공통 로딩/초기화 훅 (모작 패턴) |

### Knife에 맞게 설계할 때

- 이미 **챕터 스토리/보스/엔딩** 구조가 있으므로, 모작의 “챕터 = 씬 내 비활성 오브젝트 토글” 방식을 **그대로 복제할 필요는 없음**.
- 유용한 아이디어만 추리면: **구역 전환 시 Confiner만 바꾼다**, **클리어 이벤트 → 다음 페이즈 활성화**, **세이브는 단일 숫자/플래그가 아니라 StoryDatabase 등 기존 데이터와 통합** 정도.

## 코드로 이미 이식한 것 (이 문서와 별개)

- `Assets/Utils/GameExtensions.cs` — 레이어 마스크, 딜레이 코루틴, 2D 각도/방향 헬퍼.
- `Assets/Utils/SimpleGameObjectPool.cs` — 단순 프리팹 풀.

카메라 매니저·씬 컨트롤러 **풀 소스**는 복사하지 않았습니다.
