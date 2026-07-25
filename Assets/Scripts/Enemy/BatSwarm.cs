using Photon.Pun;
using UnityEngine;

public class BatSwarm : EnemyBase
{
    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float patrolSpeed = 3f;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float maxDashDistance = 8f;
    [SerializeField] private float preDashDelay = 0.4f;
    [SerializeField] private LayerMask wallLayer;

    private enum BatState { Patrolling, PreDash, Dashing }
    private BatState batState = BatState.Patrolling;

    private int waypointIndex;
    private Vector2 dashDirection;
    private Vector3 dashOrigin;
    private float preDashTimer;

    protected override void UpdateBehavior()
    {
        switch (batState)
        {
            case BatState.Patrolling:
                Patrol();
                if (IsAuthority)
                    TryDetectPlayer();
                break;
            case BatState.PreDash:
                preDashTimer -= Time.deltaTime;
                if (preDashTimer <= 0f)
                {
                    dashOrigin = transform.position;
                    batState = BatState.Dashing;
                }
                break;
            case BatState.Dashing:
                float stepDist = dashSpeed * Time.deltaTime;
                RaycastHit2D wallHit = Physics2D.Raycast(transform.position, dashDirection, stepDist + 0.1f, wallLayer);
                if (wallHit.collider != null)
                {
                    EndDash();
                    break;
                }
                transform.position += (Vector3)(dashDirection * stepDist);
                if (dashDirection.x != 0f)
                    FlipX(dashDirection.x < 0f);
                if (Vector3.Distance(transform.position, dashOrigin) >= maxDashDistance)
                    EndDash();
                break;
        }
    }

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;
        Transform target = waypoints[waypointIndex];
        Vector3 dir = target.position - transform.position;
        transform.position = Vector3.MoveTowards(transform.position, target.position, patrolSpeed * Time.deltaTime);
        if (dir.x != 0f)
            FlipX(dir.x < 0f);
        if (Vector3.Distance(transform.position, target.position) < 0.1f)
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
    }

    private Transform visual;

    protected override void Start()
    {
        base.Start();
        visual = transform.Find("Visual");
        Debug.Log($"BatSwarm Start: visual={(visual != null ? visual.name : "NULL")}");
    }

    private void FlipX(bool facingLeft)
    {
        if (visual == null) { Debug.Log("FlipX: visual is null"); return; }
        Vector3 s = visual.localScale;
        visual.localScale = new Vector3(facingLeft ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
        Debug.Log($"FlipX: facingLeft={facingLeft} scale={visual.localScale}");
    }

    private void TryDetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (hit == null)
            return;
        dashDirection = (hit.transform.position - transform.position).normalized;
        batState = BatState.PreDash;
        preDashTimer = preDashDelay;
    }

    private void EndDash()
    {
        batState = BatState.Patrolling;
        DisableTemporarily();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (batState != BatState.Dashing)
            return;
        PlayerControllerWithPhysics player = other.GetComponent<PlayerControllerWithPhysics>();
        if (player == null)
            return;
        HitPlayer(player, dashDirection * knockbackForce);
        EndDash();
    }

    protected override void OnBecomeDisabled()
    {
        batState = BatState.Patrolling;
        // snap to nearest waypoint so it resumes patrol cleanly
        if (waypoints == null || waypoints.Length == 0)
            return;
        float minDist = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float d = Vector3.Distance(transform.position, waypoints[i].position);
            if (d >= minDist)
                continue;
            minDist = d;
            waypointIndex = i;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
