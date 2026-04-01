using System.Collections;
using UnityEngine;

/// <summary>
/// 하운드 Phase 1/3 수평 돌진 패턴.
/// 예고(Telegraph) → 돌진 → 완료 후 HoundClaw로 전이.
/// Phase 3는 charge 속도가 phase3SpeedMultiplier 배 상승.
/// </summary>
public class HoundChargeState : BossState
{
    private readonly EnemyBossHound _hound;
    private Coroutine _routine;

    private const float WALL_CHECK_DIST   = 0.3f;
    private const float WALL_CHECK_HEIGHT = 0.5f;

    public HoundChargeState(EnemyBossHound hound) : base(hound)
    {
        _hound = hound;
    }

    public override void Enter()
    {
        if (_hound.Rb == null)
        {
            Debug.LogError("[HoundChargeState] Rigidbody2D missing.");
            GoTo(Machine.Vulnerable);
            return;
        }

        Machine.FacePlayer();
        _routine = Machine.StartCoroutine(ChargeRoutine());
    }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }

        if (_hound.Rb != null)
            _hound.Rb.linearVelocity = Vector2.zero;
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator ChargeRoutine()
    {
        if (Data == null) { GoTo(Machine.Vulnerable); yield break; }

        // 예고 정지
        _hound.Rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(Data.chargeTelegraphDuration);

        // 돌진
        Vector2 dir   = _hound.GetDirectionToPlayer();
        dir.y          = 0f;
        float speed    = _hound.GetChargeSpeed();
        _hound.Rb.linearVelocity = dir.normalized * speed;

        float elapsed = 0f;
        while (elapsed < Data.chargeDuration)
        {
            // 벽 충돌 조기 종료
            if (IsHittingWall(dir.normalized))
                break;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _hound.Rb.linearVelocity = Vector2.zero;

        // 다음 패턴: Claw 근접 콤보
        GoTo(_hound.HoundClaw);
    }

    private bool IsHittingWall(Vector2 dir)
    {
        if (_hound.Col == null) return false;

        Vector2 origin = new Vector2(
            _hound.Rb.position.x,
            _hound.Rb.position.y + WALL_CHECK_HEIGHT
        );

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            dir,
            WALL_CHECK_DIST,
            1 << Layers.Wall
        );

        return hit.collider != null;
    }
}
