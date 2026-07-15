using UnityEngine;

/// <summary>
/// A trigger zone at the bottom of the map.
/// When the player enters, the GameManager respawns them.
/// Attach this to a GameObject with a BoxCollider2D set to IsTrigger.
/// </summary>
public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check for PlayerController rather than a tag — avoids needing a "Player" tag
        if (other.GetComponent<PlayerController>() == null)
            return;

        if (GameManager.Instance != null)
            GameManager.Instance.Respawn();
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D bc = GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.offset, bc.size);
        }
    }
}
