using System.Collections;
using UnityEngine;
using Photon.Pun;

public class SpringTrap : MonoBehaviour
{
    [SerializeField, Min(0f)] private float launchSpeed = 14f;
    [SerializeField, Min(0f)] private float reactivationDelay = 0.1f;
    [SerializeField] private Sprite[] springSprites;
    [SerializeField, Min(0.01f)] private float frameDuration = 0.06f;

    private SpriteRenderer spriteRenderer;
    private Coroutine animationRoutine;
    private float nextActivationTime;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        SetSprite(0);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Rigidbody2D body = collision.rigidbody;
        if (body == null || !IsSupportedSlime(body) || Time.time < nextActivationTime)
            return;

        PhotonView view = body.GetComponent<PhotonView>();
        if (view != null && view.ViewID != 0 && !view.IsMine)
            return;

        Launch(body);
    }

    private bool IsSupportedSlime(Rigidbody2D body)
    {
        return body.CompareTag("Player") || body.GetComponent<PlayerControllerWithPhysics>() != null;
    }

    private void Launch(Rigidbody2D body)
    {
        Vector2 direction = transform.up.normalized;
        float incomingSpeed = Vector2.Dot(body.linearVelocity, direction);
        body.linearVelocity += direction * (launchSpeed - incomingSpeed);
        nextActivationTime = Time.time + reactivationDelay;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(PlayAnimation());
    }

    private IEnumerator PlayAnimation()
    {
        for (int frame = 0; frame < springSprites.Length; frame++)
        {
            SetSprite(frame);
            yield return new WaitForSeconds(frameDuration);
        }
        for (int frame = springSprites.Length - 2; frame >= 0; frame--)
        {
            SetSprite(frame);
            yield return new WaitForSeconds(frameDuration);
        }
        animationRoutine = null;
    }

    private void SetSprite(int index)
    {
        if (spriteRenderer != null && springSprites != null && springSprites.Length > 0)
            spriteRenderer.sprite = springSprites[Mathf.Clamp(index, 0, springSprites.Length - 1)];
    }
}
