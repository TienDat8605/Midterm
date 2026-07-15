using UnityEngine;

public enum SFX
{
    Jump,
    Land,
    Death,
    Win
}

/// <summary>
/// Singleton audio manager. Persists across scenes.
/// Usage: AudioManager.Instance.PlaySFX(SFX.Jump)
///        AudioManager.Instance.PlayBGM()
///        AudioManager.Instance.StopBGM()
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    [Header("SFX Clips")]
    public AudioClip sfxJump;
    public AudioClip sfxLand;
    public AudioClip sfxDeath;
    public AudioClip sfxWin;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
    }

    private void Start()
    {
        PlayBGM();
    }

    public void PlayBGM()
    {
        if (bgmClip == null) return;
        bgmSource.clip = bgmClip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PlaySFX(SFX sfx)
    {
        AudioClip clip = sfx switch
        {
            SFX.Jump  => sfxJump,
            SFX.Land  => sfxLand,
            SFX.Death => sfxDeath,
            SFX.Win   => sfxWin,
            _         => null
        };

        if (clip != null)
            sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = volume;
        bgmSource.volume = volume;
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
    }
}
