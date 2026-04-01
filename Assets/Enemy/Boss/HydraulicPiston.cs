using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 하운드 보스의 유압 피스톤 약점 컴포넌트 (IGrabbable 구현).
///
/// 플레이어와의 상호작용 흐름:
///   1. EnemyBossHound가 HoundExposePistonState 진입
///       → HydraulicPiston.SetExposed(true) 호출
///   2. PlayerBlinkController2D가 IsGrabbable == true 감지
///       → GrabState.SetTarget(piston) → SlamState → TakeSlamDamage → BreakPiston()
///   3. BreakPiston() → OnPistonBroken 이벤트 → EnemyBossHound.OnPistonBroken()
///
/// 설계 원칙:
///   - 피스톤 자체는 EnemyBossHound와 느슨하게 결합 (이벤트 기반).
///   - CurrentLives = 1 (파괴되면 즉시 0).
///   - 파괴 후 IsGrabbable = false — 중복 그랩 방지.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HydraulicPiston : MonoBehaviour, IGrabbable
{
    // ─── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 피스톤 파괴 완료 시 발화.
    /// EnemyBossHound가 구독하여 BrokenPistonCount를 증가시킨다.
    /// </summary>
    public UnityEvent OnPistonBroken;

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    /// <summary>노출 상태 && 아직 파괴되지 않은 경우에만 true.</summary>
    public bool IsGrabbable  => _isExposed && !_isBroken && !_isLockedForGrab;

    /// <summary>파괴되면 0, 아니면 1.</summary>
    public int  CurrentLives => _isBroken ? 0 : 1;

    // ─── 상태 ─────────────────────────────────────────────────────────────────

    private bool _isExposed;
    private bool _isBroken;
    private bool _isLockedForGrab;

    // ─── 공개 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Phase 2 진입 시 EnemyBossHound가 호출. 그랩 가능 상태로 전환.
    /// </summary>
    public void SetExposed(bool exposed)
    {
        if (_isBroken) return;
        _isExposed = exposed;
    }

    // ─── IHealth ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>일반 데미지는 피스톤에 효과 없음. 슬램 전용.</remarks>
    public void TakeDamage(float damage) { }

    /// <inheritdoc/>
    public void Die() => BreakPiston();

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void LockForGrab()
    {
        _isLockedForGrab = true;
        // Collider 비활성화로 연속 충돌 방지
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    /// <inheritdoc/>
    public void ReleaseGrab(bool executePendingDeath)
    {
        _isLockedForGrab = false;

        if (executePendingDeath)
            BreakPiston();
    }

    /// <inheritdoc/>
    /// <remarks>피스톤 파괴 트리거. SlamState.ExecuteTargetDeath()에서 호출.</remarks>
    public void TakeSlamDamage(float damage)
    {
        BreakPiston();
    }

    // ─── 파괴 처리 ────────────────────────────────────────────────────────────

    private void BreakPiston()
    {
        if (_isBroken) return;
        _isBroken = true;
        _isExposed = false;

        // TODO(작성자): 파괴 이펙트 / 애니메이션 재생 — 2026-04-01
        OnPistonBroken?.Invoke();

        // 피스톤 메시 비활성화 (GameObject는 이벤트 수신 유지를 위해 유지)
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = false;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }
}
