using UnityEngine;

/// <summary>
/// 씬 전역 이펙트 관리 싱글턴.
/// - 모든 Instantiate에 Destroy를 보장하여 파티클 누수를 방지한다.
/// </summary>
public class EffectManager : MonoBehaviour
{
    /// <summary>싱글턴 인스턴스.</summary>
    public static EffectManager Instance { get; private set; }

    [Header("Effect Prefabs")]
    [Tooltip("기본 스파크 이펙트 프리팹")]
    public ParticleSystem DefaultSpark;

    [Tooltip("파티클 duration 이후 추가 여유 시간(초). 루프 파티클 방지.")]
    [SerializeField] private float _destroyGracePeriod = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 기본 스파크 이펙트를 특정 위치/방향에 소환하고 자동 파괴를 예약한다.
    /// </summary>
    public void SpawnDefaultSpark(Vector2 position, Vector2 normal)
    {
        if (DefaultSpark == null) return;

        SpawnEffect(DefaultSpark, position, normal);
    }

    /// <summary>
    /// 지정 파티클 프리팹을 소환하고 <c>duration + gracePeriod</c> 후 자동 파괴한다.
    /// </summary>
    /// <param name="effectPrefab">소환할 파티클 프리팹.</param>
    /// <param name="position">소환 위치 (2D 월드 좌표).</param>
    /// <param name="normal">표면 법선 방향. 회전 계산에 사용.</param>
    public void SpawnEffect(ParticleSystem effectPrefab, Vector2 position, Vector2 normal)
    {
        if (effectPrefab == null) return;

        // Vector2 normal → Quaternion: 2D 표면이라도 파티클 회전은 3D 기반
        Vector3 normal3 = new Vector3(normal.x, normal.y, 0f);
        Quaternion rotation = normal3 != Vector3.zero
            ? Quaternion.LookRotation(Vector3.forward, normal3)
            : Quaternion.identity;

        ParticleSystem ps = Instantiate(
            effectPrefab,
            new Vector3(position.x, position.y, 0f),
            rotation
        );

        ps.Play();

        float lifetime = ps.main.duration + _destroyGracePeriod;
        Destroy(ps.gameObject, lifetime);
    }
}
