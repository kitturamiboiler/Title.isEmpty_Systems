using UnityEngine;

/// <summary>
/// 회차 추적 — PlayerPrefs 기반 정적 유틸리티.
///
/// 대안 1 (Soft-Lock Skip):
///   1회차: CutscenePlayer.AllowSkip = false (스킵 불가)
///   2회차+: CutscenePlayer.AllowSkip = true  (자동 활성화)
///
/// TriggerCutscene이 Fire() 시점에 HasCompletedChapter()를 조회하여
/// CutscenePlayer.AllowSkip을 자동 결정한다.
///
/// 챕터 완료 마킹:
///   각 챕터 종료 TriggerCutscene 또는 씬 전환 시
///   PlaythroughTracker.MarkChapterComplete(chapterIndex) 호출.
/// </summary>
public static class PlaythroughTracker
{
    private const string KEY_PREFIX    = "chapter_done_";
    private const string KEY_TOTAL_RUN = "total_runs";
    private const int    MAX_CHAPTERS  = 12;

    // ─── 챕터 완료 마킹 ───────────────────────────────────────────────────────

    /// <summary>챕터 클리어 시 호출. 1~12 범위.</summary>
    public static void MarkChapterComplete(int chapter)
    {
        if (!IsValidChapter(chapter)) return;
        PlayerPrefs.SetInt(KEY_PREFIX + chapter, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// H3 증분 저장 안전망: chapter 1 ~ upToChapter 전체를 한 번에 완료 처리.
    ///
    /// 사용 시점:
    ///   TriggerCutscene.ActivateTrigger()에서 직전 챕터를 저장할 때,
    ///   "1장 트리거에 도달 = 1장 이전은 모두 완료" 논리가 성립하지 않을 수 있으므로
    ///   '지금까지 도달한 최고 챕터 직전'까지 연속 저장을 보장한다.
    ///
    /// 예: MarkUpToChapter(5) → 1, 2, 3, 4, 5 모두 완료 처리.
    /// </summary>
    public static void MarkUpToChapter(int upToChapter)
    {
        if (!IsValidChapter(upToChapter)) return;

        bool anyNew = false;
        for (int i = 1; i <= upToChapter; i++)
        {
            if (PlayerPrefs.GetInt(KEY_PREFIX + i, 0) != 1)
            {
                PlayerPrefs.SetInt(KEY_PREFIX + i, 1);
                anyNew = true;
            }
        }

        if (anyNew) PlayerPrefs.Save();
    }

    /// <summary>
    /// 현재 PlayerPrefs 기준으로 완료된 가장 높은 챕터 번호를 반환.
    /// 클리어 기록이 없으면 0 반환.
    /// </summary>
    public static int GetHighestCompletedChapter()
    {
        for (int i = MAX_CHAPTERS; i >= 1; i--)
            if (HasCompletedChapter(i)) return i;
        return 0;
    }

    /// <summary>해당 챕터를 이미 한 번 이상 클리어했는지.</summary>
    public static bool HasCompletedChapter(int chapter)
    {
        if (!IsValidChapter(chapter)) return false;
        return PlayerPrefs.GetInt(KEY_PREFIX + chapter, 0) == 1;
    }

    /// <summary>1회차 여부. 한 챕터도 클리어하지 않은 상태.</summary>
    public static bool IsFirstPlaythrough()
    {
        for (int i = 1; i <= MAX_CHAPTERS; i++)
            if (HasCompletedChapter(i)) return false;
        return true;
    }

    /// <summary>전체 클리어 횟수. 12장 완료 시 1 증가.</summary>
    public static int GetTotalRuns() => PlayerPrefs.GetInt(KEY_TOTAL_RUN, 0);

    /// <summary>12장 클리어 확정 시 호출.</summary>
    public static void MarkFullRunComplete()
    {
        int runs = GetTotalRuns() + 1;
        PlayerPrefs.SetInt(KEY_TOTAL_RUN, runs);
        PlayerPrefs.Save();
    }

    // ─── 개발용 ───────────────────────────────────────────────────────────────

    /// <summary>개발 테스트용 전체 리셋. 빌드에서는 호출하지 말 것.</summary>
    public static void ResetAll()
    {
        for (int i = 1; i <= MAX_CHAPTERS; i++)
            PlayerPrefs.DeleteKey(KEY_PREFIX + i);
        PlayerPrefs.DeleteKey(KEY_TOTAL_RUN);
        PlayerPrefs.Save();
        Debug.LogWarning("[PlaythroughTracker] 전체 회차 데이터 리셋 완료.");
    }

    private static bool IsValidChapter(int c) => c >= 1 && c <= MAX_CHAPTERS;
}
