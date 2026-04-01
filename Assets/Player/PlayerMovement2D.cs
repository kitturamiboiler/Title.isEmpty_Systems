using UnityEngine;

/// <summary>
/// PlayerBlinkController2D / MovementData와 맞춘 2D 이동.
/// 상태: Grounded / Air / WallClimb — FixedUpdate에서 분리 처리.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    private enum MovementPhase
    {
        Grounded,
        Air,
        WallClimb
    }

    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private PlayerBlinkController2D blinkController;
    [SerializeField] private MovementData _movementData;
    [Tooltip("SlamState 진행 중 수평 이동 간섭 차단용. 같은 GameObject에 부착된 PlayerStateMachine.")]
    [SerializeField] private PlayerStateMachine _stateMachine;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float inputDeadZone = 0.01f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 12f;
    [Tooltip("바닥만 코요테에 포함 (벽 제외). MovementData가 있으면 그 coyoteTime 사용.")]
    [SerializeField] private float coyoteTimeFallback = 0.1f;
    [Tooltip("MovementData가 있으면 그 inputBufferTime 사용.")]
    [SerializeField] private float inputBufferTimeFallback = 0.1f;

    [Header("Ground Check (floor only)")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 groundBoxSize = new Vector2(0.45f, 0.08f);
    [Tooltip("MovementData 없을 때 BoxCast 거리.")]
    [SerializeField] private float groundCastDistanceFallback = 0.28f;
    [Tooltip("MovementData 없을 때 바닥 법선 최소 Y.")]
    [SerializeField] private float minFloorNormalYFallback = 0.35f;
    [Tooltip("MovementData 없을 때 발 모서리 보조 레이 안쪽 여백.")]
    [SerializeField] private float groundFootCornerInsetFallback = 0.06f;
    [Tooltip("MovementData 없을 때 캐스트 시작 상승.")]
    [SerializeField] private float groundCheckVerticalLiftFallback = 0.02f;

    [Header("Wall Climb")]
    [SerializeField] private LayerMask wallMask;
    [Tooltip("벽 방향으로의 레이 길이(몸 가장자리에서).")]
    [SerializeField] private float wallRayLength = 0.12f;
    [SerializeField] private float wallClimbSpeed = 3f;
    [Tooltip("발/몸통/머리 높이에서 벽 연속 여부 판정 시 몸 경계에서 안쪽으로 당긴 거리.")]
    [SerializeField] private float wallRayVerticalInset = 0.05f;

    [Header("Wall Jump (fallback if MovementData null)")]
    [SerializeField] private float wallJumpHorizontalFallback = 7f;
    [SerializeField] private float wallJumpVerticalFallback = 12f;
    [SerializeField] private float wallJumpInputLockFallback = 0.15f;

    [Header("Flip (localScale.x, 단검 플립과 동일 규칙)")]
    [SerializeField] private float flipSuppressAfterAttackSeconds = 0.08f;

    private float _defaultGravityScale = 1f;
    private float _lastFloorGroundedTime = -999f;
    private bool _wasFloorGrounded;
    private float _jumpInputBufferedUntil = -1f;

    private MovementPhase _currentPhase = MovementPhase.Air;
    private int _activeWallSide;
    private bool _wasWallClimbing;

    /// <summary>벽 점프 직후, 해당 벽 방향(왼쪽 벽 = -1)으로의 이동 입력 무시.</summary>
    private float _wallJumpInputLockUntil = -1f;
    private int _wallJumpLockWallSide;

    private float CoyoteDuration =>
        _movementData != null ? _movementData.coyoteTime : coyoteTimeFallback;

    private float InputBufferDuration =>
        _movementData != null ? _movementData.inputBufferTime : inputBufferTimeFallback;

    private float WallJumpHorizontal =>
        _movementData != null ? _movementData.wallJumpHorizontalForce : wallJumpHorizontalFallback;

    private float WallJumpVertical =>
        _movementData != null ? _movementData.wallJumpVerticalForce : wallJumpVerticalFallback;

    private float WallJumpInputLockDuration =>
        _movementData != null ? _movementData.wallJumpInputLockTime : wallJumpInputLockFallback;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
        if (blinkController == null)
            blinkController = GetComponent<PlayerBlinkController2D>();
        if (_stateMachine == null)
            _stateMachine = GetComponent<PlayerStateMachine>();

        _defaultGravityScale = rb.gravityScale;
    }

    /// <summary>블링크/투척 로직에서 벽 타기는 지상·코요테와 동일하게 '벽 접촉'으로 취급.</summary>
    public bool IsWallClimbing => _currentPhase == MovementPhase.WallClimb;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _jumpInputBufferedUntil = Time.time + InputBufferDuration;
    }

    private void FixedUpdate()
    {
        if (blinkController != null && blinkController.IsHitStopBlockingMovement)
        {
            if (_wasWallClimbing)
                RestoreGravityScale();
            _wasWallClimbing = false;
            _currentPhase = MovementPhase.Air;
            return;
        }

        // SlamState 진행 중 수평 이동·중력 간섭 차단 (SE-1 방어)
        // TODO(작성자): IMovementBlockingState 인터페이스 도입으로 SlamState 직접 참조 제거 권장 — 날짜
        if (_stateMachine != null && _stateMachine.CurrentState is SlamState)
        {
            if (_wasWallClimbing)
                RestoreGravityScale();
            _wasWallClimbing = false;
            _currentPhase = MovementPhase.Air;
            return;
        }

        bool floorGrounded = EvaluateFloorGrounded(out _);
        if (floorGrounded)
            _lastFloorGroundedTime = Time.time;

        float hRaw = Input.GetAxisRaw("Horizontal");
        float hForWall = ApplyWallJumpInputLock(hRaw);
        WallProbe wallProbe = BuildWallProbe(hForWall);

        MovementPhase phase = ResolvePhase(floorGrounded, wallProbe);

        _currentPhase = phase;
        if (phase == MovementPhase.WallClimb)
            _activeWallSide = wallProbe.ClimbWallSide;
        else
            _activeWallSide = 0;

        switch (phase)
        {
            case MovementPhase.Grounded:
                TickGrounded(floorGrounded, hRaw);
                break;
            case MovementPhase.WallClimb:
                TickWallClimb(wallProbe);
                break;
            case MovementPhase.Air:
                TickAir(floorGrounded, hRaw);
                break;
        }

        if (floorGrounded && !_wasFloorGrounded && blinkController != null)
            blinkController.SyncAirBlinkAfterFloorLanding();

        bool nowWallClimb = _currentPhase == MovementPhase.WallClimb;
        if (nowWallClimb && !_wasWallClimbing && blinkController != null)
            blinkController.SyncAirBlinkAfterWallAttach();

        _wasFloorGrounded = floorGrounded;
        _wasWallClimbing = nowWallClimb;
    }

    private void LateUpdate()
    {
        if (blinkController != null && blinkController.IsHitStopBlockingMovement)
            return;

        // Slam 중 스프라이트 플립 억제
        if (_stateMachine != null && _stateMachine.CurrentState is SlamState)
            return;

        if (Time.time - (blinkController != null ? blinkController.LastAttackFlipGameTime : -999f)
            < flipSuppressAfterAttackSeconds)
            return;

        if (_currentPhase == MovementPhase.WallClimb && _activeWallSide != 0)
        {
            Vector3 scale = transform.localScale;
            float sign = _activeWallSide < 0f ? -1f : 1f;
            scale.x = Mathf.Abs(scale.x) * sign;
            transform.localScale = scale;
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(h) < inputDeadZone)
            return;

        Vector3 s = transform.localScale;
        float faceSign = h >= 0f ? 1f : -1f;
        s.x = Mathf.Abs(s.x) * faceSign;
        transform.localScale = s;
    }

    private MovementPhase ResolvePhase(bool floorGrounded, WallProbe wallProbe)
    {
        if (floorGrounded)
            return MovementPhase.Grounded;

        if (wallProbe.CanClimb)
            return MovementPhase.WallClimb;

        return MovementPhase.Air;
    }

    private void TickGrounded(bool floorGrounded, float hRaw)
    {
        if (_wasWallClimbing)
            RestoreGravityScale();

        float h = ApplyWallJumpInputLock(hRaw);
        ApplyHorizontalMove(h);

        bool coyoteOk = (Time.time - _lastFloorGroundedTime) <= CoyoteDuration;
        bool canJump = floorGrounded || coyoteOk;
        TryNormalJump(canJump);
    }

    private void TickAir(bool floorGrounded, float hRaw)
    {
        if (_wasWallClimbing)
            RestoreGravityScale();

        float h = ApplyWallJumpInputLock(hRaw);
        ApplyHorizontalMove(h);

        bool coyoteOk = (Time.time - _lastFloorGroundedTime) <= CoyoteDuration;
        bool canJump = floorGrounded || coyoteOk;
        TryNormalJump(canJump);
    }

    private void TickWallClimb(WallProbe wallProbe)
    {
        int side = wallProbe.ClimbWallSide;

        SetClimbGravity();

        if (TryWallJump(side))
            return;

        float vy = 0f;
        if (Input.GetKey(KeyCode.W))
            vy = wallClimbSpeed;
        else if (Input.GetKey(KeyCode.S))
            vy = -wallClimbSpeed;

        Vector2 v = rb.linearVelocity;
        v.x = 0f;
        v.y = vy;
        rb.linearVelocity = v;
    }

    private bool TryWallJump(int wallSide)
    {
        bool localBuffered = _jumpInputBufferedUntil > Time.time;
        bool blinkBuffered = blinkController != null && blinkController.HasBufferedJumpInput();
        if (!localBuffered && !blinkBuffered)
            return false;

        _jumpInputBufferedUntil = -1f;
        if (blinkController != null && blinkController.HasBufferedJumpInput())
            blinkController.ConsumeBufferedJumpInput();

        float away = wallSide < 0 ? 1f : -1f;
        rb.linearVelocity = new Vector2(away * WallJumpHorizontal, WallJumpVertical);

        _wallJumpInputLockUntil = Time.time + WallJumpInputLockDuration;
        _wallJumpLockWallSide = wallSide;

        RestoreGravityScale();
        _currentPhase = MovementPhase.Air;
        _activeWallSide = 0;
        return true;
    }

    private void TryNormalJump(bool canJump)
    {
        bool localBuffered = _jumpInputBufferedUntil > Time.time;
        bool blinkBuffered = blinkController != null && blinkController.HasBufferedJumpInput();
        if (!localBuffered && !blinkBuffered)
            return;

        if (!canJump)
            return;

        _jumpInputBufferedUntil = -1f;
        if (blinkController != null && blinkController.HasBufferedJumpInput())
            blinkController.ConsumeBufferedJumpInput();

        Vector2 v = rb.linearVelocity;
        v.y = jumpVelocity;
        rb.linearVelocity = v;
    }

    private float ApplyWallJumpInputLock(float hRaw)
    {
        if (Time.time >= _wallJumpInputLockUntil)
            return hRaw;

        if (_wallJumpLockWallSide < 0 && hRaw < 0f)
            return 0f;
        if (_wallJumpLockWallSide > 0 && hRaw > 0f)
            return 0f;

        return hRaw;
    }

    private void ApplyHorizontalMove(float h)
    {
        if (Mathf.Abs(h) < inputDeadZone)
            h = 0f;

        Vector2 v = rb.linearVelocity;
        v.x = h * moveSpeed;
        rb.linearVelocity = v;
    }

    private void RestoreGravityScale()
    {
        rb.gravityScale = _defaultGravityScale;
    }

    private void SetClimbGravity()
    {
        rb.gravityScale = 0f;
    }

    private WallProbe BuildWallProbe(float horizontalForClimb)
    {
        var p = new WallProbe();

        if (bodyCollider == null)
            return p;

        Bounds b = bodyCollider.bounds;
        float leftX = b.min.x;
        float rightX = b.max.x;
        float footY = b.min.y + wallRayVerticalInset;
        float headY = b.max.y - wallRayVerticalInset;
        float midY = b.center.y;

        p.LeftFoot = WallRayHit(new Vector2(leftX, footY), Vector2.left);
        p.LeftMid = WallRayHit(new Vector2(leftX, midY), Vector2.left);
        p.LeftHead = WallRayHit(new Vector2(leftX, headY), Vector2.left);

        p.RightFoot = WallRayHit(new Vector2(rightX, footY), Vector2.right);
        p.RightMid = WallRayHit(new Vector2(rightX, midY), Vector2.right);
        p.RightHead = WallRayHit(new Vector2(rightX, headY), Vector2.right);

        bool towardLeft = horizontalForClimb < -inputDeadZone;
        bool towardRight = horizontalForClimb > inputDeadZone;

        bool segmentLeft = p.LeftFoot && p.LeftMid && p.LeftHead;
        bool segmentRight = p.RightFoot && p.RightMid && p.RightHead;

        if (segmentLeft && towardLeft)
        {
            p.ClimbWallSide = -1;
            p.CanClimb = true;
        }
        else if (segmentRight && towardRight)
        {
            p.ClimbWallSide = 1;
            p.CanClimb = true;
        }

        return p;
    }

    private bool WallRayHit(Vector2 origin, Vector2 dir)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, wallRayLength, wallMask);
        return hit.collider != null;
    }

    private bool EvaluateFloorGrounded(out RaycastHit2D bestHit)
    {
        bestHit = default;
        if (groundMask.value == 0)
            return false;

        float castDist  = _movementData != null ? _movementData.groundBoxCastDistance   : groundCastDistanceFallback;
        float minNy     = _movementData != null ? _movementData.groundMinFloorNormalY    : minFloorNormalYFallback;
        float cornerInset = _movementData != null ? _movementData.groundFootCornerInset  : groundFootCornerInsetFallback;
        float lift      = _movementData != null ? _movementData.groundCheckVerticalLift  : groundCheckVerticalLiftFallback;

        Vector2 boxOrigin;
        if (bodyCollider != null)
        {
            Bounds b = bodyCollider.bounds;
            boxOrigin = new Vector2(b.center.x, b.min.y) + groundCheckOffset + Vector2.up * lift;
        }
        else
            boxOrigin = (Vector2)transform.position + groundCheckOffset + Vector2.up * lift;

        RaycastHit2D hit = Physics2D.BoxCast(
            boxOrigin,
            groundBoxSize,
            0f,
            Vector2.down,
            castDist,
            groundMask,
            -Mathf.Infinity,
            Mathf.Infinity);

        if (hit.collider != null && hit.normal.y >= minNy)
        {
            bestHit = hit;
            return true;
        }

        if (bodyCollider != null)
        {
            Bounds b = bodyCollider.bounds;
            float footY = b.min.y + lift * 0.5f;
            Vector2 left  = new Vector2(b.min.x + cornerInset, footY) + (Vector2)groundCheckOffset;
            Vector2 right = new Vector2(b.max.x - cornerInset, footY) + (Vector2)groundCheckOffset;
            float rayLen  = castDist + Mathf.Abs(lift) + groundBoxSize.y * 0.5f;

            hit = Physics2D.Raycast(left, Vector2.down, rayLen, groundMask);
            if (hit.collider != null && hit.normal.y >= minNy)
            {
                bestHit = hit;
                return true;
            }

            hit = Physics2D.Raycast(right, Vector2.down, rayLen, groundMask);
            if (hit.collider != null && hit.normal.y >= minNy)
            {
                bestHit = hit;
                return true;
            }
        }

        return false;
    }

    private struct WallProbe
    {
        public bool LeftFoot, LeftMid, LeftHead;
        public bool RightFoot, RightMid, RightHead;
        public bool CanClimb;
        public int ClimbWallSide;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float castDist = _movementData != null ? _movementData.groundBoxCastDistance   : groundCastDistanceFallback;
        float lift     = _movementData != null ? _movementData.groundCheckVerticalLift  : groundCheckVerticalLiftFallback;
        Vector2 origin = bodyCollider != null
            ? new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y) + groundCheckOffset + Vector2.up * lift
            : (Vector2)transform.position + groundCheckOffset + Vector2.up * lift;
        Gizmos.color = Color.green;
        Vector2 a  = origin + new Vector2(-groundBoxSize.x * 0.5f, -groundBoxSize.y * 0.5f);
        Vector2 br = origin + new Vector2( groundBoxSize.x * 0.5f, -groundBoxSize.y * 0.5f);
        Vector2 c  = origin + new Vector2( groundBoxSize.x * 0.5f,  groundBoxSize.y * 0.5f);
        Vector2 d  = origin + new Vector2(-groundBoxSize.x * 0.5f,  groundBoxSize.y * 0.5f);
        Gizmos.DrawLine(a, br);
        Gizmos.DrawLine(br, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        Gizmos.color = Color.yellow;
        Vector2 end = origin + Vector2.down * (castDist + groundBoxSize.y * 0.5f);
        Gizmos.DrawLine(origin, end);

        if (bodyCollider == null)
            return;

        Bounds b = bodyCollider.bounds;
        float footY = b.min.y + wallRayVerticalInset;
        float headY = b.max.y - wallRayVerticalInset;
        float midY  = b.center.y;
        Gizmos.color = Color.cyan;
        DrawWallRayGizmo(new Vector2(b.min.x, footY), Vector2.left);
        DrawWallRayGizmo(new Vector2(b.min.x, midY), Vector2.left);
        DrawWallRayGizmo(new Vector2(b.min.x, headY), Vector2.left);
        DrawWallRayGizmo(new Vector2(b.max.x, footY), Vector2.right);
        DrawWallRayGizmo(new Vector2(b.max.x, midY), Vector2.right);
        DrawWallRayGizmo(new Vector2(b.max.x, headY), Vector2.right);
    }

    private void DrawWallRayGizmo(Vector2 o, Vector2 dir)
    {
        Gizmos.DrawLine(o, o + dir * wallRayLength);
    }
#endif
}
