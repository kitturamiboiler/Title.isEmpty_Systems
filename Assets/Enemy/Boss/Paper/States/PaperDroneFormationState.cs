using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 1 공격 패턴: 드론 편대 소환 → 일제 발사.
///
/// 흐름:
///   Enter → formationPoints 수만큼 드론 스폰
///   → 각 드론이 fireDelay 후 플레이어를 향해 패리 가능 투사체 1발 발사
///   → 모든 드론 발사 완료(+대기 여유) → Despawn → Vulnerable 전이
///
/// Phase 2 이상: 드론 발사 후 추가 PaperStorm으로 전이 (더 강한 연계).
/// </summary>
public class PaperDroneFormationState : BossState
{
    private readonly EnemyBossPaper _paper;
    private Coroutine _routine;

    private const float DRONE_FIRE_WAIT      = 0.5f;  // 배치 → 발사까지 드론별 딜레이
    private const float POST_FORMATION_WAIT  = 0.8f;  // 마지막 발사 후 전이까지 대기

    public PaperDroneFormationState(EnemyBossPaper paper) : base(paper)
    {
        _paper = paper;
    }

    public override void Enter()
    {
        if (_paper.Rb != null)
            _paper.Rb.linearVelocity = Vector2.zero;

        _routine = Machine.StartCoroutine(FormationRoutine());
    }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator FormationRoutine()
    {
        int droneCount = _paper.FormationDroneCount;
        if (droneCount == 0)
        {
            // 포메이션 포인트 미연결 → 즉시 Vulnerable
            GoTo(Machine.Vulnerable);
            yield break;
        }

        // 드론 순차 스폰 (약간의 시간차로 연출 효과)
        float spawnInterval = IsPhase2 ? 0.1f : 0.2f;
        var drones = new PaperDrone[droneCount];

        for (int i = 0; i < droneCount; i++)
        {
            drones[i] = _paper.SpawnFormationDrone(i);
            yield return new WaitForSeconds(spawnInterval);
        }

        // 드론들이 플레이어를 향해 조준 + 발사 (InitializeShootOnce 내부에서 처리)
        for (int i = 0; i < droneCount; i++)
        {
            if (drones[i] == null) continue;
            drones[i].InitializeShootOnce(
                _paper.PlayerTransform != null ? _paper.PlayerTransform : Machine.transform
            );
        }

        // 모든 드론 발사 완료 대기
        float totalWait = DRONE_FIRE_WAIT + POST_FORMATION_WAIT;
        yield return new WaitForSeconds(totalWait * (IsPhase2 ? 0.7f : 1f));

        // 드론 Despawn (아직 살아있는 것들)
        for (int i = 0; i < droneCount; i++)
            if (drones[i] != null) drones[i].Die();

        // Phase 2 이상: DroneFormation → PaperStorm 연계
        if (IsPhase2)
            GoTo(_paper.PaperStorm);
        else
            GoTo(Machine.Vulnerable);
    }
}
