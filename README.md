🗡️ Title.isEmpty | 개발일지
📅 2026-03-28 (토) — Dagger Blink System 구현
## 1. 핵심 구현 내용 (The Spike) 🚀
Dagger & Blink: 투사체 기반 위치 이동 시스템의 기틀 마련.

Constraint: 공중 블링크 1회 제한 (착지/벽 점착 시 리셋) 로직으로 무한 체공 방어.

Nuance (Juice): * Hit-Stop: 블링크 직후 시간 왜곡을 통한 공간 절단 타격감 구현.

Ghost Fade: 검붉은(#800000) 잔상으로 누아르 톤 비주얼 피드백 강화.

I-frame: 이동 경로 및 도착 직후 짧은 무적 프레임으로 불합리한 피격 방지.

## 2. 기술적 방어 & 버그 픽스 (Anti-Bug) 🛡️
Collision Filter: 단검이 플레이어 본인에게 부딪혀 블링크 카운트가 씹히던 현상 해결 (Layer Collision Matrix 최적화).

Wall Safe Offset: 텔레포트 시 벽 내부에 끼이는 현상을 콜라이더 두께 기반 오프셋 계산으로 해결.

Anti-Tunneling: 고속 투사체가 벽을 뚫고 지나가는 현상을 Continuous 충돌 감지로 원천 차단.

## 3. 시스템 구조 및 설정 (Setup) ⚙️
Future-proofing: Ray-Plane Cast 방식 채택으로 향후 2.5D 카메라 전환 시에도 조준 로직 유지 가능.

📅 2026-03-28 (토) - 2부: 기동성 혁신 (Wall Climb & Jump)

### 1. 핵심 구현 내용 (The Spike) 🚀
6-Ray Wall Probe 시스템: 발, 허리, 머리 높이에서 좌우 총 6개의 레이를 쏘아 '연속된 벽'을 정밀하게 감지. 반쪽짜리 벽에 매달리는 시각적 버그 방어.

Vertical Mobility: 벽에 붙은 상태에서 W/S 키로 자유로운 상하 이동 구현 (gravityScale = 0).

Wall Jump & Input Lock: 벽 점프 시 반대 방향으로 튕겨 나가는 힘과 함께 0.15초의 입력 잠금을 적용하여 조작의 쫀득함과 수직 상승 버그 방지 동시 확보.

### 2. 시스템 통합 (Integration) 🛡️
Resource Reset: 벽에 붙는 순간 공중 블링크 횟수를 즉시 초기화하여 '벽-블링크-벽'으로 이어지는 무한 기동의 발판 마련.

State-Based Architecture: Grounded, Air, WallClimb 상태를 명확히 분리하여 복합적인 이동 상황에서도 물리 로직이 충돌하지 않도록 재설계.

Layer Matrix: Player, Dagger, Wall, Ground 간의 상호작용 우선순위 확립.

## 4. 다음 목표 (Next Milestone) 🎯
Core Movement: FSM(유한 상태 머신) 기반의 Walk / Jump 연동.

Sprite Animation: Aseprite에서 작업한 픽셀 아트 스프라이트 시트 적용 및 방향 전환(Flip) 최적화.

Stress Test: 윈도우 환경에서의 '좌클릭 + Shift' 극한 연타를 통한 예외 상황 검증.


##[미해결 버그 리스트 (To-do for Tomorrow)]

지상 점프 불능 (Grounded Detection Failure)

현상: 바닥에 서 있어도 점프 입력이 무시됨.

가설 1: Ground Cast Distance(0.12)가 너무 짧아 레이가 바닥 콜라이더에 도달 못 함.

가설 2: Ground Mask와 실제 바닥 오브젝트의 레이어 불일치.

벽 부착 중 블링크 트리거 오류

현상: 벽에 붙은 상태에서 단검은 나가지만 Shift 입력 시 블링크 이동이 발생하지 않음.

가설 1: 벽 타기 상태(isWallClimbing)가 블링크 로직의 시작 조건을 차단 중.

가설 2: 벽에 붙은 직후 AirBlinkCount가 초기화되지 않아 횟수 부족으로 판정됨.

단검 지면 미끄러짐 현상 (Dagger Friction/Collision Error)

현상: 바닥에 던진 단검이 박히지 않고 바닥을 타고 미끄러짐.

가설 1: 단검의 OnTriggerEnter2D 로직에서 Ground 레이어 충돌 시 Rigidbody를 Kinematic으로 바꾸는 처리가 누락됨.

가설 2: 단검 프리팹의 콜라이더가 너무 작아 바닥 콜라이더 사이로 '터널링' 발생.
