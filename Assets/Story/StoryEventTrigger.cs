using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 공간 기반 1회성 스토리 트리거. 플레이어 진입 시 이벤트 발행 후 Collider 즉시 비활성화로 중복 발동 차단.
///
/// 물리 전제: <c>OnTriggerEnter2D</c>는 이 오브젝트 또는 진입 오브젝트 중 하나에 <c>Rigidbody2D</c>가 있어야 발생한다.
/// 플레이어에 <c>Rigidbody2D</c>가 없으면 <c>Awake</c> 경고를 참고해 수동으로 확인할 것.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StoryEventTrigger : MonoBehaviour
{
    [SerializeField]
    [Tooltip("플레이어(Tag=Player) 최초 진입 시 1회 호출.")]
    private UnityEvent _onPlayerEntered;

    Collider2D _triggerCollider;
    bool _consumed;

    void Awake()
    {
        _triggerCollider = GetComponent<Collider2D>();
        if (_triggerCollider != null && !_triggerCollider.isTrigger)
            Debug.LogWarning($"[StoryEventTrigger] Collider2D는 Is Trigger 권장 — {gameObject.name}");

        // 이 오브젝트에 RB가 없으면 상대방(플레이어)에 있어야 OnTriggerEnter2D가 발생함
        if (GetComponent<Rigidbody2D>() == null)
            Debug.LogWarning($"[StoryEventTrigger] 이 오브젝트에 Rigidbody2D 없음 — 플레이어 쪽에 RB가 있어야 트리거 발생. ({gameObject.name})");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed || other == null)
            return;

        if (!IsPlayer(other))
            return;

        _consumed = true;
        if (_triggerCollider != null)
            _triggerCollider.enabled = false;

        _onPlayerEntered?.Invoke();
    }

    /// <summary>
    /// Tag·Layer 이중 검증. Tag가 "Player"로 다르거나 없는 경우 Layer로 보조 판별.
    /// </summary>
    static bool IsPlayer(Collider2D col)
    {
        if (col.CompareTag("Player"))
            return true;

        int playerLayer = Layers.Player;
        if (playerLayer >= 0 && col.gameObject.layer == playerLayer)
        {
            Debug.LogWarning("[StoryEventTrigger] Tag가 'Player' 아니지만 Layer=Player로 통과 — Tag 설정 확인 권장.");
            return true;
        }

        return false;
    }
}
