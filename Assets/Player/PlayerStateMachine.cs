using UnityEngine;

/// <summary>
/// 플레이어 FSM 허브.
/// - Awake: 모든 State를 생성하고 의존성을 주입한다.
/// - ChangeState: 유일한 상태 전이 진입점.
///
/// 주의: PlayerBlinkController2D · PlayerMovement2D 가 독립 Update/FixedUpdate를 가지므로
/// 현재는 FSM과 기존 컨트롤러가 병렬 동작한다.
/// TODO(작성자): 입력 처리를 각 State로 이관 후 기존 컨트롤러 Update 제거 — 날짜
/// </summary>
/// <summary>
/// IBindable: BrotherBindProjectile2D가 플레이어를 구속할 때 사용.
/// </summary>
public class PlayerStateMachine : MonoBehaviour, IBindable
{
    [Header("Refs")]
    [Tooltip("비우면 Layers.PlayerPhysicsGroundMask 사용 (Movement와 동일 폴백).")]
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private WeaponData _weaponData;

    [Header("Animator (optional)")]
    [Tooltip("아트 미연결 시 비워두면 됨. PlayerAnimHashes 트리거명과 컨트롤러를 맞출 것.")]
    [SerializeField] private Animator _animator;

    // -------------------------------------------------------------------------
    // State 접근자 (GrabState.SetTarget 등 외부 호출용)
    // -------------------------------------------------------------------------

    /// <summary>대기 상태.</summary>
    public IdleState  Idle  { get; private set; }

    /// <summary>이동 상태.</summary>
    public RunState   Run   { get; private set; }

    /// <summary>블링크 상태.</summary>
    public BlinkState Blink { get; private set; }

    /// <summary>그랩 상태. 진입 전 Grab.SetTarget(enemy) 호출 필수.</summary>
    public GrabState  Grab  { get; private set; }

    /// <summary>슬램 상태.</summary>
    public SlamState  Slam  { get; private set; }

    /// <summary>Boss 3 [Brother] 구속 상태. Bind() 호출 전 SetDuration() 자동 처리.</summary>
    public PlayerBoundState Bound { get; private set; }

    /// <summary>현재 활성 State. 외부에서 직접 교체 금지.</summary>
    public IState2D CurrentState { get; private set; }

    private void Awake()
    {
        var rb        = GetComponent<Rigidbody2D>();
        var col       = GetComponent<Collider2D>();
        var blinkCtrl = GetComponent<PlayerBlinkController2D>();

        if (rb == null)
            Debug.LogError($"[PlayerStateMachine] Rigidbody2D가 없습니다 — {gameObject.name}");
        if (col == null)
            Debug.LogError($"[PlayerStateMachine] Collider2D가 없습니다 — {gameObject.name}");
        if (_weaponData == null)
            Debug.LogError($"[PlayerStateMachine] WeaponData가 할당되지 않았습니다 — {gameObject.name}");
        if (blinkCtrl == null)
            Debug.LogWarning($"[PlayerStateMachine] PlayerBlinkController2D가 없습니다 — {gameObject.name}");

        LayerMask slamGround =
            _groundMask.value != 0 ? _groundMask : Layers.PlayerPhysicsGroundMask;

        // 의존 순서: Idle 먼저 생성(SlamState가 참조) → Slam → Grab → Blink(blinkCtrl 필요)
        Idle  = new IdleState(this);
        Run   = new RunState(this);
        Slam  = new SlamState(this, rb, col, _weaponData, slamGround, blinkCtrl, Idle);
        Grab  = new GrabState(this, Slam, _weaponData);
        Blink = new BlinkState(this, blinkCtrl);
        Bound = new PlayerBoundState(this, rb, blinkCtrl);
    }

    private void Start()
    {
        ChangeState(Idle);
    }

    // -------------------------------------------------------------------------
    // FSM 핵심
    // -------------------------------------------------------------------------

    /// <summary>
    /// 상태를 전환한다. 현재 State의 Exit → 새 State의 Enter 순서를 보장.
    /// </summary>
    /// <param name="newState">전환할 State. null이면 LogError 후 무시.</param>
    public void ChangeState(IState2D newState)
    {
        if (newState == null)
        {
            Debug.LogError("[PlayerStateMachine] ChangeState: newState가 null입니다. 전이를 중단합니다.");
            return;
        }

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    // ─── IBindable ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Bind(float duration)
    {
        if (Bound == null)
        {
            Debug.LogError("[PlayerStateMachine] PlayerBoundState가 초기화되지 않았습니다.");
            return;
        }
        Bound.SetDuration(duration);
        ChangeState(Bound);
    }

    /// <inheritdoc/>
    public void Unbind()
    {
        if (CurrentState == Bound)
            ChangeState(Idle);
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Update()
    {
        CurrentState?.Tick();
    }

    private void FixedUpdate()
    {
        CurrentState?.FixedTick();
    }

    /// <summary>플레이어 Animator 트리거. 해시는 <see cref="PlayerAnimHashes"/>.</summary>
    /// <param name="triggerHash"><see cref="Animator.SetTrigger(int)"/>용 해시.</param>
    public void NotifyPlayerAnim(int triggerHash)
    {
        if (_animator == null) return;
        _animator.SetTrigger(triggerHash);
    }
}
