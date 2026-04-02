using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Shadow 보스 퍼즐의 잔상 스위치.
///
/// BlinkGhostMarker가 트리거 영역에 진입 → IsActive = true → OnActivated 발화.
/// BlinkGhostMarker가 모두 떠나거나 소멸 → IsActive = false → OnDeactivated 발화.
///
/// EnemyBossShadow가 모든 GhostSwitch.IsActive를 모니터링하여 취약 상태로 전이.
///
/// 방어 설계:
/// - HashSet으로 중복 마커 추적 → 마커가 여러 개 올라와도 단 하나만 남아도 활성
/// - 마커 Destroy 시 OnTriggerExit이 호출되지 않을 수 있음
///   → CleanupDestroyedMarkers()를 Update에서 주기적으로 실행
/// - IsFake 플래그: Phase 3 가짜 스위치는 활성화되어도 퍼즐 카운트에 포함 안 됨
/// </summary>
public class GhostSwitch : MonoBehaviour
{
    [Header("Identification")]
    [Tooltip("Phase 3: 가짜 스위치는 true. EnemyBossShadow 카운팅 시 제외됨.")]
    [SerializeField] private bool _isFake;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _switchRenderer;
    [SerializeField] private Color          _inactiveColor = Color.gray;
    [SerializeField] private Color          _activeColor   = Color.cyan;

    [Header("Events")]
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    /// <summary>진짜 스위치인지 여부. Phase 3 가짜 스위치 식별에 사용.</summary>
    public bool IsFake => _isFake;

    /// <summary>현재 BlinkGhostMarker가 올라와 있는지.</summary>
    public bool IsActive { get; private set; }

    private readonly HashSet<BlinkGhostMarker> _presentMarkers = new HashSet<BlinkGhostMarker>();

    private void Awake()
    {
        if (_switchRenderer == null)
            _switchRenderer = GetComponentInChildren<SpriteRenderer>();
        UpdateVisual();
    }

    private void Update()
    {
        // 방어: Destroy된 마커가 HashSet에 남아있으면 자동 제거
        _presentMarkers.RemoveWhere(m => m == null);
        bool shouldBeActive = _presentMarkers.Count > 0;
        if (shouldBeActive != IsActive)
            SetActive(shouldBeActive);
    }

    /// <summary>BlinkGhostMarker가 진입 시 호출 (BlinkGhostMarker.OnTriggerEnter2D에서).</summary>
    public void OnGhostEnter(BlinkGhostMarker marker)
    {
        if (marker == null) return;
        _presentMarkers.Add(marker);
        if (!IsActive) SetActive(true);
    }

    /// <summary>BlinkGhostMarker가 이탈 시 호출 (BlinkGhostMarker.OnTriggerExit2D에서).</summary>
    public void OnGhostExit(BlinkGhostMarker marker)
    {
        if (marker == null) return;
        _presentMarkers.Remove(marker);
        if (_presentMarkers.Count == 0 && IsActive) SetActive(false);
    }

    /// <summary>외부에서 스위치 강제 초기화 (보스 Phase 전환 시).</summary>
    public void Reset()
    {
        _presentMarkers.Clear();
        SetActive(false);
    }

    private void SetActive(bool value)
    {
        IsActive = value;
        UpdateVisual();

        if (value) OnActivated?.Invoke();
        else       OnDeactivated?.Invoke();
    }

    private void UpdateVisual()
    {
        if (_switchRenderer == null) return;
        _switchRenderer.color = IsActive ? _activeColor : _inactiveColor;
    }
}
