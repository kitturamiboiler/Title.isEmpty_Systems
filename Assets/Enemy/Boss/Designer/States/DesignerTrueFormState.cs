using System.Collections;
using UnityEngine;

/// <summary>
/// 설계자 Phase 2: 진면목.
///
/// 우산 없음. 직접 전투 개시.
///
/// 패턴:
///   A. DashSlash  — 플레이어 방향으로 순간 접근 후 근접 슬래시
///   B. BoltBarrage — 패리 가능 탄막 3발 연속 발사
///   C. ReversePary — 플레이어 패리 감지 시 즉시 구속 와이어 발사 (역패리)
///
/// Struggle 트리거:
///   HP가 1 life 남으면 DesignerStruggleState로 전이.
///
/// 방어 설계:
///   - Phase 2 진입 후 OnDeflected를 구독: 패리 성공 시 FireCounterWire 발동
///   - Struggle 전이는 1회만 (이미 Struggle 상태이면 재전이 금지)
/// </summary>
public class DesignerTrueFormState : BossState
{
    private readonly EnemyBossDesigner _designer;
    private Coroutine _patternRoutine;
    private bool      _hasTriggeredStruggle;

    private const float DASH_SPEED         = 16f;
    private const float SLASH_RANGE        = 1.5f;
    private const float SLASH_DAMAGE       = 1f;
    private const float BOLT_INTERVAL      = 0.4f;
    private const float PATTERN_INTERVAL   = 2.5f;
    private const float TELEGRAPH_TIME     = 0.5f;

    public DesignerTrueFormState(BossStateMachine machine) : base(machine)
    {
        _designer = machine as EnemyBossDesigner;
    }

    public override void Enter()
    {
        _hasTriggeredStruggle = false;
        _patternRoutine       = Machine.StartCoroutine(PatternLoop());
    }

    public override void Tick()
    {
        // Struggle 트리거: Phase 2에서 1 life 도달
        if (!_hasTriggeredStruggle
            && Health != null
            && Health.CurrentLives <= 1
            && _designer?.DesignerStruggle != null)
        {
            _hasTriggeredStruggle = true;
            GoTo(_designer.DesignerStruggle);
        }
    }

    public override void FixedTick() { }

    public override void Exit()
    {
        if (_patternRoutine != null)
        {
            Machine.StopCoroutine(_patternRoutine);
            _patternRoutine = null;
        }
    }

    // ─── 패턴 루프 ────────────────────────────────────────────────────────────

    private IEnumerator PatternLoop()
    {
        int turn = 0;

        while (true)
        {
            yield return new WaitForSeconds(PATTERN_INTERVAL);
            if (Machine.PlayerTransform == null) continue;

            switch (turn % 2)
            {
                case 0: yield return DashSlashRoutine();  break;
                case 1: yield return BoltBarrageRoutine(); break;
            }
            turn++;
        }
    }

    // ── A. 대시 슬래시 ────────────────────────────────────────────────────────

    private IEnumerator DashSlashRoutine()
    {
        if (Machine.Rb == null || Machine.PlayerTransform == null) yield break;

        // 전조
        Machine.Rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(TELEGRAPH_TIME);

        // 순간 대시
        Vector2 dir       = Machine.GetDirectionToPlayer();
        Vector2 targetPos = (Vector2)Machine.PlayerTransform.position + dir * -1.2f;

        Machine.Rb.MovePosition(targetPos);
        Machine.FacePlayer();

        // 슬래시 판정
        float dist = Vector2.Distance(Machine.transform.position, Machine.PlayerTransform.position);
        if (dist <= SLASH_RANGE)
        {
            Machine.PlayerTransform.GetComponent<IHealth>()?.TakeDamage(SLASH_DAMAGE);
            HitStopManager.Instance?.Request(
                Data?.hitStopDurationOnHit ?? 0.05f,
                Data?.hitStopTimeScaleOnHit ?? 0.1f
            );
        }

        yield return new WaitForSeconds(0.4f);
    }

    // ── B. 볼트 바라지 ────────────────────────────────────────────────────────

    private IEnumerator BoltBarrageRoutine()
    {
        if (Machine.PlayerTransform == null) yield break;

        Machine.Rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(TELEGRAPH_TIME);

        for (int i = 0; i < 3; i++)
        {
            Machine.FacePlayer();
            float   angle  = (i - 1) * 20f; // -20, 0, +20도 퍼짐
            Vector2 dir    = (Vector2)(UnityEngine.Quaternion.Euler(0f, 0f, angle)
                             * Machine.GetDirectionToPlayer());

            var proj = Machine.SpawnParryableProjectile(dir.normalized);
            if (proj != null)
            {
                // 역패리: 패리 성공 시 즉시 구속 와이어 반격
                proj.OnDeflected += OnPlayerParriedInPhase2;
            }

            yield return new WaitForSeconds(BOLT_INTERVAL);
        }
    }

    // ─── 역패리 핸들러 ────────────────────────────────────────────────────────

    private void OnPlayerParriedInPhase2()
    {
        if (Machine.PlayerTransform == null) return;
        _designer?.FireCounterWire(Machine.PlayerTransform.position);
        // TODO(기획): 역패리 경고 이펙트 — 2026-04-02
    }
}
