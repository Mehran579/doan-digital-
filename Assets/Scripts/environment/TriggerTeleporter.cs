
using System.Collections;
using UnityEngine;
using UnityEngine.Apple;
using UnityEngine.UI;

public class TriggerTeleporter : MonoBehaviour
{
    public Transform player;
    public Image fadeImage;

    public Transform Destination;
    Vector2 destination;
    public float fadeDuration = 0.5f;

    public float nextCameraSize;
    bool isTeleporting;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTeleporting || player == null || Destination == null)
            return;

        // Preserve the scene's original trigger behavior. The configured
        // teleporter always moves its assigned player when its trigger fires.
        destination = Destination.position;
        StartTeleport();
    }

    public void Teleport()
    {
        if (Destination == null)
        {
            Debug.LogError($"{name}: Teleporter has no destination assigned.", this);
            return;
        }

        destination = Destination.position;
        StartTeleport();
    }

    void StartTeleport()
    {
        if (!isTeleporting)
            StartCoroutine(TeleportRoutine());
    }

    IEnumerator TeleportRoutine()
    {
        isTeleporting = true;

        if (player == null)
        {
            Debug.LogError($"{name}: Teleporter has no player assigned.", this);
            isTeleporting = false;
            yield break;
        }

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(true);

        // Fade to black
        if (fadeImage != null)
            yield return Fade(0f, 1f);

        CompanionGroupController group = player.GetComponent<CompanionGroupController>();
        CompanionAgent[] companions = group != null
            ? group.Companions
            : player.GetComponentsInChildren<CompanionAgent>(true);
        Vector3[] companionOffsets = new Vector3[companions.Length];
        for (int i = 0; i < companions.Length; i++)
        {
            if (companions[i] == null)
                continue;
            companionOffsets[i] = companions[i].transform.position - player.position;
        }

        foreach (CompanionAgent companion in companions)
            if (companion != null && companion.gameObject.activeInHierarchy)
                companion.PrepareForTeleport();

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = destination;
            rb.linearVelocity = Vector2.zero;
            player.position = new Vector3(destination.x, destination.y, player.position.z);
        }
        else
        {
            player.position = new Vector3(
                destination.x,
                destination.y,
                player.position.z
            );
        }

        Physics2D.SyncTransforms();
        for (int i = 0; i < companions.Length; i++)
        {
            CompanionAgent companion = companions[i];
            if (companion != null && companion.gameObject.activeInHierarchy)
                companion.FinishTeleportAt(player.position + companionOffsets[i]);
        }

        NotifyFollowerGroup();

        // Fade back in
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.orthographicSize = nextCameraSize;
        if (fadeImage != null)
            yield return Fade(1f, 0f);

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);
        isTeleporting = false;
    }

    void NotifyFollowerGroup()
    {
        CompanionGroupController group = player.GetComponent<CompanionGroupController>();
        if (group != null)
            group.OnPlayerTeleported();
    }

    IEnumerator Fade(float start, float end)
    {
        float elapsed = 0f;
        Color color = fadeImage.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(start, end, elapsed / fadeDuration);
            fadeImage.color = color;

            yield return null;
        }

        color.a = end;
        fadeImage.color = color;
    }
}
