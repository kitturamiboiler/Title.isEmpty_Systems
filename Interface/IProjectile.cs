using UnityEngine;

public interface IProjectile
{
    /// <summary>
    /// 투사체를 발사할 때 호출. 방향, 속도 등 초기 설정.
    /// </summary>
    /// <param name="origin">발사 위치</param>
    /// <param name="direction">정규화된 발사 방향</param>
    /// <param name="weaponData">무기 데이터 (데미지, 탄속 등)</param>
    void Launch(Vector3 origin, Vector3 direction, WeaponData weaponData);

    /// <summary>
    /// 투사체가 무언가에 맞았을 때 호출.
    /// </summary>
    /// <param name="hit">충돌 정보</param>
    void OnHit(RaycastHit hit);

    /// <summary>
    /// 단검 블링크(순간 이동) 등, 투사체 위치로 플레이어를 이동시킬 때 호출.
    /// </summary>
    /// <param name="targetTransform">블링크할 대상(보통 플레이어)</param>
    void BlinkTo(Transform targetTransform);
}
