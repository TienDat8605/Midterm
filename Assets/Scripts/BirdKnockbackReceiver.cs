using Photon.Pun;
using UnityEngine;

public class BirdKnockbackReceiver : MonoBehaviourPun
{
    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    [PunRPC]
    public void ApplyBirdKnockbackRpc(Vector2 velocity)
    {
        if (photonView != null && photonView.ViewID != 0 && !photonView.IsMine)
            return;
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            body.linearVelocity = velocity;
    }

    public void ApplyBirdKnockback(Vector2 velocity)
    {
        ApplyBirdKnockbackRpc(velocity);
    }
}
