using UnityEngine;

/// <summary>
/// 보스 전체 수치 공급원 (ScriptableObject).
/// 5인의 시련 공통 구조를 담으며, 보스별 특수 수치는 서브클래스 또는 별도 SO로 확장한다.
///
/// Create > Game > Boss Data 로 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewBossData", menuName = "Game/Boss Data")]
public class BossData : ScriptableObject
{
    // ──────────────────────────────── 기본 체력 / 페이즈 ──────────────────────

    [Header("Health & Phase Thresholds")]
    [Tooltip("최대 Lives. 보스별 권장: Hound=6, Paper=4, Brother=5, Shadow=3, Designer=8")]
    public int maxLives = 6;

    [Tooltip("이 Lives 이하로 떨어지면 Phase 2 전환.")]
    public int phase2LivesThreshold = 4;

    [Tooltip("이 Lives 이하로 떨어지면 Phase 3 전환 (최종 페이즈).")]
    public int phase3LivesThreshold = 2;

    [Tooltip("이 Lives 이하이고 ArmorGauge == 0 일 때 IsGrabbable = true.")]
    public int grabbableLivesThreshold = 1;

    // ──────────────────────────────── 갑옷 게이지 ─────────────────────────────

    [Header("Armor Gauge")]
    [Tooltip("최대 갑옷 게이지. 0이면 갑옷 없음 (잡몹용 보스에 사용).")]
    public float maxArmorGauge = 3f;

    [Tooltip("Execution/Slam 타격 1회당 갑옷 감소량.")]
    public float armorDamagePerExecution = 1f;

    [Tooltip("이 값 이상의 데미지만 갑옷에 영향을 준다 (블링크 처형 500 >> 일반 단검 10).")]
    public float armorBreakThreshold = 100f;

    // ──────────────────────────────── 이동 ────────────────────────────────────

    [Header("Movement")]
    [Tooltip("플레이어 감지 범위.")]
    public float detectionRange = 12f;

    [Tooltip("Idle/순찰 이동 속도.")]
    public float patrolSpeed = 2.5f;

    [Tooltip("Phase 2 이상에서 모든 속도 계수에 곱하는 배율.")]
    public float phase2SpeedMultiplier = 1.3f;

    [Tooltip("Phase 3 이상에서 모든 속도 계수에 곱하는 배율.")]
    public float phase3SpeedMultiplier = 1.6f;

    // ──────────────────────────────── 취약 창 ─────────────────────────────────

    [Header("Vulnerable Window")]
    [Tooltip("Phase 1 공격 후 피격 가능 창 (초).")]
    public float vulnerableDurationPhase1 = 1.5f;

    [Tooltip("Phase 2 피격 가능 창 (초).")]
    public float vulnerableDurationPhase2 = 1.8f;

    [Tooltip("Phase 3 피격 가능 창 (초). Shield 파괴 후에는 이 값 사용.")]
    public float vulnerableDurationPhase3 = 2.2f;

    // ──────────────────────────────── 공통 투사체 ─────────────────────────────

    [Header("Projectile (Common)")]
    [Tooltip("기본 투사체 프리팹. BossProjectile2D 컴포넌트 필수.")]
    public GameObject projectilePrefab;

    [Tooltip("패리 가능 투사체 프리팹. BossParryableProjectile2D 컴포넌트 필수.")]
    public GameObject parryableProjectilePrefab;

    [Tooltip("투사체 이동 속도.")]
    public float projectileSpeed = 9f;

    [Tooltip("투사체 기본 데미지.")]
    public float projectileDamage = 1f;

    [Tooltip("투사체 최대 생존 시간 (초).")]
    public float projectileLifetime = 5f;

    // ──────────────────────────────── 공통 패턴 파라미터 ──────────────────────

    [Header("Bullet Wall Pattern")]
    public int    bulletWallCount             = 5;
    public float  bulletWallSpread            = 50f;
    public int    bulletWallWaves             = 3;
    public float  bulletWallWaveDelay         = 0.45f;
    public float  bulletWallTelegraphDuration = 0.7f;

    [Header("Charge Pattern")]
    public float chargeSpeed             = 14f;
    public float chargeDuration          = 1.1f;
    public float chargeTelegraphDuration = 0.8f;

    [Header("Ground Pound Pattern")]
    public float groundPoundJumpForce    = 16f;
    public float groundPoundFallSpeed    = -28f;
    public float groundPoundRadius       = 3.2f;
    public float groundPoundDamage       = 1f;
    public float groundPoundFallTimeout  = 2.5f;

    // ──────────────────────────────── 아머 재생 ───────────────────────────────

    [Header("Armor Regen (가설 3 방어)")]
    [Tooltip("마지막 블링크/실행 타격 이후 갑옷 재생이 시작되기까지의 지연 시간 (초).\n" +
             "0이면 재생 없음. 권장값: 3~5초 — '몰아치는 전투' 유도.")]
    public float armorRegenDelay = 4f;

    [Tooltip("갑옷 재생 속도 (gauge/초). 지연 후 서서히 회복.\n" +
             "maxArmorGauge=3, rate=0.5 → 6초 완전 회복. 0이면 재생 없음.")]
    public float armorRegenRate  = 0.5f;

    // ──────────────────────────────── HitStop ─────────────────────────────────

    [Header("HitStop Response")]
    [Tooltip("보스가 피격 시 HitStopManager에 요청할 히트스톱 시간 (초). 0이면 비활성.")]
    public float hitStopDurationOnHit    = 0.04f;

    [Tooltip("보스 피격 히트스톱 timeScale.")]
    [Range(0.01f, 1f)]
    public float hitStopTimeScaleOnHit   = 0.15f;

    // ──────────────────────────────── 헬퍼 ────────────────────────────────────

    /// <summary>현재 Phase에 맞는 속도 배율 반환.</summary>
    public float GetSpeedMultiplier(BossPhase phase)
    {
        return phase switch
        {
            BossPhase.Phase3 => phase3SpeedMultiplier,
            BossPhase.Phase2 => phase2SpeedMultiplier,
            _                => 1f,
        };
    }

    /// <summary>현재 Phase에 맞는 취약 창 길이 반환.</summary>
    public float GetVulnerableDuration(BossPhase phase)
    {
        return phase switch
        {
            BossPhase.Phase3 => vulnerableDurationPhase3,
            BossPhase.Phase2 => vulnerableDurationPhase2,
            _                => vulnerableDurationPhase1,
        };
    }
}
