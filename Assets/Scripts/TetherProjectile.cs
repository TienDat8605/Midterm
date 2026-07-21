using UnityEngine;

public class TetherProjectile : MonoBehaviour
{
    [Tooltip("How fast the tether line extends (units per second).")]
    public float extendSpeed = 40f;

    [Tooltip("Layers the tether can collide with.")]
    public LayerMask hitMask = ~0;

    [Tooltip("Maximum length the tether can reach before disappearing.")]
    public float maxLength = 8f;

    private Vector2 direction;
    private float currentLength;
    private StickySlime shooter;
    private SpriteRenderer sr;
    private float shooterRadius;
    private float spriteWidth;
    private bool isAuthoritative;
    private bool hasHit;
    private Rigidbody2D hitTarget;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            spriteWidth = sr.sprite.bounds.size.x;
    }

    public void LaunchAuthoritative(StickySlime owner, Vector2 dir, float maxRange)
    {
        Launch(owner, dir, maxRange, true);
    }

    public void LaunchReplica(StickySlime owner, Vector2 dir, float maxRange)
    {
        Launch(owner, dir, maxRange, false);
    }

    public void AttachToTarget(Rigidbody2D target)
    {
        if (target == null)
            return;

        hasHit = true;
        hitTarget = target;
    }

    public void Terminate()
    {
        Destroy(gameObject);
    }

    private void Launch(StickySlime owner, Vector2 dir, float maxRange, bool authoritative)
    {
        shooter = owner;
        direction = dir.sqrMagnitude > Mathf.Epsilon ? dir.normalized : Vector2.right;
        currentLength = 0f;
        maxLength = maxRange;
        isAuthoritative = authoritative;

        Collider2D shooterCol = shooter != null ? shooter.GetComponent<Collider2D>() : null;
        shooterRadius = 0.5f;
        if (shooterCol is BoxCollider2D boxCol)
        {
            Vector2 halfSize = Vector2.Scale(boxCol.size * 0.5f, boxCol.transform.lossyScale);
            Vector2 absDir = new Vector2(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            shooterRadius = Vector2.Dot(halfSize, absDir) + 0.1f;
        }
        else if (shooterCol != null)
        {
            shooterRadius = shooterCol.bounds.extents.magnitude + 0.1f;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Rigidbody2D shooterBody = shooter != null ? shooter.Rigidbody : null;
        if (shooterBody != null)
            transform.position = shooterBody.position + direction * shooterRadius;
    }

    void Update()
    {
        Rigidbody2D shooterBody = shooter != null ? shooter.Rigidbody : null;
        if (shooterBody == null)
        {
            Destroy(gameObject);
            return;
        }

        if (hasHit)
        {
            if (hitTarget == null || !shooter.IsTethered)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 shooterPos = shooterBody.position;
            Vector2 difference = hitTarget.position - shooterPos;
            float length = difference.magnitude;

            if (length > Mathf.Epsilon)
                direction = difference / length;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            UpdateVisual(shooterPos, length);
            return;
        }

        currentLength += extendSpeed * Time.deltaTime;
        bool reachedMaxLength = currentLength >= maxLength;
        if (reachedMaxLength)
            currentLength = maxLength;

        Vector2 origin = shooterBody.position + direction * shooterRadius;
        if (isAuthoritative)
        {
            const float raycastStartDistance = 0.1f;
            Vector2 rayOrigin = origin + direction * raycastStartDistance;
            if (currentLength > 0f)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    rayOrigin,
                    direction,
                    currentLength,
                    hitMask);
                if (hit.collider != null && !hit.collider.transform.IsChildOf(shooter.transform))
                {
                    currentLength = hit.distance;
                    UpdateVisual(origin, currentLength);
                    HandleAuthoritativeHit(hit);
                    return;
                }
            }
        }

        UpdateVisual(origin, currentLength);

        if (reachedMaxLength)
            FinishFlight();
    }

    private void UpdateVisual(Vector2 origin, float length)
    {
        if (sr == null || spriteWidth <= Mathf.Epsilon)
            return;

        float scale = length / spriteWidth;
        transform.localScale = new Vector3(scale, Mathf.Max(2f, scale), 1f);
        transform.position = origin + direction * (length * 0.5f);
    }

    private void HandleAuthoritativeHit(RaycastHit2D hit)
    {
        Rigidbody2D targetBody = hit.collider.attachedRigidbody;
        bool hitPlayer = hit.collider.CompareTag("Player") ||
                         (targetBody != null && targetBody.CompareTag("Player"));

        if (hitPlayer && targetBody != null && shooter.OnProjectileHit(targetBody))
        {
            AttachToTarget(targetBody);
            return;
        }

        FinishFlight();
    }

    private void FinishFlight()
    {
        if (isAuthoritative && shooter != null)
            shooter.OnProjectileTerminated(this);

        Destroy(gameObject);
    }
}
