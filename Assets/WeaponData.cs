using UnityEngine;
// 탄속, 데미지, 블링크 쿨타임 
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Dagger Stats")]
    public float damage = 10f;
    public float projectileSpeed = 20f; // 탄속 (단도 날아가는 속도)
    public float blinkCooldown = 3f;
    public float maxDistance = 15f;     // 단검이 유효한 최대 거리
    public float coyoteTime = 0.1f;    // 낭떠러지 끝에서의 자비
    public float inputBufferTime = 0.1f;  // 키씹 방지 -> 선 입력

    [Header("Wall Jump")]
    [Tooltip("벽 반대 방향으로 밀어내는 수평 속도 성분.")]
    public float wallJumpHorizontalForce = 7f;
    [Tooltip("벽 점프 시 위로 주는 수직 속도.")]
    public float wallJumpVerticalForce = 12f;
    [Tooltip("벽 점프 직후 벽 방향 입력 무시 시간(지그재그 상승 방지). 약 0.15초 권장.")]
    public float wallJumpInputLockTime = 0.15f;

    [Header("Blink Invincibility")]
    public float invincibleDuration = 0.15f;
    public string invincibleLayerName = "PlayerInvincible";
    public string originalLayerName = "Player";

    [Header("Visuals")]
    public Sprite weaponSprite;
    public ParticleSystem hitParticle;
    public ParticleSystem blinkEffectPrefab; // 블링크 히트 스톱용 짧은 이펙트
    public float daggerTrailTime = 0.08f; // 단도 날아가는 궤적 속도감 
    public float ghostDuration = 0.1f; // 척살 후 반 투명화
    public float cameraShakeDuration = 0.06f; // 카메라 앵글 흔들림 지속
    public float cameraShakeIntensity = 0.08f; // 카메라 애글 흔들림

    [Header("Prefabs")]
    public GameObject daggerProjectilePrefab; // 2D 단검 투사체 프리팹
}
