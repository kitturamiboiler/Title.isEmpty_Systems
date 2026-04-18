using System.Collections.Generic;
using UnityEngine;
using static CutscenePlayer;

/// <summary>
/// TriggerCutscene와 같은 GameObject에 붙이는 컴패니언 컴포넌트.
/// Inspector에서 챕터 키를 선택하면 라인을 읽어
/// TriggerCutscene._cutsceneLines에 자동 주입한다.
/// Chapter1_Opening은 <see cref="StoryJsonManager"/> JSON 우선, 실패 시 StoryDatabase 폴백.
///
/// 사용법:
///   1. TriggerCutscene GameObject에 ChapterStoryLoader를 Add Component.
///   2. _storyKey에서 원하는 챕터/시퀀스를 선택.
///   3. Play — TriggerCutscene이 평소처럼 동작하되, 라인은 StoryDatabase에서 온다.
///
/// 주의: TriggerCutscene._cutsceneLines가 Inspector에 직접 입력된 경우,
///       이 컴포넌트가 그것을 Awake에 덮어쓴다.
/// </summary>
[RequireComponent(typeof(TriggerCutscene))]
public class ChapterStoryLoader : MonoBehaviour
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("불러올 스토리 시퀀스")]
    [SerializeField] private StoryKey _storyKey = StoryKey.None;

    [Header("디버그")]
    [Tooltip("true: Awake에서 주입된 라인 수를 로그에 출력.")]
    [SerializeField] private bool _logOnInject = false;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_storyKey == StoryKey.None) return;

        var lines = GetLines(_storyKey);
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning($"[ChapterStoryLoader] {gameObject.name}: '{_storyKey}'에 해당하는 라인이 없습니다.");
            return;
        }

        var trigger = GetComponent<TriggerCutscene>();
        if (trigger == null)
        {
            Debug.LogError($"[ChapterStoryLoader] {gameObject.name}: TriggerCutscene이 없습니다.");
            return;
        }

        trigger.OverrideCutsceneLines(lines);

        if (_logOnInject)
            Debug.LogWarning($"[ChapterStoryLoader] '{_storyKey}' 라인 {lines.Count}개 주입 완료 → {gameObject.name}");
    }

    // ─── 라인 조회 ────────────────────────────────────────────────────────────

    private static List<CutsceneLine> GetLines(StoryKey key)
    {
        switch (key)
        {
            case StoryKey.Chapter1_Opening:
                if (StoryJsonManager.TryLoadCutsceneLines(StoryKey.Chapter1_Opening, out var ch1Json) &&
                    ch1Json != null &&
                    ch1Json.Count > 0)
                    return ch1Json;
                Debug.LogWarning("[ChapterStoryLoader] Chapter1_Opening JSON 로드 실패 — StoryDatabase 폴백 (앞 53라인만).");
                return StoryJsonManager.CopyChapter1OpeningSliceFirst53(StoryDatabase.GetChapter1Lines());
            case StoryKey.Chapter2_Safe:           return StoryDatabase.GetChapter2Lines();
            case StoryKey.Chapter3_Decision:       return StoryDatabase.GetChapter3_DecisionLines();
            case StoryKey.Chapter4_PreBoss:        return StoryDatabase.GetChapter4_PreBossLines();
            case StoryKey.Chapter4_PostBoss:       return StoryDatabase.GetChapter4_PostBossLines();
            case StoryKey.Chapter5_Captive:        return StoryDatabase.GetChapter5Lines();
            case StoryKey.Chapter6_PreBoss:        return StoryDatabase.GetChapter6_PreBossLines();
            case StoryKey.Chapter6_PostBoss:       return StoryDatabase.GetChapter6_PostBossLines();
            case StoryKey.Chapter7_BadgeDiscovery: return StoryDatabase.GetChapter7_BadgeLines();
            case StoryKey.Chapter8_Helicopter:     return StoryDatabase.GetChapter8_HelicopterLines();
            case StoryKey.Chapter8_PostFight:      return StoryDatabase.GetChapter8_PostFightLines();
            case StoryKey.Chapter9_Mechanic:       return StoryDatabase.GetChapter9_MechanicLines();
            case StoryKey.Chapter10_PreBoss:       return StoryDatabase.GetChapter10_PreBossLines();
            case StoryKey.Chapter10_Reveal:        return StoryDatabase.GetChapter10_RevealLines();
            case StoryKey.Chapter11_Collapse:      return StoryDatabase.GetChapter11_CollapseLines();
            case StoryKey.Chapter11_Acceptance:    return StoryDatabase.GetChapter11_AcceptanceLines();
            case StoryKey.Chapter11_Awakening:     return StoryDatabase.GetChapter11_AwakeningLines();
            case StoryKey.Chapter12_Opening:       return StoryDatabase.GetChapter12_OpeningLines();
            default:
                return null;
        }
    }
}
