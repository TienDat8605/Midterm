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

    [Header("Stone Ability")]
    [Tooltip("Visual scale when turned to stone (squash effect).")]
    public Vector3 stoneScale = new Vector3(1.2f, 0.8f, 1f);

    [Tooltip("Color tint when turned to stone (darker).")]
    public Color stoneColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private bool isStone;
    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private RigidbodyType2D savedBodyType;

    protected override void Initialize()
    {
        if (rb != null)
            rb.mass = anchorMass;
        originalScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
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
        return !isStone;
    }

    protected override void UpdateAbility()
    {
        if (inputEnabled && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (isStone)
                EndStone();
            else if (isGrounded)
                StartStone();
        }
    }

    protected override void FixedUpdateAbility()
    {
        if (isStone)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void StartStone()
    {
        isStone = true;
        savedBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (spriteRenderer != null)
            spriteRenderer.color = stoneColor;
        if (anim)
        {
            anim.speed = 0f;
            anim.SetBool("isBraced", true);
        }
    }

    private void EndStone()
    {
        isStone = false;
        rb.bodyType = savedBodyType;
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        if (anim)
        {
            anim.speed = 1f;
            anim.SetBool("isBraced", false);
        }
    }

    public bool IsStone => isStone;
}
