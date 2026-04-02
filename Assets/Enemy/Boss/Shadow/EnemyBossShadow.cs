using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 4 [Shadow / 잔상] — EnemyBossShadow
///
/// 컨셉: 전투가 없는 보스. 플레이어의 블링크 '잔상(BlinkGhostMarker)'을 이용한
/// 스위칭 퍼즐로만 대미지를 줄 수 있다.
///
/// 퍼즐 흐름:
///   Phase 1 (lives 3): 스위치 1개 → 취약
///   Phase 2 (lives 2): 스위치 2개 동시 활성 → 취약
///   Phase 3 (lives 1): 스위치 3개 동시 활성 → 취약 + 가짜 스위치 배치
///
/// 방어 설계 (가설 3개):
///
/// 가설 1 — 지형 끼임:
///   OnPlayerBlinked에서 Physics2D.OverlapPoint로 벽 안 스폰 사전 차단.
///   BlinkGhostMarker.Initialize에 switchLayerMask 전달 → 즉시 OverlapCollider 체크.
///
/// 가설 2 — HauntState 무한 루프:
///   HauntMaxDuration을 인스펙터에서 설정. PuzzleResetPoint로 워프 지원.
///   ShadowHauntState가 이 값을 참조하여 절대 시간 기반으로 퍼즐 재시작.
///
/// 가설 3 — Bind/IBindable 충돌:
///   Bind() 오버라이드 → 모든 활성 마커 Pause() + PAUSED_ALPHA 시각화.
///   Unbind() 오버라이드 → 모든 활성 마커 Resume() + 색상 복원.
///   신규 마커 스폰 시 현재 _isBound 상태면 즉시 Pause().
/// </summary>
public class EnemyBossShadow : BossStateMachine
{
    // ─── 인스펙터 ──────────────────────────────────────────────────────────────

    [Header("Shadow — Puzzle Switches")]
    [Tooltip("씬에 배치된 진짜 스위치 3개. 인덱스 순서가 Phase별 활성 수.")]
    [SerializeField] private GhostSwitch[] _realSwitches;

    [Tooltip("Phase 3에서 추가 배치되는 가짜 스위치.")]
    [SerializeField] private GhostSwitch[] _fakeSwitches;

    [Header("Shadow — Ghost Marker")]
    [SerializeField] private GameObject _ghostMarkerPrefab;
    [Tooltip("Phase 1 기준 마커 생존 시간(초). Phase 2/3에서 자동 단축.")]
    [SerializeField] private float _ghostMarkerLifetime = 3.5f;
    [Tooltip("가설 5: 동시 활성 마커 최대 개수. 초과 시 가장 오래된 마커(LRU) 제거.")]
    [SerializeField] private int   _maxActiveMarkers    = 5;
    [Tooltip("퍼즐 리셋까지의 타임아웃(초). ShadowGhostPhaseState에서 참조.")]
    [SerializeField] private float _puzzleTimeout = 12f;

    [Header("Shadow — Layer Masks (가설 1)")]
    [Tooltip("가설 1: 마커 즉시 감지용. GhostSwitch가 속한 레이어.")]
    [SerializeField] private LayerMask _switchLayerMask;
    [Tooltip("가설 1: 벽 안 스폰 방지용 레이어.")]
    [SerializeField] private LayerMask _wallMask;

    [Header("Shadow — Haunt (가설 2)")]
    [Tooltip("가설 2: HauntState 절대 타임아웃(초). AI 경로 무관하게 강제 퍼즐 재시작.")]
    [SerializeField] private float     _hauntMaxDuration = 6f;
    [Tooltip("가설 2: 퍼즐 재시작 시 보스가 워프해올 위치. 비워두면 현재 위치 유지.")]
    [SerializeField] private Transform _puzzleResetPoint;

    [Header("Shadow — Haunt Speed")]
    [SerializeField] private float _hauntSpeedPhase1 = 2.5f;
    [SerializeField] private float _hauntSpeedPhase2 = 3.8f;
    [SerializeField] private float _hauntSpeedPhase3 = 5.2f;

    // ─── State 프로퍼티 ────────────────────────────────────────────────────────

    public ShadowGhostPhaseState ShadowGhostPhase { get; private set; }
    public ShadowHauntState      ShadowHaunt      { get; private set; }

    // ─── 공개 데이터 (States에서 참조) ────────────────────────────────────────

    public float     PuzzleTimeout    => _puzzleTimeout;
    public float     HauntMaxDuration => _hauntMaxDuration;
    public Transform PuzzleResetPoint => _puzzleResetPoint;

    // ─── 런타임 캐시 ──────────────────────────────────────────────────────────

    private PlayerBlinkController2D        _blinkCtrl;
    private readonly List<BlinkGhostMarker> _activeMarkers = new List<BlinkGhostMarker>();
    private bool                           _isBound;        // 가설 3

    // ─── BossStateMachine 오버라이드 ──────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _blinkCtrl = Object.FindFirstObjectByType<PlayerBlinkController2D>();
        if (_blinkCtrl == null)
            Debug.LogError("[EnemyBossShadow] PlayerBlinkController2D를 찾을 수 없습니다. 퍼즐 마커 스폰 불가.");

        if (_realSwitches == null || _realSwitches.Length < 3)
            Debug.LogWarning("[EnemyBossShadow] _realSwitches가 3개 미만입니다. Phase 3 퍼즐이 정상 작동하지 않을 수 있습니다.");

        DeactivateAllSwitches();
    }

    protected override void Start()
    {
        base.Start();

        if (_blinkCtrl != null)
            _blinkCtrl.OnBlinkExecuted += OnPlayerBlinked;
    }

    private void OnDestroy()
    {
        if (_blinkCtrl != null)
            _blinkCtrl.OnBlinkExecuted -= OnPlayerBlinked;
    }

    protected override void InitializeStates()
    {
        ShadowGhostPhase = new ShadowGhostPhaseState(this);
        ShadowHaunt      = new ShadowHauntState(this);
    }

    public override IBossState GetFirstAttackState() => ShadowGhostPhase;

    protected override void OnPhaseChanged(BossPhase newPhase)
    {
        ResetAllSwitches();
        // TODO(기획): Phase 진입 연출 이펙트 추가 — 2026-04-01
        ChangeState(ShadowGhostPhase);
    }

    // ─── IBindable 오버라이드 (가설 3) ────────────────────────────────────────

    /// <summary>
    /// 가설 3: 보스 구속 시 모든 활성 마커를 일시 정지 + 투명화.
    /// 신규 마커가 스폰될 때도 _isBound를 참조하여 즉시 Pause().
    /// </summary>
    public override void Bind(float duration)
    {
        _isBound = true;
        base.Bind(duration);
        PauseAllMarkers();
    }

    /// <summary>가설 3: 구속 해제 시 모든 마커 타이머 재개 + 색상 복원.</summary>
    public override void Unbind()
    {
        _isBound = false;
        base.Unbind();
        ResumeAllMarkers();
    }

    // ─── 퍼즐 API (States에서 호출) ───────────────────────────────────────────

    /// <summary>Phase 번호에 따른 필요 스위치 수. Phase 1=1, Phase 2=2, Phase 3=3.</summary>
    public int GetRequiredSwitchCount()
    {
        if (IsPhase3) return 3;
        if (IsPhase2) return 2;
        return 1;
    }

    /// <summary>진짜 스위치 배열 반환. GhostPhaseState에서 활성 체크에 사용.</summary>
    public GhostSwitch[] GetRealSwitches() => _realSwitches;

    /// <summary>Phase에 맞는 스위치 count개를 활성화(Trigger Collider ON).</summary>
    public void ActivateSwitches(int count)
    {
        if (_realSwitches == null) return;

        int activate = Mathf.Min(count, _realSwitches.Length);
        for (int i = 0; i < _realSwitches.Length; i++)
        {
            if (_realSwitches[i] == null) continue;
            _realSwitches[i].gameObject.SetActive(i < activate);
        }
    }

    /// <summary>Phase 3 가짜 스위치 활성화.</summary>
    public void ActivateFakeSwitches()
    {
        if (_fakeSwitches == null) return;
        foreach (var sw in _fakeSwitches)
        {
            if (sw != null) sw.gameObject.SetActive(true);
        }
    }

    /// <summary>모든 스위치 초기화 + 비활성화 (Phase 전환 / 리셋).</summary>
    public void ResetAllSwitches()
    {
        if (_realSwitches != null)
            foreach (var sw in _realSwitches)
                sw?.Reset();

        if (_fakeSwitches != null)
            foreach (var sw in _fakeSwitches)
            {
                sw?.Reset();
                sw?.gameObject.SetActive(false);
            }
    }

    public float GetHauntSpeed()
    {
        if (IsPhase3) return _hauntSpeedPhase3;
        if (IsPhase2) return _hauntSpeedPhase2;
        return _hauntSpeedPhase1;
    }

    /// <summary>
    /// 가설 5 + Phase 2 압박:
    /// Phase 2에서 마커 수명 80%, Phase 3에서 60%로 단축.
    /// 플레이어는 더 빠르게 다음 스위치로 이동해야 한다.
    /// </summary>
    public float GetCurrentMarkerLifetime()
    {
        if (IsPhase3) return _ghostMarkerLifetime * 0.6f;
        if (IsPhase2) return _ghostMarkerLifetime * 0.8f;
        return _ghostMarkerLifetime;
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void DeactivateAllSwitches()
    {
        if (_realSwitches != null)
            foreach (var sw in _realSwitches)
                sw?.gameObject.SetActive(false);

        if (_fakeSwitches != null)
            foreach (var sw in _fakeSwitches)
                sw?.gameObject.SetActive(false);
    }

    /// <summary>
    /// PlayerBlinkController2D.OnBlinkExecuted 구독.
    /// 블링크 도착 지점에 BlinkGhostMarker를 스폰.
    /// </summary>
    private void OnPlayerBlinked(Vector2 _, Vector2 blinkPos)
    {
        if (_ghostMarkerPrefab == null)
        {
            Debug.LogWarning("[EnemyBossShadow] _ghostMarkerPrefab이 비어있습니다.");
            return;
        }

        // 가설 1: 벽 안 스폰 방지 — 마커가 물리적으로 유효한 위치에만 생성
        if (_wallMask.value != 0 && Physics2D.OverlapPoint(blinkPos, _wallMask))
        {
            Debug.LogWarning($"[EnemyBossShadow] 블링크 위치({blinkPos})가 벽 안입니다. 마커 스폰 취소.");
            return;
        }

        // 가설 5: LRU — 최대 마커 수 초과 시 가장 오래된 마커 제거
        _activeMarkers.RemoveAll(m => m == null);
        if (_activeMarkers.Count >= _maxActiveMarkers)
        {
            var oldest = _activeMarkers[0];
            _activeMarkers.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }

        var go = Instantiate(_ghostMarkerPrefab, blinkPos, Quaternion.identity);

        var marker = go.GetComponent<BlinkGhostMarker>();
        if (marker == null)
        {
            Debug.LogError("[EnemyBossShadow] _ghostMarkerPrefab에 BlinkGhostMarker 컴포넌트가 없습니다.");
            Destroy(go);
            return;
        }

        // 가설 1 + Phase 2 압박: Phase별 수명 적용
        marker.Initialize(GetCurrentMarkerLifetime(), _switchLayerMask);

        _activeMarkers.Add(marker);

        // 가설 3: 현재 보스가 구속 상태면 신규 마커도 즉시 일시 정지
        if (_isBound) marker.Pause();
    }

    /// <summary>가설 3: 모든 활성 마커 일시 정지.</summary>
    private void PauseAllMarkers()
    {
        _activeMarkers.RemoveAll(m => m == null);
        foreach (var m in _activeMarkers)
            m.Pause();
    }

    /// <summary>가설 3: 모든 활성 마커 재개.</summary>
    private void ResumeAllMarkers()
    {
        _activeMarkers.RemoveAll(m => m == null);
        foreach (var m in _activeMarkers)
            m.Resume();
    }
}
