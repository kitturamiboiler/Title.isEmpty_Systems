using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어 체력(Lives) 관리.
/// - 피격 시 WeaponData 기반 히트스톱 → Layers.PlayerInvincible 무적 처리.
/// - PlayerBlinkController2D 의 블링크 무적과 독립적으로 동작한다.
///   (양쪽이 동시에 hitStopTimeScale을 건드릴 수 있으므로, _isHitStopping 플래그로 중복 차단.)
/// </summary>
public class PlayerHealth : MonoBehaviour, IHealth
{
    [Header("Refs")]
    [SerializeField] private WeaponData _weaponData;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("Lives")]
    [Tooltip("플레이어 목숨 수. 기본 3.")]
    [SerializeField] private int _maxLives = 3;

    [Header("Events")]
    [Tooltip("피격 시 호출. 남은 Lives를 파라미터로 전달.")]
    public UnityEvent<int> OnDamaged;
    [Tooltip("모든 Lives 소진 시 호출.")]
    public UnityEvent OnDied;

    /// <summary>현재 Lives.</summary>
    public int CurrentLives { get; private set; }

    /// <summary>최대 Lives.</summary>
    public int MaxLives => _maxLives;

    /// <summary>무적 프레임 진행 중 여부. 외부 공격 판정 코드에서 참조 가능.</summary>
    public bool IsInvincible => _isInvincible;

    /// <summary>사망 여부. PlayerBoundState가 타임아웃 데미지 후 Idle 복귀 방지에 사용.</summary>
    public bool IsDead => _isDead;

    private bool _isDead;
    private bool _isInvincible;
    private bool _isHitStopping;

    private int _originalLayer;
    private float _originalAlpha;

    private Coroutine _invincibleCoroutine;

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_maxLives <= 0)
        {
            Debug.LogError($"[PlayerHealth] MaxLives가 0 이하입니다 — {gameObject.name}. 기본값 1로 보정.");
            _maxLives = 1;
        }

        CurrentLives = _maxLives;
        _originalLayer = gameObject.layer;
        _originalAlpha = _spriteRenderer != null ? _spriteRenderer.color.a : 1f;
    }

    // -------------------------------------------------------------------------
    // IHealth
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>양수 데미지 1회 = Lives 1 감소. 무적 중 호출은 무시된다.</remarks>
    public void TakeDamage(float damage)
    {
        if (_isDead || _isInvincible) return;

        if (damage <= 0f)
        {
            Debug.LogWarning($"[PlayerHealth] 비양수 데미지({damage}) 입력 — {gameObject.name}. 무시.");
            return;
        }

        CurrentLives--;
        CurrentLives = Mathf.Max(CurrentLives, 0);
        OnDamaged?.Invoke(CurrentLives);

        // 히트스톱 → 무적 순서 보장 (히트스톱이 먼저 끝나야 무적 타이머가 정확)
        StartHitStop();
        StartInvincibility();

        if (CurrentLives <= 0)
            Die();
    }

    /// <inheritdoc/>
    public void Die()
    {
        if (_isDead) return;

        _isDead = true;
        CleanupCoroutines();
        RestoreState();
        OnDied?.Invoke();

        SoundManager.Instance?.PlayDeath();

        // TODO(작성자): 사망 연출·게임 오버 씬 전환 연결 — 날짜
        gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // 히트스톱
    // -------------------------------------------------------------------------

    private void StartHitStop()
    {
        if (_isHitStopping) return;
        StartCoroutine(PerformHitStop());
    }

    private IEnumerator PerformHitStop()
    {
        _isHitStopping = true;

        float duration  = _weaponData != null ? _weaponData.hitStopDuration  : 0.05f;
        float timeScale = _weaponData != null ? _weaponData.hitStopTimeScale : 0.1f;

        // timeScale 관리를 HitStopManager에 위임 (블링크 히트스톱과 동시 발생 시 병합 처리)
        HitStopManager.Instance?.Request(duration, timeScale);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _isHitStopping = false;
    }

    // -------------------------------------------------------------------------
    // 무적 (I-frame)
    // -------------------------------------------------------------------------

    private void StartInvincibility()
    {
        if (_invincibleCoroutine != null)
            StopCoroutine(_invincibleCoroutine);

        _invincibleCoroutine = StartCoroutine(PerformInvincibility());
    }

    private IEnumerator PerformInvincibility()
    {
        _isInvincible = true;

        int invLayer = Layers.PlayerInvincible;
        if (invLayer == -1)
        {
            Debug.LogWarning("[PlayerHealth] Layers.PlayerInvincible 가 -1. " +
                             "Project Settings > Tags and Layers를 확인하세요.");
            _isInvincible = false;
            yield break;
        }

        // 원본 백업 후 무적 레이어 + 반투명 전환
        _originalLayer = Layers.Player;
        gameObject.layer = invLayer;

        if (_spriteRenderer != null)
        {
            Color c = _spriteRenderer.color;
            c.a = _weaponData != null ? _weaponData.invincibleAlpha : 0.5f;
            _spriteRenderer.color = c;
        }

        // hitStopDuration 만큼 추가로 보장 (히트스톱 끝날 때까지 무적 유지)
        float hitStop  = _weaponData != null ? _weaponData.hitStopDuration  : 0.05f;
        float duration = _weaponData != null ? _weaponData.invincibleDuration + hitStop : 0.2f;
        yield return new WaitForSecondsRealtime(duration);

        RestoreState();
        _isInvincible = false;
        _invincibleCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // 상태 복구 / 정리
    // -------------------------------------------------------------------------

    private void RestoreState()
    {
        gameObject.layer = _originalLayer;

        if (_spriteRenderer != null)
        {
            Color c = _spriteRenderer.color;
            c.a = _originalAlpha;
            _spriteRenderer.color = c;
        }
    }

    private void CleanupCoroutines()
    {
        if (_invincibleCoroutine != null)
        {
            StopCoroutine(_invincibleCoroutine);
            _invincibleCoroutine = null;
        }
    }

    private void OnDisable()
    {
        CleanupCoroutines();
        RestoreState();
    }

    private void OnDestroy()
    {
        RestoreState();
    }
}
