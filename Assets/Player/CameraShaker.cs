using System.Collections;
using UnityEngine;

/// <summary>
/// 카메라 셰이크 + 이동 중 Look Ahead 통합 싱글턴.
///
/// 기능:
///   Shake(duration, intensity) — 충격 피드백. 슬램/그랩/블링크 도착에서 호출.
///   Look Ahead                — 플레이어 이동 방향으로 카메라가 선행.
///                               공간감·속도감이 산나비/카타나 제로 수준으로 올라옴.
///
/// 설치:
///   카메라를 빈 부모 오브젝트(CameraRig) 아래에 넣고,
///   CameraRig에 이 컴포넌트를 부착.
///   [CameraRig (this component)]
///     └─ [Main Camera]
///
/// 참고:
///   셰이크는 CameraRig.localPosition 기준으로 오프셋을 더하고 뺀다.
///   Look Ahead도 같은 오프셋 위에 Lerp로 누적.
///   따라서 Cinemachine 없이도 완전한 카메라 연출이 가능.
/// </summary>
public class CameraShaker : MonoBehaviour
{
    // ─── 싱글턴 ───────────────────────────────────────────────────────────────

    public static CameraShaker Instance { get; private set; }

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("플레이어 Transform. 비워두면 Start()에서 PlayerBlinkController2D로 자동 탐색.")]
    [SerializeField] private Transform _playerTransform;

    [Header("Look Ahead")]
    [Tooltip("이동 방향으로 카메라가 선행할 최대 거리(유닛). 2~3이 자연스럽다.")]
    [SerializeField] private float _lookAheadDistance  = 2.2f;
    [Tooltip("Look Ahead 보간 속도. 작을수록 느리고 부드럽다. 3~5 권장.")]
    [SerializeField] private float _lookAheadLerpSpeed = 4.0f;
    [Tooltip("이 속도 이하에서는 Look Ahead를 0으로 복귀.")]
    [SerializeField] private float _velocityThreshold  = 0.5f;

    [Header("Shake Defaults (코드에서 값을 넘기지 않을 때 폴백)")]
    [SerializeField] private float _defaultDuration  = 0.06f;
    [SerializeField] private float _defaultIntensity = 0.08f;

    [Header("Slam — Orthographic Zoom Punch")]
    [Tooltip("Orthographic 메인 카메라. 비우면 Start에서 Camera.main 1회 캐싱.")]
    [SerializeField] private Camera _orthoCamera;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private Rigidbody2D _playerRb;
    private Coroutine   _shakeCoroutine;
    private Coroutine   _orthoPulseCoroutine;
    private float       _orthoBaseSize;
    private Vector3     _shakeOffset;       // 현재 프레임 셰이크 오프셋
    private Vector2     _lookAheadOffset;   // 현재 Look Ahead 오프셋 (Lerp 결과)
    private Vector3     _baseLocalPos;      // 셰이크/Look Ahead 없는 원점 로컬 위치

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _baseLocalPos = transform.localPosition;
    }

    private void Start()
    {
        // 플레이어 레퍼런스 자동 탐색
        if (_playerTransform == null)
        {
            var blinkCtrl = Object.FindFirstObjectByType<PlayerBlinkController2D>();
            if (blinkCtrl != null)
                _playerTransform = blinkCtrl.transform;
        }

        if (_playerTransform != null)
            _playerRb = _playerTransform.GetComponent<Rigidbody2D>();

        if (_orthoCamera == null)
            _orthoCamera = Camera.main;
    }

    private void LateUpdate()
    {
        UpdateLookAhead();
        ApplyOffset();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// 카메라 셰이크 요청.
    /// duration/intensity가 0이면 Inspector 기본값 사용.
    ///
    /// 호출 예시:
    ///   SlamState.ExecuteImpact()  → Shake(slamDuration, slamIntensity)
    ///   GrabState.Enter()          → Shake(grabDuration, grabIntensity)
    ///   PlayerBlinkController2D    → Shake(blinkDuration, blinkIntensity)
    /// </summary>
    public void Shake(float duration = 0f, float intensity = 0f)
    {
        float d = duration  > 0f ? duration  : _defaultDuration;
        float i = intensity > 0f ? intensity : _defaultIntensity;

        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);

        _shakeCoroutine = StartCoroutine(ShakeRoutine(d, i));
    }

    /// <summary>
    /// 슬램 등 강한 임팩트용: orthographicSize를 잠깐 줄였다가(줌 인) 복귀.
    /// unscaledDeltaTime 기반이라 히트스톱 중에도 부드럽게 동작한다.
    /// </summary>
    /// <param name="zoomInAmount">orthographicSize에서 뺄 값(양수). 0이면 무시.</param>
    /// <param name="recoverDuration">원래 크기로 돌아오는 시간(초).</param>
    public void SlamOrthoZoomPunch(float zoomInAmount, float recoverDuration)
    {
        if (zoomInAmount <= 0f || recoverDuration <= 0f) return;
        if (_orthoCamera == null)
            _orthoCamera = Camera.main;
        if (_orthoCamera == null || !_orthoCamera.orthographic) return;

        if (_orthoPulseCoroutine != null)
            StopCoroutine(_orthoPulseCoroutine);
        _orthoPulseCoroutine = StartCoroutine(OrthoZoomPunchRoutine(zoomInAmount, recoverDuration));
    }

    private IEnumerator OrthoZoomPunchRoutine(float zoomInAmount, float recoverDuration)
    {
        float startSize = _orthoCamera.orthographicSize;
        float peakSize  = Mathf.Max(0.35f, startSize - zoomInAmount);
        _orthoCamera.orthographicSize = peakSize;

        float t = 0f;
        while (t < recoverDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / recoverDuration);
            k = 1f - (1f - k) * (1f - k);
            _orthoCamera.orthographicSize = Mathf.Lerp(peakSize, startSize, k);
            yield return null;
        }

        _orthoCamera.orthographicSize = startSize;
        _orthoPulseCoroutine = null;
    }

    // ─── Look Ahead ───────────────────────────────────────────────────────────

    private void UpdateLookAhead()
    {
        if (_playerRb == null) return;

        float vx = _playerRb.linearVelocity.x;
        float targetX = Mathf.Abs(vx) > _velocityThreshold
            ? Mathf.Sign(vx) * _lookAheadDistance
            : 0f;

        _lookAheadOffset = Vector2.Lerp(
            _lookAheadOffset,
            new Vector2(targetX, 0f),
            Time.deltaTime * _lookAheadLerpSpeed
        );
    }

    private void ApplyOffset()
    {
        transform.localPosition = _baseLocalPos
            + new Vector3(_lookAheadOffset.x, _lookAheadOffset.y, 0f)
            + _shakeOffset;
    }

    // ─── 셰이크 코루틴 ────────────────────────────────────────────────────────

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 시간이 지날수록 강도 감쇠 — 첫 프레임이 가장 강하다
            float decay = 1f - (elapsed / duration);
            Vector2 rand = Random.insideUnitCircle * (intensity * decay);
            _shakeOffset = new Vector3(rand.x, rand.y, 0f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _shakeOffset    = Vector3.zero;
        _shakeCoroutine = null;
    }
}
