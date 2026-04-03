using System.Collections;
using UnityEngine;

/// <summary>
/// 사운드 매니저 — 오디오 뼈대.
///
/// 현재 상태: 오디오 파일 없음. AudioClip 슬롯만 정의.
/// 나중에 AudioClip이 준비되면 Inspector에서 슬롯에 연결하면 된다.
///
/// H1 방어 (오디오 동기화 이탈):
///   _bgmSource.ignoreListenerPause = true
///   _sfxSource.ignoreListenerPause = true
///   → Time.timeScale = 0 (컷씬 중 시간 정지) 상태에서도 오디오 계속 재생.
///
/// 구성:
///   BGM AudioSource — 루프 배경음악. 페이드 인/아웃 지원.
///   SFX AudioSource — 단발 효과음. PlayOneShot 방식.
///   UI  AudioSource — UI 클릭음 등 별도 채널.
///
/// TODO: AudioMixer 연동, 볼륨 옵션 저장 — 2026-04-02
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // ─── AudioSource 채널 ─────────────────────────────────────────────────────

    [Header("AudioSource 채널")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _uiSource;

    // ─── BGM 클립 슬롯 (TODO: 파일 준비 시 연결) ─────────────────────────────

    [Header("BGM Clips — 준비되면 연결")]
    [SerializeField] private AudioClip _bgmChapter1;   // 1장 반만요
    [SerializeField] private AudioClip _bgmChapter4;   // 4장 하운드 전투
    [SerializeField] private AudioClip _bgmChapter6;   // 6장 서류 전투
    [SerializeField] private AudioClip _bgmChapter10;  // 10장 형 전투
    [SerializeField] private AudioClip _bgmChapter11;  // 11장 그림자
    [SerializeField] private AudioClip _bgmChapter12;  // 12장 설계자
    [SerializeField] private AudioClip _bgmEnding;     // 엔딩

    // ─── SFX 클립 슬롯 ───────────────────────────────────────────────────────

    [Header("SFX Clips — 준비되면 연결")]
    [SerializeField] private AudioClip _sfxBlink;
    [SerializeField] private AudioClip _sfxParry;
    [SerializeField] private AudioClip _sfxGrab;
    [SerializeField] private AudioClip _sfxSlam;
    [SerializeField] private AudioClip _sfxDeath;
    [SerializeField] private AudioClip _sfxUmbrellaReflect;  // 설계자 우산 반사음

    [Header("설정")]
    [SerializeField] private float _defaultFadeDuration = 1f;
    [SerializeField] [Range(0f, 1f)] private float _bgmVolume = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 1f;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private Coroutine _fadeBgmRoutine;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupSources();
    }

    private void SetupSources()
    {
        if (_bgmSource != null)
        {
            _bgmSource.loop                = true;
            _bgmSource.volume              = _bgmVolume;
            // H1 핵심: Time.timeScale = 0 상태에서도 BGM 계속 재생
            _bgmSource.ignoreListenerPause = true;
        }

        if (_sfxSource != null)
        {
            _sfxSource.volume              = _sfxVolume;
            _sfxSource.ignoreListenerPause = true;
        }

        if (_uiSource != null)
        {
            _uiSource.ignoreListenerPause  = true;
        }
    }

    // ─── BGM API ──────────────────────────────────────────────────────────────

    /// <summary>BGM 재생. fadeTime > 0이면 크로스페이드.</summary>
    public void PlayBGM(AudioClip clip, float fadeTime = -1f)
    {
        if (_bgmSource == null) return;
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] PlayBGM: clip이 null입니다. 슬롯 연결 필요.");
            return;
        }

        float fade = fadeTime >= 0f ? fadeTime : _defaultFadeDuration;

        if (_fadeBgmRoutine != null) StopCoroutine(_fadeBgmRoutine);
        _fadeBgmRoutine = StartCoroutine(CrossFadeBGM(clip, fade));
    }

    public void StopBGM(float fadeTime = -1f)
    {
        if (_bgmSource == null) return;
        float fade = fadeTime >= 0f ? fadeTime : _defaultFadeDuration;
        if (_fadeBgmRoutine != null) StopCoroutine(_fadeBgmRoutine);
        _fadeBgmRoutine = StartCoroutine(FadeOutBGM(fade));
    }

    // ─── SFX API ──────────────────────────────────────────────────────────────

    public void PlaySFX(AudioClip clip)
    {
        if (_sfxSource == null || clip == null) return;
        _sfxSource.PlayOneShot(clip, _sfxVolume);
    }

    // ─── 이름 기반 SFX 단축 메서드 ───────────────────────────────────────────

    public void PlayBlink()          => PlaySFX(_sfxBlink);
    public void PlayParry()          => PlaySFX(_sfxParry);
    public void PlayGrab()           => PlaySFX(_sfxGrab);
    public void PlaySlam()           => PlaySFX(_sfxSlam);
    public void PlayDeath()          => PlaySFX(_sfxDeath);
    public void PlayUmbrellaReflect() => PlaySFX(_sfxUmbrellaReflect);

    // ─── 챕터 BGM 단축 메서드 ────────────────────────────────────────────────

    public void PlayChapterBGM(int chapter)
    {
        AudioClip clip = chapter switch
        {
            1  => _bgmChapter1,
            4  => _bgmChapter4,
            6  => _bgmChapter6,
            10 => _bgmChapter10,
            11 => _bgmChapter11,
            12 => _bgmChapter12,
            _  => null,
        };
        if (clip != null) PlayBGM(clip);
        // clip == null → 해당 챕터 BGM 미준비. 조용히 스킵.
    }

    public void PlayEndingBGM() => PlayBGM(_bgmEnding);

    // ─── 볼륨 ─────────────────────────────────────────────────────────────────

    public void SetBGMVolume(float v)
    {
        _bgmVolume = Mathf.Clamp01(v);
        if (_bgmSource != null) _bgmSource.volume = _bgmVolume;
    }

    public void SetSFXVolume(float v)
    {
        _sfxVolume = Mathf.Clamp01(v);
    }

    // ─── 페이드 코루틴 ────────────────────────────────────────────────────────

    private IEnumerator CrossFadeBGM(AudioClip newClip, float duration)
    {
        // 페이드 아웃
        yield return FadeOutBGM(duration * 0.5f);

        // 새 클립으로 교체
        _bgmSource.clip = newClip;
        _bgmSource.Play();

        // 페이드 인
        float elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed           += Time.unscaledDeltaTime;
            _bgmSource.volume  = Mathf.Lerp(0f, _bgmVolume, elapsed / (duration * 0.5f));
            yield return null;
        }
        _bgmSource.volume = _bgmVolume;
    }

    private IEnumerator FadeOutBGM(float duration)
    {
        float startVolume = _bgmSource.volume;
        float elapsed     = 0f;
        while (elapsed < duration)
        {
            elapsed           += Time.unscaledDeltaTime;
            _bgmSource.volume  = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        _bgmSource.Stop();
        _bgmSource.volume = 0f;
    }
}
