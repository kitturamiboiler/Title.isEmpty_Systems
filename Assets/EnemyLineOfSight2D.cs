using UnityEngine;

/// <summary>
/// 적에서 플레이어를 향해 빨간색 점선 조준선을 그리는 컴포넌트.
/// LineRenderer의 Material 인스턴스를 Awake에서 1회만 생성하고 캐싱해
/// Update 내 GC 할당을 제거한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class EnemyLineOfSight2D : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("플레이어 Transform. 인스펙터에 할당하면 씬 탐색을 건너뜀.")]
    [SerializeField] private Transform playerTransform;

    [Header("Line Settings")]
    [Tooltip("점선 타일링 계수. 낮을수록 점이 촘촘해짐.")]
    [SerializeField] private float dashTiling = 5f;

    private LineRenderer _lineRenderer;

    /// <summary>
    /// Awake에서 1회 생성한 Material 인스턴스. Update에서 재사용.
    /// OnDestroy에서 명시적으로 파괴한다.
    /// </summary>
    private Material _cachedMaterial;

    private static readonly Vector2 TILING_Y_FIXED = new Vector2(0f, 1f);

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();

        if (_lineRenderer == null)
        {
            Debug.LogError($"[EnemyLineOfSight2D] LineRenderer 컴포넌트 없음 — {gameObject.name}");
            return;
        }

        // material getter는 최초 1회 호출 시 sharedMaterial의 인스턴스를 생성한다.
        // 이후 Update에서는 이 캐시를 재사용해 GC 할당을 제거한다.
        _cachedMaterial = _lineRenderer.material;
    }

    private void Start()
    {
        // 인스펙터에 이미 할당된 경우 씬 전체 탐색을 건너뜀
        if (playerTransform == null)
        {
            var playerCtrl = FindFirstObjectByType<PlayerBlinkController2D>();
            if (playerCtrl != null)
            {
                playerTransform = playerCtrl.transform;
            }
            else
            {
                Debug.LogWarning($"[EnemyLineOfSight2D] PlayerBlinkController2D를 찾지 못했습니다. " +
                                 $"인스펙터에서 playerTransform을 직접 할당하세요. — {gameObject.name}");
            }
        }

        if (_lineRenderer == null) return;

        _lineRenderer.startColor = Color.red;
        _lineRenderer.endColor = Color.red;
        _lineRenderer.useWorldSpace = true;
    }

    private void Update()
    {
        if (playerTransform == null || _lineRenderer == null || _cachedMaterial == null)
            return;

        Vector3 startPos = transform.position;
        Vector3 endPos = playerTransform.position;

        _lineRenderer.SetPosition(0, startPos);
        _lineRenderer.SetPosition(1, endPos);

        float lineLength = Vector3.Distance(startPos, endPos);
        float currentTilingX = lineLength * dashTiling;

        // 캐시된 머티리얼에 직접 쓰기 — 매 프레임 new Vector2 struct 생성은 스택 할당이라 허용 범위
        _cachedMaterial.mainTextureScale = new Vector2(currentTilingX, TILING_Y_FIXED.y);
    }

    private void OnDestroy()
    {
        // Awake에서 생성한 Material 인스턴스 명시적 파괴
        if (_cachedMaterial != null)
            Destroy(_cachedMaterial);
    }
}
