using UnityEngine;

/// <summary>
/// 플레이어가 블링크를 실행할 때 도착 지점에 스폰되는 '잔상 마커'.
///
/// 방어 설계 (가설 3개 대응):
///
/// 가설 1 — 지형 끼임 / 판정 불능:
///   Unity OnTriggerEnter2D는 스폰 시점에 이미 트리거 안에 있으면 '진입 이벤트'가
///   발화하지 않는다. Initialize() 직후 Physics2D.OverlapCollider로 수동 체크하여
///   이미 스위치 존 안에 있으면 즉시 OnGhostEnter 호출.
///   벽 안에 스폰 방지는 EnemyBossShadow.OnPlayerBlinked에서 선행 처리.
///
/// 가설 3 — 보스 Bind 시 판정 불일치:
///   Pause() / Resume() API로 타이머와 페이드를 동결.
///   일시정지 중엔 _sr.color = PAUSED_COLOR(alpha 0.25) 시각적 구분.
///   코루틴 대신 Update 기반 타이머를 사용하여 Pause 시 Time.deltaTime 누적 중단.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BlinkGhostMarker : MonoBehaviour
{
    // ─── 상수 ─────────────────────────────────────────────────────────────────

    /// <summary>가설 3: 보스 구속 중 '비상호작용' 표시 알파.</summary>
    private const float PAUSED_ALPHA = 0.25f;

    /// <summary>마지막 30% 구간부터 페이드 아웃 시작.</summary>
    private const float FADE_START_RATIO = 0.7f;

    // ─── 런타임 ───────────────────────────────────────────────────────────────

    private float          _lifetime;
    private float          _elapsed;
    private SpriteRenderer _sr;
    private Color          _baseColor;
    private LayerMask      _switchLayerMask;

    private bool           _isPaused;
    private bool           _isFinished;

    // ─── 초기화 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// EnemyBossShadow.OnPlayerBlinked에서 스폰 직후 호출.
    /// </summary>
    /// <param name="lifetime">마커 생존 시간(초).</param>
    /// <param name="switchLayerMask">가설 1: 즉시 OverlapCollider 체크에 사용할 레이어.</param>
    /// <param name="ghostSprite">선택적 스프라이트 오버라이드.</param>
    public void Initialize(float lifetime, LayerMask switchLayerMask, Sprite ghostSprite = null)
    {
        _lifetime        = Mathf.Max(0.1f, lifetime);
        _elapsed         = 0f;
        _isPaused        = false;
        _isFinished      = false;
        _switchLayerMask = switchLayerMask;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
        {
            Debug.LogError($"[BlinkGhostMarker] SpriteRenderer missing on {gameObject.name}");
        }
        else
        {
            if (ghostSprite != null) _sr.sprite = ghostSprite;
            _baseColor = _sr.color;
        }

        // 가설 1: 스폰 직후 스위치 존 안에 있는지 즉시 수동 감지
        CheckImmediateOverlap();
    }

    // ─── 가설 3: Pause / Resume API ───────────────────────────────────────────

    /// <summary>보스 Bind 상태 진입 시 호출. 타이머 동결 + 투명화.</summary>
    public void Pause()
    {
        if (_isPaused || _isFinished) return;
        _isPaused = true;

        if (_sr == null) return;
        Color c = _sr.color;
        c.a = PAUSED_ALPHA;
        _sr.color = c;
    }

    /// <summary>보스 Unbind 시 호출. 타이머 재개 + 색상 복원.</summary>
    public void Resume()
    {
        if (!_isPaused || _isFinished) return;
        _isPaused = false;

        if (_sr == null) return;
        // 현재 경과 시간 기준으로 페이드 값 재계산
        _sr.color = GetFadedColor();
    }

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_isFinished || _isPaused) return;

        _elapsed += Time.deltaTime;

        if (_sr != null)
            _sr.color = GetFadedColor();

        if (_elapsed >= _lifetime)
        {
            _isFinished = true;
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isFinished) return;
        var sw = other.GetComponent<GhostSwitch>();
        if (sw == null) return;
        sw.OnGhostEnter(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var sw = other.GetComponent<GhostSwitch>();
        if (sw == null) return;
        sw.OnGhostExit(this);
    }

    private void OnDestroy()
    {
        // 조기 Destroy 시에도 스위치에 이탈 통보
        NotifyAllSwitchesOnDestroy();
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 가설 1:
    /// OnTriggerEnter2D는 오브젝트가 스폰될 때 이미 트리거 존 안에 있으면 발화하지 않음.
    /// OverlapCollider로 현재 위치의 스위치를 즉시 감지하여 수동 활성화.
    /// </summary>
    private void CheckImmediateOverlap()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        if (_switchLayerMask.value == 0) return;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(_switchLayerMask);
        filter.useTriggers = true;

        Collider2D[] results = new Collider2D[4];
        int count = Physics2D.OverlapCollider(col, filter, results);

        for (int i = 0; i < count; i++)
        {
            if (results[i] == null) continue;
            var sw = results[i].GetComponent<GhostSwitch>();
            if (sw != null) sw.OnGhostEnter(this);
        }
    }

    /// <summary>
    /// 조기 Destroy(보스 사망, 페이즈 전환 등) 시 연결된 스위치에 이탈 통보.
    /// GhostSwitch.Update()의 null 정리로도 처리되지만 즉시 비활성화를 보장.
    /// </summary>
    private void NotifyAllSwitchesOnDestroy()
    {
        if (_switchLayerMask.value == 0) return;

        var col = GetComponent<Collider2D>();
        if (col == null) return;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(_switchLayerMask);
        filter.useTriggers = true;

        Collider2D[] results = new Collider2D[4];
        int count = Physics2D.OverlapCollider(col, filter, results);

        for (int i = 0; i < count; i++)
        {
            if (results[i] == null) continue;
            var sw = results[i].GetComponent<GhostSwitch>();
            if (sw != null) sw.OnGhostExit(this);
        }
    }

    private Color GetFadedColor()
    {
        Color c = _baseColor;
        if (_elapsed >= _lifetime * FADE_START_RATIO)
        {
            float t = (_elapsed - _lifetime * FADE_START_RATIO) / (_lifetime * (1f - FADE_START_RATIO));
            c.a = Mathf.Lerp(_baseColor.a, 0f, t);
        }
        return c;
    }
}
