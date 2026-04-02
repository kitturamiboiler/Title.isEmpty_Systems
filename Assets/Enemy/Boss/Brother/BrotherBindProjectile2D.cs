using UnityEngine;

/// <summary>
/// Boss 3 [Brother] 구속 와이어 투사체.
///
/// 플레이어에 명중 시:
///   IBindable.Bind(bindDuration) 호출 → PlayerBoundState 진입.
///
/// 패리 불가:
///   이 투사체는 BossParryableProjectile2D가 아니므로 패리 판정 없음.
///   회피 방법: Blink 또는 점프로 와이어 라인을 피해야 한다.
///
/// 발사체 특성:
///   낮은 속도, 넓은 판정 — 예고 시간이 충분해 회피 가능하되 실수하면 구속.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BrotherBindProjectile2D : MonoBehaviour
{
    // ─── Private ──────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private float       _bindDuration;
    private bool        _hasHit;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
            Debug.LogError($"[BrotherBindProjectile2D] Rigidbody2D missing on {gameObject.name}");
    }

    // ─── 공개 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 투사체 발사.
    /// </summary>
    /// <param name="direction">이동 방향.</param>
    /// <param name="speed">이동 속도. 낮을수록 회피 기회 ↑.</param>
    /// <param name="bindDuration">명중 시 플레이어 구속 지속 시간 (초).</param>
    /// <param name="lifetime">자동 파괴 시간 (초).</param>
    public void Launch(Vector2 direction, float speed, float bindDuration, float lifetime)
    {
        if (_rb == null) return;

        _bindDuration         = bindDuration;
        _rb.linearVelocity    = direction.normalized * speed;
        Destroy(gameObject, lifetime);
    }

    // ─── 충돌 ─────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;

        int layer = other.gameObject.layer;

        // 지형 충돌 → 소멸
        if (layer == Layers.Wall || layer == Layers.Ground)
        {
            Destroy(gameObject);
            return;
        }

        // 무적 플레이어 무시
        if (layer == Layers.PlayerInvincible) return;

        // 플레이어 충돌 → 구속
        if (layer == Layers.Player)
        {
            _hasHit = true;

            var bindable = other.GetComponentInParent<IBindable>();
            if (bindable == null)
            {
                Debug.LogWarning("[BrotherBindProjectile2D] IBindable을 찾지 못했습니다. 구속 불가.");
            }
            else
            {
                bindable.Bind(_bindDuration);
            }

            Destroy(gameObject);
        }
    }
}
