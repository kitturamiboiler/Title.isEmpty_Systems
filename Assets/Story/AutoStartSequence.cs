using System.Collections;
using UnityEngine;

/// <summary>
/// 씬 진입 후 오프닝 물리 적용 및(옵션) 컷씬 트리거 강제 점화.
/// JSON/ChapterStoryLoader 주입 직후 실행되도록 짧게 대기한다.
/// </summary>
public class AutoStartSequence : MonoBehaviour
{
    [Header("Opening physics")]
    [SerializeField] private PlayerMovement2D _playerMovement;

    [Header("Auto cutscene (optional)")]
    [Tooltip("비우면 같은 GameObject에서 TriggerCutscene을 찾는다.")]
    [SerializeField] private TriggerCutscene _triggerCutscene;

    [Header("Timing")]
    [SerializeField] private float _jsonInjectSettleSeconds = 0.1f;

    private IEnumerator Start()
    {
        if (_jsonInjectSettleSeconds > 0f)
            yield return new WaitForSeconds(_jsonInjectSettleSeconds);

        if (_playerMovement != null)
        {
            _playerMovement.BeginOpeningSequencePhysics(
                OpeningSequencePhysicsDefaults.MoveSpeed,
                OpeningSequencePhysicsDefaults.JumpVelocity,
                OpeningSequencePhysicsDefaults.GroundAcceleration);
            Debug.LogWarning(
                $"[AutoStart] {gameObject.name}: 오프닝 물리 적용 (move={OpeningSequencePhysicsDefaults.MoveSpeed}, jump={OpeningSequencePhysicsDefaults.JumpVelocity}).");
        }
        else
            Debug.LogWarning($"[AutoStart] {gameObject.name}: PlayerMovement2D 미할당 — 물리 스킵.");

        var trigger = _triggerCutscene != null ? _triggerCutscene : GetComponent<TriggerCutscene>();
        if (trigger != null)
        {
            Debug.LogWarning($"[AutoStart] {gameObject.name}: 시퀀스 강제 점화 → TriggerCutscene.Play().");
            trigger.Play();
        }
        else
            Debug.LogError($"[AutoStart] {gameObject.name}: TriggerCutscene을 찾지 못했습니다.");
    }
}
