using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// 라인바이라인 나레이션 엔진.
///
/// 사용법:
///   1. Play(lines, onComplete) 호출
///   2. 각 CutsceneLine이 순서대로 표시됨
///   3. 모든 라인 소진 시 onComplete 콜백
///
/// 지원 타입 (CutsceneLineType):
///   Narration  — 화자 없음 (프리팹에서 나레이션 TMP가 배치된 영역에 표시).
///   Dialogue   — 화자명 + 대사 (프리팹에서 대사 TMP가 배치된 영역에 표시).
///   Pause      — 텍스트 없음. duration 동안 대기 (장면 전환 숨 고르기).
///   FadeOut    — 화면 페이드 아웃 후 다음 라인 진행.
///   FadeIn     — 화면 페이드 인.
///
/// 스프라이트 없이 TextMeshPro + CanvasGroup만으로 동작.
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    /// <summary>컷신 재생 FSM 상태.</summary>
    public enum CutsceneState
    {
        Idle,
        Playing,
        WaitingForTrigger,
        Completed,
    }

    /// <summary>특정 <see cref="CutsceneLine.lineId"/> 재생 완료 시 UnityEvent (점프 힌트 등).</summary>
    [Serializable]
    public class CutsceneLineIdHook
    {
        /// <summary>매칭할 라인 ID (JSON <c>id</c>와 동일).</summary>
        public int lineId;
        /// <summary>해당 라인 연출 종료 직후 호출.</summary>
        public UnityEvent onLineFinished;
    }

    // ─── 데이터 정의 ──────────────────────────────────────────────────────────

    public enum CutsceneLineType
    {
        Narration,  // 화자 없음 (나레이션 TMP 슬롯)
        Dialogue,   // 화자명 + 대사 (대사 TMP 슬롯)
        Pause,      // 텍스트 없음. duration 대기.
        FadeOut,    // 화면 페이드 아웃.
        FadeIn,     // 화면 페이드 인.
        Confirm,    // 대안 2: 텍스트 표시 후 Space 확인 대기. _allowSkip 무관하게 항상 입력 필요.
    }

    [System.Serializable]
    public class CutsceneLine
    {
        public CutsceneLineType type = CutsceneLineType.Narration;
        [TextArea(1, 4)]
        public string text = "";
        public string speaker = "";           // Dialogue 타입일 때만 사용
        [Tooltip("라인 표시 지속 시간(초). 0이면 타입 기본값 사용.")]
        public float duration = 0f;
        [Tooltip("JSON/스토리 라인 id. 0이면 미사용.")]
        public int lineId = 0;
    }

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Narration (하단 자막)")]
    [SerializeField] private TextMeshProUGUI _narrationText;
    [SerializeField] private CanvasGroup _narrationGroup;
    [Tooltip("JSON 등의 줄바꿈(\\n)을 공백으로 합쳐 한 줄로 이어 표시. 편집용 다줄 원고는 유지 가능.")]
    [SerializeField] private bool _narrationCollapseLineBreaksToSingleLine = true;

    [Header("Dialogue (중앙 텍스트)")]
    [SerializeField] private TextMeshProUGUI _dialogueText;
    [SerializeField] private TextMeshProUGUI _speakerText;
    [SerializeField] private CanvasGroup _dialogueGroup;

    [Header("Fade Overlay")]
    [Tooltip("전체 화면을 덮는 검정 Image의 CanvasGroup.")]
    [SerializeField] private CanvasGroup _fadeOverlay;

    [Header("Timing Defaults")]
    [SerializeField] private float _narrationDefaultDuration = 2.2f;
    [SerializeField] private float _dialogueDefaultDuration = 2.8f;
    [SerializeField] private float _fadeDefaultDuration = 0.8f;
    [SerializeField] private float _textFadeDuration = 0.3f;

    [Header("Skip 설정")]
    [Tooltip("false = 스킵 불가 (1장 등 강제 관람 챕터). true = Space로 현재 라인 즉시 완성.")]
    [SerializeField] private bool _allowSkip = true;

    [Header("Dialogue — 화자·본문 간격")]
    [Tooltip("화자 TMP 끝에 붙여 Speaker_Text와 Body_Text 사이 시각 간격 확보 (한글 붙음 방지). 비우면 미사용.")]
    [SerializeField] private string _speakerBodySeparator = " ";
    [Tooltip("화자명이 있을 때만 구분 문자를 덧붙임.")]
    [SerializeField] private bool _appendSeparatorAfterSpeaker = true;

    [Header("Line ID Hooks (점프 힌트 등)")]
    [Tooltip("예: lineId 39 재생 후 점프 힌트 UI On.")]
    [SerializeField] private CutsceneLineIdHook[] _lineIdHooks;

    [Header("Resume Handoff")]
    [Tooltip("ResumeFromID 직후 입력 억제 대상 — 비우면 재개 입력 방어만 생략.")]
    [SerializeField] private PlayerMovement2D _playerMovementForInputSuppress;
    [SerializeField] private float _resumeInputSuppressSeconds = 0.15f;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private Coroutine _playRoutine;
    /// <summary>향후 타이프라이터 등 보조 코루틴 — 중첩 시 스미어 원인.</summary>
    private Coroutine _textEffectRoutine;
    Coroutine _resumeInputSuppressRoutine;
    private bool _isPlaying;
    private bool _skipCurrentLine; // 현재 라인 타이핑 즉시 완성 요청

    CutsceneState _cutsceneState = CutsceneState.Idle;
    IList<CutsceneLine> _resumeLineList;
    Action _pendingOnComplete;

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>현재 컷신 FSM 상태.</summary>
    public CutsceneState CurrentState => _cutsceneState;

    public bool IsPlaying => _isPlaying;
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
        KillTextEffectRoutine();
        StopResumeSuppressRoutineIfAny();
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _resumeLineList = lines;
        _pendingOnComplete = onComplete;
        _playRoutine = StartCoroutine(PlayRoutine(lines, onComplete));
    }

    /// <summary>
    /// <paramref name="targetLineId"/>인 라인을 재생·연출까지 끝낸 뒤 멈추고 트리거 대기한다.
    /// 목록에 해당 ID가 없으면 전부 재생 후 Completed로 종료하고 LogWarning.
    /// </summary>
    public void PlayUntil(IList<CutsceneLine> lines, int targetLineId, Action onComplete = null)
    {
        if (lines == null)
        {
            Debug.LogWarning($"[CutscenePlayer] PlayUntil: lines null — 호출 무시.");
            return;
        }
        if (targetLineId <= 0)
        {
            Debug.LogWarning("[CutscenePlayer] PlayUntil: targetLineId는 1 이상이어야 함 — 호출 무시.");
            return;
        }

        KillTextEffectRoutine();
        StopResumeSuppressRoutineIfAny();
        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        _resumeLineList = lines;
        _pendingOnComplete = onComplete;
        _playRoutine = StartCoroutine(PlayUntilRoutine(lines, targetLineId, onComplete));
    }

    /// <summary>
    /// 트리거 신호 후 <paramref name="nextLineId"/>가 붙은 줄부터 재생한다.
    /// <see cref="CutsceneState.WaitingForTrigger"/>가 아니면 무시하고 LogWarning.
    /// </summary>
    public void ResumeFromID(int nextLineId)
    {
        if (_cutsceneState != CutsceneState.WaitingForTrigger)
        {
            Debug.LogWarning(
                $"[CutscenePlayer] ResumeFromID({nextLineId}) 무시 — 현재 상태 {_cutsceneState} (WaitingForTrigger 필요)");
            return;
        }

        if (_resumeLineList == null || _resumeLineList.Count == 0)
        {
            Debug.LogError($"[CutscenePlayer] ResumeFromID: 라인 목록 없음 — 재개 불가, Idle 전이 — {gameObject.name}");
            _cutsceneState = CutsceneState.Idle;
            return;
        }

        if (nextLineId <= 0)
        {
            Debug.LogError($"[CutscenePlayer] ResumeFromID: 잘못된 nextLineId ({nextLineId}) — WaitingForTrigger 유지.");
            return;
        }

        int idx = FindLineIndexByLineId(_resumeLineList, nextLineId);
        if (idx < 0 || idx >= _resumeLineList.Count)
        {
            Debug.LogError(
                $"[CutscenePlayer] ResumeFromID: lineId {nextLineId} 는 로드된 목록 범위에 없음 — 재개 취소, WaitingForTrigger 유지.");
            return;
        }

        // Resume 직후 대사 미표시 방어: 패널 활성 + Alpha 즉시 복구, 전면 암전 잔상 제거
        PrepareStoryUiForResume();

        _cutsceneState = CutsceneState.Playing;

        StartResumeInputSuppressFollowThrough();

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(ResumeRoutine(idx));
    }

    /// <summary>
    /// Resume 시 대기 구간에서 꺼진 Narration/Dialogue 루트·Alpha 잔상·Canvas_Fade 암전을 수치로 즉시 복구한다.
    /// </summary>
    void PrepareStoryUiForResume()
    {
        if (_narrationGroup != null)
        {
            _narrationGroup.gameObject.SetActive(true);
            _narrationGroup.alpha = 1f;
        }

        if (_dialogueGroup != null)
        {
            _dialogueGroup.gameObject.SetActive(true);
            _dialogueGroup.alpha = 1f;
        }

        // 트윈·FadeOut 도중 중단 시 검정 화면이 남는 경우 — 게임플레이 가시성 회복 (FadeManager 별도 오버레이는 미연동)
        if (_fadeOverlay != null && _fadeOverlay.alpha > 0.99f)
        {
            _fadeOverlay.alpha = 0f;
            _fadeOverlay.gameObject.SetActive(false);
        }
    }

    /// <summary>현재 재생 중인 컷씬 즉시 중단.</summary>
    public void Stop()
    {
        KillTextEffectRoutine();
        StopResumeSuppressRoutineIfAny();
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        _isPlaying = false;
        _cutsceneState = CutsceneState.Idle;
        _resumeLineList = null;
        _pendingOnComplete = null;
        HideAll();
    }

    /// <summary>
    /// 재생 중일 때 현재 라인 대기를 건너뛰어 다음 줄로 진행한다(Space 스킵과 동일).
    /// 인스펙터 컴포넌트 우클릭 메뉴에서 호출 가능.
    /// </summary>
    [ContextMenu("Play Next Line")]
    public void PlayNextLine()
    {
        if (!_isPlaying)
            return;
        _skipCurrentLine = true;
    }

    void KillTextEffectRoutine()
    {
        if (_textEffectRoutine != null)
        {
            StopCoroutine(_textEffectRoutine);
            _textEffectRoutine = null;
        }
    }

    /// <summary>스미어 방지: 모든 TMP 문자열·메시 버퍼 비우기.</summary>
    void ClearAllStoryTexts()
    {
        if (_narrationText != null)
        {
            _narrationText.richText = false;
            _narrationText.text = string.Empty;
            _narrationText.ForceMeshUpdate(true);
        }
        if (_dialogueText != null)
        {
            _dialogueText.text = string.Empty;
            _dialogueText.ForceMeshUpdate(true);
        }
        if (_speakerText != null)
        {
            _speakerText.text = string.Empty;
            _speakerText.ForceMeshUpdate(true);
        }
    }

    /// <summary>나레이션 라인만 — 상대 패널은 렌더 트리에서 제거.</summary>
    void ActivateNarrationPanelOnly()
    {
        if (_dialogueGroup != null)
        {
            _dialogueGroup.alpha = 0f;
            _dialogueGroup.gameObject.SetActive(false);
        }
        if (_narrationGroup != null)
            _narrationGroup.gameObject.SetActive(true);
    }

    /// <summary>대사 라인만 — 상대 패널 비활성.</summary>
    void ActivateDialoguePanelOnly()
    {
        if (_narrationGroup != null)
        {
            _narrationGroup.alpha = 0f;
            _narrationGroup.gameObject.SetActive(false);
        }
        if (_dialogueGroup != null)
            _dialogueGroup.gameObject.SetActive(true);
    }

    /// <summary>라인 시작 전 공통 방어: 보조 코루틴 킬 + 버퍼 클리어.</summary>
    void PrepareStoryLineBeforeInject()
    {
        KillTextEffectRoutine();
        ClearAllStoryTexts();
    }

    // ─── 재생 코루틴 ──────────────────────────────────────────────────────────

    IEnumerator PlayRoutine(IList<CutsceneLine> lines, Action onComplete)
    {
        _cutsceneState = CutsceneState.Playing;
        _isPlaying = true;
        HideAll();

        foreach (var line in lines)
        {
            if (line == null)
                continue;
            yield return ProcessLineWithHooks(line);
        }

        _isPlaying = false;
        _cutsceneState = CutsceneState.Completed;
        _playRoutine = null;
        _resumeLineList = null;
        _pendingOnComplete = null;
        onComplete?.Invoke();
    }

    IEnumerator PlayUntilRoutine(IList<CutsceneLine> lines, int targetLineId, Action onComplete)
    {
        _cutsceneState = CutsceneState.Playing;
        _isPlaying = true;
        HideAll();

        bool hit = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == null)
                continue;

            yield return ProcessLineWithHooks(line);

            if (line.lineId == targetLineId)
            {
                hit = true;
                _isPlaying = false;
                _cutsceneState = CutsceneState.WaitingForTrigger;
                _playRoutine = null;
                yield break;
            }
        }

        if (!hit && targetLineId != 0)
            Debug.LogWarning($"[CutscenePlayer] PlayUntil: lineId {targetLineId} 없음 — 끝까지 재생 후 완료.");

        _isPlaying = false;
        _cutsceneState = CutsceneState.Completed;
        _playRoutine = null;
        _resumeLineList = null;
        _pendingOnComplete = null;
        onComplete?.Invoke();
    }

    IEnumerator ResumeRoutine(int startIndex)
    {
        _cutsceneState = CutsceneState.Playing;
        _isPlaying = true;

        var lines = _resumeLineList;
        var onComplete = _pendingOnComplete;

        for (int i = startIndex; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == null)
                continue;
            yield return ProcessLineWithHooks(line);
        }

        _isPlaying = false;
        _cutsceneState = CutsceneState.Completed;
        _playRoutine = null;
        _resumeLineList = null;
        _pendingOnComplete = null;
        onComplete?.Invoke();
    }

    IEnumerator ProcessLineWithHooks(CutsceneLine line)
    {
        yield return ProcessLine(line);
        RaiseLineIdHooks(line);
    }

    void RaiseLineIdHooks(CutsceneLine line)
    {
        if (line == null || _lineIdHooks == null || _lineIdHooks.Length == 0)
            return;
        int id = line.lineId;
        if (id == 0)
            return;

        for (int i = 0; i < _lineIdHooks.Length; i++)
        {
            var hook = _lineIdHooks[i];
            if (hook == null || hook.lineId != id)
                continue;
            hook.onLineFinished?.Invoke();
        }
    }

    static int FindLineIndexByLineId(IList<CutsceneLine> lines, int lineId)
    {
        if (lines == null)
            return -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line != null && line.lineId == lineId)
                return i;
        }
        return -1;
    }

    void StartResumeInputSuppressFollowThrough()
    {
        if (_resumeInputSuppressRoutine != null)
            StopCoroutine(_resumeInputSuppressRoutine);
        _resumeInputSuppressRoutine = StartCoroutine(ResumeInputSuppressCoroutine());
    }

    void StopResumeSuppressRoutineIfAny()
    {
        if (_resumeInputSuppressRoutine != null)
        {
            StopCoroutine(_resumeInputSuppressRoutine);
            _resumeInputSuppressRoutine = null;
        }
    }

    /// <summary>재개 직후 플레이어 입력 억제 펄스 — 코루틴으로 실행 창 추적.</summary>
    IEnumerator ResumeInputSuppressCoroutine()
    {
        if (_playerMovementForInputSuppress != null)
            _playerMovementForInputSuppress.ApplyResumeInputSuppressPulse(_resumeInputSuppressSeconds);
        else
            Debug.LogWarning($"[CutscenePlayer] PlayerMovement2D 미할당 — 재개 입력 억제 생략 ({gameObject.name})");

        yield return new WaitForSecondsRealtime(_resumeInputSuppressSeconds);
        _resumeInputSuppressRoutine = null;
    }

    private IEnumerator ProcessLine(CutsceneLine line)
    {
        float dur = line.duration > 0f ? line.duration : GetDefaultDuration(line.type);

        switch (line.type)
        {
            case CutsceneLineType.Narration:
                yield return ShowNarration(line.text, dur, line.lineId);
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
                if (line.lineId == StoryJsonManager.Chapter1OpeningFinalizeFadeInLineId)
                {
                    if (FadeManager.Instance != null)
                        yield return FadeManager.Instance.FadeToBlackAndFinalizeOpening(dur);
                    else
                        yield return FadeOverlay(0f, 1f, dur);
                }
                else
                    yield return FadeOverlay(1f, 0f, dur);
                break;

            case CutsceneLineType.Confirm:
                yield return ShowConfirm(line.text, line.speaker);
                break;
        }
    }

    // ─── 나레이션 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 나레이션 원문의 CR/LF를 공백으로 바꿔 한 덩어리로 이어 준다. 화면에서의 자동 줄바꿈은 TMP Rect 너비에 따름.
    /// </summary>
    static string NormalizeNarrationLineBreaksToSpaces(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        var segments = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return string.Empty;
        if (segments.Length == 1)
            return segments[0].Trim();
        for (int i = 0; i < segments.Length; i++)
            segments[i] = segments[i].Trim();
        return string.Join(" ", segments);
    }

    private IEnumerator ShowNarration(string text, float holdDuration, int lineId = 0)
    {
        // 스미어 방어: 코루틴 잔류 / TMP 중첩 / 교차 패널 렌더
        PrepareStoryLineBeforeInject();
        ActivateNarrationPanelOnly();

        string display = lineId == StoryJsonManager.Chapter1OnomatopoeiaLineId
            ? StoryJsonManager.FormatOnomatopoeiaNarration(text)
            : text;
        if (_narrationCollapseLineBreaksToSingleLine)
            display = NormalizeNarrationLineBreaksToSpaces(display);
        if (_narrationText != null)
        {
            _narrationText.richText = lineId == StoryJsonManager.Chapter1OnomatopoeiaLineId;
            _narrationText.text = display;
            _narrationText.ForceMeshUpdate(true);
        }
        if (_narrationGroup != null) _narrationGroup.alpha = 0f;

        _skipCurrentLine = false;
        yield return FadeGroup(_narrationGroup, 0f, 1f, _textFadeDuration);
        yield return HoldWithSkip(holdDuration);
        yield return FadeGroup(_narrationGroup, 1f, 0f, _textFadeDuration);
        if (_narrationText != null)
            _narrationText.richText = false;

        FollowThroughAfterNarrationLine();
    }

    // ─── 대화 ─────────────────────────────────────────────────────────────────

    private IEnumerator ShowDialogue(string speaker, string text, float holdDuration)
    {
        PrepareStoryLineBeforeInject();
        ActivateDialoguePanelOnly();

        SetDialogueSpeakerAndBody(speaker, text);
        if (_dialogueGroup != null) _dialogueGroup.alpha = 0f;

        _skipCurrentLine = false;
        yield return FadeGroup(_dialogueGroup, 0f, 1f, _textFadeDuration);
        yield return HoldWithSkip(holdDuration);
        yield return FadeGroup(_dialogueGroup, 1f, 0f, _textFadeDuration);

        FollowThroughAfterDialogueLine();
    }

    /// <summary>
    /// 화자명과 본문을 분리 TMP에 주입. 구분 문자로 ‘현여기서’처럼 붙는 현상 완화.
    /// </summary>
    void SetDialogueSpeakerAndBody(string speaker, string body)
    {
        string sp = speaker != null ? speaker.Trim() : "";
        string sep = "";
        if (_appendSeparatorAfterSpeaker && sp.Length > 0 && !string.IsNullOrEmpty(_speakerBodySeparator))
            sep = _speakerBodySeparator;

        if (_speakerText != null)
        {
            _speakerText.text = sp.Length > 0 ? sp + sep : string.Empty;
            _speakerText.ForceMeshUpdate(true);
        }
        if (_dialogueText != null)
        {
            _dialogueText.text = body ?? string.Empty;
            _dialogueText.ForceMeshUpdate(true);
        }
    }

    void FollowThroughAfterNarrationLine()
    {
        if (_narrationText != null)
        {
            _narrationText.text = string.Empty;
            _narrationText.ForceMeshUpdate(true);
        }
        if (_narrationGroup != null)
        {
            _narrationGroup.alpha = 0f;
            _narrationGroup.gameObject.SetActive(false);
        }
    }

    void FollowThroughAfterDialogueLine()
    {
        if (_speakerText != null)
        {
            _speakerText.text = string.Empty;
            _speakerText.ForceMeshUpdate(true);
        }
        if (_dialogueText != null)
        {
            _dialogueText.text = string.Empty;
            _dialogueText.ForceMeshUpdate(true);
        }
        if (_dialogueGroup != null)
        {
            _dialogueGroup.alpha = 0f;
            _dialogueGroup.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 대안 2: 텍스트 표시 후 Space 확인 대기.
    /// _allowSkip 값과 무관하게 항상 Space 입력을 요구한다.
    /// 특정 문장에서만 "[ Space ] 계속" 프롬프트를 보여주는 인터랙티브 방식.
    /// </summary>
    private IEnumerator ShowConfirm(string text, string speaker)
    {
        PrepareStoryLineBeforeInject();

        // 텍스트 표시 (화자가 있으면 Dialogue, 없으면 Narration 레이아웃)
        if (!string.IsNullOrEmpty(speaker))
        {
            ActivateDialoguePanelOnly();
            SetDialogueSpeakerAndBody(speaker, text);
            if (_dialogueGroup != null) _dialogueGroup.alpha = 0f;
            yield return FadeGroup(_dialogueGroup, 0f, 1f, _textFadeDuration);
        }
        else
        {
            ActivateNarrationPanelOnly();
            string narr = text;
            if (_narrationCollapseLineBreaksToSingleLine)
                narr = NormalizeNarrationLineBreaksToSpaces(narr);
            if (_narrationText != null) _narrationText.text = narr;
            if (_narrationText != null) _narrationText.ForceMeshUpdate(true);
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
        {
            yield return FadeGroup(_dialogueGroup, 1f, 0f, _textFadeDuration);
            FollowThroughAfterDialogueLine();
        }
        else
        {
            yield return FadeGroup(_narrationGroup, 1f, 0f, _textFadeDuration);
            FollowThroughAfterNarrationLine();
        }
    }

    /// <summary>
    /// holdDuration 동안 대기.
    /// _allowSkip = true이면 Space 입력으로 즉시 다음 라인으로 넘어감.
    /// _allowSkip = false이면 어떤 입력도 무시 (1장 강제 관람).
    /// </summary>
    private IEnumerator HoldWithSkip(float holdDuration)
    {
        _skipCurrentLine = false;
        float elapsed = 0f;
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
            elapsed += Time.unscaledDeltaTime;
            _fadeOverlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
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
        if (!group.gameObject.activeInHierarchy && to > 0f)
            group.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    // ─── 헬퍼 ─────────────────────────────────────────────────────────────────

    private void HideAll()
    {
        KillTextEffectRoutine();
        ClearAllStoryTexts();
        if (_narrationGroup != null)
        {
            _narrationGroup.alpha = 0f;
            _narrationGroup.gameObject.SetActive(false);
        }
        if (_dialogueGroup != null)
        {
            _dialogueGroup.alpha = 0f;
            _dialogueGroup.gameObject.SetActive(false);
        }
    }

    private float GetDefaultDuration(CutsceneLineType type) => type switch
    {
        CutsceneLineType.Narration => _narrationDefaultDuration,
        CutsceneLineType.Dialogue => _dialogueDefaultDuration,
        CutsceneLineType.FadeOut => _fadeDefaultDuration,
        CutsceneLineType.FadeIn => _fadeDefaultDuration,
        _ => 1f,
    };
}
