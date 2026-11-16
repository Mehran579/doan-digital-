using UnityEngine;

/// <summary>Scout-style gold silhouette outline used on kidnapped copies while Detective is ready.</summary>
[DisallowMultipleComponent]
public class KidnappedSurvivorOutline : MonoBehaviour
{
    Material silhouetteMaterial;
    [SerializeField] Color outlineColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] float outlineThickness = 0.04f;
    [SerializeField] int sortingOrderOffset = -1;

    public void SetVisible(bool visible)
    {
        Transform existing = transform.Find("ScoutOutline");
        if (!visible)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }
        if (existing != null) return;

        SpriteRenderer source = GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null) return;
        EnsureMaterial();
        if (silhouetteMaterial == null) return;

        GameObject outline = new GameObject("ScoutOutline");
        outline.transform.SetParent(transform, false);
        Vector2[] offsets =
        {
            new Vector2(-outlineThickness, 0f), new Vector2(outlineThickness, 0f),
            new Vector2(0f, -outlineThickness), new Vector2(0f, outlineThickness)
        };
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject part = new GameObject("Outline_" + i);
            part.transform.SetParent(outline.transform, false);
            part.transform.localPosition = offsets[i];
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = silhouetteMaterial;
            renderer.color = outlineColor;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + sortingOrderOffset;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
        }
    }

    void EnsureMaterial()
    {
        if (silhouetteMaterial != null) return;
        Shader shader = Shader.Find("Hidden/SolidWhite");
        if (shader == null) shader = Shader.Find("GUI/Text Shader");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
            silhouetteMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    void OnDestroy()
    {
        if (silhouetteMaterial != null)
        {
            Destroy(silhouetteMaterial);
            silhouetteMaterial = null;
        }
    }
}
