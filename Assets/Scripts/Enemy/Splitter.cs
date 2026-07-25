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
    private Transform visual;

    protected override void Start()
    {
        base.Start();
        visual = transform.Find("Visual");
    }

    private void FaceTarget(Vector2 targetPos)
    {
        if (visual == null) return;
        bool playerIsRight = targetPos.x > transform.position.x;
        Vector3 s = visual.localScale;
        // default scale x=1 faces left, so flip (x=-1) when player is right
        visual.localScale = new Vector3(playerIsRight ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
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

        float dirX = Mathf.Sign(targetPos.x - firePoint.position.x);
        Vector2 launchVelocity = new Vector2(dirX * projectileSpeed, 0f);

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
