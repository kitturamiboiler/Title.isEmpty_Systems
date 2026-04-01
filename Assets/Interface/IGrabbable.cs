using UnityEngine;

/// <summary>
/// 그랩-슬램 상호작용 계약.
/// IHealth를 확장하여 그랩 가능한 모든 객체는 반드시 데미지 수신 가능임을 보장한다.
///
/// 구현 규칙:
///   - EnemyHealth  : TakeSlamDamage → TakeDamage 위임 (갑옷 없음)
///   - BossHealth   : TakeSlamDamage → ArmorGauge 감소 후 체력 감소 (갑옷 관통)
///   - HydraulicPiston: TakeSlamDamage → 피스톤 파괴 처리
/// </summary>
public interface IGrabbable : IHealth
{
    // ─── 상태 쿼리 ────────────────────────────────────────────────────────────

    /// <summary>현재 그랩 가능 여부.</summary>
    bool IsGrabbable { get; }

    /// <summary>현재 남은 lives. SlamState가 마지막 일격 여부를 판단하는 데 사용.</summary>
    int CurrentLives { get; }

    /// <summary>
    /// 소유 GameObject. MonoBehaviour 구현체가 자동 제공.
    /// SlamState가 Rigidbody2D 캐싱에 사용.
    /// </summary>
    GameObject gameObject { get; }

    // ─── 그랩 소유권 ──────────────────────────────────────────────────────────

    /// <summary>GrabState 진입 시 호출. 외부 데미지·Die 차단 시작.</summary>
    void LockForGrab();

    /// <summary>
    /// GrabState/SlamState 종료 시 호출. 잠금 해제.
    /// </summary>
    /// <param name="executePendingDeath">
    /// true면 Lock 중 쌓인 사망 요청을 즉시 실행.
    /// SlamState가 직접 처리할 경우 false 전달.
    /// </param>
    void ReleaseGrab(bool executePendingDeath);

    // ─── 특수 데미지 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 슬램/처형 전용 데미지. 일반 TakeDamage와 달리:
    ///   - BossHealth의 ArmorGauge를 감소시킨다.
    ///   - 취약(IsVulnerable) 여부와 무관하게 체력을 감소시킨다.
    /// EnemyHealth 구현체는 TakeDamage를 단순 위임한다.
    /// </summary>
    void TakeSlamDamage(float damage);
}
