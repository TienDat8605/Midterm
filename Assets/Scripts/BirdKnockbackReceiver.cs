using System.Collections;
using Photon.Pun;
using UnityEngine;

public class BirdKnockbackReceiver : MonoBehaviourPun
{
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;


    [Header("Hit Effect")]
    [SerializeField] private Sprite[] hitEffectFrames;
    [SerializeField, Min(0.01f)] private float hitEffectFrameDuration = 0.08f;
    [SerializeField, Min(0.1f)] private float hitEffectScale = 3f;
    private Coroutine hitFlashCoroutine;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    [PunRPC]
    public void ApplyBirdKnockbackRpc(Vector2 velocity)
    {
        bool ownsPhysics = photonView == null || photonView.ViewID == 0 || photonView.IsMine;
        if (ownsPhysics)
        {
            if (body == null)
                body = GetComponent<Rigidbody2D>();
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
                body.linearVelocity = velocity;
        }

        // Every client displays and hears the transient hit event, while only the
        // struck player's owner changes the authoritative Rigidbody2D.
        PlayHitFeedback();
    }

    public void ApplyBirdKnockback(Vector2 velocity)
    {
        ApplyBirdKnockbackRpc(velocity);
    }

    private void PlayHitFeedback()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(SFX.BirdHit);

        if (spriteRenderer != null)
        {
            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlash());
        }
    }

    private IEnumerator HitFlash()
    {
        GameObject effect = null;
        if (hitEffectFrames != null && hitEffectFrames.Length > 0)
        {
            effect = new GameObject("BirdHitEffect");
            effect.transform.position = transform.position;
            effect.transform.localScale = Vector3.one * hitEffectScale;

            SpriteRenderer effectRenderer = effect.AddComponent<SpriteRenderer>();
            effectRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            effectRenderer.sortingOrder = spriteRenderer.sortingOrder + 100;

            foreach (Sprite frame in hitEffectFrames)
            {
                if (frame != null)
                    effectRenderer.sprite = frame;
                yield return new WaitForSeconds(hitEffectFrameDuration);
            }
        }

        spriteRenderer.color = originalColor;
        if (effect != null)
            Destroy(effect);
        hitFlashCoroutine = null;
    }
}