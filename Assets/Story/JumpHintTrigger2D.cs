using UnityEngine;

/// <summary>
/// 플레이어가 트리거에 진입하면 <see cref="_jumpUi"/> 를 켜고, 점프 입력 또는 트리거 이탈 시 끈다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class JumpHintTrigger2D : MonoBehaviour
{
    [SerializeField] private GameObject _jumpUi;

    private bool _waitingForDismiss;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[JumpHintTrigger2D] Collider2D는 Is Trigger 권장 — {gameObject.name}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;
        if (!IsPlayerCollider(other))
            return;
        if (_jumpUi == null)
        {
            Debug.LogError($"[JumpHintTrigger2D] Jump UI 미할당 — {gameObject.name}");
            return;
        }

        _jumpUi.SetActive(true);
        _waitingForDismiss = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !IsPlayerCollider(other))
            return;
        if (!_waitingForDismiss || _jumpUi == null)
            return;

        _jumpUi.SetActive(false);
        _waitingForDismiss = false;
    }

    private void Update()
    {
        if (!_waitingForDismiss || _jumpUi == null)
            return;

        if (!WasJumpPressedThisFrame())
            return;

        _jumpUi.SetActive(false);
        _waitingForDismiss = false;
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (Layers.Player >= 0 && other.gameObject.layer == Layers.Player)
            return true;
        return other.GetComponent<PlayerMovement2D>() != null;
    }

    private static bool WasJumpPressedThisFrame()
    {
        return Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
    }
}
