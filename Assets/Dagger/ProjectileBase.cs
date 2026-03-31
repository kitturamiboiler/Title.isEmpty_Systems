using UnityEngine;

/// <summary>
/// 순수 2D 투사체 공통 베이스. <see cref="IProjectile2D"/> 계약 준수.
/// 이전의 3D 기반 ProjectileBase (Rigidbody, RaycastHit)를 완전히 대체한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class ProjectileBase2D : MonoBehaviour, IProjectile2D
{
    [Header("Base Refs (ProjectileBase2D)")]
    [SerializeField] protected WeaponData weaponData;
    [SerializeField] protected Rigidbody2D rb;

    protected Vector2 LaunchOrigin { get; private set; }
    protected Vector2 LaunchDirection { get; private set; }
    protected bool IsLaunched { get; private set; }

    protected virtual void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }

    /// <inheritdoc/>
    public virtual void Launch(Vector2 origin, Vector2 direction, WeaponData data)
    {
        if (data == null)
        {
            Debug.LogError($"[{nameof(Launch)}] WeaponData is null on {gameObject.name}. 발사를 중단합니다.");
            return;
        }

        weaponData = data;
        LaunchOrigin = origin;
        LaunchDirection = direction.normalized;
        transform.position = origin;
        IsLaunched = true;

        if (rb == null)
        {
            Debug.LogError($"[{nameof(Launch)}] Rigidbody2D missing on {gameObject.name}.");
            return;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearVelocity = LaunchDirection * weaponData.projectileSpeed;

        OnLaunched();
    }

    /// <inheritdoc/>
    public virtual void OnHit(Collision2D collision)
    {
        if (collision == null) return;

        Vector2 normal = collision.contactCount > 0
            ? (Vector2)collision.GetContact(0).normal
            : Vector2.up;

        HandleDamage(collision);
        PlayHitEffect((Vector2)collision.GetContact(0).point, normal);
    }

    /// <inheritdoc/>
    public virtual bool CanBlink => false;

    /// <inheritdoc/>
    public virtual Vector2 CurrentPosition => transform.position;

    /// <summary>발사 직후 한 번 호출되는 훅. 서브클래스 커스텀 초기화 용도.</summary>
    protected virtual void OnLaunched() { }

    /// <summary>충돌 대상에게 데미지를 적용한다. 서브클래스에서 override 가능.</summary>
    protected virtual void HandleDamage(Collision2D collision)
    {
        if (weaponData == null) return;

        var health = collision.gameObject.GetComponentInParent<IHealth>();
        if (health == null) return;

        health.TakeDamage(weaponData.damage);
    }

    /// <summary>
    /// 충돌 지점에 이펙트를 재생한다. EffectManager가 수명을 책임진다.
    /// </summary>
    protected virtual void PlayHitEffect(Vector2 position, Vector2 normal)
    {
        if (weaponData == null || weaponData.hitParticle == null) return;

        EffectManager.Instance?.SpawnEffect(weaponData.hitParticle, position, normal);
    }
}
