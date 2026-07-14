using UnityEngine;
using UnityEngine.InputSystem;

public class AnchorSlime : PlayerControllerWithPhysics
{
    [Header("Anchor Tuning")]
    [Tooltip("Rigidbody mass for this heavy slime.")]
    public float anchorMass = 3f;

    [Tooltip("Ground walk speed — slower than the base slime.")]
    public float anchorWalkSpeed = 4f;

    [Tooltip("Minimum upward jump speed when fully uncharged.")]
    public float anchorMinJumpUpSpeed = 7f;

    [Tooltip("Maximum upward jump speed when fully charged.")]
    public float anchorMaxJumpUpSpeed = 12f;

    [Header("Brace Ability")]
    [Tooltip("Duration the anchor stays braced (seconds).")]
    public float braceDuration = 3f;

    [Tooltip("Cooldown before Brace can be used again (seconds).")]
    public float braceCooldown = 5f;

    [Tooltip("Visual scale when braced (squash effect).")]
    public Vector3 bracedScale = new Vector3(1.2f, 0.8f, 1f);

    private bool isBraced;
    private float braceTimer;
    private float cooldownTimer;
    private Vector3 originalScale;
    private RigidbodyType2D savedBodyType;

    protected override void Initialize()
    {
        if (rb != null)
            rb.mass = anchorMass;
        originalScale = transform.localScale;
    }

    protected override float GetWalkSpeed()
    {
        return anchorWalkSpeed;
    }

    protected override Vector2 ComputeJumpVelocity(float chargePercent, float direction)
    {
        float upSpeed = Mathf.Lerp(anchorMinJumpUpSpeed, anchorMaxJumpUpSpeed, chargePercent);
        float sideSpeed = direction * Mathf.Lerp(minJumpHorizontalSpeed, maxJumpHorizontalSpeed, chargePercent);
        return new Vector2(sideSpeed, upSpeed);
    }

    protected override bool CanChargeJump()
    {
        return !isBraced;
    }

    protected override void UpdateAbility()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (isBraced)
        {
            braceTimer -= Time.deltaTime;
            if (braceTimer <= 0f)
                EndBrace();
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame && isGrounded && cooldownTimer <= 0f)
            StartBrace();
    }

    protected override void FixedUpdateAbility()
    {
        if (isBraced)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void StartBrace()
    {
        isBraced = true;
        braceTimer = braceDuration;
        savedBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        transform.localScale = bracedScale;
        if (anim) anim.SetBool("isBraced", true);
    }

    private void EndBrace()
    {
        isBraced = false;
        rb.bodyType = savedBodyType;
        transform.localScale = originalScale;
        cooldownTimer = braceCooldown;
        if (anim) anim.SetBool("isBraced", false);
    }

    public bool IsBraced => isBraced;
}
