using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class BirdEnemy : MonoBehaviourPun, IPunObservable
{
    public enum BirdState { Idle, Moving }

    // Ping-pong idle animation: 1 -> 2 -> 3 -> 4 -> 5 -> 4 -> 3 -> 2 -> 1.
    // The loop delay then holds frame 1 before the next cycle starts.
    private static readonly int[] IdleFrameIndices = { 0, 1, 10, 11, 12, 11, 10, 1, 0 };
    private static readonly int[] MovingFrameIndices = { 2, 3, 4, 5, 6, 7, 8, 9 };

    [Header("Path and Movement")]
    [SerializeField] private BirdPath path;
    [SerializeField, Min(0f)] private float minSpeed = 0.5f;
    [SerializeField, Min(0f)] private float maxSpeed = 2f;
    [SerializeField, Min(0f)] private float acceleration = 4f;
    [SerializeField, Min(0f)] private float deceleration = 6f;
    [SerializeField, Min(0.01f)] private float animationSpeed = 8f;
    [SerializeField, Min(0.01f)] private float idleFrameDuration = 0.18f;
    [SerializeField, Min(0f)] private float idleLoopDelay = 0.35f;
    [SerializeField] private int currentWaypoint;
    [SerializeField] private int pathDirection = 1;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float knockbackStrength = 18f;
    [SerializeField, Min(0f)] private float hitCooldown = 0.75f;
    [SerializeField] private float knockbackAngleRange = 25f;

    [Header("Sprites")]
    [SerializeField] private Sprite[] frames = new Sprite[13];

    [SerializeField] private BirdState state = BirdState.Idle;
    private float waitRemaining;
    private float currentSpeed;
    private float animationTimer;
    private bool lastAnimationMoving;
    private float lastHitTime = -Mathf.Infinity;
    private Vector2 networkPosition;
    private BirdState networkState;
    private int networkWaypoint;
    private int networkDirection;
    private int facingDirection = 1;
    private int networkFacingDirection = 1;
    private SpriteRenderer spriteRenderer;
    private Collider2D birdCollider;
    private readonly Dictionary<int, float> playerHitTimes = new Dictionary<int, float>();

    public BirdPath Path => path;
    public BirdState State => state;
    public int CurrentWaypoint => currentWaypoint;
    public int PathDirection => pathDirection;
    public float WaitRemaining => waitRemaining;
    public float MovementSpeed { get => maxSpeed; set => maxSpeed = Mathf.Max(0f, value); }
    public float MinSpeed { get => minSpeed; set => minSpeed = Mathf.Max(0f, value); }
    public float MaxSpeed { get => maxSpeed; set => maxSpeed = Mathf.Max(0f, value); }
    public float Acceleration { get => acceleration; set => acceleration = Mathf.Max(0f, value); }
    public float Deceleration { get => deceleration; set => deceleration = Mathf.Max(0f, value); }
    public float CurrentSpeed => currentSpeed;

    private void OnValidate()
    {
        minSpeed = Mathf.Max(0f, minSpeed);
        maxSpeed = Mathf.Max(minSpeed, maxSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        idleFrameDuration = Mathf.Max(0.01f, idleFrameDuration);
        idleLoopDelay = Mathf.Max(0f, idleLoopDelay);
    }

    private bool HasAuthority => photonView == null || photonView.ViewID == 0 || photonView.IsMine;

    public static Vector2 GetKnockbackDirection(float angleDegrees)
    {
        float angle = Mathf.Clamp(angleDegrees, -25f, 25f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
    }

    public void ConfigurePath(BirdPath assignedPath, int waypoint = 0)
    {
        path = assignedPath;
        currentWaypoint = Mathf.Clamp(waypoint, 0, Mathf.Max(0, path != null ? path.WaypointCount - 1 : 0));
        pathDirection = 1;
        state = BirdState.Idle;
        currentSpeed = 0f;
        waitRemaining = path != null ? path.GetWaitDuration(currentWaypoint) : 0f;
        if (path != null)
            transform.position = path.GetWaypointPosition(currentWaypoint);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        birdCollider = GetComponent<Collider2D>();
        // BirdEnemy owns the SpriteRenderer frame-by-frame.  Leaving an Animator
        // enabled on the same renderer lets it overwrite the selected frame.
        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;
        networkPosition = transform.position;
        networkState = state;
        if (frames == null || frames.Length < 13)
            frames = new Sprite[13];
        animationTimer = 0f;
        lastAnimationMoving = false;
        SetAnimationFrame(false);
    }

    private void Start()
    {
        if (path == null)
        {
            BirdSpawner spawner = FindFirstObjectByType<BirdSpawner>();
            if (photonView != null && photonView.InstantiationData != null && photonView.InstantiationData.Length > 0 && spawner != null)
                path = spawner.GetPath((int)photonView.InstantiationData[0]);
        }
        if (path != null && currentWaypoint >= path.WaypointCount)
            ConfigurePath(path);
    }

    private void Update()
    {
        if (!HasAuthority)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, 12f * Time.deltaTime);
            UpdateSprite(networkState == BirdState.Moving, Vector2.right * networkFacingDirection);
            return;
        }

        if (path == null || path.WaypointCount == 0)
        {
            UpdateSprite(false, Vector2.right * facingDirection);
            return;
        }
        if (state == BirdState.Idle)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
            waitRemaining -= Time.deltaTime;
            if (waitRemaining <= 0f && path.WaypointCount > 1)
                BeginMoving();
            UpdateSprite(state == BirdState.Moving, GetMovementDirection());
            return;
        }

        Vector3 target = path.GetWaypointPosition(currentWaypoint);
        Vector2 movementDirection = (Vector2)(target - transform.position);
        float distanceToTarget = Vector3.Distance(transform.position, target);
        float targetSpeed = Mathf.Max(minSpeed, maxSpeed);
        float stoppingDistance = deceleration > 0f
            ? currentSpeed * currentSpeed / (2f * deceleration)
            : 0f;
        bool shouldBrake = deceleration > 0f && distanceToTarget <= stoppingDistance + 0.05f;
        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            shouldBrake ? 0f : targetSpeed,
            (shouldBrake ? deceleration : acceleration) * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, target, currentSpeed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude <= 0.0001f ||
            (shouldBrake && currentSpeed <= 0.001f))
        {
            transform.position = target;
            state = BirdState.Idle;
            waitRemaining = path.GetWaitDuration(currentWaypoint);
        }
        UpdateSprite(state == BirdState.Moving, movementDirection);
    }

    private void BeginMoving()
    {
        currentWaypoint += pathDirection;
        if (currentWaypoint >= path.WaypointCount || currentWaypoint < 0)
        {
            pathDirection = -pathDirection;
            currentWaypoint += pathDirection * 2;
        }
        state = BirdState.Moving;
        currentSpeed = Mathf.Max(currentSpeed, minSpeed);
        animationTimer = 0f;
    }

    private Vector2 GetMovementDirection()
    {
        if (path == null || state != BirdState.Moving || currentWaypoint < 0 || currentWaypoint >= path.WaypointCount)
            return Vector2.right * facingDirection;

        return (Vector2)(path.GetWaypointPosition(currentWaypoint) - transform.position);
    }

    private void UpdateSprite(bool moving, Vector2 movementDirection)
    {
        if (spriteRenderer == null || frames == null)
            return;
        if (moving != lastAnimationMoving)
        {
            animationTimer = 0f;
            lastAnimationMoving = moving;
        }

        animationTimer += Time.deltaTime;
        SetAnimationFrame(moving);
        if (moving && Mathf.Abs(movementDirection.x) > 0.001f)
            facingDirection = movementDirection.x > 0f ? 1 : -1;
        // The source sprite sheet faces left: keep leftward travel unflipped and
        // mirror only while travelling right.
        spriteRenderer.flipX = facingDirection > 0;
    }

    private void SetAnimationFrame(bool moving)
    {
        if (spriteRenderer == null || frames == null)
            return;
        int[] sequence = moving ? MovingFrameIndices : IdleFrameIndices;
        float frameDuration = moving ? 1f / animationSpeed : idleFrameDuration;
        float loopDuration = frameDuration * sequence.Length;
        float animationTime;
        if (moving)
        {
            animationTime = animationTimer % loopDuration;
        }
        else
        {
            float idleCycleTime = animationTimer % (loopDuration + idleLoopDelay);
            animationTime = Mathf.Min(idleCycleTime, loopDuration - frameDuration);
        }
        int frameIndex = Mathf.Clamp(Mathf.FloorToInt(animationTime / frameDuration), 0, sequence.Length - 1);
        Sprite frame = frames[sequence[frameIndex]];
        if (frame != null)
            spriteRenderer.sprite = frame;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!HasAuthority || state != BirdState.Moving || Time.time - lastHitTime < hitCooldown)
            return;
        PlayerControllerWithPhysics networkPlayer = other.GetComponentInParent<PlayerControllerWithPhysics>();
        PlayerController localPlayer = other.GetComponentInParent<PlayerController>();
        if (networkPlayer == null && localPlayer == null)
            return;
        PhotonView targetView = other.GetComponentInParent<PhotonView>();
        int key = targetView != null ? targetView.ViewID : other.GetInstanceID();
        if (playerHitTimes.TryGetValue(key, out float previousHit) && Time.time - previousHit < hitCooldown)
            return;
        Vector2 velocity = GetKnockbackDirection(Random.Range(-knockbackAngleRange, knockbackAngleRange)) * knockbackStrength;
        if (targetView != null && targetView.ViewID != 0)
            targetView.RPC("ApplyBirdKnockbackRpc", targetView.Owner, velocity);
        else
            ApplyKnockback(other, velocity);
        playerHitTimes[key] = Time.time;
        lastHitTime = Time.time;
    }
    private static void ApplyKnockback(Collider2D source, Vector2 velocity)
    {
        BirdKnockbackReceiver receiver = source.GetComponentInParent<BirdKnockbackReceiver>();
        if (receiver != null)
        {
            receiver.ApplyBirdKnockback(velocity);
            return;
        }

        Rigidbody2D body = source.GetComponentInParent<Rigidbody2D>();
        if (body != null)
            body.linearVelocity = velocity;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext((Vector2)transform.position); stream.SendNext((int)state);
            stream.SendNext(currentWaypoint); stream.SendNext(pathDirection); stream.SendNext(facingDirection);
        }
        else
        {
            networkPosition = (Vector2)stream.ReceiveNext(); networkState = (BirdState)(int)stream.ReceiveNext();
            networkWaypoint = (int)stream.ReceiveNext(); networkDirection = (int)stream.ReceiveNext();
            networkFacingDirection = (int)stream.ReceiveNext();
        }
    }
}
