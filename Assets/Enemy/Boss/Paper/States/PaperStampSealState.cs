using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 3 전용 패턴: 도장 인장 (Stamp Seal).
///
/// 메카닉:
///   보스가 플레이어 위치 예고 → 인장 낙하 충격파.
///   패리 불가 — 플레이어는 반드시 블링크로 회피해야 한다.
///   동시에 DroneFormation도 진행 → 패리와 회피를 동시에 요구.
///
/// 흐름:
///   Enter → 동시에 드론 소환 요청 + 인장 루프 코루틴 시작
///   인장 루프: 플레이어 위치 마킹 → telegraph → 충격파 → 반복
///   탈출: 일정 횟수 인장 후 Vulnerable
///
/// 설계 포인트:
///   - _stampTargetPos를 Enter 시점에 고정해야 '순간이동 회피' 방지.
///   - 예고 연출(SpawnStampIndicator)은 고정 위치에, 실제 충격은 그 위치에 발동.
/// </summary>
public class PaperStampSealState : BossState
{
    private readonly EnemyBossPaper _paper;
    private Coroutine _routine;

    private const int   STAMP_COUNT   = 3;      // 총 인장 횟수 후 Vulnerable
    private const float STAMP_INTERVAL = 2.5f;  // 인장 간 대기 시간

    public PaperStampSealState(EnemyBossPaper paper) : base(paper)
    {
        _paper = paper;
    }

    public override void Enter()
    {
        if (_paper.Rb != null)
            _paper.Rb.linearVelocity = Vector2.zero;

        _routine = Machine.StartCoroutine(StampRoutine());
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

    private IEnumerator StampRoutine()
    {
        // 동시 드론 소환 (DroneFormationState 직접 호출 대신 간단한 스폰)
        int droneCount = _paper.FormationDroneCount;
        var drones = new PaperDrone[droneCount];
        for (int i = 0; i < droneCount; i++)
        {
            drones[i] = _paper.SpawnFormationDrone(i);
            if (drones[i] != null)
                drones[i].InitializeShootOnce(
                    _paper.PlayerTransform != null
                        ? _paper.PlayerTransform
                        : Machine.transform
                );
        }

        // 인장 반복
        for (int stamp = 0; stamp < STAMP_COUNT; stamp++)
        {
            // ① 플레이어 현재 위치 캡처 (예고 고정)
            Vector2 stampPos = _paper.PlayerTransform != null
                ? (Vector2)_paper.PlayerTransform.position
                : (Vector2)Machine.transform.position;

            // ② 예고 연출 스폰
            _paper.SpawnStampIndicator(stampPos);

            // ③ 예고 시간 대기 (플레이어에게 회피 기회)
            yield return new WaitForSeconds(_paper.StampTelegraphTime);

            // ④ 충격파 발동
            _paper.ExecuteStampImpact(stampPos);

            // ⑤ 다음 인장까지 간격
            if (stamp < STAMP_COUNT - 1)
            {
                float interval = STAMP_INTERVAL * (IsPhase3 ? 0.7f : 1f);
                yield return new WaitForSeconds(interval);
            }
        }

        // 드론 Despawn
        for (int i = 0; i < droneCount; i++)
            if (drones[i] != null) drones[i].Die();

        GoTo(Machine.Vulnerable);
    }
}
