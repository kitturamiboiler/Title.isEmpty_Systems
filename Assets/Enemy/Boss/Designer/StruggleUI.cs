using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설계자 Struggle 연출 UI.
///
/// 구성 (Canvas 하위에 수동 배치):
///   StrugglePanel (Image — 반투명 검정)
///     └─ PromptText   (TextMeshProUGUI — "[ SPACE ] 연타!")
///     └─ BarBackground (Image — 흰 박스)
///         └─ BarFill   (Image — fillMethod: Horizontal)
///     └─ CountText    (TextMeshProUGUI — "0 / 12")
///
/// 스프라이트 없이 Unity 기본 UI 컴포넌트만으로 동작한다.
/// 디자인 완성 후 Sprite만 교체하면 된다.
/// </summary>
public class StruggleUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI _promptText;
    [SerializeField] private TextMeshProUGUI _countText;

    [Header("Progress Bar")]
    [Tooltip("fillMethod = Horizontal 으로 설정된 Image 컴포넌트.")]
    [SerializeField] private Image _barFill;

    [Header("Pulse")]
    [SerializeField] private float _pulseScale   = 1.25f;
    [SerializeField] private float _pulseDuration = 0.08f;

    private int   _required;
    private float _pulseTimer;
    private bool  _isPulsing;
    private Vector3 _originalScale;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_panel == null)
            Debug.LogError("[StruggleUI] _panel이 연결되지 않았습니다.");

        _originalScale = _promptText != null ? _promptText.transform.localScale : Vector3.one;
        Hide();
    }

    private void Update()
    {
        if (!_isPulsing) return;

        _pulseTimer += Time.unscaledDeltaTime;
        if (_pulseTimer >= _pulseDuration)
        {
            _pulseTimer = 0f;
            _isPulsing  = false;
            if (_promptText != null)
                _promptText.transform.localScale = _originalScale;
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Struggle 진입 시 DesignerStruggleState에서 호출.</summary>
    public void Show(int required)
    {
        _required = required;

        if (_panel != null)       _panel.SetActive(true);
        if (_promptText != null)  _promptText.text = "[ SPACE ] 연타!";
        if (_barFill != null)     _barFill.fillAmount = 0f;
        if (_countText != null)   _countText.text = $"0 / {_required}";
    }

    /// <summary>연타 횟수 갱신 시 호출. 진행도 바와 카운트 텍스트 업데이트.</summary>
    public void UpdateProgress(int current)
    {
        if (_barFill != null)
            _barFill.fillAmount = _required > 0 ? (float)current / _required : 0f;

        if (_countText != null)
            _countText.text = $"{current} / {_required}";

        // 연타 피드백: 텍스트 펄스
        TriggerPulse();
    }

    /// <summary>Struggle 종료(승리/강제 해제) 시 호출.</summary>
    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void TriggerPulse()
    {
        if (_promptText == null) return;
        _isPulsing  = true;
        _pulseTimer = 0f;
        _promptText.transform.localScale = _originalScale * _pulseScale;
    }
}
