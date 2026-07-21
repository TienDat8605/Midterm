using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BirdWaypoint
{
    public Transform platform;
    [Min(0f)] public float waitDuration;

    public Vector3 Position => platform != null ? platform.position : Vector3.zero;
}

public class BirdPath : MonoBehaviour
{
    [SerializeField, Min(0f)] private float defaultWaitDuration = 1f;
    [SerializeField] private List<BirdWaypoint> waypoints = new List<BirdWaypoint>();

    public int WaypointCount => waypoints.Count;
    public float DefaultWaitDuration => defaultWaitDuration;
    public IReadOnlyList<BirdWaypoint> Waypoints => waypoints;

    public Vector3 GetWaypointPosition(int index)
    {
        if (index < 0 || index >= waypoints.Count)
            return transform.position;
        return waypoints[index].Position;
    }

    public float GetWaitDuration(int index)
    {
        if (index < 0 || index >= waypoints.Count)
            return 0f;
        return Mathf.Max(0f, waypoints[index].waitDuration);
    }

    public void SetWaypoints(List<BirdWaypoint> newWaypoints)
    {
        waypoints = newWaypoints ?? new List<BirdWaypoint>();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 point = waypoints[i].Position;
            Gizmos.DrawWireSphere(point, 0.16f);
            if (i > 0)
                Gizmos.DrawLine(waypoints[i - 1].Position, point);
        }
    }

    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(1f, 0.85f, 0.2f, 0.85f);
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 point = waypoints[i].Position;
            UnityEditor.Handles.Label(point + Vector3.up * 0.2f,
                $"{i}: wait {GetWaitDuration(i):0.##}s");
        }
    }
#endif
}
