using UnityEngine;

/// <summary>
/// Unity 6에서 폐기된 NonAlloc 오버랩/캐스트 대신
/// Physics2D.OverlapCircle / Physics2D.CircleCast 의 ContactFilter2D 오버로드에 넘길
/// <see cref="ContactFilter2D"/> 를 만든다.
/// </summary>
public static class Physics2DQueryUtil
{
    /// <summary>지정 레이어 마스크로 겹침·캐스트 필터를 구성한다.</summary>
    public static ContactFilter2D Filter(LayerMask layerMask, bool hitTriggers = true)
    {
        var f = new ContactFilter2D();
        f.SetLayerMask(layerMask);
        f.useTriggers = hitTriggers;
        return f;
    }

    /// <summary>구형 무지정 마스크 OverlapCircleNonAlloc 과 유사하게 기본 레이캐스트 레이어만 검사한다.</summary>
    public static ContactFilter2D DefaultLayersFilter(bool hitTriggers = true)
    {
        var f = new ContactFilter2D();
        f.SetLayerMask(Physics2D.DefaultRaycastLayers);
        f.useTriggers = hitTriggers;
        return f;
    }
}
