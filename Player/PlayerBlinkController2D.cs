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

    // 히트 스톱 중복 실행 방지
    private bool _isHitStopping;

    // 블링크 후 무적(I-frame) 관리
    private System.Collections.Coroutine _invincibleCoroutine;
    private float _originalAlpha = 1f;
    private int _originalLayer;

    private void Awake()
    {
        if (playerRb == null)
            playerRb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

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
            TryBlinkToDagger();
        }
    }

    #endregion

    #region Throw & Blink Logic

    private void ThrowDagger()
    {
        if (weaponData == null || weaponData.daggerProjectilePrefab == null)
            return;

        // 기존 단검이 있다면 먼저 회수
        if (currentDagger != null)
        {
            Destroy(currentDagger.gameObject);
            currentDagger = null;
        }

        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mouseWorldPos - transform.position);
        direction.Normalize();

        GameObject daggerObj = Instantiate(
            weaponData.daggerProjectilePrefab,
            transform.position,
            Quaternion.identity
        );

        currentDagger = daggerObj.GetComponent<DaggerProjectile2D>();
        if (currentDagger != null)
        {
            currentDagger.Launch(transform.position, direction, weaponData);
        }
    }

    private void TryBlinkToDagger()
    {
        if (currentDagger == null || !currentDagger.CanBlink)
            return;

        // 무한 체공 방지: 공중에서 블링크 횟수 소모
        bool isInAir = !isGrounded && !isOnWall;
        if (isInAir && currentAirBlinkCount <= 0)
        {
            // 공중 블링크 횟수 초과
            return;
        }

        if (isInAir)
        {
            // 블링크 실행 시 즉시 소모
            currentAirBlinkCount--;
        }

        Vector2 targetPos = currentDagger.CurrentPosition;
        Vector2 finalPos = targetPos;

        // 1) 끼임 방지: 단검이 벽에 박힌 상태면 Normal 방향으로 0.5 유닛 Offset
        if (currentDagger.IsStuckToWall)
        {
            finalPos += currentDagger.LastHitNormal * 0.5f;
        }

        // 블링크 순간 속도 0으로 초기화 (Snappy한 조작감)
        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
        }

        // 실제 위치 이동
        transform.position = finalPos;

        // 블링크 경로 잔상 연출
        SpawnBlinkTrail((Vector2)transform.position, targetPos);

        // 도착 후 무적(I-frame) 시작
        StartBlinkInvincibility();

        // 히트 스톱 & 검붉은 누아르 이펙트
        StartCoroutine(PerformHitStop());

        // 단검은 즉시 파괴(회수)
        Destroy(currentDagger.gameObject);
        currentDagger = null;
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
            ResetAirBlinkCount();
        }
        else if ((wallMask.value & layerBit) != 0)
        {
            isOnWall = true;
            isGrounded = false;
            ResetAirBlinkCount();
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

