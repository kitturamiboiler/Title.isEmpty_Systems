using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 1/2 공격 패턴: 보스 직접 탄막 발사.
///
/// Phase 1: 부채꼴 스프레드 3웨이브 (모두 패리 가능)
/// Phase 2: 나선형 + 일부 패리 불가 고속 탄 혼합
/// Phase 3: 이 State를 사용하지 않음 (StampSeal이 대체)
///
/// 탄막 설계 원칙:
///   - 패리 가능 투사체(느림): 플레이어가 의도적으로 맞받아쳐야 함.
///   - 패리 불가 투사체(빠름): 블링크로 회피해야 함.
///   → 두 종류가 섞이면 '반사 vs 회피' 순간 판단을 요구.
/// </summary>
public class PaperStormState : BossState
{
    private readonly EnemyBossPaper _paper;
    private Coroutine _routine;

    // Phase 1 파라미터
    private const int   P1_BULLET_COUNT        = 5;
    private const float P1_SPREAD_DEG          = 60f;
    private const int   P1_WAVES               = 3;
    private const float P1_WAVE_INTERVAL       = 0.55f;
    private const float P1_TELEGRAPH           = 0.6f;

    // Phase 2 추가 파라미터
    private const int   P2_SPIRAL_COUNT        = 8;
    private const float P2_SPIRAL_ANGLE_STEP   = 45f;
    private const float P2_SPIRAL_INTERVAL     = 0.18f;
    private const float P2_FAST_BULLET_SPEED   = 18f;   // 패리 불가 고속탄

    public PaperStormState(EnemyBossPaper paper) : base(paper)
    {
        _paper = paper;
    }

    public override void Enter()
    {
        if (_paper.Rb != null)
            _paper.Rb.linearVelocity = Vector2.zero;

        _routine = Machine.StartCoroutine(IsPhase2
            ? Phase2StormRoutine()
            : Phase1StormRoutine());
    }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }
    }

    // ─── Phase 1: 부채꼴 웨이브 ──────────────────────────────────────────────

    private IEnumerator Phase1StormRoutine()
    {
        yield return new WaitForSeconds(P1_TELEGRAPH);

        for (int w = 0; w < P1_WAVES; w++)
        {
            FireSpread(P1_BULLET_COUNT, P1_SPREAD_DEG, parryable: true);
            yield return new WaitForSeconds(P1_WAVE_INTERVAL);
        }

        GoTo(Machine.Vulnerable);
    }

    // ─── Phase 2: 나선 + 고속탄 혼합 ──────────────────────────────────────────

    private IEnumerator Phase2StormRoutine()
    {
        yield return new WaitForSeconds(P1_TELEGRAPH * 0.6f);

        float spiralAngle = 0f;
        for (int i = 0; i < P2_SPIRAL_COUNT; i++)
        {
            // 나선 패리 가능탄
            Vector2 spiralDir = Quaternion.Euler(0, 0, spiralAngle) * Machine.GetDirectionToPlayer();
            Machine.SpawnParryableProjectile(spiralDir);
            spiralAngle += P2_SPIRAL_ANGLE_STEP;

            // 짝수 번째에 고속 직선탄 (패리 불가)
            if (i % 2 == 0)
                FireFastBullet();

            yield return new WaitForSeconds(P2_SPIRAL_INTERVAL);
        }

        // Phase 2는 Storm → Vulnerable
        GoTo(Machine.Vulnerable);
    }

    // ─── 발사 헬퍼 ────────────────────────────────────────────────────────────

    private void FireSpread(int count, float spreadDeg, bool parryable)
    {
        if (count <= 0) return;

        Vector2 baseDir  = Machine.GetDirectionToPlayer();
        float halfSpread = spreadDeg * 0.5f;
        float step       = count > 1 ? spreadDeg / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = -halfSpread + step * i;
            Vector2 dir = Quaternion.Euler(0, 0, angle) * baseDir;

            if (parryable)
                Machine.SpawnParryableProjectile(dir);
            else
                Machine.SpawnProjectile(dir);
        }
    }

    /// <summary>패리 불가 고속탄. 플레이어가 블링크로 회피해야 하는 탄.</summary>
    private void FireFastBullet()
    {
        if (Data == null || Data.projectilePrefab == null) return;

        Vector2 dir = Machine.GetDirectionToPlayer();
        var go = Object.Instantiate(
            Data.projectilePrefab,
            Machine.transform.position,
            Quaternion.identity
        );
        go.GetComponent<BossProjectile2D>()?.Launch(
            dir,
            P2_FAST_BULLET_SPEED,
            Data.projectileDamage,
            Data.projectileLifetime
        );
        Object.Destroy(go, Data.projectileLifetime + 0.5f);
    }
}
