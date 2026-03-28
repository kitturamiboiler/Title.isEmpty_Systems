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
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform firePoint; // 단검 발사 지점
    [SerializeField] private Collider2D playerCollider;

    [Header("Ground / Wall Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask wallMask;

    [Header("Air Blink Settings")]
    [SerializeField] private int maxAirBlinkCount = 1;
    private int currentAirBlinkCount;

    private bool isGrounded;
    private bool isOnWall;

    private DaggerProjectile2D currentDagger;

    private Camera mainCam;
    private Plane aimPlane = new Plane(Vector3.forward, Vector3.zero);

    // 히트 스톱 중복 실행 방지
    private bool _isHitStopping;

    // 블링크 후 무적(I-frame) 관리
    private Coroutine _invincibleCoroutine;
    private Coroutine _cameraShakeCoroutine;
    private float _originalAlpha = 1f;
    private int _originalLayer;

    // Coyote / Input Buffer
    private float _lastGroundedOrWallTime = -999f;
    private float _jumpInputBufferedUntil = -1f;

    private void Awake()
    {
        if (playerRb == null)
            playerRb = GetComponent<Rigidbody2D>();

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
    }

    private void Update()
    {
        HandleThrowInput();
        HandleBlinkInput();
        HandleJumpBufferInput();
    }

    #region Input

    private void HandleThrowInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            ThrowDagger();
        }
    }

    private void HandleBlinkInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            // Blink는 오발사를 막기 위해 버퍼링하지 않고 즉시 1회 시도만 한다.
            TryBlinkToDagger();
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
        if (weaponData == null || weaponData.daggerProjectilePrefab == null)
            return;

        if (mainCam == null)
            mainCam = Camera.main;
        if (mainCam == null)
            return;

        // 기존 단검이 있다면 먼저 회수
        if (currentDagger != null)
        {
            Destroy(currentDagger.gameObject);
            currentDagger = null;
        }

        // 카메라 타입(Ortho/Perspective)과 무관하게 정확한 조준을 위한 Ray-Plane Cast
        Ray aimRay = mainCam.ScreenPointToRay(Input.mousePosition);
        if (!aimPlane.Raycast(aimRay, out float enter))
            return;
        Vector3 mouseWorldPos = aimRay.GetPoint(enter);

        // 실행 순서 보장: 방향 전환(Flip) 후 firePoint 위치를 읽어 발사한다.
        UpdateCharacterFlip(mouseWorldPos);

        Vector2 firePosition = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 direction = ((Vector2)mouseWorldPos - firePosition);
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;
        direction.Normalize();

        // 2D 투사체가 진행 방향을 바라보도록 회전 동기화
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angle);

        GameObject daggerObj = Instantiate(
            weaponData.daggerProjectilePrefab,
            firePosition,
            spawnRotation
        );

        currentDagger = daggerObj.GetComponent<DaggerProjectile2D>();
        if (currentDagger != null)
        {
            currentDagger.Launch(firePosition, direction, weaponData);
        }
    }

    private bool TryBlinkToDagger()
    {
        if (currentDagger == null || !currentDagger.CanBlink)
            return false;

        // 코요테 타임: 땅/벽을 막 벗어난 뒤 짧은 시간은 지상 판정처럼 취급
        bool hasCoyoteGrace = (Time.time - _lastGroundedOrWallTime) <= GetCoyoteTime();
        bool isInAir = !isGrounded && !isOnWall && !hasCoyoteGrace;
        if (isInAir && currentAirBlinkCount <= 0)
        {
            // 공중 블링크 횟수 초과
            return false;
        }

        if (isInAir)
        {
            // 블링크 실행 시 즉시 소모
            currentAirBlinkCount--;
        }

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
            playerRb.velocity = Vector2.zero;
        }

        // 실제 위치 이동
        transform.position = finalPos;

        // 블링크 경로 잔상 연출
        SpawnBlinkTrail(startPos, finalPos);
        CreateGhost(startPos, finalPos);
        TriggerCameraShake();

        // 도착 후 무적(I-frame) 시작
        StartBlinkInvincibility();

        // 히트 스톱 & 검붉은 누아르 이펙트
        StartCoroutine(PerformHitStop());

        // 단검은 즉시 파괴(회수)
        Destroy(currentDagger.gameObject);
        currentDagger = null;
        return true;
    }

    #endregion

    #region Ground / Wall Check (무한 체공 방지)

    private void OnCollisionEnter2D(Collision2D collision)
    {
        int layer = collision.gameObject.layer;
        int layerBit = 1 << layer;

        // OnCollisionEnter2D + LayerMask를 사용해 지면/벽 판정
        if ((groundMask.value & layerBit) != 0)
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

        if ((groundMask.value & layerBit) != 0)
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
        return weaponData != null ? weaponData.coyoteTime : 0.1f;
    }

    private float GetInputBufferTime()
    {
        return weaponData != null ? weaponData.inputBufferTime : 0.1f;
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

        // 머티리얼이 없으면 기본 머티리얼 생성
        line.material = new Material(Shader.Find("Sprites/Default"));

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
        if (mainCam == null) return;

        if (_cameraShakeCoroutine != null)
        {
            StopCoroutine(_cameraShakeCoroutine);
        }

        _cameraShakeCoroutine = StartCoroutine(PerformCameraShake());
    }

    private System.Collections.IEnumerator PerformCameraShake()
    {
        if (mainCam == null) yield break;

        Transform camTransform = mainCam.transform;
        Vector3 originalPos = camTransform.localPosition;
        float duration = weaponData != null ? weaponData.cameraShakeDuration : 0.06f;
        float intensity = weaponData != null ? weaponData.cameraShakeIntensity : 0.08f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector2 offset = Random.insideUnitCircle * intensity;
            camTransform.localPosition = originalPos + new Vector3(offset.x, offset.y, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        camTransform.localPosition = originalPos;
        _cameraShakeCoroutine = null;
    }

    private Vector2 CalculateWallSafeOffset(Vector2 hitNormal)
    {
        Vector2 normal = hitNormal.normalized;
        float baseOffset = 0.5f;
        float safetyMargin = 0.02f;

        if (playerCollider == null)
        {
            return normal * (baseOffset + safetyMargin);
        }

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
        localScale.x = Mathf.Abs(localScale.x) * sign;
        transform.localScale = localScale;
    }

    #endregion

    #region Hit Stop

    /// <summary>
    /// 블링크 직후 짧은 히트 스톱 연출.
    /// Time.timeScale을 잠시 0.1로 내렸다 복구.
    /// </summary>
    private System.Collections.IEnumerator PerformHitStop()
    {
        if (_isHitStopping)
            yield break;

        _isHitStopping = true;

        // 검붉은 누아르 블링크 이펙트 소환
        if (weaponData != null && weaponData.blinkEffectPrefab != null)
        {
            // 플레이어 위치에서 짧게 터뜨리는 파티클
            var ps = Instantiate(weaponData.blinkEffectPrefab, transform.position, Quaternion.identity);
            ps.Play();
        }

        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.1f;

        // timeScale에 영향을 받지 않도록 realtime 기준 대기
        float hitStopDuration = 0.05f;
        float elapsed = 0f;
        while (elapsed < hitStopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = originalTimeScale;
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
        // 중요: Project Settings > Physics 2D 에서 PlayerInvincible 레이어 충돌 매트릭스를 설정해야
        // 적 투사체/트랩과의 충돌 무시가 정상 동작한다.
        // 레이어 이름을 실제 레이어 인덱스로 변환
        int invLayer = LayerMask.NameToLayer(weaponData.invincibleLayerName);
        int origLayer = LayerMask.NameToLayer(weaponData.originalLayerName);

        if (invLayer == -1 || origLayer == -1)
        {
            Debug.LogWarning("Invincibility layer 설정이 잘못되었습니다. Project Settings > Tags and Layers에서 레이어를 확인하세요.");
            yield break;
        }

        // 원본 레이어/알파 백업
        _originalLayer = origLayer;
        float backupAlpha = _originalAlpha;
        if (spriteRenderer != null)
        {
            backupAlpha = spriteRenderer.color.a;
        }

        // 무적 레이어로 전환
        gameObject.layer = invLayer;

        // 시각적 피드백: 알파 0.5f로 반투명 처리
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            _originalAlpha = backupAlpha;
            c.a = 0.5f;
            spriteRenderer.color = c;
        }

        // 무적 유지 시간: invincibleDuration + 히트 스톱 0.05f
        float duration = weaponData.invincibleDuration + 0.05f;
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

internal sealed class GhostFade : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private float _duration;
    private float _elapsed;
    private Color _baseColor;

    public void Initialize(SpriteRenderer renderer, float duration)
    {
        _renderer = renderer;
        _duration = Mathf.Max(0.01f, duration);
        _baseColor = _renderer != null ? _renderer.color : Color.white;
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
        {
            Destroy(gameObject);
        }
    }
}

