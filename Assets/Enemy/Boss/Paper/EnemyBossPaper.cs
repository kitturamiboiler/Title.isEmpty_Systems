using UnityEngine;

/// <summary>
/// Boss 2: 서류 [Paper] — 패리 반응 숙달 시험.
///
/// 컨셉: 관료주의 사이버펑크 간부. 직접 싸우지 않고 드론 군단을 지휘한다.
///       패리하지 못한 플레이어는 드론 탄막에 압도당한다.
///
/// 페이즈 구조:
///   Phase 1 (lives 4~3): DroneFormation → PaperStorm → Vulnerable 반복
///   Phase 2 (lives 2~1): DroneShield (드론 방어막) → PaperStorm(가속) → Vulnerable
///   Phase 3 (lives 1):   StampSeal + DroneFormation 동시, ArmorGauge 재충전
///
/// ArmorGauge:
///   maxArmorGauge = 2 권장.
///   편향된 투사체가 보스 본체에 명중 시 TakeArmorDamage 호출.
///   ArmorGauge = 0 + lives == 1 → IsGrabbable = true → Blink 처형 가능.
///
/// BossStateMachine 확장 패턴:
///   protected override void InitializeStates() — Paper 전용 State 등록
///   public override IBossState GetFirstAttackState() — DroneFormation 반환
/// </summary>
public class EnemyBossPaper : BossStateMachine
{
    // ─── 직렬화 ───────────────────────────────────────────────────────────────

    [Header("Paper — Drone Formation")]
    [SerializeField] private Transform[] _formationPoints;   // 드론 배치 위치 (인스펙터 연결)
    [SerializeField] private GameObject  _dronePrefab;       // PaperDrone 컴포넌트 필수

    [Header("Paper — Stamp Seal")]
    [SerializeField] private GameObject  _stampIndicatorPrefab; // 착탄 예고 연출
    [SerializeField] private float       _stampTelegraphTime = 1.2f;
    [SerializeField] private float       _stampAoeRadius     = 2.5f;
    [SerializeField] private float       _stampDamage        = 1f;

    // ─── Paper 전용 State ─────────────────────────────────────────────────────

    public PaperDroneFormationState PaperDroneFormation { get; private set; }
    public PaperStormState          PaperStorm          { get; private set; }
    public PaperDroneShieldState    PaperDroneShield    { get; private set; }
    /// <summary>Phase 2 전용: 드론 방어막 파괴 직후 급속 패리 탄막.</summary>
    public PaperOverclockState      PaperOverclock      { get; private set; }
    public PaperStampSealState      PaperStampSeal      { get; private set; }

    // ─── 드론 트래킹 ──────────────────────────────────────────────────────────

    /// <summary>현재 활성 드론 수 (Formation + Shield 합산).</summary>
    public int ActiveDroneCount { get; private set; }

    // ─── BossStateMachine 구현 ────────────────────────────────────────────────

    protected override void InitializeStates()
    {
        PaperDroneFormation = new PaperDroneFormationState(this);
        PaperStorm          = new PaperStormState(this);
        PaperDroneShield    = new PaperDroneShieldState(this);
        PaperOverclock      = new PaperOverclockState(this);
        PaperStampSeal      = new PaperStampSealState(this);
    }

    public override IBossState GetFirstAttackState() => PaperDroneFormation;

    protected override void OnPhaseChanged(BossPhase newPhase)
    {
        switch (newPhase)
        {
            case BossPhase.Phase2:
                // Phase 2 진입: 드론 방어막 패턴으로 전환
                ChangeState(PaperDroneShield);
                break;

            case BossPhase.Phase3:
                // Phase 3 진입: 갑옷 재충전 + 도장 인장 패턴
                Health?.ResetArmor();
                ChangeState(PaperStampSeal);
                break;
        }
    }

    // ─── 드론 스폰 / 해제 API (State에서 호출) ────────────────────────────────

    /// <summary>
    /// Formation 드론 스폰. PaperDroneFormationState에서 호출.
    /// formationPoints 배열 순서로 배치.
    /// </summary>
    public PaperDrone SpawnFormationDrone(int index)
    {
        if (_dronePrefab == null)
        {
            Debug.LogWarning("[EnemyBossPaper] DronePrefab이 연결되지 않았습니다.");
            return null;
        }
        if (_formationPoints == null || index >= _formationPoints.Length) return null;

        var go    = Object.Instantiate(_dronePrefab, _formationPoints[index].position, Quaternion.identity);
        var drone = go.GetComponent<PaperDrone>();
        if (drone == null) return null;

        drone.OnDroneDestroyed += OnDroneDestroyed;
        ActiveDroneCount++;
        return drone;
    }

    /// <summary>
    /// Shield 드론 스폰 (보스 주위 공전). PaperDroneShieldState에서 호출.
    /// </summary>
    /// <param name="initialAngle">공전 초기 각도 (도). 3드론: 0, 120, 240.</param>
    public PaperDrone SpawnShieldDrone(float initialAngle)
    {
        if (_dronePrefab == null) return null;

        var go    = Object.Instantiate(_dronePrefab, transform.position, Quaternion.identity);
        var drone = go.GetComponent<PaperDrone>();
        if (drone == null) return null;

        drone.InitializeOrbit(transform, initialAngle);
        drone.OnDroneDestroyed += OnDroneDestroyed;
        ActiveDroneCount++;
        return drone;
    }

    // ─── 인장 타격 (StampSealState에서 호출) ─────────────────────────────────

    /// <summary>착탄 예고 연출 오브젝트 스폰 후 지정 시간 후 자동 파괴.</summary>
    public void SpawnStampIndicator(Vector2 pos)
    {
        if (_stampIndicatorPrefab == null) return;
        var go = Object.Instantiate(_stampIndicatorPrefab, pos, Quaternion.identity);
        Destroy(go, _stampTelegraphTime + 0.2f);
    }

    /// <summary>
    /// 인장 충격파 범위 데미지 + HitStop.
    /// 가설 3 방어: center 하단에 지면이 없으면 충격파 불발.
    ///   → 공중·플랫폼 끝 인장이 허공으로 뻗어 나가는 판정 오류 차단.
    /// </summary>
    public void ExecuteStampImpact(Vector2 center)
    {
        // 지면 존재 확인 — center 바로 아래 짧은 Raycast
        const float GROUND_CHECK_DIST = 0.6f;
        RaycastHit2D groundHit = Physics2D.Raycast(
            center,
            Vector2.down,
            GROUND_CHECK_DIST,
            _groundMask
        );

        if (groundHit.collider == null)
        {
            // 지면 없음 → 충격파 불발 (플레이어는 데미지 없음)
            return;
        }

        // 실제 충격 기준점을 지면 표면으로 고정 — 공중 오버랩 오탐 방지
        Vector2 impactPoint = groundHit.point;

        var buf = new Collider2D[8];
        int count = Physics2D.OverlapCircleNonAlloc(
            impactPoint,
            _stampAoeRadius,
            buf,
            1 << Layers.Player
        );
        for (int i = 0; i < count; i++)
            buf[i].GetComponentInParent<IHealth>()?.TakeDamage(_stampDamage);

        HitStopManager.Instance?.Request(
            _data != null ? _data.hitStopDurationOnHit : 0.05f,
            _data != null ? _data.hitStopTimeScaleOnHit : 0.1f
        );
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────

    public float  StampTelegraphTime => _stampTelegraphTime;
    public int    FormationDroneCount => _formationPoints != null ? _formationPoints.Length : 0;

    // ─── Private ──────────────────────────────────────────────────────────────

    private void OnDroneDestroyed(PaperDrone drone)
    {
        ActiveDroneCount = Mathf.Max(0, ActiveDroneCount - 1);
    }
}
