using System.Collections.Generic;
using UnityEngine;
using static CutscenePlayer;

/// <summary>
/// <see cref="StoryKey"/>별 <see cref="CutsceneLine"/> 로드 단일 진입점.
/// <see cref="ChapterStoryLoader"/>와 <see cref="GameInitializer"/>가 이 클래스를 공유한다.
/// 여기서만 수정하면 두 곳 모두 반영된다.
/// </summary>
public static class StoryLineLoader
{
    /// <summary>
    /// 지정 키의 라인 목록을 반환한다.
    /// Chapter1_Opening: JSON 우선, 실패 시 StoryDatabase 53줄 폴백.
    /// 미지원·None 시 null + LogWarning.
    /// </summary>
    public static List<CutsceneLine> Load(StoryKey key)
    {
        switch (key)
        {
            case StoryKey.Chapter1_Opening:
                if (StoryJsonManager.TryLoadCutsceneLines(StoryKey.Chapter1_Opening, out var ch1Json) &&
                    ch1Json != null &&
                    ch1Json.Count > 0)
                    return ch1Json;
                Debug.LogWarning("[StoryLineLoader] Chapter1_Opening JSON 로드 실패 — StoryDatabase 폴백(앞 53줄).");
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
                Debug.LogWarning($"[StoryLineLoader] 미지원 StoryKey — {key}");
                return null;
        }
    }
}
