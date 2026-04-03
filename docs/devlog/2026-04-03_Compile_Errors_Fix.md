# 📋 Devlog — 2026-04-03 · 컴파일 / Safe Mode 에러 정리

> **목적**: Unity Safe Mode 및 CSxxxx 컴파일 실패 해소  
> **핵심 키워드**: 패키지 누락, API 정합성, null-coalescing 타입, 미사용 필드

---

## 개요

에디터가 Safe Mode로 기동되거나 빌드가 막히는 원인을 **패키지 → 스크립트 시그니처 → 타입 일치** 순으로 정리해 수정했다.

---

## 1. 패키지 (manifest)

| 항목 | 내용 |
|------|------|
| `com.unity.ugui` | `2.0.0` — UI 누락으로 발생하던 `CS0246` 등 완화 |
| `com.unity.textmeshpro` | `3.0.9` — TMP 참조 복구 |

---

## 2. 스크립트별 수정 요약

| 파일 / 영역 | 증상 | 조치 |
|-------------|------|------|
| `ArmJoint.cs` | `CS0108` — `gameObject` 멤버가 부모와 충돌 | `new` 키워드 정리(또는 명시적 숨김 처리) |
| `DaggerProjectile2D.cs` | 존재하지 않는 필드 참조 | `weaponData.speed` → `weaponData.projectileSpeed` |
| `StoryDatabase.cs` | `CS0103` `Debug` | `using UnityEngine;` 추가 |
| `BossStateMachine.SpawnParryableProjectile` | `void` 대입 `CS0815` | 반환형을 `BossParryableProjectile2D`로 명시 |
| `DesignerWaveState`, `HoundShockwaveState`, `PaperDroneShieldState`, `ShadowHauntState` | `??` 좌우 타입 불일치 | `(… as IBossState) ?? …` 형태로 통일 |
| `CombatDialogueUI.cs` | `CS0414` 미사용 필드 | `DialoguePriority.Combat` 분기에서 `_urgentHoldTime` 사용 등으로 해소 |

---

## 3. 잔여 메모

- **Physics2D.*NonAlloc** Obsolete 경고는 Unity 6 API 마이그레이션 시 별도 대응 가능.
- **프리팹/씬** 유실은 코드 밖 작업 — 에디터에서 재할당 필요.

---

## 4. 관련 커밋 / 후속

- 동일 일자 devlog [`2026-04-03_Infra_Ground_Pool_Sound_Animator.md`](./2026-04-03_Infra_Ground_Pool_Sound_Animator.md) — 에러 수정 이후 진행한 인프라 작업.
