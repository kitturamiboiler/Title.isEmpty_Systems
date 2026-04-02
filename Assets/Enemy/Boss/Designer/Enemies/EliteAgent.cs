using System.Collections;
using UnityEngine;

/// <summary>
/// Wave 1/3 소환 요원. 기동성 테스트용 다수 등장 적.
///
/// AI: 플레이어 방향 추적 → 근접 시 슬래시 공격.
/// IHealth만 구현 (그랩 불가 — 빠르고 가볍기 때문).
///
/// DesignerWaveState가 OnAgentDied 이벤트를 구독하여 Wave 완료 여부를 판단한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EliteAgent : MonoBehaviour, IHealth
{
    [Header("Agent — Combat")]
    [SerializeField] private float _moveSpeed    = 4.5f;
    [SerializeField] private float _attackRange  = 1.2f;
    [SerializeField] private float _attackDamage = 0.5f;
    [SerializeField] private float _attackCooldown = 1.2f;
    [SerializeField] private float _maxHealth    = 2f;

    [Header("Agent — Refs")]
    [SerializeField] private GameObject _deathFxPrefab;

    // ─── IHealth ──────────────────────────────────────────────────────────────

    public float MaxHealth     => _maxHealth;
    public float CurrentHealth { get; private set; }

    // ─── 이벤트 ───────────────────────────────────────────────────────────────

    public System.Action<EliteAgent> OnAgentDied;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private Transform _playerTransform;
    private Rigidbody2D _rb;
    private float _attackTimer;
    private bool  _isDead;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        CurrentHealth    = _maxHealth;
        _playerTransform = Object.FindFirstObjectByType<PlayerBlinkController2D>()?.transform;
    }

    private void FixedUpdate()
    {
        if (_isDead || _playerTransform == null) return;

        Vector2 dir  = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        float   dist = Vector2.Distance(transform.position, _playerTransform.position);

        if (dist > _attackRange)
        {
            _rb.linearVelocity = dir * _moveSpeed;
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
            TryAttack();
        }
    }

    // ─── IHealth ──────────────────────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        CurrentHealth -= damage;
        if (CurrentHealth <= 0f) Die();
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

        OnAgentDied?.Invoke(this);
        Destroy(gameObject, 0.1f);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void TryAttack()
    {
        _attackTimer += Time.fixedDeltaTime;
        if (_attackTimer < _attackCooldown) return;
        _attackTimer = 0f;

        _playerTransform.GetComponent<IHealth>()?.TakeDamage(_attackDamage);
    }
}
