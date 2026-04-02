using UnityEngine;

/// <summary>
/// Wave 2 NULL 사이보그 — 그랩&투척 테스트 적.
///
/// 맷집이 강하고 느리다. 일반 공격으로는 처치 효율이 낮아 플레이어가
/// 자연스럽게 Grab → Slam 루프를 시도하도록 유도한다.
///
/// IGrabbable 구현:
///   Slam 데미지만 정상 적용. 일반 TakeDamage는 절반만 받음(외골격 방어).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class NullCyborg : MonoBehaviour, IGrabbable
{
    [Header("NULL Cyborg — Combat")]
    [SerializeField] private float _moveSpeed    = 1.8f;
    [SerializeField] private float _attackRange  = 1.5f;
    [SerializeField] private float _attackDamage = 1f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private float _maxHealth    = 5f;
    [SerializeField] private float _exoskeletonDamageReduction = 0.5f;

    [Header("NULL Cyborg — Refs")]
    [SerializeField] private GameObject _deathFxPrefab;

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    public float MaxHealth     => _maxHealth;
    public float CurrentHealth { get; private set; }
    public int   CurrentLives  { get; private set; } = 2;
    public bool  IsGrabbable   => CurrentLives > 0 && !_isDead && !_isLocked;
    public new GameObject gameObject => base.gameObject;

    // ─── 이벤트 ───────────────────────────────────────────────────────────────

    public System.Action<NullCyborg> OnCyborgDied;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private Transform   _playerTransform;
    private Rigidbody2D _rb;
    private float       _attackTimer;
    private bool        _isDead;
    private bool        _isLocked;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        CurrentHealth    = _maxHealth;
        _playerTransform = Object.FindFirstObjectByType<PlayerBlinkController2D>()?.transform;
    }

    private void FixedUpdate()
    {
        if (_isDead || _isLocked || _playerTransform == null) return;

        Vector2 dir  = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        float   dist = Vector2.Distance(transform.position, _playerTransform.position);

        _rb.linearVelocity = dist > _attackRange ? dir * _moveSpeed : Vector2.zero;

        if (dist <= _attackRange)
        {
            _attackTimer += Time.fixedDeltaTime;
            if (_attackTimer >= _attackCooldown)
            {
                _attackTimer = 0f;
                _playerTransform.GetComponent<IHealth>()?.TakeDamage(_attackDamage);
            }
        }
    }

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        // 외골격 방어 — 일반 데미지 감소
        CurrentHealth -= damage * _exoskeletonDamageReduction;
        if (CurrentHealth <= 0f)
        {
            CurrentLives--;
            if (CurrentLives <= 0) Die();
            else CurrentHealth = _maxHealth;
        }
    }

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;

        if (_deathFxPrefab != null)
        {
            var fx = Instantiate(_deathFxPrefab, transform.position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        OnCyborgDied?.Invoke(this);
        Destroy(gameObject, 0.15f);
    }

    public void LockForGrab()
    {
        _isLocked          = true;
        _rb.bodyType       = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
    }

    public void ReleaseGrab(bool executePendingDeath)
    {
        _isLocked    = false;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        if (executePendingDeath) Die();
    }

    public void TakeSlamDamage(float damage)
    {
        if (_isDead) return;
        // 슬램은 외골격 무시 — 직접 체력 감소
        CurrentHealth -= damage;
        CurrentLives--;
        if (CurrentLives <= 0 || CurrentHealth <= 0f) Die();
        else CurrentHealth = _maxHealth;
    }
}
