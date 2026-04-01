using UnityEngine;

/// <summary>
/// Phase 2 전용: 유압 피스톤 노출 + 그랩 대기 상태.
///
/// Enter 시 EnemyBossHound.ExposeAllPistons(true) 호출 → 피스톤 IsGrabbable = true.
/// 플레이어가 피스톤 슬램 시 HydraulicPiston.OnPistonBroken → EnemyBossHound.OnPistonBroken().
///
/// 탈출 조건:
///   ① AllPistonsDestroyed → Phase 3 전환 대기 (BossStateMachine.OnPhaseChanged 처리)
///   ② 최대 노출 대기 시간 초과 → 피스톤 숨김 후 HoundCharge로 복귀 (플레이어 실패 페널티)
/// </summary>
public class HoundExposePistonState : BossState
{
    private readonly EnemyBossHound _hound;

    private float _timer;
    private const float MAX_EXPOSE_DURATION = 8f;  // 플레이어에게 주어진 최대 시간

    public HoundExposePistonState(EnemyBossHound hound) : base(hound)
    {
        _hound = hound;
    }

    public override void Enter()
    {
        _timer = 0f;

        if (_hound.Rb != null)
            _hound.Rb.linearVelocity = Vector2.zero;

        // 피스톤 노출 → 플레이어가 그랩 가능해짐
        _hound.ExposeAllPistons(true);
    }

    public override void Tick()
    {
        // 모든 피스톤 파괴 → 이 State는 Phase 3 전환을 BossStateMachine에 맡기고 대기
        if (_hound.AllPistonsDestroyed) return;

        _timer += Time.deltaTime;
        if (_timer >= MAX_EXPOSE_DURATION)
        {
            // 타임아웃 — 피스톤 숨김, 재충전 유도
            _hound.ExposeAllPistons(false);
            GoTo(_hound.HoundCharge);
        }
    }

    public override void Exit()
    {
        // 비정상 탈출 시 피스톤 노출 상태 정리
        if (!_hound.AllPistonsDestroyed)
            _hound.ExposeAllPistons(false);
    }
}
