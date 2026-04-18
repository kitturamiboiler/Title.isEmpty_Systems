using UnityEngine;

/// <summary>
/// 목표와의 거리를 유지하며 X-Y 평면에서 따라간다(점프·공중 포함). 가까우면 추적 속도만 줄이고 수직 속도는 유지한다.
/// 너무 멀어지면 현(목표) 등 뒤로 고무줄 워프한다.
/// </summary>
public class NPCFollow2D : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("비우면 같은 GameObject에서 Rigidbody2D를 찾는다.")]
    [SerializeField] private Rigidbody2D _rb;

    [Header("Move")]
    [Tooltip("speedReference 없을 때 사용하는 속도.")]
    [SerializeField] private float _moveSpeed = 4f;
    [Tooltip("비우면 위 _moveSpeed만 사용. 있으면 max 속도 = 플레이어 ConfiguredMoveSpeed × 배율.")]
    [SerializeField] private PlayerMovement2D _speedReference;
    [SerializeField] private float _maxSpeedMultiplierVsPlayer = 1.2f;
    [Tooltip("이 거리 안이면 추적 가속만 끈다(속도 0은 수평·추적 성분만). StartFollowing(followDistance)로 갱신.")]
    [SerializeField] private float _stoppingDistance = 1.5f;

    [Header("Rubber band")]
    [Tooltip("목표와의 거리가 이 값을 넘으면 월드 위치를 강제로 스냅한다.")]
    [SerializeField] private float _warpDistance = 10f;
    [Tooltip("워프 시 목표의 바라보는 방향 반대편으로 떨어진 X 오프셋(월드).")]
    [SerializeField] private float _warpBehindOffset = 1.5f;

    private Transform _target;
    private bool _active;
    private PlayerMovement2D _speedRefRuntime;

    private void Awake()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 추적을 시작한다. 목표와의 거리가 stopping 거리 이하면 추적을 멈춘다.
    /// </summary>
    /// <param name="target">따를 대상.</param>
    /// <param name="followDistance">이 거리 안으로 들어오면 추적 정지.</param>
    /// <param name="speedReference">한 번만 넘기면 런타임 상한 속도에 사용(인스펙터 할당보다 우선).</param>
    public void StartFollowing(Transform target, float followDistance, PlayerMovement2D speedReference = null)
    {
        if (target == null)
        {
            Debug.LogError($"[NPCFollow2D] StartFollowing: target is null — {gameObject.name}");
            return;
        }

        _target = target;
        _stoppingDistance = Mathf.Max(0.05f, followDistance);
        _speedRefRuntime = speedReference != null ? speedReference : _speedReference;
        _active = true;
    }

    private float EffectiveFollowSpeed
    {
        get
        {
            if (_speedRefRuntime != null && _maxSpeedMultiplierVsPlayer > 0f)
                return Mathf.Max(0.01f, _speedRefRuntime.ConfiguredMoveSpeed * _maxSpeedMultiplierVsPlayer);
            return Mathf.Max(0.01f, _moveSpeed);
        }
    }

    /// <summary>추적을 끊고 속도를 0으로 만든다(Dynamic인 경우).</summary>
    public void StopFollowing()
    {
        _active = false;
        _target = null;
        if (_rb != null && _rb.bodyType == RigidbodyType2D.Dynamic)
            _rb.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (!_active || _target == null)
            return;

        if (_rb == null)
        {
            FollowWithoutRigidbody();
            TryRubberBandWarpTransformOnly();
            return;
        }

        Vector2 self = _rb.position;
        Vector2 tpos = (Vector2)_target.position;
        float distance = Vector2.Distance(self, tpos);

        if (distance <= _stoppingDistance)
        {
            if (_rb.bodyType == RigidbodyType2D.Dynamic)
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        float speed = EffectiveFollowSpeed;
        Vector2 delta = tpos - self;
        float dist = delta.magnitude;
        Vector2 dir = dist > 1e-5f ? delta / dist : Vector2.right;

        if (_rb.bodyType == RigidbodyType2D.Dynamic)
        {
            _rb.linearVelocity = dir * speed;
            ApplySpriteFlip(Mathf.Sign(dir.x));
        }
        else
        {
            Vector2 step = dir * (speed * Time.fixedDeltaTime);
            Vector2 next = self + step;
            if (Vector2.Distance(next, tpos) < _stoppingDistance)
            {
                Vector2 fromTarget = next - tpos;
                if (fromTarget.sqrMagnitude > 1e-8f)
                    next = tpos + fromTarget.normalized * _stoppingDistance;
            }
            _rb.MovePosition(next);
            ApplySpriteFlip(Mathf.Sign(dir.x));
        }

        TryRubberBandWarp();
    }

    private void FollowWithoutRigidbody()
    {
        Vector3 self3 = transform.position;
        Vector2 self = (Vector2)self3;
        Vector2 tpos = (Vector2)_target.position;
        float distance = Vector2.Distance(self, tpos);
        if (distance <= _stoppingDistance)
            return;

        float speed = EffectiveFollowSpeed;
        Vector2 next = Vector2.MoveTowards(self, tpos, speed * Time.fixedDeltaTime);
        if (Vector2.Distance(next, tpos) < _stoppingDistance)
        {
            Vector2 d = next - tpos;
            if (d.sqrMagnitude > 1e-8f)
                next = tpos + d.normalized * _stoppingDistance;
        }
        transform.position = new Vector3(next.x, next.y, self3.z);
        float sx = Mathf.Sign(tpos.x - self.x);
        if (sx != 0f)
            ApplySpriteFlip(sx);
    }

    private void TryRubberBandWarpTransformOnly()
    {
        if (_warpDistance <= 0f || _target == null)
            return;

        Vector2 self = (Vector2)transform.position;
        Vector2 tpos = (Vector2)_target.position;
        if (Vector2.Distance(self, tpos) <= _warpDistance)
            return;

        Vector2 warp = ComputeWarpPosition(tpos);
        transform.position = new Vector3(warp.x, warp.y, transform.position.z);
    }

    private void TryRubberBandWarp()
    {
        if (_warpDistance <= 0f || _target == null || _rb == null)
            return;

        Vector2 self = _rb.position;
        Vector2 tpos = (Vector2)_target.position;
        if (Vector2.Distance(self, tpos) <= _warpDistance)
            return;

        Vector2 warp = ComputeWarpPosition(tpos);

        _rb.position = warp;
        if (_rb.bodyType == RigidbodyType2D.Dynamic)
            _rb.linearVelocity = Vector2.zero;
    }

    private Vector2 ComputeWarpPosition(Vector2 tpos)
    {
        float face = Mathf.Sign(_target.localScale.x);
        if (Mathf.Abs(face) < 0.01f)
            face = 1f;
        float wx = tpos.x - face * _warpBehindOffset;
        return new Vector2(wx, tpos.y);
    }

    private void ApplySpriteFlip(float directionSign)
    {
        if (directionSign == 0f)
            return;
        float sx = Mathf.Abs(transform.localScale.x);
        if (sx < 1e-4f)
            sx = 1f;
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(sx * Mathf.Sign(directionSign), s.y, s.z);
    }
}
