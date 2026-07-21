using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages game state: spawn, respawn, goal detection, and win condition.
/// Singleton — call GameManager.Instance from other scripts.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public GameObject playerPrefab;
    public Transform fallResetY; // object whose Y position marks the death line

    [Header("Settings")]
    public float respawnDelay = 0.3f;

    public enum GameState { Playing, Won }
    public GameState State { get; private set; } = GameState.Playing;

    // Internal
    private GameObject playerInstance;
    private PlayerController playerController;
    private Vector3 spawnPosition;
    private Transform goalTransform;
    private bool isRespawning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        FindSpawnPoint();
        FindGoalPoint();
        SpawnPlayer();
    }

    private void Update()
    {
        if (State != GameState.Playing || playerInstance == null)
            return;

        // Check if player fell below the death line
        if (fallResetY != null && playerInstance.transform.position.y < fallResetY.position.y)
        {
            Respawn();
            return;
        }

        // Check if player reached the goal
        if (goalTransform != null)
        {
            float dist = Vector2.Distance(
                new Vector2(playerInstance.transform.position.x, playerInstance.transform.position.y),
                new Vector2(goalTransform.position.x, goalTransform.position.y));
            if (dist < 1.0f)
            {
                Win();
            }
        }
    }

    private void FindSpawnPoint()
    {
        SpawnPoint sp = FindObjectOfType<SpawnPoint>();
        if (sp != null)
        {
            spawnPosition = sp.transform.position;
        }
        else
        {
            Debug.LogWarning("[GameManager] No SpawnPoint found in scene.");
            spawnPosition = Vector3.zero;
        }
    }

    private void FindGoalPoint()
    {
        GoalPoint gp = FindObjectOfType<GoalPoint>();
        if (gp != null)
        {
            goalTransform = gp.transform;
        }
        else
        {
            Debug.LogWarning("[GameManager] No GoalPoint found in scene.");
        }
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[GameManager] No playerPrefab assigned.");
            return;
        }

        if (playerInstance != null)
            Destroy(playerInstance);

        playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        playerController = playerInstance.GetComponent<PlayerController>();

        if (playerController == null)
            Debug.LogError("[GameManager] Player prefab has no PlayerController component.");
    }

    public void Respawn()
    {
        if (isRespawning) return;
        isRespawning = true;
        AudioManager.Instance?.PlaySFX(SFX.Death);

        if (playerInstance != null)
        {
            // Reset velocity before teleporting
            Rigidbody2D rb = playerInstance.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            playerInstance.transform.position = spawnPosition;
        }

        Invoke(nameof(ClearRespawnFlag), respawnDelay);
    }

    private void ClearRespawnFlag()
    {
        isRespawning = false;
    }

    private void Win()
    {
        State = GameState.Won;
        Debug.Log("[GameManager] You reached the goal! You win!");
        AudioManager.Instance?.PlaySFX(SFX.Win);

        // Disable player control
        if (playerController != null)
            playerController.enabled = false;

        if (playerInstance != null)
        {
            Rigidbody2D rb = playerInstance.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        EndGameController endGame = FindFirstObjectByType<EndGameController>();
        if (endGame != null)
            endGame.ShowEndGame();
    }

    public void CompleteLevel()
    {
        if (State == GameState.Playing)
            Win();
    }

    public void RestartGame()
    {
        State = GameState.Playing;
        SpawnPlayer();
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void OnDrawGizmos()
    {
        if (fallResetY != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(-50, fallResetY.position.y, 0),
                            new Vector3(50, fallResetY.position.y, 0));
        }
    }
}
