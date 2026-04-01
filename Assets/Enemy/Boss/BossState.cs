/// <summary>
/// 모든 보스 State의 추상 기반 클래스.
///
/// 사용 방법:
/// <code>
/// public class HoundChargeState : BossState
/// {
///     private readonly EnemyBossHound _hound;
///
///     public HoundChargeState(EnemyBossHound hound) : base(hound)
///     {
///         _hound = hound;
///     }
///
///     public override void Enter() { ... }
/// }
/// </code>
///
/// 설계 원칙:
///   - 모든 메서드는 가상(virtual) — 구현 강제 없이 필요한 것만 오버라이드.
///   - Machine, Data, Health 프로퍼티를 통해 공통 참조 제공.
///   - GoTo()로 null 체크 없이 안전한 전이 가능.
/// </summary>
public abstract class BossState : IBossState
{
    // ─── 공통 참조 ────────────────────────────────────────────────────────────

    /// <summary>소유 FSM 허브.</summary>
    protected readonly BossStateMachine Machine;

    /// <summary>보스 수치 ScriptableObject.</summary>
    protected BossData Data => Machine.Data;

    /// <summary>보스 체력 컴포넌트.</summary>
    protected BossHealth Health => Machine.Health;

    /// <summary>Phase 2 이상 여부.</summary>
    protected bool IsPhase2 => Machine.CurrentPhase >= BossPhase.Phase2;

    /// <summary>Phase 3 이상 여부.</summary>
    protected bool IsPhase3 => Machine.CurrentPhase >= BossPhase.Phase3;

    // ─── 생성자 ───────────────────────────────────────────────────────────────

    protected BossState(BossStateMachine machine)
    {
        Machine = machine;
    }

    // ─── IBossState 기본 구현 ─────────────────────────────────────────────────

    public virtual void Enter()    { }
    public virtual void Tick()     { }
    public virtual void FixedTick() { }
    public virtual void Exit()     { }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────

    /// <summary>null-safe 상태 전이 단축 메서드.</summary>
    protected void GoTo(IBossState next) => Machine.ChangeState(next);
}
