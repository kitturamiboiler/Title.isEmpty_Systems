using UnityEngine;

/// <summary>
/// Boss 3: 형 [Brother] — 단검-블링크 정밀도 시험.
///
/// 컨셉: 플레이어의 스승 같은 존재. 와이어 구속으로 단검 조준 실력을 테스트한다.
///       패리보다 정확한 투척 위치 선택이 핵심.
///
/// 페이즈 구조:
///   Phase 1 (lives 5~4): SlashCombo → BindAttack → Vulnerable 반복
///   Phase 2 (lives 3~2): WireWhip → BindAttack → CounterStrike 위협
///   Phase 3 (lives 1):   BindAttack + WireWhip 동시, 구속 창 단축, ArmorGauge 재충전
///
/// 구속 메카닉:
///   BrotherBindProjectile2D 명중 → PlayerBoundState 진입
///   플레이어: 단검 투척 → Shift 블링크 → 탈출 성공
///   탈출 실패 시: 데미지 + 형제가 SlashCombo로 연계
///
/// CounterStrike (Phase 2+):
///   플레이어가 단검 없이 블링크 시도 시 형제가 순간 반격.
///   TODO: PlayerBlinkController2D.OnBlinkExecuted 이벤트 연결 후 구현 — 2026-04-01
/// </summary>
public class EnemyBossBrother : BossStateMachine
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Brother — Bind Wire")]
    [SerializeField] private GameObject _bindProjectilePrefab;
    [SerializeField] private float      _bindProjectileSpeed    = 6f;
    [SerializeField] private float      _bindDurationPhase1     = 3.5f;
    [SerializeField] private float      _bindDurationPhase2     = 2.8f;
    [SerializeField] private float      _bindDurationPhase3     = 2.0f;  // Phase 3: 매우 짧음
    [SerializeField] private float      _bindProjectileLifetime = 4f;

    [Header("Brother — Slash")]
    [SerializeField] private float _slashRange    = 1.6f;
    [SerializeField] private float _slashDamage   = 1f;
    [SerializeField] private float _slashSpeed    = 8f;   // 슬래시 돌진 속도
    [SerializeField] private float _dashCloseRange = 1.8f;

    [Header("Brother — Wire Whip")]
    [SerializeField] private float _whipSpeed    = 5f;
    [SerializeField] private float _whipDamage   = 0.5f;
    [SerializeField] private float _whipLifetime = 2.5f;

    [Header("Brother — Counter Strike")]
    [SerializeField] private float _counterDamage = 1.5f;

    // ─── Brother 전용 State ───────────────────────────────────────────────────

    public BrotherSlashState    BrotherSlash    { get; private set; }
    public BrotherBindState     BrotherBind     { get; private set; }
    public BrotherWireWhipState BrotherWireWhip { get; private set; }
    public BrotherCounterState  BrotherCounter  { get; private set; }

    // ─── 블링크 반격 데이터 ───────────────────────────────────────────────────

    /// <summary>
    /// PlayerBlinkController2D.OnBlinkExecuted로 갱신되는 마지막 블링크 착지점.
    /// BrotherCounterState가 순간이동 목표로 사용한다.
    /// </summary>
    public Vector2 LastPlayerBlinkPos { get; private set; }

    public float CounterDamage => _counterDamage;

    private PlayerBlinkController2D _blinkCtrl;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    protected override void Start()
    {
        base.Start();

        _blinkCtrl = Object.FindFirstObjectByType<PlayerBlinkController2D>();
        if (_blinkCtrl != null)
            _blinkCtrl.OnBlinkExecuted += OnPlayerBlinked;
        else
            Debug.LogWarning("[EnemyBossBrother] PlayerBlinkController2D를 찾을 수 없습니다. CounterState 착지점 추적 불가.");
    }

    private void OnDestroy()
    {
        if (_blinkCtrl != null)
            _blinkCtrl.OnBlinkExecuted -= OnPlayerBlinked;
    }

    // ─── BossStateMachine 구현 ────────────────────────────────────────────────

    protected override void InitializeStates()
    {
        BrotherSlash    = new BrotherSlashState(this);
        BrotherBind     = new BrotherBindState(this);
        BrotherWireWhip = new BrotherWireWhipState(this);
        BrotherCounter  = new BrotherCounterState(this);
    }

    public override IBossState GetFirstAttackState() => BrotherSlash;

    protected override void OnPhaseChanged(BossPhase newPhase)
    {
        switch (newPhase)
        {
            case BossPhase.Phase2:
                // Phase 2: WireWhip 추가, CounterStrike 위협
                ChangeState(BrotherWireWhip);
                break;

            case BossPhase.Phase3:
                // Phase 3: 갑옷 재충전 + 구속 창 단축
                Health?.ResetArmor();
                ChangeState(BrotherBind);
                break;
        }
    }

    // ─── 구속 와이어 발사 API ─────────────────────────────────────────────────

    /// <summary>BrotherBindState에서 호출. 현재 Phase에 맞는 구속 지속 시간으로 발사.</summary>
    public void FireBindProjectile(Vector2 direction)
    {
        if (_bindProjectilePrefab == null)
        {
            Debug.LogWarning("[EnemyBossBrother] BindProjectilePrefab이 연결되지 않았습니다.");
            return;
        }
        if (_firePoint == null)
        {
            Debug.LogWarning("[EnemyBossBrother] FirePoint가 연결되지 않았습니다.");
            return;
        }

        float duration = CurrentPhase switch
        {
            BossPhase.Phase3 => _bindDurationPhase3,
            BossPhase.Phase2 => _bindDurationPhase2,
            _                => _bindDurationPhase1,
        };

        var go = Object.Instantiate(_bindProjectilePrefab, _firePoint.position, Quaternion.identity);
        go.GetComponent<BrotherBindProjectile2D>()?.Launch(
            direction,
            _bindProjectileSpeed,
            duration,
            _bindProjectileLifetime
        );
        Destroy(go, _bindProjectileLifetime + 0.5f);
    }

    /// <summary>와이어 채찍 발사 (Parryable). BrotherWireWhipState에서 호출.</summary>
    public void FireWireWhip(Vector2 direction)
    {
        if (_data == null || _data.parryableProjectilePrefab == null) return;
        if (_firePoint == null) return;

        var go = Object.Instantiate(_data.parryableProjectilePrefab, _firePoint.position, Quaternion.identity);
        go.GetComponent<BossParryableProjectile2D>()?.Launch(
            direction,
            _whipSpeed,
            _whipDamage,
            _whipLifetime,
            isParryable: true
        );
        Destroy(go, _whipLifetime + 0.5f);
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────

    private void OnPlayerBlinked(Vector2 _, Vector2 blinkPos)
    {
        LastPlayerBlinkPos = blinkPos;
    }

    public float SlashRange    => _slashRange;
    public float SlashDamage   => _slashDamage;
    public float SlashSpeed    => _slashSpeed;
    public float DashCloseRange => _dashCloseRange;
    public float WhipLifetime  => _whipLifetime;
}
