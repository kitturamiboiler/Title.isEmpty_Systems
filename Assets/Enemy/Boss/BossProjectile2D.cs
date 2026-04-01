using UnityEngine;

/// <summary>
/// 보스 전용 2D 투사체.
/// - Trigger 기반: 벽/바닥 충돌 시 자폭, 플레이어 충돌 시 데미지 후 자폭.
/// - PlayerInvincible 레이어는 레이어 충돌 매트릭스에서 제외 → 무적 중 피격 없음.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossProjectile2D : MonoBehaviour
{
    private Rigidbody2D _rb;
    private float _damage;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 투사체를 발사한다.
    /// </summary>
    /// <param name="direction">정규화된 발사 방향.</param>
    /// <param name="speed">이동 속도.</param>
    /// <param name="damage">플레이어에게 입힐 데미지.</param>
    /// <param name="lifetime">자폭까지 최대 생존 시간(초).</param>
    public void Launch(Vector2 direction, float speed, float damage, float lifetime)
    {
        if (_rb == null)
        {
            Debug.LogError($"[BossProjectile2D] Rigidbody2D 없음 — {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        _damage             = damage;
        _rb.bodyType        = RigidbodyType2D.Dynamic;
        _rb.gravityScale    = 0f;
        _rb.linearVelocity  = direction.normalized * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int layer = other.gameObject.layer;

        // 벽 / 바닥 → 자폭
        if (layer == Layers.Wall || layer == Layers.Ground)
        {
            Destroy(gameObject);
            return;
        }

        // 플레이어만 타격 (PlayerInvincible 레이어는 충돌 매트릭스에서 이미 제외 권장)
        if (layer != Layers.Player) return;

        var health = other.GetComponentInParent<IHealth>();
        health?.TakeDamage(_damage);
        Destroy(gameObject);
    }
}
