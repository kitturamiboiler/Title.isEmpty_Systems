using UnityEngine;

/// <summary>
/// 2D 단검 투사체. 플레이어 블링크의 기준 위치가 되는 오브젝트.
/// - 거리 제한
/// - 벽/적 충돌 시 정보 저장
/// </summary>
public class DaggerProjectile2D : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private TrailRenderer trailRenderer;

    private Vector2 launchPosition;
    private bool isLaunched;
    private bool canBlink = true;

    // 벽에 박혔을 때 사용되는 정보
    public bool IsStuckToWall { get; private set; }
    public Vector2 LastHitNormal { get; private set; } = Vector2.up;

    // 적에게 적중했는지 여부 (연출/추가 로직용)
    public bool HitEnemy { get; private set; }

    public Vector2 CurrentPosition => transform.position;
    public bool CanBlink => canBlink;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (trailRenderer == null)
            trailRenderer = GetComponent<TrailRenderer>();
    }

    public void Launch(Vector2 origin, Vector2 direction, WeaponData data)
    {
        weaponData = data;
        launchPosition = origin;
        transform.position = origin;

        isLaunched = true;
        canBlink = true;
        IsStuckToWall = false;
        HitEnemy = false;

        if (rb != null)
        {
            rb.velocity = direction.normalized * weaponData.projectileSpeed;
        }

        if (trailRenderer != null)
        {
            trailRenderer.time = weaponData != null ? weaponData.daggerTrailTime : 0.08f;
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }
    }

    private void Update()
    {
        if (!isLaunched || weaponData == null) return;

        float distance = Vector2.Distance(launchPosition, transform.position);
        if (distance > weaponData.maxDistance)
        {
            // 거리 초과: 블링크 권한 상실 및 즉시 회수
            canBlink = false;
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLaunched) return;

        // 첫 번째 접촉 지점 기준으로 Normal 저장
        if (collision.contactCount > 0)
        {
            LastHitNormal = collision.GetContact(0).normal;
        }

        // 레이어 이름으로 벽/적 등을 구분 (프로젝트 설정에 따라 수정 가능)
        string layerName = LayerMask.LayerToName(collision.gameObject.layer);

        if (layerName == "Wall")
        {
            IsStuckToWall = true;

            // 벽에 "박힌" 느낌을 위해 속도를 0으로
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.isKinematic = true;
            }
        }
        else if (layerName == "Enemy")
        {
            HitEnemy = true;

            // 여기서 적의 체력 컴포넌트가 있다면 데미지 처리
            var health = collision.gameObject.GetComponent<IHealth>();
            if (health != null && weaponData != null)
            {
                health.TakeDamage(weaponData.damage);
            }

            // 적에게 박혀 있는 연출을 원하면 속도 0, 아니면 그대로 둬도 됨
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.isKinematic = true;
            }
        }
        else
        {
            // 기타 충돌(바닥 등)도 필요하다면 여기서 처리
        }

        // 피격 이펙트
        if (weaponData != null && weaponData.hitParticle != null)
        {
            EffectManager.Instance?.SpawnEffect(
                weaponData.hitParticle,
                collision.GetContact(0).point,
                LastHitNormal
            );
        }
    }
}

