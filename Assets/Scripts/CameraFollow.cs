using UnityEngine;

/// <summary>
/// 플레이어를 부드럽게 추종하는 카메라 컴포넌트.
/// SmoothDamp를 사용하여 프레임률에 독립적으로 동작한다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [Tooltip("낮을수록 더 빠르게 따라붙음. SmoothDamp smoothTime 파라미터.")]
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("카메라 최대 이동 속도. 0 이하면 제한 없음.")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, -10f);

    private Vector3 _velocity = Vector3.zero;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref _velocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime
        );
    }
}
