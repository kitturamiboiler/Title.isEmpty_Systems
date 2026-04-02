using System.Collections.Generic;
using UnityEngine;
using static CutscenePlayer;

/// <summary>
/// TriggerCutscene와 같은 GameObject에 붙이는 컴패니언 컴포넌트.
/// Inspector에서 챕터 키를 선택하면 StoryDatabase에서 라인을 읽어
/// TriggerCutscene._cutsceneLines에 자동 주입한다.
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
    public enum StoryKey
    {
        None,

        // ── 블로킹 컷씬 ────────────────────────────────────────────────────
        Chapter1_Opening,           // 1장 전체 (스킵 불가)
        Chapter2_Safe,              // 2장 금고
        Chapter3_Decision,          // 3장 결심 (탈출 직전)
        Chapter4_PreBoss,           // 4장 하운드 보스 전
        Chapter4_PostBoss,          // 4장 하운드 보스 후
        Chapter5_Captive,           // 5장 감금
        Chapter6_PreBoss,           // 6장 서류 보스 전
        Chapter6_PostBoss,          // 6장 서류 보스 후
        Chapter7_BadgeDiscovery,    // 7장 견장 발견
        Chapter8_Helicopter,        // 8장 헬기 장면
        Chapter8_PostFight,         // 8장 전투 후 출발
        Chapter9_Mechanic,          // 9장 야장
        Chapter10_PreBoss,          // 10장 형 보스 전
        Chapter10_Reveal,           // 10장 폭로 + 현·연서 대화
        Chapter11_Collapse,         // 11장 현 쓰러짐 (Shadow 보스 전)
        Chapter11_Acceptance,       // 11장 그림자와 화해 (Shadow 보스 내면)
        Chapter11_Awakening,        // 11장 각성 (Shadow 보스 후)
        Chapter12_Opening,          // 12장 설계자 옥상 오프닝
    }

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
            case StoryKey.Chapter1_Opening:        return StoryDatabase.GetChapter1Lines();
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
