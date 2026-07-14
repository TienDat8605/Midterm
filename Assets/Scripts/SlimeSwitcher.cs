using UnityEngine;
using UnityEngine.InputSystem;

public class SlimeSwitcher : MonoBehaviour
{
    [Header("Slime References")]
    [Tooltip("Optional: manually order the slimes (assigned in inspector). If empty, finds all PlayerControllerWithPhysics in the scene.")]
    public Transform[] slimeRoots;

    [Header("Camera")]
    [Tooltip("Optional: camera follow script to update target when switching.")]
    public CameraFollow2D cameraFollow2D;
    public CameraFollow cameraFollow;

    [Header("Visual Feedback")]
    [Tooltip("Optional: color tint for active slime (white = no tint).")]
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private PlayerControllerWithPhysics[] slimes;
    private int activeIndex = 0;

    void Start()
    {
        if (slimeRoots != null && slimeRoots.Length > 0)
        {
            slimes = new PlayerControllerWithPhysics[slimeRoots.Length];
            for (int i = 0; i < slimeRoots.Length; i++)
            {
                if (slimeRoots[i] != null)
                    slimes[i] = slimeRoots[i].GetComponent<PlayerControllerWithPhysics>();
            }
        }
        else
        {
            slimes = FindObjectsByType<PlayerControllerWithPhysics>(FindObjectsSortMode.None);
        }

        if (slimes == null || slimes.Length == 0)
        {
            Debug.LogError("SlimeSwitcher: No slimes found!");
            enabled = false;
            return;
        }

        if (cameraFollow2D == null && cameraFollow == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraFollow2D = mainCam.GetComponent<CameraFollow2D>();
                if (cameraFollow2D == null)
                    cameraFollow = mainCam.GetComponent<CameraFollow>();
            }
        }

        SetActiveSlime(0);
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            int nextIndex = (activeIndex + 1) % slimes.Length;
            SetActiveSlime(nextIndex);
        }
    }

    private void SetActiveSlime(int index)
    {
        if (index < 0 || index >= slimes.Length)
            return;

        activeIndex = index;

        for (int i = 0; i < slimes.Length; i++)
        {
            if (slimes[i] == null)
                continue;

            bool isActive = (i == activeIndex);
            slimes[i].inputEnabled = isActive;

            if (activeColor != inactiveColor)
            {
                SpriteRenderer sr = slimes[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = isActive ? activeColor : inactiveColor;
            }
        }

        PlayerControllerWithPhysics active = slimes[activeIndex];
        if (active == null)
            return;

        Transform activeTransform = active.transform;

        if (cameraFollow2D != null)
            cameraFollow2D.target = activeTransform;
        else if (cameraFollow != null)
            cameraFollow.SetTarget(activeTransform);
    }

    public PlayerControllerWithPhysics ActiveSlime => slimes != null && activeIndex < slimes.Length ? slimes[activeIndex] : null;
    public int ActiveIndex => activeIndex;
}
