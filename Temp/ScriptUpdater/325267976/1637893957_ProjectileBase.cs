using UnityEngine;

/// <summary>
/// 모든 투척 무기가 공통으로 사용할 기본 투사체 베이스.
/// 단검 전용 기본 로직을 포함하되, 상속으로 쉽게 확장 가능하도록 설계.
/// </summary>
public abstract class ProjectileBase : MonoBehaviour, IProjectile
{
    [SerializeField] protected WeaponData weaponData;
    [SerializeField] protected Rigidbody rb;

    protected Vector3 launchDirection;
    protected bool isLaunched;

    protected virtual void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    public virtual void Launch(Vector3 origin, Vector3 direction, WeaponData data)
    {
        weaponData = data;
        launchDirection = direction.normalized;
        transform.position = origin;

        isLaunched = true;

        if (rb != null)
        {
            rb.linearVelocity = launchDirection * weaponData.projectileSpeed;
        }

        OnLaunched();
    }

    /// <summary>
    /// 발사 직후 한 번 호출되는 훅. 각 무기별 커스텀 초기화에 사용.
    /// </summary>
    protected virtual void OnLaunched() { }

    public virtual void OnHit(RaycastHit hit)
    {
        HandleDamage(hit);
        PlayHitEffect(hit.point, hit.normal);
    }

    /// <summary>
    /// 충돌 대상에게 데미지를 적용하는 기본 단검 로직.
    /// 나중에 다른 무기는 이 메서드를 override 해서 커스텀 가능.
    /// </summary>
    protected virtual void HandleDamage(RaycastHit hit)
    {
        var health = hit.collider.GetComponent<IHealth>();
        if (health != null && weaponData != null)
        {
            health.TakeDamage(weaponData.damage);
        }
    }

    /// <summary>
    /// 피격 이펙트 재생. Sprite / Particle 은 WeaponData 에서 참조.
    /// </summary>
    protected virtual void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        if (weaponData != null && weaponData.hitParticle != null)
        {
            ParticleSystem ps = Instantiate(weaponData.hitParticle, position, Quaternion.LookRotation(normal));
            ps.Play();
        }
    }

    /// <summary>
    /// 단검 블링크 기본 로직:
    /// 플레이어(또는 캐릭터)를 현재 투사체 위치로 이동.
    /// </summary>
    public virtual void BlinkTo(Transform targetTransform)
    {
        if (targetTransform == null) return;

        targetTransform.position = transform.position;
        OnBlink(targetTransform);
    }

    /// <summary>
    /// 블링크 후 각 무기별 추가 처리용 훅.
    /// </summary>
    protected virtual void OnBlink(Transform targetTransform) { }
}

