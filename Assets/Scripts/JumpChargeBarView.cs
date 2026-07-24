using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Client-local world-space presentation for a slime's charge jump.
/// </summary>
public sealed class JumpChargeBarView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private RectTransform fillClip;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform highlight;

    [Header("Placement")]
    [SerializeField, Min(0f)] private float ownerGap = 0.18f;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float popDuration = 0.12f;
    [SerializeField, Min(0f)] private float minimumPulseAmount = 0.015f;
    [SerializeField, Min(0f)] private float maximumPulseAmount = 0.09f;
    [SerializeField, Min(0f)] private float minimumPulseSpeed = 5f;
    [SerializeField, Min(0f)] private float maximumPulseSpeed = 13f;
    [SerializeField, Min(0.01f)] private float highlightSweepSpeed = 2.6f;

    private PlayerControllerWithPhysics owner;
    private Collider2D ownerCollider;
    private bool isCharging;
    private float normalizedCharge;
    private float visibleTime;

    public float NormalizedCharge => normalizedCharge;
    public bool IsVisualActive => visualRoot != null && visualRoot.gameObject.activeSelf;

    /// <summary>
    /// Binds this local-only view to its owning controller.
    /// </summary>
    public void Initialize(PlayerControllerWithPhysics owningController)
    {
        owner = owningController;
        ownerCollider = owner != null ? owner.GetComponent<Collider2D>() : null;

        // The bar is deliberately not parented to the slime. Its world scale therefore
        // stays fixed even when a role prefab uses a different transform scale.
        transform.SetParent(null, true);
        SetChargeState(false, 0f);
        UpdateWorldPosition();
    }

    /// <summary>
    /// Updates visibility and normalized presentation without affecting gameplay.
    /// </summary>
    public void SetChargeState(bool charging, float charge)
    {
        normalizedCharge = Mathf.Clamp01(charge);
        ApplyProgress(normalizedCharge);

        bool stateChanged = charging != isCharging;
        isCharging = charging;
        if (stateChanged)
            visibleTime = 0f;

        if (visualRoot != null)
        {
            if (visualRoot.gameObject.activeSelf != charging)
                visualRoot.gameObject.SetActive(charging);
            if (stateChanged)
                visualRoot.localScale = charging ? Vector3.zero : Vector3.one;
        }
    }

    public static Color EvaluateChargeColor(float charge)
    {
        float clampedCharge = Mathf.Clamp01(charge);
        Color green = new Color32(61, 220, 93, 255);
        Color yellow = new Color32(255, 214, 61, 255);
        Color red = new Color32(244, 67, 54, 255);

        return clampedCharge <= 0.5f
            ? Color.Lerp(green, yellow, clampedCharge * 2f)
            : Color.Lerp(yellow, red, (clampedCharge - 0.5f) * 2f);
    }

    private void Awake()
    {
        if (worldCanvas != null)
        {
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.overrideSorting = true;
        }

        SetChargeState(false, 0f);
    }

    private void LateUpdate()
    {
        UpdateWorldPosition();

        if (!isCharging || visualRoot == null)
            return;

        visibleTime += Time.unscaledDeltaTime;

        float popProgress = Mathf.Clamp01(visibleTime / popDuration);
        float popScale = 1f + Mathf.Sin(popProgress * Mathf.PI) * 0.12f;
        if (popProgress < 1f)
            popScale *= Mathf.SmoothStep(0f, 1f, popProgress);

        float pulseAmount = Mathf.Lerp(
            minimumPulseAmount,
            maximumPulseAmount,
            normalizedCharge);
        float pulseSpeed = Mathf.Lerp(
            minimumPulseSpeed,
            maximumPulseSpeed,
            normalizedCharge);
        float pulseScale = 1f + Mathf.Sin(visibleTime * pulseSpeed) * pulseAmount;
        visualRoot.localScale = Vector3.one * popScale * pulseScale;

        if (highlight != null)
        {
            float sweep = Mathf.Repeat(visibleTime * highlightSweepSpeed, 1.35f) - 0.2f;
            Vector2 anchor = highlight.anchorMin;
            anchor.x = sweep;
            highlight.anchorMin = anchor;
            anchor = highlight.anchorMax;
            anchor.x = sweep;
            highlight.anchorMax = anchor;
        }
    }

    private void ApplyProgress(float progress)
    {
        if (fillClip != null)
        {
            Vector2 anchorMax = fillClip.anchorMax;
            anchorMax.x = progress;
            fillClip.anchorMax = anchorMax;
        }

        if (fillImage != null)
            fillImage.color = EvaluateChargeColor(progress);
    }

    private void UpdateWorldPosition()
    {
        if (owner == null)
            return;

        if (ownerCollider == null)
            ownerCollider = owner.GetComponent<Collider2D>();

        Bounds bounds = ownerCollider != null
            ? ownerCollider.bounds
            : new Bounds(owner.transform.position, Vector3.one);
        transform.position = new Vector3(
            bounds.center.x,
            bounds.min.y - ownerGap,
            owner.transform.position.z);
        transform.rotation = Quaternion.identity;
    }
}
