using System.Collections;
using UnityEngine;

/// <summary>
/// 슬램 착지 순간 플레이어 스프라이트 스쿼시(가로 늘리기 + 세로 눌림).
/// 아트 없이도 ‘무게’가 보이게 만드는 코드 전용 주스.
/// <see cref="SlamState"/>가 <see cref="WeaponData"/> 수치로 호출한다.
/// </summary>
public class SlamImpactJuice : MonoBehaviour
{
    [Tooltip("비우면 이 오브젝트의 Transform.localScale을 조작한다.")]
    [SerializeField] private Transform _squashRoot;

    private Coroutine _squashCoroutine;

    /// <summary>슬램 임팩트 연출 재생. 진행 중이면 중단 후 다시 시작.</summary>
    public void PlaySquash(WeaponData weaponData)
    {
        if (weaponData == null) return;
        if (weaponData.slamVisualSquashDuration <= 0f) return;

        Transform root = _squashRoot != null ? _squashRoot : transform;
        if (_squashCoroutine != null)
            StopCoroutine(_squashCoroutine);
        _squashCoroutine = StartCoroutine(SquashRoutine(root, weaponData));
    }

    private IEnumerator SquashRoutine(Transform root, WeaponData wd)
    {
        Vector3 baseScale = root.localScale;
        float sign = Mathf.Sign(baseScale.x);
        float magX = Mathf.Abs(baseScale.x);
        float magY = Mathf.Abs(baseScale.y);

        float half = wd.slamVisualSquashDuration * 0.5f;
        root.localScale = new Vector3(
            magX * sign * wd.slamVisualSquashStretchX,
            magY * wd.slamVisualSquashCompressY,
            baseScale.z
        );

        yield return new WaitForSecondsRealtime(half);
        root.localScale = baseScale;
        yield return new WaitForSecondsRealtime(half);
        _squashCoroutine = null;
    }

    private void OnDisable()
    {
        if (_squashCoroutine != null)
        {
            StopCoroutine(_squashCoroutine);
            _squashCoroutine = null;
        }
    }
}
