using System.Collections;
using UnityEngine;

/// <summary>Temporarily renders a sprite hierarchy as a white silhouette.</summary>
public sealed class HitBlinker : MonoBehaviour
{
    static Material solidWhiteMaterial;

    SpriteRenderer[] renderers;
    Material[][] originalMaterials;
    Coroutine blinkRoutine;

    void Awake() => CacheRenderers();

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i] != null ? renderers[i].sharedMaterials : null;
    }

    public static void Flash(GameObject target, float duration = 0.08f)
    {
        if (target == null)
            return;

        HitBlinker blinker = target.GetComponent<HitBlinker>();
        if (blinker == null)
            blinker = target.AddComponent<HitBlinker>();
        blinker.Blink(duration);
    }

    public void Blink(float duration = 0.08f)
    {
        if (renderers == null || renderers.Length == 0)
            CacheRenderers();
        if (renderers == null || renderers.Length == 0)
            return;

        EnsureMaterial();
        if (solidWhiteMaterial == null)
            return;

        if (blinkRoutine != null)
            StopCoroutine(blinkRoutine);

        ApplyFlashMaterial();
        blinkRoutine = StartCoroutine(BlinkRoutine(duration));
    }

    IEnumerator BlinkRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
        RestoreMaterials();
        blinkRoutine = null;
    }

    void ApplyFlashMaterial()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            Material[] materials = new Material[renderers[i].sharedMaterials.Length];
            for (int j = 0; j < materials.Length; j++)
                materials[j] = solidWhiteMaterial;
            renderers[i].sharedMaterials = materials;
        }
    }

    void RestoreMaterials()
    {
        if (renderers == null || originalMaterials == null)
            return;
        for (int i = 0; i < renderers.Length && i < originalMaterials.Length; i++)
            if (renderers[i] != null && originalMaterials[i] != null)
                renderers[i].sharedMaterials = originalMaterials[i];
    }

    static void EnsureMaterial()
    {
        if (solidWhiteMaterial != null)
            return;
        Shader shader = Shader.Find("Hidden/SolidWhite");
        if (shader != null)
            solidWhiteMaterial = new Material(shader) { name = "Solid White Hit Flash", hideFlags = HideFlags.HideAndDontSave };
    }

    void OnDisable()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
        RestoreMaterials();
    }
}
