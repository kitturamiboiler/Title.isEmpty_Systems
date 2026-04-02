using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CutscenePlayer;
using static EndingChoiceUI;

/// <summary>
/// 설계자 사망 후 스폰되는 USB 아이템 — 엔딩 분기 오케스트레이터.
///
/// 흐름:
///   1. 스폰 즉시 카메라 포커스 (강제 시선 유도 — 가설 3)
///   2. 공통 프리 컷씬 재생 (우산 꽂이, 9년 전 회상, 빗속 현과 연서)
///   3. [선택] UI 표시: [연서가 막는다] / [연서가 누른다]
///   4. 선택에 따라 엔딩 A 또는 B 컷씬 재생
///   5. 페이드 아웃 → 크레딧 씬 (TODO)
///
/// 가설 3 방어:
///   카메라가 USB로 포커스하고, 플레이어는 자동으로 컷씬에 진입.
///   플레이어 이동을 컷씬 중 차단하여 엔딩 이탈 불가.
/// </summary>
public class EndingUSBItem : MonoBehaviour
{
    [Header("USB — Refs")]
    [SerializeField] private CutscenePlayer  _cutscenePlayer;
    [SerializeField] private EndingChoiceUI  _choiceUI;

    [Header("USB — Camera Focus")]
    [SerializeField] private float   _cameraFocusDuration = 1.8f;
    [SerializeField] private Vector2 _cameraOffset        = new Vector2(0f, 2f);
    [SerializeField] private float   _cameraLerpSpeed     = 3f;

    [Header("USB — Pickup")]
    [SerializeField] private float _pickupRadius = 3f;

    [Header("Credits Scene")]
    [SerializeField] private string _creditsSceneName = "Credits";

    public System.Action<EndingChoice> OnEndingChosen;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private bool    _hasStarted;
    private Camera  _mainCamera;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Start()
    {
        _mainCamera = Camera.main;
        StartCoroutine(FocusThenBeginCutscene());

        if (_choiceUI != null)
            _choiceUI.OnChoiceSelected += OnChoiceMade;
    }

    private void OnDestroy()
    {
        if (_choiceUI != null)
            _choiceUI.OnChoiceSelected -= OnChoiceMade;
    }

    // ─── 카메라 포커스 + 컷씬 진입 ───────────────────────────────────────────

    private IEnumerator FocusThenBeginCutscene()
    {
        // 가설 3: 강제 시선 유도
        if (_mainCamera != null)
        {
            Vector3 target = (Vector2)transform.position + _cameraOffset;
            target.z       = _mainCamera.transform.position.z;
            float elapsed  = 0f;

            while (elapsed < _cameraFocusDuration)
            {
                _mainCamera.transform.position = Vector3.Lerp(
                    _mainCamera.transform.position, target, Time.deltaTime * _cameraLerpSpeed);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 공통 프리 컷씬 재생 후 선택지 표시
        if (_cutscenePlayer != null)
            _cutscenePlayer.Play(BuildPreChoiceLines(), OnPreCutsceneComplete);
        else
            OnPreCutsceneComplete(); // CutscenePlayer 없으면 바로 선택으로
    }

    private void OnPreCutsceneComplete()
    {
        _choiceUI?.Show();
    }

    private void OnChoiceMade(EndingChoice choice)
    {
        OnEndingChosen?.Invoke(choice);

        if (_cutscenePlayer == null)
        {
            Debug.LogWarning("[EndingUSBItem] CutscenePlayer가 없습니다. 엔딩 씬으로 직행.");
            StartCoroutine(LoadCreditsAfterDelay(1f));
            return;
        }

        var lines = choice == EndingChoice.A_YeonsseePrevents
                    ? BuildEndingALines()
                    : BuildEndingBLines();

        _cutscenePlayer.Play(lines, () => StartCoroutine(LoadCreditsAfterDelay(1f)));
    }

    // ─── 컷씬 라인 데이터 ─────────────────────────────────────────────────────

    /// <summary>
    /// 공통 프리 컷씬:
    /// USB 경고문 → 현의 망설임 → 우산꽂이 → 9년 전 회상 → 현재
    /// </summary>
    private static List<CutsceneLine> BuildPreChoiceLines() => new List<CutsceneLine>
    {
        new CutsceneLine { type = CutsceneLineType.FadeIn,    duration  = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text = "*셧다운 프로토콜. 복종 회로 전체 비활성화.\n경고: 대상자 사이버네틱 전체 정지 포함.*",
            duration = 3.5f },

        new CutsceneLine { type = CutsceneLineType.Pause,     duration  = 0.6f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현이 손을 들었다.\nUSB를 봤다.",
            duration = 2.2f },

        new CutsceneLine { type = CutsceneLineType.Pause,     duration  = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "우산꽂이가 보였다.\n옥상 입구 옆이었다.",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "설계자가 가져간 검은 우산이 없었다.\n빨간 우산이 하나 남아있었다.",
            duration = 3.0f },

        new CutsceneLine { type = CutsceneLineType.Pause,     duration  = 0.8f },

        // ── 9년 전 회상 ──────────────────────────────────────────────────────
        new CutsceneLine { type = CutsceneLineType.FadeOut,   duration  = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "9년 전이었다.\n학교 앞이었다.",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "저벅. 저벅.",
            duration = 1.5f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "현",
            text     = "\"여기서 뭐해. 집에 가자.\"",
            duration = 2.8f },

        new CutsceneLine { type = CutsceneLineType.FadeOut,   duration  = 0.5f },

        // ── 현재 ─────────────────────────────────────────────────────────────
        new CutsceneLine { type = CutsceneLineType.FadeIn,    duration  = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현이 우산꽂이 쪽으로 걸었다.\n빨간 우산을 집었다.",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Pause,     duration  = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 있었다.\n옥상 입구에 서 있었다. 굳어진 얼굴이었다.\n앰버빛 눈이 현을 보고 있었다.",
            duration = 3.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "우산을 봤다.\n현의 얼굴을 봤다.",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Pause,     duration  = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현이 연서에게 걸어갔다.\n우산을 연서 위에 들었다.\n자신은 비를 맞으며.",
            duration = 3.2f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "현",
            text     = "\"미안해.\"",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "빗소리가 들렸다.",
            duration = 1.8f },

        new CutsceneLine { type = CutsceneLineType.Pause,     duration  = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "현",
            text     = "\"이제 절대 놓치지 않을게.\"",
            duration = 3.0f },

        new CutsceneLine { type = CutsceneLineType.Pause,     duration  = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 USB를 봤다.\n현의 손에 들린 것을.",
            duration = 2.5f },

        // → 선택지 표시
    };

    /// <summary>Ending A — 연서가 막는다 (희망 엔딩)</summary>
    private static List<CutsceneLine> BuildEndingALines() => new List<CutsceneLine>
    {
        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.4f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 걸어왔다.\n현 앞에 섰다.",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"누르지 마요.\"",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"그거 누르면. 오빠도 꺼지잖아요.\"",
            duration = 3.2f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 1.0f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현이 아무 말 하지 않았다.",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 현의 손에서 USB를 꺼냈다.\n주머니에 넣었다.",
            duration = 3.0f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "침묵이었다.\n빗소리가 들렸다.",
            duration = 2.2f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서의 손이 우산 살을 잡았다.\n천천히.\n현 쪽으로 밀었다.",
            duration = 3.2f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"그때. 내가 뭐라고 했는지 알아요?\"",
            duration = 3.0f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"…반만요.\"",
            duration = 2.8f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 우산을 당겼다.\n현 쪽으로.",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "둘이 서 있었다.\n우산 아래.\n반씩 비를 맞으며.",
            duration = 3.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "빗물이 고였다.\n그래도 서 있었다.",
            duration = 2.8f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"현아.\"",
            duration = 2.2f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "처음으로.\n이름을 불렀다.",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현은 아무 말 하지 않았다.\n우산을 들고 있었다.\n비를 맞고 있었다.",
            duration = 3.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "그게 전부였다.\n그게 전부이기에 충분했다.",
            duration = 3.5f },

        new CutsceneLine { type = CutsceneLineType.FadeOut, duration = 1.5f },
    };

    /// <summary>Ending B — 연서가 누른다 (비극 엔딩)</summary>
    private static List<CutsceneLine> BuildEndingBLines() => new List<CutsceneLine>
    {
        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.4f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 걸어왔다.\n현 앞에 섰다.",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"오빠.\"",
            duration = 1.8f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"회로가 한 거라는 거 알아요.\"",
            duration = 2.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현은 연서를 봤다.\n연서가 현을 봤다.",
            duration = 2.2f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"오빠가 나쁜 사람이 아닌 것도.\"",
            duration = 2.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "목소리가 흔들렸다.",
            duration = 1.5f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"근데. 엄마 아빠 생각하면.\"",
            duration = 3.0f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 1.2f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 손을 뻗었다.\nUSB를 잡았다.\n현이 놓지 않았다.",
            duration = 3.2f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"오빠 얼굴을 못 보겠어요.\"",
            duration = 3.0f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서의 눈이 흔들렸다.\n굳어진 얼굴이었는데.\n처음으로.\n흔들렸다.",
            duration = 3.8f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.6f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현이 USB를 놓았다.",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 USB를 봤다.\n한동안 봤다.",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"잘가요..\"",
            duration = 2.2f },

        new CutsceneLine { type = CutsceneLineType.Dialogue,
            speaker  = "연서",
            text     = "\"오빠.\"",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "눌렀다.",
            duration = 1.5f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "현이 멈췄다.\n손이 내려갔다.\n우산이 기울었다.",
            duration = 3.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "빗속에서 천천히.\n기울었다.",
            duration = 2.8f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 현을 봤다.\n현의 눈이 감겼다.",
            duration = 2.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "우산이 바닥에 떨어졌다.\n빨간 우산이었다.\n펼쳐진 채로 바닥에 있었다.",
            duration = 3.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "빗물이 고였다.",
            duration = 2.0f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 1.2f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 우산을 봤다.\n바닥에 펼쳐진.\n아무도 가리지 않는.",
            duration = 3.8f },

        new CutsceneLine { type = CutsceneLineType.Pause, duration = 0.8f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "연서가 손을 뻗었다.\n우산을 집었다.",
            duration = 2.5f },

        new CutsceneLine { type = CutsceneLineType.Narration,
            text     = "들지 않았다.\n그냥 쥐고 있었다.\n빗속에서.\n혼자.",
            duration = 4.5f },

        new CutsceneLine { type = CutsceneLineType.FadeOut, duration = 2.0f },
    };

    // ─── 크레딧 전환 ──────────────────────────────────────────────────────────

    private IEnumerator LoadCreditsAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        // TODO: UnityEngine.SceneManagement.SceneManager.LoadScene(_creditsSceneName) — 2026-04-02
        Debug.LogWarning($"[EndingUSBItem] 크레딧 씬 '{_creditsSceneName}' 로드 (씬 추가 후 활성화 필요).");
    }

    // ─── Editor 기즈모 ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _pickupRadius);
    }
}
