using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Elite 0x01의 어깨 관절 — IGrabbable 약점 컴포넌트.
///
/// 그랩 조건: 단검이 이 관절에 박혀야 IsGrabbable = true.
/// 설계자의 DaggerProjectile2D가 이 레이어에 적중하면 SetExposed(true) 호출.
///
/// 가설 1 방어 (물리 엔진 분리 오류):
///   팔 분리 시 Joint2D를 파괴하지 않는다.
///   원본 팔(_armRenderer + 콜라이더)을 enabled = false 처리하여 물리 무결성 유지.
///   별도의 _brokenArmPrefab을 Instantiate + Rigidbody2D.AddForce로 날려 시각적 분리 연출.
///   → 보스 본체 Rigidbody2D는 영향을 전혀 받지 않는다.
/// </summary>
public class ArmJoint : MonoBehaviour, IGrabbable
{
    [Header("Arm Joint — Refs")]
    [Tooltip("원본 팔 SpriteRenderer. 관절 파괴 시 비활성화.")]
    [SerializeField] private SpriteRenderer _armRenderer;
    [Tooltip("가설 1: 분리된 팔 프리팹. 물리 적용 후 날아감. Rigidbody2D 필수.")]
    [SerializeField] private GameObject     _brokenArmPrefab;
    [Tooltip("팔 자체 콜라이더 (본체와 별도). 파괴 시 비활성화.")]
    [SerializeField] private Collider2D     _armCollider;

    [Header("Arm Joint — Settings")]
    [SerializeField] private float _popForce   = 5f;
    [SerializeField] private float _popTorque  = 4f;
    [SerializeField] private float _brokenTtl  = 3f; // 분리된 팔 자동 파괴 시간(초)

    [Header("Events")]
    public UnityEvent OnJointBroken;

    // ─── 상태 ─────────────────────────────────────────────────────────────────

    private bool _isExposed;    // 단검 박힌 상태 (그랩 허용)
    private bool _isBroken;
    private bool _isLocked;     // LockForGrab 중

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    public bool      IsGrabbable  => _isExposed && !_isBroken && !_isLocked;
    public int       CurrentLives => _isBroken ? 0 : 1;
    public float     MaxHealth    => 1f;
    public float     CurrentHealth => _isBroken ? 0f : 1f;
    public new GameObject gameObject => base.gameObject;

    public void TakeDamage(float damage)  { } // 관절은 슬램으로만 파괴

    public void Die()
    {
        if (!_isBroken) BreakJoint();
    }

    public void LockForGrab()  => _isLocked = true;

    public void ReleaseGrab(bool executePendingDeath)
    {
        _isLocked = false;
        if (executePendingDeath && !_isBroken) BreakJoint();
    }

    public void TakeSlamDamage(float damage)
    {
        if (_isBroken) return;
        BreakJoint();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>단검 박힘/이탈 시 EliteZeroX01에서 호출.</summary>
    public void SetExposed(bool exposed)
    {
        if (_isBroken) return;
        _isExposed = exposed;
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 가설 1: 물리 무결성 보장 방식으로 팔 분리 연출.
    /// - 원본 팔: enabled = false (물리 연산 완전 차단)
    /// - 분리된 팔 프리팹: 별도 스폰 → AddForce/Torque → 자동 파괴
    /// </summary>
    private void BreakJoint()
    {
        if (_isBroken) return;
        _isBroken  = true;
        _isExposed = false;

        // 원본 팔 비활성화 — 본체 Rigidbody2D에 영향 없음
        if (_armRenderer != null) _armRenderer.enabled = false;
        if (_armCollider != null) _armCollider.enabled = false;

        // 분리된 팔 프리팹 스폰 — 완전히 독립적인 물리 오브젝트
        if (_brokenArmPrefab != null)
        {
            var broken = Instantiate(_brokenArmPrefab, transform.position, transform.rotation);
            var brokenRb = broken.GetComponent<Rigidbody2D>();
            if (brokenRb != null)
            {
                Vector2 popDir = new Vector2(
                    Random.Range(-0.8f, 0.8f),
                    Random.Range(0.4f, 1.2f)
                ).normalized;
                brokenRb.AddForce(popDir * _popForce, ForceMode2D.Impulse);
                brokenRb.AddTorque(Random.Range(-_popTorque, _popTorque), ForceMode2D.Impulse);
            }
            Destroy(broken, _brokenTtl);
        }

        OnJointBroken?.Invoke();
    }
}
