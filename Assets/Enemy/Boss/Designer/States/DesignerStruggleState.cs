using UnityEngine;

/// <summary>
/// 설계자 Phase 2 최종 연출: 동시 대치 (Struggle).
///
/// 연출 흐름:
///   1. 설계자가 플레이어를 잡음 → 양측 Rigidbody Kinematic 고정
///   2. UI: Space 연타 프롬프트 표시 (TODO: UI 연동)
///   3. _requiredMashes 번 이상 연타 → 플레이어 승리 → 양측 반대로 튕겨냄
///      → 설계자: BossVulnerableState (최후 취약)
///      → 플레이어: Unbind() (IBindable)
///
/// 가설 2 방어 (동시 잡기 무한 루프):
///   _forceReleaseTimer >= FORCED_RELEASE_DURATION:
///   연출 완료 여부와 무관하게 강제로 양측 분리 후 BossVulnerableState.
///   외부 투사체나 이벤트 누락으로 Release 신호가 안 와도 게임이 멈추지 않는다.
///
/// 방어 설계:
///   - IBindable null 체크: 없으면 플레이어 고정 없이 보스만 처리
///   - _hasReleased 플래그: 중복 해제 방지
/// </summary>
public class DesignerStruggleState : BossState
{
    private readonly EnemyBossDesigner _designer;

    private const float FORCED_RELEASE_DURATION = 5f;    // 가설 2: 절대 해제 시간
    private const int   REQUIRED_MASHES         = 12;    // 승리에 필요한 연타 수
    private const float PUSH_FORCE              = 8f;    // 분리 시 밀어내는 힘
    private const float STRUGGLE_PLAYER_BIND_DURATION = 4.5f;

    private float _forceReleaseTimer;
    private int   _mashCount;
    private bool  _hasReleased;

    private IBindable    _playerBindable;
    private Rigidbody2D  _playerRb;
    private StruggleUI   _ui;

    public DesignerStruggleState(BossStateMachine machine) : base(machine)
    {
        _designer = machine as EnemyBossDesigner;
    }

    public override void Enter()
    {
        _forceReleaseTimer = 0f;
        _mashCount         = 0;
        _hasReleased       = false;

        // 플레이어 구속
        if (Machine.PlayerTransform != null)
        {
            _playerBindable = Machine.PlayerTransform.GetComponent<IBindable>();
            _playerRb       = Machine.PlayerTransform.GetComponent<Rigidbody2D>();
            _playerBindable?.Bind(STRUGGLE_PLAYER_BIND_DURATION);
        }

        // 설계자 정지
        if (Machine.Rb != null)
        {
            Machine.Rb.linearVelocity = Vector2.zero;
            Machine.Rb.bodyType       = RigidbodyType2D.Kinematic;
        }

        // UI 활성화
        _ui = Object.FindFirstObjectByType<StruggleUI>();
        _ui?.Show(REQUIRED_MASHES);
    }

    public override void Tick()
    {
        if (_hasReleased) return;

        _forceReleaseTimer += Time.deltaTime;

        // 가설 2: 절대 해제 타이머 — 연출 완료 여부 무관
        if (_forceReleaseTimer >= FORCED_RELEASE_DURATION)
        {
            ForceRelease();
            return;
        }

        // Space 연타 감지
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _mashCount++;
            _ui?.UpdateProgress(_mashCount);

            if (_mashCount >= REQUIRED_MASHES)
                PlayerWins();
        }
    }

    public override void FixedTick() { }

    public override void Exit()
    {
        _ui?.Hide();

        // 설계자 물리 복원
        if (Machine.Rb != null)
            Machine.Rb.bodyType = RigidbodyType2D.Dynamic;
    }

    // ─── 해제 경로 ────────────────────────────────────────────────────────────

    /// <summary>플레이어 연타 승리 → 양측 분리 → 설계자 최후 취약.</summary>
    private void PlayerWins()
    {
        if (_hasReleased) return;
        _hasReleased = true;

        _playerBindable?.Unbind();
        PushApart();
        GoTo(Machine.Vulnerable);
    }

    /// <summary>
    /// 가설 2: 타이머 초과 강제 해제.
    /// 플레이어 승리 실패 시에도 보스가 밀쳐내며 BossVulnerableState로 진입.
    /// (설계자가 여전히 위험하지만 게임이 멈추지는 않는다.)
    /// </summary>
    private void ForceRelease()
    {
        if (_hasReleased) return;
        _hasReleased = true;

        _playerBindable?.Unbind();
        PushApart();
        GoTo(Machine.Vulnerable);
    }

    /// <summary>설계자와 플레이어를 반대 방향으로 밀어냄.</summary>
    private void PushApart()
    {
        if (Machine.PlayerTransform == null) return;

        Vector2 fromPlayer = ((Vector2)Machine.transform.position
                             - (Vector2)Machine.PlayerTransform.position).normalized;

        if (Machine.Rb != null)
            Machine.Rb.AddForce(fromPlayer * PUSH_FORCE, ForceMode2D.Impulse);

        if (_playerRb != null)
            _playerRb.AddForce(-fromPlayer * PUSH_FORCE, ForceMode2D.Impulse);
    }
}
