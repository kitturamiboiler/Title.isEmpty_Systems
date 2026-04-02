using UnityEngine;

/// <summary>
/// Phase 2 전용 패턴: 드론 방어막.
///
/// 메카닉:
///   3개의 드론이 보스 주위를 공전하며 플레이어에게 패리 가능 투사체를 지속 발사.
///   플레이어가 투사체를 패리 → 편향 → 드론에 명중 → 드론 파괴.
///   3개 전부 파괴 시 → BossVulnerable 전이.
///
/// 보스 면역:
///   방어막이 활성된 동안 BossHealth.IsVulnerable = false (공격 불가).
///   → 플레이어는 반드시 드론을 모두 제거해야 피격 창이 열린다.
///
/// 탈출 조건:
///   ① 드론 3개 전부 파괴 → Vulnerable
///   ② 최대 대기 시간(_maxShieldDuration) 초과 → Shield 강제 해제 후 DroneFormation (페널티 없음)
/// </summary>
public class PaperDroneShieldState : BossState
{
    private readonly EnemyBossPaper _paper;

    private const int   SHIELD_DRONE_COUNT = 3;
    private const float MAX_SHIELD_DURATION = 20f;  // 무한 대기 방지

    private PaperDrone[] _shieldDrones;
    private int          _destroyedCount;
    private float        _elapsed;

    public PaperDroneShieldState(EnemyBossPaper paper) : base(paper)
    {
        _paper = paper;
    }

    public override void Enter()
    {
        _destroyedCount = 0;
        _elapsed        = 0f;
        _shieldDrones   = new PaperDrone[SHIELD_DRONE_COUNT];

        if (_paper.Rb != null)
            _paper.Rb.linearVelocity = Vector2.zero;

        // 3개 드론 균등 각도로 공전 스폰
        for (int i = 0; i < SHIELD_DRONE_COUNT; i++)
        {
            float angle = i * (360f / SHIELD_DRONE_COUNT);
            _shieldDrones[i] = _paper.SpawnShieldDrone(angle);
            if (_shieldDrones[i] != null)
                _shieldDrones[i].OnDroneDestroyed += OnShieldDroneDestroyed;
        }
    }

    public override void Tick()
    {
        _elapsed += Time.deltaTime;

        // 타임아웃 — 드론들을 강제 해제하고 다음 패턴으로
        if (_elapsed >= MAX_SHIELD_DURATION)
        {
            DespawnRemainingDrones();
            GoTo(_paper.PaperDroneFormation);
        }
    }

    public override void Exit()
    {
        DespawnRemainingDrones();

        // 구독 해제 방어
        for (int i = 0; i < SHIELD_DRONE_COUNT; i++)
        {
            if (_shieldDrones?[i] != null)
                _shieldDrones[i].OnDroneDestroyed -= OnShieldDroneDestroyed;
        }
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private void OnShieldDroneDestroyed(PaperDrone drone)
    {
        _destroyedCount++;

        if (_destroyedCount >= SHIELD_DRONE_COUNT)
        {
            // Phase 2: 드론 전멸 직후 오버클럭 탄막으로 추가 압박
            GoTo(_paper.PaperOverclock ?? Machine.Vulnerable);
        }
    }

    private void DespawnRemainingDrones()
    {
        if (_shieldDrones == null) return;
        for (int i = 0; i < _shieldDrones.Length; i++)
        {
            if (_shieldDrones[i] != null)
            {
                _shieldDrones[i].OnDroneDestroyed -= OnShieldDroneDestroyed;
                _shieldDrones[i].Die();
            }
        }
    }
}
