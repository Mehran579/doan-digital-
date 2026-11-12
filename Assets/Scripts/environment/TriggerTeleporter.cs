
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        destination = Destination.position;
        Teleport();
    }

    public void Teleport()
    {
        StartCoroutine(TeleportRoutine());
    }

    IEnumerator TeleportRoutine()
    {
        fadeImage.gameObject.SetActive(true);

        // Fade to black
        yield return Fade(0f, 1f);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = destination;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            player.position = new Vector3(
                destination.x,
                destination.y,
                player.position.z
            );
        }

        // Fade back in
        Camera.main.orthographicSize = nextCameraSize;
        yield return Fade(1f, 0f);

        fadeImage.gameObject.SetActive(false);
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
