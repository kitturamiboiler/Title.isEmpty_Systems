using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// int Lives 기반 적 체력 관리. IHealth 계약 준수.
/// - 잡몹: MaxLives = 1 / 중형몹: MaxLives = 2
/// - Lives == 1 시 OnGrabbable 이벤트 발화 (GrabState 진입 신호)
/// - LockForGrab 중에는 TakeDamage · Die 모두 차단 → 가설 2(타겟 유실) 방어
/// </summary>
public class EnemyHealth : MonoBehaviour, IHealth
{
    /// <summary>Grab 가능 상태로 전환되는 Lives 임계값.</summary>
    private const int GRAB_LIVES_THRESHOLD = 1;

    [Header("Lives")]
    [Tooltip("잡몹: 1 / 중형몹: 2")]
    [SerializeField] private int _maxLives = 1;

    [Header("Death")]
    [Tooltip("사망 시 스폰할 파티클 프리팹. 비워두면 이펙트 생략.")]
    [SerializeField] private ParticleSystem _deathFxPrefab;
    [Tooltip("사망 후 GameObject 제거까지 대기 시간(초).")]
    [SerializeField] private float _destroyDelay = 0.5f;

    [Header("Events")]
    [Tooltip("피격 시 호출. 현재 Lives를 파라미터로 전달.")]
    public UnityEvent<int> OnDamaged;
    [Tooltip("Lives == 1 진입 시 호출. GrabState 진입 신호.")]
    public UnityEvent OnGrabbable;
    [Tooltip("사망 시 호출.")]
    public UnityEvent OnDied;

    /// <summary>현재 Lives.</summary>
    public int CurrentLives { get; private set; }

    /// <summary>최대 Lives.</summary>
    public int MaxLives => _maxLives;

    /// <summary>현재 Grab 가능 상태인지 여부.</summary>
    public bool IsGrabbable => CurrentLives == GRAB_LIVES_THRESHOLD && !_isDead && !_isLockedForGrab;

    private bool _isDead;

    /// <summary>
    /// GrabState 소유권 잠금 플래그.
    /// true인 동안 TakeDamage · Die 호출을 차단하고 _pendingDeath로 지연한다.
    /// </summary>
    private bool _isLockedForGrab;
    private bool _pendingDeath;

    private void Awake()
    {
        if (_maxLives <= 0)
        {
            Debug.LogError($"[EnemyHealth] MaxLives가 0 이하입니다 — {gameObject.name}. 기본값 1로 보정.");
            _maxLives = 1;
        }

        CurrentLives = _maxLives;
    }

    // -------------------------------------------------------------------------
    // IHealth
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>양수 데미지 1회 = Lives 1 감소. float 크기는 Lives 감소량에 영향 없음.</remarks>
    public void TakeDamage(float damage)
    {
        if (_isDead || _isLockedForGrab) return;

        if (damage <= 0f)
        {
            Debug.LogWarning($"[EnemyHealth] 비양수 데미지({damage}) 입력 — {gameObject.name}. 무시.");
            return;
        }

        CurrentLives--;
        CurrentLives = Mathf.Max(CurrentLives, 0);

        OnDamaged?.Invoke(CurrentLives);

        if (CurrentLives == GRAB_LIVES_THRESHOLD)
            OnGrabbable?.Invoke();

        if (CurrentLives <= 0)
            Die();
    }

    /// <inheritdoc/>
    public void Die()
    {
        if (_isDead) return;

        if (_isLockedForGrab)
        {
            _pendingDeath = true;
            return;
        }

        ExecuteDeath();
    }

    // -------------------------------------------------------------------------
    // Grab 소유권 잠금
    // -------------------------------------------------------------------------

    /// <summary>
    /// GrabState 진입 시 호출. Grab 중 외부 데미지 · Die 차단 시작.
    /// </summary>
    public void LockForGrab()
    {
        _isLockedForGrab = true;
        _pendingDeath = false;
    }

    /// <summary>
    /// GrabState 종료 시 호출. 잠금 해제 후 대기 중이던 Die를 처리한다.
    /// </summary>
    /// <param name="executePendingDeath">
    /// true면 Grab 중 쌓인 사망 요청을 즉시 실행. Slam 직접 처리 시에는 false로 전달.
    /// </param>
    public void ReleaseGrab(bool executePendingDeath = true)
    {
        _isLockedForGrab = false;

        if (executePendingDeath && _pendingDeath)
            ExecuteDeath();
    }

    // -------------------------------------------------------------------------
    // 내부 헬퍼
    // -------------------------------------------------------------------------

    private void ExecuteDeath()
    {
        if (_isDead) return;

        _isDead = true;
        OnDied?.Invoke();
        SpawnDeathFx();
        DisableGameplayComponents();
        Destroy(gameObject, _destroyDelay);
    }

    private void SpawnDeathFx()
    {
        if (_deathFxPrefab == null) return;

        EffectManager.Instance?.SpawnEffect(
            _deathFxPrefab,
            transform.position,
            Vector2.up
        );
    }

    private void DisableGameplayComponents()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.enabled = false;
    }
}
