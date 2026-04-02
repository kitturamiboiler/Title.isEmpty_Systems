using System.Collections;
using UnityEngine;

/// <summary>
/// 하운드 근접 할퀴기 콤보 패턴.
/// Charge 후 연속으로 실행. 범위 내 플레이어에게 ClawDamage 적용.
/// 완료 후 BossVulnerableState로 전이 → 피격 가능 창.
///
/// Phase 3에서는 쿨다운이 절반으로 단축된다.
/// </summary>
public class HoundClawState : BossState
{
    private readonly EnemyBossHound _hound;
    private Coroutine _routine;

    private const int COMBO_COUNT = 2;

    public HoundClawState(EnemyBossHound hound) : base(hound)
    {
        _hound = hound;
    }

    public override void Enter()
    {
        _routine = Machine.StartCoroutine(ClawRoutine());
    }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator ClawRoutine()
    {
        float cooldown = _hound.ClawCooldown * (IsPhase3 ? 0.5f : 1f);

        for (int i = 0; i < COMBO_COUNT; i++)
        {
            Machine.FacePlayer();
            ApplyClaw();
            yield return new WaitForSeconds(cooldown);
        }

        GoTo(Machine.Vulnerable);
    }

    private void ApplyClaw()
    {
        if (Machine.PlayerTransform == null) return;

        float dist = Vector2.Distance(
            Machine.transform.position,
            Machine.PlayerTransform.position
        );

        if (dist <= _hound.ClawRange)
        {
            var health = Machine.PlayerTransform.GetComponentInParent<IHealth>();
            health?.TakeDamage(_hound.ClawDamage);

            HitStopManager.Instance?.Request(
                Data != null ? Data.hitStopDurationOnHit : 0.05f,
                Data != null ? Data.hitStopTimeScaleOnHit : 0.1f
            );
        }
    }
}
