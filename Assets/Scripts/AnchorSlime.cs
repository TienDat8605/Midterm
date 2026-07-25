using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

public class AnchorSlime : PlayerControllerWithPhysics, IBraceable
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
    private Color originalColor;
    private RigidbodyType2D savedBodyType;

    protected override void Initialize()
    {
        if (rb != null)
            rb.mass = anchorMass;
        originalScale = transform.localScale;
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
            else if (!isGrounded)
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

    protected override void PrepareForFlightMode()
    {
        if (isStone)
            EndStone();
    }

    protected override void PrepareForTetherYank()
    {
        if (isStone)
            EndStone();
    }

    private void StartStone()
    {
        savedBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        SetStoneVisual(true);
        SyncStoneVisual(true);
    }

    private void EndStone()
    {
        rb.bodyType = savedBodyType;
        SetStoneVisual(false);
        SyncStoneVisual(false);
    }

    private void SyncStoneVisual(bool active)
    {
        if (photonView != null && photonView.ViewID != 0)
            photonView.RPC(nameof(RpcSetStoneVisual), RpcTarget.Others, active);
    }

    [PunRPC]
    public void RpcSetStoneVisual(bool active)
    {
        SetStoneVisual(active);
    }

    private void SetStoneVisual(bool active)
    {
        isStone = active;
        if (spriteRenderer != null)
            spriteRenderer.color = active ? stoneColor : originalColor;
        if (anim)
        {
            anim.SetBool("isBraced", active);
            anim.speed = active ? 0f : 1f;
        }
    }

    public bool IsStone => isStone;
    public bool IsBraced => isStone;
}
