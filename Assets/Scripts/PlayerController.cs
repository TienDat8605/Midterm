using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Horizontal Movement")]
    public float maxSpeed = 7f;         
    public float acceleration = 35f;     
    public float deceleration = 25f;     

    [Header("Explicit Jump Settings")]
    [Tooltip("The maximum height (in grid units) the slime can reach.")]
    public float maxJumpHeight = 3.5f; 
    [Tooltip("How fast the slime launches upward. Increase this for a faster snap.")]
    public float jumpUpSpeed = 15f;
    [Tooltip("The maximum speed the slime can reach while falling down.")]
    public float maxFallSpeed = 20f;
    [Tooltip("Extra gravity scaling applied only while falling down to snap to the ground.")]
    public float fallGravityMultiplier = 3f;

    [Header("Ground Check")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private float targetHorizontalInput;
    private float currentHorizontalSpeed;
    private bool isGrounded;
    private float defaultGravityScale;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
    }

    void Update()
    {
        targetHorizontalInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) 
                targetHorizontalInput = 1f;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) 
                targetHorizontalInput = -1f;

            // Trigger the jump using your explicit upward speed
            if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpUpSpeed);
            }
        }
    }

    void FixedUpdate()
    {
        // 1. Handle sliding horizontal movement
        float targetSpeed = targetHorizontalInput * maxSpeed;
        float speedRate = (targetHorizontalInput != 0) ? acceleration : deceleration;
        currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, speedRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(currentHorizontalSpeed, rb.linearVelocity.y);

        // 2. Dynamic Height & Custom Fall Gravity Calculation
        if (rb.linearVelocity.y > 0)
        {
            // If the player releases the spacebar early, or passes the max allowed height, cut upward velocity
            if ((Keyboard.current != null && !Keyboard.current.spaceKey.isPressed) || transform.position.y > maxJumpHeight)
            {
                rb.gravityScale = defaultGravityScale * 2f; // Quick brake
            }
            else
            {
                rb.gravityScale = defaultGravityScale;
            }
        }
        else if (rb.linearVelocity.y < 0)
        {
            // Apply custom falling gravity scale for a punchy drop
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;

            // Clamp the maximum falling speed so it never exceeds your setting
            float clampedYVelocity = Mathf.Max(rb.linearVelocity.y, -maxFallSpeed);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, clampedYVelocity);
        }
        else
        {
            rb.gravityScale = defaultGravityScale;
        }

        // 3. Ground Check
        if (groundCheckPoint != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        else
        {
            isGrounded = true; 
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}