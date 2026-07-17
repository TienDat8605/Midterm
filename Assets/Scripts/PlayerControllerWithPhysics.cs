using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

/// <summary>
/// Base physics controller for playable slimes (formerly the GreenSlime physics script).
/// Implements charge-to-jump ("Jump King") ground movement + wall-bonk.
/// Subclass per slime role (Anchor, Bouncy, Sticky) to tune stats and hook into
/// jump/land events for abilities.
/// </summary>
public class PlayerControllerWithPhysics : MonoBehaviourPun, IPunObservable
{
    [Header("Ground Movement")]
    public float walkSpeed = 7f;

    [Header("Physics")]
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
    public bool inputEnabled = true;

    protected Rigidbody2D rb;
    public Rigidbody2D Rigidbody => rb;
    protected Animator anim;
    protected SpriteRenderer spriteRenderer;
    protected float moveInput;
    protected float jumpCharge;
    protected float jumpDirection;
    protected bool isChargingJump;
    protected bool isGrounded;
    protected float defaultGravityScale;
    protected float lastAirHorizontalSpeed;
    protected bool hasJumped;

    [Header("Network Smoothing")]
    [SerializeField] private float networkPositionLerpSpeed = 12f;
    [SerializeField] private float networkRotationLerpSpeed = 12f;

    private Vector2 networkPosition;
    private Vector2 networkVelocity;
    private float networkRotation;
    private Vector3 networkScale;
    private bool networkIsGrounded;
    private bool networkIsChargingJump;

    /// <summary>
    /// Local scene instances remain controllable. Photon-instantiated instances are
    /// controlled and simulated only by their owning client.
    /// </summary>
    protected bool HasInputAuthority =>
        photonView == null || photonView.ViewID == 0 || photonView.IsMine;

    private LayerMask groundAndPlayerMask;
    private RaycastHit2D[] raycastHitBuffer = new RaycastHit2D[8];

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        defaultGravityScale = gravityScale;
        rb.gravityScale = gravityScale;
        networkPosition = rb.position;
        networkVelocity = rb.linearVelocity;
        networkRotation = rb.rotation;
        networkScale = transform.localScale;
        if (groundCheckPoint == null)
            groundCheckPoint = transform.Find("GroundCheck");
        if (groundLayer.value == 0)
            groundLayer = LayerMask.GetMask("Ground");

        groundAndPlayerMask = groundLayer | LayerMask.GetMask("Default");

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && col.sharedMaterial == null)
        {
            PhysicsMaterial2D slimeFriction = new PhysicsMaterial2D("SlimeFriction");
            slimeFriction.friction = 0.5f;
            slimeFriction.bounciness = 0f;
            col.sharedMaterial = slimeFriction;
        }

        Initialize();

        if (HasInputAuthority)
        {
            UpdateGrounded();
            TryAssignCamera();
        }
        else
        {
            inputEnabled = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }
    }

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

    protected virtual void Initialize() { }

    void Update()
    {
        if (!HasInputAuthority)
        {
            UpdateRemoteVisuals();
            return;
        }

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
            jumpDirection = 0f;
            if (anim) anim.SetBool("isCharging", true);
        }

        if (!isChargingJump)
            return;

        if (horizontalInput != 0f)
            jumpDirection = horizontalInput;
        jumpCharge = Mathf.Min(jumpCharge + Time.deltaTime, maxChargeTime);

        if (Keyboard.current.spaceKey.wasReleasedThisFrame || jumpCharge >= maxChargeTime)
            Jump();
    }

    protected virtual void UpdateAbility() { }

    void FixedUpdate()
    {
        if (!HasInputAuthority)
        {
            SmoothRemoteMovement();
            return;
        }

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

        if (spriteRenderer != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            spriteRenderer.flipX = rb.linearVelocity.x < 0f;
        }

        hasJumped = false;
        FixedUpdateAbility();
    }

    protected virtual void FixedUpdateAbility() { }

    protected virtual float GetWalkSpeed()
    {
        return walkSpeed;
    }

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

    protected virtual Vector2 ComputeJumpVelocity(float chargePercent, float direction)
    {
        float upSpeed = Mathf.Lerp(minJumpUpSpeed, maxJumpUpSpeed, chargePercent);
        float sideSpeed = direction * Mathf.Lerp(minJumpHorizontalSpeed, maxJumpHorizontalSpeed, chargePercent);
        return new Vector2(sideSpeed, upSpeed);
    }

    protected virtual void OnJumpLaunched(Vector2 launchVelocity) { }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!HasInputAuthority)
            return;

        WallBonk(collision);
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (!HasInputAuthority)
            return;

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
        isGrounded = false;

        if (groundCheckPoint != null)
        {
            Collider2D col = GetComponent<Collider2D>();
            float halfWidth = col != null ? col.bounds.size.x * 0.5f : 0.3f;
            float rayLength = groundCheckRadius + 0.1f;

            Vector2 origin = groundCheckPoint.position;
            Vector2 leftOrigin = new Vector2(origin.x - halfWidth * 0.4f, origin.y);
            Vector2 rightOrigin = new Vector2(origin.x + halfWidth * 0.4f, origin.y);

            if (CheckGroundRay(origin, rayLength) ||
                CheckGroundRay(leftOrigin, rayLength) ||
                CheckGroundRay(rightOrigin, rayLength))
            {
                isGrounded = true;
            }
        }

        if (wasGrounded != isGrounded)
            OnGroundedChanged(isGrounded);
    }

    private bool CheckGroundRay(Vector2 origin, float length)
    {
        int count = Physics2D.RaycastNonAlloc(origin, Vector2.down, raycastHitBuffer, length, groundAndPlayerMask);
        for (int i = 0; i < count; i++)
        {
            Collider2D hitCol = raycastHitBuffer[i].collider;
            if (hitCol == null || hitCol.transform == transform)
                continue;

            if (hitCol.CompareTag("Player"))
                return true;

            if (((1 << hitCol.gameObject.layer) & groundLayer.value) != 0)
                return true;
        }
        return false;
    }

    protected virtual void OnGroundedChanged(bool grounded) { }

    private void SmoothRemoteMovement()
    {
        Vector2 smoothedPosition = Vector2.Lerp(
            rb.position,
            networkPosition,
            networkPositionLerpSpeed * Time.fixedDeltaTime);
        float smoothedRotation = Mathf.LerpAngle(
            rb.rotation,
            networkRotation,
            networkRotationLerpSpeed * Time.fixedDeltaTime);

        rb.MovePosition(smoothedPosition);
        rb.MoveRotation(smoothedRotation);
    }

    private void UpdateRemoteVisuals()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            networkScale,
            networkPositionLerpSpeed * Time.deltaTime);

        if (anim == null)
            return;

        anim.SetBool("isGrounded", networkIsGrounded);
        anim.SetBool("isCharging", networkIsChargingJump);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (stream.IsWriting)
        {
            stream.SendNext(rb.position);
            stream.SendNext(rb.linearVelocity);
            stream.SendNext(rb.rotation);
            stream.SendNext(transform.localScale);
            stream.SendNext(isGrounded);
            stream.SendNext(isChargingJump);
            return;
        }

        Vector2 receivedPosition = (Vector2)stream.ReceiveNext();
        networkVelocity = (Vector2)stream.ReceiveNext();
        networkRotation = (float)stream.ReceiveNext();
        networkScale = (Vector3)stream.ReceiveNext();
        networkIsGrounded = (bool)stream.ReceiveNext();
        networkIsChargingJump = (bool)stream.ReceiveNext();

        float lag = Mathf.Clamp((float)(PhotonNetwork.Time - info.SentServerTime), 0f, 1f);
        networkPosition = receivedPosition + networkVelocity * lag;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null)
            return;

        Collider2D col = GetComponent<Collider2D>();
        float halfWidth = col != null ? col.bounds.size.x * 0.5f : 0.3f;
        float rayLength = groundCheckRadius + 0.1f;

        Vector2 origin = groundCheckPoint.position;
        Vector2 leftOrigin = new Vector2(origin.x - halfWidth * 0.4f, origin.y);
        Vector2 rightOrigin = new Vector2(origin.x + halfWidth * 0.4f, origin.y);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + Vector2.down * rayLength);
        Gizmos.DrawLine(leftOrigin, leftOrigin + Vector2.down * rayLength);
        Gizmos.DrawLine(rightOrigin, rightOrigin + Vector2.down * rayLength);
    }
}
