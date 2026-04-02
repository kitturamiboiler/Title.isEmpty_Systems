using System.Collections.Generic;
using UnityEngine;
using static CutscenePlayer;

/// <summary>
/// 이동 중 특정 지점 통과 시 대화를 발동하는 트리거 존.
/// 스토리 챕터(1,2,3,5,7,8,9)의 워크앤톡에 사용.
///
/// 두 가지 모드:
///   Walk-and-Talk (기본):
///     플레이어 이동을 막지 않음. CombatDialogueUI로 자막 표시.
///   Blocking Cutscene:
///     CutscenePlayer 전체화면 재생. 챕터 전환 분기점에 사용.
///
/// H2 — 블링크 스킵 방어:
///   Start()에서 PlayerBlinkController2D.OnBlinkExecuted(from, to) 구독.
///   블링크 경로(선분)가 이 Collider2D를 통과하면 Physics2D.Linecast로 감지.
///   Collider2D가 물리적으로 통과되지 않아도 강제 발동.
///
/// H3 — 사망 시 리셋:
///   _resetOnDeath = true → PlayerHealth.OnDied 구독 → _hasTriggered 리셋.
///   중요 서사는 사망 후 재부활 시 다시 들려줌.
///   _resetOnDeath = false (기본) → 추임새 대사는 1회만.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TriggerCutscene : MonoBehaviour
{
    // ─── 모드 ─────────────────────────────────────────────────────────────────

    public enum TriggerMode
    {
        WalkAndTalk,      // 비블로킹 — CombatDialogueUI 자막
        BlockingCutscene, // 블로킹  — CutscenePlayer 전체화면
    }

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("트리거 설정")]
    [SerializeField] private TriggerMode _mode        = TriggerMode.WalkAndTalk;
    [SerializeField] private bool        _onlyOnce    = true;
    [SerializeField] private string      _playerTag   = "Player";
    [SerializeField] private float       _delay       = 0f;

    [Header("챕터 연동")]
    [Tooltip("이 트리거가 속한 챕터 번호 (1~12). 0이면 챕터 연동 없음.")]
    [SerializeField] private int    _chapterIndex    = 0;
    [Tooltip("이 트리거 이름 (체크포인트 레이블, 디버그용).")]
    [SerializeField] private string _checkpointLabel = "";
    [Tooltip("true: 이 트리거 위치를 체크포인트로 저장 (H2 강제종료 방어).")]
    [SerializeField] private bool   _saveCheckpoint  = true;

    [Header("대안 1 — 회차 자동 스킵")]
    [Tooltip("true: 2회차부터 CutscenePlayer.AllowSkip 자동 활성화.")]
    [SerializeField] private bool _autoSkipOnReplay = true;

    [Header("H2 — 블링크 스킵 방어")]
    [Tooltip("true: 블링크로 이 존을 건너뛰어도 Linecast로 강제 감지.")]
    [SerializeField] private bool _detectBlinkSkip = true;

    [Header("H3 — 사망 리셋")]
    [Tooltip("true: 플레이어 사망 시 _hasTriggered를 리셋. 중요 서사에 사용.")]
    [SerializeField] private bool _resetOnDeath = false;

    [Header("Walk-and-Talk 대사 (WalkAndTalk 모드)")]
    [SerializeField] private WalkAndTalkLine[] _walkAndTalkLines;

    [Header("블로킹 컷씬 (BlockingCutscene 모드)")]
    [SerializeField] private CutscenePlayer _cutscenePlayer;
    [SerializeField] private CutsceneLine[] _cutsceneLines;
    [Tooltip("true: 컷씬 재생 중 Time.timeScale=0. 플레이어 이동·물리 완전 정지.\n1장 오프닝 등 강제 관람에 사용.")]
    [SerializeField] private bool _freezeTimeScale = false;

    // ─── 내부 구조 ────────────────────────────────────────────────────────────

    [System.Serializable]
    public class WalkAndTalkLine
    {
        public string speaker  = "연서";
        [TextArea(1, 3)]
        public string text     = "";
        public float  holdTime = 0f;
        [Tooltip("이전 대사 재생 완료 후 다음 대사까지 대기 시간(초).")]
        public float  delay    = 0.5f;
    }

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private bool                    _hasTriggered;
    private Collider2D              _col;
    private PlayerBlinkController2D _blinkCtrl;
    private PlayerHealth            _playerHealth;
    private Transform               _playerTransform;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col != null) _col.isTrigger = true;
    }

    private void Start()
    {
        _blinkCtrl       = Object.FindFirstObjectByType<PlayerBlinkController2D>();
        _playerTransform = _blinkCtrl != null ? _blinkCtrl.transform : null;

        // H2: 블링크 스킵 감지 구독
        if (_detectBlinkSkip)
        {
            if (_blinkCtrl != null)
                _blinkCtrl.OnBlinkExecuted += OnPlayerBlinked;
        }

        // H3: 사망 리셋 구독
        if (_resetOnDeath)
        {
            _playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
            if (_playerHealth != null)
                _playerHealth.OnDied.AddListener(OnPlayerDied);
        }
    }

    private void OnDestroy()
    {
        if (_blinkCtrl   != null) _blinkCtrl.OnBlinkExecuted -= OnPlayerBlinked;
        if (_playerHealth != null) _playerHealth.OnDied.RemoveListener(OnPlayerDied);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasTriggered && _onlyOnce) return;
        if (!other.CompareTag(_playerTag)) return;

        ActivateTrigger();
    }

    // ─── H2: 블링크 경로 Linecast ─────────────────────────────────────────────

    private void OnPlayerBlinked(Vector2 from, Vector2 to)
    {
        if (_hasTriggered && _onlyOnce) return;
        if (_col == null) return;

        // 블링크 선분이 이 Collider2D를 통과하는지 체크
        var hits = Physics2D.LinecastAll(from, to);
        foreach (var hit in hits)
        {
            if (hit.collider == _col)
            {
                ActivateTrigger();
                return;
            }
        }
    }

    // ─── H3: 사망 리셋 ────────────────────────────────────────────────────────

    private void OnPlayerDied()
    {
        _hasTriggered = false; // 다음 부활 시 재발동
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void ActivateTrigger()
    {
        _hasTriggered = true;

        // H2: 체크포인트 저장 (강제 종료 후 재시작 시 안전한 위치로 복귀)
        if (_saveCheckpoint && _playerTransform != null)
        {
            CheckpointManager.Instance?.SaveCheckpoint(
                _playerTransform.position,
                _chapterIndex,
                string.IsNullOrEmpty(_checkpointLabel) ? gameObject.name : _checkpointLabel
            );
        }

        // H3: 증분 저장 — 이 트리거 도달 = 직전 챕터까지 전부 완료된 것.
        // MarkUpToChapter로 1~(N-1) 전체를 한 번에 저장하여 연속 스킵권 보장.
        // 강제 종료 시에도 재시작하면 최소 '여기까지' 스킵 가능.
        if (_chapterIndex > 1)
            PlaythroughTracker.MarkUpToChapter(_chapterIndex - 1);

        if (_delay > 0f)
            StartCoroutine(DelayedFire());
        else
            Fire();
    }

    private System.Collections.IEnumerator DelayedFire()
    {
        yield return new WaitForSeconds(_delay);
        Fire();
    }

    private void Fire()
    {
        switch (_mode)
        {
            case TriggerMode.WalkAndTalk:
                StartCoroutine(PlayWalkAndTalk());
                break;

            case TriggerMode.BlockingCutscene:
                if (_cutscenePlayer == null)
                {
                    Debug.LogWarning($"[TriggerCutscene] {gameObject.name}: _cutscenePlayer가 없습니다.");
                    return;
                }
                if (_cutsceneLines == null || _cutsceneLines.Length == 0)
                {
                    Debug.LogWarning($"[TriggerCutscene] {gameObject.name}: _cutsceneLines가 비어있습니다.");
                    return;
                }

                if (_freezeTimeScale) Time.timeScale = 0f;

                // 대안 1: 2회차+ 자동 스킵 활성화
                if (_autoSkipOnReplay && _chapterIndex > 0
                    && PlaythroughTracker.HasCompletedChapter(_chapterIndex))
                {
                    _cutscenePlayer.AllowSkip = true;
                }

                _cutscenePlayer.Play(new List<CutsceneLine>(_cutsceneLines), OnBlockingCutsceneComplete);
                break;
        }
    }

    private void OnBlockingCutsceneComplete()
    {
        if (_freezeTimeScale) Time.timeScale = 1f;
    }

    private System.Collections.IEnumerator PlayWalkAndTalk()
    {
        if (_walkAndTalkLines == null) yield break;

        foreach (var line in _walkAndTalkLines)
        {
            if (line == null || string.IsNullOrEmpty(line.text)) continue;

            if (CombatDialogueUI.Instance != null)
            {
                float hold = line.holdTime > 0f
                    ? line.holdTime
                    : Mathf.Max(1.5f, line.text.Length * 0.05f); // 글자 수 기반 자동 계산

                CombatDialogueUI.Instance.Show(line.speaker, line.text, hold);
                yield return new WaitForSeconds(hold + line.delay);
            }
            else
            {
                Debug.LogWarning("[TriggerCutscene] CombatDialogueUI.Instance가 없습니다.");
            }
        }
    }

    // ─── 공개 주입 API (ChapterStoryLoader 등 외부에서 사용) ──────────────────

    /// <summary>
    /// 런타임에서 컷씬 라인을 덮어쓴다.
    /// ChapterStoryLoader가 StoryDatabase 데이터를 주입할 때 사용.
    /// </summary>
    public void OverrideCutsceneLines(List<CutsceneLine> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning($"[TriggerCutscene] {gameObject.name}: OverrideCutsceneLines에 빈 리스트가 전달되었습니다.");
            return;
        }
        _cutsceneLines = lines.ToArray();
    }

    // ─── Editor 기즈모 ────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = _mode == TriggerMode.WalkAndTalk
            ? new Color(0.2f, 0.8f, 0.2f, 0.3f)   // 초록 — Walk-and-Talk
            : new Color(0.8f, 0.4f, 0f, 0.3f);     // 주황 — Blocking

        var col = GetComponent<BoxCollider2D>();
        if (col != null)
            Gizmos.DrawCube(transform.position + (Vector3)col.offset,
                            new Vector3(col.size.x, col.size.y, 0.1f));
    }
}
