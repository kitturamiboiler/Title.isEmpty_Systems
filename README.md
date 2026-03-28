# 🗡️ Title.isEmpty | 개발일지

## 2026-03-28 (토) — Dagger Blink System 구현

### 오늘 한 것
- Unity 2D 프로젝트 세팅 (씬, 레이어, Physics 2D)
- 단검 투척 및 블링크 로직 구현
- 공중 블링크 1회 제한 (착지/벽 닿으면 초기화)
- 벽 끼임 방지 Wall Safe Offset
- 히트스톱 + 카메라 쉐이크
- 잔상 이펙트 Ghost Fade (#800000)
- 블링크 I-frame 무적 처리

### 버그 픽스
- 단검이 Player 콜라이더에 충돌해 블링크 카운트 리셋되던 문제 → Dagger 레이어 분리로 해결
- 공중 블링크 무한 사용 버그 → 던질 때 카운트 차감으로 수정
- 단검 벽 뚫림 → Continuous Collision Detection 적용

### 다음 목표
- 플레이어 이동 (Walk / Jump) 구현
- FSM 적용
- 픽셀아트 스프라이트 연동
