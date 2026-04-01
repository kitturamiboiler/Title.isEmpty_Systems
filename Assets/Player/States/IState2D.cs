/// <summary>
/// 2D 플레이어 상태 계약.
/// - Tick    : MonoBehaviour.Update() 상당. 입력·전이 조건 평가.
/// - FixedTick: MonoBehaviour.FixedUpdate() 상당. 물리 처리.
/// - Update 내 if/else 체인 금지 (Rule 3) — 모든 행동은 이 인터페이스를 구현하는 State 클래스로 분리.
/// </summary>
public interface IState2D
{
    /// <summary>상태 진입 시 1회 호출.</summary>
    void Enter();

    /// <summary>상태 유지 중 매 프레임 호출 (입력·전이 조건).</summary>
    void Tick();

    /// <summary>상태 유지 중 매 물리 프레임 호출 (속도·힘·Raycast).</summary>
    void FixedTick();

    /// <summary>상태 탈출 시 1회 호출. 리소스 정리·플래그 복구.</summary>
    void Exit();
}
