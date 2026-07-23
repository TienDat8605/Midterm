using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

public class StickySlime : PlayerControllerWithPhysics
{
    [Header("Wall Cling Passive")]
    [Tooltip("Maximum time the slime can cling to a wall (seconds).")]
    public float maxClingDuration = 2f;

    [Tooltip("Gravity scale while clinging to a wall.")]
    public float clingGravityScale = 0.1f;

    [Tooltip("Downward slide speed while clinging to a wall.")]
    public float wallSlideSpeed = 1f;

    [Tooltip("If true, Sticky will stick to walls on contact.")]
    public bool wallClingEnabled = true;

    [Tooltip("Layer mask for valid wall surfaces.")]
    public LayerMask wallLayer;

    [Header("Tether Ability")]
    [Tooltip("Prefab of the tether projectile to shoot.")]
    public GameObject tetherProjectilePrefab;

    [Tooltip("Speed of the tether projectile.")]
    public float tetherProjectileSpeed = 20f;

    [Tooltip("Maximum range to shoot the tether.")]
    public float tetherMaxRange = 8f;

    [Tooltip("Maximum force the tether can withstand before snapping.")]
    public float tetherBreakForce = 50f;

    [Tooltip("Duration the tether stays active (seconds).")]
    public float tetherDuration = 10f;

    [Tooltip("Cooldown before Tether can be used again (seconds).")]
    public float tetherCooldown = 5f;

    [Tooltip("Mass-independent velocity impulse applied to the attached slime when Tether is triggered again.")]
    public float tetherYankImpulse = 15f;

    [Tooltip("Layer mask for valid tether targets (teammates, anchor points).")]
    public LayerMask tetherTargetLayer;

    private bool isClinging;
    private float clingTimer;
    private float savedGravityScale;
    private Vector2 clingNormal;

    private bool isTethered;
    private float tetherTimer;
    private float tetherCooldownTimer;
    private Rigidbody2D tetheredTarget;
    private TetherProjectile activeTetherProjectile;
    private int tetherShotId;
    private int tetheredTargetViewId;
    private Vector2 tetherShotDirection;

    protected override void Initialize()
    {
        if (wallLayer.value == 0)
            wallLayer = groundLayer;
        if (tetherTargetLayer.value == 0)
            tetherTargetLayer = LayerMask.GetMask("Default");

    }

    protected override bool CanChargeJump()
    {
        return !isClinging;
    }

    protected override void UpdateAbility()
    {
        UpdateTether();
    }

    protected override void FixedUpdateAbility()
    {
        if (isClinging)
        {
            clingTimer -= Time.fixedDeltaTime;
            if (clingTimer <= 0f || isGrounded)
            {
                EndCling();
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, -wallSlideSpeed);
                rb.angularVelocity = 0f;
            }
        }

        if (isTethered && tetheredTarget != null)
        {
            CheckTetherBreak();
        }
    }

    protected override void PrepareForFlightMode()
    {
        if (isClinging)
            EndCling();
        if (isTethered)
            EndTether();
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (!wallClingEnabled)
        {
            base.OnCollisionEnter2D(collision);
            return;
        }

        HandleWallCollision(collision);
        if (!isClinging)
            base.OnCollisionEnter2D(collision);
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        if (!wallClingEnabled)
        {
            base.OnCollisionStay2D(collision);
            return;
        }

        HandleWallCollision(collision);
        if (!isClinging)
            base.OnCollisionStay2D(collision);
    }

    private void HandleWallCollision(Collision2D collision)
    {
        if (isGrounded || collision.gameObject.CompareTag("Player"))
            return;

        if (((1 << collision.gameObject.layer) & wallLayer.value) == 0)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) > 0.5f)
            {
                StartCling(-normal);
                return;
            }
        }
    }

    private void StartCling(Vector2 surfaceNormal)
    {
        if (isClinging)
        {
            clingTimer = maxClingDuration;
            return;
        }

        isClinging = true;
        clingTimer = maxClingDuration;
        clingNormal = surfaceNormal;
        savedGravityScale = rb.gravityScale;
        rb.gravityScale = clingGravityScale;
        rb.linearVelocity = new Vector2(0f, -wallSlideSpeed);
        if (anim) anim.SetBool("isClinging", true);
    }

    private void EndCling()
    {
        isClinging = false;
        rb.gravityScale = savedGravityScale;
        if (anim) anim.SetBool("isClinging", false);
    }

    protected override void Jump()
    {
        if (isClinging)
        {
            EndCling();
            rb.linearVelocity = new Vector2(clingNormal.x * maxJumpHorizontalSpeed, maxJumpUpSpeed);
            hasJumped = true;
            AudioManager.Instance?.PlaySFX(SFX.Jump);
            OnJumpLaunched(rb.linearVelocity);
            return;
        }
        base.Jump();
    }

    private void UpdateTether()
    {
        if (tetherCooldownTimer > 0f)
            tetherCooldownTimer -= Time.deltaTime;

        if (isTethered)
        {
            tetherTimer -= Time.deltaTime;
            if (tetherTimer <= 0f || tetheredTarget == null)
            {
                EndTether();
                return;
            }

            if (inputEnabled && Keyboard.current.eKey.wasPressedThisFrame)
                YankTetheredTarget();

            return;
        }

        if (activeTetherProjectile != null)
            return;

        if (inputEnabled && Keyboard.current.eKey.wasPressedThisFrame && tetherCooldownTimer <= 0f)
            TryShootTether();
    }

    private void TryShootTether()
    {
        if (tetherProjectilePrefab == null)
        {
            Debug.LogWarning("StickySlime: No tether projectile prefab assigned.");
            return;
        }

        Vector2 direction = GetAimDirection();
        tetherShotDirection = direction;

        GameObject projectileObj = Instantiate(tetherProjectilePrefab, rb.position, Quaternion.identity);
        activeTetherProjectile = projectileObj.GetComponent<TetherProjectile>();
        if (activeTetherProjectile != null)
        {
            activeTetherProjectile.extendSpeed = tetherProjectileSpeed;
            activeTetherProjectile.hitMask = tetherTargetLayer | groundLayer;
            activeTetherProjectile.LaunchAuthoritative(this, direction, tetherMaxRange);
        }

        tetherShotId = tetherShotId == int.MaxValue ? 1 : tetherShotId + 1;

        if (HasNetworkView)
            photonView.RPC(nameof(RpcTetherShot), RpcTarget.Others, tetherShotId, direction);

        tetherCooldownTimer = tetherCooldown;
    }

    private Vector2 GetAimDirection()
    {
        if (Mouse.current == null)
            return new Vector2(1f, 0.3f).normalized;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;
        return (mouseWorld - transform.position).normalized;
    }

    public bool OnProjectileHit(Rigidbody2D target)
    {
        if (target == null)
            return false;

        PhotonView targetView = target.GetComponent<PhotonView>();
        if (HasNetworkView && (targetView == null || targetView.ViewID == 0))
            return false;
        if (targetView != null && targetView == photonView)
            return false;

        int targetViewId = targetView != null ? targetView.ViewID : 0;
        StartTether(target, targetViewId);

        if (HasNetworkView)
        {
            photonView.RPC(
                nameof(RpcTetherAttached),
                RpcTarget.Others,
                tetherShotId,
                targetViewId);
        }

        return true;
    }

    private void StartTether(Rigidbody2D target, int targetViewId)
    {
        isTethered = true;
        tetherTimer = tetherDuration;
        tetheredTarget = target;
        tetheredTargetViewId = targetViewId;

        if (activeTetherProjectile != null)
            activeTetherProjectile.AttachToTarget(target);

        RegisterIncomingTetherIfLocallyOwned(targetViewId);

        if (anim) anim.SetBool("isTethered", true);
    }

    private void EndTether()
    {
        int endedShotId = tetherShotId;
        if (HasNetworkView)
            photonView.RPC(nameof(RpcTetherEnded), RpcTarget.Others, endedShotId);

        EndTetherLocal(endedShotId);
    }

    private void EndTetherLocal(int endedShotId)
    {
        ClearIncomingTetherIfLocallyOwned(endedShotId);
        isTethered = false;
        tetheredTarget = null;
        tetheredTargetViewId = 0;
        tetherCooldownTimer = tetherCooldown;

        if (activeTetherProjectile != null)
        {
            activeTetherProjectile.Terminate();
            activeTetherProjectile = null;
        }

        if (anim) anim.SetBool("isTethered", false);
    }

    private void CheckTetherBreak()
    {
        if (tetheredTarget == null)
            return;

        float currentDistance = Vector2.Distance(rb.position, tetheredTarget.position);
        if (currentDistance > 15f)
        {
            EndTether();
            return;
        }

    }

    public void OnProjectileTerminated(TetherProjectile projectile)
    {
        if (projectile != activeTetherProjectile || isTethered)
            return;

        activeTetherProjectile = null;
        if (HasNetworkView)
            photonView.RPC(nameof(RpcTetherEnded), RpcTarget.Others, tetherShotId);
    }

    private void YankTetheredTarget()
    {
        if (tetheredTarget == null)
        {
            EndTether();
            return;
        }

        if (HasNetworkView)
        {
            photonView.RPC(
                nameof(RpcTetherYanked),
                RpcTarget.Others,
                tetherShotId,
                tetheredTargetViewId,
                rb.position,
                tetherYankImpulse);
        }
        else
        {
            PlayerControllerWithPhysics targetController =
                tetheredTarget.GetComponent<PlayerControllerWithPhysics>();
            if (targetController != null)
            {
                targetController.ApplyTetherYank(rb.position, tetherYankImpulse);
            }
            else
            {
                Vector2 yankImpulse = TetherPhysics.CalculateYankImpulse(
                    tetheredTarget.position,
                    rb.position,
                    tetheredTarget.linearVelocity,
                    tetherYankImpulse,
                    tetheredTarget.mass);
                tetheredTarget.AddForce(yankImpulse, ForceMode2D.Impulse);
            }
        }

        EndTether();
    }

    [PunRPC]
    private void RpcTetherShot(int shotId, Vector2 direction, PhotonMessageInfo info)
    {
        if (!IsRpcFromOwner(info) || shotId <= tetherShotId)
            return;

        if (isTethered || activeTetherProjectile != null)
            EndTetherLocal(tetherShotId);

        tetherShotId = shotId;
        tetherShotDirection = direction;
        CreateReplicaProjectile();
    }

    [PunRPC]
    private void RpcTetherAttached(int shotId, int targetViewId, PhotonMessageInfo info)
    {
        if (!IsRpcFromOwner(info) || shotId != tetherShotId || targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        Rigidbody2D targetBody = targetView != null
            ? targetView.GetComponent<Rigidbody2D>()
            : null;
        if (targetBody == null || targetView == photonView)
            return;

        if (activeTetherProjectile == null)
            CreateReplicaProjectile();

        StartTether(targetBody, targetViewId);
    }

    [PunRPC]
    private void RpcTetherYanked(
        int shotId,
        int targetViewId,
        Vector2 stickyPosition,
        float impulse,
        PhotonMessageInfo info)
    {
        if (!IsRpcFromOwner(info) ||
            !isTethered ||
            shotId != tetherShotId ||
            targetViewId != tetheredTargetViewId)
        {
            return;
        }

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || !targetView.IsMine)
            return;

        PlayerControllerWithPhysics targetController =
            targetView.GetComponent<PlayerControllerWithPhysics>();
        if (targetController != null)
        {
            targetController.ApplyIncomingTetherYank(
                photonView.ViewID,
                shotId,
                stickyPosition,
                impulse);
        }
    }

    [PunRPC]
    private void RpcTetherEnded(int shotId, PhotonMessageInfo info)
    {
        if (!IsRpcFromOwner(info) || shotId != tetherShotId)
            return;

        EndTetherLocal(shotId);
    }

    private void CreateReplicaProjectile()
    {
        if (tetherProjectilePrefab == null || rb == null)
            return;

        GameObject projectileObject = Instantiate(
            tetherProjectilePrefab,
            rb.position,
            Quaternion.identity);
        activeTetherProjectile = projectileObject.GetComponent<TetherProjectile>();
        if (activeTetherProjectile == null)
            return;

        activeTetherProjectile.extendSpeed = tetherProjectileSpeed;
        activeTetherProjectile.LaunchReplica(this, tetherShotDirection, tetherMaxRange);
    }

    private void RegisterIncomingTetherIfLocallyOwned(int targetViewId)
    {
        if (!HasNetworkView || targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || !targetView.IsMine)
            return;

        PlayerControllerWithPhysics targetController =
            targetView.GetComponent<PlayerControllerWithPhysics>();
        if (targetController != null)
        {
            targetController.BeginIncomingTether(
                photonView.ViewID,
                tetherShotId);
        }
    }

    private void ClearIncomingTetherIfLocallyOwned(int shotId)
    {
        if (!HasNetworkView || tetheredTargetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(tetheredTargetViewId);
        if (targetView == null || !targetView.IsMine)
            return;

        PlayerControllerWithPhysics targetController =
            targetView.GetComponent<PlayerControllerWithPhysics>();
        if (targetController != null)
            targetController.EndIncomingTether(photonView.ViewID, shotId);
    }

    private bool IsRpcFromOwner(PhotonMessageInfo info)
    {
        return photonView != null &&
               photonView.Owner != null &&
               info.Sender == photonView.Owner;
    }

    private bool HasNetworkView =>
        PhotonNetwork.InRoom && photonView != null && photonView.ViewID != 0;

    private void OnDisable()
    {
        ClearIncomingTetherIfLocallyOwned(tetherShotId);

        if (activeTetherProjectile != null)
        {
            activeTetherProjectile.Terminate();
            activeTetherProjectile = null;
        }
    }

    public bool IsClinging => isClinging;
    public bool IsTethered => isTethered;
}
