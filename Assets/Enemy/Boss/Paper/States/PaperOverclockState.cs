using System.Collections;
using UnityEngine;

/// <summary>
/// Paper Phase 2 전용 패턴: 오버클럭 탄막.
///
/// 트리거: PaperDroneShieldState에서 드론 3개를 모두 파괴한 직후.
///
/// 흐름:
///   1. 전조 0.4초 — 보스가 멈추고 과부하 이펙트 (TODO)
///   2. 패리 가능 볼트 4발을 0.25초 간격으로 발사
///      홀수 발: +15° 오프셋 / 짝수 발: -15° 오프셋 (교차 패턴)
///   3. 발사 완료 → BossVulnerableState 전이
///
/// 설계 의도:
///   드론 방어막을 겨우 파괴한 직후 플레이어를 즉시 압박.
///   4발 모두 패리하면 에어블링크 최대 4회 충전 → Phase 2 취약 실행의 보상.
///   드론 방어막 파괴라는 '성공' 직후에 오는 '추가 테스트'로 극적 효과를 높인다.
///
/// 방어 설계:
///   - PlayerTransform null: 발사 건너뜀, 즉시 Vulnerable로
///   - SpawnParryableProjectile null 방어는 BossStateMachine에서 처리
/// </summary>
public class PaperOverclockState : BossState
{
    private readonly EnemyBossPaper _paper;
    private Coroutine _routine;

    private const int   BOLT_COUNT      = 4;
    private const float BOLT_INTERVAL   = 0.25f;
    private const float TELEGRAPH_TIME  = 0.4f;
    private const float ANGLE_OFFSET    = 15f;

    public PaperOverclockState(EnemyBossPaper paper) : base(paper)
    {
        _paper = paper;
    }

    public override void Enter()
    {
        if (_paper.Rb != null)
            _paper.Rb.linearVelocity = Vector2.zero;

        _routine = Machine.StartCoroutine(OverclockRoutine());
    }

    public override void Tick()   { }
    public override void FixedTick() { }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator OverclockRoutine()
    {
        // ── 전조 ──────────────────────────────────────────────────────────────
        yield return new UnityEngine.WaitForSeconds(TELEGRAPH_TIME);

        if (Machine.PlayerTransform == null)
        {
            GoTo(Machine.Vulnerable);
            yield break;
        }

        // ── 교차 패턴 4발 발사 ─────────────────────────────────────────────────
        for (int i = 0; i < BOLT_COUNT; i++)
        {
            Machine.FacePlayer();
            Vector2 baseDir   = Machine.GetDirectionToPlayer();
            float   angleSign = (i % 2 == 0) ? ANGLE_OFFSET : -ANGLE_OFFSET;
            Vector2 fireDir   = (Vector2)(UnityEngine.Quaternion.Euler(0f, 0f, angleSign) * baseDir);

            Machine.SpawnParryableProjectile(fireDir.normalized);

            yield return new UnityEngine.WaitForSeconds(BOLT_INTERVAL);
        }

        // ── 취약 상태로 전이 ───────────────────────────────────────────────────
        GoTo(Machine.Vulnerable);
    }
}
