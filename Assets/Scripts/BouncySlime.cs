using System.Collections.Generic;
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
    [Tooltip("Upward velocity applied when another slime lands on this one in normal mode.")]
    public float passiveBounceForce = 0f;

    [Header("Trampoline Ability")]
    [Tooltip("Fixed upward velocity applied when another slime lands on trampoline.")]
    public float trampolineBounceForce = 35f;

    [Tooltip("Multiplier for incoming velocity - higher values give bigger boost.")]
    public float trampolineBoostMultiplier = 1.5f;

    [Tooltip("Maximum upward launch velocity cap.")]
    public float trampolineMaxLaunchForce = 50f;

    [Tooltip("How wide the slime stretches in trampoline mode.")]
    public float trampolineWidthScale = 1.6f;

    [Tooltip("How flat the slime becomes in trampoline mode.")]
    public float trampolineHeightScale = 0.4f;

    [Header("Bounce Cooldown")]
    [Tooltip("Minimum time between bounces for the same target (seconds).")]
    public float bounceCooldown = 0.2f;

    private bool isTrampoline;
    private Vector3 originalScale;
    private float originalDrawWidth;
    private Vector2 originalBoxSize;
    private BoxCollider2D boxCollider;
    private Dictionary<Rigidbody2D, float> lastBounceTimes = new Dictionary<Rigidbody2D, float>();

    protected override void Initialize()
    {
        originalScale = transform.localScale;
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
            originalBoxSize = boxCollider.size;
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

        CleanupBounceCooldowns();
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
        if (!isTrampoline)
            return;

        if (!collision.gameObject.CompareTag("Player"))
            return;

        Rigidbody2D otherRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (otherRb == null || otherRb == rb)
            return;

        // Respect per-target cooldown.
        float now = Time.time;
        if (lastBounceTimes.TryGetValue(otherRb, out float lastTime) && now - lastTime < bounceCooldown)
            return;

        // Only bounce if contact is on the top/bottom surface (vertical collision).
        bool verticalContact = false;
        float bestNormalY = 0f;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.y) > Mathf.Abs(bestNormalY))
                bestNormalY = normal.y;
            if (Mathf.Abs(normal.y) > 0.3f)
                verticalContact = true;
        }

        Debug.Log($"[BouncySlime] Player collision. bestNormalY={bestNormalY:F2}, verticalContact={verticalContact}, otherVelY={otherRb.linearVelocity.y:F2}");

        if (!verticalContact)
            return;

        lastBounceTimes[otherRb] = now;

        float incomingSpeed = Mathf.Abs(otherRb.linearVelocity.y);
        float boostedSpeed = incomingSpeed * trampolineBoostMultiplier;
        float finalSpeed = Mathf.Max(boostedSpeed, trampolineBounceForce);
        finalSpeed = Mathf.Min(finalSpeed, trampolineMaxLaunchForce);

        Debug.Log($"[BouncySlime] BOUNCING {collision.gameObject.name} upward with speed {finalSpeed}!");
        otherRb.linearVelocity = new Vector2(otherRb.linearVelocity.x, finalSpeed);
    }

    private void CleanupBounceCooldowns()
    {
        float now = Time.time;
        List<Rigidbody2D> expired = new List<Rigidbody2D>();
        foreach (var pair in lastBounceTimes)
        {
            if (now - pair.Value > bounceCooldown * 2f)
                expired.Add(pair.Key);
        }
        foreach (var entry in expired)
        {
            lastBounceTimes.Remove(entry);
        }
    }

    private void StartTrampoline()
    {
        isTrampoline = true;

        Vector3 newScale = new Vector3(originalScale.x * trampolineWidthScale, originalScale.y * trampolineHeightScale, originalScale.z);
        float oldHalfHeight = (originalBoxSize.y * originalScale.y) * 0.5f;
        float newHalfHeight = (originalBoxSize.y * newScale.y) * 0.5f;
        float yOffset = oldHalfHeight - newHalfHeight;

        transform.localScale = newScale;
        transform.position += new Vector3(0f, -yOffset, 0f);

        if (anim) anim.SetBool("isTrampoline", true);
    }

    private void EndTrampoline()
    {
        isTrampoline = false;

        float currentHalfHeight = (originalBoxSize.y * transform.localScale.y) * 0.5f;
        float newHalfHeight = (originalBoxSize.y * originalScale.y) * 0.5f;
        float yOffset = currentHalfHeight - newHalfHeight;

        transform.localScale = originalScale;
        transform.position += new Vector3(0f, yOffset, 0f);

        if (anim) anim.SetBool("isTrampoline", false);
    }

    public bool IsTrampoline => isTrampoline;
}
