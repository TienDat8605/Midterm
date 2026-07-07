using UnityEngine;

/// <summary>
/// Marks the goal / finish point on a generated map.
/// The map generator places this where 'G' appears in the map data.
/// </summary>
public class GoalPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.6f);
    }
}
