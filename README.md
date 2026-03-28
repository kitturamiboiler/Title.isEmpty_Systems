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

Layer Matrix: Player, Dagger, Wall, Ground 간의 상호작용 우선순위 확립.

## 4. 다음 목표 (Next Milestone) 🎯
Core Movement: FSM(유한 상태 머신) 기반의 Walk / Jump 연동.

Sprite Animation: Aseprite에서 작업한 픽셀 아트 스프라이트 시트 적용 및 방향 전환(Flip) 최적화.

Stress Test: 윈도우 환경에서의 '좌클릭 + Shift' 극한 연타를 통한 예외 상황 검증.
