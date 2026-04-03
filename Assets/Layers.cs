using UnityEngine;

/// <summary>
/// 프로젝트 전역 레이어 인덱스 상수.
/// LayerMask.NameToLayer 는 Awake 이전에 호출해도 안전하지만,
/// static readonly 로 한 번만 계산해 런타임 문자열 조회를 제거한다.
/// </summary>
public static class Layers
{
    /// <summary>플레이어 레이어.</summary>
    public static readonly int Player          = LayerMask.NameToLayer("Player");

    /// <summary>무적 상태 전환용 레이어.</summary>
    public static readonly int PlayerInvincible = LayerMask.NameToLayer("PlayerInvincible");

    /// <summary>적 레이어.</summary>
    public static readonly int Enemy           = LayerMask.NameToLayer("Enemy");

    /// <summary>벽 레이어.</summary>
    public static readonly int Wall            = LayerMask.NameToLayer("Wall");

    /// <summary>바닥 레이어.</summary>
    public static readonly int Ground          = LayerMask.NameToLayer("Ground");

    /// <summary>
    /// 플레이어 착지·슬램·블링크 지면 판정 공통 기본 마스크 (Ground 단일 비트).
    /// 인스펙터 <c>groundMask</c>가 비어 있으면 Movement / FSM / Blink가 이 값을 폴백으로 사용한다.
    /// </summary>
    public static readonly LayerMask PlayerPhysicsGroundMask =
        Ground >= 0 ? (LayerMask)(1 << Ground) : default;

    /// <summary>단검 투사체 레이어.</summary>
    public static readonly int Dagger          = LayerMask.NameToLayer("Dagger");

    // -------------------------------------------------------------------------
    // LayerMask 헬퍼 (비트 연산 비용 최소화용 캐시)
    // -------------------------------------------------------------------------

    /// <summary>바닥 + 벽을 포함한 단검 박힘 표면 마스크 (기본).</summary>
    public static readonly int EmbedSurfaceMask =
        (1 << LayerMask.NameToLayer("Wall")) |
        (1 << LayerMask.NameToLayer("Ground"));

    /// <summary>LayerMask에 특정 레이어가 포함되는지 (산나비 모작 Extension.Contain 대응).</summary>
    public static bool MaskContains(LayerMask mask, int layer) => GameExtensions.ContainsLayer(mask, layer);
}
