using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 3 전용: 피스톤 3개 파괴 후 분노 상태.
///
/// 패턴: 고속 Charge 반복. Vulnerable 창 없음 (BossVulnerable 미사용).
/// 보스 체력이 1로 고정 → 마지막 IsGrabbable 트리거 조건.
///
/// 탈출:
///   BossHealth.TakeDamage(execution) → ArmorGauge 소진 → IsGrabbable = true
///   → PlayerBlinkController2D가 Grab 트리거 → GrabState → SlamState → 사망
/// </summary>
public class HoundFrenzyState : BossState
{
    private readonly EnemyBossHound _hound;
    private Coroutine _routine;

    // 분노 상태: Charge 후 짧은 대기만 있음
    private const float FRENZY_PAUSE_BETWEEN_CHARGES = 0.3f;

    public HoundFrenzyState(EnemyBossHound hound) : base(hound)
    {
        _hound = hound;
    }

    public override void Enter()
    {
        if (_hound.Rb == null)
        {
            Debug.LogError("[HoundFrenzyState] Rigidbody2D missing.");
            return;
        }

        // 갑옷 재충전 → 플레이어가 블링크 실행으로 다시 깎아야 함
        Health?.ResetArmor();

        _routine = Machine.StartCoroutine(FrenzyLoop());
    }

    public override void Exit()
    {
        if (_routine != null)
        {
            Machine.StopCoroutine(_routine);
            _routine = null;
        }

        if (_hound.Rb != null)
            _hound.Rb.linearVelocity = Vector2.zero;
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator FrenzyLoop()
    {
        if (Data == null) yield break;

        while (true)
        {
            // IsGrabbable 상태가 되면 플레이어의 Grab을 기다린다
            if (Health != null && Health.IsGrabbable) yield break;

            Machine.FacePlayer();
            yield return Machine.StartCoroutine(SingleCharge());

            yield return new WaitForSeconds(FRENZY_PAUSE_BETWEEN_CHARGES);
        }
    }

    private IEnumerator SingleCharge()
    {
        Vector2 dir  = _hound.GetDirectionToPlayer();
        dir.y         = 0f;
        float speed   = _hound.GetChargeSpeed();  // phase3SpeedMultiplier 적용
        _hound.Rb.linearVelocity = dir.normalized * speed;

        float elapsed = 0f;
        float duration = Data.chargeDuration * 0.6f;  // 분노 = 더 짧은 돌진

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _hound.Rb.linearVelocity = Vector2.zero;
    }
}
