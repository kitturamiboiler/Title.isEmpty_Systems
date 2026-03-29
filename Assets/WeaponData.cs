using UnityEngine;
// 탄속, 데미지, 블링크 쿨타임 
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Player Ground Check (PlayerMovement2D)")]
    [Tooltip("BoxCast 하단 거리. 캡슐/타일 두께에 맞게 조정.")]
    public float groundBoxCastDistance = 0.28f;
    [Range(0f, 1f)]
    [Tooltip("바닥 법선 최소 Y. 낮출수록 경사·가장자리에서도 착지 인정.")]
    public float groundMinFloorNormalY = 0.35f;
    [Tooltip("발 모서리 보조 레이의 좌우 안쪽 여백.")]
    public float groundFootCornerInset = 0.06f;
    [Tooltip("BoxCast 시작점을 발보다 약간 위로 올려 콜라이더 내부 시작 문제 완화.")]
    public float groundCheckVerticalLift = 0.02f;

    [Header("Dagger Embed (DaggerProjectile2D)")]
    [Tooltip("박혀 고정될 표면 레이어(Ground, Wall 등). 비어 있으면 이름 Wall/Ground로 폴백.")]
    public LayerMask daggerEmbedSurfaceMask;

    [Header("Blink Combat (PlayerBlinkController2D)")]
    [Tooltip("블링크 경로/도착에서 체크할 적 레이어.")]
    public LayerMask blinkEnemyLayerMask;
    [Tooltip("이동 경로 스윕(원형 캐스트) 반경.")]
    public float blinkEnemySweepRadius = 0.32f;
    [Tooltip("도착 지점 추가 오버랩 반경.")]
    public float blinkDestinationEnemyRadius = 0.4f;
    [Tooltip("IHealth 없을 때 Destroy로 즉시 제거할지.")]
    public bool blinkInstantKillDestroysEnemyWithoutHealth = true;
    [Tooltip("블링크로 적에게 피해를 줄 때의 값(척살용 고정 데미지).")]
    public float blinkExecutionDamage = 500f;
    [Tooltip("블링크로 적 1체 이상 타격 시 공중 블링크 횟수를 max로 회복.")]
    public bool blinkKillRefillsAirBlink = true;

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
