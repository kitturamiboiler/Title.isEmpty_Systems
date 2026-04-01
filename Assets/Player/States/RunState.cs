/// <summary>
/// 플레이어 이동 상태.
/// TODO(작성자): 수평 입력 없음 → IdleState 전이 연결 — 날짜
/// TODO(작성자): PlayerMovement2D 이동 로직 이관 — 날짜
/// </summary>
public class RunState : IState2D
{
    private readonly PlayerStateMachine _machine;

    public RunState(PlayerStateMachine machine)
    {
        _machine = machine;
    }

    public void Enter() { }

    public void Tick()
    {
        // TODO(작성자): 수평 입력 없음 시 _machine.ChangeState(idleState) — 날짜
    }

    public void FixedTick()
    {
        // TODO(작성자): PlayerMovement2D 수평 이동 로직 이관 — 날짜
    }

    public void Exit() { }
}
