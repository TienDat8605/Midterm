using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class VortexTrap : MonoBehaviour
{
    [Header("Capture")]
    [SerializeField, Min(0.1f)] private float captureRadius = 3f;
    [SerializeField, Min(0.1f)] private float captureDuration = 3f;
    [SerializeField, Min(0f)] private float orbitRadius = 1.1f;
    [SerializeField] private bool clockwise = false;

    [Header("Rotation")]
    [SerializeField, Min(0f)] private float startingOrbitSpeed = 120f;
    [SerializeField, Min(0f)] private float finalOrbitSpeed = 2000f;
    [SerializeField, Min(0f)] private float finalAnimationSpeedMultiplier = 2.5f;

    [Header("Launch")]
    [SerializeField, Min(0f)] private float downwardLaunchImpulse = 30f;
    [SerializeField, Min(0f)] private float pauseAfterRelease = 1f;

    private readonly Dictionary<Rigidbody2D, CapturedSlime> captured = new Dictionary<Rigidbody2D, CapturedSlime>();
    private Vector3 initialScale;
    private float captureResumeTime;
    private Animator animator;

    private sealed class CapturedSlime
    {
        public float angle;
        public float elapsed;
        public bool inputWasEnabled;
        public float gravityScale;
    }

    private void Awake()
    {
        initialScale = transform.localScale;
        animator = GetComponent<Animator>();
    }

    private void FixedUpdate()
    {
        if (Time.time < captureResumeTime)
        {
            SetVisualSpeed(0f);
            return;
        }

        CaptureNearbySlimes();

        float visualProgress = GetVisualProgress();
        SetVisualSpeed(visualProgress);

        bool releaseAll = false;
        foreach (KeyValuePair<Rigidbody2D, CapturedSlime> pair in captured)
        {
            if (pair.Key == null)
                continue;

            CapturedSlime slime = pair.Value;
            slime.elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(slime.elapsed / captureDuration);
            float speed = Mathf.Lerp(startingOrbitSpeed, finalOrbitSpeed, progress);
            // Slimes orbit opposite to the Vortex's visual rotation.
            float direction = clockwise ? 1f : -1f;
            slime.angle += direction * speed * Time.fixedDeltaTime;

            Vector2 center = transform.position;
            Vector2 offset = Quaternion.Euler(0f, 0f, slime.angle) * Vector2.right * orbitRadius;
            pair.Key.MovePosition(center + offset);
            pair.Key.linearVelocity = Vector2.zero;

            if (slime.elapsed >= captureDuration)
                releaseAll = true;
        }

        if (releaseAll)
            ReleaseAllSlimes();
    }

    private float GetVisualProgress()
    {
        float progress = 0f;
        foreach (CapturedSlime slime in captured.Values)
            progress = Mathf.Max(progress, Mathf.Clamp01(slime.elapsed / captureDuration));
        return progress;
    }

    private void SetVisualSpeed(float progress)
    {
        if (animator != null)
            animator.speed = Mathf.Lerp(1f, finalAnimationSpeedMultiplier, progress);
    }

    private void ReleaseAllSlimes()
    {
        foreach (KeyValuePair<Rigidbody2D, CapturedSlime> pair in captured)
        {
            if (pair.Key == null)
                continue;

            // Restore simulation before launching so the impulse is handled by
            // the physics engine instead of appearing as a teleport.
            pair.Key.bodyType = RigidbodyType2D.Dynamic;
            pair.Key.gravityScale = pair.Value.gravityScale;
            pair.Key.linearVelocity = Vector2.zero;
            pair.Key.WakeUp();
            pair.Key.AddForce(Vector2.down * downwardLaunchImpulse, ForceMode2D.Impulse);

            PlayerControllerWithPhysics controller = pair.Key.GetComponent<PlayerControllerWithPhysics>();
            if (controller != null)
                controller.inputEnabled = pair.Value.inputWasEnabled;
        }

        captured.Clear();
        captureResumeTime = Time.time + pauseAfterRelease;
    }

    private void CaptureNearbySlimes()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, captureRadius);
        foreach (Collider2D hit in hits)
        {
            Rigidbody2D body = hit.attachedRigidbody;
            if (body == null || captured.ContainsKey(body) || !IsSlime(body) || !HasInputAuthority(body))
                continue;

            PlayerControllerWithPhysics controller = body.GetComponent<PlayerControllerWithPhysics>();
            captured.Add(body, new CapturedSlime
            {
                angle = Mathf.Atan2(body.position.y - transform.position.y, body.position.x - transform.position.x) * Mathf.Rad2Deg,
                inputWasEnabled = controller == null || controller.inputEnabled,
                gravityScale = body.gravityScale
            });

            if (controller != null)
                controller.inputEnabled = false;

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }
    }

    private bool IsSlime(Rigidbody2D body)
    {
        return body.CompareTag("Player") || body.GetComponent<PlayerControllerWithPhysics>() != null;
    }

    private bool HasInputAuthority(Rigidbody2D body)
    {
        PhotonView view = body.GetComponent<PhotonView>();
        return view == null || view.ViewID == 0 || view.IsMine;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
}
