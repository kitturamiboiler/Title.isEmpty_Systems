using UnityEngine;

// ─── Phase 정의 ──────────────────────────────────────────────────────────────

/// <summary>보스 전투 페이즈 (3단계). 수치 비교를 위해 int 기반.</summary>
public enum BossPhase { Phase1 = 1, Phase2 = 2, Phase3 = 3 }

// ─── BossStateMachine ────────────────────────────────────────────────────────

/// <summary>
/// 5인의 시련 공통 FSM 허브 (추상 클래스).
///
/// 계층 구조:
///   BossStateMachine (abstract)
///     └─ EnemyBossHound      : BossStateMachine
///     └─ EnemyBossPaper      : BossStateMachine
///     └─ (...)
///
/// 서브클래스 구현 의무:
///   protected override void InitializeStates() — 보스 전용 State 인스턴스 생성
///   protected virtual  void OnPhaseChanged(BossPhase) — 페이즈 전환 시 커스텀 처리
///
/// 공통 State (모든 보스 포함):
///   Idle, Vulnerable, Grabbed, Dead
///
/// 사용 예:
/// <code>
/// public class EnemyBossHound : BossStateMachine
/// {
///     public HoundChargeState HoundCharge { get; private set; }
///
///     protected override void InitializeStates()
///     {
///         HoundCharge = new HoundChargeState(this);
///     }
/// }
/// </code>
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(BossHealth))]
/// <summary>
/// IBindable 확장: 플레이어가 보스를 일시 구속할 수 있는 인터페이스.
/// Shadow 보스의 잔상 고정, Designer 보스의 군단원 구속 등에 활용.
/// 기본 구현은 Rigidbody 정지 + AI 일시 중단. 서브클래스 오버라이드 가능.
/// </summary>
public abstract class BossStateMachine : MonoBehaviour, IBindable
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Boss Core Refs")]
    [SerializeField] protected BossData      _data;
    [SerializeField] protected Transform     _playerTransform;
    [SerializeField] protected Transform     _firePoint;
    [SerializeField] protected LayerMask     _groundMask;

    [Header("Boss Ground Check")]
    [SerializeField] protected float _groundCheckDistance = 0.08f;
    [SerializeField] protected float _groundCheckBoxWidth = 0.8f;

    // ─── 컴포넌트 캐시 ────────────────────────────────────────────────────────

    /// <summary>보스 Rigidbody2D.</summary>
    public Rigidbody2D Rb  { get; private set; }

    /// <summary>보스 Collider2D.</summary>
    public Collider2D  Col { get; private set; }

    /// <summary>보스 체력 컴포넌트.</summary>
    public BossHealth  Health { get; private set; }

    // ─── 외부 참조 ────────────────────────────────────────────────────────────

    /// <summary>플레이어 Transform. Idle/감지 판정용.</summary>
    public Transform PlayerTransform => _playerTransform;

    /// <summary>보스 수치 ScriptableObject.</summary>
    public BossData Data => _data;

    // ─── 페이즈 ───────────────────────────────────────────────────────────────

    /// <summary>현재 전투 페이즈.</summary>
    public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

    /// <summary>Phase2 이상 여부.</summary>
    public bool IsPhase2 => CurrentPhase >= BossPhase.Phase2;

    /// <summary>Phase3 최종 페이즈 여부.</summary>
    public bool IsPhase3 => CurrentPhase >= BossPhase.Phase3;

    // ─── 공통 State ───────────────────────────────────────────────────────────

    /// <summary>플레이어 미감지 대기 상태.</summary>
    public BossIdleState       Idle       { get; private set; }

    /// <summary>공격 후 피격 가능 창 상태.</summary>
    public BossVulnerableState Vulnerable { get; private set; }

    /// <summary>플레이어에게 잡힌 상태. AI 비활성 + Kinematic 전환.</summary>
    public BossGrabbedState    Grabbed    { get; private set; }

    /// <summary>사망 처리 상태 (Terminal).</summary>
    public BossDeadState       Dead       { get; private set; }

    // ─── 현재 상태 ────────────────────────────────────────────────────────────

    /// <summary>현재 활성 State.</summary>
    public IBossState CurrentState { get; private set; }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        Rb     = GetComponent<Rigidbody2D>();
        Col    = GetComponent<Collider2D>();
        Health = GetComponent<BossHealth>();

        if (Rb == null)     Debug.LogError($"[{GetType().Name}] Rigidbody2D missing on {gameObject.name}");
        if (Col == null)    Debug.LogError($"[{GetType().Name}] Collider2D missing on {gameObject.name}");
        if (Health == null) Debug.LogError($"[{GetType().Name}] BossHealth missing on {gameObject.name}");
        if (_data == null)  Debug.LogError($"[{GetType().Name}] BossData not assigned on {gameObject.name}");

        // 공통 State 초기화
        Idle       = new BossIdleState(this);
        Vulnerable = new BossVulnerableState(this);
        Grabbed    = new BossGrabbedState(this);
        Dead       = new BossDeadState(this);

        // 보스별 State 초기화 (서브클래스 구현)
        InitializeStates();
    }

    protected virtual void Start()
    {
        if (Health == null) return;

        // BossHealth 이벤트 구독
        Health.OnDied.AddListener(OnBossDied);
        Health.OnDamaged.AddListener(OnBossDamaged);
        Health.OnGrabbed.AddListener(() => ChangeState(Grabbed));
        Health.OnGrabReleased.AddListener(OnBossGrabReleased);

        ChangeState(Idle);
    }

    protected virtual void Update()
    {
        CurrentState?.Tick();
    }

    protected virtual void FixedUpdate()
    {
        CurrentState?.FixedTick();
    }

    // ─── FSM 핵심 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 안전한 상태 전이.
    /// StopAllCoroutines() 선행 → 이전 상태의 코루틴이 Exit 이후에 재개되는 경쟁 조건 차단.
    /// null 전달 시 LogError 후 무시.
    /// </summary>
    public void ChangeState(IBossState newState)
    {
        if (newState == null)
        {
            Debug.LogError($"[{GetType().Name}] ChangeState: null State 전달. 전이 거부.");
            return;
        }

        // 가설 1 방어: 코루틴이 Exit보다 1프레임 늦게 재개될 경우 상태 오염 원천 차단.
        // 보스의 모든 코루틴은 State 소유이므로 전역 중단이 안전하다.
        StopAllCoroutines();

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    // ─── 페이즈 관리 ──────────────────────────────────────────────────────────

    private void OnBossDamaged(int lives)
    {
        if (_data == null) return;

        if (CurrentPhase == BossPhase.Phase1 && lives <= _data.phase2LivesThreshold)
            TransitionToPhase(BossPhase.Phase2);
        else if (CurrentPhase == BossPhase.Phase2 && lives <= _data.phase3LivesThreshold)
            TransitionToPhase(BossPhase.Phase3);
    }

    private void TransitionToPhase(BossPhase newPhase)
    {
        if (newPhase <= CurrentPhase) return;
        CurrentPhase = newPhase;

        // 페이즈 전환 대사 자동 발동 (BossCombatDialogue가 없으면 조용히 스킵)
        GetComponent<BossCombatDialogue>()?.TriggerPhase(newPhase);

        OnPhaseChanged(newPhase);
    }

    /// <summary>
    /// 페이즈 전환 시 서브클래스 커스텀 처리. 기본 구현은 아무 것도 하지 않는다.
    /// 예: Phase2 진입 시 갑옷 재충전, 새 공격 패턴 언락 등.
    /// </summary>
    protected virtual void OnPhaseChanged(BossPhase newPhase) { }

    // ─── 이벤트 핸들러 ────────────────────────────────────────────────────────

    private void OnBossDied()
    {
        ChangeState(Dead);
    }

    private void OnBossGrabReleased()
    {
        if (Health == null) return;

        // SlamState가 아직 TakeSlamDamage를 호출하기 전 — Lives > 0이면 임시 Vulnerable
        if (Health.CurrentLives > 0)
            ChangeState(Vulnerable);
        // Lives == 0 은 Die() → OnBossDied() → Dead 처리
    }

    // ─── 공통 헬퍼 ────────────────────────────────────────────────────────────

    /// <summary>박스캐스트 기반 착지 판정.</summary>
    public bool IsBossGrounded()
    {
        if (Col == null) return false;

        return Physics2D.BoxCast(
            new Vector2(Col.bounds.center.x, Col.bounds.min.y),
            new Vector2(Col.bounds.size.x * _groundCheckBoxWidth, 0.05f),
            0f,
            Vector2.down,
            _groundCheckDistance,
            _groundMask
        ).collider != null;
    }

    /// <summary>플레이어 방향 단위벡터. PlayerTransform 없으면 Vector2.right.</summary>
    public Vector2 GetDirectionToPlayer()
    {
        if (_playerTransform == null) return Vector2.right;
        return (_playerTransform.position - transform.position).normalized;
    }

    /// <summary>플레이어를 향해 localScale.x 뒤집기.</summary>
    public void FacePlayer()
    {
        if (_playerTransform == null) return;

        float dir = _playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(dir) < 0.01f) return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (dir > 0f ? 1f : -1f);
        transform.localScale = scale;
    }

    /// <summary>기본 투사체 발사. 서브클래스에서 오버라이드해 패리 가능 버전도 발사 가능.</summary>
    public virtual void SpawnProjectile(Vector2 direction)
    {
        if (_data == null || _data.projectilePrefab == null) return;
        if (_firePoint == null)
        {
            Debug.LogWarning($"[{GetType().Name}] FirePoint가 연결되지 않았습니다.");
            return;
        }

        var go = Object.Instantiate(
            _data.projectilePrefab,
            _firePoint.position,
            Quaternion.identity
        );
        go.GetComponent<BossProjectile2D>()?.Launch(
            direction,
            _data.projectileSpeed,
            _data.projectileDamage,
            _data.projectileLifetime
        );

        Destroy(go, _data.projectileLifetime + 0.5f);
    }

    /// <summary>패리 가능 투사체 발사. Parry 시스템이 연결되면 자동으로 PlayerBlink 리필 트리거.</summary>
    /// <returns>런치된 <see cref="BossParryableProjectile2D"/> 또는 실패 시 null.</returns>
    public virtual BossParryableProjectile2D SpawnParryableProjectile(Vector2 direction)
    {
        if (_data == null || _data.parryableProjectilePrefab == null) return null;
        if (_firePoint == null)
        {
            Debug.LogWarning($"[{GetType().Name}] FirePoint가 연결되지 않았습니다.");
            return null;
        }

        var go = Object.Instantiate(
            _data.parryableProjectilePrefab,
            _firePoint.position,
            Quaternion.identity
        );
        var proj = go.GetComponent<BossParryableProjectile2D>();
        proj?.Launch(
            direction,
            _data.projectileSpeed,
            _data.projectileDamage,
            _data.projectileLifetime,
            isParryable: true
        );

        Destroy(go, _data.projectileLifetime + 0.5f);
        return proj;
    }

    // ─── IBindable (보스 구속) ────────────────────────────────────────────────

    private Coroutine _bindRoutine;

    /// <summary>
    /// 플레이어가 보스를 일시 구속.
    /// 기본: Rigidbody 정지 + duration 후 자동 해제.
    /// Shadow 잔상 고정 / Designer 군단원 구속 등 서브클래스 오버라이드 가능.
    /// </summary>
    public virtual void Bind(float duration)
    {
        if (duration <= 0f) return;
        if (_bindRoutine != null) StopCoroutine(_bindRoutine);
        _bindRoutine = StartCoroutine(BossBindRoutine(duration));
    }

    /// <summary>외부에서 보스 구속을 즉시 해제.</summary>
    public virtual void Unbind()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }
        if (Rb != null) Rb.bodyType = RigidbodyType2D.Dynamic;
    }

    private System.Collections.IEnumerator BossBindRoutine(float duration)
    {
        if (Rb != null)
        {
            Rb.linearVelocity = Vector2.zero;
            Rb.bodyType       = RigidbodyType2D.Kinematic;
        }
        // TODO(기획): 보스 구속 전용 시각 이펙트 추가 — 2026-04-01
        yield return new WaitForSeconds(duration);
        Unbind();
    }

    // ─── 서브클래스 계약 ──────────────────────────────────────────────────────

    /// <summary>
    /// 보스별 State 인스턴스 생성 메서드. Awake 내부에서 공통 State 초기화 직후 호출.
    /// </summary>
    protected abstract void InitializeStates();

    /// <summary>
    /// Idle → 첫 공격 State 전이 시 반환할 State.
    /// 보스별로 오버라이드. null 반환 시 Idle 유지.
    /// </summary>
    public virtual IBossState GetFirstAttackState() => null;
}
