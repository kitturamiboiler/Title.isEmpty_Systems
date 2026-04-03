using UnityEngine;

/// <summary>
/// 중형 몹/보스를 잡은 상태 (IGrabbable.IsGrabbable == true 진입 조건).
/// - Enter : IGrabbable.LockForGrab() → 외부 데미지·Die 차단
/// - Tick  : S키 즉시 슬램 / slamAutoTriggerTime 초과 시 자동 슬램
/// - ExitForSlam : 소유권을 SlamState로 이전 후 ChangeState
/// - Exit  : _target == null 이면 즉시 리턴 (중복 ReleaseGrab 방지)
/// </summary>
public class GrabState : IState2D
{
    private readonly PlayerStateMachine _machine;
    private readonly SlamState _slamState;
    private readonly WeaponData _weaponData;

    private IGrabbable _target;
    private float _slamTimer;

    /// <summary>
    /// HitStop 종료 후에만 카운트다운 허용하는 플래그 (가설 3 방어).
    /// TODO(작성자): HitStop 완료 콜백 연결 후 Enter에서 false 초기화로 변경 — 날짜
    /// </summary>
    private bool _canCountDown;

    /// <summary>
    /// </summary>
    /// <param name="machine">PlayerStateMachine 허브.</param>
    /// <param name="slamState">전이 대상. ExitForSlam 내부에서 소유권 이전에 사용.</param>
    /// <param name="weaponData">slamAutoTriggerTime 참조용.</param>
    public GrabState(PlayerStateMachine machine, SlamState slamState, WeaponData weaponData)
    {
        _machine    = machine;
        _slamState  = slamState;
        _weaponData = weaponData;
    }

    /// <summary>GrabState 진입 전 반드시 호출. null 전달 시 Enter에서 조기 탈출.</summary>
    public void SetTarget(IGrabbable target)
    {
        _target = target;
    }

    public void Enter()
    {
        if (_target == null)
        {
            Debug.LogError("[GrabState] Enter 전 SetTarget() 호출이 필요합니다.");
            return;
        }

        _target.LockForGrab();
        _slamTimer    = 0f;
        // MVP: HitStop 콜백 미연결 상태 — 진입 즉시 카운트다운 허용
        _canCountDown = true;

        // 그랩 순간 짧은 충격 셰이크 — '잡았다'는 손맛 피드백
        if (CameraShaker.Instance != null && _weaponData != null)
            CameraShaker.Instance.Shake(_weaponData.grabShakeDuration, _weaponData.grabShakeIntensity);

        SoundManager.Instance?.PlayGrab();
        _machine.NotifyPlayerAnim(PlayerAnimHashes.Grab);
    }

    public void Tick()
    {
        if (_target == null)
        {
            // 타겟 유실 → 즉시 Idle로 탈출해 소프트 락 방지
            _machine.ChangeState(_machine.Idle);
            return;
        }

        if (_canCountDown)
            _slamTimer += Time.unscaledDeltaTime;

        // Shift: 슬램 즉시 발동 / 자동 타이머: 입력 없을 때 안전망
        bool shiftPressed = Input.GetKeyDown(KeyCode.LeftShift);
        bool autoTrigger  = _weaponData != null
                            && _slamTimer >= _weaponData.slamAutoTriggerTime;

        if (shiftPressed || autoTrigger)
            ExitForSlam();
    }

    public void FixedTick() { }

    /// <summary>
    /// SlamState로 소유권 이전 후 전이.
    /// 실행 순서:
    ///   ① slamState.SetTarget(_target) — SlamState 레퍼런스 선취
    ///   ② _target.ReleaseGrab(false)   — 잠금 해제, 파괴 유예
    ///   ③ _target = null               — GrabState 소유권 포기
    ///   ④ ChangeState(slamState)       — Exit() 시 _target == null → 중복 해제 없음
    /// </summary>
    private void ExitForSlam()
    {
        if (_slamState == null)
        {
            Debug.LogError("[GrabState] SlamState 레퍼런스가 null입니다. 슬램 전이 불가.");
            return;
        }
        if (_target == null)
        {
            Debug.LogWarning("[GrabState] ExitForSlam 호출 시 타겟이 이미 null입니다.");
            return;
        }

        _slamState.SetTarget(_target);                            // ①
        _target.ReleaseGrab(executePendingDeath: false);          // ②
        _target = null;                                           // ③
        _machine.ChangeState(_slamState);                         // ④
    }

    public void Exit()
    {
        if (_target == null) return;

        // 비정상 탈출(외부 강제 ChangeState) 시 안전망
        _target.ReleaseGrab(executePendingDeath: true);
        _target = null;
    }
}
