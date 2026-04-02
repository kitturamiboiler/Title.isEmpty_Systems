using System.Collections;
using UnityEngine;

/// <summary>
/// 설계자 Phase 1: 검은 우산 공방.
///
/// 공략 루프:
///   1. 설계자가 BossParryableProjectile2D 발사 (주기적)
///   2. 플레이어 패리 성공 → OnDeflected → umbrella.Stun(_stunWindow)
///   3. 기절 창 동안 단검 던지면 우산 통과 → 설계자 본체에 박힘
///   4. 박힌 단검으로 블링크 → ProcessBlinkEnemyInteractions가 피격 처리
///      (BossHealth.TakeDamage → lives 감소 → BossVulnerableState 전이)
///
///   기절 창 없이 단검 투척 → DesignerUmbrella.Reflect() → 블링크 차단 + 반격
///
/// 방어 설계:
///   - _attackRoutine null 체크로 중복 코루틴 방지
///   - OnDeflected 구독/해제를 Enter/Exit에서 반드시 짝으로 처리
/// </summary>
public class DesignerUmbrellaPhaseState : BossState
{
    private readonly EnemyBossDesigner _designer;
    private Coroutine _attackRoutine;

    private const float MOVE_SPEED        = 2.2f;
    private const float ATTACK_INTERVAL   = 3.5f;
    private const float STUN_WINDOW       = 2.5f;  // 패리 후 우산 기절 시간
    private const float TELEGRAPH_TIME    = 0.8f;

    public DesignerUmbrellaPhaseState(BossStateMachine machine) : base(machine)
    {
        _designer = machine as EnemyBossDesigner;
        if (_designer == null)
            Debug.LogError("[DesignerUmbrellaPhaseState] BossStateMachine이 EnemyBossDesigner가 아닙니다.");
    }

    public override void Enter()
    {
        _attackRoutine = Machine.StartCoroutine(AttackLoop());
    }

    public override void Tick() { }

    public override void FixedTick()
    {
        if (Machine.Rb == null || Machine.PlayerTransform == null) return;

        // 천천히 플레이어 방향으로 이동 (우산 뒤에 숨으며 압박)
        Vector2 dir = Machine.GetDirectionToPlayer();
        Machine.Rb.linearVelocity = dir * MOVE_SPEED;
        Machine.FacePlayer();
    }

    public override void Exit()
    {
        if (_attackRoutine != null)
        {
            Machine.StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    // ─── 공격 루프 ────────────────────────────────────────────────────────────

    private IEnumerator AttackLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(ATTACK_INTERVAL);
            if (Machine.PlayerTransform == null) continue;

            // 전조
            if (Machine.Rb != null) Machine.Rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(TELEGRAPH_TIME);

            // 패리 가능 투사체 발사 + OnDeflected 구독
            var proj = Machine.SpawnParryableProjectile(Machine.GetDirectionToPlayer());
            if (proj != null)
            {
                proj.OnDeflected += OnPlayerParried;
            }
        }
    }

    private void OnPlayerParried()
    {
        // 패리 성공 → 우산 기절 창 개방
        _designer?.Umbrella?.Stun(STUN_WINDOW);
        // TODO(기획): 우산 기절 파티클 이펙트 — 2026-04-02
    }
}
