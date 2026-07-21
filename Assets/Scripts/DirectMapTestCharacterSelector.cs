using Photon.Pun;
using UnityEngine;

/// <summary>
/// Development-only character picker used when MapScene is played directly.
/// It disables itself when the scene is loaded through an active Photon room.
/// </summary>
public sealed class DirectMapTestCharacterSelector : MonoBehaviour
{
    [SerializeField] private GameObject anchorPrefab;
    [SerializeField] private GameObject bouncyPrefab;
    [SerializeField] private GameObject stickyPrefab;
    [SerializeField] private Transform testSpawnPoint;

    private GameObject currentCharacter;
    private bool isDirectTestMode;

    public static bool IsDirectTestMode { get; private set; }

    private void Awake()
    {
        isDirectTestMode =
            !SinglePlayerSession.IsActive &&
            NetworkManager.Instance == null &&
            !PhotonNetwork.InRoom;
        IsDirectTestMode = isDirectTestMode;
        if (!isDirectTestMode)
            enabled = false;
    }

    private void OnDestroy()
    {
        if (isDirectTestMode)
            IsDirectTestMode = false;
    }

    private void OnGUI()
    {
        if (!isDirectTestMode)
            return;

        GUILayout.BeginArea(new Rect(20f, 20f, 240f, 190f), GUI.skin.box);
        GUILayout.Label("Direct Map Test");
        GUILayout.Label(currentCharacter == null
            ? "Choose a character to spawn"
            : $"Testing: {currentCharacter.name}");

        DrawCharacterButton("Anchor Slime", anchorPrefab);
        DrawCharacterButton("Bouncy Slime", bouncyPrefab);
        DrawCharacterButton("Sticky Slime", stickyPrefab);
        GUILayout.EndArea();
    }

    private void DrawCharacterButton(string label, GameObject prefab)
    {
        GUI.enabled = prefab != null;
        if (GUILayout.Button(label))
            SpawnCharacter(prefab);
        GUI.enabled = true;
    }

    private void SpawnCharacter(GameObject prefab)
    {
        if (prefab == null)
            return;

        if (currentCharacter != null)
            Destroy(currentCharacter);

        Vector3 spawnPosition = testSpawnPoint != null
            ? testSpawnPoint.position
            : Vector3.up;
        currentCharacter = Instantiate(prefab, spawnPosition, Quaternion.identity);
        currentCharacter.name = $"{prefab.name} (Direct Test)";

        CameraFollow cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null)
            cameraFollow.SetTarget(currentCharacter.transform);
    }
}
