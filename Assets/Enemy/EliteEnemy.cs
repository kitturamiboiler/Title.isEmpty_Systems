using System.Collections;
using UnityEngine;

/// <summary>
/// 근접 패리 시스템 검증용 엘리트 적.
///
/// 설계 목적:
///   - IParryableMelee + IGrabbable 조합이 "패리 → 블링크 → 그랩 → 슬램"으로 정상 연계되는지
///     보스 FSM 복잡도 없이 샌드박스 환경에서 검증한다.
///   - parryMeleeRadius / stunDuration / parryMeleeLaunchForce 수치 튜닝 전용.
///
/// 공격 사이클:
///   대기(Idle) → 선행 동작 Anticipation (0.2s) → 히트박스 활성(Swing, 0.3s) → 회복(Recovery, 0.4s) → 반복
///   - Anticipation: IsInMeleeAttack = false  (패리해도 무효 — 너무 이르면 상호작용 없음)
///   - Swing:        IsInMeleeAttack = true   (이 구간만 패리 성공 가능)
///   - Recovery:     IsInMeleeAttack = false
///
/// Lives 규칙:
///   Lives 2 → 그랩-슬램 가능
///   Lives 1 → 블링크 도착 즉시 척살 (PlayerBlinkController2D Lives 조건 이미 구현됨)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EliteEnemy : MonoBehaviour, IGrabbable, IParryableMelee
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Stats")]
    [SerializeField] private int   _maxLives        = 2;
    [Tooltip("히트박스 활성 구간 반경 (Gizmo로 시각화됨).")]
    [SerializeField] private float _swingHitRadius  = 1.4f;
    [SerializeField] private LayerMask _playerMask;

    [Header("Attack Timing")]
    [Tooltip("공격 전 선행 동작 시간(초). 이 구간엔 패리 불가.")]
    [SerializeField] private float _anticipationTime = 0.25f;
    [Tooltip("히트박스 활성(Swing) 구간 시간(초). 패리 성공 윈도우.")]
    [SerializeField] private float _swingActiveTime  = 0.30f;
    [Tooltip("공격 후 회복 시간(초).")]
    [SerializeField] private float _recoveryTime     = 0.45f;
    [Tooltip("공격 사이 대기 시간(초).")]
    [SerializeField] private float _idleTime         = 1.2f;

    [Header("Damage")]
    [SerializeField] private float _swingDamage = 1f;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private int  _currentLives;
    private bool _isGrabbable;
    private bool _isLocked;          // GrabState 점유 중 — 외부 데미지 차단
    private bool _pendingDeath;      // 잠금 중 사망 요청 보류
    private bool _isDead;

    private Coroutine _attackCoroutine;
    private Coroutine _stunCoroutine;
    private Rigidbody2D _rb;
    private float _originalGravityScale; // H3: 스턴 복원용 원본 gravityScale

    // H1: 동시 판정 충돌 방어용 — Awake에서 1회 캐싱
    private PlayerParryController2D _playerParry;

    // ─── IParryableMelee ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsInMeleeAttack { get; private set; }

    /// <inheritdoc/>
    public void OnMeleeParried(float stunDuration)
    {
        if (_isDead) return;

        // 공격 루틴 즉시 중단
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        IsInMeleeAttack = false;

        // H2: 발사 벡터 = 위쪽 70% + 플레이어 방향 인력 30%
        // 적이 플레이어 반대편으로 날아가 블링크 사거리를 벗어나는 상황 방어
        if (_rb != null && _playerParry != null)
        {
            Vector2 toPlayer = (_playerParry.transform.position - transform.position).normalized;
            Vector2 launchDir = (Vector2.up * 0.7f + toPlayer * 0.3f).normalized;

            // PlayerParryController2D에서 이미 AddForce를 호출했으므로
            // 여기서는 velocity를 직접 덮어써서 방향을 교정한다
            float speed = _rb.linearVelocity.magnitude;
            if (speed < 0.1f) speed = 9f; // 외부 힘이 없었던 경우 기본값
            _rb.linearVelocity = launchDir * speed;
        }

        // 스턴 루틴: stunDuration 동안 IsGrabbable = true + 체공 처리
        if (_stunCoroutine != null)
            StopCoroutine(_stunCoroutine);
        _stunCoroutine = StartCoroutine(ParryStunRoutine(stunDuration));
    }

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsGrabbable => _isGrabbable && !_isLocked;

    /// <inheritdoc/>
    public int CurrentLives => _currentLives;

    /// <inheritdoc/>
    public void LockForGrab()
    {
        _isLocked = true;

        // 그랩 점유 중에는 공격 루틴 중단
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        IsInMeleeAttack = false;
    }

    /// <inheritdoc/>
    public void ReleaseGrab(bool executePendingDeath)
    {
        _isLocked   = false;
        _isGrabbable = false;

        if (executePendingDeath && _pendingDeath)
            Die();
    }

    /// <inheritdoc/>
    public void TakeSlamDamage(float damage)
    {
        TakeDamage(damage);
    }

    // ─── IHealth ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        if (_isLocked)
        {
            _pendingDeath = true;
            return;
        }

        _currentLives -= Mathf.CeilToInt(amount);
        if (_currentLives <= 0)
            Die();
    }

    /// <inheritdoc/>
    public void Die()
    {
        if (_isDead) return;
        _isDead = true;

        if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
        if (_stunCoroutine   != null) StopCoroutine(_stunCoroutine);

        IsInMeleeAttack = false;
        _isGrabbable    = false;

        // TODO(기획): 사망 이펙트 / 사운드 — 아트 완성 후 EffectManager 연결
        Destroy(gameObject, 0.1f);
    }

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb                   = GetComponent<Rigidbody2D>();
        _currentLives         = _maxLives;
        _originalGravityScale = _rb != null ? _rb.gravityScale : 1f;
    }

    private void Start()
    {
        // H1: Awake에서 씬이 완전히 초기화되지 않을 수 있어 Start에서 1회 탐색
        _playerParry = Object.FindFirstObjectByType<PlayerParryController2D>();

        _attackCoroutine = StartCoroutine(AttackLoop());
    }


    // ─── 공격 루틴 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Idle → Anticipation → Swing(패리 가능 구간) → Recovery → 반복.
    /// Step 2 방어: Anticipation을 두어 선 패리(치팅) 방지.
    /// </summary>
    private IEnumerator AttackLoop()
    {
        while (!_isDead)
        {
            // 대기
            yield return new WaitForSeconds(_idleTime);
            if (_isDead || _isLocked) continue;

            // Anticipation — 선행 동작, 패리 불가 구간
            IsInMeleeAttack = false;
            yield return new WaitForSeconds(_anticipationTime);
            if (_isDead || _isLocked) continue;

            // Swing — 히트박스 활성, 패리 가능 구간
            IsInMeleeAttack = true;
            yield return new WaitForSeconds(_swingActiveTime);
            IsInMeleeAttack = false;
            if (_isDead || _isLocked) continue;

            // 히트 판정
            ApplySwingDamage();

            // Recovery
            yield return new WaitForSeconds(_recoveryTime);
        }
    }

    /// <summary>
    /// Swing 종료 시점에 플레이어가 범위 내이면 데미지.
    /// H1 방어: 패리 판정 창이 열려있으면 데미지 무효화.
    /// </summary>
    private void ApplySwingDamage()
    {
        if (_playerMask.value == 0) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, _swingHitRadius, _playerMask);
        if (hit == null) return;

        // H1: 패리 창이 열려있으면 이 프레임 데미지를 무효화
        // (패리와 데미지가 동일 프레임에 발생해도 플레이어는 맞지 않는다)
        if (_playerParry != null && _playerParry.IsParryWindowActive)
            return;

        var health = hit.GetComponentInParent<IHealth>();
        health?.TakeDamage(_swingDamage);
    }

    // ─── 패리 스턴 ────────────────────────────────────────────────────────────

    private IEnumerator ParryStunRoutine(float duration)
    {
        _isGrabbable = true;

        // H3: 체공 시간(Hang Time) — 스턴 중 중력 제거로 자연스러운 부유 연출
        // 빠른 낙하로 블링크-그랩 연계 타이밍이 망가지는 상황 방어
        if (_rb != null)
        {
            _rb.gravityScale    = 0f;
            _rb.linearVelocity  = Vector2.zero; // 초기 속도 정리
        }

        // 스턴 절반 지점부터 중력을 절반 수준으로 서서히 복원
        float halfTime = duration * 0.5f;
        yield return new WaitForSeconds(halfTime);

        if (!_isDead && !_isLocked && _rb != null)
            _rb.gravityScale = _originalGravityScale * 0.5f;

        yield return new WaitForSeconds(halfTime);

        // H3: 중력 완전 복원
        if (_rb != null)
            _rb.gravityScale = _originalGravityScale;

        if (!_isDead && !_isLocked)
        {
            _isGrabbable = false;
            // 스턴 해제 후 공격 루틴 재개
            _attackCoroutine = StartCoroutine(AttackLoop());
        }

        _stunCoroutine = null;
    }

    // ─── 디버그 시각화 ────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 스윙 히트박스 (활성 중 빨간색, 비활성 회색)
        UnityEditor.Handles.color = IsInMeleeAttack
            ? new Color(1f, 0.1f, 0.1f, 0.4f)
            : new Color(0.6f, 0.6f, 0.6f, 0.2f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.forward, _swingHitRadius);
    }
#endif
}
