using UnityEngine;

/// <summary>
/// Marks the player spawn point on a generated map.
/// The map generator places this where 'S' appears in the map data.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.6f);
    }
}
