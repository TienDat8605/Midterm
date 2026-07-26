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
        if (!TryGetCurrentWaypoint(out Transform target))
            return;

        Vector3 dir = target.position - transform.position;
        transform.position = Vector3.MoveTowards(transform.position, target.position, patrolSpeed * Time.deltaTime);
        if (dir.x != 0f)
            FlipX(dir.x < 0f);
        if (Vector3.Distance(transform.position, target.position) < 0.1f)
            MoveToNextValidWaypoint();
    }

    private Transform visual;

    protected override void Start()
    {
        base.Start();
        visual = transform.Find("Visual");
        if (visual == null)
            Debug.LogWarning("[BatSwarm] Visual child is missing; sprite flipping is disabled.", this);
    }

    private void FlipX(bool facingLeft)
    {
        if (visual == null)
            return;

        Vector3 s = visual.localScale;
        visual.localScale = new Vector3(facingLeft ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
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
            if (waypoints[i] == null)
                continue;

            float d = Vector3.Distance(transform.position, waypoints[i].position);
            if (d >= minDist)
                continue;
            minDist = d;
            waypointIndex = i;
        }
    }

    private bool TryGetCurrentWaypoint(out Transform waypoint)
    {
        waypoint = null;
        if (waypoints == null || waypoints.Length == 0)
            return false;

        waypointIndex = Mathf.Clamp(waypointIndex, 0, waypoints.Length - 1);
        if (waypoints[waypointIndex] != null)
        {
            waypoint = waypoints[waypointIndex];
            return true;
        }

        return MoveToNextValidWaypoint(out waypoint);
    }

    private void MoveToNextValidWaypoint()
    {
        MoveToNextValidWaypoint(out _);
    }

    private bool MoveToNextValidWaypoint(out Transform waypoint)
    {
        waypoint = null;
        if (waypoints == null || waypoints.Length == 0)
            return false;

        for (int offset = 1; offset <= waypoints.Length; offset++)
        {
            int candidateIndex = (waypointIndex + offset) % waypoints.Length;
            Transform candidate = waypoints[candidateIndex];
            if (candidate == null)
                continue;

            waypointIndex = candidateIndex;
            waypoint = candidate;
            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
