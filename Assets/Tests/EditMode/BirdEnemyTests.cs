using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BirdEnemyTests
{
    [Test]
    public void KnockbackDirection_StaysInsideDownwardCone()
    {
        for (float angle = -25f; angle <= 25f; angle += 1f)
        {
            Vector2 direction = BirdEnemy.GetKnockbackDirection(angle);
            Assert.Less(direction.y, 0f);
            Assert.That(Vector2.Angle(Vector2.down, direction), Is.LessThanOrEqualTo(25.001f));
        }
    }

    [Test]
    public void Path_StoresIndependentWaitDurationsIncludingZero()
    {
        GameObject root = new GameObject("BirdPath");
        GameObject first = new GameObject("First");
        GameObject second = new GameObject("Second");
        try
        {
            BirdPath path = root.AddComponent<BirdPath>();
            path.SetWaypoints(new List<BirdWaypoint>
            {
                new BirdWaypoint { platform = first.transform, waitDuration = 0f },
                new BirdWaypoint { platform = second.transform, waitDuration = 2.5f }
            });

            Assert.AreEqual(2, path.WaypointCount);
            Assert.AreEqual(0f, path.GetWaitDuration(0));
            Assert.AreEqual(2.5f, path.GetWaitDuration(1));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }
}
