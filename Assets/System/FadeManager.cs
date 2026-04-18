using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// 전역 화면 암전(검정 오버레이). 오프닝 1장 ID 53 FadeIn 종료 시 <see cref="CutscenePlayer"/>에서 호출.
/// 씬에 Canvas 하위에 전체 화면 Image + <see cref="CanvasGroup"/>을 두고 인스펙터로 연결한다.
/// </summary>
public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [Header("Overlay")]
    [Tooltip("전체 화면 검정. alpha 0=투명, 1=완전 암전.")]
    [SerializeField] private CanvasGroup _fullScreenBlack;

    [Header("Opening — 1장 ID 53 암전")]
    [Tooltip("라인 duration이 0에 가까울 때 사용할 암전 시간(초). JSON ID 53은 보통 0.9.")]
    [SerializeField] private float _defaultOpeningFadeToBlackDuration = 0.9f;

    [Header("Opening — 종료")]
    [Tooltip("암전 완료 시 — 씬 전환·입력 영구 차단 유지 등.")]
    [FormerlySerializedAs("_onOpeningChapterSequenceFinalized")]
    [SerializeField] private UnityEvent _onFinalizeOpening;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_fullScreenBlack != null)
        {
            _fullScreenBlack.alpha           = 0f;
            _fullScreenBlack.blocksRaycasts  = false;
            _fullScreenBlack.interactable    = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>1장 오프닝 시퀀스 안전 종료 — 인스펙터 이벤트와 동일.</summary>
    public void FinalizeOpening()
    {
        _onFinalizeOpening?.Invoke();
    }

    /// <summary>알파를 1로 올려 암전한 뒤 <see cref="FinalizeOpening"/> 호출.</summary>
    public IEnumerator FadeToBlackAndFinalizeOpening(float duration)
    {
        if (_fullScreenBlack == null)
        {
            Debug.LogError($"[FadeManager] fullScreenBlack 미할당 — {gameObject.name}");
            FinalizeOpening();
            yield break;
        }

        float useDuration = duration > 0.01f ? duration : _defaultOpeningFadeToBlackDuration;
        useDuration = Mathf.Max(0.01f, useDuration);

        _fullScreenBlack.gameObject.SetActive(true);
        _fullScreenBlack.blocksRaycasts = true;
        _fullScreenBlack.interactable   = true;

        float from = _fullScreenBlack.alpha;
        float e    = 0f;
        while (e < useDuration)
        {
            e += Time.unscaledDeltaTime;
            _fullScreenBlack.alpha = Mathf.Lerp(from, 1f, e / useDuration);
            yield return null;
        }

        _fullScreenBlack.alpha = 1f;
        FinalizeOpening();
    }
}
