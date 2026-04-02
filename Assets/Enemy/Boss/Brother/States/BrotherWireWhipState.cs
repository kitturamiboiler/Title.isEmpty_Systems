using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 2+ 와이어 채찍 패턴 (Parryable).
///
/// 메카닉:
///   보스가 수평 방향으로 느린 패리 가능 투사체를 발사한다.
///   플레이어 선택:
///     ① 패리(Q) → 반사 → 형제에게 명중 → ArmorDamage
///     ② 점프 회피 → 안전, 하지만 블링크 충전 불가
///
/// Phase 2: 플레이어 방향 단일 채찍
/// Phase 3: 좌우 동시 2발 (이 State가 직접 처리하지 않음 — BrotherBindState가 호출)
///
/// 완료 후 BrotherBind로 전이 — Bind와 Whip의 조합이 핵심 압박.
/// </summary>
public class BrotherWireWhipState : BossState
{
    private readonly EnemyBossBrother _brother;
    private Coroutine _routine;

    private const float TELEGRAPH_DURATION = 0.6f;
    private const float POST_FIRE_WAIT     = 0.3f;

    public BrotherWireWhipState(EnemyBossBrother brother) : base(brother)
    {
        _brother = brother;
    }

    public override void Enter()
    {
        if (_brother.Rb != null)
            _brother.Rb.linearVelocity = Vector2.zero;

        _routine = Machine.StartCoroutine(WhipRoutine());
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

    private IEnumerator WhipRoutine()
    {
        Machine.FacePlayer();
        yield return new WaitForSeconds(TELEGRAPH_DURATION);

        // 플레이어 방향 수평 채찍
        Vector2 dir = Machine.GetDirectionToPlayer();
        dir.y = 0f;  // 수평 고정 — 점프로 회피 가능하게
        _brother.FireWireWhip(dir.normalized);

        yield return new WaitForSeconds(POST_FIRE_WAIT);

        // WireWhip → BrotherBind 연계
        GoTo(_brother.BrotherBind);
    }
}
