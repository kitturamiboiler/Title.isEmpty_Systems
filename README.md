# Title.isEmpty_Systems
Advanced 2D Blink Mechanics with Hit-stop, I-frame, and Normal-offset in Unity.

📝 개발 일지: 2026-03-27
[단검 블링크: 절대적 조작성과 방어적 설계의 완성]

## 1. 핵심 구현 내용 (The Spike) 🚀
### 조준 시스템 강화 (Ray-Plane Cast)
Z=0 평면 기준 마우스 레이캐스트 좌표 계산 방식을 도입했습니다.

Future-proofing: 카메라 방식(원근/평행)에 관계없이 커서 끝에 단검이 박히는 절대 명중률을 확보했습니다.

### 비주얼 주스 (Juice) 심화
Ghost Fade-out: 검붉은(#800000) 잔상이 시간이 지남에 따라 알파값이 흐려지며 소멸하는 고급 연출을 적용했습니다.

Dagger Trail: 투사체 뒤에 0.08초의 강렬한 트레일을 남겨 탄속 체감을 극대화했습니다.

### 캐릭터 Flip & 사출 동기화
사출 직전 1프레임 내에 **캐릭터 방향 전환(Flip)**을 강제 실행하여, 발사 위치(FirePoint)가 꼬이는 시각적 버그를 원천 차단했습니다.

## 2. 기술적 방어 포인트 (Anti-Bug) 🛡️
### 오발사 (Misfire) 방어 로직
입력 분리: 점프는 '예약(Buffering)'을 허용하되, 단검 블링크는 '즉시 실행'만 허용하여 조작 오염을 방지했습니다.

장부 파기: 지면이나 벽에 접촉하는 순간(OnCollisionEnter2D), 남아있을지 모를 모든 예약 입력을 즉시 초기화(Clear)합니다.

### 물리 Stuck (끼임) 방지
CalculateWallSafeOffset 로직을 도입했습니다.

단순 수치가 아닌, **캐릭터 콜라이더의 실제 크기(Extents)**를 계산하여 벽 내부로 텔레포트되는 사고를 수학적으로 방지합니다.

## 3. 시스템 구조 및 설정 (Setup) ⚙️
[Key Principle]
"단순한 이동을 '공간을 찢는 액션'으로 승화시키다."

### 📊 WeaponData 튜닝 가이드 (Inspector)
coyoteTime (0.1s): 낭떠러지 끝 조작 유예 시간

inputBufferTime (0.1s): 착지 전 점프 선입력 유효 시간

ghostDuration (0.1s): 잔상 유지 및 페이드아웃 시간

cameraShake (0.08): 블링크 성공 시 임팩트 수치

## 4. 향후 과제 (Next Steps) 🎯
LayerMask 기반 최적화: 문자열(Tag) 비교 대신 비트 연산 방식을 적용하여 물리 엔진 부하 감소.

Stress Test: 윈도우 환경에서 '좌클릭 + Shift' 난사를 통한 로직 한계점 검증.
