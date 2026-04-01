using System.Collections;
using UnityEngine;

/// <summary>
/// 제네릭 보스 공격 순환 State (BulletWall → Charge → GroundPound 로테이션).
/// 고유한 커스텀 공격 패턴이 없는 단순 테스트 보스에 기본 제공.
/// 커스텀 패턴이 있는 보스(Hound, Paper 등)는 이 State를 사용하지 않는다.
/// </summary>
public class BossPhaseAttackState : BossState
{
    // ─── 패턴 로테이션 ────────────────────────────────────────────────────────

    private static readonly System.Action<BossPhaseAttackState>[] PHASE1_ROTATION =
    {
        s => s.Machine.StartCoroutine(s.BulletWallRoutine()),
        s => s.Machine.StartCoroutine(s.ChargeRoutine()),
        s => s.Machine.StartCoroutine(s.GroundPoundRoutine()),
    };

    private static readonly System.Action<BossPhaseAttackState>[] PHASE2_ROTATION =
    {
        s => s.Machine.StartCoroutine(s.BulletWallRoutine()),
        s => s.Machine.StartCoroutine(s.ChargeRoutine()),
        s => s.Machine.StartCoroutine(s.GroundPoundRoutine()),
        s => s.Machine.StartCoroutine(s.ShieldModeRoutine()),
    };

    private int       _phase1Index;
    private int       _phase2Index;
    private int       _previousPhaseOrdinal;
    private Coroutine _activeRoutine;

    public BossPhaseAttackState(BossStateMachine machine) : base(machine) { }

    // ─── IBossState ───────────────────────────────────────────────────────────

    public override void Enter()
    {
        int currentOrdinal = (int)Machine.CurrentPhase;
        if (currentOrdinal != _previousPhaseOrdinal)
        {
            _phase1Index          = 0;
            _phase2Index          = 0;
            _previousPhaseOrdinal = currentOrdinal;
        }

        RunCurrentPattern();
    }

    public override void Exit()
    {
        if (_activeRoutine != null)
        {
            Machine.StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        if (Machine.Rb != null)
            Machine.Rb.linearVelocity = Vector2.zero;
    }

    // ─── 패턴 디스패치 ────────────────────────────────────────────────────────

    private void RunCurrentPattern()
    {
        if (IsPhase2)
        {
            PHASE2_ROTATION[_phase2Index % PHASE2_ROTATION.Length](this);
            _phase2Index++;
        }
        else
        {
            PHASE1_ROTATION[_phase1Index % PHASE1_ROTATION.Length](this);
            _phase1Index++;
        }
    }

    private void EndPattern()
    {
        GoTo(Machine.Vulnerable);
    }

    // ─── 패턴 코루틴 ──────────────────────────────────────────────────────────

    private IEnumerator BulletWallRoutine()
    {
        if (Data == null) { EndPattern(); yield break; }

        float telegraph = Data.bulletWallTelegraphDuration * (IsPhase2 ? 0.7f : 1f);
        yield return new WaitForSeconds(telegraph);

        int waves = Data.bulletWallWaves;
        for (int w = 0; w < waves; w++)
        {
            float halfSpread = Data.bulletWallSpread * 0.5f;
            float step       = Data.bulletWallCount > 1
                ? Data.bulletWallSpread / (Data.bulletWallCount - 1)
                : 0f;

            for (int i = 0; i < Data.bulletWallCount; i++)
            {
                float angle = -halfSpread + step * i;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * Machine.GetDirectionToPlayer();
                Machine.SpawnProjectile(dir);
            }

            float delay = Data.bulletWallWaveDelay * (IsPhase2 ? 0.7f : 1f);
            yield return new WaitForSeconds(delay);
        }

        EndPattern();
    }

    private IEnumerator ChargeRoutine()
    {
        if (Data == null || Machine.Rb == null) { EndPattern(); yield break; }

        Machine.FacePlayer();
        yield return new WaitForSeconds(Data.chargeTelegraphDuration);

        Vector2 chargeDir  = Machine.GetDirectionToPlayer();
        chargeDir.y        = 0f;
        float speed        = Data.chargeSpeed * Data.GetSpeedMultiplier(Machine.CurrentPhase);
        Machine.Rb.linearVelocity = chargeDir.normalized * speed;

        yield return new WaitForSeconds(Data.chargeDuration);

        Machine.Rb.linearVelocity = Vector2.zero;
        EndPattern();
    }

    private IEnumerator GroundPoundRoutine()
    {
        if (Data == null || Machine.Rb == null) { EndPattern(); yield break; }

        Machine.Rb.linearVelocity = new Vector2(0f, Data.groundPoundJumpForce);

        float elapsed = 0f;
        while (!Machine.IsBossGrounded() && elapsed < Data.groundPoundFallTimeout)
        {
            // 정점 이후 강제 낙하
            if (Machine.Rb.linearVelocity.y < 0f)
                Machine.Rb.linearVelocity = new Vector2(0f, Data.groundPoundFallSpeed);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        Machine.Rb.linearVelocity = Vector2.zero;

        // 충격파 범위 데미지
        var buf = new Collider2D[16];
        int count = Physics2D.OverlapCircleNonAlloc(
            Machine.transform.position,
            Data.groundPoundRadius,
            buf,
            1 << Layers.Player
        );
        for (int i = 0; i < count; i++)
            buf[i].GetComponentInParent<IHealth>()?.TakeDamage(Data.groundPoundDamage);

        EndPattern();
    }

    private IEnumerator ShieldModeRoutine()
    {
        if (Data == null || Health == null) { EndPattern(); yield break; }

        Health.ResetArmor();

        // 갑옷이 파괴되거나 취약 창 길이만큼 대기
        float elapsed  = 0f;
        float maxWait  = Data.GetVulnerableDuration(Machine.CurrentPhase) * 2f;
        while (!Health.IsArmorBroken && elapsed < maxWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        EndPattern();
    }
}
