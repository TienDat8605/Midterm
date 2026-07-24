using UnityEngine;

/// <summary>
/// Defines the vertical range used by the gameplay progress minimap.
/// </summary>
public sealed class ProgressMinimapBounds : MonoBehaviour
{
    [SerializeField] private Transform startReference;
    [SerializeField] private Transform goalReference;

    public bool IsConfigured => startReference != null && goalReference != null &&
                                !Mathf.Approximately(startReference.position.y, goalReference.position.y);

    public float GetProgress01(float worldHeight)
    {
        if (!IsConfigured)
            return 0f;

        return Mathf.Clamp01(Mathf.InverseLerp(
            startReference.position.y,
            goalReference.position.y,
            worldHeight));
    }
}
