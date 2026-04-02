using UnityEngine;

/// <summary>
/// Wave 3 엘리트 0x01 — 관절 타격 정밀 조작 시험.
///
/// 특수 메카닉:
///   어깨 관절(ArmJoint × 2)에 단검을 박아야 IsGrabbable 상태가 된다.
///   단순 공격으로는 체력이 천천히 깎이며, 관절 슬램이 효율적 클리어 루트.
///
/// ArmJoint 연동:
///   단검이 ArmJoint 레이어에 적중 → ArmJoint.SetExposed(true)를 이 클래스가 처리.
///   (실제 단검 충돌은 ArmJoint의 OnCollisionEnter2D를 통해 감지하지만,
///    간이 구현을 위해 ArmJoint.OnJointBroken 이벤트로 처리.)
///
/// 가설 1 (ArmJoint.cs에서 방어됨):
///   관절 파괴 시 본체 Rigidbody2D는 영향을 받지 않음.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EliteZeroX01 : MonoBehaviour, IHealth
{
    [Header("0x01 — Combat")]
    [SerializeField] private float _moveSpeed      = 3.2f;
    [SerializeField] private float _attackRange    = 1.4f;
    [SerializeField] private float _attackDamage   = 0.75f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private float _maxHealth      = 6f;

    [Header("0x01 — Joints")]
    [SerializeField] private ArmJoint _leftJoint;
    [SerializeField] private ArmJoint _rightJoint;

    [Header("0x01 — Refs")]
    [SerializeField] private GameObject _deathFxPrefab;

    // ─── IHealth ──────────────────────────────────────────────────────────────

    public float MaxHealth     => _maxHealth;
    public float CurrentHealth { get; private set; }

    // ─── 이벤트 ───────────────────────────────────────────────────────────────

    public System.Action<EliteZeroX01> OnZeroX01Died;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private Transform   _playerTransform;
    private Rigidbody2D _rb;
    private float       _attackTimer;
    private bool        _isDead;
    private int         _brokenJoints;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        CurrentHealth    = _maxHealth;
        _playerTransform = Object.FindFirstObjectByType<PlayerBlinkController2D>()?.transform;

        if (_leftJoint  != null) _leftJoint .OnJointBroken.AddListener(OnJointDestroyed);
        if (_rightJoint != null) _rightJoint.OnJointBroken.AddListener(OnJointDestroyed);
    }

    private void OnDestroy()
    {
        if (_leftJoint  != null) _leftJoint .OnJointBroken.RemoveListener(OnJointDestroyed);
        if (_rightJoint != null) _rightJoint.OnJointBroken.RemoveListener(OnJointDestroyed);
    }

    private void FixedUpdate()
    {
        if (_isDead || _playerTransform == null) return;

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

        OnZeroX01Died?.Invoke(this);
        Destroy(gameObject, 0.15f);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void OnJointDestroyed()
    {
        _brokenJoints++;
        // 양쪽 관절 파괴 시 즉사
        if (_brokenJoints >= 2) Die();
    }
}
