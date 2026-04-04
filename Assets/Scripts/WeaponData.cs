using UnityEngine;
// 탄속, 데미지, 블링크 쿨타임 
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Dagger Embed (DaggerProjectile2D)")]
    [Tooltip("박혀 고정될 표면 레이어(Ground, Wall 등). 비어 있으면 이름 Wall/Ground로 폴백.")]
    public LayerMask daggerEmbedSurfaceMask;

    [Tooltip("단검 Rigidbody2D.sharedMaterial. 비우면 탄성 0·마찰 높은 기본 머티리얼을 런타임 생성.")]
    public PhysicsMaterial2D daggerPhysicsMaterial;

    [Tooltip("박힘 직전 접점을 표면 안쪽으로 밀어 넣는 거리. 탄성·미끄럼으로 튕겨 나가는 현상 완화.")]
    [Min(0f)]
    public float daggerSurfaceEmbedInset = 0.04f;

    [Header("Blink Combat (PlayerBlinkController2D)")]
    [Tooltip("블링크 경로/도착에서 체크할 적 레이어.")]
    public LayerMask blinkEnemyLayerMask;
    [Tooltip("이동 경로 스윕(원형 캐스트) 반경.")]
    public float blinkEnemySweepRadius = 0.32f;
    [Tooltip("도착 지점 추가 오버랩 반경.")]
    public float blinkDestinationEnemyRadius = 0.4f;
    [Tooltip("IHealth 없을 때 Destroy로 즉시 제거할지.")]
    public bool blinkInstantKillDestroysEnemyWithoutHealth = true;
    [Tooltip("블링크로 적에게 피해를 줄 때의 값(척살용 고정 데미지).")]
    public float blinkExecutionDamage = 500f;
    [Tooltip("블링크로 적 1체 이상 타격 시 공중 블링크 횟수를 max로 회복.")]
    public bool blinkKillRefillsAirBlink = true;

    [Header("Dagger Stats")]
    public float damage = 10f;
    public float projectileSpeed = 20f; // 탄속 (단도 날아가는 속도)
    public float blinkCooldown = 3f;
    public float maxDistance = 15f;     // 단검이 유효한 최대 거리

    [Header("Blink Invincibility")]
    public float invincibleDuration = 0.15f;
    [Tooltip("무적 중 스프라이트 알파값 (0=완전투명, 1=불투명).")]
    [Range(0f, 1f)]
    public float invincibleAlpha = 0.5f;
    // invincibleLayerName / originalLayerName 제거 — Layers.PlayerInvincible / Layers.Player 상수 사용

    [Header("Hit Stop")]
    [Tooltip("블링크 히트 스톱 유지 시간(초). 0.05 권장.")]
    public float hitStopDuration = 0.05f;
    [Tooltip("히트 스톱 중 timeScale 배율. 0에 가까울수록 강한 슬로우.")]
    [Range(0.01f, 1f)]
    public float hitStopTimeScale = 0.1f;

    [Header("Blink Wall Offset")]
    [Tooltip("벽에 박힌 단검으로 블링크 시 법선 방향으로 밀어내는 기본 거리.")]
    public float blinkWallSafeOffset = 0.5f;
    [Tooltip("벽 끼임 방지 추가 여유 거리.")]
    public float blinkWallSafetyMargin = 0.02f;

    [Header("Air Throw (헬리콥터 방지)")]
    [Tooltip("공중 투척 시 강제 하강 최저 속도(양수 입력, 내부에서 음수 적용).")]
    public float airThrowFallSpeed = 2f;

    [Header("Visuals")]
    public Sprite weaponSprite;
    public ParticleSystem hitParticle;
    public ParticleSystem blinkEffectPrefab; // 블링크 히트 스톱용 짧은 이펙트
    public float daggerTrailTime = 0.08f; // 단도 날아가는 궤적 속도감 
    public float ghostDuration = 0.1f; // 척살 후 반 투명화
    public float cameraShakeDuration = 0.06f; // 카메라 앵글 흔들림 지속
    public float cameraShakeIntensity = 0.08f; // 카메라 애글 흔들림

    [Header("Slam (SlamState)")]
    [Tooltip("급강하 속도. 음수로 지정. 절대값이 클수록 빠른 하강.")]
    public float slamVerticalVelocity = -30f;
    [Tooltip("blinkEnemyLayerMask와 독립된 슬램 범위 데미지 대상 레이어.")]
    public LayerMask slamEnemyLayerMask;
    [Tooltip("BoxCast 예측 거리 추가 여유값. 얇은 플랫폼 대비 완충재.")]
    public float slamCastMargin = 0.1f;
    [Tooltip("그랩 후 입력 없을 때 자동 슬램 발동까지 대기 시간(초).")]
    public float slamAutoTriggerTime = 0.3f;
    [Tooltip("슬램 착지 시 데미지. 1.0 = Lives 1 감소.")]
    public float slamDamage = 1.0f;
    [Tooltip("착지 충격파 범위(유닛). 주변 적 범위 데미지 반경.")]
    public float slamRadius = 2.5f;

    [Header("Slam Bounce")]
    [Tooltip("슬램 착지 후 Space 입력 시 위로 튀어오르는 속도.")]
    public float slamBounceForce = 20f;
    [Tooltip("슬램 착지 후 바운스 입력을 받는 유효 시간(초). 이 시간 안에 Space를 눌러야 한다.")]
    public float slamBounceWindowDuration = 0.35f;

    [Header("Slam Collateral")]
    [Tooltip("슬램 피해자를 날렸을 때 2차 피해자에게 가하는 힘. 클수록 멀리 날린다.")]
    public float slamCollateralForce = 14f;

    [Header("Slam Juice — HitStop + Zoom + Squash")]
    [Tooltip("슬램 착지 순간 히트스톱 지속(초, unscaled). 0이면 비활성.")]
    public float slamHitStopDuration = 0.16f;
    [Tooltip("슬램 히트스톱 timeScale. 낮을수록 ‘멈춤’이 강함 (0.04~0.12 권장).")]
    [Range(0.01f, 1f)]
    public float slamHitStopTimeScale = 0.06f;
    [Tooltip("Orthographic 카메라 줌 인(orthographicSize 감소량). 0이면 비활성. 임팩트 ‘펀치’용.")]
    public float slamCameraOrthoZoomIn = 0.45f;
    [Tooltip("줌 인 후 원래 크기로 복귀하는 시간(초, unscaled).")]
    public float slamOrthoZoomRecoverDuration = 0.28f;
    [Tooltip("슬램 순간 스프라이트 가로 스트레치 배율.")]
    public float slamVisualSquashStretchX = 1.14f;
    [Tooltip("슬램 순간 스프라이트 세로 압축 배율.")]
    public float slamVisualSquashCompressY = 0.68f;
    [Tooltip("스쿼시 연출 전체 길이(초, unscaled).")]
    public float slamVisualSquashDuration = 0.14f;

    [Header("Camera Shake — Slam")]
    [Tooltip("슬램 착지 시 카메라 셰이크 지속 시간. 히트스톱과 겹쳐도 unscaled로 잘 보임.")]
    public float slamShakeDuration  = 0.22f;
    [Tooltip("슬램 착지 시 카메라 셰이크 강도.")]
    public float slamShakeIntensity = 0.42f;

    [Header("Camera Shake — Grab")]
    [Tooltip("그랩 순간 짧은 충격 셰이크 지속 시간 (0.05 권장).")]
    public float grabShakeDuration  = 0.05f;
    [Tooltip("그랩 순간 짧은 충격 셰이크 강도 (0.12 권장).")]
    public float grabShakeIntensity = 0.12f;

    [Header("Parry (PlayerParryController2D)")]
    [Tooltip("패리 판정 반지름. 플레이어 중심 기준 OverlapCircle 반경.")]
    public float parryRadius = 1.2f;

    [Tooltip("패리 버튼 입력 후 판정이 유지되는 시간 (초). 너무 길면 연타 패널티 실종.")]
    public float parryActiveDuration = 0.12f;

    [Tooltip("패리 성공·실패 후 재사용 대기 시간 (초).")]
    public float parryCooldown = 0.5f;

    [Tooltip("패리 성공 1회당 공중 블링크 충전량. 1 = 블링크 1회 회복.")]
    public int parryAirBlinkRefill = 1;

    [Tooltip("패리 성공 시 HitStop 요청 시간 (초).")]
    public float parryHitStopDuration = 0.06f;

    [Tooltip("패리 성공 시 HitStop timeScale.")]
    [Range(0.01f, 1f)]
    public float parryHitStopTimeScale = 0.05f;

    [Header("Parry — Melee (IParryableMelee)")]
    [Tooltip("근접 패리 감지 반경. 투사체 parryRadius보다 작게 유지하는 것을 권장.")]
    public float parryMeleeRadius = 0.9f;
    [Tooltip("근접 패리 성공 시 적이 그랩 가능 상태를 유지하는 시간(초). 너무 길면 보스전 밸런스 붕괴.")]
    public float parryMeleeStunDuration = 1.0f;
    [Tooltip("근접 패리 성공 시 적에게 가하는 위쪽 방향 힘. 블링크-그랩 연계를 위해 약간 공중으로 띄운다.")]
    public float parryMeleeLaunchForce = 9f;

    [Header("Prefabs")]
    public GameObject daggerProjectilePrefab; // 2D 단검 투사체 프리팹
}
