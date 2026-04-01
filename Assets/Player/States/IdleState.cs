/// <summary>
/// 플레이어 대기 상태. 이동·투사체 입력이 없는 기본 상태.
/// TODO(작성자): 이동 입력 감지 → RunState 전이 연결 — 날짜
/// </summary>
public class IdleState : IState2D
{
    private readonly PlayerStateMachine _machine;

    public IdleState(PlayerStateMachine machine)
    {
        _machine = machine;
    }

    public void Enter() { }

    public void Tick()
    {
        // TODO(작성자): 이동 입력 감지 시 _machine.ChangeState(runState) — 날짜
    }

    public void FixedTick() { }

    public void Exit() { }
}
