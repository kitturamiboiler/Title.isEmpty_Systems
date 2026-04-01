using UnityEngine;

/// <summary>
/// 플레이어 이동 상태.
/// 수평 입력 소멸 시 IdleState로 복귀한다.
/// 실제 물리 이동은 PlayerMovement2D가 처리 (FSM 통합 미완 상태 유지).
/// TODO(작성자): PlayerMovement2D 수평 이동 로직 이관 — 날짜
/// </summary>
public class RunState : IState2D
{
    private readonly PlayerStateMachine _machine;

    private const float INPUT_DEAD_ZONE = 0.01f;

    public RunState(PlayerStateMachine machine)
    {
        _machine = machine;
    }

    public void Enter() { }

    public void Tick()
    {
        float h = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(h) <= INPUT_DEAD_ZONE)
            _machine.ChangeState(_machine.Idle);
    }

    public void FixedTick()
    {
        // TODO(작성자): PlayerMovement2D 수평 이동 로직 이관 — 날짜
    }

    public void Exit() { }
}
