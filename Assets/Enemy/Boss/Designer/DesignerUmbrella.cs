using System.Collections;
using UnityEngine;

/// <summary>
/// 설계자의 검은 우산 — Anti-Blink Shield.
///
/// 핵심 메카닉:
///   우산이 활성 상태 → 단검 진입 즉시 Reflect() 호출 → 블링크 원천 차단.
///   우산이 기절 상태 → 단검 통과 → 설계자 본체에 박힘 → 블링크 가능.
///
/// 기절 조건:
///   DesignerUmbrellaPhaseState가 발사한 BossParryableProjectile2D를 플레이어가 패리.
///   OnDeflected 이벤트 → Stun() 호출 → _stunDuration 초간 비활성.
///
/// 방어 설계:
///   - _stunRoutine 중복 방지: 기절 중 재기절 요청 시 기존 코루틴 교체 (연장)
///   - Reflect() 반사 방향: 단검 진입 방향의 정반사 (입사 → 반사) 계산
///   - IsReflected 단검은 PlayerBlinkController2D에서 즉시 거부 처리됨 (DaggerProjectile2D.IsReflected)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DesignerUmbrella : MonoBehaviour
{
    [Header("Umbrella Settings")]
    [SerializeField] private float         _stunDuration    = 2.5f;
    [SerializeField] private SpriteRenderer _umbrellaRenderer;

    [Header("Reflect")]
    [Tooltip("단검 반사 시 플레이어 방향 유지 비율 (0=완전 반대, 1=원래 방향 유지).")]
    [SerializeField] private float _reflectBackBias = 0.3f;

    private static readonly Color ACTIVE_COLOR  = Color.white;
    private static readonly Color STUNNED_COLOR = new Color(1f, 0.35f, 0.35f, 0.65f);

    /// <summary>패리로 기절한 상태. true이면 단검이 통과한다.</summary>
    public bool IsStunned { get; private set; }

    private Coroutine _stunRoutine;
    private Transform _playerTransform;

    private void Awake()
    {
        _playerTransform = Object.FindFirstObjectByType<PlayerBlinkController2D>()?.transform;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsStunned) return; // 기절 중 → 통과

        var dagger = other.GetComponent<DaggerProjectile2D>();
        if (dagger == null) return;

        // 반사 방향: 우산 중심 → 단검 방향의 반사 + 약간 플레이어 방향 편향
        Vector2 incidentDir = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
        Vector2 reflectDir  = Vector2.Reflect(incidentDir, GetUmbrellaFacingNormal());

        // 플레이어 방향으로 약간 편향 (반사 단검이 플레이어를 향하게)
        if (_playerTransform != null)
        {
            Vector2 toPlayer = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            reflectDir = Vector2.Lerp(reflectDir, toPlayer, _reflectBackBias).normalized;
        }

        dagger.Reflect(reflectDir);
        // TODO(기획): 반사 충격 이펙트 스폰 — 2026-04-02
    }

    /// <summary>
    /// 플레이어가 설계자 투사체를 패리했을 때 DesignerUmbrellaPhaseState에서 호출.
    /// 기절 상태를 _stunDuration 초간 부여. 진행 중인 기절은 초기화 후 재시작(연장).
    /// </summary>
    public void Stun(float? durationOverride = null)
    {
        if (_stunRoutine != null) StopCoroutine(_stunRoutine);
        _stunRoutine = StartCoroutine(StunRoutine(durationOverride ?? _stunDuration));
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private IEnumerator StunRoutine(float duration)
    {
        IsStunned = true;
        if (_umbrellaRenderer != null) _umbrellaRenderer.color = STUNNED_COLOR;

        yield return new WaitForSeconds(duration);

        IsStunned = false;
        if (_umbrellaRenderer != null) _umbrellaRenderer.color = ACTIVE_COLOR;
        _stunRoutine = null;
    }

    /// <summary>우산이 향하는 방향의 법선. 기본은 오른쪽(설계자가 오른쪽을 바라볼 때).</summary>
    private Vector2 GetUmbrellaFacingNormal()
    {
        // 우산 Transform의 right 방향을 법선으로 사용 (스프라이트 피벗 기준)
        return transform.right;
    }
}
