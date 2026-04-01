using UnityEngine;

/// <summary>
/// 플레이어 이동 관련 수치 전용 ScriptableObject.
/// WeaponData(전투 수치)와 분리하여 단일 책임 원칙을 준수한다.
/// 인스펙터에서 Create > Game > Movement Data 로 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewMovementData", menuName = "Game/Movement Data")]
public class MovementData : ScriptableObject
{
    [Header("Ground Check (PlayerMovement2D)")]
    [Tooltip("BoxCast 하단 거리. 캡슐/타일 두께에 맞게 조정.")]
    public float groundBoxCastDistance = 0.28f;

    [Range(0f, 1f)]
    [Tooltip("바닥 법선 최소 Y. 낮출수록 경사·가장자리에서도 착지 인정.")]
    public float groundMinFloorNormalY = 0.35f;

    [Tooltip("발 모서리 보조 레이의 좌우 안쪽 여백.")]
    public float groundFootCornerInset = 0.06f;

    [Tooltip("BoxCast 시작점을 발보다 약간 위로 올려 콜라이더 내부 시작 문제 완화.")]
    public float groundCheckVerticalLift = 0.02f;

    [Header("Coyote & Input Buffer")]
    [Tooltip("낭떠러지 끝에서의 자비 시간(초).")]
    public float coyoteTime = 0.1f;

    [Tooltip("키씹 방지 선 입력 허용 시간(초).")]
    public float inputBufferTime = 0.1f;

    [Header("Wall Jump")]
    [Tooltip("벽 반대 방향으로 밀어내는 수평 속도 성분.")]
    public float wallJumpHorizontalForce = 7f;

    [Tooltip("벽 점프 시 위로 주는 수직 속도.")]
    public float wallJumpVerticalForce = 12f;

    [Tooltip("벽 점프 직후 벽 방향 입력 무시 시간(지그재그 상승 방지). 약 0.15초 권장.")]
    public float wallJumpInputLockTime = 0.15f;
}
