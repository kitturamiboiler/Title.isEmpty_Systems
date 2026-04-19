using UnityEngine;

/// <summary>
/// 튜토리얼·컷신에서 물리 입력과 가상 입력 주고받기용 계약.
/// 구현체: <see cref="PlayerMovement2D"/>.
/// </summary>
public interface IPlayerCutsceneHandoff
{
    /// <summary>
    /// true면 실제 키보드/마우스 이동·점프 예약 무시, <see cref="InjectVirtualInput"/>만 물리에 반영.
    /// false 전환 시 속도·내부 버퍼 즉시 소거(연타 점프 방지).
    /// </summary>
    /// <param name="isCutscene">컷신 조작 탈취 여부.</param>
    void SetCutsceneMode(bool isCutscene);

    /// <summary>
    /// 컷신 모드일 때만 적용. 매 FixedUpdate 전 또는 입력 소스에서 호출해 방향 유지(-1~1 권장).
    /// </summary>
    /// <param name="dir">x=수평, y=벽타기 등 수직 보조(컷신 중 벽 구간 한정).</param>
    void InjectVirtualInput(Vector2 dir);
}
