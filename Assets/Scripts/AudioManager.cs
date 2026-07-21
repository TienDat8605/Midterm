using UnityEngine;
using UnityEngine.SceneManagement;

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
    [Tooltip("BGM plays only while one of these scenes is active.")]
    [SerializeField] private string[] gameplaySceneNames = { "Map1", "Map2" };

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

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        UpdateBGMForScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateBGMForScene(scene);
    }

    private void UpdateBGMForScene(Scene scene)
    {
        bool isGameplayScene = false;
        foreach (string gameplaySceneName in gameplaySceneNames)
        {
            if (scene.name == gameplaySceneName)
            {
                isGameplayScene = true;
                break;
            }
        }

        if (isGameplayScene)
            PlayBGM();
        else
            StopBGM();
    }

    public void PlayBGM()
    {
        if (bgmClip == null) return;

        if (bgmSource.isPlaying && bgmSource.clip == bgmClip)
            return;

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
        {
            if (sfx == SFX.Jump)
                Debug.Log($"[AudioManager] Playing jump SFX: {clip.name}", this);

            sfxSource.PlayOneShot(clip, sfxVolume);
        }
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
