using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public enum SFX
{
    Jump,
    Land,
    Death,
    BirdHit,
    Win,
    UIHover,
    UIClick
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
    public AudioClip sfxBirdHit;
    public AudioClip sfxWin;
    public AudioClip sfxUIHover;
    public AudioClip sfxUIClick;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private readonly HashSet<Button> boundUIButtons = new HashSet<Button>();

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

        EnsureSfxSource();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        UpdateBGMForScene(SceneManager.GetActiveScene());
        StartCoroutine(BindUISoundsNextFrame());
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
        boundUIButtons.Clear();
        StartCoroutine(BindUISoundsNextFrame());
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
            SFX.Jump => sfxJump,
            SFX.Land => sfxLand,
            SFX.Death => sfxDeath,
            SFX.BirdHit => sfxBirdHit,
            SFX.Win => sfxWin,
            SFX.UIHover => sfxUIHover,
            SFX.UIClick => sfxUIClick,
            _ => null
        };

        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] No clip assigned for {sfx}.", this);
            return;
        }

        EnsureSfxSource();
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    private IEnumerator BindUISoundsNextFrame()
    {
        yield return null;

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (UIDocument document in documents)
        {
            List<Button> buttons = document.rootVisualElement.Query<Button>().ToList();
            foreach (Button button in buttons)
            {
                if (!boundUIButtons.Add(button))
                    continue;

                button.RegisterCallback<PointerEnterEvent>(OnUIButtonPointerEnter);
                button.clicked += OnUIButtonClicked;
            }
        }
    }

    private void OnUIButtonPointerEnter(PointerEnterEvent evt)
    {
        if (evt.currentTarget is Button button && button.enabledInHierarchy)
            PlaySFX(SFX.UIHover);
    }

    private void OnUIButtonClicked()
    {
        PlaySFX(SFX.UIClick);
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


private void EnsureSfxSource()
    {
        if (sfxSource != null)
            return;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 1f;
        sfxSource.ignoreListenerPause = true;
    }
}
