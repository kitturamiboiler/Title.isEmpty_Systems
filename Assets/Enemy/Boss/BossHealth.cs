using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 보스 체력 관리 컴포넌트 (IGrabbable 구현).
///
/// 데미지 흐름:
///   ① TakeDamage(float) — 일반 공격
///      - ArmorGauge > 0 이면 갑옷 차단. 단, 실행 등급 데미지(≥ armorBreakThreshold)는 갑옷 감소.
///      - ArmorGauge == 0 이고 IsVulnerable == true 일 때만 Lives 감소.
///   ② TakeSlamDamage(float) — 슬램/처형 전용
///      - ArmorGauge 감소 후 Lives 무조건 감소 (취약 여부 무시).
///   ③ TakeArmorDamage(float) — 갑옷만 직접 감소 (외부 명시 호출용).
///
/// IGrabbable 흐름:
///   LockForGrab() → OnGrabbed 이벤트 → BossStateMachine.GrabbedState 진입
///   ReleaseGrab() → OnGrabReleased 이벤트 → BossStateMachine이 다음 상태 결정
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossHealth : MonoBehaviour, IGrabbable
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [SerializeField] private BossData _data;

    // ─── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>피격 시 (남은 lives 전달).</summary>
    public UnityEvent<int>   OnDamaged;

    /// <summary>사망 시.</summary>
    public UnityEvent        OnDied;

    /// <summary>갑옷 파괴 시.</summary>
    public UnityEvent        OnArmorBroken;

    /// <summary>갑옷 수치 변화 시 (현재, 최대).</summary>
    public UnityEvent<float, float> OnArmorChanged;

    /// <summary>Phase 전환 시 (새 Phase 전달).</summary>
    public UnityEvent<BossPhase> OnPhaseChanged;

    /// <summary>LockForGrab() 호출 시. BossStateMachine이 GrabbedState로 전이하는 데 사용.</summary>
    public UnityEvent OnGrabbed;

    /// <summary>ReleaseGrab() 호출 시. BossStateMachine이 다음 상태를 결정하는 데 사용.</summary>
    public UnityEvent OnGrabReleased;

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsGrabbable =>
        ArmorGauge <= 0f                              &&
        CurrentLives <= (_data != null ? _data.grabbableLivesThreshold : 1) &&
        !_isDead                                       &&
        !_isLockedForGrab;

    /// <inheritdoc/>
    public int CurrentLives { get; private set; }

    // ─── 갑옷 ─────────────────────────────────────────────────────────────────

    /// <summary>현재 갑옷 게이지. UI/이펙트에서 구독하려면 OnArmorChanged 이벤트 사용.</summary>
    public float ArmorGauge     { get; private set; }

    /// <summary>최대 갑옷 게이지.</summary>
    public float MaxArmorGauge  { get; private set; }

    /// <summary>갑옷이 완전히 파괴된 상태.</summary>
    public bool  IsArmorBroken  => ArmorGauge <= 0f;

    // ─── 취약 / 잠금 ──────────────────────────────────────────────────────────

    /// <summary>
    /// BossVulnerableState.Enter()에서 true, Exit()에서 false.
    /// TakeDamage가 갑옷 없을 때만 Lives를 감소시키는 게이트.
    /// </summary>
    public bool IsVulnerable { get; private set; }

    // ─── Private State ────────────────────────────────────────────────────────

    private bool  _isDead;
    private bool  _isLockedForGrab;

    /// <summary>보스가 사망 처리된 상태. BossCombatDialogue 등 외부 가드용.</summary>
    public bool IsDead => _isDead;

    // 가설 3: 아머 재생 타이머. TakeArmorDamage 호출 시 갱신.
    private float _lastArmorDamageTime = float.NegativeInfinity;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_data == null)
        {
            Debug.LogError($"[BossHealth] BossData가 {gameObject.name}에 연결되지 않았습니다.");
            return;
        }

        CurrentLives = _data.maxLives;
        MaxArmorGauge = _data.maxArmorGauge;
        ArmorGauge    = MaxArmorGauge;
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Update()
    {
        RegenerateArmor();
    }

    // ─── IHealth ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void TakeDamage(float damage)
    {
        if (!CanReceiveDamage(damage)) return;

        float threshold = _data != null ? _data.armorBreakThreshold : 100f;

        // 갑옷이 있을 때
        if (ArmorGauge > 0f)
        {
            // 실행 등급 공격 → 갑옷 감소
            if (damage >= threshold)
            {
                float armorDmg = _data != null ? _data.armorDamagePerExecution : 1f;
                TakeArmorDamage(armorDmg);

                RequestHitStop();
            }
            // 어떤 경우든 갑옷이 있는 한 Lives 미감소
            return;
        }

        // 갑옷 없음 + 취약 또는 GrabLock 상태에서만 Lives 감소
        if (!IsVulnerable && !_isLockedForGrab) return;

        DecreaseLives();
    }

    /// <inheritdoc/>
    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        OnDied?.Invoke();
    }

    // ─── IGrabbable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// 이 메서드가 호출되면 OnGrabbed 이벤트 발사 →
    /// BossStateMachine이 GrabbedState로 전이 → Rigidbody Kinematic 전환.
    /// </remarks>
    public void LockForGrab()
    {
        _isLockedForGrab = true;
        OnGrabbed?.Invoke();
    }

    /// <inheritdoc/>
    public void ReleaseGrab(bool executePendingDeath)
    {
        _isLockedForGrab = false;
        OnGrabReleased?.Invoke();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// ArmorGauge 완전 소진 후 Lives 강제 감소.
    /// SlamState.ExecuteTargetDeath()에서 호출.
    /// </remarks>
    public void TakeSlamDamage(float damage)
    {
        if (_isDead) return;
        if (damage <= 0f) return;

        // 갑옷 완전 소진
        if (ArmorGauge > 0f)
            TakeArmorDamage(ArmorGauge);

        // Lives 강제 감소 (취약 여부 무시)
        DecreaseLives();
    }

    // ─── 갑옷 ─────────────────────────────────────────────────────────────────

    /// <summary>갑옷 게이지를 직접 감소. Execution/Slam 외 외부 호출도 허용.</summary>
    public void TakeArmorDamage(float amount)
    {
        if (ArmorGauge <= 0f) return;
        if (amount <= 0f) return;

        ArmorGauge = Mathf.Max(0f, ArmorGauge - amount);
        // 가설 3: 타격 타이머 갱신 → 재생 지연 리셋
        _lastArmorDamageTime = Time.time;

        OnArmorChanged?.Invoke(ArmorGauge, MaxArmorGauge);

        if (ArmorGauge <= 0f)
            OnArmorBroken?.Invoke();
    }

    /// <summary>갑옷 게이지를 최대치로 재충전. Phase 전환 등에서 사용.</summary>
    public void ResetArmor()
    {
        ArmorGauge = MaxArmorGauge;
        _lastArmorDamageTime = float.NegativeInfinity;
        OnArmorChanged?.Invoke(ArmorGauge, MaxArmorGauge);
    }

    // ─── 갑옷 재생 (가설 3) ───────────────────────────────────────────────────

    /// <summary>
    /// 마지막 블링크 타격 이후 armorRegenDelay초가 지나면
    /// armorRegenRate(gauge/s) 속도로 갑옷을 서서히 회복.
    /// '몰아치는 전투' 강제 유도 — 플레이어가 뜸 들이면 처음부터 다시 깎아야 한다.
    /// </summary>
    private void RegenerateArmor()
    {
        if (_data == null || _data.armorRegenRate <= 0f) return;
        if (MaxArmorGauge <= 0f)  return;   // 갑옷 없는 보스
        if (ArmorGauge >= MaxArmorGauge) return;
        if (_isDead || _isLockedForGrab)  return;

        float timeSinceHit = Time.time - _lastArmorDamageTime;
        if (timeSinceHit < _data.armorRegenDelay) return;

        float prev = ArmorGauge;
        ArmorGauge = Mathf.Min(MaxArmorGauge,
                               ArmorGauge + _data.armorRegenRate * Time.deltaTime);

        if (!Mathf.Approximately(prev, ArmorGauge))
            OnArmorChanged?.Invoke(ArmorGauge, MaxArmorGauge);
    }

    // ─── 취약 제어 ────────────────────────────────────────────────────────────

    /// <summary>BossVulnerableState가 호출. 취약 창 열기/닫기.</summary>
    public void SetVulnerable(bool value) => IsVulnerable = value;

    // ─── Private ──────────────────────────────────────────────────────────────

    private bool CanReceiveDamage(float damage)
    {
        if (_isDead || _isLockedForGrab) return false;
        if (damage <= 0f) return false;
        return true;
    }

    private void DecreaseLives()
    {
        CurrentLives = Mathf.Max(0, CurrentLives - 1);
        OnDamaged?.Invoke(CurrentLives);

        RequestHitStop();

        if (CurrentLives <= 0)
            Die();
    }

    private void RequestHitStop()
    {
        if (_data == null || _data.hitStopDurationOnHit <= 0f) return;
        HitStopManager.Instance?.Request(
            _data.hitStopDurationOnHit,
            _data.hitStopTimeScaleOnHit
        );
    }
}
