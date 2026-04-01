using UnityEngine;

/// <summary>
/// 보스 대기 상태. 플레이어가 detectionRange 내로 들어오면 Attack으로 전이.
/// 각 보스의 첫 공격 State로의 전이는 서브클래스 BossStateMachine.GetFirstAttackState()로 위임.
/// </summary>
public class BossIdleState : BossState
{
    public BossIdleState(BossStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        if (Machine.Rb == null) return;
        Machine.Rb.linearVelocity = Vector2.zero;
    }

    public override void Tick()
    {
        if (Machine.PlayerTransform == null) return;
        if (Data == null) return;

        float dist = Vector2.Distance(
            Machine.transform.position,
            Machine.PlayerTransform.position
        );

        if (dist <= Data.detectionRange)
        {
            var firstAttack = Machine.GetFirstAttackState();
            if (firstAttack != null)
                GoTo(firstAttack);
        }
    }
}
