using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Base physics controller for playable slimes (formerly the GreenSlime physics script).
/// Implements charge-to-jump ("Jump King") ground movement + wall-bonk.
/// Subclass per slime role (Anchor, Bouncy, Sticky) to tune stats and hook into
/// jump/land events for abilities.
/// </summary>
public class PlayerControllerWithPhysics : MonoBehaviour
{
    [Header("Ground Movement")]
    public float walkSpeed = 7f;

    [Header("Physics")]
    [Tooltip("Overrides the Rigidbody2D gravity scale at start.")]
    public float gravityScale = 1f;

    [Header("Jump King Jump")]
    public float maxChargeTime = 1f;
    public float minJumpUpSpeed = 8f;
    public float maxJumpUpSpeed = 15f;
    public float minJumpHorizontalSpeed = 3f;
    public float maxJumpHorizontalSpeed = 9f;

    [Header("Ground Check")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Input Control")]
    [Tooltip("When false, this slime ignores player input but physics and abilities still run.")]
    public bool inputEnabled = true;

    protected Rigidbody2D rb;
    public Rigidbody2D Rigidbody => rb;
    protected Animator anim;
    protected float moveInput;
    protected float jumpCharge;
    protected float jumpDirection;
    protected bool isChargingJump;
    protected bool isGrounded;
    protected float defaultGravityScale;
    protected float lastAirHorizontalSpeed;
    protected bool hasJumped;

    private Collider2D[] overlapCache = new Collider2D[8];
    private ContactFilter2D overlapFilter;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        defaultGravityScale = gravityScale;
        rb.gravityScale = gravityScale;
        if (groundCheckPoint == null)
            groundCheckPoint = transform.Find("GroundCheck");
        if (groundLayer.value == 0)
            groundLayer = LayerMask.GetMask("Ground");
        overlapFilter = new ContactFilter2D();
        overlapFilter.useTriggers = false;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && col.sharedMaterial == null)
        {
            PhysicsMaterial2D slimeFriction = new PhysicsMaterial2D("SlimeFriction");
            slimeFriction.friction = 0.8f;
            slimeFriction.bounciness = 0f;
            col.sharedMaterial = slimeFriction;
        }
        
        UpdateGrounded();
        Initialize();
        TryAssignCamera();
    }

    /// <summary>
    /// Auto-assigns the main camera to follow this slime.
    /// Works with CameraFollow2D (instant) and CameraFollow (smooth) on the Main Camera.
    /// </summary>
    private void TryAssignCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        CameraFollow2D follow2D = mainCam.GetComponent<CameraFollow2D>();
        if (follow2D != null)
        {
            follow2D.target = transform;
            return;
        }

        CameraFollow follow = mainCam.GetComponent<CameraFollow>();
        if (follow != null)
            follow.SetTarget(transform);
    }

    /// <summary>
    /// Override this instead of Start() to add per-slime setup.
    /// </summary>
    protected virtual void Initialize() { }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        UpdateAbility();

        if (!inputEnabled)
            return;

        float horizontalInput = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            horizontalInput = 1f;
        else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            horizontalInput = -1f;

        if (!isGrounded)
        {
            moveInput = 0f;
            isChargingJump = false;
            if (anim) anim.SetBool("isCharging", false);
            return;
        }

        moveInput = isChargingJump ? 0f : horizontalInput;

        if (CanChargeJump() && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isChargingJump = true;
            jumpCharge = 0f;
            if (anim) anim.SetBool("isCharging", true);
        }

        if (!isChargingJump)
            return;

        jumpDirection = horizontalInput;
        jumpCharge = Mathf.Min(jumpCharge + Time.deltaTime, maxChargeTime);

        if (Keyboard.current.spaceKey.wasReleasedThisFrame || jumpCharge >= maxChargeTime)
            Jump();
    }

    protected virtual void UpdateAbility() { }

    void FixedUpdate()
    {
        UpdateGrounded();
        if (anim) anim.SetBool("isGrounded", isGrounded);

        if (isGrounded && !hasJumped)
        {
            if (isChargingJump)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            else if (inputEnabled)
            {
                rb.linearVelocity = new Vector2(moveInput * GetWalkSpeed(), rb.linearVelocity.y);
            }
        }
        else if (!Mathf.Approximately(rb.linearVelocity.x, 0f))
        {
            lastAirHorizontalSpeed = rb.linearVelocity.x;
        }

        hasJumped = false;
        FixedUpdateAbility();
    }

    protected virtual void FixedUpdateAbility() { }

    /// <summary>
    /// Override to change ground movement speed (e.g. Anchor moves slower).
    /// </summary>
    protected virtual float GetWalkSpeed()
    {
        return walkSpeed;
    }

    /// <summary>
    /// Override to forbid charging in specific states (e.g. Sticky wall-cling).
    /// </summary>
    protected virtual bool CanChargeJump()
    {
        return true;
    }

    protected virtual void Jump()
    {
        float chargePercent = maxChargeTime <= 0f ? 1f : jumpCharge / maxChargeTime;
        Vector2 launchVelocity = ComputeJumpVelocity(chargePercent, jumpDirection);

        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = launchVelocity;
        lastAirHorizontalSpeed = launchVelocity.x;
        moveInput = 0f;
        jumpCharge = 0f;
        isChargingJump = false;
        isGrounded = false;
        hasJumped = true;
        if (anim)
        {
            anim.SetBool("isCharging", false);
            anim.SetBool("isGrounded", false);
            anim.SetTrigger("doJump");
        }

        OnJumpLaunched(launchVelocity);
    }

    /// <summary>
    /// Override to change jump velocity per role (e.g. Anchor jumps lower, Bouncy jumps higher).
    /// </summary>
    protected virtual Vector2 ComputeJumpVelocity(float chargePercent, float direction)
    {
        float upSpeed = Mathf.Lerp(minJumpUpSpeed, maxJumpUpSpeed, chargePercent);
        float sideSpeed = direction * Mathf.Lerp(minJumpHorizontalSpeed, maxJumpHorizontalSpeed, chargePercent);
        return new Vector2(sideSpeed, upSpeed);
    }

    /// <summary>
    /// Called right after the jump velocity has been applied.
    /// </summary>
    protected virtual void OnJumpLaunched(Vector2 launchVelocity) { }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        WallBonk(collision);
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        WallBonk(collision);
    }

    protected virtual void WallBonk(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            return;

        if (isGrounded || Mathf.Approximately(lastAirHorizontalSpeed, 0f))
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) <= Mathf.Abs(normal.y))
                continue;

            float speed = Mathf.Max(Mathf.Abs(lastAirHorizontalSpeed), Mathf.Abs(rb.linearVelocity.x));
            rb.linearVelocity = new Vector2(Mathf.Sign(normal.x) * speed, rb.linearVelocity.y);
            lastAirHorizontalSpeed = rb.linearVelocity.x;
            return;
        }
    }

    protected virtual void UpdateGrounded()
    {
        bool wasGrounded = isGrounded;

        if (groundCheckPoint != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
            if (!isGrounded)
                isGrounded = CheckForPlayerBelow();
        }
        else
        {
            isGrounded = false;
        }

        if (wasGrounded != isGrounded)
            OnGroundedChanged(isGrounded);
    }

    private bool CheckForPlayerBelow()
    {
        if (groundCheckPoint == null)
            return false;

        int count = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, overlapFilter, overlapCache);
        for (int i = 0; i < count; i++)
        {
            if (overlapCache[i].CompareTag("Player") && overlapCache[i].transform != transform)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Called when grounded state changes.
    /// </summary>
    protected virtual void OnGroundedChanged(bool grounded) { }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }
}
