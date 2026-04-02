using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 분기 선택지 패널.
///
/// [선택] 타이밍에 두 버튼을 표시.
///   A — 연서가 막는다
///   B — 연서가 누른다
///
/// 선택 시 OnChoiceSelected(EndingChoice) 콜백 → EndingUSBItem이 씬 진행.
///
/// Canvas 구성 (수동 배치):
///   ChoicePanel (Image — 반투명 배경)
///     └─ TitleText   (TextMeshProUGUI — "선택")
///     └─ ButtonA     (Button + TextMeshProUGUI — "연서가 막는다")
///     └─ ButtonB     (Button + TextMeshProUGUI — "연서가 누른다")
///
/// 스프라이트 없이 기본 UI로 동작. 디자인 완성 후 Sprite 교체만 하면 된다.
/// </summary>
public class EndingChoiceUI : MonoBehaviour
{
    public enum EndingChoice { None, A_YeonsseePrevents, B_YeonsseePresses }

    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    [Header("Buttons")]
    [SerializeField] private Button              _buttonA;
    [SerializeField] private Button              _buttonB;
    [SerializeField] private TextMeshProUGUI     _labelA;
    [SerializeField] private TextMeshProUGUI     _labelB;

    [Header("Labels")]
    [SerializeField] private string _textA = "연서가 막는다";
    [SerializeField] private string _textB = "연서가 누른다";

    public System.Action<EndingChoice> OnChoiceSelected;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_buttonA != null) _buttonA.onClick.AddListener(OnClickA);
        if (_buttonB != null) _buttonB.onClick.AddListener(OnClickB);
        Hide();
    }

    private void OnDestroy()
    {
        if (_buttonA != null) _buttonA.onClick.RemoveListener(OnClickA);
        if (_buttonB != null) _buttonB.onClick.RemoveListener(OnClickB);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void Show()
    {
        if (_labelA != null) _labelA.text = _textA;
        if (_labelB != null) _labelB.text = _textB;
        if (_panel  != null) _panel.SetActive(true);
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    // ─── 버튼 핸들러 ──────────────────────────────────────────────────────────

    private void OnClickA()
    {
        Hide();
        OnChoiceSelected?.Invoke(EndingChoice.A_YeonsseePrevents);
    }

    private void OnClickB()
    {
        Hide();
        OnChoiceSelected?.Invoke(EndingChoice.B_YeonsseePresses);
    }
}
