using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PhotonView))]
public class SplitterProjectile : MonoBehaviourPun
{
    [SerializeField] private float debuffSlowMultiplier = 0.4f;
    [SerializeField] private float debuffDuration = 3f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float lifetime = 5f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Init(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
        StartCoroutine(LifetimeExpire());
    }

    private bool IsAuthority => !PhotonNetwork.InRoom || photonView.IsMine;

    private void DestroyProjectile()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAuthority)
            return;

        PlayerControllerWithPhysics player = other.GetComponent<PlayerControllerWithPhysics>();
        if (player != null)
        {
            Vector2 knockDir = new Vector2(rb.linearVelocity.x, 0f).normalized;
            if (knockDir == Vector2.zero) knockDir = Vector2.right;
            Vector2 force = knockDir * knockbackForce;
            Debug.Log($"Knockback force={force}, playerRb={player.GetComponent<Rigidbody2D>()?.linearVelocity}");
            player.ApplyKnockback(force);
            player.ApplyDebuff(debuffSlowMultiplier, debuffDuration);
            DestroyProjectile();
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            DestroyProjectile();
    }

    private IEnumerator LifetimeExpire()
    {
        yield return new WaitForSeconds(lifetime);
        if (IsAuthority)
            DestroyProjectile();
    }
}
