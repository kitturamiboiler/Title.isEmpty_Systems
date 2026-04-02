using UnityEngine;

/// <summary>
/// 2D 단검 투사체. <see cref="ProjectileBase2D"/> 를 상속하여 <see cref="IProjectile2D"/> 계약을 이행한다.
/// - 거리 초과 자동 파괴
/// - 벽/바닥/적 충돌 시 박힘(Embed) 및 이펙트
/// - 적 적중 시 PlayerBlinkController2D.ImmediateBlink() 자동 호출
/// </summary>
public class DaggerProjectile2D : ProjectileBase2D
{
    [Header("Dagger Refs")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Collider2D daggerCollider;

    private bool _canBlink = true;
    private bool _embedded;

    /// <summary>PlayerBlinkController2D — Launch 시 1회 캐싱.</summary>
    private PlayerBlinkController2D _blinkCtrl;

    // -------------------------------------------------------------------------
    // IProjectile2D 오버라이드
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public override bool CanBlink => _canBlink;

    /// <inheritdoc/>
    public override Vector2 CurrentPosition => transform.position;

    /// <summary>단검이 벽/바닥에 박혔는지 여부.</summary>
    public bool IsStuckToWall { get; private set; }

    /// <summary>마지막 충돌 법선. 블링크 위치 오프셋 계산에 사용.</summary>
    public Vector2 LastHitNormal { get; private set; } = Vector2.up;

    /// <summary>적에게 적중했는지 여부 (연출·추가 로직용).</summary>
    public bool HitEnemy { get; private set; }

    /// <summary>
    /// DesignerUmbrella가 반사한 상태.
    /// true이면 PlayerBlinkController2D.TryBlinkToDagger()가 블링크를 거부한다.
    /// </summary>
    public bool IsReflected { get; private set; }

    // -------------------------------------------------------------------------
    // Unity 콜백
    // -------------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();

        if (trailRenderer == null)
            trailRenderer = GetComponent<TrailRenderer>();
        if (daggerCollider == null)
            daggerCollider = GetComponent<Collider2D>();

        // Awake 시점에 씬에 이미 컨트롤러가 있으면 캐싱 (씬 로드 후 소환 패턴)
        _blinkCtrl = Object.FindFirstObjectByType<PlayerBlinkController2D>();
    }

    /// <inheritdoc/>
    public override void Launch(Vector2 origin, Vector2 direction, WeaponData data)
    {
        if (data == null)
        {
            Debug.LogError($"[DaggerProjectile2D.Launch] WeaponData is null on {gameObject.name}.");
            return;
        }

        // 캐싱 — Awake 이전 Instantiate 시 null일 수 있으므로 Launch 시점에도 보강
        if (_blinkCtrl == null)
            _blinkCtrl = Object.FindFirstObjectByType<PlayerBlinkController2D>();

        _canBlink   = true;
        IsStuckToWall = false;
        HitEnemy    = false;
        IsReflected = false;
        _embedded   = false;
        LastHitNormal = Vector2.up;

        if (daggerCollider != null)
            daggerCollider.enabled = true;

        if (trailRenderer != null)
        {
            trailRenderer.time = data.daggerTrailTime;
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }

        base.Launch(origin, direction, data);
    }

    private void Update()
    {
        if (!IsLaunched || weaponData == null) return;

        if (Vector2.Distance(LaunchOrigin, transform.position) > weaponData.maxDistance)
        {
            _canBlink = false;
            Destroy(gameObject);
        }
    }

    // -------------------------------------------------------------------------
    // 충돌 처리
    // -------------------------------------------------------------------------

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsLaunched || _embedded) return;

        if (collision.contactCount > 0)
            LastHitNormal = collision.GetContact(0).normal;

        int layer = collision.gameObject.layer;

        if (layer == Layers.Enemy)
        {
            HandleEnemyHit(collision);
            return;
        }

        if (IsEmbedSurface(layer))
        {
            IsStuckToWall = true;
            EmbedIntoSurface();
            SpawnHitFx(collision);
            return;
        }

        SpawnHitFx(collision);
    }

    private void HandleEnemyHit(Collision2D collision)
    {
        HitEnemy = true;

        if (weaponData != null)
        {
            var health = collision.gameObject.GetComponentInParent<IHealth>();
            if (health != null)
                health.TakeDamage(weaponData.damage);
        }

        EmbedIntoSurface();
        SpawnHitFx(collision);

        if (_blinkCtrl == null)
        {
            Debug.LogWarning("[DaggerProjectile2D] PlayerBlinkController2D 캐시가 없어 자동 블링크 불가.");
            return;
        }

        _blinkCtrl.ImmediateBlink();
    }

    // -------------------------------------------------------------------------
    // IProjectile2D.OnHit — 외부에서 직접 호출할 때 진입점
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public override void OnHit(Collision2D collision)
    {
        if (collision == null) return;

        int layer = collision.gameObject.layer;

        if (collision.contactCount > 0)
            LastHitNormal = collision.GetContact(0).normal;

        if (layer == Layers.Enemy)
        {
            HandleEnemyHit(collision);
            return;
        }

        base.OnHit(collision);
    }

    // -------------------------------------------------------------------------
    // 내부 헬퍼
    // -------------------------------------------------------------------------

    /// <summary>
    /// DesignerUmbrella에서 호출. 단검을 newDirection 방향으로 반사하고 블링크 불가 상태로 전환.
    /// 반사된 단검은 플레이어 방향으로 날아가 위협이 된다.
    /// </summary>
    public void Reflect(Vector2 newDirection)
    {
        if (_embedded || IsReflected) return;

        IsReflected = true;
        _canBlink   = false; // 블링크 대상에서 즉시 제외

        if (rb != null)
        {
            rb.bodyType       = RigidbodyType2D.Dynamic;
            rb.linearVelocity = newDirection.normalized * (weaponData != null ? weaponData.speed : 12f);
        }

        if (daggerCollider != null)
            daggerCollider.enabled = true;

        if (trailRenderer != null)
            trailRenderer.emitting = true;

        // TODO(기획): 반사 이펙트 스폰 — 2026-04-02
    }

    private bool IsEmbedSurface(int layer)
    {
        if (weaponData != null && weaponData.daggerEmbedSurfaceMask.value != 0)
            return (weaponData.daggerEmbedSurfaceMask.value & (1 << layer)) != 0;

        return layer == Layers.Wall || layer == Layers.Ground;
    }

    private void EmbedIntoSurface()
    {
        _embedded = true;
        _canBlink = true;

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
        if (weaponData == null || weaponData.hitParticle == null) return;
        if (collision.contactCount <= 0) return;

        EffectManager.Instance?.SpawnEffect(
            weaponData.hitParticle,
            collision.GetContact(0).point,
            LastHitNormal
        );
    }
}
