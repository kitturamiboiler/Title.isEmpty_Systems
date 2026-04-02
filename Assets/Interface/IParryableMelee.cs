/// <summary>
/// 근접 공격 패리 계약.
///
/// 흐름:
///   플레이어 Q 패리 → IsInMeleeAttack == true 인 적 감지
///   → OnMeleeParried(stunDuration) 호출
///   → 적이 스턴 상태 진입 (IsGrabbable = true, 공중으로 튕김)
///   → 플레이어가 즉시 블링크-그랩 → Shift 슬램 → 볼링핀 연계
///
/// 구현 규칙:
///   - 근접 공격 State(예: HoundChargeState)에서 IsInMeleeAttack = true 반환
///   - OnMeleeParried 내부에서 FSM을 StunState로 전이, stunDuration 후 복귀
///   - 스턴 중 IGrabbable.IsGrabbable은 반드시 true를 반환해야 한다
/// </summary>
public interface IParryableMelee
{
    /// <summary>
    /// 현재 근접 공격 중(히트박스 활성화 구간)이면 true.
    /// 선제 패리(공격 전) 나 회피(공격 후) 구간에서는 반드시 false 반환.
    /// </summary>
    bool IsInMeleeAttack { get; }

    /// <summary>
    /// 패리 성공 시 PlayerParryController2D가 호출.
    /// 구현체는 다음을 반드시 처리해야 한다:
    ///   1. 현재 공격 State 즉시 중단
    ///   2. IGrabbable.IsGrabbable = true (stunDuration 동안)
    ///   3. stunDuration 후 IsGrabbable = false 복귀
    /// </summary>
    /// <param name="stunDuration">그랩 가능 상태 유지 시간(초).</param>
    void OnMeleeParried(float stunDuration);
}
