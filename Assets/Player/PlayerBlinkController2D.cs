using UnityEngine;

/// <summary>
/// 2D 액션 게임용 단검 블링크 컨트롤러.
/// - 마우스 클릭: 단검 투척
/// - Shift: 단검 위치로 블링크 (공중 1회 제한)
/// - 3대 방어 로직 포함
/// </summary>
public class PlayerBlinkController2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D playerRb;
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private MovementData _movementData;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform firePoint; // 단검 발사 지점
    [SerializeField] private Collider2D playerCollider;

    [Header("Ground / Wall Check")]
    [Tooltip("비우면 Layers.PlayerPhysicsGroundMask 사용.")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask wallMask;

    [Header("Pooling (optional)")]
    [Tooltip("SimpleGameObjectPool의 프리팹은 WeaponData.daggerProjectilePrefab과 동일해야 한다.")]
    [SerializeField] private SimpleGameObjectPool _daggerPool;

    private LayerMask EffectiveGroundMask =>
        groundMask.value != 0 ? groundMask : Layers.PlayerPhysicsGroundMask;

    [Header("Air Blink Settings")]
    [SerializeField] private int maxAirBlinkCount = 1;
    [SerializeField] private int currentAirBlinkCount;

    [Header("Blink Trail Visual")]
    [Tooltip("블링크 잔상 LineRenderer용 머티리얼. 비워두면 Awake에서 Sprites/Default로 자동 생성.")]
    [SerializeField] private Material _blinkTrailMaterial;

    /// <summary>
    /// 블링크 실행 완료 시 발화. 인자: (출발지, 도착지).
    /// Shadow 보스 잔상 퍼즐·BlinkGhostMarker, TriggerCutscene 블링크 스킵 감지에서 구독.
    /// </summary>
    public System.Action<Vector2, Vector2> OnBlinkExecuted;

    /// <summary>Grab 전이 연결용. 같은 GameObject의 PlayerStateMachine을 Awake에서 캐싱.</summary>
    private PlayerStateMachine _stateMachine;

    private bool isGrounded;
    private bool isOnWall;

    private DaggerProjectile2D currentDagger;

    private Camera mainCam;
    private Plane aimPlane = new Plane(Vector3.forward, Vector3.zero);

    // 히트 스톱 중복 실행 방지
    private bool _isHitStopping;

    /// <summary>단검 조준 플립 직후 이동 플립이 덮어쓰지 않도록 사용.</summary>
    private float _lastAttackFlipGameTime = -999f;

    // 블링크 후 무적(I-frame) 관리
    private Coroutine _invincibleCoroutine;
    private Coroutine _cameraShakeCoroutine;
    private float _originalAlpha = 1f;
    private int _originalLayer;

    // Coyote / Input Buffer
    private float _lastGroundedOrWallTime = -999f;
    private float _jumpInputBufferedUntil = -1f;
    private float _lastThrowTime = -999f;

    private static readonly RaycastHit2D[] BlinkSweepHits = new RaycastHit2D[16];
    private static readonly Collider2D[] BlinkOverlapBuffer = new Collider2D[16];
    private static readonly int[] BlinkProcessedInstanceIds = new int[32];

    private void Awake()
    {
        if (playerRb == null)
            playerRb = GetComponent<Rigidbody2D>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (playerCollider == null)
            playerCollider = GetComponent<Collider2D>();

        _originalLayer = gameObject.layer;
        if (spriteRenderer != null)
        {
            _originalAlpha = spriteRenderer.color.a;
        }

        mainCam = Camera.main;
        ResetAirBlinkCount();
        _stateMachine = GetComponent<PlayerStateMachine>();

        if (_blinkTrailMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                Debug.LogError("[PlayerBlinkController2D] Shader 'Sprites/Default' 없음. 인스펙터에서 BlinkTrailMaterial을 직접 할당하세요.");
            else
                _blinkTrailMaterial = new Material(shader);
        }
    }

    /// <summary>히트스톱 중에는 이동 스크립트가 velocity.x 등을 건드리지 않도록.</summary>
    public bool IsHitStopBlockingMovement =>
        HitStopManager.Instance != null && HitStopManager.Instance.IsActive;

    /// <summary>UpdateCharacterFlip로 방향이 바뀐 시각(이동 플립 억제용).</summary>
    public float LastAttackFlipGameTime => _lastAttackFlipGameTime;

    /// <summary>점프는 착지 가능 시에만 소비해야 하므로, 소비 전에 버퍼 존재 여부만 확인.</summary>
    public bool HasBufferedJumpInput() => _jumpInputBufferedUntil > Time.time;

    /// <summary>이동용 지면 판정과 동기화할 때 공중 블링크 횟수를 복구.</summary>
    public void SyncAirBlinkAfterFloorLanding()
    {
        ResetAirBlinkCount();
    }

    /// <summary>벽 타기에 붙었을 때 공중 블링크 횟수를 복구.</summary>
    public void SyncAirBlinkAfterWallAttach()
    {
        ResetAirBlinkCount();
    }

    /// <summary>현재 비행 중인 단검. BlinkState가 소멸 감지에 사용.</summary>
    public DaggerProjectile2D CurrentDagger => currentDagger;

    /// <summary>
    /// true일 때 ThrowDagger() 내부의 ChangeState(Blink) 호출을 억제한다.
    /// PlayerBoundState가 Enter 시 true로 설정하고 Exit 시 반드시 false로 복구해야 한다.
    /// </summary>
    public bool SuppressStateChangeOnThrow { get; set; }

    private void Update()
    {
        // GrabState · SlamState 진행 중 단검 투척·블링크 입력 차단
        // 자기전이(Grab→Grab) 및 Slam 도중 2차 블링크 오발사 방지
        if (IsGrabOrSlamActive())
            return;

        HandleThrowInput();
        // Shift 블링크 입력은 BlinkState.Tick()으로 이관됨 — 여기서는 처리하지 않음
        HandleJumpBufferInput();
    }

    /// <summary>FSM이 Grab 또는 Slam 상태이면 true.</summary>
    private bool IsGrabOrSlamActive()
    {
        if (_stateMachine == null) return false;
        return _stateMachine.CurrentState is GrabState
               || _stateMachine.CurrentState is SlamState;
    }

    #region Input

    private void HandleThrowInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            ThrowDagger();
        }
    }

    private void HandleJumpBufferInput()
    {
        // 점프 실제 실행은 다른 이동 스크립트에서 담당하더라도,
        // 입력 예약 타임스탬프를 여기서 관리하면 같은 버퍼 윈도우를 공유할 수 있다.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _jumpInputBufferedUntil = Time.time + GetInputBufferTime();
        }
    }

    #endregion

    #region Throw & Blink Logic


    private void ThrowDagger()
    {
        // WeaponData가 없으면 모든 수치를 읽을 수 없으므로 가장 먼저 체크
        if (weaponData == null || weaponData.daggerProjectilePrefab == null)
            return;

        // 투척 쿨타임 (WeaponData.blinkCooldown 기준)
        if (Time.time < _lastThrowTime + weaponData.blinkCooldown) return;

        // 이미 던진 단검이 맵에 존재하면 새로 던질 수 없음
        if (currentDagger != null) return;

        // 공중 체공 제한 해제 (산나비식 스윙을 위해 주석 처리)
        bool isInAir = IsAirborneForBlinkRules();
        // if (isInAir && currentAirBlinkCount <= 0) return;
        // if (isInAir) currentAirBlinkCount--;

        if (mainCam == null)
            mainCam = Camera.main;
        if (mainCam == null)
            return;

        Ray aimRay = mainCam.ScreenPointToRay(Input.mousePosition);
        if (!aimPlane.Raycast(aimRay, out float enter))
            return;
        Vector3 mouseWorldPos = aimRay.GetPoint(enter);

        UpdateCharacterFlip(mouseWorldPos);

        Vector2 firePosition = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 direction = ((Vector2)mouseWorldPos - firePosition);
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;
        direction.Normalize();

        // 무한 헬리콥터 체공 방지 (공중 투척 시 WeaponData.airThrowFallSpeed 이상 하강 보장)
        if (isInAir && playerRb != null)
        {
            float fallCap = -weaponData.airThrowFallSpeed;
            playerRb.linearVelocity = new Vector2(
                playerRb.linearVelocity.x,
                Mathf.Min(playerRb.linearVelocity.y, fallCap)
            );
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angle);

        SimpleGameObjectPool poolForThrow = null;
        if (_daggerPool != null)
        {
            if (_daggerPool.PooledPrefab == weaponData.daggerProjectilePrefab)
                poolForThrow = _daggerPool;
            else if (_daggerPool.PooledPrefab != null)
            {
                Debug.LogWarning(
                    "[PlayerBlinkController2D] Dagger pool prefab ≠ WeaponData.daggerProjectilePrefab — Instantiate로 폴백.");
            }
        }

        GameObject daggerObj = poolForThrow != null
            ? poolForThrow.Get(firePosition, spawnRotation)
            : Instantiate(weaponData.daggerProjectilePrefab, firePosition, spawnRotation);

        currentDagger = daggerObj.GetComponent<DaggerProjectile2D>();
        if (currentDagger != null)
        {
            currentDagger.SetReleasePool(poolForThrow);
            currentDagger.Launch(firePosition, direction, weaponData);

            // 투척 성공 시 쿨타임 갱신 + BlinkState 전이
            _lastThrowTime = Time.time;

            // PlayerBoundState 활성 중에는 BlinkState 전이 억제 (구속 탈출 전용 흐름 유지)
            if (_stateMachine != null && !SuppressStateChangeOnThrow)
                _stateMachine.ChangeState(_stateMachine.Blink);
        }
    }
    /// <summary>
    /// DaggerProjectile2D 적중 등 외부 트리거로 즉시 블링크를 실행한다.
    /// 공중 블링크 소모·코요테 규칙을 모두 준수한다.
    /// </summary>
    public bool ImmediateBlink() => TryBlinkToDagger();

    public bool TryBlinkToDagger()
    {
        if (currentDagger == null || !currentDagger.CanBlink)
            return false;

        // 설계자 우산에 반사된 단검 — 블링크 원천 차단
        if (currentDagger.IsReflected)
            return false;

        // 공중 블링크 제한 없음 — "단검 1개 = 블링크 1회" 규칙이 자연 제한 역할
        // 단검이 없으면 ImmediateBlink/TryBlinkToDagger 자체가 false 반환하므로 무한 체공 불가

        Vector2 startPos = transform.position;
        Vector2 targetPos = currentDagger.CurrentPosition;
        Vector2 finalPos = targetPos;

        // 1) 끼임 방지: 단검이 벽에 박힌 상태면 Normal 방향으로 0.5 유닛 Offset
        if (currentDagger.IsStuckToWall)
        {
            finalPos += CalculateWallSafeOffset(currentDagger.LastHitNormal);
        }

        // 블링크 순간 속도 0으로 초기화 (Snappy한 조작감)
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
        }

        // 실제 위치 이동
        transform.position = finalPos;

        ProcessBlinkEnemyInteractions(startPos, finalPos);

        // 블링크 경로 잔상 연출
        SpawnBlinkTrail(startPos, finalPos);
        CreateGhost(startPos, finalPos);
        TriggerCameraShake();

        // 도착 후 무적(I-frame) 시작
        StartBlinkInvincibility();

        // 히트 스톱 & 검붉은 누아르 이펙트
        StartCoroutine(PerformHitStop());

        // 단검은 즉시 회수 (풀 또는 Destroy)
        currentDagger.ReleaseToPoolOrDestroy();
        currentDagger = null;

        SoundManager.Instance?.PlayBlink();

        // Shadow 보스 잔상 퍼즐, TriggerCutscene 블링크 스킵 감지에서 구독.
        OnBlinkExecuted?.Invoke(startPos, finalPos);

        return true;
    }

    #endregion

    /// <summary>
    /// 물리 접촉 벽(isOnWall)이 없어도 PlayerMovement2D 벽 타기는 벽으로 간주해 공중 블링크 소모를 막는다.
    /// </summary>
    private bool IsAirborneForBlinkRules()
    {
        bool hasCoyoteGrace = (Time.time - _lastGroundedOrWallTime) <= GetCoyoteTime();
        bool onWallOrClimb = isOnWall || (playerMovement != null && playerMovement.IsWallClimbing);
        return !isGrounded && !onWallOrClimb && !hasCoyoteGrace;
    }

    /// <summary>
    /// 블링크 경로 스윕 + 도착 오버랩으로 적 타격. NonAlloc만 사용.
    /// </summary>
    private void ProcessBlinkEnemyInteractions(Vector2 from, Vector2 to)
    {
        if (weaponData == null || weaponData.blinkEnemyLayerMask.value == 0)
            return;

        int mask = weaponData.blinkEnemyLayerMask.value;
        float sweepR = weaponData.blinkEnemySweepRadius;
        float destR = weaponData.blinkDestinationEnemyRadius;

        int processed = 0;
        bool rewarded = false;

        Vector2 delta = to - from;
        float mag = delta.magnitude;
        Vector2 dir = mag > 1e-5f ? delta / mag : Vector2.right;

        int sweepCount = Physics2D.CircleCastNonAlloc(from, sweepR, dir, BlinkSweepHits, mag, mask);
        for (int i = 0; i < sweepCount; i++)
        {
            Collider2D c = BlinkSweepHits[i].collider;
            if (c == null) continue;
            int id = c.gameObject.GetInstanceID();
            if (!TryRegisterBlinkProcessed(id, ref processed)) continue;
            if (ApplyBlinkHitToEnemy(c.gameObject, ref rewarded)) return; // Grab 트리거 시 즉시 중단
        }

        int overlapCount = Physics2D.OverlapCircleNonAlloc(to, destR, BlinkOverlapBuffer, mask);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D c = BlinkOverlapBuffer[i];
            if (c == null) continue;
            int id = c.gameObject.GetInstanceID();
            if (!TryRegisterBlinkProcessed(id, ref processed)) continue;
            if (ApplyBlinkHitToEnemy(c.gameObject, ref rewarded)) return; // Grab 트리거 시 즉시 중단
        }
    }

    private bool TryRegisterBlinkProcessed(int instanceId, ref int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (BlinkProcessedInstanceIds[i] == instanceId)
                return false;
        }

        if (count >= BlinkProcessedInstanceIds.Length)
            return false;

        BlinkProcessedInstanceIds[count++] = instanceId;
        return true;
    }

    /// <returns>true = Grab 트리거 완료 (이후 루프 즉시 중단). false = 일반 처형 처리.</returns>
    private bool ApplyBlinkHitToEnemy(GameObject hitObject, ref bool rewarded)
    {
        if (weaponData == null) return false;

        // ── Grab 분기 ─────────────────────────────────────────────────────────
        // IGrabbable 구현체(EnemyHealth / BossHealth / HydraulicPiston) 통합 처리
        var grabbable = hitObject.GetComponentInParent<IGrabbable>();
        // Lives >= 2 인 적만 그랩 가능. Lives 1 짜리는 블링크 도착 즉시 척살 분기로 내려감.
        bool canGrab = grabbable != null && grabbable.IsGrabbable && grabbable.CurrentLives >= 2;
        if (canGrab)
        {
            if (_stateMachine == null)
            {
                Debug.LogWarning("[PlayerBlinkController2D] PlayerStateMachine 캐시가 없어 Grab 전이 불가. 처형으로 대체.");
            }
            else
            {
                _stateMachine.Grab.SetTarget(grabbable);
                _stateMachine.ChangeState(_stateMachine.Grab);

                // 그랩 성공도 공중 블링크 회복 보상 부여
                if (weaponData.blinkKillRefillsAirBlink && !rewarded)
                {
                    ResetAirBlinkCount();
                    rewarded = true;
                }

                return true; // 루프 조기 종료 신호
            }
        }

        // ── 일반 처형 분기 ────────────────────────────────────────────────────
        var health = hitObject.GetComponentInParent<IHealth>();
        if (health != null)
        {
            health.TakeDamage(weaponData.blinkExecutionDamage);
            if (weaponData.blinkKillRefillsAirBlink && !rewarded)
            {
                ResetAirBlinkCount();
                rewarded = true;
            }

            return false;
        }

        if (!weaponData.blinkInstantKillDestroysEnemyWithoutHealth)
            return false;

        if (weaponData.blinkKillRefillsAirBlink && !rewarded)
        {
            ResetAirBlinkCount();
            rewarded = true;
        }

        Destroy(hitObject.transform.root.gameObject);
        return false;
    }

    #region Ground / Wall Check (무한 체공 방지)

    private void OnCollisionEnter2D(Collision2D collision)
    {
        int layer = collision.gameObject.layer;
        int layerBit = 1 << layer;
        // Dagger layer 추가 -> 단검 충돌 무시
        if (collision.gameObject.layer == Layers.Dagger) return;

        // OnCollisionEnter2D + LayerMask를 사용해 지면/벽 판정
        if ((EffectiveGroundMask.value & layerBit) != 0)
        {
            isGrounded = true;
            isOnWall = false;
            _lastGroundedOrWallTime = Time.time;
            ResetAirBlinkCount();
            ClearBufferedInputs();
        }
        else if ((wallMask.value & layerBit) != 0)
        {
            isOnWall = true;
            isGrounded = false;
            _lastGroundedOrWallTime = Time.time;
            ResetAirBlinkCount();
            ClearBufferedInputs();
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        int layer = collision.gameObject.layer;
        int layerBit = 1 << layer;

        if ((EffectiveGroundMask.value & layerBit) != 0)
        {
            isGrounded = false;
        }
        else if ((wallMask.value & layerBit) != 0)
        {
            isOnWall = false;
        }
    }

    private void ResetAirBlinkCount()
    {
        currentAirBlinkCount = maxAirBlinkCount;
    }

    /// <summary>
    /// 패리 성공 시 PlayerParryController2D가 호출.
    /// 공중 블링크 게이지를 amount만큼 충전 (최대치 초과 불가).
    /// </summary>
    public void RefillAirBlink(int amount = 1)
    {
        if (amount <= 0) return;
        currentAirBlinkCount = Mathf.Min(currentAirBlinkCount + amount, maxAirBlinkCount);
    }

    private void ClearBufferedInputs()
    {
        // Misfire 방지: 지면/벽 접촉 순간 예약 입력을 전부 비운다.
        _jumpInputBufferedUntil = -1f;
    }

    public bool ConsumeBufferedJumpInput()
    {
        // 이동 스크립트가 착지 직후 호출하면 입력 버퍼링 효과를 얻을 수 있다.
        if (_jumpInputBufferedUntil > Time.time)
        {
            _jumpInputBufferedUntil = -1f;
            return true;
        }

        return false;
    }

    private float GetCoyoteTime()
    {
        return _movementData != null ? _movementData.coyoteTime : 0.1f;
    }

    private float GetInputBufferTime()
    {
        return _movementData != null ? _movementData.inputBufferTime : 0.1f;
    }

    #endregion

    #region Blink Trail (Hex #800000)

    /// <summary>
    /// 블링크 경로에 잔상을 남기는 단순 연출.
    /// LineRenderer를 잠깐 생성했다가 제거.
    /// </summary>
    private void SpawnBlinkTrail(Vector2 from, Vector2 to)
    {
        GameObject trailObj = new GameObject("BlinkTrail");
        var line = trailObj.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.SetPosition(0, from);
        line.SetPosition(1, to);

        // Awake에서 1회 생성된 캐시 사용 — Shader.Find / new Material 런타임 호출 없음
        if (_blinkTrailMaterial != null)
            line.material = _blinkTrailMaterial;

        // Hex #800000 → RGB(128, 0, 0)
        Color c = new Color(128f / 255f, 0f, 0f, 1f);
        line.startColor = c;
        line.endColor = c;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;

        // 짧게 유지 후 제거
        Destroy(trailObj, 0.1f);
    }

    /// <summary>
    /// 블링크 이동 경로에 플레이어 실루엣 잔상을 잠깐 남긴다.
    /// </summary>
    private void CreateGhost(Vector2 from, Vector2 to)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        GameObject ghost = new GameObject("BlinkGhost");
        ghost.transform.position = from;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        SpriteRenderer ghostSr = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite = spriteRenderer.sprite;
        ghostSr.flipX = spriteRenderer.flipX;
        ghostSr.flipY = spriteRenderer.flipY;
        ghostSr.sortingLayerID = spriteRenderer.sortingLayerID;
        ghostSr.sortingOrder = spriteRenderer.sortingOrder - 1;

        float ghostLife = weaponData != null ? weaponData.ghostDuration : 0.1f;
        Color ghostColor = new Color(128f / 255f, 0f, 0f, 0.5f); // #800000
        ghostSr.color = ghostColor;

        // 코루틴 대신 초경량 컴포넌트로 페이드 처리해 GC 부담을 낮춤.
        GhostFade fade = ghost.AddComponent<GhostFade>();
        fade.Initialize(ghostSr, ghostLife);
    }

    private void TriggerCameraShake()
    {
        float duration  = weaponData != null ? weaponData.cameraShakeDuration  : 0f;
        float intensity = weaponData != null ? weaponData.cameraShakeIntensity : 0f;

        if (CameraShaker.Instance != null)
        {
            CameraShaker.Instance.Shake(duration, intensity);
            return;
        }

        // CameraShaker가 씬에 없을 경우 폴백: Camera.main 직접 흔들기
        if (mainCam == null) return;
        if (_cameraShakeCoroutine != null)
            StopCoroutine(_cameraShakeCoroutine);
        _cameraShakeCoroutine = StartCoroutine(FallbackShakeRoutine(duration, intensity));
    }

    private System.Collections.IEnumerator FallbackShakeRoutine(float duration, float intensity)
    {
        if (mainCam == null) yield break;
        Transform camT = mainCam.transform;
        Vector3 origin = camT.localPosition;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            Vector2 offset = Random.insideUnitCircle * intensity;
            camT.localPosition = origin + new Vector3(offset.x, offset.y, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        camT.localPosition   = origin;
        _cameraShakeCoroutine = null;
    }

    private Vector2 CalculateWallSafeOffset(Vector2 hitNormal)
    {
        Vector2 normal = hitNormal.normalized;
        float baseOffset   = weaponData != null ? weaponData.blinkWallSafeOffset    : 0.5f;
        float safetyMargin = weaponData != null ? weaponData.blinkWallSafetyMargin  : 0.02f;

        if (playerCollider == null)
            return normal * (baseOffset + safetyMargin);

        // 콜라이더 반경을 Normal 축에 투영해 벽 끼임을 방지한다.
        Vector2 ext = playerCollider.bounds.extents;
        float projectedHalfSize = Mathf.Abs(normal.x) * ext.x + Mathf.Abs(normal.y) * ext.y;
        return normal * (baseOffset + projectedHalfSize + safetyMargin);
    }

    private void UpdateCharacterFlip(Vector3 targetWorldPos)
    {
        Vector3 localScale = transform.localScale;
        float dx = targetWorldPos.x - transform.position.x;
        if (Mathf.Abs(dx) <= 0.001f) return;

        float sign = dx >= 0f ? 1f : -1f;
        float newX = Mathf.Abs(localScale.x) * sign;
        if (Mathf.Abs(localScale.x - newX) > 0.0001f)
            _lastAttackFlipGameTime = Time.time;
        localScale.x = newX;
        transform.localScale = localScale;
    }

    #endregion

    #region Hit Stop

    /// <summary>
    /// 블링크 직후 짧은 히트 스톱 연출.
    /// timeScale 조작은 HitStopManager에 위임. 파티클 스폰 중복 방지만 로컬 처리.
    /// </summary>
    private System.Collections.IEnumerator PerformHitStop()
    {
        if (_isHitStopping)
            yield break;

        _isHitStopping = true;

        if (weaponData != null && weaponData.blinkEffectPrefab != null)
        {
            var ps = Instantiate(weaponData.blinkEffectPrefab, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + 0.5f);
        }

        float hitStopDuration = weaponData != null ? weaponData.hitStopDuration  : 0.05f;
        float hitStopScale    = weaponData != null ? weaponData.hitStopTimeScale : 0.1f;

        // timeScale 관리를 HitStopManager에 위임 (PlayerHealth와 동시 발생 시 병합 처리)
        HitStopManager.Instance?.Request(hitStopDuration, hitStopScale);

        float elapsed = 0f;
        while (elapsed < hitStopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _isHitStopping = false;
    }

    /// <summary>
    /// 나중에 EffectManager에서 블링크 관련 사운드를 재생할 때,
    /// Time.timeScale의 영향을 받지 않도록 pitch를 고정하는 방어용 헬퍼.
    /// </summary>
    private void PlayBlinkSoundSafe(AudioSource source)
    {
        if (source == null) return;

        source.pitch = 1f; // 히트 스톱 중에도 재생 속도 유지
        source.Play();
    }

    #endregion

    #region Blink Invincibility (I-frame)

    private void StartBlinkInvincibility()
    {
        if (weaponData == null) return;

        // 기존 코루틴이 돌고 있으면 중복 실행 방지를 위해 종료
        if (_invincibleCoroutine != null)
        {
            StopCoroutine(_invincibleCoroutine);
            _invincibleCoroutine = null;
        }

        _invincibleCoroutine = StartCoroutine(PerformBlinkInvincibility());
    }

    /// <summary>
    /// 블링크 도착 직후 일정 시간 동안 무적 레이어로 전환.
    /// WaitForSecondsRealtime을 사용하여 히트 스톱(0.05f) 포함 시간 동안 유지.
    /// </summary>
    private System.Collections.IEnumerator PerformBlinkInvincibility()
    {
        // Layers.cs 상수를 직접 사용 — WeaponData string 필드 제거
        // 주의: Project Settings > Physics 2D 에서 PlayerInvincible 레이어 충돌 매트릭스를 설정해야
        // 적 투사체/트랩과의 충돌 무시가 정상 동작한다.
        int invLayer  = Layers.PlayerInvincible;
        int origLayer = Layers.Player;

        if (invLayer == -1 || origLayer == -1)
        {
            Debug.LogWarning("[PlayerBlinkController2D] Layers.PlayerInvincible 또는 Layers.Player 가 -1입니다. " +
                             "Project Settings > Tags and Layers에서 레이어를 확인하세요.");
            yield break;
        }

        // 원본 레이어/알파 백업
        _originalLayer = origLayer;
        float backupAlpha = spriteRenderer != null ? spriteRenderer.color.a : _originalAlpha;

        // 무적 레이어로 전환
        gameObject.layer = invLayer;

        // 시각적 피드백: WeaponData.invincibleAlpha 로 반투명 처리
        if (spriteRenderer != null)
        {
            _originalAlpha = backupAlpha;
            Color c = spriteRenderer.color;
            c.a = weaponData != null ? weaponData.invincibleAlpha : 0.5f;
            spriteRenderer.color = c;
        }

        // 무적 유지 시간: invincibleDuration + hitStopDuration (hitStop이 끝날 때까지 보장)
        float hitStop  = weaponData != null ? weaponData.hitStopDuration : 0.05f;
        float duration = weaponData != null ? weaponData.invincibleDuration + hitStop : 0.2f;
        yield return new WaitForSecondsRealtime(duration);

        RestoreInvincibilityState();
        _invincibleCoroutine = null;
    }

    private void RestoreInvincibilityState()
    {
        // 레이어 복구
        gameObject.layer = _originalLayer;

        // 알파 복구
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = _originalAlpha;
            spriteRenderer.color = c;
        }
    }

    private void OnDisable()
    {
        // 비활성화 시 무적 상태 정리
        if (_invincibleCoroutine != null)
        {
            StopCoroutine(_invincibleCoroutine);
            _invincibleCoroutine = null;
        }

        RestoreInvincibilityState();
    }

    private void OnDestroy()
    {
        // 파괴 시에도 레이어/알파가 이상하게 남지 않도록 강제 원복
        RestoreInvincibilityState();
    }

    #endregion
}

