using UnityEngine;
using UnityEngine.InputSystem;

public class BouncySlime : PlayerControllerWithPhysics
{
    [Header("Bouncy Tuning")]
    [Tooltip("Minimum upward jump speed when fully uncharged.")]
    public float bouncyMinJumpUpSpeed = 12f;

    [Tooltip("Maximum upward jump speed when fully charged.")]
    public float bouncyMaxJumpUpSpeed = 18f;

    [Header("Passive Bounce")]
    [Tooltip("Upward velocity applied when another slime lands on this one.")]
    public float passiveBounceForce = 12f;

    [Header("Trampoline Ability")]
    [Tooltip("Duration the trampoline stays active (seconds).")]
    public float trampolineDuration = 4f;

    [Tooltip("Cooldown before Trampoline can be used again (seconds).")]
    public float trampolineCooldown = 6f;

    [Tooltip("Upward launch velocity for teammates in trampoline mode.")]
    public float trampolineLaunchForce = 20f;

    [Tooltip("Visual scale when in trampoline mode (squash effect).")]
    public Vector3 trampolineScale = new Vector3(1.4f, 0.5f, 1f);

    private bool isTrampoline;
    private float trampolineTimer;
    private float cooldownTimer;
    private Vector3 originalScale;

    protected override void Initialize()
    {
        originalScale = transform.localScale;
    }

    protected override Vector2 ComputeJumpVelocity(float chargePercent, float direction)
    {
        float upSpeed = Mathf.Lerp(bouncyMinJumpUpSpeed, bouncyMaxJumpUpSpeed, chargePercent);
        float sideSpeed = direction * Mathf.Lerp(minJumpHorizontalSpeed, maxJumpHorizontalSpeed, chargePercent);
        return new Vector2(sideSpeed, upSpeed);
    }

    protected override bool CanChargeJump()
    {
        return !isTrampoline;
    }

    protected override void UpdateAbility()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (isTrampoline)
        {
            trampolineTimer -= Time.deltaTime;
            if (trampolineTimer <= 0f)
                EndTrampoline();
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame && isGrounded && cooldownTimer <= 0f)
            StartTrampoline();
    }

    protected override void FixedUpdateAbility()
    {
        if (isTrampoline)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (!HasInputAuthority)
            return;

        HandleBounceCollision(collision);
    }

    private void HandleBounceCollision(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (normal.y < 0.5f)
                continue;

            Rigidbody2D otherRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (otherRb == null)
                continue;

            float launchForce = isTrampoline ? trampolineLaunchForce : passiveBounceForce;
            otherRb.linearVelocity = new Vector2(otherRb.linearVelocity.x, launchForce);
            break;
        }
    }

    private void StartTrampoline()
    {
        isTrampoline = true;
        trampolineTimer = trampolineDuration;
        transform.localScale = trampolineScale;
        if (anim) anim.SetBool("isTrampoline", true);
    }

    private void EndTrampoline()
    {
        isTrampoline = false;
        transform.localScale = originalScale;
        cooldownTimer = trampolineCooldown;
        if (anim) anim.SetBool("isTrampoline", false);
    }

    public bool IsTrampoline => isTrampoline;
}
