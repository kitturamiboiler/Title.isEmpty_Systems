using UnityEngine;

/// <summary>
/// 그랩 상태에서 이어지는 급강하·착지 충격 처리 상태.
///
/// 방어 설계 요약:
///   - 가설 1 (Tunneling)  : castDist = |velocity.y| × fixedDeltaTime + margin 으로 동적 계산.
///   - 가설 2 (Stale Ref)  : GrabState.ExitForSlam()에서 소유권 이전 후 Enter에서 재잠금.
///   - 가설 3 (중복 발화)  : _hasImpacted 플래그로 ExecuteImpact 1회 실행 보장.
/// </summary>
public class SlamState : IState2D
{
    /// <summary>BoxCast 슬라이스 두께. 얇을수록 오탐(벽 모서리 등) 감소.</summary>
    private const float CAST_BOX_HEIGHT = 0.05f;

    private readonly PlayerStateMachine _machine;
    private readonly Rigidbody2D _rb;
    private readonly Collider2D _playerCollider;
    private readonly WeaponData _weaponData;
    private readonly LayerMask _groundMask;
    private readonly PlayerBlinkController2D _blinkController;
    private readonly IdleState _idleState;

    private EnemyHealth _target;
    private Rigidbody2D _targetRb;
    private bool _hasImpacted;

    private static readonly Collider2D[] _overlapBuffer = new Collider2D[16];

    /// <summary>
    /// </summary>
    /// <param name="machine">FSM 허브.</param>
    /// <param name="rb">플레이어 Rigidbody2D.</param>
    /// <param name="playerCollider">발바닥 좌표 계산용 플레이어 Collider2D.</param>
    /// <param name="weaponData">슬램 수치 공급원.</param>
    /// <param name="groundMask">착지 감지 레이어. PlayerMovement2D.groundMask와 동일 값 사용.</param>
    /// <param name="blinkController">착지 후 공중 판정 동기화용.</param>
    /// <param name="idleState">착지 완료 후 복귀 대상.</param>
    public SlamState(
        PlayerStateMachine machine,
        Rigidbody2D rb,
        Collider2D playerCollider,
        WeaponData weaponData,
        LayerMask groundMask,
        PlayerBlinkController2D blinkController,
        IdleState idleState)
    {
        _machine         = machine;
        _rb              = rb;
        _playerCollider  = playerCollider;
        _weaponData      = weaponData;
        _groundMask      = groundMask;
        _blinkController = blinkController;
        _idleState       = idleState;
    }

    /// <summary>GrabState.ExitForSlam()에서 소유권 이전 시 호출.</summary>
    public void SetTarget(EnemyHealth target)
    {
        _target = target;
    }

    // -------------------------------------------------------------------------
    // IState2D
    // -------------------------------------------------------------------------

    public void Enter()
    {
        // null 가드보다 앞에 리셋 — Enter 조기 리턴 시에도 FixedTick의 중복 발화 방지
        _hasImpacted = false;

        if (_rb == null)
        {
            Debug.LogError("[SlamState] Rigidbody2D가 null입니다.");
            return;
        }
        if (_weaponData == null)
        {
            Debug.LogError("[SlamState] WeaponData가 null입니다.");
            return;
        }
        if (_target == null)
        {
            Debug.LogError("[SlamState] Enter 전 SetTarget() 호출이 필요합니다.");
            return;
        }

        // 타겟 Rigidbody2D 캐싱 + Kinematic 전환
        // → 물리 엔진이 타겟 위치를 덮어쓰는 SE-3 방어
        _targetRb = _target.GetComponent<Rigidbody2D>();
        if (_targetRb != null)
            _targetRb.bodyType = RigidbodyType2D.Kinematic;

        // GrabState.ExitForSlam()이 ReleaseGrab(false) 후 소유권을 넘겼으므로 재잠금
        _target.LockForGrab();

        // 수평 속도 0 고정 + 수직 급강하
        // → PlayerMovement2D.FixedUpdate 가드가 이 값을 덮어쓰지 않도록 SE-1 방어
        _rb.linearVelocity = new Vector2(0f, _weaponData.slamVerticalVelocity);
    }

    public void Tick() { }

    public void FixedTick()
    {
        // 가설 3 방어
        if (_hasImpacted) return;

        // _rb null이면 물리 처리 불가 — 크래시 방지 후 복귀
        if (_rb == null)
        {
            Debug.LogError("[SlamState] FixedTick: Rigidbody2D가 null입니다. IdleState로 복귀.");
            _machine.ChangeState(_idleState);
            return;
        }

        // 타겟 유실 안전망
        if (_target == null)
        {
            Debug.LogWarning("[SlamState] FixedTick 중 타겟이 null입니다. IdleState로 복귀.");
            _machine.ChangeState(_idleState);
            return;
        }

        // 타겟을 플레이어 발밑에 Kinematic MovePosition으로 고정
        if (_playerCollider != null && _targetRb != null)
        {
            Vector2 feetPos = new Vector2(
                _rb.position.x,
                _playerCollider.bounds.min.y
            );
            _targetRb.MovePosition(feetPos);
        }

        if (_playerCollider == null || _weaponData == null) return;

        // 가설 1 방어: 다음 프레임 예상 이동거리 + 여유값으로 동적 거리 계산
        float castDist = Mathf.Abs(_rb.linearVelocity.y) * Time.fixedDeltaTime
                         + _weaponData.slamCastMargin;

        // 발바닥 하단 중점을 Origin으로 설정
        Vector2 castOrigin = new Vector2(
            _playerCollider.bounds.center.x,
            _playerCollider.bounds.min.y
        );
        // 가로 0.9배: 벽 모서리 오탐 방지
        Vector2 castSize = new Vector2(
            _playerCollider.bounds.size.x * 0.9f,
            CAST_BOX_HEIGHT
        );

        RaycastHit2D groundHit = Physics2D.BoxCast(
            castOrigin,
            castSize,
            0f,
            Vector2.down,
            castDist,
            _groundMask
        );

        if (groundHit.collider != null)
            ExecuteImpact(groundHit.point);
    }

    public void Exit()
    {
        // 비정상 탈출(외부 강제 ChangeState) 시 리소스 안전망
        if (_target != null)
        {
            if (_targetRb != null)
                _targetRb.bodyType = RigidbodyType2D.Dynamic;

            _target.ReleaseGrab(executePendingDeath: true);
            _target    = null;
            _targetRb  = null;
        }

        _hasImpacted = false;
    }

    // -------------------------------------------------------------------------
    // 충격 처리
    // -------------------------------------------------------------------------

    private void ExecuteImpact(Vector2 impactPoint)
    {
        // 가설 3 방어: 중복 발화 원천 차단
        _hasImpacted = true;

        // 착지 순간 수직 속도 0 (추가 파고듦 방지)
        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        ApplyAreaDamage(impactPoint);
        ExecuteTargetDeath();

        // SE-2 방어: 착지 판정 즉시 동기화 → 공중 1프레임 방지
        _blinkController?.SyncAirBlinkAfterFloorLanding();

        _machine.ChangeState(_idleState);
    }

    private void ApplyAreaDamage(Vector2 center)
    {
        if (_weaponData == null || _weaponData.slamEnemyLayerMask.value == 0) return;

        int count = Physics2D.OverlapCircleNonAlloc(
            center,
            _weaponData.slamRadius,
            _overlapBuffer,
            _weaponData.slamEnemyLayerMask
        );

        for (int i = 0; i < count; i++)
        {
            if (_overlapBuffer[i] == null) continue;

            // 소유 타겟은 ExecuteTargetDeath()에서 별도 처리
            var hitEnemy = _overlapBuffer[i].GetComponentInParent<EnemyHealth>();
            if (hitEnemy != null && hitEnemy == _target) continue;

            var health = _overlapBuffer[i].GetComponentInParent<IHealth>();
            health?.TakeDamage(_weaponData.slamDamage);
        }
    }

    private void ExecuteTargetDeath()
    {
        if (_target == null) return;

        // Kinematic → Dynamic 복구 (파괴 전 물리 정상화)
        if (_targetRb != null)
            _targetRb.bodyType = RigidbodyType2D.Dynamic;

        // 잠금 해제 후 명시적 데미지 → Lives 0 → Die()
        _target.ReleaseGrab(executePendingDeath: false);
        if (_target.CurrentLives > 0)
            _target.TakeDamage(_weaponData.slamDamage);

        _target   = null;
        _targetRb = null;
    }
}
