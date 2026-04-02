using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 라인바이라인 나레이션 엔진.
///
/// 사용법:
///   1. Play(lines, onComplete) 호출
///   2. 각 CutsceneLine이 순서대로 표시됨
///   3. 모든 라인 소진 시 onComplete 콜백
///
/// 지원 타입 (CutsceneLineType):
///   Narration  — 화자 없음. 중앙 정렬 나레이션 텍스트.
///   Dialogue   — 화자명 + 대사. 하단 자막 형식.
///   Pause      — 텍스트 없음. duration 동안 대기 (장면 전환 숨 고르기).
///   FadeOut    — 화면 페이드 아웃 후 다음 라인 진행.
///   FadeIn     — 화면 페이드 인.
///
/// 스프라이트 없이 TextMeshPro + CanvasGroup만으로 동작.
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    // ─── 데이터 정의 ──────────────────────────────────────────────────────────

    public enum CutsceneLineType
    {
        Narration,  // 화자 없음. 중앙 나레이션.
        Dialogue,   // 화자명 + 대사.
        Pause,      // 텍스트 없음. duration 대기.
        FadeOut,    // 화면 페이드 아웃.
        FadeIn,     // 화면 페이드 인.
        Confirm,    // 대안 2: 텍스트 표시 후 Space 확인 대기. _allowSkip 무관하게 항상 입력 필요.
    }

    [System.Serializable]
    public class CutsceneLine
    {
        public CutsceneLineType type     = CutsceneLineType.Narration;
        [TextArea(1, 4)]
        public string           text     = "";
        public string           speaker  = "";           // Dialogue 타입일 때만 사용
        [Tooltip("라인 표시 지속 시간(초). 0이면 타입 기본값 사용.")]
        public float            duration = 0f;
    }

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Narration (중앙 텍스트)")]
    [SerializeField] private TextMeshProUGUI _narrationText;
    [SerializeField] private CanvasGroup     _narrationGroup;

    [Header("Dialogue (하단 자막)")]
    [SerializeField] private TextMeshProUGUI _dialogueText;
    [SerializeField] private TextMeshProUGUI _speakerText;
    [SerializeField] private CanvasGroup     _dialogueGroup;

    [Header("Fade Overlay")]
    [Tooltip("전체 화면을 덮는 검정 Image의 CanvasGroup.")]
    [SerializeField] private CanvasGroup _fadeOverlay;

    [Header("Timing Defaults")]
    [SerializeField] private float _narrationDefaultDuration = 2.2f;
    [SerializeField] private float _dialogueDefaultDuration  = 2.8f;
    [SerializeField] private float _fadeDefaultDuration      = 0.8f;
    [SerializeField] private float _textFadeDuration         = 0.3f;

    [Header("Skip 설정")]
    [Tooltip("false = 스킵 불가 (1장 등 강제 관람 챕터). true = Space로 현재 라인 즉시 완성.")]
    [SerializeField] private bool _allowSkip = true;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private Coroutine _playRoutine;
    private bool      _isPlaying;
    private bool      _skipCurrentLine; // 현재 라인 타이핑 즉시 완성 요청

    // ─── Public API ───────────────────────────────────────────────────────────

    public bool IsPlaying  => _isPlaying;
    public bool AllowSkip
    {
        get => _allowSkip;
        set => _allowSkip = value;
    }

    private void Update()
    {
        // 스킵 허용 상태일 때만 Space 입력 감지
        if (_allowSkip && _isPlaying && Input.GetKeyDown(KeyCode.Space))
            _skipCurrentLine = true;
    }

    /// <summary>컷씬 재생 시작. 완료 시 onComplete 호출.</summary>
    public void Play(IList<CutsceneLine> lines, System.Action onComplete = null)
    {
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(PlayRoutine(lines, onComplete));
    }

    /// <summary>현재 재생 중인 컷씬 즉시 중단.</summary>
    public void Stop()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        _isPlaying = false;
        HideAll();
    }

    // ─── 재생 코루틴 ──────────────────────────────────────────────────────────

    private IEnumerator PlayRoutine(IList<CutsceneLine> lines, System.Action onComplete)
    {
        _isPlaying = true;
        HideAll();

        foreach (var line in lines)
        {
            if (line == null) continue;
            yield return ProcessLine(line);
        }

        _isPlaying = false;
        onComplete?.Invoke();
    }

    private IEnumerator ProcessLine(CutsceneLine line)
    {
        float dur = line.duration > 0f ? line.duration : GetDefaultDuration(line.type);

        switch (line.type)
        {
            case CutsceneLineType.Narration:
                yield return ShowNarration(line.text, dur);
                break;

            case CutsceneLineType.Dialogue:
                yield return ShowDialogue(line.speaker, line.text, dur);
                break;

            case CutsceneLineType.Pause:
                HideAll();
                yield return new WaitForSecondsRealtime(dur);
                break;

            case CutsceneLineType.FadeOut:
                yield return FadeOverlay(0f, 1f, dur);
                break;

            case CutsceneLineType.FadeIn:
                yield return FadeOverlay(1f, 0f, dur);
                break;

            case CutsceneLineType.Confirm:
                yield return ShowConfirm(line.text, line.speaker);
                break;
        }
    }

    // ─── 나레이션 ─────────────────────────────────────────────────────────────

    private IEnumerator ShowNarration(string text, float holdDuration)
    {
        if (_dialogueGroup  != null) _dialogueGroup.alpha  = 0f;
        if (_narrationText  != null) _narrationText.text   = text;
        if (_narrationGroup != null) _narrationGroup.alpha = 0f;

        _skipCurrentLine = false;
        yield return FadeGroup(_narrationGroup, 0f, 1f, _textFadeDuration);
        yield return HoldWithSkip(holdDuration);
        yield return FadeGroup(_narrationGroup, 1f, 0f, _textFadeDuration);
    }

    // ─── 대화 ─────────────────────────────────────────────────────────────────

    private IEnumerator ShowDialogue(string speaker, string text, float holdDuration)
    {
        if (_narrationGroup != null) _narrationGroup.alpha = 0f;
        if (_speakerText    != null) _speakerText.text     = speaker;
        if (_dialogueText   != null) _dialogueText.text    = text;
        if (_dialogueGroup  != null) _dialogueGroup.alpha  = 0f;

        _skipCurrentLine = false;
        yield return FadeGroup(_dialogueGroup, 0f, 1f, _textFadeDuration);
        yield return HoldWithSkip(holdDuration);
        yield return FadeGroup(_dialogueGroup, 1f, 0f, _textFadeDuration);
    }

    /// <summary>
    /// 대안 2: 텍스트 표시 후 Space 확인 대기.
    /// _allowSkip 값과 무관하게 항상 Space 입력을 요구한다.
    /// 특정 문장에서만 "[ Space ] 계속" 프롬프트를 보여주는 인터랙티브 방식.
    /// </summary>
    private IEnumerator ShowConfirm(string text, string speaker)
    {
        // 텍스트 표시 (화자가 있으면 Dialogue, 없으면 Narration 레이아웃)
        if (!string.IsNullOrEmpty(speaker))
        {
            if (_speakerText  != null) _speakerText.text  = speaker;
            if (_dialogueText != null) _dialogueText.text = text;
            if (_dialogueGroup != null) _dialogueGroup.alpha = 0f;
            yield return FadeGroup(_dialogueGroup, 0f, 1f, _textFadeDuration);
        }
        else
        {
            if (_narrationText  != null) _narrationText.text  = text;
            if (_narrationGroup != null) _narrationGroup.alpha = 0f;
            yield return FadeGroup(_narrationGroup, 0f, 1f, _textFadeDuration);
        }

        // "[ Space ] 계속" 프롬프트 표시
        // TODO: 전용 PromptText UI 연결 — 2026-04-02
        _skipCurrentLine = false;
        yield return new WaitUntil(() =>
        {
            if (Input.GetKeyDown(KeyCode.Space)) { _skipCurrentLine = true; }
            return _skipCurrentLine;
        });
        _skipCurrentLine = false;

        // 페이드 아웃
        if (!string.IsNullOrEmpty(speaker))
            yield return FadeGroup(_dialogueGroup,  1f, 0f, _textFadeDuration);
        else
            yield return FadeGroup(_narrationGroup, 1f, 0f, _textFadeDuration);
    }

    /// <summary>
    /// holdDuration 동안 대기.
    /// _allowSkip = true이면 Space 입력으로 즉시 다음 라인으로 넘어감.
    /// _allowSkip = false이면 어떤 입력도 무시 (1장 강제 관람).
    /// </summary>
    private IEnumerator HoldWithSkip(float holdDuration)
    {
        _skipCurrentLine = false;
        float elapsed    = 0f;
        while (elapsed < holdDuration)
        {
            if (_allowSkip && _skipCurrentLine) break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _skipCurrentLine = false;
    }

    // ─── 페이드 ───────────────────────────────────────────────────────────────

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (_fadeOverlay == null) yield break;

        _fadeOverlay.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed            += Time.unscaledDeltaTime;
            _fadeOverlay.alpha  = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _fadeOverlay.alpha = to;

        // 페이드 인 완료 시 오버레이 비활성
        if (Mathf.Approximately(to, 0f))
            _fadeOverlay.gameObject.SetActive(false);
    }

    private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed     += Time.unscaledDeltaTime;
            group.alpha  = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    // ─── 헬퍼 ─────────────────────────────────────────────────────────────────

    private void HideAll()
    {
        if (_narrationGroup != null) _narrationGroup.alpha = 0f;
        if (_dialogueGroup  != null) _dialogueGroup.alpha  = 0f;
    }

    private float GetDefaultDuration(CutsceneLineType type) => type switch
    {
        CutsceneLineType.Narration => _narrationDefaultDuration,
        CutsceneLineType.Dialogue  => _dialogueDefaultDuration,
        CutsceneLineType.FadeOut   => _fadeDefaultDuration,
        CutsceneLineType.FadeIn    => _fadeDefaultDuration,
        _                          => 1f,
    };
}
