using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class NpcIntroduction : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject speechBubble;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 4)] private string[] lines =
    {
        "Hello there!",
        "Keep climbing, little slimes."
    };

    [Header("Trigger")]
    [SerializeField] private bool waitUntilPlayerIsNearby;
    [SerializeField, Min(0.1f)] private float triggerDistance = 3f;
    [SerializeField] private bool repeatWhenPlayerReturns = true;
    [SerializeField, Min(0.1f)] private float rearmDistance = 4f;
    [SerializeField, Min(0f)] private float retriggerCooldown = 2f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float initialDelay = 1f;
    [SerializeField, Min(0.001f)] private float secondsPerCharacter = 0.035f;
    [SerializeField, Min(0f)] private float pauseBetweenLines = 1.5f;
    [SerializeField, Min(0f)] private float finalDisplayTime = 3f;

    protected virtual IEnumerator Start()
    {
        ResolveReferences();

        if (speechBubble == null || dialogueText == null)
        {
            Debug.LogError(
                $"{name} requires a SpeechBubble child containing TMP dialogue text.",
                this);
            yield break;
        }

        speechBubble.SetActive(false);
        dialogueText.text = string.Empty;

        PlayerControllerWithPhysics localPlayer = null;
        while (localPlayer == null)
        {
            localPlayer = FindLocalPlayer();
            yield return null;
        }

        if (waitUntilPlayerIsNearby)
            yield return WaitUntilPlayerIsWithinRange(localPlayer, triggerDistance);

        while (true)
        {
            yield return new WaitForSeconds(initialDelay);
            yield return PlaySequence();

            if (!repeatWhenPlayerReturns)
                yield break;

            yield return WaitUntilPlayerLeavesRange(localPlayer);
            yield return new WaitForSeconds(retriggerCooldown);
            yield return WaitUntilPlayerIsWithinRange(localPlayer, triggerDistance);
        }
    }

    private void ResolveReferences()
    {
        if (speechBubble == null)
        {
            Transform bubbleTransform = transform.Find("SpeechBubble");
            if (bubbleTransform != null)
                speechBubble = bubbleTransform.gameObject;
        }

        if (dialogueText == null && speechBubble != null)
            dialogueText = speechBubble.GetComponentInChildren<TMP_Text>(true);
    }

    private IEnumerator PlaySequence()
    {
        speechBubble.SetActive(true);

        for (int i = 0; i < lines.Length; i++)
        {
            yield return TypeLine(lines[i]);

            float delay = i == lines.Length - 1
                ? finalDisplayTime
                : pauseBetweenLines;
            yield return new WaitForSeconds(delay);
        }

        speechBubble.SetActive(false);
    }

    private IEnumerator WaitUntilPlayerIsWithinRange(
        PlayerControllerWithPhysics localPlayer,
        float distance)
    {
        float distanceSquared = distance * distance;
        while ((localPlayer.transform.position - transform.position).sqrMagnitude
               > distanceSquared)
        {
            yield return null;
        }
    }

    private IEnumerator WaitUntilPlayerLeavesRange(
        PlayerControllerWithPhysics localPlayer)
    {
        float safeRearmDistance = Mathf.Max(rearmDistance, triggerDistance);
        float rearmDistanceSquared = safeRearmDistance * safeRearmDistance;
        while ((localPlayer.transform.position - transform.position).sqrMagnitude
               <= rearmDistanceSquared)
        {
            yield return null;
        }
    }

    private static PlayerControllerWithPhysics FindLocalPlayer()
    {
        PlayerControllerWithPhysics[] players =
            FindObjectsByType<PlayerControllerWithPhysics>(
                FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            PhotonView view = players[i].GetComponent<PhotonView>();
            if (view == null || view.ViewID == 0 || view.IsMine)
                return players[i];
        }

        return null;
    }

    private IEnumerator TypeLine(string line)
    {
        dialogueText.text = line ?? string.Empty;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int characterCount = dialogueText.textInfo.characterCount;
        for (int visibleCharacters = 1;
             visibleCharacters <= characterCount;
             visibleCharacters++)
        {
            dialogueText.maxVisibleCharacters = visibleCharacters;
            yield return new WaitForSeconds(secondsPerCharacter);
        }
    }
}
