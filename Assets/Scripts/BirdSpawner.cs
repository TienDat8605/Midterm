using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class BirdSpawner : MonoBehaviour
{
    [SerializeField] private BirdEnemy birdPrefab;
    [SerializeField] private List<BirdPath> paths = new List<BirdPath>();
    [SerializeField, Min(0.1f)] private float spawnInterval = 5f;
    [SerializeField, Min(1)] private int maxActiveBirds = 3;
    [SerializeField] private bool spawnOnStart = true;
    private readonly List<BirdEnemy> activeBirds = new List<BirdEnemy>();
    private float nextSpawnTime;

    public IReadOnlyList<BirdPath> Paths => paths;
    public BirdPath GetPath(int index) => index >= 0 && index < paths.Count ? paths[index] : null;

    private void Start()
    {
        nextSpawnTime = spawnOnStart ? 0f : Time.time + spawnInterval;
    }

    private void Update()
    {
        if (!CanSpawn() || Time.time < nextSpawnTime)
            return;
        activeBirds.RemoveAll(bird => bird == null);
        if (activeBirds.Count < maxActiveBirds && paths.Count > 0 && birdPrefab != null)
            SpawnBird(Random.Range(0, paths.Count));
        nextSpawnTime = Time.time + spawnInterval;
    }

    private bool CanSpawn()
    {
        if (SinglePlayerSession.IsActive)
            return true;

        return !PhotonNetwork.IsConnected ||
               (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient);
    }

    private void SpawnBird(int pathIndex)
    {
        BirdPath selectedPath = paths[pathIndex];
        Vector3 position = selectedPath != null && selectedPath.WaypointCount > 0
            ? selectedPath.GetWaypointPosition(0) : transform.position;
        BirdEnemy bird;
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            bird = PhotonNetwork.InstantiateRoomObject(birdPrefab.name, position, Quaternion.identity,
                0, new object[] { pathIndex }).GetComponent<BirdEnemy>();
        else
            bird = Instantiate(birdPrefab, position, Quaternion.identity);
        bird.ConfigurePath(selectedPath);
        activeBirds.Add(bird);
    }
}
