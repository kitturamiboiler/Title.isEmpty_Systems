/// <summary>
/// 구속 상태 진입·해제 계약.
/// BrotherBindProjectile2D가 충돌 시 이 인터페이스를 통해 대상을 속박한다.
///
/// 구현체:
///   PlayerStateMachine — 플레이어 구속 → PlayerBoundState 진입
///
/// 해제 조건 (PlayerBoundState 내부에서 처리):
///   ① 단검 투척 → 블링크 성공 → 즉시 해제
///   ② duration 만료 → TakeDamage 후 강제 해제
/// </summary>
public interface IBindable
{
    /// <summary>
    /// 구속 시작. duration 이내에 탈출하지 못하면 TakeDamage 후 자동 해제.
    /// </summary>
    void Bind(float duration);

    /// <summary>
    /// 외부에서 강제 해제 (보스 사망 등 예외 상황).
    /// </summary>
    void Unbind();
}
