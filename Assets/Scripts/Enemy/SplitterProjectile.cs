using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PhotonView))]
public class SplitterProjectile : MonoBehaviourPun
{
    [SerializeField] private float debuffSlowMultiplier = 0.4f;
    [SerializeField] private float debuffDuration = 3f;
    [SerializeField] private float lifetime = 5f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Init(Vector2 direction, float speed)
    {
        rb.linearVelocity = direction * speed;
        StartCoroutine(LifetimeExpire());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine)
            return;

        PlayerControllerWithPhysics player = other.GetComponent<PlayerControllerWithPhysics>();
        if (player != null)
        {
            player.ApplyDebuff(debuffSlowMultiplier, debuffDuration);
            PhotonNetwork.Destroy(gameObject);
            return;
        }

        // Destroy on terrain contact
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            PhotonNetwork.Destroy(gameObject);
    }

    private IEnumerator LifetimeExpire()
    {
        yield return new WaitForSeconds(lifetime);
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }
}
