using UnityEngine;

/// <summary>
/// 플레이어 Animator 트리거 해시. 컨트롤러에 동일 이름의 Trigger를 추가하면 <see cref="PlayerStateMachine.NotifyPlayerAnim"/>과 연결된다.
/// </summary>
public static class PlayerAnimHashes
{
    /// <summary>대기 상태 진입.</summary>
    public static readonly int Idle = Animator.StringToHash("Player_Idle");

    /// <summary>이동 상태 진입.</summary>
    public static readonly int Run = Animator.StringToHash("Player_Run");

    /// <summary>단검 비행(블링크 가능) 상태 진입.</summary>
    public static readonly int BlinkCombat = Animator.StringToHash("Player_BlinkCombat");

    /// <summary>그랩 상태 진입.</summary>
    public static readonly int Grab = Animator.StringToHash("Player_Grab");

    /// <summary>슬램(낙하) 상태 진입.</summary>
    public static readonly int Slam = Animator.StringToHash("Player_Slam");

    /// <summary>구속(Brother 바인드) 상태 진입.</summary>
    public static readonly int Bound = Animator.StringToHash("Player_Bound");
}
