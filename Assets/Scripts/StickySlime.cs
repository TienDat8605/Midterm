using UnityEngine;
using UnityEngine.InputSystem;

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

    [Tooltip("Force applied to pull the tethered target.")]
    public float tetherPullForce = 15f;

    [Tooltip("Maximum force the tether can withstand before snapping.")]
    public float tetherBreakForce = 50f;

    [Tooltip("Duration the tether stays active (seconds).")]
    public float tetherDuration = 3f;

    [Tooltip("Cooldown before Tether can be used again (seconds).")]
    public float tetherCooldown = 5f;

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
    private DistanceJoint2D tetherJoint;

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
                EndTether();
            return;
        }

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

        GameObject projectileObj = Instantiate(tetherProjectilePrefab, rb.position, Quaternion.identity);
        TetherProjectile projectile = projectileObj.GetComponent<TetherProjectile>();
        if (projectile != null)
        {
            projectile.extendSpeed = tetherProjectileSpeed;
            projectile.hitMask = tetherTargetLayer | groundLayer;
            projectile.Launch(this, direction, tetherMaxRange);
        }

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

    public void OnProjectileHit(Rigidbody2D target)
    {
        if (target == null)
            return;

        StartTether(target);
    }

    private void StartTether(Rigidbody2D target)
    {
        isTethered = true;
        tetherTimer = tetherDuration;
        tetheredTarget = target;

        if (anim) anim.SetBool("isTethered", true);
    }

    private void EndTether()
    {
        isTethered = false;
        tetheredTarget = null;
        tetherCooldownTimer = tetherCooldown;

        if (tetherJoint != null)
        {
            Destroy(tetherJoint);
            tetherJoint = null;
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

        Vector2 toTarget = (tetheredTarget.position - rb.position).normalized;
        
        if (currentDistance > 1.5f)
        {
            float pullStrength = tetherPullForce * 10f;
            rb.AddForce(toTarget * pullStrength, ForceMode2D.Force);
            tetheredTarget.AddForce(-toTarget * pullStrength, ForceMode2D.Force);
        }

        if (currentDistance < 2.5f)
        {
            float approachSpeed = Vector2.Dot(rb.linearVelocity, toTarget);
            if (approachSpeed > 2f)
            {
                rb.linearVelocity -= toTarget * (approachSpeed - 2f);
            }

            float targetApproachSpeed = Vector2.Dot(tetheredTarget.linearVelocity, -toTarget);
            if (targetApproachSpeed > 2f)
            {
                tetheredTarget.linearVelocity -= (-toTarget) * (targetApproachSpeed - 2f);
            }

            Vector2 pushDirection = -toTarget;
            if (pushDirection.sqrMagnitude < 0.01f)
                pushDirection = Vector2.right;
            
            float pushStrength = (2.5f - currentDistance) * 400f;
            rb.AddForce(pushDirection * pushStrength, ForceMode2D.Force);
            tetheredTarget.AddForce(-pushDirection * pushStrength, ForceMode2D.Force);
        }
    }

    public bool IsClinging => isClinging;
    public bool IsTethered => isTethered;
}
