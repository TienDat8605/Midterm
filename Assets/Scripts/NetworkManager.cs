using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Minimal NetworkManager for prototype testing.
/// Attach to an empty GameObject in your scene.
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    public int maxPlayers = 10;

    [Tooltip("Your Player prefab name — must be inside a Resources/ folder")]
    public string playerPrefabName = "PlayerPrefab";

    [Tooltip("Where players spawn. If empty, spawns at (0, 0, 0)")]
    public Transform[] spawnPoints;

    void Start()
    {
            Debug.Log("[Network] Connecting...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[Network] Connected. Joining room...");
        RoomOptions opts = new RoomOptions { MaxPlayers = (byte)maxPlayers };
        PhotonNetwork.JoinOrCreateRoom("TestRoom", opts, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[Network] Joined! Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");
        SpawnLocalPlayer();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Network] {newPlayer.NickName} connected. Total: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnPlayerLeftRoom(Player other)
    {
        Debug.Log($"[Network] {other.NickName} left.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Network] Disconnected: {cause}");
    }

    private void SpawnLocalPlayer()
    {
        Vector3 pos = Vector3.zero;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int i = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % spawnPoints.Length;
            pos = spawnPoints[i].position;
        }

        PhotonNetwork.Instantiate(playerPrefabName, pos, Quaternion.identity);
    }
}