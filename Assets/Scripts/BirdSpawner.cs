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

    private bool initialSpawnPending;
    private float nextSpawnTime;

    public IReadOnlyList<BirdPath> Paths => paths;
    public BirdPath GetPath(int index) => index >= 0 && index < paths.Count ? paths[index] : null;

    private void Start()
    {
        initialSpawnPending = spawnOnStart;
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        if (!CanSpawn())
            return;

        TrackExistingBirds();
        activeBirds.RemoveAll(bird => bird == null);
        if (initialSpawnPending)
        {
            initialSpawnPending = false;
            SpawnBirdsUpToLimit();
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (Time.time < nextSpawnTime)
            return;

        if (activeBirds.Count < maxActiveBirds && birdPrefab != null)
        {
            int pathIndex = GetAvailablePathIndex();
            if (pathIndex >= 0)
                SpawnBird(pathIndex);
        }

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


    private int GetAvailablePathIndex()
    {
        List<int> availablePathIndices = new List<int>();
        for (int i = 0; i < paths.Count; i++)
        {
            BirdPath candidatePath = paths[i];
            if (candidatePath == null)
                continue;

            bool pathInUse = activeBirds.Exists(bird => bird != null && bird.Path == candidatePath);
            if (!pathInUse)
                availablePathIndices.Add(i);
        }

        return availablePathIndices.Count > 0
            ? availablePathIndices[Random.Range(0, availablePathIndices.Count)]
            : -1;
    }


    private void SpawnBirdsUpToLimit()
    {
        if (birdPrefab == null)
            return;

        while (activeBirds.Count < maxActiveBirds)
        {
            int pathIndex = GetAvailablePathIndex();
            if (pathIndex < 0)
                break;

            SpawnBird(pathIndex);
        }
    }

    private void TrackExistingBirds()
    {
        BirdEnemy[] existingBirds = FindObjectsByType<BirdEnemy>(FindObjectsSortMode.None);
        foreach (BirdEnemy bird in existingBirds)
        {
            if (bird != null && !activeBirds.Contains(bird))
                activeBirds.Add(bird);
        }
    }

}
