using UnityEngine;

/// <summary>
/// 플레이어 패리 컨트롤러.
///
/// 패리-블링크 연결 흐름:
///   Q 키 입력
///   → parryActiveDuration 동안 OverlapCircle 유지
///   → BossParryableProjectile2D 감지 시 Deflect() 호출
///   → OnDeflected 콜백 → RefillAirBlink() → HitStop 요청
///
/// 사이드 이펙트 3가지:
///   SE-1: 판정 창(activeDuration)이 너무 길면 연속 패리로 무한 블링크.
///         → parryCooldown + activeDuration 합산이 블링크 소모 속도보다 느려야 한다.
///   SE-2: HitStop 중 추가 Q 입력이 큐에 쌓일 수 있음.
///         → _isCooling 플래그로 unscaledTime 기반 쿨다운 적용.
///   SE-3: 편향된 투사체가 파괴되지 않고 무한 반사되는 상황.
///         → BossParryableProjectile2D.IsDeflected = true 이후 IsParryable = false.
/// </summary>
[RequireComponent(typeof(PlayerBlinkController2D))]
public class PlayerParryController2D : MonoBehaviour
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [SerializeField] private WeaponData _weaponData;

    // ─── 캐시 ─────────────────────────────────────────────────────────────────

    private PlayerBlinkController2D _blinkCtrl;

    // ─── 패리 상태 ────────────────────────────────────────────────────────────

    private bool  _isActive;        // 판정 창 열린 상태
    private bool  _isCooling;       // 쿨다운 중
    private bool  _hasParried;      // 가설 1: 이번 윈도우 패리 성공 여부 — 중복 발화 하드 게이트
    private float _activeTimer;
    private float _coolTimer;

    private static readonly Collider2D[] _overlapBuffer = new Collider2D[8];

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _blinkCtrl = GetComponent<PlayerBlinkController2D>();
        if (_blinkCtrl == null)
            Debug.LogError($"[PlayerParryController2D] PlayerBlinkController2D missing on {gameObject.name}");
        if (_weaponData == null)
            Debug.LogError($"[PlayerParryController2D] WeaponData not assigned on {gameObject.name}");
    }

    private void Update()
    {
        TickCooldown();
        TickActiveWindow();
        HandleInput();
    }

    // ─── 입력 ─────────────────────────────────────────────────────────────────

    private void HandleInput()
    {
        if (_isCooling || _isActive) return;
        if (_weaponData == null) return;

        // TODO(기획): Q 키 → InputAction으로 이식 — 2026-04-01
        if (Input.GetKeyDown(KeyCode.Q))
            ActivateParryWindow();
    }

    // ─── 패리 창 ──────────────────────────────────────────────────────────────

    private void ActivateParryWindow()
    {
        _isActive    = true;
        _hasParried  = false;   // 가설 1: 새 윈도우마다 리셋
        _activeTimer = 0f;
    }

    private void TickActiveWindow()
    {
        if (!_isActive) return;
        if (_weaponData == null) { _isActive = false; return; }

        _activeTimer += Time.unscaledDeltaTime;

        // 매 프레임 판정 체크
        CheckParryHit();

        if (_activeTimer >= _weaponData.parryActiveDuration)
            EndParryWindow(success: false);
    }

    private void CheckParryHit()
    {
        // 가설 1 하드 게이트: 이번 윈도우에서 이미 패리했으면 추가 판정 차단
        if (_hasParried) return;

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            _weaponData != null ? _weaponData.parryRadius : 1.2f,
            _overlapBuffer
        );

        for (int i = 0; i < count; i++)
        {
            if (_overlapBuffer[i] == null) continue;

            var proj = _overlapBuffer[i].GetComponent<BossParryableProjectile2D>();
            if (proj == null || !proj.IsParryable) continue;

            // 패리 성공 — 플레이어를 향해 날아오는 방향으로 편향
            Vector2 reflectDir = -(proj.transform.position - transform.position).normalized;
            proj.Deflect(reflectDir);

            _hasParried = true;   // 가설 1: 동일 프레임 내 추가 Deflect 원천 차단
            OnParrySuccess();
            EndParryWindow(success: true);
            return;
        }
    }

    private void OnParrySuccess()
    {
        if (_weaponData == null) return;

        // 공중 블링크 충전
        _blinkCtrl?.RefillAirBlink(_weaponData.parryAirBlinkRefill);

        // HitStop 요청 — SE-2 방어: unscaledDeltaTime 기반 쿨다운이 HitStop 중에도 정상 작동
        HitStopManager.Instance?.Request(
            _weaponData.parryHitStopDuration,
            _weaponData.parryHitStopTimeScale
        );

        // TODO(작성자): 패리 성공 이펙트 / 사운드 트리거 — 2026-04-01
    }

    private void EndParryWindow(bool success)
    {
        _isActive    = false;
        _activeTimer = 0f;

        // 성공·실패 모두 쿨다운 진입
        _isCooling = true;
        _coolTimer = 0f;
    }

    private void TickCooldown()
    {
        if (!_isCooling) return;
        if (_weaponData == null) { _isCooling = false; return; }

        // SE-2 방어: unscaledDeltaTime — HitStop 중에도 쿨다운 진행
        _coolTimer += Time.unscaledDeltaTime;
        if (_coolTimer >= _weaponData.parryCooldown)
            _isCooling = false;
    }

    // ─── 디버그 시각화 ────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_weaponData == null || !_isActive) return;
        UnityEditor.Handles.color = new Color(0f, 1f, 0.5f, 0.3f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.forward, _weaponData.parryRadius);
    }
#endif
}
