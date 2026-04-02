using System.Collections;
using UnityEngine;

/// <summary>
/// Shadow 보스 배회 상태: 퍼즐 리셋 후 플레이어를 천천히 추적하며 압박.
///
/// 방어 설계 (가설 2):
///
/// 가설 2 — HauntState 무한 루프:
///   _hauntTimer는 보스 위치/경로 탐색과 무관한 '절대 시간' 카운터.
///   HauntMaxDuration(EnemyBossShadow 인스펙터에서 설정) 경과 시
///   보스가 안개처럼 FadeOut → PuzzleResetPoint로 워프 → FadeIn 후 퍼즐 강제 재시작.
///   AI가 지형에 갇혀도 타이머는 멈추지 않는다.
///
///   코루틴 타이밍: FadeAndWarp 코루틴이 GoTo를 호출 → BossStateMachine.ChangeState가
///   StopAllCoroutines를 실행하지만 GoTo는 마지막 라인이므로 정상 동작 보장.
/// </summary>
public class ShadowHauntState : BossState
{
    private readonly EnemyBossShadow _shadow;

    private float _hauntTimer;
    private float _zigzagTimer;
    private int   _zigzagDir   = 1;
    private bool  _isFadingOut;        // 가설 2: 페이드 중복 시작 방지

    private const float ZIGZAG_INTERVAL   = 0.8f;
    private const float FADE_OUT_DURATION = 0.5f;
    private const float FADE_IN_DURATION  = 0.3f;

    public ShadowHauntState(BossStateMachine machine) : base(machine)
    {
        _shadow = machine as EnemyBossShadow;
        if (_shadow == null)
            Debug.LogError("[ShadowHauntState] BossStateMachine이 EnemyBossShadow가 아닙니다.");
    }

    public override void Enter()
    {
        _hauntTimer  = 0f;
        _zigzagTimer = 0f;
        _isFadingOut = false;
    }

    public override void Tick()
    {
        if (_isFadingOut) return;

        _hauntTimer  += Time.deltaTime;
        _zigzagTimer += Time.deltaTime;

        if ((Machine.IsPhase2 || Machine.IsPhase3) && _zigzagTimer >= ZIGZAG_INTERVAL)
        {
            _zigzagDir   = -_zigzagDir;
            _zigzagTimer = 0f;
        }

        // 가설 2: 절대 시간 초과 → AI 위치·경로 무관하게 강제 퍼즐 재시작
        float maxDuration = _shadow != null ? _shadow.HauntMaxDuration : 6f;
        if (_hauntTimer >= maxDuration)
        {
            _isFadingOut = true;

            if (Machine.Rb != null)
                Machine.Rb.linearVelocity = Vector2.zero;

            Machine.StartCoroutine(FadeAndWarpRoutine());
        }
    }

    public override void FixedTick()
    {
        if (_isFadingOut) return;
        if (Machine.Rb == null) return;

        if (Machine.PlayerTransform == null)
        {
            Machine.Rb.linearVelocity = Vector2.zero;
            GoTo(Machine.Idle);
            return;
        }

        Vector2 dir   = Machine.GetDirectionToPlayer();
        float   speed = _shadow != null ? _shadow.GetHauntSpeed() : 3f;

        Vector2 perpendicular = new Vector2(-dir.y, dir.x) * _zigzagDir;
        float   zigzagFactor  = (Machine.IsPhase2 || Machine.IsPhase3) ? 0.3f : 0f;

        Vector2 moveDir = (dir + perpendicular * zigzagFactor).normalized;
        Machine.Rb.linearVelocity = moveDir * speed;

        Machine.FacePlayer();
    }

    public override void Exit()
    {
        if (Machine.Rb != null)
            Machine.Rb.linearVelocity = Vector2.zero;
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 가설 2: 보스가 안개처럼 사라지고 퍼즐 시작 위치로 워프 후 퍼즐 강제 재시작.
    /// FadeOut → 위치 이동 → FadeIn → GoTo(GhostPhase) 순서 보장.
    ///
    /// 가설 4: 페이드 진행 중 플레이어 사망 감지.
    /// 각 yield 직전 IsDead 체크 → 사망 시 sr.color 복원 후 즉시 yield break.
    /// (퍼즐 강제 재시작 없이 보스만 대기 상태로 남음 → 데스 씬이 우선권을 가짐)
    /// </summary>
    private IEnumerator FadeAndWarpRoutine()
    {
        var sr = Machine.GetComponentInChildren<SpriteRenderer>();
        Color originalColor = sr != null ? sr.color : Color.white;

        // 가설 4: 페이드 진행 중 사망 감지용 PlayerHealth 캐싱
        var playerHealth = Machine.PlayerTransform != null
            ? Machine.PlayerTransform.GetComponent<PlayerHealth>()
            : null;

        // ── 1. 페이드 아웃 ────────────────────────────────────────────────────
        float t = 0f;
        while (t < 1f)
        {
            // 가설 4: 사망 시 코루틴 즉시 중단 — 퍼즐 재시작 없이 색상만 복원
            if (playerHealth != null && playerHealth.IsDead)
            {
                if (sr != null) sr.color = originalColor;
                yield break;
            }

            t += Time.unscaledDeltaTime / FADE_OUT_DURATION;
            if (sr != null)
            {
                Color c = originalColor;
                c.a = Mathf.Lerp(originalColor.a, 0f, t);
                sr.color = c;
            }
            yield return null;
        }

        // 워프 직전에도 사망 재확인
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (sr != null) sr.color = originalColor;
            yield break;
        }

        // ── 2. 퍼즐 시작 위치로 워프 ─────────────────────────────────────────
        if (_shadow?.PuzzleResetPoint != null)
            Machine.transform.position = _shadow.PuzzleResetPoint.position;

        // ── 3. 페이드 인 ─────────────────────────────────────────────────────
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / FADE_IN_DURATION;
            if (sr != null)
            {
                Color c = originalColor;
                c.a = Mathf.Lerp(0f, originalColor.a, t);
                sr.color = c;
            }
            yield return null;
        }

        if (sr != null) sr.color = originalColor;

        // ── 4. 퍼즐 강제 재시작 ──────────────────────────────────────────────
        GoTo(_shadow?.ShadowGhostPhase ?? Machine.Idle);
    }
}
