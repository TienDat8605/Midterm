using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RollingRock : EnemyBase
{
    [Header("Roll")]
    [SerializeField] private float initialNudgeForce = 3f;
    [SerializeField] private float maxSpeed = 10f;

    [Header("Player Sight")]
    [SerializeField] private float sightRadius = 10f;

    private Rigidbody2D rb;
    private Vector2 savedVelocity;
    private bool isFrozen;

    protected override void Start()
    {
        base.Start();
        rb = GetComponent<Rigidbody2D>();
        if (IsAuthority)
            rb.AddForce(Vector2.right * initialNudgeForce, ForceMode2D.Impulse);
    }

    protected override void UpdateBehavior()
    {
        if (!IsAuthority)
            return;

        bool seen = IsSeenByAnyPlayer();

        if (!seen && !isFrozen)
        {
            savedVelocity = rb.linearVelocity;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            isFrozen = true;
        }
        else if (seen && isFrozen)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            if (savedVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = savedVelocity;
            }
            else
            {
                PlayerControllerWithPhysics nearest = FindNearestPlayer();
                float dir = nearest != null ? Mathf.Sign(nearest.transform.position.x - transform.position.x) : 1f;
                rb.linearVelocity = Vector2.right * dir * initialNudgeForce;
            }
            isFrozen = false;
        }

        if (!isFrozen && rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    private PlayerControllerWithPhysics FindNearestPlayer()
    {
        PlayerControllerWithPhysics nearest = null;
        float minDist = float.MaxValue;
        foreach (var player in FindObjectsByType<PlayerControllerWithPhysics>(FindObjectsSortMode.None))
        {
            float d = Vector2.Distance(transform.position, player.transform.position);
            if (d < minDist) { minDist = d; nearest = player; }
        }
        return nearest;
    }

    private bool IsSeenByAnyPlayer()
    {
        foreach (var player in FindObjectsByType<PlayerControllerWithPhysics>(FindObjectsSortMode.None))
        {
            Vector2 toRock = (Vector2)transform.position - (Vector2)player.transform.position;
            if (toRock.magnitude > sightRadius)
                continue;

            SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
            bool facingRight = sr == null || !sr.flipX;
            bool rockIsRight = toRock.x > 0f;

            if (facingRight == rockIsRight)
                return true;
        }
        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (State == EnemyState.Disabled || isFrozen)
            return;
        if (!IsAuthority)
            return;

        PlayerControllerWithPhysics player = collision.gameObject.GetComponent<PlayerControllerWithPhysics>();
        if (player != null)
        {
            IBraceable anchor = player as IBraceable;
            if (anchor != null && anchor.IsBraced)
            {
                Vector2 reflectDir = Vector2.Reflect(rb.linearVelocity.normalized, collision.contacts[0].normal);
                rb.linearVelocity = reflectDir * rb.linearVelocity.magnitude * 0.8f;
                return;
            }

            Vector2 knockDir = collision.contacts[0].normal;
            HitPlayer(player, knockDir * knockbackForce);
            return;
        }

        // Bounce off walls — reverse X on near-vertical surface normal
        Vector2 normal = collision.contacts[0].normal;
        if (Mathf.Abs(normal.x) > 0.5f)
        {
            float speed = Mathf.Max(Mathf.Abs(rb.linearVelocity.x), initialNudgeForce);
            rb.linearVelocity = new Vector2(Mathf.Sign(normal.x) * speed, rb.linearVelocity.y);
        }
    }

    protected override void OnBecomeDisabled()
    {
        if (rb == null) return;
        savedVelocity = Vector2.zero;
        isFrozen = false;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    protected override void OnBecomeActive()
    {
        if (rb == null) return;
        rb.bodyType = RigidbodyType2D.Dynamic;
        if (IsAuthority)
            rb.AddForce(Vector2.right * initialNudgeForce, ForceMode2D.Impulse);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }
}
