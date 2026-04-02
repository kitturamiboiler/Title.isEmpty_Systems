using UnityEngine;

/// <summary>
/// 보스 전투 중 대사 트리거 컴포넌트.
/// EnemyBossXxx 오브젝트에 추가하여 Inspector에서 대사를 입력한다.
///
/// 트리거 시점 (기획 Q2 혼합형):
///   B — 페이즈 전환 시: Phase1 / Phase2 진입 대사 (HitStop과 연동)
///   C — 플레이어 행동 시: 패리 성공 / 그랩 성공 / 보스 취약 진입
///
/// 호출 방법:
///   각 BossState에서 직접 호출:
///     GetComponent<BossCombatDialogue>()?.TriggerPhase(BossPhase.Phase2);
///     GetComponent<BossCombatDialogue>()?.TriggerReaction(ReactionType.PlayerParried);
///
/// 방어 설계:
///   - 같은 타입 대사가 _cooldown 이내 재호출되면 무시 (전투 혼잡 방어)
///   - 대사 배열이 비어있으면 조용히 스킵
///   - 랜덤 픽 → 마지막 인덱스 기억으로 연속 중복 방지
/// </summary>
public class BossCombatDialogue : MonoBehaviour
{
    // ─── 트리거 타입 ──────────────────────────────────────────────────────────

    public enum ReactionType
    {
        PlayerParried,   // 플레이어 패리 성공
        PlayerGrabbed,   // 플레이어 그랩 성공
        BossVulnerable,  // 보스 취약 상태 진입
        BossPhase1,      // Phase 1 진입
        BossPhase2,      // Phase 2 진입
    }

    // ─── 대사 묶음 ────────────────────────────────────────────────────────────

    [System.Serializable]
    public class DialogueSet
    {
        public string   speaker = "보스";
        [Tooltip("랜덤 픽. 여러 줄 입력 시 중복 없이 순환.")]
        [TextArea(1, 3)]
        public string[] lines;
        [Tooltip("대사 표시 지속 시간. 0이면 CombatDialogueUI 기본값 사용.")]
        public float    holdTime = 0f;
        [Tooltip("우선순위: Ambient=배경, Combat=보스반응(Ambient 선점), Story=전부 중단")]
        public CombatDialogueUI.DialoguePriority priority = CombatDialogueUI.DialoguePriority.Combat;
    }

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("보스 정보")]
    [SerializeField] private string _bossName = "보스";

    [Header("페이즈 대사")]
    [SerializeField] private DialogueSet _phase1Dialogue;
    [SerializeField] private DialogueSet _phase2Dialogue;

    [Header("반응 대사")]
    [SerializeField] private DialogueSet _onPlayerParried;
    [SerializeField] private DialogueSet _onPlayerGrabbed;
    [SerializeField] private DialogueSet _onBossVulnerable;

    [Header("설정")]
    [Tooltip("같은 타입 대사의 최소 재호출 간격(초).")]
    [SerializeField] private float _reactionCooldown = 4f;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private float _lastParriedTime    = -999f;
    private float _lastGrabbedTime    = -999f;
    private float _lastVulnerableTime = -999f;

    private int _lastParryIndex      = -1;
    private int _lastGrabIndex       = -1;
    private int _lastVulnerableIndex = -1;
    private int _lastPhase1Index     = -1;
    private int _lastPhase2Index     = -1;

    // H2: 사망 경합 방어 — BossHealth 캐싱
    private BossHealth _bossHealth;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _bossHealth = GetComponent<BossHealth>();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>페이즈 전환 시 BossStateMachine.OnPhaseChanged에서 호출.</summary>
    public void TriggerPhase(BossPhase phase)
    {
        // 사망 후 페이즈 전환은 허용 (예: 설계자 마지막 대사 등)
        // 단, Phase1/2 전환 자체가 사망과 동시에 일어나는 경우 방어
        if (_bossHealth != null && _bossHealth.IsDead) return;

        switch (phase)
        {
            case BossPhase.Phase1:
                FireSet(_phase1Dialogue, ref _lastPhase1Index, ignoreCooldown: true);
                break;
            case BossPhase.Phase2:
                FireSet(_phase2Dialogue, ref _lastPhase2Index, ignoreCooldown: true);
                break;
        }
    }

    /// <summary>플레이어 행동 감지 시 각 State에서 호출.</summary>
    public void TriggerReaction(ReactionType type)
    {
        // H2: 보스가 이미 사망했으면 모든 반응 대사 차단 (서사적 모순 방지)
        if (_bossHealth != null && _bossHealth.IsDead) return;

        switch (type)
        {
            case ReactionType.PlayerParried:
                if (Time.time - _lastParriedTime < _reactionCooldown) return;
                _lastParriedTime = Time.time;
                FireSet(_onPlayerParried, ref _lastParryIndex);
                break;

            case ReactionType.PlayerGrabbed:
                if (Time.time - _lastGrabbedTime < _reactionCooldown) return;
                _lastGrabbedTime = Time.time;
                FireSet(_onPlayerGrabbed, ref _lastGrabIndex);
                break;

            case ReactionType.BossVulnerable:
                if (Time.time - _lastVulnerableTime < _reactionCooldown) return;
                _lastVulnerableTime = Time.time;
                FireSet(_onBossVulnerable, ref _lastVulnerableIndex);
                break;
        }
    }

    // ─── 공개 주입 API (BossDialogueLoader에서 StoryDatabase 데이터 주입) ──────

    /// <summary>Phase1 진입 대사 배열을 런타임에 덮어쓴다.</summary>
    public void OverridePhase1Lines(string speaker, string[] lines)
    {
        if (_phase1Dialogue == null) _phase1Dialogue = new DialogueSet();
        _phase1Dialogue.speaker = speaker;
        _phase1Dialogue.lines   = lines;
    }

    /// <summary>Phase2 진입 대사 배열을 런타임에 덮어쓴다.</summary>
    public void OverridePhase2Lines(string speaker, string[] lines)
    {
        if (_phase2Dialogue == null) _phase2Dialogue = new DialogueSet();
        _phase2Dialogue.speaker = speaker;
        _phase2Dialogue.lines   = lines;
    }

    /// <summary>패리 반응 대사 배열을 런타임에 덮어쓴다.</summary>
    public void OverrideParriedLines(string speaker, string[] lines)
    {
        if (_onPlayerParried == null) _onPlayerParried = new DialogueSet();
        _onPlayerParried.speaker = speaker;
        _onPlayerParried.lines   = lines;
    }

    /// <summary>그랩 반응 대사 배열을 런타임에 덮어쓴다.</summary>
    public void OverrideGrabbedLines(string speaker, string[] lines)
    {
        if (_onPlayerGrabbed == null) _onPlayerGrabbed = new DialogueSet();
        _onPlayerGrabbed.speaker = speaker;
        _onPlayerGrabbed.lines   = lines;
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void FireSet(DialogueSet set, ref int lastIndex, bool ignoreCooldown = false)
    {
        if (set == null || set.lines == null || set.lines.Length == 0) return;
        if (CombatDialogueUI.Instance == null) return;

        string speaker = string.IsNullOrEmpty(set.speaker) ? _bossName : set.speaker;
        string line    = PickLine(set.lines, ref lastIndex);
        if (string.IsNullOrEmpty(line)) return;

        CombatDialogueUI.Instance.Show(
            speaker,
            line,
            set.holdTime,
            set.priority
        );
    }

    /// <summary>배열에서 랜덤 픽. 직전 인덱스와 중복 방지.</summary>
    private string PickLine(string[] lines, ref int lastIndex)
    {
        if (lines.Length == 1)
        {
            lastIndex = 0;
            return lines[0];
        }

        int idx;
        int attempts = 0;
        do
        {
            idx = Random.Range(0, lines.Length);
            attempts++;
        }
        while (idx == lastIndex && attempts < 10);

        lastIndex = idx;
        return lines[idx];
    }
}
