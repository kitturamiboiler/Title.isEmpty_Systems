using UnityEngine;

/// <summary>
/// BossCombatDialogue와 같은 GameObject에 붙이는 컴패니언 컴포넌트.
/// Inspector에서 보스 종류를 선택하면 StoryDatabase에서 전투 대사를
/// BossCombatDialogue에 자동 주입한다.
///
/// 사용법:
///   1. 보스 GameObject에 BossDialogueLoader를 Add Component.
///   2. _bossType을 해당 보스로 선택.
///   3. Play — BossCombatDialogue가 StoryDatabase 대사를 사용한다.
/// </summary>
[RequireComponent(typeof(BossCombatDialogue))]
public class BossDialogueLoader : MonoBehaviour
{
    public enum BossType
    {
        None,
        Hound,    // 4장 하운드
        Paper,    // 6장 서류
        Brother,  // 10장 형
        Shadow,   // 11장 그림자
        Designer, // 12장 설계자
    }

    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("보스 종류")]
    [SerializeField] private BossType _bossType = BossType.None;

    [Header("디버그")]
    [SerializeField] private bool _logOnInject = false;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_bossType == BossType.None) return;

        var dialogue = GetComponent<BossCombatDialogue>();
        if (dialogue == null)
        {
            Debug.LogError($"[BossDialogueLoader] {gameObject.name}: BossCombatDialogue가 없습니다.");
            return;
        }

        switch (_bossType)
        {
            case BossType.Hound:
                dialogue.OverridePhase1Lines("하운드", StoryDatabase.HoundPhase1Lines);
                dialogue.OverridePhase2Lines("하운드", StoryDatabase.HoundPhase2Lines);
                dialogue.OverrideGrabbedLines("하운드", StoryDatabase.HoundOnGrabbedLines);
                break;

            case BossType.Paper:
                dialogue.OverridePhase1Lines("서류", StoryDatabase.PaperCombatLines);
                dialogue.OverrideParriedLines("서류", StoryDatabase.PaperOnParriedLines);
                dialogue.OverrideGrabbedLines("서류", StoryDatabase.PaperOnGrabbedLines);
                break;

            case BossType.Brother:
                dialogue.OverridePhase1Lines("형", StoryDatabase.BrotherCombatLines);
                dialogue.OverrideParriedLines("형", StoryDatabase.BrotherOnParriedLines);
                break;

            case BossType.Shadow:
                dialogue.OverridePhase1Lines("그림자", StoryDatabase.ShadowPhase1Lines);
                dialogue.OverridePhase2Lines("그림자", StoryDatabase.ShadowPhase2Lines);
                break;

            case BossType.Designer:
                dialogue.OverridePhase1Lines("설계자", StoryDatabase.DesignerPhase1Lines);
                dialogue.OverridePhase2Lines("설계자", StoryDatabase.DesignerPhase2Lines);
                dialogue.OverrideParriedLines("설계자", StoryDatabase.DesignerOnParriedLines);
                dialogue.OverrideGrabbedLines("설계자", StoryDatabase.DesignerOnGrabbedLines);
                break;
        }

        if (_logOnInject)
            Debug.LogWarning($"[BossDialogueLoader] '{_bossType}' 대사 주입 완료 → {gameObject.name}");
    }
}
