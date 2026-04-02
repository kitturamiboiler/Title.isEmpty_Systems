using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 2+ 반격 패턴 (Counter Strike) — 정식 구현.
///
/// 트리거:
///   EnemyBossBrother가 PlayerBlinkController2D.OnBlinkExecuted를 구독.
///   블링크 착지점이 CounterRange 이내면 BrotherWireWhip/Bind 이후 이 State로 전이.
///
/// 흐름:
///   1. 전조 0.05초 (반응 불가 수준 — 플레이어가 위치를 신중히 골라야 한다)
///   2. 마지막 블링크 착지점(_brother.LastPlayerBlinkPos)으로 순간이동
///   3. 도착 후 즉각 슬래시 — 플레이어가 그 위치에 있으면 피격
///   4. → BossVulnerableState
///
/// 설계 의도:
///   '형제'는 플레이어의 블링크 패턴을 기억한다. 예측 불가한 위치로 블링크하거나,
///   반격 시점을 읽고 일부러 범위 밖으로 이동하는 플레이가 정답.
/// </summary>
public class BrotherCounterState : BossState
{
    private readonly EnemyBossBrother _brother;
    private Coroutine _routine;

    private const float TELEPORT_DELAY = 0.05f;  // 반응 불가 수준의 짧은 전조

    public BrotherCounterState(EnemyBossBrother brother) : base(brother)
    {
        _brother = brother;
    }

    public override void Enter()
    {
        if (_brother.Rb != null)
            _brother.Rb.linearVelocity = Vector2.zero;

        _routine = Machine.StartCoroutine(CounterRoutine());
    }

    public override void Tick()      { }
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

    private IEnumerator CounterRoutine()
    {
        // 전조: 거의 즉각적 (플레이어가 블링크 위치를 미리 신중히 골라야 함)
        yield return new WaitForSeconds(TELEPORT_DELAY);

        // 마지막 블링크 착지점으로 순간이동
        if (_brother.Rb != null)
        {
            Vector2 counterPos = _brother.LastPlayerBlinkPos;

            // 플레이어와 겹치지 않도록 약간 앞쪽 오프셋
            if (Machine.PlayerTransform != null)
            {
                Vector2 toPlayer = ((Vector2)Machine.PlayerTransform.position - counterPos).normalized;
                counterPos += toPlayer * 0.8f;
            }

            _brother.Rb.MovePosition(counterPos);
        }

        Machine.FacePlayer();

        // 착지 즉시 슬래시 판정
        if (Machine.PlayerTransform != null)
        {
            float dist = Vector2.Distance(
                Machine.transform.position,
                Machine.PlayerTransform.position
            );

            if (dist <= _brother.SlashRange)
            {
                Machine.PlayerTransform.GetComponentInParent<IHealth>()
                    ?.TakeDamage(_brother.SlashDamage);

                HitStopManager.Instance?.Request(
                    Data != null ? Data.hitStopDurationOnHit : 0.05f,
                    Data != null ? Data.hitStopTimeScaleOnHit : 0.1f
                );
            }
        }

        // 짧은 리커버리 후 취약
        yield return new WaitForSeconds(0.3f);
        GoTo(Machine.Vulnerable);
    }
}
