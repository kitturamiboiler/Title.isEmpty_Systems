using System.Collections;
using UnityEngine;

/// <summary>
/// 구속 와이어 발사 패턴.
///
/// 흐름:
///   예고 정지(telegraph) → FireBindProjectile → Vulnerable 대기
///
/// 구속 탈출 감지:
///   이 State는 플레이어가 구속에서 탈출했는지 직접 모니터링하지 않는다.
///   탈출 여부는 PlayerBoundState가 자체 처리 (FSM 결합도 최소화).
///
/// Phase 3:
///   와이어 발사 후 동시에 WireWhip도 발사 → 양쪽을 동시에 처리해야 함.
/// </summary>
public class BrotherBindState : BossState
{
    private readonly EnemyBossBrother _brother;
    private Coroutine _routine;

    private const float TELEGRAPH_DURATION = 0.9f;

    public BrotherBindState(EnemyBossBrother brother) : base(brother)
    {
        _brother = brother;
    }

    public override void Enter()
    {
        if (_brother.Rb != null)
            _brother.Rb.linearVelocity = Vector2.zero;

        _routine = Machine.StartCoroutine(BindRoutine());
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

    private IEnumerator BindRoutine()
    {
        // 예고 — 플레이어에게 회피 기회 제공
        Machine.FacePlayer();
        float telegraph = TELEGRAPH_DURATION * (IsPhase3 ? 0.65f : 1f);
        yield return new WaitForSeconds(telegraph);

        // 와이어 발사
        Vector2 dir = Machine.GetDirectionToPlayer();
        _brother.FireBindProjectile(dir);

        // Phase 3: 동시에 WireWhip 양방향
        if (IsPhase3)
        {
            _brother.FireWireWhip(Vector2.right);
            _brother.FireWireWhip(Vector2.left);
        }

        // 발사 후 취약 창 (플레이어가 구속 중에도 공격 가능)
        GoTo(Machine.Vulnerable);
    }
}
