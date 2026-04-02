using UnityEngine;

/// <summary>
/// Boss 1: 하운드 — 블링크·그랩 숙달 시험.
///
/// 설계 요약:
///   Phase 1 (lives 6~4): Charge → Claw → Vulnerable 반복
///   Phase 2 (lives 3~2): 피스톤 노출 (HoundExposePistonState) → 슬램으로 3개 파괴
///   Phase 3 (lives 1~0): 분노 질주 (HoundFrenzyState) — 속도 × phase3SpeedMultiplier
///
/// 약점 메카닉:
///   - 유압 피스톤 3개: Phase 2에서 노출, 슬램으로 파괴
///   - 3개 파괴 전까지는 Phase 2 전투가 반복
///   - 3개 모두 파괴 시 Phase 3 트리거
///
/// BossStateMachine 확장 패턴:
///   Awake() → base.Awake() → InitializeStates()
///   Update()/FixedUpdate() → base 위임 (CurrentState.Tick/FixedTick 자동 호출)
/// </summary>
public class EnemyBossHound : BossStateMachine
{
    // ─── 하운드 전용 직렬화 ───────────────────────────────────────────────────

    [Header("Hound — Hydraulic Pistons")]
    [SerializeField] private HydraulicPiston[] _pistons;  // 인스펙터에서 3개 연결 필수

    [Header("Hound — Claw Data")]
    [SerializeField] private float _clawDamage   = 1f;
    [SerializeField] private float _clawRange    = 1.8f;
    [SerializeField] private float _clawCooldown = 0.5f;

    [Header("Hound — Phase 2 Shockwave")]
    [Tooltip("충격파 발사 전 전조 시간(초).")]
    [SerializeField] private float _shockwaveTelegraphTime = 0.8f;
    [Tooltip("충격파 발사 후 피스톤 노출까지 대기 시간(초).")]
    [SerializeField] private float _shockwaveWaitAfter     = 1.2f;

    // ─── 하운드 전용 State ────────────────────────────────────────────────────

    /// <summary>수평 돌진 패턴.</summary>
    public HoundChargeState      HoundCharge      { get; private set; }

    /// <summary>근접 할퀴기 콤보.</summary>
    public HoundClawState        HoundClaw        { get; private set; }

    /// <summary>Phase 2 진입 직후 충격파 압박.</summary>
    public HoundShockwaveState    HoundShockwave    { get; private set; }

    /// <summary>Phase 2 피스톤 노출 + 그랩 대기.</summary>
    public HoundExposePistonState HoundExposePiston { get; private set; }

    /// <summary>Phase 3 분노 패턴.</summary>
    public HoundFrenzyState      HoundFrenzy      { get; private set; }

    // ─── 피스톤 트래킹 ────────────────────────────────────────────────────────

    /// <summary>파괴된 피스톤 수.</summary>
    public int BrokenPistonCount { get; private set; }

    /// <summary>모든 피스톤이 파괴된 상태.</summary>
    public bool AllPistonsDestroyed => _pistons != null
                                       && BrokenPistonCount >= _pistons.Length;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();  // 공통 State + InitializeStates() 호출

        // 피스톤 이벤트 구독
        if (_pistons != null)
        {
            foreach (var piston in _pistons)
            {
                if (piston == null) continue;
                piston.OnPistonBroken.AddListener(OnPistonBroken);
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyBossHound] 피스톤 배열이 {gameObject.name}에 연결되지 않았습니다.");
        }
    }

    // ─── BossStateMachine 구현 ────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void InitializeStates()
    {
        HoundCharge       = new HoundChargeState(this);
        HoundClaw         = new HoundClawState(this);
        HoundShockwave    = new HoundShockwaveState(this);
        HoundExposePiston = new HoundExposePistonState(this);
        HoundFrenzy       = new HoundFrenzyState(this);
    }

    /// <inheritdoc/>
    /// <remarks>Idle → 최초 공격 State = HoundCharge.</remarks>
    public override IBossState GetFirstAttackState() => HoundCharge;

    /// <inheritdoc/>
    protected override void OnPhaseChanged(BossPhase newPhase)
    {
        switch (newPhase)
        {
            case BossPhase.Phase2:
                // Phase 2 진입: 먼저 충격파로 압박 → 이후 피스톤 노출
                ChangeState(HoundShockwave);
                break;

            case BossPhase.Phase3:
                // Phase 3 진입: 분노 패턴 시작
                ChangeState(HoundFrenzy);
                break;
        }
    }

    // ─── 피스톤 이벤트 ────────────────────────────────────────────────────────

    /// <summary>HydraulicPiston.OnPistonBroken 이벤트 핸들러.</summary>
    public void OnPistonBroken()
    {
        BrokenPistonCount++;

        if (AllPistonsDestroyed && CurrentPhase == BossPhase.Phase2)
        {
            // 남은 lives를 강제로 phase3LivesThreshold 이하로 만들어 Phase 3 전환을 트리거
            // (이미 체력이 낮다면 OnDamaged에서 자동 전환됨)
            // TODO(작성자): Phase 3 강제 전환 로직 — BossHealth.ForcePhaseTransition() 추가 검토 — 2026-04-01
        }
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────

    /// <summary>Phase 2 진입 시 HoundExposePistonState에서 호출해 피스톤 노출.</summary>
    public void ExposeAllPistons(bool exposed)
    {
        if (_pistons == null) return;
        foreach (var piston in _pistons)
            piston?.SetExposed(exposed);
    }

    public float ShockwaveTelegraphTime => _shockwaveTelegraphTime;
    public float ShockwaveWaitAfter     => _shockwaveWaitAfter;

    /// <summary>현재 Phase 속도 배율 적용된 Charge 속도.</summary>
    public float GetChargeSpeed()
    {
        if (_data == null) return 12f;
        return _data.chargeSpeed * _data.GetSpeedMultiplier(CurrentPhase);
    }

    /// <summary>근접 할퀴기 데미지.</summary>
    public float ClawDamage   => _clawDamage;

    /// <summary>근접 할퀴기 범위.</summary>
    public float ClawRange    => _clawRange;

    /// <summary>근접 할퀴기 쿨다운.</summary>
    public float ClawCooldown => _clawCooldown;
}
