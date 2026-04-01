using System.Collections;
using UnityEngine;

/// <summary>
/// 전역 히트 스톱 관리자.
/// 복수 요청 시 가장 강한(낮은) timeScale · 가장 긴 duration을 병합 적용한다.
/// </summary>
public class HitStopManager : MonoBehaviour
{
    /// <summary>싱글턴 인스턴스.</summary>
    public static HitStopManager Instance { get; private set; }

    /// <summary>현재 히트 스톱 진행 여부. PlayerMovement2D 이동 차단 판단에 사용.</summary>
    public bool IsActive => _isActive;

    private float _savedTimeScale = 1f;
    private float _stopDeadline   = 0f;
    private bool  _isActive       = false;
    private Coroutine _watchCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 히트 스톱 요청.
    /// 이미 진행 중이면 duration을 연장하고, 더 강한 timeScale을 적용한다.
    /// </summary>
    /// <param name="duration">정지 지속 시간(초). unscaledTime 기준.</param>
    /// <param name="timeScale">목표 timeScale. 0.001~1 범위로 클램프.</param>
    public void Request(float duration, float timeScale)
    {
        if (duration <= 0f) return;

        timeScale = Mathf.Clamp(timeScale, 0.001f, 1f);

        if (!_isActive)
            _savedTimeScale = Time.timeScale;

        // 이미 진행 중이면 더 강한(낮은) timeScale 선택
        Time.timeScale = _isActive
            ? Mathf.Min(Time.timeScale, timeScale)
            : timeScale;

        // deadline: 남은 시간과 새 요청 중 더 긴 쪽
        float newDeadline = Time.unscaledTime + duration;
        _stopDeadline = Mathf.Max(_stopDeadline, newDeadline);

        if (_watchCoroutine != null)
            StopCoroutine(_watchCoroutine);

        _isActive       = true;
        _watchCoroutine = StartCoroutine(WatchDeadline());
    }

    private IEnumerator WatchDeadline()
    {
        while (Time.unscaledTime < _stopDeadline)
            yield return null;

        Time.timeScale  = _savedTimeScale;
        _isActive       = false;
        _watchCoroutine = null;
    }

    private void OnDestroy()
    {
        // 강제 종료 시 timeScale 복구
        if (_isActive)
            Time.timeScale = _savedTimeScale;
    }
}
