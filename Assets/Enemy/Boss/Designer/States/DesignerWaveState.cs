using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설계자 전투 전 웨이브 관리 State.
///
/// Wave 1 → Wave 2 → Wave 3 순서로 진행. 모든 적 사망 시 다음 Wave.
/// Wave 3 완료 후 Phase 1 (DesignerUmbrellaPhaseState) 전이.
///
/// 방어 설계:
///   - SpawnPoint null 체크 → null인 포인트는 보스 자신 위치에 스폰
///   - 적 사망 이벤트 구독 누락 방지: 각 적의 OnXxxDied를 Enter에서 구독, Exit에서 해제
///   - 모든 적 소멸 후에도 전이가 안 되는 케이스: _aliveCount가 0이면 Tick에서도 감지
/// </summary>
public class DesignerWaveState : BossState
{
    private readonly EnemyBossDesigner _designer;

    private int   _currentWave;   // 1, 2, 3
    private int   _aliveCount;
    private bool  _waveComplete;

    private readonly List<EliteAgent>   _spawnedAgents  = new List<EliteAgent>();
    private readonly List<NullCyborg>   _spawnedCyborgs = new List<NullCyborg>();
    private readonly List<EliteZeroX01> _spawnedElites  = new List<EliteZeroX01>();

    public DesignerWaveState(BossStateMachine machine) : base(machine)
    {
        _designer = machine as EnemyBossDesigner;
        if (_designer == null)
            Debug.LogError("[DesignerWaveState] BossStateMachine이 EnemyBossDesigner가 아닙니다.");
    }

    public override void Enter()
    {
        _currentWave  = 1;
        _waveComplete = false;
        Machine.StartCoroutine(WaveSequence());
    }

    public override void Tick()
    {
        // 안전망: 이벤트 누락 시 Tick에서 aliveCount 재확인
        if (!_waveComplete && _aliveCount <= 0 && _currentWave > 3)
        {
            _waveComplete = true;
            GoTo(_designer?.DesignerUmbrellaPhase ?? Machine.Vulnerable);
        }
    }

    public override void FixedTick() { }

    public override void Exit()
    {
        UnsubscribeAll();
    }

    // ─── Wave 시퀀스 ──────────────────────────────────────────────────────────

    private IEnumerator WaveSequence()
    {
        // Wave 1: 요원 군단 (다수 EliteAgent)
        yield return SpawnAndWaitWave1();
        _currentWave = 2;

        // Wave 2: NULL 사이보그 (그랩&투척)
        yield return SpawnAndWaitWave2();
        _currentWave = 3;

        // Wave 3: 엘리트 0x01 (관절 타격)
        yield return SpawnAndWaitWave3();
        _currentWave = 4;

        // 모든 Wave 완료 → 설계자 Phase 1 시작
        _waveComplete = true;
        GoTo(_designer?.DesignerUmbrellaPhase ?? Machine.Vulnerable);
    }

    private IEnumerator SpawnAndWaitWave1()
    {
        int count = _designer != null ? _designer.Wave1AgentCount : 4;
        _aliveCount = 0;

        for (int i = 0; i < count; i++)
        {
            var agent = _designer?.SpawnAgent(i);
            if (agent == null) continue;
            _aliveCount++;
            _spawnedAgents.Add(agent);
            agent.OnAgentDied += OnAgentDied;
        }

        yield return new WaitUntil(() => _aliveCount <= 0);
        // Wave 1 클리어 후 짧은 연출 대기
        yield return new WaitForSeconds(0.8f);
    }

    private IEnumerator SpawnAndWaitWave2()
    {
        int count = _designer != null ? _designer.Wave2CyborgCount : 2;
        _aliveCount = 0;

        for (int i = 0; i < count; i++)
        {
            var cyborg = _designer?.SpawnCyborg(i);
            if (cyborg == null) continue;
            _aliveCount++;
            _spawnedCyborgs.Add(cyborg);
            cyborg.OnCyborgDied += OnCyborgDied;
        }

        yield return new WaitUntil(() => _aliveCount <= 0);
        yield return new WaitForSeconds(0.8f);
    }

    private IEnumerator SpawnAndWaitWave3()
    {
        int count = _designer != null ? _designer.Wave3EliteCount : 1;
        _aliveCount = 0;

        for (int i = 0; i < count; i++)
        {
            var elite = _designer?.SpawnElite(i);
            if (elite == null) continue;
            _aliveCount++;
            _spawnedElites.Add(elite);
            elite.OnZeroX01Died += OnEliteDied;
        }

        yield return new WaitUntil(() => _aliveCount <= 0);
        yield return new WaitForSeconds(1.2f); // 설계자 등장 전 긴장감
    }

    // ─── 적 사망 핸들러 ───────────────────────────────────────────────────────

    private void OnAgentDied(EliteAgent a)
    {
        a.OnAgentDied -= OnAgentDied;
        _aliveCount    = Mathf.Max(0, _aliveCount - 1);
    }

    private void OnCyborgDied(NullCyborg c)
    {
        c.OnCyborgDied -= OnCyborgDied;
        _aliveCount     = Mathf.Max(0, _aliveCount - 1);
    }

    private void OnEliteDied(EliteZeroX01 e)
    {
        e.OnZeroX01Died -= OnEliteDied;
        _aliveCount      = Mathf.Max(0, _aliveCount - 1);
    }

    // ─── 정리 ─────────────────────────────────────────────────────────────────

    private void UnsubscribeAll()
    {
        foreach (var a in _spawnedAgents)  { if (a != null) a.OnAgentDied  -= OnAgentDied; }
        foreach (var c in _spawnedCyborgs) { if (c != null) c.OnCyborgDied -= OnCyborgDied; }
        foreach (var e in _spawnedElites)  { if (e != null) e.OnZeroX01Died -= OnEliteDied; }
        _spawnedAgents.Clear();
        _spawnedCyborgs.Clear();
        _spawnedElites.Clear();
    }
}
