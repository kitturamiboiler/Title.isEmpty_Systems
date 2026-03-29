using UnityEngine;

/// <summary>
/// 적에서 플레이어를 향해 빨간색 점선(----) 조준선을 그리는 스크립트.
/// LineRenderer 컴포넌트가 필요합니다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class EnemyLineOfSight2D : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("플레이어 오브젝트 (스크립트가 자동으로 찾습니다)")]
    [SerializeField] private Transform playerTransform;

    [Header("Line Settings")]
    [Tooltip("점선의 간격 조절 수치 (낮을수록 점이 촘촘해집니다)")]
    [SerializeField] private float dashTiling = 5f;

    private LineRenderer _lineRenderer;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        // 스크립트가 자동으로 플레이어 위치를 찾습니다.
        var playerCtrl = FindFirstObjectByType<PlayerBlinkController2D>();
        if (playerCtrl != null)
        {
            playerTransform = playerCtrl.transform;
        }

        // 초기 색상 설정 (RequireComponent로 인스펙터 설정을 덮어쓸 수 있으므로 방어용)
        _lineRenderer.startColor = Color.red;
        _lineRenderer.endColor = Color.red;
        _lineRenderer.useWorldSpace = true; // 실제 월드 공간 좌표 사용
    }

    private void Update()
    {
        if (playerTransform == null || _lineRenderer == null) return;

        // 1. 선의 시작점과 끝점 설정
        Vector3 startPos = transform.position; // 적 위치
        Vector3 endPos = playerTransform.position; // 플레이어 위치

        _lineRenderer.SetPosition(0, startPos);
        _lineRenderer.SetPosition(1, endPos);

        // 2. 핵심: 선의 길이에 비례하여 점선 타일링 조절
        // 선이 길어지면 점의 개수도 늘어나게 만듭니다.
        float lineLength = Vector3.Distance(startPos, endPos);

        // 인스펙터에서 설정한 tiling 수치와 길이를 곱해서 반복 횟수 결정
        float currentTilingX = lineLength * dashTiling;

        // LineRenderer의 재질(Material)에 타일링 수치 적용
        _lineRenderer.material.mainTextureScale = new Vector2(currentTilingX, 1f);
    }
}