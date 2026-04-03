using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 3 [Brother] 전용 플레이어 구속 상태.
///
/// 방어 설계 (가설 3개):
///
/// 가설 1: 단검 유실 소프트 락
///   단검 투척 후 CurrentDagger가 소멸(맵 밖/감지 불가)됐을 때,
///   _daggerLostPenalty만큼 타이머를 가속 → 반복 실패 시 조기 타임아웃.
///   플레이어는 재투척 가능하나 시간 패널티가 누적된다.
///
/// 가설 2: 시각적 노이즈 (구속 인지 불가)
///   Enter 시 SpriteRenderer 색상을 BOUND_COLOR(#4A4A4A)로 변경.
///   블링크 시도 실패(단검 없이 Shift) 시 RejectFlash 코루틴으로 붉은 섬광 + TODO 이펙트.
///
/// 가설 3: 타임아웃 피해 → 사망 시 Idle 복귀 충돌
///   TakeDamage(timeout) 이후 Die()가 이미 FSM 상태를 변경했다면
///   Escape()에서 CurrentState != this 체크로 Idle 강제 전이를 건너뛴다.
/// </summary>
public class PlayerBoundState : IState2D
{
    // ─── 상수 ─────────────────────────────────────────────────────────────────

    private const float DAMAGE_ON_TIMEOUT    = 1f;

    /// <summary>단검 소멸 시 타임아웃 카운터에 추가되는 패널티 (초).</summary>
    private const float DAGGER_LOST_PENALTY  = 1.2f;

    /// <summary>가설 2: 구속 상태 스프라이트 색상 (#4A4A4A).</summary>
    private static readonly Color BOUND_COLOR  = new Color(0.29f, 0.29f, 0.29f, 1f);

    /// <summary>블링크 거부 섬광 색상.</summary>
    private static readonly Color REJECT_COLOR = new Color(1f, 0.15f, 0.15f, 1f);

    private const float REJECT_FLASH_DURATION = 0.08f;

    // ─── 의존성 ───────────────────────────────────────────────────────────────

    private readonly PlayerStateMachine      _machine;
    private readonly Rigidbody2D             _rb;
    private readonly PlayerBlinkController2D _blinkCtrl;

    // ─── 런타임 ───────────────────────────────────────────────────────────────

    private float          _duration;
    private float          _timer;
    private bool           _hasEscaped;
    private bool           _hasThrownDagger;   // 가설 1: 투척 감지 플래그

    private SpriteRenderer _spriteRenderer;    // 가설 2
    private Color          _originalColor;
    private Coroutine      _rejectFlashRoutine;

    // ─── 생성자 ───────────────────────────────────────────────────────────────

    public PlayerBoundState(
        PlayerStateMachine machine,
        Rigidbody2D rb,
        PlayerBlinkController2D blinkCtrl)
    {
        _machine   = machine;
        _rb        = rb;
        _blinkCtrl = blinkCtrl;
    }

    /// <summary>IBindable.Bind()에서 호출. 진입 전 duration 설정 필수.</summary>
    public void SetDuration(float duration)
    {
        _duration = Mathf.Max(0.1f, duration);
    }

    // ─── IState2D ─────────────────────────────────────────────────────────────

    public void Enter()
    {
        _timer           = 0f;
        _hasEscaped      = false;
        _hasThrownDagger = false;

        if (_rb == null)
        {
            Debug.LogError("[PlayerBoundState] Rigidbody2D가 null. 즉시 Idle 복귀.");
            _machine.ChangeState(_machine.Idle);
            return;
        }

        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType       = RigidbodyType2D.Kinematic;

        if (_blinkCtrl != null)
            _blinkCtrl.SuppressStateChangeOnThrow = true;

        // 가설 2: 구속 색상 적용
        _spriteRenderer = _machine.GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _originalColor         = _spriteRenderer.color;
            _spriteRenderer.color  = BOUND_COLOR;
        }

        _machine.NotifyPlayerAnim(PlayerAnimHashes.Bound);
    }

    public void Tick()
    {
        if (_hasEscaped) return;

        _timer += Time.deltaTime;

        // ── 가설 1: 단검 상태 모니터링 ───────────────────────────────────────
        if (_blinkCtrl != null)
        {
            bool hasDagger = _blinkCtrl.CurrentDagger != null;

            if (!_hasThrownDagger && hasDagger)
                _hasThrownDagger = true;   // 투척 감지

            if (_hasThrownDagger && !hasDagger)
            {
                // 단검 소멸 (블링크 없이) → 패널티 누적
                _timer           += DAGGER_LOST_PENALTY;
                _hasThrownDagger =  false;  // 재투척 허용
            }

            // ── 블링크 탈출 판정 ─────────────────────────────────────────────
            if (hasDagger && Input.GetKeyDown(KeyCode.LeftShift))
            {
                _blinkCtrl.TryBlinkToDagger();

                if (_blinkCtrl.CurrentDagger == null)
                {
                    // 블링크 성공 → 탈출
                    Escape();
                    return;
                }
            }

            // 가설 2: 단검 없이 Shift 입력 → 거부 섬광
            if (!hasDagger && Input.GetKeyDown(KeyCode.LeftShift))
                TriggerRejectFlash();
        }

        // ── 타임아웃 ──────────────────────────────────────────────────────────
        if (_timer >= _duration)
        {
            var health = _machine.GetComponent<IHealth>();
            health?.TakeDamage(DAMAGE_ON_TIMEOUT);

            // 가설 3: 사망으로 상태가 이미 전환됐으면 Idle 복귀 건너뜀
            if (_machine.CurrentState == this)
                Escape();
            else
                _hasEscaped = true; // Exit() 클린업만 수행
        }
    }

    public void FixedTick() { }

    public void Exit()
    {
        if (_rejectFlashRoutine != null)
        {
            _machine.StopCoroutine(_rejectFlashRoutine);
            _rejectFlashRoutine = null;
        }

        // 가설 2: 색상 복구
        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;

        // 물리 복구
        if (_rb != null)
            _rb.bodyType = RigidbodyType2D.Dynamic;

        // Suppress 플래그 반드시 해제
        if (_blinkCtrl != null)
            _blinkCtrl.SuppressStateChangeOnThrow = false;

        _hasEscaped      = false;
        _hasThrownDagger = false;
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void Escape()
    {
        if (_hasEscaped) return;
        _hasEscaped = true;

        // 가설 3: 이미 다른 상태(DeathState 등)로 전환됐으면 Idle 덮어쓰기 금지
        if (_machine.CurrentState != this) return;

        _machine.ChangeState(_machine.Idle);
    }

    /// <summary>
    /// 가설 2: 블링크 거부 시 붉은 섬광 + TODO 이펙트 트리거.
    /// SpriteRenderer 색상을 REJECT_COLOR → BOUND_COLOR로 복원.
    /// </summary>
    private void TriggerRejectFlash()
    {
        if (_spriteRenderer == null) return;

        if (_rejectFlashRoutine != null)
            _machine.StopCoroutine(_rejectFlashRoutine);

        _rejectFlashRoutine = _machine.StartCoroutine(RejectFlashRoutine());

        // TODO(작성자): EffectManager.Instance.SpawnEffect(rejectSparkPrefab, _machine.transform.position) — 2026-04-01
    }

    private IEnumerator RejectFlashRoutine()
    {
        if (_spriteRenderer == null) yield break;

        _spriteRenderer.color = REJECT_COLOR;
        yield return new WaitForSeconds(REJECT_FLASH_DURATION);

        // 아직 구속 상태면 BOUND_COLOR 복원
        if (_machine.CurrentState == this)
            _spriteRenderer.color = BOUND_COLOR;

        _rejectFlashRoutine = null;
    }
}
