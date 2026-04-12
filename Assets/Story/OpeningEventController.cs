using System.Collections;
using UnityEngine;

/// <summary>
/// 오프닝 구간 등 트리거 진입 시 연출 순서를 한 번에 실행하는 마스터 컨트롤러.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class OpeningEventController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerMovement2D _playerMovement;
    [SerializeField] private PlayerBlinkController2D _playerBlink;
    [SerializeField] private PlayerParryController2D _playerParry;
    [SerializeField] private NPCFollow2D _npc;
    [SerializeField] private GameObject _storyGate;

    [Header("Settings")]
    [SerializeField] private float _umbrellaOpenDelay = 0.5f;
    [SerializeField] private float _followDistance = 1.5f;
    [Tooltip("시퀀스 종료 직후 이동·전투 입력을 다시 연다.")]
    [SerializeField] private bool _restorePlayerControlAfterSequence = true;
    [Tooltip("체크 시 시퀀스 끝에 우산 스프라이트를 원래로 되돌린다(기본은 우산 유지).")]
    [SerializeField] private bool _restoreDefaultSpriteAfterSequence;

    [Header("Landing (anti air-freeze)")]
    [Tooltip("잠금 전에 바닥에 닿을 때까지 대기. 공중에서 gravityScale=0 되는 붕 뜸 방지.")]
    [SerializeField] private bool _waitForFloorBeforeLock = true;
    [SerializeField] private float _landingWaitTimeoutSeconds = 8f;
    [Tooltip("타임아웃 시 플레이어 월드 Y를 이 값으로 스냅(그레이박스 바닥 등). 끄면 스냅 안 함.")]
    [SerializeField] private bool _snapWorldYOnLandingTimeout = true;
    [SerializeField] private float _forcedGroundWorldY = 1.1f;

    private bool _eventTriggered;
    private Rigidbody2D _playerRb;

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
        StartCoroutine(RunOpeningSequence());
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (Layers.Player >= 0 && other.gameObject.layer == Layers.Player)
            return true;
        return other.GetComponent<PlayerMovement2D>() != null;
    }

    private IEnumerator RunOpeningSequence()
    {
        if (_playerMovement == null || _playerBlink == null)
        {
            Debug.LogError($"[OpeningEventController] PlayerMovement2D / PlayerBlinkController2D 필수 — {gameObject.name}");
            yield break;
        }

        if (_playerRb == null)
            _playerRb = _playerMovement.GetComponent<Rigidbody2D>();

        yield return WaitUntilPlayerFlooredOrSnapped();

        SetPlayerStoryLocked(true);

        yield return new WaitForSeconds(_umbrellaOpenDelay);
        _playerBlink.ChangeToUmbrellaSprite();

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
            _playerBlink.RestoreSpriteAfterStory();
    }

    private IEnumerator WaitUntilPlayerFlooredOrSnapped()
    {
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
            Debug.LogWarning($"[OpeningEventController] 착지 대기 타임아웃 — 공중에서 잠금이 걸립니다. 트리거 위치를 조정하거나 스냅을 켜세요 — {gameObject.name}");
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
