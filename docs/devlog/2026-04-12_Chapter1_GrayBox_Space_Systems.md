# 📋 Devlog — 2026-04-12 · 챕터 1 그레이박스 · 공간·시스템·환경 (PM 요약)

> **목적**: 우산 핵심 오브젝트 전에 **「9년 전 그날」의 공간 문법**과 **시스템적 제어권**을 고정한다. 예쁘게 그리기 전, 버그가 나기 어려운 로직 바닥을 다지는 단계.  
> **핵심 키워드**: Gray-box, `OpeningEventController`, `SetStoryInputLocked`, 착지 대기, 입력 억제, `NPCFollow2D`, Pixel Perfect, Rain, VisualRoot

---

## 개요 (15년 차 PM 모드 요약)

디렉터님, 우산이라는 핵심 오브젝트가 들어가기 전까지 우리는 **「9년 전 그날」의 공간적 문법과 시스템적 제어권**을 설계하는 데 집중했습니다. 예쁘게 그리기 전, 버그가 발생할 수 없는 단단한 바닥(Logic)을 다지는 과정이었습니다.

아래는 **[개발일지: 챕터 1 공간 및 시스템 구축 단계]**로 정리한 진행 상황입니다.

---

## 1. 레벨 디자인 및 공간 좌표 확정 (Level Design)

| 구간 | 내용 |
|------|------|
| **X = 0 ~ 40** (학교 정문) | 감정적 몰입을 위한 적막한 빗길 구간 설계 |
| **X = 40** (목표 지점) | 연서(NPC) 배치, 비를 피할 수 있는 처마 **Y = 4** 레이아웃 |
| **X = 42** (물리 차단) | 이벤트 전 무단 이탈 방지 — `Obj_StoryGate_School` |
| **X = 70** (점프 튜토리얼) | 3칸 규격 Gap으로 기본 도약 메커니즘 검증 구간 |
| **X = 100** (분식집 랜드마크) | **10×6** 거대 매스로 두 번째 시퀀스 도달점 시각화 |

**월드 좌표 설계**: X = 100까지 바닥 공사 완료로 간주.

---

## 2. 시각적 환경 및 픽셀 파이프라인 (Atmosphere)

| 항목 | 내용 |
|------|------|
| **Pixel Perfect** | 홀수 해상도 경고(593×333) 대응 — **16:9 고정 + 정수 배율 1280×720**로 픽셀 뭉개짐 방어 |
| **배경** | 느와르 톤 — **Deep Black `#06060a`**로 픽셀 실루엣 대비 강화 |
| **Rain** | World Space 파티클 — 중력 1.5, 낙하 속도 20, **Stretched Billboard**로 빗줄기 질감 |

**환경 연출**: 비·배경색 중심으로 약 **90%** 수준으로 정리(우산 스프라이트 본작은 다음 단계).

---

## 3. 이벤트 및 입력 제어 아키텍처 (System Architecture)

| 항목 | 내용 |
|------|------|
| **제어권 분리** | `PlayerMovement2D`, Blink, Parry 등에서 **`SetStoryInputLocked`** 로 컷씬·연출 중 돌발 입력 방어 |
| **트리거** | **X ≈ 37** `OpeningEventController`로 연출 시작 시점 동기화 |
| **방어적 연출** | 공중 트리거 시 부유 방지 — **착지 후 잠금**(`WaitUntilPlayerFloored` 계열), 해제 직후 튐 완화 — **수평 입력 억제 ~0.08s** + `Input.ResetInputAxes()` |
| **동행** | NPC 연서 — 플레이어 대비 **1.2× 속도**, **Follow Distance 1.5** (`NPCFollow2D` + 플레이어 `ConfiguredMoveSpeed` 연동) |

**입력 제어 로직**: State 기반 Input Lock 구조를 오프닝 시나리오에 맞게 연결 완료로 간주.

**참고**: X = 70 Gap 구간은 직선 추적만으로는 추락·끼임 리스크가 있어, 이후 Waypoint / Ghost Path 등 별도 방어가 필요하다는 전제가 코드 주석에 남아 있음.

---

## 4. 애니메이션용 하이어라키 (Visual Refactoring)

| 항목 | 내용 |
|------|------|
| **VisualRoot 분리** | 물리 콜라이더와 시각 에셋 분리 — 우산 펼침 시 **바운스·스쿼시**가 본체 물리에 덜 미치도록 |
| **소팅 레이어** | 캐릭터(10) / 우산(11) 순서 확정 — 실루엣·간섭 방어 |

**시각 에셋(우산 본편)**: 스프라이트·애니 제작은 **0% → 다음 작업**으로 예약.

---

## 진행률 스냅샷

| 영역 | 상태 |
|------|------|
| 월드 좌표 설계 | **100%** (X = 100 바닥) |
| 환경 연출 | **~90%** (비·배경) |
| 입력 제어·오프닝 로직 | **100%** (현 단계 기준) |
| 우산 시각 에셋 | **0%** (다음) |

---

## 관련 코드·에셋 (참고)

- `Assets/Story/OpeningEventController.cs`
- `Assets/Story/NPCFollow2D.cs`
- `Assets/Player/PlayerMovement2D.cs` — `IsFloorGrounded`, `SetStoryInputLocked`, `ReadHorizontalRaw`
- `Assets/Player/PlayerBlinkController2D.cs` — 우산 스프라이트·스쿼시·스토리 입력 잠금
- `Assets/Player/PlayerParryController2D.cs` — 스토리 입력 잠금
