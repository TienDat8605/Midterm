using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControllerWithPhysics : MonoBehaviour
{
    [Header("Ground Movement")]
    public float walkSpeed = 7f;

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

    private Rigidbody2D rb;
    private float moveInput;
    private float jumpCharge;
    private float jumpDirection;
    private bool isChargingJump;
    private bool isGrounded;
    private float defaultGravityScale;
    private float lastAirHorizontalSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
        if (groundCheckPoint == null)
            groundCheckPoint = transform.Find("GroundCheck");
        if (groundLayer.value == 0)
            groundLayer = LayerMask.GetMask("Ground");
        UpdateGrounded();
    }

    void Update()
    {
        if (Keyboard.current == null)
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
            return;
        }

        moveInput = isChargingJump ? 0f : horizontalInput;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isChargingJump = true;
            jumpCharge = 0f;
        }

        if (!isChargingJump)
            return;

        jumpDirection = horizontalInput;
        jumpCharge = Mathf.Min(jumpCharge + Time.deltaTime, maxChargeTime);

        if (Keyboard.current.spaceKey.wasReleasedThisFrame || jumpCharge >= maxChargeTime)
            Jump();
    }

    void FixedUpdate()
    {
        UpdateGrounded();

        if (isGrounded && !isChargingJump)
            rb.linearVelocity = new Vector2(moveInput * walkSpeed, rb.linearVelocity.y);
        else if (isGrounded)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        else if (!Mathf.Approximately(rb.linearVelocity.x, 0f))
            lastAirHorizontalSpeed = rb.linearVelocity.x;
    }

    private void Jump()
    {
        float chargePercent = maxChargeTime <= 0f ? 1f : jumpCharge / maxChargeTime;
        float upSpeed = Mathf.Lerp(minJumpUpSpeed, maxJumpUpSpeed, chargePercent);
        float sideSpeed = jumpDirection * Mathf.Lerp(minJumpHorizontalSpeed, maxJumpHorizontalSpeed, chargePercent);

        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = new Vector2(sideSpeed, upSpeed);
        lastAirHorizontalSpeed = sideSpeed;
        moveInput = 0f;
        jumpCharge = 0f;
        isChargingJump = false;
        isGrounded = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        WallBonk(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        WallBonk(collision);
    }

    private void WallBonk(Collision2D collision)
    {
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

    private void UpdateGrounded()
    {
        isGrounded = groundCheckPoint != null &&
            Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }
}
