using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 공간 기반 1회성 스토리 트리거. 플레이어 진입 시 이벤트 발행 후 Collider 즉시 비활성화로 중복 발동 차단.
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
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed || other == null)
            return;
        if (!other.CompareTag("Player"))
            return;

        _consumed = true;
        if (_triggerCollider != null)
            _triggerCollider.enabled = false;

        _onPlayerEntered?.Invoke();
    }
}
