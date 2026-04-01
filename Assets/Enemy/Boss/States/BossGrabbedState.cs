using UnityEngine;

/// <summary>
/// 플레이어 GrabState에 의해 붙잡힌 상태.
/// 진입 시 AI(FSM Update 루프)를 차단하고 Rigidbody를 Kinematic으로 전환한다.
/// 플레이어의 SlamState가 보스 위치를 MovePosition으로 직접 제어한다.
///
/// 탈출 흐름:
///   SlamState.ExecuteTargetDeath()
///     → BossHealth.ReleaseGrab()
///     → BossHealth.OnGrabReleased 이벤트
///     → BossStateMachine.OnBossGrabReleased()
///     → BossVulnerableState 또는 BossDeadState로 전이
/// </summary>
public class BossGrabbedState : BossState
{
    public BossGrabbedState(BossStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        if (Machine.Rb == null)
        {
            Debug.LogError("[BossGrabbedState] Rigidbody2D가 null입니다. 물리 제어 불가.");
            return;
        }
        if (Machine.Col == null)
        {
            Debug.LogError("[BossGrabbedState] Collider2D가 null입니다.");
            return;
        }

        // 수평 충격 잔상 제거
        Machine.Rb.linearVelocity = Vector2.zero;

        // Kinematic 전환 → 물리 엔진이 위치를 덮어쓰지 않도록 차단
        Machine.Rb.bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>
    /// Tick/FixedTick 의도적 비활성화.
    /// 위치 제어는 SlamState의 MovePosition에 전적으로 위임한다.
    /// </summary>
    public override void Tick()     { }
    public override void FixedTick() { }

    public override void Exit()
    {
        if (Machine.Rb == null) return;

        // 슬램 완료 후 물리 복구
        Machine.Rb.bodyType = RigidbodyType2D.Dynamic;
    }
}
