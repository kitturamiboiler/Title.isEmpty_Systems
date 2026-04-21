using System;
using System.Collections.Generic;
using UnityEngine;
using static CutscenePlayer;
using CutsceneLine = CutscenePlayer.CutsceneLine;

/// <summary>
/// <c>Resources/StoryData/&lt;StoryKey&gt;</c> JSON을 로드해 <see cref="CutsceneLine"/> 리스트로 변환한다.
/// 각 세그먼트 <c>text</c>는 <c>\n</c>으로 나눈 줄마다 공백 제외 26자 이하 — 위반 시 <c>LogError</c>.
/// </summary>
public static class StoryJsonManager
{
    const int MaxCharsPerLineExcludingSpaces = 26;
    const string ResourceFolder = "StoryData";

    /// <summary>Chapter1_Opening 오프닝 블로킹 구간 마지막 라인 id (FadeIn). 이후 라인은 다음 씬용.</summary>
    public const int Chapter1OpeningMaxLineId = 53;

    /// <summary>의성어 나레이션 (꼬르륵.) — 컷씬/전투 UI에서 별도 서식.</summary>
    public const int Chapter1OnomatopoeiaLineId = 43;

    /// <summary>오프닝 구간 종료 FadeIn (암전 + Finalize).</summary>
    public const int Chapter1OpeningFinalizeFadeInLineId = 53;

    static readonly Dictionary<StoryKey, List<CutsceneLine>> _cache = new Dictionary<StoryKey, List<CutsceneLine>>();

    /// <summary>의성어 한 줄을 TMP rich text 이탤릭 + 괄호로 감싼다.</summary>
    public static string FormatOnomatopoeiaNarration(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "<i>*( … )*</i>";
        string t = raw.Trim();
        if (t.Contains("꼬르륵"))
            return "<i>*(꼬르륵...)*</i>";
        return $"<i>*({t})*</i>";
    }

    /// <summary>StoryDatabase 폴백용 — 앞 53라인만 복제하고 lineId 1~53을 부여한다.</summary>
    public static List<CutsceneLine> CopyChapter1OpeningSliceFirst53(IList<CutsceneLine> source)
    {
        if (source == null || source.Count == 0)
            return null;
        int n = Mathf.Min(Chapter1OpeningMaxLineId, source.Count);
        var dst = new List<CutsceneLine>(n);
        for (int i = 0; i < n; i++)
        {
            var s = source[i];
            dst.Add(new CutsceneLine
            {
                type     = s.type,
                text     = s.text,
                speaker  = s.speaker,
                duration = s.duration,
                lineId   = i + 1,
            });
        }
        return dst;
    }

    /// <summary>로드 성공 시 true. JSON 없음/파싱 실패 시 false (<paramref name="lines"/>는 null).</summary>
    public static bool TryLoadCutsceneLines(StoryKey key, out List<CutsceneLine> lines, bool useCache = true)
    {
        lines = null;
        if (key == StoryKey.None)
            return false;

        if (useCache && _cache.TryGetValue(key, out var cached) && cached != null && cached.Count > 0)
        {
            lines = cached;
            return true;
        }

        string path = $"{ResourceFolder}/{key}";
        var ta = Resources.Load<TextAsset>(path);
        if (ta == null)
        {
            Debug.LogError($"[StoryJsonManager] Resources.Load 실패 — {path}");
            return false;
        }

        StoryJsonFileRoot root;
        try
        {
            root = JsonUtility.FromJson<StoryJsonFileRoot>(ta.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StoryJsonManager] JSON 파싱 실패 — {path} — {ex.Message}");
            return false;
        }

        if (root?.lines == null || root.lines.Length == 0)
        {
            Debug.LogError($"[StoryJsonManager] lines 비어 있음 — {path}");
            return false;
        }

        var list = new List<CutsceneLine>(root.lines.Length);
        foreach (var jl in root.lines)
        {
            ValidateSegmentLineLength(key, jl);
            list.Add(ToCutsceneLine(jl));
        }

        if (key == StoryKey.Chapter1_Opening)
            list.RemoveAll(l => l.lineId > Chapter1OpeningMaxLineId);

        _cache[key] = list;
        lines = list;
        return true;
    }

    /// <summary>인덱스는 0 기준. 실패 시 false.</summary>
    public static bool TryGetLineAt(StoryKey key, int index, out CutsceneLine line, bool useCache = true)
    {
        line = null;
        if (!TryLoadCutsceneLines(key, out var list, useCache) || list == null)
            return false;
        if (index < 0 || index >= list.Count)
            return false;
        line = list[index];
        return true;
    }

    /// <summary>에디터/핫리로드 시 캐시 무효화.</summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }

    static void ValidateSegmentLineLength(StoryKey key, StoryJsonLineEntry jl)
    {
        if (string.IsNullOrEmpty(jl.text))
            return;

        foreach (var raw in jl.text.Split('\n'))
        {
            var segment = raw.TrimEnd('\r');
            int count = 0;
            for (int i = 0; i < segment.Length; i++)
            {
                if (!char.IsWhiteSpace(segment[i]))
                    count++;
            }

            if (count > MaxCharsPerLineExcludingSpaces)
            {
                Debug.LogError(
                    $"[StoryJsonManager] 디렉터님, 26자 넘었습니다! key={key} id={jl.id} ({count}자) — \"{segment}\"");
            }
        }
    }

    static CutsceneLine ToCutsceneLine(StoryJsonLineEntry j)
    {
        var type = ParseLineType(j.type);
        var line = new CutsceneLine { type = type, duration = j.duration, lineId = j.id };
        switch (type)
        {
            case CutsceneLineType.Narration:
                line.text = j.text ?? "";
                break;
            case CutsceneLineType.Dialogue:
                line.speaker = j.speaker ?? "";
                line.text = j.text ?? "";
                break;
            default:
                break;
        }

        return line;
    }

    static CutsceneLineType ParseLineType(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            Debug.LogWarning("[StoryJsonManager] ParseLineType: type 필드가 빈 문자열 — Narration으로 폴백.");
            return CutsceneLineType.Narration;
        }

        switch (s)
        {
            case "FadeIn":    return CutsceneLineType.FadeIn;
            case "FadeOut":   return CutsceneLineType.FadeOut;
            case "Pause":     return CutsceneLineType.Pause;
            case "Narration": return CutsceneLineType.Narration;
            case "Dialogue":  return CutsceneLineType.Dialogue;
            case "Confirm":   return CutsceneLineType.Confirm;
            default:
                Debug.LogWarning(
                    $"[StoryJsonManager] ParseLineType: 알 수 없는 type \"{s}\" — Narration으로 폴백. JSON 오타 여부를 확인하세요.");
                return CutsceneLineType.Narration;
        }
    }
}

/// <summary>StoryData/*.json 루트 — JsonUtility용.</summary>
[Serializable]
public class StoryJsonFileRoot
{
    /// <summary>파일명과 동일한 키 문자열.</summary>
    public string storyKey;
    /// <summary>컷씬 라인 배열.</summary>
    public StoryJsonLineEntry[] lines;
}

/// <summary>한 컷씬 라인 — JsonUtility용.</summary>
[Serializable]
public class StoryJsonLineEntry
{
    /// <summary>1부터 증가하는 라인 id (검증 로그용).</summary>
    public int id;
    /// <summary>FadeIn, FadeOut, Pause, Narration, Dialogue.</summary>
    public string type;
    /// <summary>표시/페이드 지속 시간(초).</summary>
    public float duration;
    /// <summary>나레이션·대사 본문.</summary>
    public string text;
    /// <summary>Dialogue일 때 화자명.</summary>
    public string speaker;
}
