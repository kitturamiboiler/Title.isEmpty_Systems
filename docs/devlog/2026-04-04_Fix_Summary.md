# 📋 Devlog — 2026-04-04 · 정리 · Unity 1~3단계 · 단검 물리

> **목적**: Git/README 정리, 외부 패치 검토, **그레이박스용 씬·에셋 구조 정돈**, 단검 지면 미끄럼 수정  
> **핵심 키워드**: rebase, `Assets/Scripts`·`Scenes`·`Art`·`Data`, Pixel Perfect, `DaggerProjectile2D`, Physics2D

---

## 개요

유니티 에디터에서 **플레이 가능한 테스트 씬**을 만들기 위한 1~3단계(씬·플레이어·매니저·단검 루프)를 진행하면서 폴더·패키지·프리팹이 정리되었고, 단검이 지면에서 미끄러지던 현상을 코드로 보강했다.

---

## 1. Git — `feature/Unity` rebase 및 README

| 항목 | 내용 |
|------|------|
| 상황 | `feature/Unity` rebase 중 `README.md` **both modified** |
| 조치 | 2026-04-03 devlog 링크 세 줄 병합 (`Unity_Error_Fix` 등) |
| 결과 | rebase 완료 |

---

## 2. 외부 스크립트 초안(Claude 등) vs 현재 레포

| 판단 |
|------|
| NonAlloc 경로 유지 — `List` 할당 버전은 통째 적용하지 않음 |
| `Desktop/files` 류는 백업·참고만 |

---

## 3. 코드 수정

| 파일 | 내용 |
|------|------|
| `Player/PlayerBlinkController2D.cs` | `OnBlinkExecuted` **중복 `/// <summary>`** 제거 |
| `Dagger/DaggerProjectile2D.cs` | 지면에서 튕김·미끄럼 완화: `Continuous` 검출, 무탄성 기본 `PhysicsMaterial2D`, `PinToContact`, 임베드 시 각속도·회전 고정 |
| `Scripts/WeaponData.cs` | `daggerPhysicsMaterial`, `daggerSurfaceEmbedInset` 추가 |

---

## 4. Unity 에디터 작업 — 1~3단계에 해당하는 변경 (커밋 `feat: UnitySecene Woring` 기준)

이 절은 **Git에 추적되는 경로만** 적는다. `.gitignore`에 걸리는 캐시·로그·에디터 로컬 산출물은 일지에 쓰지 않는다.

### 4.1 폴더·에셋 구조

| 경로 | 내용 |
|------|------|
| `Assets/Scenes/` | `TestScene.unity` 이동, **`Test_Arena.unity`** 추가 — 그레이박스·전투 테스트용 |
| `Assets/Art/Materials/` | `DashMat.mat` 이동 |
| `Assets/Art/Sprites/` | `WhiteDash_Asset.png` 이동, **`spr_Tile_Base.png`** 추가 |
| `Assets/Data/` | **`NewWeaponData.asset`** — 루트의 `NewWeaponData.asset` 대체·이동 |
| `Assets/Scripts/` | **`Layers.cs`**, **`WeaponData.cs`**, **`MovementData.cs`**, **`CameraFollow.cs`**, **`EnemyLineOfSight2D.cs`** 등 베이스 스크립트 이동 (루트 혼잡 감소) |
| `Assets/Sound/` | 폴더만 생성(메타) — SFX 연결 예정 자리 |
| 삭제 | `Assets/GroundPalette.prefab`, 루트 `NewWeaponData.asset` |

### 4.2 프리팹·씬 연동

| 항목 | 내용 |
|------|------|
| `Prefabs/Dagger.prefab` | 단검 프리팹 갱신 (`DaggerProjectile2D`·물리 설정 반영) |
| 씬 | 플레이어·바닥·`WeaponData`·매니저 배치 등 **1~3단계**에 맞춰 구성 |

### 4.3 패키지·프로젝트 설정

| 항목 | 내용 |
|------|------|
| `Packages/manifest.json` | **`com.unity.2d.pixel-perfect`** `5.1.1` 추가 — 픽셀 아트 카메라 정수 스케일 등 |
| `ProjectSettings/Physics2DSettings.asset` | 물리 2D 설정 조정 |
| `ProjectSettings/TagManager.asset` | 레이어·태그 정리(씬과 맞춤) |

### 4.4 체크리스트 대응 (당시 세션 기준)

1. **씬 + 플레이어 + 바닥** — `TestScene` / `Test_Arena`  
2. **HitStop / CameraShaker / SoundManager** — 씬에 배치·참조 연결  
3. **단검 루프** — 프리팹 + `WeaponData` + (선택) 풀  

---

## 5. 후속 (4단계 이후)

- Animator 컨트롤러에 `PlayerAnimHashes` 트리거 이름 맞추기  
- `SoundManager` 클립 채우기  
- 타일맵·본격 아트는 `spr_Tile_Base` 등을 기준으로 확장  

---

## 6. 관련 문서

- [`2026-04-03_Compile_Errors_Fix.md`](./2026-04-03_Compile_Errors_Fix.md)  
- [`2026-04-03_Infra_Ground_Pool_Sound_Animator.md`](./2026-04-03_Infra_Ground_Pool_Sound_Animator.md)
