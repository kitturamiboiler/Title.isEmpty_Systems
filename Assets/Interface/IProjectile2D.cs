using UnityEngine;

/// <summary>
/// 순수 2D 투사체 계약. Vector2 / Collision2D 기반.
/// </summary>
public interface IProjectile2D
{
    /// <summary>
    /// 투사체를 발사한다. 반드시 <paramref name="data"/> null 체크 후 진입.
    /// </summary>
    /// <param name="origin">발사 기준 월드 좌표.</param>
    /// <param name="direction">정규화된 발사 방향.</param>
    /// <param name="data">무기 스탯 (속도·데미지 등).</param>
    void Launch(Vector2 origin, Vector2 direction, WeaponData data);

    /// <summary>
    /// 물리 충돌 발생 시 투사체가 처리할 동작 (데미지·박힘·이펙트).
    /// </summary>
    /// <param name="collision">Unity 2D 충돌 정보.</param>
    void OnHit(Collision2D collision);

    /// <summary>이 투사체로 블링크 가능 여부.</summary>
    bool CanBlink { get; }

    /// <summary>현재 투사체 월드 위치.</summary>
    Vector2 CurrentPosition { get; }
}
