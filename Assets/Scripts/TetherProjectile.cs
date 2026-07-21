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
    private bool hasHit;
    private Rigidbody2D hitTarget;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            spriteWidth = sr.sprite.bounds.size.x;

    }

    public void Launch(StickySlime owner, Vector2 dir, float maxRange)
    {
        shooter = owner;
        direction = dir.normalized;
        currentLength = 0f;
        maxLength = maxRange;

        Collider2D shooterCol = shooter.GetComponent<Collider2D>();
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

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        transform.position = shooter.Rigidbody.position + direction * shooterRadius;

        Debug.Log($"[TetherProjectile] Launched. Direction: {direction}, ShooterRadius: {shooterRadius}");
    }

    void Update()
    {
        if (shooter == null)
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

            Vector2 shooterPos = shooter.Rigidbody.position;
            Vector2 targetPos = hitTarget.position;
            Vector2 diff = targetPos - shooterPos;
            float length = diff.magnitude;

            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            direction = diff.normalized;
            UpdateVisual(shooterPos, length);
            return;
        }

        currentLength += extendSpeed * Time.deltaTime;
        bool reachedMaxLength = currentLength >= maxLength;
        if (reachedMaxLength)
            currentLength = maxLength;

        Vector2 origin = shooter.Rigidbody.position + direction * shooterRadius;
        float raycastStartDist = 0.1f;
        Vector2 rayOrigin = origin + direction * raycastStartDist;
        float raycastLength = currentLength;

        if (raycastLength > 0f)
        {
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, direction, raycastLength, hitMask);
            if (hit.collider != null)
            {
                if (hit.collider.transform == shooter.transform)
                {
                    if (reachedMaxLength)
                    {
                        UpdateVisual(origin, currentLength);
                        Destroy(gameObject);
                    }
                    return;
                }

                currentLength = hit.distance;
                UpdateVisual(origin, currentLength);
                OnHit(hit);
                return;
            }
        }

        UpdateVisual(origin, currentLength);

        if (reachedMaxLength)
            Destroy(gameObject);
    }

    void UpdateVisual(Vector2 origin, float length)
    {
        if (sr == null)
            return;

        float scale = length / spriteWidth;
        transform.localScale = new Vector3(scale, Mathf.Max(2f, scale), 1f);
        transform.position = origin + direction * (length * 0.5f);
    }

    void OnHit(RaycastHit2D hit)
    {
        if (hit.collider.CompareTag("Player") && hit.collider.transform != shooter.transform)
        {
            hasHit = true;
            hitTarget = hit.collider.GetComponent<Rigidbody2D>();
            if (hitTarget != null)
                shooter.OnProjectileHit(hitTarget);
        }
        else
        {
            Debug.Log($"[TetherProjectile] Hit non-player object: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}. Destroying.");
            Destroy(gameObject);
        }
    }
}
