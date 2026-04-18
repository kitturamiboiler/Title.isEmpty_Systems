using System.Collections;
using UnityEngine;

/// <summary>
/// 오프닝 구간 등 트리거 진입 시 연출 순서를 한 번에 실행하는 마스터 컨트롤러.
/// 우산 펼침은 Bottom Pivot 전제로 Scale Y 스미어 + VisualRoot Y 바운스 + 팔로우스루를 수행한다.
/// 우산 스프라이트: **hold PPU 16**, **open(pull) PPU 8**. 미세 Y는 해당 그리드의 1픽셀(1/PPU) 이상을 권장.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class OpeningEventController : MonoBehaviour
{
    private const float SmearScaleYStart = 0.1f;
    private const float SmearScaleYPeak = 1.2f;

    /// <summary><c>umbrella_hold</c> 등 PPU 16 스프라이트 1픽셀 = 1/16 월드 유닛.</summary>
    public const float OnePixelWorldUnitUmbrellaHoldPpu16 = 1f / 16f;

    /// <summary>펼친 우산(open / pull) PPU 8 스프라이트 1픽셀 = 1/8 월드 유닛.</summary>
    public const float OnePixelWorldUnitUmbrellaPullPpu8 = 1f / 8f;

    [Header("Dependencies")]
    [SerializeField] private PlayerMovement2D _playerMovement;
    [SerializeField] private PlayerBlinkController2D _playerBlink;
    [SerializeField] private PlayerParryController2D _playerParry;
    [SerializeField] private NPCFollow2D _npc;
    [SerializeField] private GameObject _storyGate;

    [Header("Umbrella — Squash & Stretch (Bottom pivot 전제)")]
    [Tooltip("선행 동작용. 비우면 오픈 스프라이트만 사용해 스킵.")]
    [SerializeField] private Sprite _spriteUmbrellaHold;
    [SerializeField] private Sprite _spriteUmbrellaOpen;
    [Tooltip("비우면 PlayerBlinkController2D.PlayerSpriteRenderer. 우산 전용 자식 SR 권장.")]
    [SerializeField] private SpriteRenderer _umbrellaSpriteRenderer;
    [Tooltip("비우면 PlayerBlink StoryVisualRootTransform.")]
    [SerializeField] private Transform _visualRoot;
    [Tooltip("Scale Y 스미어(0.1→1.2). 비우면 VisualRoot와 동일 — 바운스와 겹치므로 자식 분리 권장.")]
    [SerializeField] private Transform _umbrellaStretchRoot;
    [Tooltip("연출 중 Transform을 덮어쓰는 Animator가 있으면 할당(연출 구간 speed=0). 없으면 None — null 가드로 동작 유지.")]
    [SerializeField] private Animator _storyVisualAnimator;

    [Header("Umbrella — Timing")]
    [SerializeField] private float _anticipationHoldDuration = 0.2f;
    [SerializeField] private float _smearDuration = 0.05f;
    [SerializeField] private float _followThroughDuration = 0.1f;
    [Tooltip("정점에서 VisualRoot 로컬 Y. open(pull) PPU8 기준 1px=-0.125, hold PPU16 기준 1px=-0.0625.")]
    [SerializeField] private float _bounceVisualLocalY = -OnePixelWorldUnitUmbrellaPullPpu8;
    [Tooltip("우산 SpriteRenderer 로컬 Z. 머리와 동일 평면 Z-Fighting 완화(연출 종료 후 원복).")]
    [SerializeField] private float _umbrellaSpriteLocalZ = -0.01f;

    [Header("Settings")]
    [SerializeField] private float _followDistance = 1.5f;
    [Tooltip("시퀀스 종료 직후 이동·전투 입력을 다시 연다.")]
    [SerializeField] private bool _restorePlayerControlAfterSequence = true;
    [Tooltip("체크 시 시퀀스 끝에 우산 이전 스프라이트로 되돌린다.")]
    [SerializeField] private bool _restoreDefaultSpriteAfterSequence;

    [Header("Landing (anti air-freeze)")]
    [Tooltip("잠금 전에 바닥에 닿을 때까지 대기.")]
    [SerializeField] private bool _waitForFloorBeforeLock = true;
    [SerializeField] private float _landingWaitTimeoutSeconds = 8f;
    [SerializeField] private bool _snapWorldYOnLandingTimeout = true;
    [SerializeField] private float _forcedGroundWorldY = 1.1f;
    [Tooltip("착지 스냅 직후 1프레임 VisualRoot Y. hold·씬 정렬 PPU16 기준 1px=-0.0625 권장.")]
    [SerializeField] private float _landingSnapMicroBounceLocalY = -OnePixelWorldUnitUmbrellaHoldPpu16;

    private bool _eventTriggered;
    private Rigidbody2D _playerRb;
    private SpriteRenderer _sequenceSpriteRenderer;
    private Sprite _sequenceStartSpriteSnapshot;
    private bool _sequenceUsesBlinkBodySprite;
    private bool _landingSequenceDidSnap;
    private Transform _umbrellaZTransform;
    private float _umbrellaZSaved;
    private bool _umbrellaZModified;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[OpeningEventController] Collider2D는 Is Trigger 권장 — {gameObject.name}");

        if (_playerMovement != null)
            _playerRb = _playerMovement.GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_eventTriggered || other == null)
            return;
        if (!IsPlayerCollider(other))
            return;

        _eventTriggered = true;
        StartCoroutine(StartOpeningSequence());
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (Layers.Player >= 0 && other.gameObject.layer == Layers.Player)
            return true;
        return other.GetComponent<PlayerMovement2D>() != null;
    }

    private IEnumerator StartOpeningSequence()
    {
        if (_playerMovement == null || _playerBlink == null)
        {
            Debug.LogError($"[OpeningEventController] PlayerMovement2D / PlayerBlinkController2D 필수 — {gameObject.name}");
            yield break;
        }

        if (_playerRb == null)
            _playerRb = _playerMovement.GetComponent<Rigidbody2D>();

        _playerMovement.BeginOpeningSequencePhysics(
            OpeningSequencePhysicsDefaults.MoveSpeed,
            OpeningSequencePhysicsDefaults.JumpVelocity,
            OpeningSequencePhysicsDefaults.GroundAcceleration);

        yield return WaitUntilPlayerFlooredOrSnapped();

        if (_landingSequenceDidSnap)
            yield return LandingSnapMicroBounceOneFrame();

        SetPlayerStoryLocked(true);

        yield return PlayUmbrellaSquashStretchSequence();

        if (_storyGate != null)
            _storyGate.SetActive(false);

        if (_npc != null)
            _npc.StartFollowing(_playerMovement.transform, _followDistance, _playerMovement);

        if (_restorePlayerControlAfterSequence)
        {
            SetPlayerStoryLocked(false);
            Input.ResetInputAxes();
        }

        if (_restoreDefaultSpriteAfterSequence)
            RestoreOpeningSpriteSnapshot();
    }

    /// <summary>
    /// 선행(홀드) → 스미어(스프라이트 교체 + Scale Y) → 바운스(VisualRoot Y) → 팔로우스루(원복).
    /// 연출 전체 구간 입력 잠금은 호출부에서 유지한다.
    /// </summary>
    private IEnumerator PlayUmbrellaSquashStretchSequence()
    {
        if (!TryBeginSpriteSequence())
        {
            if (_spriteUmbrellaOpen != null && _playerBlink != null)
                _playerBlink.StoryOpeningSetSprite(_spriteUmbrellaOpen);
            yield break;
        }

        Transform visRoot = ResolveVisualRoot();
        Transform stretchRoot = ResolveStretchRoot();
        if (visRoot == null || stretchRoot == null)
        {
            Debug.LogError($"[OpeningEventController] VisualRoot / StretchRoot를 찾을 수 없습니다 — {gameObject.name}");
            if (_spriteUmbrellaOpen != null)
                OpeningApplySprite(_spriteUmbrellaOpen);
            yield break;
        }

        if (stretchRoot == visRoot)
        {
            Debug.LogWarning(
                $"[OpeningEventController] 우산 StretchRoot와 VisualRoot가 동일합니다. Bottom 피벗 기준 바운스+스케일이 겹칩니다. 자식으로 UmbrellaStretch 분리 권장 — {gameObject.name}");
        }

        BeginUmbrellaSpriteLocalZOffset();

        Vector3 stretchBaseScale = stretchRoot.localScale;
        Vector3 visBaseLocal = visRoot.localPosition;
        float savedAnimSpeed = 1f;
        if (_storyVisualAnimator != null)
        {
            savedAnimSpeed = _storyVisualAnimator.speed;
            _storyVisualAnimator.speed = 0f;
        }

        if (_spriteUmbrellaHold != null)
            OpeningApplySprite(_spriteUmbrellaHold);
        else if (_spriteUmbrellaOpen != null)
            OpeningApplySprite(_spriteUmbrellaOpen);

        float anticipation = Mathf.Max(0f, _anticipationHoldDuration);
        if (anticipation > 0f)
            yield return new WaitForSeconds(anticipation);

        if (_spriteUmbrellaOpen == null)
        {
            Debug.LogWarning($"[OpeningEventController] _spriteUmbrellaOpen 미할당 — 스미어 생략 — {gameObject.name}");
            if (_storyVisualAnimator != null)
                _storyVisualAnimator.speed = savedAnimSpeed;
            EndUmbrellaSpriteLocalZOffset();
            yield break;
        }

        // 스미어: 스프라이트 교체 시점 = Scale 변화 시작과 동일 프레임
        stretchRoot.localScale = new Vector3(stretchBaseScale.x, SmearScaleYStart, stretchBaseScale.z);
        OpeningApplySprite(_spriteUmbrellaOpen);

        float smear = Mathf.Max(0.0001f, _smearDuration);
        float smearElapsed = 0f;
        while (smearElapsed < smear)
        {
            smearElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(smearElapsed / smear);
            float u = Mathf.SmoothStep(0f, 1f, t);
            float sy = Mathf.Lerp(SmearScaleYStart, SmearScaleYPeak, u);
            stretchRoot.localScale = new Vector3(stretchBaseScale.x, sy, stretchBaseScale.z);
            yield return null;
        }

        stretchRoot.localScale = new Vector3(stretchBaseScale.x, SmearScaleYPeak, stretchBaseScale.z);

        // 무게 중심: 정점에서 VisualRoot Y(기본 1px @ open/pull PPU8)
        Vector3 bounceLocal = visBaseLocal + new Vector3(0f, _bounceVisualLocalY, 0f);
        visRoot.localPosition = bounceLocal;

        float follow = Mathf.Max(0.0001f, _followThroughDuration);
        float followElapsed = 0f;
        while (followElapsed < follow)
        {
            followElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(followElapsed / follow);
            float sy = Mathf.Lerp(SmearScaleYPeak, stretchBaseScale.y, u);
            stretchRoot.localScale = new Vector3(stretchBaseScale.x, sy, stretchBaseScale.z);
            visRoot.localPosition = Vector3.Lerp(bounceLocal, visBaseLocal, u);
            yield return null;
        }

        stretchRoot.localScale = stretchBaseScale;
        visRoot.localPosition = visBaseLocal;

        if (_storyVisualAnimator != null)
            _storyVisualAnimator.speed = savedAnimSpeed;

        EndUmbrellaSpriteLocalZOffset();
    }

    private IEnumerator LandingSnapMicroBounceOneFrame()
    {
        Transform vis = ResolveVisualRoot();
        if (vis == null)
            yield break;

        Vector3 baseLocal = vis.localPosition;
        vis.localPosition = baseLocal + new Vector3(0f, _landingSnapMicroBounceLocalY, 0f);
        yield return null;
        vis.localPosition = baseLocal;
    }

    private void BeginUmbrellaSpriteLocalZOffset()
    {
        if (_sequenceSpriteRenderer == null)
            return;

        _umbrellaZTransform = _sequenceSpriteRenderer.transform;
        _umbrellaZSaved = _umbrellaZTransform.localPosition.z;
        Vector3 p = _umbrellaZTransform.localPosition;
        p.z = _umbrellaSpriteLocalZ;
        _umbrellaZTransform.localPosition = p;
        _umbrellaZModified = true;
    }

    private void EndUmbrellaSpriteLocalZOffset()
    {
        if (!_umbrellaZModified || _umbrellaZTransform == null)
            return;

        Vector3 p = _umbrellaZTransform.localPosition;
        p.z = _umbrellaZSaved;
        _umbrellaZTransform.localPosition = p;
        _umbrellaZModified = false;
        _umbrellaZTransform = null;
    }

    private Transform ResolveVisualRoot()
    {
        if (_visualRoot != null)
            return _visualRoot;
        return _playerBlink != null ? _playerBlink.StoryVisualRootTransform : null;
    }

    private Transform ResolveStretchRoot()
    {
        if (_umbrellaStretchRoot != null)
            return _umbrellaStretchRoot;
        return ResolveVisualRoot();
    }

    private bool TryBeginSpriteSequence()
    {
        _sequenceSpriteRenderer = _umbrellaSpriteRenderer != null
            ? _umbrellaSpriteRenderer
            : _playerBlink != null ? _playerBlink.PlayerSpriteRenderer : null;

        if (_sequenceSpriteRenderer == null)
        {
            Debug.LogError($"[OpeningEventController] 우산용 SpriteRenderer를 찾을 수 없습니다 — {gameObject.name}");
            return false;
        }

        _sequenceStartSpriteSnapshot = _sequenceSpriteRenderer.sprite;
        _sequenceUsesBlinkBodySprite = _playerBlink != null && _sequenceSpriteRenderer == _playerBlink.PlayerSpriteRenderer;
        return true;
    }

    private void OpeningApplySprite(Sprite sprite)
    {
        if (sprite == null || _sequenceSpriteRenderer == null)
            return;

        if (_sequenceUsesBlinkBodySprite)
            _playerBlink.StoryOpeningSetSprite(sprite);
        else
            _sequenceSpriteRenderer.sprite = sprite;
    }

    private void RestoreOpeningSpriteSnapshot()
    {
        if (_sequenceUsesBlinkBodySprite)
            _playerBlink.RestoreSpriteAfterStory();
        else if (_sequenceSpriteRenderer != null)
            _sequenceSpriteRenderer.sprite = _sequenceStartSpriteSnapshot;
    }

    private IEnumerator WaitUntilPlayerFlooredOrSnapped()
    {
        _landingSequenceDidSnap = false;

        if (!_waitForFloorBeforeLock)
            yield break;

        float elapsed = 0f;
        while (elapsed < _landingWaitTimeoutSeconds)
        {
            if (_playerMovement.IsFloorGrounded())
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!_snapWorldYOnLandingTimeout)
        {
            Debug.LogWarning($"[OpeningEventController] 착지 대기 타임아웃 — 공중에서 잠금이 걸립니다 — {gameObject.name}");
            yield break;
        }

        Vector3 p = _playerMovement.transform.position;
        p.y = _forcedGroundWorldY;
        _playerMovement.transform.position = p;
        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector2.zero;
            _playerRb.angularVelocity = 0f;
        }

        _landingSequenceDidSnap = true;
        Debug.LogWarning($"[OpeningEventController] 착지 타임아웃 → Y={_forcedGroundWorldY} 스냅 — {gameObject.name}");
    }

    private void SetPlayerStoryLocked(bool locked)
    {
        _playerMovement.SetStoryInputLocked(locked);
        _playerBlink.SetStoryInputLocked(locked);
        if (_playerParry != null)
            _playerParry.SetStoryInputLocked(locked);
    }
}
