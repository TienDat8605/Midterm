using System.Collections;
using Photon.Pun;
using UnityEngine;

public sealed class NetworkPlayerSpawner : MonoBehaviour
{
    [Header("Role prefabs registered by NetworkManager")]
    [SerializeField] private string anchorPrefabName = "AnchorSlime";
    [SerializeField] private string bouncePrefabName = "BouncySlime";
    [SerializeField] private string stickyPrefabName = "StickySlime";

    [Header("Role spawn points")]
    [SerializeField] private Transform anchorSpawnPoint;
    [SerializeField] private Transform bounceSpawnPoint;
    [SerializeField] private Transform stickySpawnPoint;

    private IEnumerator Start()
    {
        if (SinglePlayerSession.IsActive)
        {
            SpawnSinglePlayer();
            yield break;
        }

        if (DirectMapTestCharacterSelector.IsDirectTestMode)
            yield break;

        float timeoutAt = Time.realtimeSinceStartup + 10f;
        while ((!PhotonNetwork.InRoom || NetworkManager.Instance == null) &&
               Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        if (!PhotonNetwork.InRoom || NetworkManager.Instance == null)
        {
            Debug.LogError("[Network] Cannot spawn: no active room or NetworkManager.");
            yield break;
        }

        SpawnLocalPlayerIfNeeded();
    }

    private void SpawnSinglePlayer()
    {
        SlimeRole role = SinglePlayerSession.SelectedRole;
        string prefabName = GetPrefabName(role);
        GameObject prefab = Resources.Load<GameObject>(prefabName);
        if (prefab == null)
        {
            Debug.LogError($"[Single Player] Cannot load prefab '{prefabName}' from a Resources folder.");
            return;
        }

        Transform spawnPoint = GetSpawnPoint(role);
        Vector3 position = spawnPoint != null ? spawnPoint.position : GetFallbackPosition(role);
        GameObject localPlayer = Instantiate(prefab, position, Quaternion.identity);
        localPlayer.name = $"{prefab.name} (Single Player)";
        FollowLocalPlayer(localPlayer);
    }

    private void SpawnLocalPlayerIfNeeded()
    {
        GameObject existingLocalPlayer = FindLocalNetworkPlayer();
        if (existingLocalPlayer != null)
        {
            FollowLocalPlayer(existingLocalPlayer);
            NetworkManager.Instance.MarkLocalPlayerLoaded();
            return;
        }

        SlimeRole role = NetworkManager.Instance.LocalRole;
        string prefabName = GetPrefabName(role);
        if (string.IsNullOrEmpty(prefabName))
        {
            Debug.LogError("[Network] Cannot spawn: local player has no reserved role.");
            return;
        }

        Transform spawnPoint = GetSpawnPoint(role);
        Vector3 position = spawnPoint != null ? spawnPoint.position : GetFallbackPosition(role);
        GameObject localPlayer = PhotonNetwork.Instantiate(prefabName, position, Quaternion.identity);
        FollowLocalPlayer(localPlayer);
        NetworkManager.Instance.MarkLocalPlayerLoaded();
    }

    private static void FollowLocalPlayer(GameObject localPlayer)
    {
        CameraFollow cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null && localPlayer != null)
            cameraFollow.SetTarget(localPlayer.transform);
    }

    private static GameObject FindLocalNetworkPlayer()
    {
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber &&
                view.GetComponent<PlayerControllerWithPhysics>() != null)
            {
                return view.gameObject;
            }
        }

        return null;
    }

    private string GetPrefabName(SlimeRole role)
    {
        switch (role)
        {
            case SlimeRole.Anchor: return anchorPrefabName;
            case SlimeRole.Bounce: return bouncePrefabName;
            case SlimeRole.Sticky: return stickyPrefabName;
            default: return string.Empty;
        }
    }

    private Transform GetSpawnPoint(SlimeRole role)
    {
        switch (role)
        {
            case SlimeRole.Anchor: return anchorSpawnPoint;
            case SlimeRole.Bounce: return bounceSpawnPoint;
            case SlimeRole.Sticky: return stickySpawnPoint;
            default: return null;
        }
    }

    private static Vector3 GetFallbackPosition(SlimeRole role)
    {
        switch (role)
        {
            case SlimeRole.Anchor: return new Vector3(-2f, 1f, 0f);
            case SlimeRole.Bounce: return new Vector3(0f, 1f, 0f);
            case SlimeRole.Sticky: return new Vector3(2f, 1f, 0f);
            default: return Vector3.zero;
        }
    }
}
