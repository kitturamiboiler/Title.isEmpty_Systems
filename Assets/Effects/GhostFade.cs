using UnityEngine;

/// <summary>
/// 블링크 잔상 페이드 아웃 컴포넌트.
/// GC 부담을 최소화하기 위해 Coroutine 대신 Update 기반 처리.
/// PlayerBlinkController2D.CreateGhost()에서 Instantiate 직후 Initialize()를 호출한다.
/// </summary>
internal sealed class GhostFade : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private float _duration;
    private float _elapsed;
    private Color _baseColor;

    /// <summary>
    /// 페이드 아웃 파라미터를 초기화한다. Instantiate 직후 반드시 호출할 것.
    /// </summary>
    /// <param name="renderer">대상 SpriteRenderer.</param>
    /// <param name="duration">페이드 완료까지 걸리는 시간(초). 0 이하면 0.01로 보정.</param>
    public void Initialize(SpriteRenderer renderer, float duration)
    {
        if (renderer == null)
        {
            Debug.LogError($"[GhostFade] renderer가 null입니다 — {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        _renderer = renderer;
        _duration = Mathf.Max(0.01f, duration);
        _baseColor = _renderer.color;
    }

    private void Update()
    {
        if (_renderer == null)
        {
            Destroy(gameObject);
            return;
        }

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        Color c = _baseColor;
        c.a = Mathf.Lerp(_baseColor.a, 0f, t);
        _renderer.color = c;

        if (_elapsed >= _duration)
            Destroy(gameObject);
    }
}
