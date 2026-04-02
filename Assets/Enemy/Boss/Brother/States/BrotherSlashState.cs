using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 1/2/3 슬래시 콤보 패턴.
///
/// 흐름:
///   근접 돌진(DashToPlayer) → 범위 내 진입 시 N회 할퀴기 → BrotherBind 전이
///
/// Phase 3: 타격 횟수 +1, 돌진 속도 × phase3SpeedMultiplier
///
/// 방어 설계:
///   - 돌진 중 _isClosing 플래그로 SlashRoutine 재진입 방지.
///   - 플레이어 미발견 시 dashTimeout 후 Vulnerable 전이 (무한 대기 방지).
/// </summary>
public class BrotherSlashState : BossState
{
    private readonly EnemyBossBrother _brother;
    private Coroutine _routine;

    private const int   P1_SLASH_COUNT    = 2;
    private const int   P3_SLASH_COUNT    = 3;
    private const float SLASH_DELAY       = 0.35f;
    private const float DASH_TIMEOUT      = 3f;

    public BrotherSlashState(EnemyBossBrother brother) : base(brother)
    {
        _brother = brother;
    }

    public override void Enter()
    {
        _routine = Machine.StartCoroutine(SlashRoutine());
    }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }
        if (_brother.Rb != null)
            _brother.Rb.linearVelocity = Vector2.zero;
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator SlashRoutine()
    {
        // 1단계: 플레이어에게 돌진
        yield return Machine.StartCoroutine(DashToPlayer());

        // 2단계: 슬래시 콤보
        int count = IsPhase3 ? P3_SLASH_COUNT : P1_SLASH_COUNT;
        float delay = SLASH_DELAY * (IsPhase3 ? 0.7f : 1f);

        for (int i = 0; i < count; i++)
        {
            Machine.FacePlayer();
            ApplySlash();
            yield return new WaitForSeconds(delay);
        }

        // 3단계: Phase 1 → BrotherBind, Phase 2+ → WireWhip 후 Bind
        if (IsPhase2)
            GoTo(_brother.BrotherWireWhip);
        else
            GoTo(_brother.BrotherBind);
    }

    private IEnumerator DashToPlayer()
    {
        if (_brother.Rb == null || _brother.PlayerTransform == null)
        {
            GoTo(Machine.Vulnerable);
            yield break;
        }

        float elapsed = 0f;
        float speed   = _brother.SlashSpeed
                        * (Data != null ? Data.GetSpeedMultiplier(_brother.CurrentPhase) : 1f);

        while (elapsed < DASH_TIMEOUT)
        {
            float dist = Vector2.Distance(
                _brother.transform.position,
                _brother.PlayerTransform.position
            );

            if (dist <= _brother.DashCloseRange) yield break;  // 근접 완료

            Machine.FacePlayer();
            Vector2 dir = _brother.GetDirectionToPlayer();
            dir.y       = 0f;
            _brother.Rb.linearVelocity = dir.normalized * speed;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 타임아웃 — 플레이어 못 따라잡음
        _brother.Rb.linearVelocity = Vector2.zero;
    }

    private void ApplySlash()
    {
        if (_brother.PlayerTransform == null) return;

        float dist = Vector2.Distance(
            _brother.transform.position,
            _brother.PlayerTransform.position
        );

        if (dist > _brother.SlashRange) return;

        _brother.PlayerTransform.GetComponentInParent<IHealth>()
            ?.TakeDamage(_brother.SlashDamage);

        HitStopManager.Instance?.Request(
            Data != null ? Data.hitStopDurationOnHit : 0.05f,
            Data != null ? Data.hitStopTimeScaleOnHit : 0.1f
        );
    }
}
