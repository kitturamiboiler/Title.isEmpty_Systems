using UnityEngine;

/// <summary>
/// 목표 Transform과의 거리를 유지하며 X-Y 평면에서 따라간다.
/// Rigidbody2D가 있으면 MovePosition 또는 Dynamic일 때 linearVelocity를 사용하고, 없으면 transform만 이동한다.
/// 구멍·낭떨어지 구간에서는 직선 추적만으로는 추락/끼임이 나올 수 있으므로 Waypoint·Ghost Path·NavMesh 등 별도 방어가 필요하다.
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

    private Transform _target;
    private float _followDistance;
    private bool _active;
    private PlayerMovement2D _speedRefRuntime;

    private void Awake()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 추적을 시작한다. 거리 이내면 정지한다.
    /// </summary>
    /// <param name="target">따를 대상.</param>
    /// <param name="followDistance">이 거리 안으로 들어오면 멈춤.</param>
    /// <param name="speedReference">한 번만 넘기면 런타임 상한 속도에 사용(인스펙터 할당보다 우선).</param>
    public void StartFollowing(Transform target, float followDistance, PlayerMovement2D speedReference = null)
    {
        if (target == null)
        {
            Debug.LogError($"[NPCFollow2D] StartFollowing: target is null — {gameObject.name}");
            return;
        }

        _target = target;
        _followDistance = Mathf.Max(0.05f, followDistance);
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

        Vector2 self = transform.position;
        Vector2 t = _target.position;
        Vector2 delta = t - self;
        float dist = delta.magnitude;
        if (dist <= _followDistance)
        {
            if (_rb != null && _rb.bodyType == RigidbodyType2D.Dynamic)
                _rb.linearVelocity = Vector2.zero;
            return;
        }

        float speed = EffectiveFollowSpeed;
        Vector2 dir = delta / dist;
        float step = speed * Time.fixedDeltaTime;
        Vector2 next = self + dir * step;
        float newDist = Vector2.Distance(next, t);
        if (newDist < _followDistance)
            next = t - dir * _followDistance;

        if (_rb != null)
        {
            if (_rb.bodyType == RigidbodyType2D.Dynamic)
                _rb.linearVelocity = dir * speed;
            else
                _rb.MovePosition(next);
        }
        else
        {
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }
    }
}
