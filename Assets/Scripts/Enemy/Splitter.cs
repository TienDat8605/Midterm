using Photon.Pun;
using UnityEngine;

public class Splitter : EnemyBase
{
    [Header("Fire")]
    [SerializeField] private string projectilePrefabName = "SplitterProjectile";
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireInterval = 2.5f;
    [SerializeField] private float projectileSpeed = 6f;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private LayerMask playerLayer;

    private float fireTimer;
    private SpriteRenderer visualRenderer;

    protected override void Start()
    {
        base.Start();
        Transform v = transform.Find("Visual");
        if (v != null) visualRenderer = v.GetComponentInChildren<SpriteRenderer>();
    }

    private void FaceTarget(Vector2 targetPos)
    {
        if (visualRenderer == null) return;
        visualRenderer.flipX = targetPos.x < transform.position.x;
    }

    protected override void UpdateBehavior()
    {
        if (!IsAuthority)
            return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (hit == null)
            return;

        FaceTarget(hit.transform.position);

        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f)
            return;

        Rigidbody2D playerRb = hit.GetComponent<Rigidbody2D>();
        Vector2 playerPos = hit.transform.position;
        Vector2 playerVel = playerRb != null ? playerRb.linearVelocity : Vector2.zero;
        float dist = Vector2.Distance(firePoint.position, playerPos);
        float travelTime = dist / projectileSpeed;
        Vector2 offset = Vector2.ClampMagnitude(playerVel * travelTime, 3f);
        Vector2 predictedPos = playerPos + offset;

        FireAt(predictedPos);
        fireTimer = fireInterval;
    }

    private void FireAt(Vector3 targetPos)
    {
        if (firePoint == null)
            return;

        Vector2 toTarget = (Vector2)targetPos - (Vector2)firePoint.position;
        float g = Mathf.Abs(Physics2D.gravity.y);
        float timeToTarget = Mathf.Max(Mathf.Abs(toTarget.x) / projectileSpeed, 0.1f);
        float vx = toTarget.x / timeToTarget;
        float vy = (toTarget.y + 0.5f * g * timeToTarget * timeToTarget) / timeToTarget;
        Vector2 launchVelocity = new Vector2(vx, vy);

        GameObject proj;
        if (PhotonNetwork.InRoom)
            proj = PhotonNetwork.Instantiate(projectilePrefabName, firePoint.position, Quaternion.identity);
        else
            proj = Instantiate(Resources.Load<GameObject>(projectilePrefabName), firePoint.position, Quaternion.identity);
        proj.GetComponent<SplitterProjectile>()?.Init(launchVelocity);
    }

    protected override void OnBecomeDisabled()
    {
        fireTimer = fireInterval;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
