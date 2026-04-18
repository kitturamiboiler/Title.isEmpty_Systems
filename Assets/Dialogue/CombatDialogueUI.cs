using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 중 비블로킹 자막 시스템.
///
/// CutscenePlayer와의 차이:
///   CutscenePlayer — 전체화면, 게임 일시정지, 챕터 전환·엔딩용.
///   CombatDialogueUI — 화면 한쪽 자막, 게임 계속 진행, 전투 대사·워크앤톡용.
///
/// 우선순위 (H1 — 대사 중첩 방어):
///   Ambient  (0) — 워크앤톡, 배경 설명. 재생 중이면 큐에 적재.
///   Combat   (1) — 보스 반응 대사. Ambient를 선점(preempt)하고 표시.
///   Story    (2) — 중요 서사 대사. 모든 것을 즉시 중단하고 표시.
///
///   새 대사 우선순위 > 현재 재생 우선순위 → 즉시 교체.
///   새 대사 우선순위 ≤ 현재 재생 우선순위 → 큐에 적재.
///   큐는 MAX_QUEUE_SIZE 초과 시 가장 낮은 우선순위 항목 드롭.
///
/// Canvas 구성 (수동 배치):
///   DialoguePanel (Image — 반투명, 화면 하단 또는 상단)
///     └─ SpeakerText  (TextMeshProUGUI — 화자명, 좌측)
///     └─ LineText     (TextMeshProUGUI — 대사 본문)
///     └─ PortraitImage (Image — 옵션. 화자 초상화)
/// </summary>
public class CombatDialogueUI : MonoBehaviour
{
    // ─── 싱글턴 ───────────────────────────────────────────────────────────────

    public static CombatDialogueUI Instance { get; private set; }

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Panel")]
    [SerializeField] private CanvasGroup     _panelGroup;
    [SerializeField] private Image           _panelBackground;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI _speakerText;
    [SerializeField] private TextMeshProUGUI _lineText;

    [Header("Portrait (옵션)")]
    [SerializeField] private Image           _portraitImage;

    [Header("Timing")]
    [SerializeField] private float _fadeDuration     = 0.2f;
    [SerializeField] private float _defaultHoldTime  = 2.5f;
    [SerializeField] private float _urgentHoldTime   = 1.8f;
    [SerializeField] private float _charsPerSecond   = 40f; // 타이핑 속도 (0이면 즉시 표시)

    [Header("Speaker Colors")]
    [SerializeField] private Color _colorDefault  = Color.white;
    [SerializeField] private Color _colorPlayer   = new Color(0.6f, 0.9f, 1f);   // 현 — 하늘색
    [SerializeField] private Color _colorYeonsseo = new Color(1f, 0.75f, 0.4f);  // 연서 — 앰버
    [SerializeField] private Color _colorBoss     = new Color(1f, 0.4f, 0.4f);   // 보스 — 붉은

    [Header("Ambient Onomatopoeia (ShowAmbientOnomatopoeia)")]
    [Tooltip("일반 대사 대비 배경 Image 알파에 곱함 — 0.5면 약 50% 더 투명하게.")]
    [SerializeField] [Range(0.1f, 1f)] private float _ambientPanelBackgroundAlphaScale = 0.5f;
    [SerializeField] private Color _ambientOnomatopoeiaLineColor = new Color(0.68f, 0.7f, 0.74f, 0.88f);

    // ─── 우선순위 정의 (H1) ───────────────────────────────────────────────────

    public enum DialoguePriority
    {
        Ambient = 0,  // 워크앤톡, 배경 설명
        Combat  = 1,  // 보스 반응 대사
        Story   = 2,  // 중요 서사 (모두 중단)
    }

    // ─── 내부 구조 ────────────────────────────────────────────────────────────

    private struct DialogueLine
    {
        public string          speaker;
        public string          text;
        public float           holdTime;
        public DialoguePriority priority;
        public Sprite          portrait;
        public bool            ambientOnomatopoeia;
    }

    private const int MAX_QUEUE_SIZE = 4;

    private readonly Queue<DialogueLine> _queue = new Queue<DialogueLine>();
    private Coroutine        _displayRoutine;
    private bool             _isShowing;
    private DialoguePriority _currentPriority;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_panelGroup != null) _panelGroup.alpha = 0f;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// 대사를 표시한다.
    ///
    /// H1 우선순위 규칙:
    ///   priority > 현재 재생 중인 대사의 priority → 즉시 교체 (현재 대사 중단).
    ///   priority ≤ 현재 priority → 큐에 적재.
    ///   Story(2) → 큐 전체 비우고 즉시 표시.
    /// </summary>
    public void Show(string speaker, string text,
                     float           holdTime = 0f,
                     DialoguePriority priority = DialoguePriority.Ambient,
                     Sprite          portrait  = null)
    {
        float resolvedHold = holdTime > 0f
            ? holdTime
            : (priority == DialoguePriority.Combat ? _urgentHoldTime : _defaultHoldTime);

        var line = new DialogueLine
        {
            speaker  = speaker,
            text     = text,
            holdTime = resolvedHold,
            priority = priority,
            portrait = portrait,
            ambientOnomatopoeia = false,
        };

        // Story: 모든 것을 즉시 중단하고 표시
        if (priority == DialoguePriority.Story)
        {
            _queue.Clear();
            InterruptAndPlay(line);
            return;
        }

        // Combat 이상: 현재보다 높은 우선순위면 선점
        if (_isShowing && priority > _currentPriority)
        {
            InterruptAndPlay(line);
            return;
        }

        // Ambient: 큐에 적재. 낮은 우선순위 항목 먼저 드롭
        if (_queue.Count >= MAX_QUEUE_SIZE)
            DropLowestPriority();

        _queue.Enqueue(line);

        if (!_isShowing)
            _displayRoutine = StartCoroutine(DrainQueue());
    }

    /// <summary>의성어·환경음 한 줄 (이탤릭 rich text, 화자는 환경음).</summary>
    public void ShowAmbientOnomatopoeia(
        string           rawText,
        float            holdTime = 0f,
        DialoguePriority priority  = DialoguePriority.Story)
    {
        float resolvedHold = holdTime > 0f ? holdTime : _defaultHoldTime;
        string rich          = StoryJsonManager.FormatOnomatopoeiaNarration(rawText);
        var line = new DialogueLine
        {
            speaker             = "환경음",
            text                = rich,
            holdTime            = resolvedHold,
            priority            = priority,
            portrait            = null,
            ambientOnomatopoeia = true,
        };

        if (priority == DialoguePriority.Story)
        {
            _queue.Clear();
            InterruptAndPlay(line);
            return;
        }

        if (_isShowing && priority > _currentPriority)
        {
            InterruptAndPlay(line);
            return;
        }

        if (_queue.Count >= MAX_QUEUE_SIZE)
            DropLowestPriority();

        _queue.Enqueue(line);

        if (!_isShowing)
            _displayRoutine = StartCoroutine(DrainQueue());
    }

    /// <summary>한 줄 표시가 끝날 때까지 대기 (스토리 구간 연출 등).</summary>
    public System.Collections.IEnumerator WaitUntilNotShowing()
    {
        yield return null;
        while (_isShowing)
            yield return null;
    }

    /// <summary>현재 표시 중인 대사와 큐를 모두 지운다.</summary>
    public void Clear()
    {
        _queue.Clear();
        if (_displayRoutine != null)
        {
            StopCoroutine(_displayRoutine);
            _displayRoutine = null;
        }
        StartCoroutine(FadePanel(0f));
        _isShowing = false;
    }

    // ─── 재생 코루틴 ──────────────────────────────────────────────────────────

    private void InterruptAndPlay(DialogueLine line)
    {
        if (_displayRoutine != null) StopCoroutine(_displayRoutine);
        _isShowing      = false;
        _displayRoutine = StartCoroutine(DisplayLine(line));
    }

    private void DropLowestPriority()
    {
        // 큐를 순회해 가장 낮은 우선순위 1개를 제거
        var temp    = new List<DialogueLine>(_queue);
        int minIdx  = 0;
        for (int i = 1; i < temp.Count; i++)
            if ((int)temp[i].priority < (int)temp[minIdx].priority) minIdx = i;
        temp.RemoveAt(minIdx);
        _queue.Clear();
        foreach (var l in temp) _queue.Enqueue(l);
    }

    private IEnumerator DrainQueue()
    {
        _isShowing = true;
        while (_queue.Count > 0)
        {
            var line = _queue.Dequeue();
            yield return DisplayLine(line);
        }
        _isShowing = false;
    }

    private IEnumerator DisplayLine(DialogueLine line)
    {
        _currentPriority = line.priority;
        _isShowing       = true;

        SnapDialoguePanelToIntegerPixels();

        Color savedPanelBgColor = default;
        bool  hasSavedPanelBgColor = false;
        Color savedLineTextColor = default;
        bool  hasSavedLineTextColor = false;

        if (line.ambientOnomatopoeia)
        {
            if (_panelBackground != null)
            {
                hasSavedPanelBgColor = true;
                savedPanelBgColor    = _panelBackground.color;
                var c = savedPanelBgColor;
                c.a *= _ambientPanelBackgroundAlphaScale;
                _panelBackground.color = c;
            }

            if (_lineText != null)
            {
                hasSavedLineTextColor = true;
                savedLineTextColor    = _lineText.color;
                _lineText.color       = _ambientOnomatopoeiaLineColor;
            }
        }

        // 화자·텍스트 설정
        if (_speakerText != null)
        {
            _speakerText.text  = line.speaker;
            _speakerText.color = GetSpeakerColor(line.speaker);
        }

        if (_lineText != null)
        {
            _lineText.text = "";
            _lineText.richText = line.ambientOnomatopoeia;
        }

        if (_portraitImage != null)
        {
            _portraitImage.sprite  = line.portrait;
            _portraitImage.enabled = line.portrait != null;
        }

        // 패널 페이드 인
        yield return FadePanel(1f);

        // 타이핑 효과 (rich text 의성어는 태그 깨짐 방지로 즉시 전체 표시)
        if (line.ambientOnomatopoeia && _lineText != null)
        {
            _lineText.text = line.text;
        }
        else if (_charsPerSecond > 0f && _lineText != null)
        {
            string full = line.text;
            int    len  = 0;
            while (len < full.Length)
            {
                len = Mathf.Min(len + Mathf.Max(1, Mathf.RoundToInt(_charsPerSecond * Time.deltaTime)), full.Length);
                _lineText.text = full.Substring(0, len);
                yield return null;
            }
        }
        else if (_lineText != null)
        {
            _lineText.text = line.text;
        }

        // 대사 유지
        yield return new WaitForSeconds(line.holdTime);

        // 패널 페이드 아웃
        yield return FadePanel(0f);
        if (hasSavedPanelBgColor && _panelBackground != null)
            _panelBackground.color = savedPanelBgColor;
        if (hasSavedLineTextColor && _lineText != null)
            _lineText.color = savedLineTextColor;
        if (_lineText != null)
            _lineText.richText = false;
        _isShowing       = false;
        _currentPriority = DialoguePriority.Ambient;
    }

    // ─── 헬퍼 ─────────────────────────────────────────────────────────────────

    private void SnapDialoguePanelToIntegerPixels()
    {
        if (_panelGroup == null)
            return;
        var rt = _panelGroup.transform as RectTransform;
        if (rt == null)
            return;
        var p = rt.anchoredPosition3D;
        rt.anchoredPosition3D = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), Mathf.Round(p.z));
    }

    private IEnumerator FadePanel(float target)
    {
        if (_panelGroup == null) yield break;

        float start   = _panelGroup.alpha;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed          += Time.deltaTime;
            _panelGroup.alpha = Mathf.Lerp(start, target, elapsed / _fadeDuration);
            yield return null;
        }
        _panelGroup.alpha = target;
    }

    private Color GetSpeakerColor(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return _colorDefault;
        return speaker switch
        {
            "현"    => _colorPlayer,
            "연서"  => _colorYeonsseo,
            "환경음" => new Color(0.72f, 0.72f, 0.75f),
            _       => _colorBoss,
        };
    }
}
