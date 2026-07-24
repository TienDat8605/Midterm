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
