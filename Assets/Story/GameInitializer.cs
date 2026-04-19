using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CutscenePlayer;

/// <summary>
/// 씬 시작 시 블로킹 컷신을 지정 lineId까지 재생 후 <see cref="CutscenePlayer.CutsceneState.WaitingForTrigger"/>로 멈춘다.
/// JSON은 <see cref="ChapterStoryLoader"/>와 동일 규칙으로 로드한다.
/// </summary>
[DefaultExecutionOrder(50)]
public class GameInitializer : MonoBehaviour
{
    [Header("선행 동작 · 초기화 순서")]
    [Tooltip("다른 컴포넌트 Awake/Start 후 한 프레임 양보 — CutscenePlayer 등 참조 Null 완화.")]
    [SerializeField] private bool _waitOneFrameBeforePlay = true;
    [Tooltip("첫 줄 FadeIn 등 반영 전 카메라·캔버스 안정화 유예(실시간 초). 0이면 생략.")]
    [SerializeField] private float _prePlayAnticipationSeconds = 0.15f;

    [Header("재생 대상")]
    [SerializeField] private CutscenePlayer _cutscenePlayer;

    [Tooltip("기본: Chapter1_Opening.json")]
    [SerializeField] private StoryKey _storyKey = StoryKey.Chapter1_Opening;

    [Tooltip("이 lineId 라인까지 연출 완료 후 대기 (예: 연서 조우 전 Pause = 22).")]
    [SerializeField] private int _playUntilLineId = 22;

    Coroutine _bootRoutine;

    void Start()
    {
        if (_bootRoutine != null)
            StopCoroutine(_bootRoutine);
        _bootRoutine = StartCoroutine(BootRoutine());
    }

    void OnDisable()
    {
        if (_bootRoutine != null)
        {
            StopCoroutine(_bootRoutine);
            _bootRoutine = null;
        }
    }

    IEnumerator BootRoutine()
    {
        if (_waitOneFrameBeforePlay)
            yield return null;

        if (_prePlayAnticipationSeconds > 0.0001f)
            yield return new WaitForSecondsRealtime(_prePlayAnticipationSeconds);

        ResolveAndPlay();
        _bootRoutine = null;
    }

    /// <summary>
    /// UnityEvent 등에서 매개변수 없이 호출할 때 사용 (<c>PlayUntil(lines, id)</c>은 라인 목록이 필요함).
    /// </summary>
    public void ResolveAndPlay()
    {
        if (_cutscenePlayer == null)
        {
            Debug.LogError($"[GameInitializer] CutscenePlayer 미할당 — {gameObject.name}");
            return;
        }

        List<CutsceneLine> lines = ResolveLines(_storyKey);
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning($"[GameInitializer] 스토리 라인 없음 — {_storyKey}, {gameObject.name}");
            return;
        }

        _cutscenePlayer.PlayUntil(lines, _playUntilLineId, null);
    }

    /// <summary><see cref="ChapterStoryLoader"/>와 동일한 로드 분기.</summary>
    static List<CutsceneLine> ResolveLines(StoryKey key)
    {
        switch (key)
        {
            case StoryKey.Chapter1_Opening:
                if (StoryJsonManager.TryLoadCutsceneLines(StoryKey.Chapter1_Opening, out var ch1Json) &&
                    ch1Json != null &&
                    ch1Json.Count > 0)
                    return ch1Json;
                Debug.LogWarning("[GameInitializer] Chapter1_Opening JSON 실패 — StoryDatabase 폴백(앞 53줄).");
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
                Debug.LogWarning($"[GameInitializer] StoryKey 미지원 또는 None — {key}");
                return null;
        }
    }
}
