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

    protected override void UpdateBehavior()
    {
        // Only Master Client fires projectiles; PUN.Instantiate propagates to all clients
        if (!IsAuthority)
            return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (hit == null)
            return;

        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f)
            return;

        FireAt(hit.transform.position);
        fireTimer = fireInterval;
    }

    private void FireAt(Vector3 targetPos)
    {
        if (firePoint == null)
            return;
        Vector2 dir = ((Vector2)targetPos - (Vector2)firePoint.position).normalized;
        GameObject proj = PhotonNetwork.Instantiate(projectilePrefabName, firePoint.position, Quaternion.identity);
        proj.GetComponent<SplitterProjectile>()?.Init(dir, projectileSpeed);
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
