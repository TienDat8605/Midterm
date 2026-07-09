using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

/// <summary>
/// Adapted from your original PlayerController to work with PUN 2.
/// Key changes:
///   1. Inherits MonoBehaviourPun instead of MonoBehaviour
///   2. All input/physics only runs if photonView.IsMine (local player)
///   3. Implements IPunObservable to sync position + velocity to other clients
/// </summary>
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerControllerMulti : MonoBehaviourPun, IPunObservable
{
    [Header("Horizontal Movement")]
    public float maxSpeed = 7f;
    public float acceleration = 35f;
    public float deceleration = 25f;

    [Header("Explicit Jump Settings")]
    [Tooltip("The maximum height (in grid units) the slime can reach.")]
    public float maxJumpHeight = 3.5f;
    [Tooltip("How fast the slime launches upward.")]
    public float jumpUpSpeed = 15f;
    [Tooltip("The maximum speed the slime can reach while falling down.")]
    public float maxFallSpeed = 20f;
    [Tooltip("Extra gravity scaling applied only while falling down.")]
    public float fallGravityMultiplier = 3f;

    [Header("Ground Check")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Rigidbody2D rb;
    private float targetHorizontalInput;
    private float currentHorizontalSpeed;
    private bool isGrounded;
    private float defaultGravityScale;

    // Network smoothing — used only for remote (non-mine) players
    private Vector3 networkPosition;
    private Vector2 networkVelocity;
    private const float LerpSpeed = 10f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
        networkPosition = transform.position;

        if (photonView.IsMine)
        {
            // Rename so you can see which is local in Hierarchy
            gameObject.name = $"Player_LOCAL (Actor {PhotonNetwork.LocalPlayer.ActorNumber})";
            
            CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
            if (cam != null)
                cam.SetTarget(transform);
            else
                Debug.LogError("[Player] CameraFollow not found on Main Camera!");
        }
        else
        {
            // Rename remote player too
            gameObject.name = $"Player_REMOTE";
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }
    void Update()
    {
        // ---- Only the local (owned) player reads input ----
        if (!photonView.IsMine)
        {
            SmoothRemotePlayer();
            return;
        }

        targetHorizontalInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                targetHorizontalInput = 1f;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                targetHorizontalInput = -1f;

            if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpUpSpeed);
            }
        }
    }

    void FixedUpdate()
    {
        // ---- Only simulate physics for the local player ----
        if (!photonView.IsMine) return;

        // 1. Horizontal movement
        float targetSpeed = targetHorizontalInput * maxSpeed;
        float speedRate = (targetHorizontalInput != 0) ? acceleration : deceleration;
        currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, speedRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(currentHorizontalSpeed, rb.linearVelocity.y);

        // 2. Jump gravity logic
        if (rb.linearVelocity.y > 0)
        {
            if ((Keyboard.current != null && !Keyboard.current.spaceKey.isPressed) || transform.position.y > maxJumpHeight)
                rb.gravityScale = defaultGravityScale * 2f;
            else
                rb.gravityScale = defaultGravityScale;
        }
        else if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
            float clampedY = Mathf.Max(rb.linearVelocity.y, -maxFallSpeed);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, clampedY);
        }
        else
        {
            rb.gravityScale = defaultGravityScale;
        }

        // 3. Ground check
        if (groundCheckPoint != null)
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
        else
            isGrounded = true;
    }

    // -------------------------------------------------------------------------
    // Remote player smoothing
    // -------------------------------------------------------------------------

    private void SmoothRemotePlayer()
    {
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * LerpSpeed);
    }

    // -------------------------------------------------------------------------
    // IPunObservable — called by PhotonView to sync state across the network
    // -------------------------------------------------------------------------

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(rb.linearVelocity);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkVelocity = (Vector2)stream.ReceiveNext();
        }
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}