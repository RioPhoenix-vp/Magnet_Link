using UnityEngine;

/// <summary>
/// Persistent audio singleton. Lives on _Managers, survives level swaps.
///
/// SFX use a round-robin pool of AudioSources so overlapping sounds
/// (rapid ball catches, fast drops) don't cut each other off.
/// Music uses its own dedicated looping source.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SFX Clips")]
    [SerializeField] private AudioClip ballCatchClip;
    [SerializeField] private AudioClip ballDropClip;
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip coinsClip;

    [Header("Music")]
    [SerializeField] private AudioClip menuMusicClip;

    [Header("Volumes")]
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float musicVolume = 0.25f;

    [Header("SFX Pool")]
    [Tooltip("How many AudioSources to create for overlapping SFX.")]
    [SerializeField] private int poolSize = 8;

    [Header("Drop Combo Pitch")]
    [Tooltip("Rapid consecutive drops raise the pitch. Resets after this many seconds of silence.")]
    [SerializeField] private float comboResetTime = 0.8f;
    [SerializeField] private float comboPitchStep = 0.06f;
    [SerializeField] private float maxComboPitch = 1.6f;

    private const string SFX_KEY = "SFXVolume";
    private const string MUSIC_KEY = "MusicVolume";

    private AudioSource[] sfxPool;
    private int poolIndex = 0;
    private AudioSource musicSource;

    private int comboCount = 0;
    private float lastDropTime = -99f;

    public float SFXVolume => sfxVolume;
    public float MusicVolume => musicVolume;

    private int catchCombo = 0;
    private float lastCatchTime = -99f;

    // ── LIFECYCLE ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxVolume = PlayerPrefs.GetFloat(SFX_KEY, sfxVolume);
        musicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, musicVolume);

        BuildPool();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildPool()
    {
        sfxPool = new AudioSource[Mathf.Max(1, poolSize)];

        for (int i = 0; i < sfxPool.Length; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;              // ⚠️ SFX must never loop
            src.spatialBlend = 0f;         // 2D — same volume regardless of position
            sfxPool[i] = src;
        }

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
    }

    // ── PUBLIC SFX API ────────────────────────────────────────────────────────

    /// <summary>Magnet picks up a ball.</summary>
    /// <summary>Magnet picks up a ball. Rapid catches climb in pitch.</summary>
    public void PlayBallCatch()
    {
        if (Time.unscaledTime - lastCatchTime > comboResetTime)
            catchCombo = 0;
        else
            catchCombo++;

        lastCatchTime = Time.unscaledTime;

        float pitch = Mathf.Min(1f + catchCombo * comboPitchStep, maxComboPitch);
        PlaySFX(ballCatchClip, pitch);
    }

    /// <summary>Button click — any button, anywhere.</summary>
    public void PlayButtonClick() => PlaySFX(buttonClickClip);

    /// <summary>Coins awarded on level win.</summary>
    public void PlayCoins() => PlaySFX(coinsClip);

    /// <summary>
    /// Ball lands in a box. Consecutive rapid drops climb in pitch for a
    /// satisfying combo feel, then reset after a pause.
    /// </summary>
    public void PlayBallDrop()
    {
        if (Time.unscaledTime - lastDropTime > comboResetTime)
            comboCount = 0;
        else
            comboCount++;

        lastDropTime = Time.unscaledTime;

        float pitch = Mathf.Min(1f + comboCount * comboPitchStep, maxComboPitch);
        PlaySFX(ballDropClip, pitch);
    }

    /// <summary>Plays any clip through the pool. Safe to call with null.</summary>
    public void PlaySFX(AudioClip clip, float pitch = 1f)
    {
        if (clip == null || sfxPool == null) return;

        AudioSource src = sfxPool[poolIndex];
        poolIndex = (poolIndex + 1) % sfxPool.Length;

        src.Stop();                 // reclaim the source if it's mid-clip
        src.clip = clip;
        src.pitch = pitch;
        src.volume = sfxVolume;
        src.Play();
    }

    // ── MUSIC ─────────────────────────────────────────────────────────────────

    /// <summary>Starts the menu music loop. No-op if already playing it.</summary>
    public void PlayMenuMusic()
    {
        if (menuMusicClip == null || musicSource == null) return;
        if (musicSource.isPlaying && musicSource.clip == menuMusicClip) return;

        musicSource.clip = menuMusicClip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    // ── VOLUME ────────────────────────────────────────────────────────────────

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SFX_KEY, sfxVolume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource != null) musicSource.volume = musicVolume;
        PlayerPrefs.SetFloat(MUSIC_KEY, musicVolume);
        PlayerPrefs.Save();
    }
}