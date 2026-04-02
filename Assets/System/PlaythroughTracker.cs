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
