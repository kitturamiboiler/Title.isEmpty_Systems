using System.Collections;
using UnityEngine;

/// <summary>
/// 분식집 구간 트리거: 플레이어·NPC 정지 → 전용 카메라 리그 활성화 → 오디오 페이드 → 해제 시 입력 축 리셋.
/// <see cref="OpeningEventController"/> 오프닝과 같이 이동·블링크·패리 게이트를 맞춘다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SnackShopStoryController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerMovement2D _playerMovement;
    [SerializeField] private PlayerBlinkController2D _playerBlink;
    [SerializeField] private PlayerParryController2D _playerParry;
    [Tooltip("연서 등 NPC 추적. 비우면 생략. 시퀀스 시작 시 StopFollowing으로 즉시 정지( Dynamic 시 linearVelocity 0 ).")]
    [SerializeField] private NPCFollow2D _npcFollow;
    [Tooltip("줌/구도용 카메라 리그(자식에 Camera 등). 씬에서 기본 비활성 두고, 연출 중에만 켠다. 자식 Camera에 Audio Listener를 두지 말 것 — Main Camera만 리스너 유지.")]
    [SerializeField] private GameObject _snackShopCamObject;
    [SerializeField] private AudioSource _rainSfx;
    [SerializeField] private AudioSource _pianoBgm;

    [Header("Audio targets (플랜: 빗 70% / 피아노 풀)")]
    [SerializeField] [Range(0f, 1f)] private float _rainVolumeTarget = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float _pianoVolumeTarget = 1f;

    [Header("Timing")]
    [SerializeField] private float _audioFadeDuration = 1.5f;

    [Header("Snack dialogue (JSON slice)")]
    [Tooltip("Chapter1_Opening.json 등 StoryJsonManager와 동일 키.")]
    [SerializeField] private StoryKey _storyKey = StoryKey.Chapter1_Opening;
    [Tooltip("분식집 구간 라인 슬라이스 (0 기준, JSON과 StoryDatabase 순서 동일).")]
    [SerializeField] private int _lineIndexStart = 41;
    [SerializeField] private int _lineIndexEnd = 50;

    private bool _sequenceStarted;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[SnackShopStoryController] Collider2D는 Is Trigger 권장 — {gameObject.name}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_sequenceStarted || other == null)
            return;
        if (!IsPlayerCollider(other))
            return;

        _sequenceStarted = true;
        StartCoroutine(SnackShopSequence());
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (Layers.Player >= 0 && other.gameObject.layer == Layers.Player)
            return true;
        return other.GetComponent<PlayerMovement2D>() != null;
    }

    private IEnumerator SnackShopSequence()
    {
        if (_playerMovement == null || _playerBlink == null)
        {
            Debug.LogError($"[SnackShopStoryController] PlayerMovement2D / PlayerBlinkController2D 필수 — {gameObject.name}");
            yield break;
        }

        if (_snackShopCamObject == null)
        {
            Debug.LogError($"[SnackShopStoryController] snackShopCamObject 미할당 — {gameObject.name}");
            yield break;
        }

        // 1. Anticipation: 플레이어 입력·이동 차단(현 정지), 블링크/패리 동기 잠금
        SetPlayerStoryLocked(true);
        // 2. 연서 추적 중단 + RB 정지 (NPCFollow2D.StopFollowing → Dynamic이면 linearVelocity = 0)
        if (_npcFollow != null)
            _npcFollow.StopFollowing();

        // 3. 분식집 카메라 리그
        _snackShopCamObject.SetActive(true);

        if (_rainSfx != null)
            StartCoroutine(FadeAudio(_rainSfx, _rainVolumeTarget, _audioFadeDuration));
        if (_pianoBgm != null)
            StartCoroutine(FadeAudio(_pianoBgm, _pianoVolumeTarget, _audioFadeDuration));

        yield return SnackShopDialogueFromJson();

        _snackShopCamObject.SetActive(false);
        SetPlayerStoryLocked(false);
        Input.ResetInputAxes();
    }

    private IEnumerator SnackShopDialogueFromJson()
    {
        if (!StoryJsonManager.TryLoadCutsceneLines(_storyKey, out var lines) || lines == null || lines.Count == 0)
        {
            if (CombatDialogueUI.Instance != null)
            {
                CombatDialogueUI.Instance.Show(
                    "",
                    "[데이터 없음] 스토리 JSON을 불러오지 못했습니다.",
                    3f,
                    CombatDialogueUI.DialoguePriority.Story);
                yield return CombatDialogueUI.Instance.WaitUntilNotShowing();
            }
            else
                yield return new WaitForSeconds(3f);
            yield break;
        }

        int start = Mathf.Clamp(_lineIndexStart, 0, lines.Count - 1);
        int end = Mathf.Clamp(_lineIndexEnd, start, lines.Count - 1);

        for (int i = start; i <= end; i++)
        {
            var line = lines[i];
            switch (line.type)
            {
                case CutscenePlayer.CutsceneLineType.Narration:
                    if (CombatDialogueUI.Instance != null)
                    {
                        if (line.lineId == StoryJsonManager.Chapter1OnomatopoeiaLineId)
                            CombatDialogueUI.Instance.ShowAmbientOnomatopoeia(
                                line.text,
                                line.duration,
                                CombatDialogueUI.DialoguePriority.Story);
                        else
                            CombatDialogueUI.Instance.Show(
                                "",
                                line.text,
                                line.duration,
                                CombatDialogueUI.DialoguePriority.Story);
                        yield return CombatDialogueUI.Instance.WaitUntilNotShowing();
                    }
                    else
                        yield return new WaitForSeconds(line.duration);
                    break;

                case CutscenePlayer.CutsceneLineType.Dialogue:
                    if (CombatDialogueUI.Instance != null)
                    {
                        CombatDialogueUI.Instance.Show(
                            line.speaker,
                            line.text,
                            line.duration,
                            CombatDialogueUI.DialoguePriority.Story);
                        yield return CombatDialogueUI.Instance.WaitUntilNotShowing();
                    }
                    else
                        yield return new WaitForSeconds(line.duration);
                    break;

                case CutscenePlayer.CutsceneLineType.Pause:
                    yield return new WaitForSeconds(line.duration);
                    break;

                case CutscenePlayer.CutsceneLineType.FadeIn:
                case CutscenePlayer.CutsceneLineType.FadeOut:
                    yield return new WaitForSeconds(line.duration);
                    break;

                default:
                    yield return null;
                    break;
            }
        }
    }

    private void SetPlayerStoryLocked(bool locked)
    {
        _playerMovement.SetStoryInputLocked(locked);
        _playerBlink.SetStoryInputLocked(locked);
        if (_playerParry != null)
            _playerParry.SetStoryInputLocked(locked);
    }

    private static IEnumerator FadeAudio(AudioSource source, float targetVol, float duration)
    {
        if (source == null)
            yield break;

        float startVol = source.volume;
        duration = Mathf.Max(0.0001f, duration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(startVol, targetVol, t / duration);
            yield return null;
        }

        source.volume = targetVol;
    }
}
