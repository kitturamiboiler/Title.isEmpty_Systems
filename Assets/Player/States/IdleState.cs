using UnityEngine;

/// <summary>
/// 플레이어 대기 상태.
/// 수평 입력 감지 시 RunState로 전이한다.
/// 실제 물리 이동은 PlayerMovement2D가 처리 (FSM 통합 미완 상태 유지).
/// </summary>
public class IdleState : IState2D
{
    private readonly PlayerStateMachine _machine;

    private const float INPUT_DEAD_ZONE = 0.01f;

    public IdleState(PlayerStateMachine machine)
    {
        _machine = machine;
    }

    public void Enter()
    {
        _machine.NotifyPlayerAnim(PlayerAnimHashes.Idle);
    }

    public void Tick()
    {
        float h = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(h) > INPUT_DEAD_ZONE)
            _machine.ChangeState(_machine.Run);
    }

    public void FixedTick() { }

    public void Exit() { }
}
