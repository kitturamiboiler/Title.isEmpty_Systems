using System.Collections;
using UnityEngine;

/// <summary>
/// Shadow 보스 메인 퍼즐 상태: '잔상 스위칭 퍼즐'.
///
/// 흐름:
///   1. 스위치 배치: Phase별 필요 스위치 수(1→2→3) 결정
///   2. Phase 3에서는 가짜 스위치도 추가 배치 (IsFake=true)
///   3. 플레이어의 BlinkGhostMarker가 스위치를 동시에 모두 활성화
///      → 보스 BossVulnerableState로 전이
///   4. 리셋 타이머 초과(스위치 전부 동시 활성화 실패 시) → 스위치 초기화 후 HauntState
///
/// 방어 설계:
///   - Null 방어: Switches 배열이 비어있으면 즉시 Vulnerable로 전이(fallback)
///   - 동시 활성 체크: CheckAllActive()가 프레임마다 실행, 상태 중복 전이 방지용 _triggered 플래그
///   - 리셋 타이머: 무한 대기 방지 — _puzzleTimeout 초 경과 시 스위치 초기화 후 ShadowHaunt로
/// </summary>
public class ShadowGhostPhaseState : BossState
{
    private readonly EnemyBossShadow _shadow;

    private bool _triggered;
    private float _timer;
    private int _requiredSwitches;

    public ShadowGhostPhaseState(BossStateMachine machine) : base(machine)
    {
        _shadow = machine as EnemyBossShadow;
        if (_shadow == null)
            Debug.LogError("[ShadowGhostPhaseState] BossStateMachine이 EnemyBossShadow가 아닙니다.");
    }

    public override void Enter()
    {
        _triggered = false;
        _timer = 0f;

        if (_shadow == null)
        {
            GoTo(Machine.Vulnerable);
            return;
        }

        _requiredSwitches = _shadow.GetRequiredSwitchCount();

        if (_requiredSwitches <= 0)
        {
            // 방어: 스위치 없음 → 즉시 Vulnerable
            GoTo(Machine.Vulnerable);
            return;
        }

        _shadow.ActivateSwitches(_requiredSwitches);
        _shadow.StartCoroutine(ShadowGhostPhaseRoutine());
    }

    public override void Tick()
    {
        if (_triggered) return;

        _timer += Time.deltaTime;

        // 리셋 타이머 초과 → 실패, HauntState로
        if (_timer >= _shadow.PuzzleTimeout)
        {
            _triggered = true;
            _shadow.ResetAllSwitches();
            GoTo(_shadow.ShadowHaunt);
            return;
        }

        if (CheckAllActive())
        {
            _triggered = true;
            GoTo(Machine.Vulnerable);
        }
    }

    public override void FixedTick() { }

    public override void Exit()
    {
        _shadow?.ResetAllSwitches();
    }

    private bool CheckAllActive()
    {
        var switches = _shadow.GetRealSwitches();
        if (switches == null || switches.Length == 0) return false;

        int activeCount = 0;
        foreach (var sw in switches)
        {
            if (sw != null && sw.IsActive) activeCount++;
        }
        return activeCount >= _requiredSwitches;
    }

    private IEnumerator ShadowGhostPhaseRoutine()
    {
        // Phase 3: 가짜 스위치 스폰 (0.5초 지연 후 — 플레이어가 진짜 위치를 인지할 시간 부여)
        if (Machine.IsPhase3)
        {
            yield return new WaitForSeconds(0.5f);
            _shadow.ActivateFakeSwitches();
        }
    }
}
