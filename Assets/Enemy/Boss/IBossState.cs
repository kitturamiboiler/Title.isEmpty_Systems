/// <summary>
/// 보스 FSM 상태 계약. IState2D와 동일한 구조, 보스 전용으로 분리.
/// </summary>
public interface IBossState
{
    /// <summary>상태 진입 시 1회 호출.</summary>
    void Enter();

    /// <summary>매 프레임 호출. 전이 조건·입력 평가.</summary>
    void Tick();

    /// <summary>매 물리 프레임 호출. 속도·힘·Raycast 처리.</summary>
    void FixedTick();

    /// <summary>상태 탈출 시 1회 호출. 리소스 정리·코루틴 중단.</summary>
    void Exit();
}
