using UnityEngine;

/// <summary>
/// 2D 단검 투사체. 플레이어 블링크의 기준 위치가 되는 오브젝트.
/// - 거리 제한
/// - 벽/바닥/적 충돌 시 정보 저장 및 고정(Embed)
/// </summary>
public class DaggerProjectile2D : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Collider2D daggerCollider;

    private Vector2 launchPosition;
    private bool isLaunched;
    private bool canBlink = true;
    private bool _embedded;

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

        if (daggerCollider == null)
            daggerCollider = GetComponent<Collider2D>();
    }

    public void Launch(Vector2 origin, Vector2 direction, WeaponData data)
    {
        weaponData = data;
        if (weaponData == null)
            return;

        launchPosition = origin;
        transform.position = origin;

        isLaunched = true;
        canBlink = true;
        IsStuckToWall = false;
        HitEnemy = false;
        _embedded = false;

        if (daggerCollider != null)
            daggerCollider.enabled = true;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            // [버그 픽스] 중력을 0으로 만들어 포물선 비행을 방지하고 레이저처럼 직진하게 만듭니다.
            rb.gravityScale = 0f;
            rb.linearVelocity = direction.normalized * weaponData.projectileSpeed;
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
            canBlink = false;
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLaunched || _embedded) return;

        if (collision.contactCount > 0)
            LastHitNormal = collision.GetContact(0).normal;

        int layer = collision.gameObject.layer;

        // 1. 적(Enemy)에게 맞았을 때
        if (IsEnemyLayer(layer))
        {
            HitEnemy = true;
            var health = collision.gameObject.GetComponentInParent<IHealth>();
            if (health != null && weaponData != null)
                health.TakeDamage(weaponData.damage);

            EmbedIntoSurface(collision);
            SpawnHitFx(collision);

            // [추가] 적중 시 플레이어를 즉시 당겨오는 '자동 블링크' 로직
            var blinkCtrl = Object.FindFirstObjectByType<PlayerBlinkController2D>();
            if (blinkCtrl != null)
            {
                // 주의: PlayerBlinkController2D에 이 동작을 실행하는 public 함수 이름을 맞춰주세요.
                // (예: ExecuteBlink, TryBlinkToDagger 등 현재 사용 중인 블링크 실행 함수명)
                blinkCtrl.TryBlinkToDagger();
            }
            return;
        }

        // 2. 벽/바닥(Wall/Ground)에 맞았을 때
        if (IsEmbedSurfaceLayer(layer))
        {
            IsStuckToWall = true;
            EmbedIntoSurface(collision);
            SpawnHitFx(collision);
            return;
        }

        if (weaponData != null && weaponData.hitParticle != null)
            SpawnHitFx(collision);
    }

    private bool IsEnemyLayer(int layer)
    {
        string layerName = LayerMask.LayerToName(layer);
        return layerName == "Enemy";
    }

    private bool IsEmbedSurfaceLayer(int layer)
    {
        if (weaponData != null && weaponData.daggerEmbedSurfaceMask.value != 0)
            return (weaponData.daggerEmbedSurfaceMask.value & (1 << layer)) != 0;

        string n = LayerMask.LayerToName(layer);
        return n == "Wall" || n == "Ground";
    }

    private void EmbedIntoSurface(Collision2D collision)
    {
        _embedded = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (daggerCollider != null)
            daggerCollider.enabled = false;

        if (trailRenderer != null)
            trailRenderer.emitting = false;
    }

    private void SpawnHitFx(Collision2D collision)
    {
        if (weaponData == null || weaponData.hitParticle == null)
            return;
        if (collision.contactCount <= 0)
            return;

        EffectManager.Instance?.SpawnEffect(
            weaponData.hitParticle,
            collision.GetContact(0).point,
            LastHitNormal
        );
    }
}
