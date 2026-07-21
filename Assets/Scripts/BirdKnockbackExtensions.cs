using UnityEngine;

public static class BirdKnockbackExtensions
{
    public static void ApplyBirdKnockback(this PlayerControllerWithPhysics player, Vector2 velocity)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
            body.linearVelocity = velocity;
    }

    public static void ApplyBirdKnockback(this PlayerController player, Vector2 velocity)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
            body.linearVelocity = velocity;
    }
}
