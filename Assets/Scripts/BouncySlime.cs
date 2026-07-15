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
    public float passiveBounceForce = 18f;

    [Header("Trampoline Ability")]
    [Tooltip("Upward launch velocity for teammates in trampoline mode.")]
    public float trampolineLaunchForce = 25f;

    [Tooltip("How wide the slime stretches in trampoline mode.")]
    public float trampolineWidthScale = 1.6f;

    [Tooltip("How flat the slime becomes in trampoline mode.")]
    public float trampolineHeightScale = 0.4f;

    private bool isTrampoline;
    private Vector3 originalScale;
    private float originalDrawWidth;

    protected override void Initialize()
    {
        originalScale = transform.localScale;
        if (spriteRenderer != null && spriteRenderer.drawMode == SpriteDrawMode.Tiled)
            originalDrawWidth = spriteRenderer.size.x;
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
        if (inputEnabled && Keyboard.current.eKey.wasPressedThisFrame && isGrounded)
        {
            if (isTrampoline)
                EndTrampoline();
            else
                StartTrampoline();
        }
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
        transform.localScale = new Vector3(originalScale.x * trampolineWidthScale, originalScale.y * trampolineHeightScale, originalScale.z);
        if (anim) anim.SetBool("isTrampoline", true);
    }

    private void EndTrampoline()
    {
        isTrampoline = false;
        transform.localScale = originalScale;
        if (anim) anim.SetBool("isTrampoline", false);
    }

    public bool IsTrampoline => isTrampoline;
}
