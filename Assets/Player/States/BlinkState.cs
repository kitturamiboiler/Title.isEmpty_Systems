/// <summary>
/// 단검 투척 → 블링크 순간이동 상태.
/// 블링크 실행은 PlayerBlinkController2D.TryBlinkToDagger() 에 위임한다.
/// TODO(작성자): 블링크 완료 콜백 → IdleState / GrabState 분기 연결 — 날짜
/// </summary>
public class BlinkState : IState2D
{
    private readonly PlayerStateMachine _machine;

    public BlinkState(PlayerStateMachine machine)
    {
        _machine = machine;
    }

    public void Enter()
    {
        // TODO(작성자): PlayerBlinkController2D.TryBlinkToDagger() 호출 위임 — 날짜
    }

    public void Tick()
    {
        // TODO(작성자): 블링크 완료 감지 → 전이 결정 — 날짜
    }

    public void FixedTick() { }

    public void Exit() { }
}
