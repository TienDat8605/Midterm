using UnityEngine;

public static class TetherPhysics
{
    public static Vector2 CalculateYankImpulse(
        Vector2 targetPosition,
        Vector2 stickyPosition,
        Vector2 targetVelocity,
        float velocityImpulse,
        float targetMass)
    {
        Vector2 difference = stickyPosition - targetPosition;
        if (difference.sqrMagnitude <= Mathf.Epsilon)
            return Vector2.zero;

        Vector2 desiredVelocity =
            difference.normalized * Mathf.Max(0f, velocityImpulse);
        return (desiredVelocity - targetVelocity) * Mathf.Max(0f, targetMass);
    }
}
