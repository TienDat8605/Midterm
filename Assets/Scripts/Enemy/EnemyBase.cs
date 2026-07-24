using System.Collections;
using Photon.Pun;
using UnityEngine;

public enum EnemyState { Active, Disabled }

[RequireComponent(typeof(PhotonView))]
public abstract class EnemyBase : MonoBehaviourPun
{
    [Header("Knockback")]
    [SerializeField] protected float knockbackForce = 15f;

    [Header("Disable")]
    [SerializeField] protected float disableDuration = 3f;

    public EnemyState State { get; private set; } = EnemyState.Active;

    protected virtual void Start() { }

    protected virtual void Update()
    {
        if (State == EnemyState.Disabled)
            return;
        UpdateBehavior();
    }

    /// <summary>Called every frame while Active. Implement patrol/attack logic here.</summary>
    protected abstract void UpdateBehavior();

    /// <summary>Called on all clients when enemy becomes disabled.</summary>
    protected virtual void OnBecomeDisabled() { }

    /// <summary>Called on all clients when enemy re-enables after disable duration.</summary>
    protected virtual void OnBecomeActive() { }

    /// <summary>
    /// Call from subclass when the enemy should disable (e.g. after hitting a player).
    /// Only Master Client can initiate; all clients receive the RPC.
    /// </summary>
    protected bool IsAuthority => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

    protected void DisableTemporarily()
    {
        if (!IsAuthority)
            return;
        if (PhotonNetwork.InRoom)
            photonView.RPC(nameof(RPC_Disable), RpcTarget.AllBuffered, disableDuration);
        else
            RPC_Disable(disableDuration);
    }

    /// <summary>
    /// Apply knockback to a player and disable this enemy.
    /// force is world-space impulse direction + magnitude.
    /// </summary>
    protected void HitPlayer(PlayerControllerWithPhysics player, Vector2 force)
    {
        if (State == EnemyState.Disabled)
            return;
        // Only Master Client applies the hit to avoid double-knockback
        if (PhotonNetwork.IsMasterClient)
            player.ApplyKnockback(force);
        DisableTemporarily();
    }

    [PunRPC]
    private void RPC_Disable(float duration)
    {
        if (State == EnemyState.Disabled)
            return;
        State = EnemyState.Disabled;
        OnBecomeDisabled();
        StartCoroutine(ReenableAfter(duration));
    }

    private IEnumerator ReenableAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        State = EnemyState.Active;
        OnBecomeActive();
    }
}
