using UnityEngine;

/// <summary>
/// 체크포인트 매니저 — PlayerPrefs 기반 저장/불러오기 뼈대.
///
/// H2 방어 (컷씬 도중 강제 종료 후 재시작):
///   TriggerCutscene.ActivateTrigger() 시점에 SaveCheckpoint() 호출.
///   → 재시작 시 컷씬 시작 지점으로 복귀. 플레이어가 트리거 안에 끼지 않음.
///
/// 세이브 데이터:
///   - 마지막 체크포인트 월드 좌표 (x, y)
///   - 현재 챕터 인덱스 (1~12)
///   - 각 챕터별 세이브 포인트 이름 (디버그/UI 표시용)
///
/// TODO: JSON 직렬화로 마이그레이션 (아이템 소지, 선택지 기록 등 확장 시) — 2026-04-02
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    // ─── PlayerPrefs 키 ───────────────────────────────────────────────────────

    private const string KEY_POS_X      = "cp_x";
    private const string KEY_POS_Y      = "cp_y";
    private const string KEY_CHAPTER    = "cp_chapter";
    private const string KEY_LABEL      = "cp_label";
    private const string KEY_HAS_SAVE   = "cp_exists";

    // ─── 기본 스폰 위치 ───────────────────────────────────────────────────────

    [Header("기본 스폰 위치 (세이브 없을 때)")]
    [SerializeField] private Transform _defaultSpawnPoint;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── 세이브 API ───────────────────────────────────────────────────────────

    /// <summary>
    /// 체크포인트 저장.
    /// TriggerCutscene.ActivateTrigger()에서 자동 호출됨.
    /// </summary>
    public void SaveCheckpoint(Vector2 position, int chapter, string label = "")
    {
        PlayerPrefs.SetFloat(KEY_POS_X,    position.x);
        PlayerPrefs.SetFloat(KEY_POS_Y,    position.y);
        PlayerPrefs.SetInt  (KEY_CHAPTER,  chapter);
        PlayerPrefs.SetString(KEY_LABEL,   label);
        PlayerPrefs.SetInt  (KEY_HAS_SAVE, 1);
        PlayerPrefs.Save();

        Debug.LogWarning($"[CheckpointManager] 체크포인트 저장 — 챕터 {chapter} / 위치 {position} / 레이블: {label}");
    }

    // ─── 로드 API ─────────────────────────────────────────────────────────────

    public bool HasSave() => PlayerPrefs.GetInt(KEY_HAS_SAVE, 0) == 1;

    public Vector2 GetPosition() => new Vector2(
        PlayerPrefs.GetFloat(KEY_POS_X, _defaultSpawnPoint != null ? _defaultSpawnPoint.position.x : 0f),
        PlayerPrefs.GetFloat(KEY_POS_Y, _defaultSpawnPoint != null ? _defaultSpawnPoint.position.y : 0f)
    );

    public int GetChapter() => PlayerPrefs.GetInt(KEY_CHAPTER, 1);

    public string GetLabel() => PlayerPrefs.GetString(KEY_LABEL, "");

    /// <summary>
    /// 저장된 체크포인트 위치로 플레이어를 이동.
    /// 씬 로드 후 PlayerStateMachine.Start()에서 호출 권장.
    /// </summary>
    public void RestorePlayerPosition(Transform playerTransform)
    {
        if (playerTransform == null) return;
        if (!HasSave())
        {
            if (_defaultSpawnPoint != null)
                playerTransform.position = _defaultSpawnPoint.position;
            return;
        }
        playerTransform.position = GetPosition();
    }

    // ─── 개발용 ───────────────────────────────────────────────────────────────

    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(KEY_POS_X);
        PlayerPrefs.DeleteKey(KEY_POS_Y);
        PlayerPrefs.DeleteKey(KEY_CHAPTER);
        PlayerPrefs.DeleteKey(KEY_LABEL);
        PlayerPrefs.DeleteKey(KEY_HAS_SAVE);
        PlayerPrefs.Save();
        Debug.LogWarning("[CheckpointManager] 세이브 삭제 완료.");
    }
}
