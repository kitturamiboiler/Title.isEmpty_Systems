using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 2 [Paper] 전용 드론 적.
///
/// 동작 모드:
///   ShootOnce   — DroneFormationState에서 사용. 지정 위치에 배치 후 1회 발사, 이후 대기.
///   OrbitAndShield — DroneShieldState에서 사용. 보스 주위를 공전하며 지속 발사.
///
/// 패리 연결:
///   이 드론이 발사하는 투사체는 BossParryableProjectile2D.
///   플레이어가 패리 → Deflect() → IsDeflected=true → 드론에 역반사 충돌 → TakeDamage → Die().
///
/// 드론은 자체 IHealth를 구현하지만 IGrabbable이 아니다 (그랩 불가).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PaperDrone : MonoBehaviour, IHealth
{
    // ─── 드론 동작 모드 ───────────────────────────────────────────────────────

    public enum DroneMode { ShootOnce, OrbitAndShield }

    // ─── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>파괴 완료 시 발화. EnemyBossPaper가 드론 카운트 추적에 사용.</summary>
    public System.Action<PaperDrone> OnDroneDestroyed;

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Drone — Combat")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float      _projectileSpeed    = 7f;
    [SerializeField] private float      _projectileDamage   = 1f;
    [SerializeField] private float      _projectileLifetime = 4f;
    [SerializeField] private float      _fireDelay          = 0.4f;  // 배치 후 발사까지 예고 시간

    [Header("Drone — Orbit")]
    [SerializeField] private float _orbitRadius = 2f;
    [SerializeField] private float _orbitSpeed  = 90f;  // 도/초
    [SerializeField] private float _orbitFireInterval = 1.2f;

    // ─── 런타임 ───────────────────────────────────────────────────────────────

    private DroneMode  _mode;
    private Transform  _orbitCenter;
    private float      _orbitAngle;
    private bool       _isDead;
    private Rigidbody2D _rb;
    private Coroutine  _activeRoutine;

    // ─── IHealth ──────────────────────────────────────────────────────────────

    /// <summary>파괴 전 1, 파괴 후 0.</summary>
    public int CurrentLives => _isDead ? 0 : 1;

    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        if (damage <= 0f) return;
        Die();
    }

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;

        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        // TODO(작성자): 드론 파괴 이펙트 — 2026-04-01
        OnDroneDestroyed?.Invoke(this);
        Destroy(gameObject, 0.1f);
    }

    // ─── 공개 초기화 API ──────────────────────────────────────────────────────

    /// <summary>
    /// DroneFormationState에서 호출. 1회 발사 후 대기.
    /// </summary>
    public void InitializeShootOnce(Transform target)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _mode = DroneMode.ShootOnce;
        _activeRoutine = StartCoroutine(ShootOnceRoutine(target));
    }

    /// <summary>
    /// DroneShieldState에서 호출. 보스 주위를 공전하며 지속 발사.
    /// </summary>
    /// <param name="orbitCenter">공전 중심 (보스 Transform).</param>
    /// <param name="initialAngle">초기 각도 (도). 3드론 균등 배치 시 0, 120, 240.</param>
    public void InitializeOrbit(Transform orbitCenter, float initialAngle)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _mode        = DroneMode.OrbitAndShield;
        _orbitCenter = orbitCenter;
        _orbitAngle  = initialAngle;
        _activeRoutine = StartCoroutine(OrbitAndShootRoutine());
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (_mode != DroneMode.OrbitAndShield || _orbitCenter == null || _isDead) return;

        // 공전 위치 업데이트
        _orbitAngle += _orbitSpeed * Time.fixedDeltaTime;
        float rad   = _orbitAngle * Mathf.Deg2Rad;
        Vector2 pos = (Vector2)_orbitCenter.position
                      + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _orbitRadius;
        _rb.MovePosition(pos);
    }

    // ─── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator ShootOnceRoutine(Transform target)
    {
        yield return new WaitForSeconds(_fireDelay);

        if (!_isDead && target != null)
            FireAt(target.position);

        // 발사 후 자연 대기 — FormationState가 완료 감지 후 Despawn 처리
    }

    private IEnumerator OrbitAndShootRoutine()
    {
        while (!_isDead)
        {
            yield return new WaitForSeconds(_orbitFireInterval);
            if (!_isDead)
            {
                // 플레이어 감지: 가장 가까운 Player 레이어 오브젝트
                var player = FindPlayer();
                if (player != null)
                    FireAt(player.position);
            }
        }
    }

    // ─── 내부 ─────────────────────────────────────────────────────────────────

    private void FireAt(Vector2 targetPos)
    {
        if (_projectilePrefab == null) return;

        Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
        var go = Instantiate(_projectilePrefab, transform.position, Quaternion.identity);
        go.GetComponent<BossParryableProjectile2D>()?.Launch(
            dir,
            _projectileSpeed,
            _projectileDamage,
            _projectileLifetime,
            isParryable: true
        );
        Destroy(go, _projectileLifetime + 0.5f);
    }

    private Transform FindPlayer()
    {
        // FindFirstObjectByType는 Awake에서 캐싱이 원칙이나,
        // 드론은 런타임 스폰 객체 → 초기화 시점 불확실, 발사 직전 1회만 호출
        var player = Object.FindFirstObjectByType<PlayerBlinkController2D>();
        return player != null ? player.transform : null;
    }
}
