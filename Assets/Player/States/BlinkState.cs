using UnityEngine;

/// <summary>
/// 단검이 비행 중인 전투 상태.
/// - Enter  : ThrowDagger() 성공 직후 PlayerBlinkController2D가 진입시킴.
/// - Tick   : Shift 입력 → TryBlinkToDagger() 호출 / 단검 소멸 감지 → Idle 복귀.
/// - 블링크 성공 + Grab 발생 시 GrabState가 내부에서 이미 ChangeState를 호출하므로
///   이 상태는 자연스럽게 Exit 된다.
/// </summary>
public class BlinkState : IState2D
{
    private readonly PlayerStateMachine _machine;
    private readonly PlayerBlinkController2D _blinkCtrl;

    /// <summary>
    /// </summary>
    /// <param name="machine">FSM 허브.</param>
    /// <param name="blinkCtrl">블링크 실행 위임 대상. null이면 Enter 즉시 Idle 복귀.</param>
    public BlinkState(PlayerStateMachine machine, PlayerBlinkController2D blinkCtrl)
    {
        _machine   = machine;
        _blinkCtrl = blinkCtrl;
    }

    public void Enter()
    {
        if (_blinkCtrl == null)
        {
            Debug.LogError("[BlinkState] PlayerBlinkController2D 레퍼런스가 없습니다. Idle로 즉시 복귀.");
            _machine.ChangeState(_machine.Idle);
            return;
        }

        _machine.NotifyPlayerAnim(PlayerAnimHashes.BlinkCombat);
    }

    public void Tick()
    {
        if (_blinkCtrl == null)
        {
            _machine.ChangeState(_machine.Idle);
            return;
        }

        // 단검 소멸(범위 초과 자폭, 또는 이전 프레임 블링크 완료) → 기본 상태 복귀
        if (_blinkCtrl.CurrentDagger == null)
        {
            _machine.ChangeState(_machine.Idle);
            return;
        }

        // Shift 입력 → 블링크 시도
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            bool executed = _blinkCtrl.TryBlinkToDagger();

            // 블링크가 실행됐고 GrabState 전이가 일어나지 않은 경우(여전히 BlinkState) → Idle 복귀
            if (executed && _machine.CurrentState == this)
                _machine.ChangeState(_machine.Idle);
        }
    }

    public void FixedTick() { }

    public void Exit() { }
}
