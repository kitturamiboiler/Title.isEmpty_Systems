using UnityEngine;

/// <summary>
/// Boss 5 [Designer / 0x00 The Origin] — EnemyBossDesigner
///
/// 구조:
///   Wave 1 → Wave 2 → Wave 3 → Phase 1 (우산) → Phase 2 (진면목) → Struggle → 사망
///
/// 핵심 시스템:
///   - DesignerUmbrella: 단검 반사 → 블링크 원천 차단 (Phase 1)
///   - Phase 1 패리 → 우산 기절 → 단검 박힘 → 블링크 → 피격
///   - Phase 2 역패리: 플레이어 패리 즉시 구속 와이어 반격
///   - Struggle (동시 대치): HP 1 도달 시 상호 구속 ForcedReleaseTimer (가설 2)
///
/// 엔딩:
///   사망 후 EndingUSBItem 스폰 → 카메라 포커스 → 선택지 3개 (가설 3)
/// </summary>
public class EnemyBossDesigner : BossStateMachine
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Designer — Wave Spawn")]
    [SerializeField] private Transform[]    _waveSpawnPoints;
    [SerializeField] private GameObject     _agentPrefab;
    [SerializeField] private GameObject     _cyborgPrefab;
    [SerializeField] private GameObject     _elitePrefab;
    [SerializeField] private int            _wave1AgentCount  = 4;
    [SerializeField] private int            _wave2CyborgCount = 2;
    [SerializeField] private int            _wave3EliteCount  = 1;

    [Header("Designer — Umbrella (Phase 1)")]
    [SerializeField] private DesignerUmbrella _umbrella;

    [Header("Designer — Phase 2 Counter Wire")]
    [SerializeField] private GameObject     _bindProjectilePrefab;
    [SerializeField] private float          _counterWireSpeed    = 7f;
    [SerializeField] private float          _counterWireDuration = 2.5f;
    [SerializeField] private float          _counterWireLifetime = 3f;

    [Header("Designer — Ending")]
    [Tooltip("사망 후 스폰될 USB 아이템 프리팹.")]
    [SerializeField] private GameObject _usbItemPrefab;
    [SerializeField] private Vector2    _usbSpawnOffset = new Vector2(0f, 0.5f);

    // ─── State 프로퍼티 ────────────────────────────────────────────────────────

    public DesignerWaveState          DesignerWave          { get; private set; }
    public DesignerUmbrellaPhaseState DesignerUmbrellaPhase { get; private set; }
    public DesignerTrueFormState      DesignerTrueForm      { get; private set; }
    public DesignerStruggleState      DesignerStruggle      { get; private set; }

    // ─── 공개 데이터 ──────────────────────────────────────────────────────────

    public int              Wave1AgentCount  => _wave1AgentCount;
    public int              Wave2CyborgCount => _wave2CyborgCount;
    public int              Wave3EliteCount  => _wave3EliteCount;
    public DesignerUmbrella Umbrella         => _umbrella;

    // ─── BossStateMachine 구현 ────────────────────────────────────────────────

    protected override void InitializeStates()
    {
        DesignerWave          = new DesignerWaveState(this);
        DesignerUmbrellaPhase = new DesignerUmbrellaPhaseState(this);
        DesignerTrueForm      = new DesignerTrueFormState(this);
        DesignerStruggle      = new DesignerStruggleState(this);
    }

    public override IBossState GetFirstAttackState() => DesignerWave;

    protected override void OnPhaseChanged(BossPhase newPhase)
    {
        switch (newPhase)
        {
            case BossPhase.Phase2:
                // 우산 비활성 + 진면목 공개
                if (_umbrella != null) _umbrella.gameObject.SetActive(false);
                ChangeState(DesignerTrueForm);
                break;
        }
    }

    // ─── 사망 후 엔딩 처리 ────────────────────────────────────────────────────

    protected override void Start()
    {
        base.Start();
        Health.OnDied.AddListener(OnDesignerDied);
    }

    private void OnDesignerDied()
    {
        if (_usbItemPrefab == null) return;
        var go = Instantiate(_usbItemPrefab,
                             (Vector2)transform.position + _usbSpawnOffset,
                             Quaternion.identity);
        Destroy(go, 120f); // 2분 안전 마진
    }

    // ─── Wave 스폰 API (DesignerWaveState에서 호출) ───────────────────────────

    public EliteAgent SpawnAgent(int index)
    {
        if (_agentPrefab == null)
        {
            Debug.LogWarning("[EnemyBossDesigner] _agentPrefab이 없습니다.");
            return null;
        }
        Vector2 pos = GetSpawnPos(index);
        var go      = Instantiate(_agentPrefab, pos, Quaternion.identity);
        return go.GetComponent<EliteAgent>();
    }

    public NullCyborg SpawnCyborg(int index)
    {
        if (_cyborgPrefab == null)
        {
            Debug.LogWarning("[EnemyBossDesigner] _cyborgPrefab이 없습니다.");
            return null;
        }
        Vector2 pos = GetSpawnPos(index);
        var go      = Instantiate(_cyborgPrefab, pos, Quaternion.identity);
        return go.GetComponent<NullCyborg>();
    }

    public EliteZeroX01 SpawnElite(int index)
    {
        if (_elitePrefab == null)
        {
            Debug.LogWarning("[EnemyBossDesigner] _elitePrefab이 없습니다.");
            return null;
        }
        Vector2 pos = GetSpawnPos(index);
        var go      = Instantiate(_elitePrefab, pos, Quaternion.identity);
        return go.GetComponent<EliteZeroX01>();
    }

    /// <summary>Phase 2: 플레이어 패리 성공 시 DesignerTrueFormState에서 호출. 역패리 와이어 발사.</summary>
    public void FireCounterWire(Vector2 targetPos)
    {
        if (_bindProjectilePrefab == null || _firePoint == null) return;

        Vector2 dir = (targetPos - (Vector2)_firePoint.position).normalized;
        var go      = Instantiate(_bindProjectilePrefab, _firePoint.position, Quaternion.identity);
        go.GetComponent<BrotherBindProjectile2D>()?.Launch(
            dir,
            _counterWireSpeed,
            _counterWireDuration,
            _counterWireLifetime
        );
        Destroy(go, _counterWireLifetime + 0.5f);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private Vector2 GetSpawnPos(int index)
    {
        if (_waveSpawnPoints != null && index < _waveSpawnPoints.Length && _waveSpawnPoints[index] != null)
            return _waveSpawnPoints[index].position;
        return (Vector2)transform.position + new Vector2(Random.Range(-4f, 4f), 0f);
    }
}
