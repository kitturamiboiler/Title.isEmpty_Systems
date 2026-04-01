using UnityEngine;

/// <summary>
/// 보스 사망 처리 State (Terminal — Exit 없음).
/// Enter 시 물리 정지 + Collider 비활성화 후 일정 시간 뒤 GameObject 파괴.
/// 이 State에서 다른 State로 절대 전이하지 않는다.
/// </summary>
public class BossDeadState : BossState
{
    private const float DESTROY_DELAY = 2.0f;

    public BossDeadState(BossStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        if (Machine.Rb != null)
        {
            Machine.Rb.linearVelocity = Vector2.zero;
            Machine.Rb.bodyType       = RigidbodyType2D.Static;
        }

        if (Machine.Col != null)
            Machine.Col.enabled = false;

        // TODO(작성자): 사망 연출 애니메이션 / 파티클 트리거 — 2026-04-01
        Object.Destroy(Machine.gameObject, DESTROY_DELAY);
    }

    // Terminal State — Tick/FixedTick 의도적 비활성
    public override void Tick()     { }
    public override void FixedTick() { }
    public override void Exit()     { }
}
