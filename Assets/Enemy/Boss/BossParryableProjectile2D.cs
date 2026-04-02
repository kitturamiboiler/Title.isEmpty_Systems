using UnityEngine;

/// <summary>
/// 패리 가능한 보스 투사체 (2D).
///
/// 패리-블링크 연결 계약:
///   1. 플레이어의 Parry 시스템이 이 컴포넌트를 감지하고 Deflect()를 호출한다.
///   2. Deflect() 성공 시 OnDeflected 콜백 → 플레이어 AirBlink 게이지 충전.
///   3. 한 번 패리된 투사체는 다시 패리 불가 (IsParryable = false).
///
/// TODO(기획): 패리 후 방향이 보스 역방향이 아닌, 플레이어 조준 방향으로 바뀌면
///             Boss2(Paper) 드론전 전략이 풍부해진다. — 2026-04-01
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossParryableProjectile2D : MonoBehaviour
{
    // ─── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 패리 성공 시 발화.
    /// PlayerParryController(미구현)가 구독해 AirBlink 게이지를 충전한다.
    /// </summary>
    public System.Action OnDeflected;

    // ─── 상태 ─────────────────────────────────────────────────────────────────

    /// <summary>현재 패리 가능 여부. Deflect() 호출 후 false가 된다.</summary>
    public bool IsParryable { get; private set; }

    /// <summary>
    /// Deflect() 이후 true. 편향된 투사체만 드론·보스에게 데미지를 준다.
    /// 발사 직후 false — 보스 측 아군 판정 충돌 방지.
    /// </summary>
    public bool IsDeflected { get; private set; }

    // ─── Private ──────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private float       _damage;
    private float       _speed;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
            Debug.LogError($"[BossParryableProjectile2D] Rigidbody2D missing on {gameObject.name}");
    }

    // ─── 공개 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 투사체 발사.
    /// </summary>
    /// <param name="direction">이동 방향 (정규화 불필요).</param>
    /// <param name="speed">이동 속도.</param>
    /// <param name="damage">충돌 시 플레이어에게 주는 데미지.</param>
    /// <param name="lifetime">자동 파괴 시간 (초).</param>
    /// <param name="isParryable">패리 가능 여부 초기값.</param>
    public void Launch(Vector2 direction, float speed, float damage, float lifetime,
                       bool isParryable = true)
    {
        if (_rb == null) return;

        IsParryable = isParryable;
        _damage     = damage;
        _speed      = speed;

        _rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// 패리 시스템이 이 투사체를 튕겨냈을 때 호출.
    /// 방향을 반전하고 OnDeflected 콜백을 발사한다.
    /// </summary>
    /// <param name="newDirection">
    /// 반사 이후 이동 방향. null이면 현재 방향 그대로 반전.
    /// </param>
    public void Deflect(Vector2? newDirection = null)
    {
        if (!IsParryable)
        {
            Debug.LogWarning("[BossParryableProjectile2D] 이미 패리된 투사체에 Deflect 재호출.");
            return;
        }
        if (_rb == null) return;

        IsParryable  = false;
        IsDeflected  = true;

        Vector2 dir = newDirection ?? -_rb.linearVelocity.normalized;
        _rb.linearVelocity = dir.normalized * _speed;

        OnDeflected?.Invoke();

        // 패리 반응 대사 — 이 투사체를 쏜 보스의 BossCombatDialogue에 알림
        var bossDialogue = GetComponentInParent<BossCombatDialogue>()
                        ?? Object.FindFirstObjectByType<BossCombatDialogue>();
        bossDialogue?.TriggerReaction(BossCombatDialogue.ReactionType.PlayerParried);
    }

    // ─── 충돌 ─────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 가설 2 방어: 편향탄·일반탄 모두 상대방 보스 투사체를 무시한다.
        // → 아군 탄환끼리 상쇄되는 팀 식별 오류 차단.
        // Unity Physics Layer Matrix로도 해결 가능하나, 코드 명시가 인스펙터 설정보다 견고하다.
        if (other.GetComponent<BossProjectile2D>()        != null) return;
        if (other.GetComponent<BossParryableProjectile2D>() != null) return;

        int layer = other.gameObject.layer;

        // 지형 충돌 → 파괴
        if (layer == Layers.Wall || layer == Layers.Ground)
        {
            Destroy(gameObject);
            return;
        }

        // 무적 플레이어 무시
        if (layer == Layers.PlayerInvincible) return;

        // 편향 후 적(드론·보스) 충돌 — IsDeflected일 때만 적용
        if (IsDeflected && layer == Layers.Enemy)
        {
            // 보스 본체면 ArmorDamage, 아니면 일반 TakeDamage (드론 등)
            var bossHealth = other.GetComponentInParent<BossHealth>();
            if (bossHealth != null)
                bossHealth.TakeArmorDamage(_damage);
            else
                other.GetComponentInParent<IHealth>()?.TakeDamage(_damage);

            Destroy(gameObject);
            return;
        }

        // 플레이어 충돌 (편향되지 않은 원본 투사체만)
        if (!IsDeflected && layer == Layers.Player)
        {
            other.GetComponentInParent<IHealth>()?.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
