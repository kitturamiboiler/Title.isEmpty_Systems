using UnityEngine;

/// <summary>
/// 공격 패턴 완료 후 플레이어가 데미지를 줄 수 있는 피격 가능 창.
/// Duration은 BossData.GetVulnerableDuration(CurrentPhase)에서 가져온다.
/// 시간이 만료되면 GetFirstAttackState()로 복귀.
/// </summary>
public class BossVulnerableState : BossState
{
    private float _timer;
    private float _duration;

    public BossVulnerableState(BossStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        _timer = 0f;
        _duration = Data != null
            ? Data.GetVulnerableDuration(Machine.CurrentPhase)
            : 1.5f;

        if (Machine.Rb != null)
            Machine.Rb.linearVelocity = Vector2.zero;

        Health?.SetVulnerable(true);
    }

    public override void Tick()
    {
        _timer += Time.deltaTime;
        if (_timer >= _duration)
        {
            var nextAttack = Machine.GetFirstAttackState();
            GoTo(nextAttack ?? Machine.Idle);
        }
    }

    public override void Exit()
    {
        Health?.SetVulnerable(false);
    }
}
