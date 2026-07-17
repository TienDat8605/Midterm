using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RollingRock : EnemyBase
{
    [Header("Roll")]
    [SerializeField] private float initialNudgeForce = 3f;
    [SerializeField] private float maxSpeed = 10f;

    private Rigidbody2D rb;

    protected override void Start()
    {
        base.Start();
        rb = GetComponent<Rigidbody2D>();
        if (PhotonNetwork.IsMasterClient)
            rb.AddForce(Vector2.right * initialNudgeForce, ForceMode2D.Impulse);
    }

    protected override void UpdateBehavior()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (State == EnemyState.Disabled)
            return;
        if (!PhotonNetwork.IsMasterClient)
            return;

        PlayerControllerWithPhysics player = collision.gameObject.GetComponent<PlayerControllerWithPhysics>();
        if (player == null)
            return;

        // Anchor in Brace stance redirects the rock instead of taking knockback
        IBraceable anchor = player as IBraceable;
        if (anchor != null && anchor.IsBraced)
        {
            Vector2 reflectDir = Vector2.Reflect(rb.linearVelocity.normalized, collision.contacts[0].normal);
            rb.linearVelocity = reflectDir * rb.linearVelocity.magnitude * 0.8f;
            return;
        }

        Vector2 knockDir = collision.contacts[0].normal;
        HitPlayer(player, knockDir * knockbackForce);
    }

    protected override void OnBecomeDisabled()
    {
        if (rb == null)
            return;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    protected override void OnBecomeActive()
    {
        if (rb == null)
            return;
        rb.bodyType = RigidbodyType2D.Dynamic;
        if (PhotonNetwork.IsMasterClient)
            rb.AddForce(Vector2.right * initialNudgeForce, ForceMode2D.Impulse);
    }
}
