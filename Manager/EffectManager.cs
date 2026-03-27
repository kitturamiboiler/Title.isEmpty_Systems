using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [Header("Effect Prefabs")]
    [Tooltip("기본 스파크 이펙트 프리팹 (나중에 할당)")]
    public ParticleSystem DefaultSpark;

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
    /// 특정 위치/방향에 기본 스파크 이펙트를 소환.
    /// </summary>
    public void SpawnDefaultSpark(Vector3 position, Vector3 normal)
    {
        if (DefaultSpark == null) return;

        var ps = Instantiate(DefaultSpark, position, Quaternion.LookRotation(normal));
        ps.Play();
    }

    /// <summary>
    /// 원하는 파티클 프리팹을 특정 위치/방향에 소환.
    /// </summary>
    public void SpawnEffect(ParticleSystem effectPrefab, Vector3 position, Vector3 normal)
    {
        if (effectPrefab == null) return;

        var ps = Instantiate(effectPrefab, position, Quaternion.LookRotation(normal));
        ps.Play();
    }
}

