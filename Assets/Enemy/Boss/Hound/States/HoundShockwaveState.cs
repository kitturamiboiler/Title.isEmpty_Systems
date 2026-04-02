using System.Collections;
using UnityEngine;

/// <summary>
/// Hound Phase 2 진입 직후 실행되는 충격파 압박 패턴.
///
/// 흐름:
///   1. 전조 (TelegraphDuration): 하운드가 지면을 긁으며 경고
///   2. 지면 충격: 양 방향으로 BossProjectile2D 충격파 발사
///   3. HitStop 트리거
///   4. WaitAfterShockwave 후 → HoundExposePistonState 전이
///
/// 설계 의도:
///   피스톤이 노출되기 직전 플레이어를 강제로 블링크하게 만든다.
///   Phase 2 진입 시 '뭔가 달라졌다'는 압박감 제공.
///
/// 방어 설계:
///   - PlayerTransform null → 즉시 HoundExposePiston으로 스킵
///   - SpawnProjectile null 방어는 BossStateMachine.SpawnProjectile에서 처리
/// </summary>
public class HoundShockwaveState : BossState
{
    private readonly EnemyBossHound _hound;
    private Coroutine _routine;

    public HoundShockwaveState(BossStateMachine machine) : base(machine)
    {
        _hound = machine as EnemyBossHound;
        if (_hound == null)
            Debug.LogError("[HoundShockwaveState] BossStateMachine이 EnemyBossHound가 아닙니다.");
    }

    public override void Enter()
    {
        if (Machine.Rb != null)
            Machine.Rb.linearVelocity = Vector2.zero;

        if (Machine.PlayerTransform == null)
        {
            GoTo(_hound?.HoundExposePiston ?? Machine.Vulnerable);
            return;
        }

        _routine = Machine.StartCoroutine(ShockwaveRoutine());
    }

    public override void Tick()   { }
    public override void FixedTick() { }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator ShockwaveRoutine()
    {
        // ── 전조: 하운드가 진동 (0.8초) ──────────────────────────────────────
        yield return new WaitForSeconds(_hound?.ShockwaveTelegraphTime ?? 0.8f);

        // ── 충격파: 좌우 동시 발사 ────────────────────────────────────────────
        Machine.SpawnProjectile(Vector2.right);
        Machine.SpawnProjectile(Vector2.left);

        // ── 히트스톱 ─────────────────────────────────────────────────────────
        HitStopManager.Instance?.Request(
            Data?.hitStopDurationOnHit ?? 0.06f,
            Data?.hitStopTimeScaleOnHit ?? 0.1f
        );

        // ── 충격파가 화면을 가로지를 시간 대기 ───────────────────────────────
        yield return new WaitForSeconds(_hound?.ShockwaveWaitAfter ?? 1.2f);

        // ── 피스톤 노출 단계로 전이 ───────────────────────────────────────────
        GoTo(_hound?.HoundExposePiston ?? Machine.Vulnerable);
    }
}
