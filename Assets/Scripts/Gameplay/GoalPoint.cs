using UnityEngine;

/// <summary>
/// Marks the goal / finish point on a generated map.
/// The map generator places this where 'G' appears in the map data.
/// </summary>
public class GoalPoint : MonoBehaviour
{
    private bool hasCompleted;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasCompleted)
            return;

        PlayerControllerWithPhysics networkPlayer = other.GetComponent<PlayerControllerWithPhysics>();
        if (networkPlayer != null && networkPlayer.photonView != null &&
            networkPlayer.photonView.ViewID != 0 && !networkPlayer.photonView.IsMine)
        {
            return;
        }

        if (networkPlayer == null && other.GetComponent<PlayerController>() == null)
            return;

        hasCompleted = true;
        EndGameController endGame = FindFirstObjectByType<EndGameController>();
        if (endGame != null)
        {
            AudioManager.Instance?.PlaySFX(SFX.Win);
            endGame.ShowEndGame();
        }
        else if (GameManager.Instance != null)
            GameManager.Instance.CompleteLevel();
        else
            Debug.LogWarning("[GoalPoint] No EndGameController is present in this scene.");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.6f);
    }
}
