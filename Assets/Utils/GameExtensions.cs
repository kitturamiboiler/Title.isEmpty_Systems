using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 산나비 모작에서 쓰이던 작은 헬퍼 중, KnifeSystem에 맞게 정리한 것.
/// 레이어 마스크 검사, 코루틴 딜레이, 2D 방향/각도.
/// </summary>
public static class GameExtensions
{
    /// <summary>LayerMask에 해당 레이어가 포함되는지.</summary>
    public static bool ContainsLayer(this LayerMask layerMask, int layer)
    {
        return ((1 << layer) & layerMask) != 0;
    }

    /// <summary>WaitForSeconds 후 콜백. StartCoroutine(GameExtensions.DelayRoutine(...)).</summary>
    public static IEnumerator DelayRoutine(float delaySeconds, UnityAction next)
    {
        yield return new WaitForSeconds(delaySeconds);
        next?.Invoke();
    }

    /// <summary>WaitForSecondsRealtime 후 콜백 (Time.timeScale 무시).</summary>
    public static IEnumerator DelayRoutineRealtime(float delaySeconds, UnityAction next)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        next?.Invoke();
    }

    public static float AngleToTarget2D(this Vector3 agentPosition, Vector2 targetPosition)
    {
        float dx = targetPosition.x - agentPosition.x;
        float dy = targetPosition.y - agentPosition.y;
        return Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
    }

    public static Vector3 DirectionTo2DTarget(this Vector3 agentPosition, Vector2 targetPoint)
    {
        return new Vector3(targetPoint.x - agentPosition.x, targetPoint.y - agentPosition.y, 0f).normalized;
    }

    public static Vector2 ToVector2XY(this Vector3 v) => new Vector2(v.x, v.y);

    public static Vector3 ToVector3XY(this Vector2 v, float z = 0f) => new Vector3(v.x, v.y, z);
}
