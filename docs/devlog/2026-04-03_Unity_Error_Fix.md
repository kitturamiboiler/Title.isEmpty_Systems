# 📋 Devlog
---

## 2026-04-03_유니티 에러 픽스

### 1. DaggerProjectile2D.cs
> WeaponData에는 speed 없음 → projectileSpeed 사용.

### 2. StoryDatabase.cs
> 에디터 검증에서 Debug 사용 → 파일 상단에 using UnityEngine; 추가.

### 3. BossStateMachine.SpawnParryableProjectile
> 반환이 void라 var proj = ...가 깨짐 → BossParryableProjectile2D를 반환하도록 바꿈(실패 시 null). Launch 후에도 같은 참조를 돌려준다.

### 4. ?? 타입 불일치 (CS0019)
> DesignerUmbrellaPhaseState vs BossVulnerableState처럼 서로 다른 구체 타입이라 ??가 안 됨 →
> (… as IBossState) ?? Machine.Vulnerable 형태로 통일.

> DesignerWaveState.cs (2곳)
> HoundShockwaveState.cs (2곳)
> PaperDroneShieldState.cs
> ShadowHauntState.cs

### 5. CombatDialogueUI.cs (경고 CS0414)
> Combat 우선순위일 때 기본 유지 시간으로 _urgentHoldTime 쓰도록 연결.