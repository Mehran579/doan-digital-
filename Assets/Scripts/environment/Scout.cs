using System.Collections.Generic;
using UnityEngine;

public class Scout : MonoBehaviour
{
    private static Material silhouetteMaterial;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float checkInterval = 0.25f;

    [Header("Outline Appearance")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.85f, 0.1f, 1f); // Vibrant gold/yellow
    [Tooltip("Outline thickness in local units. 4-directional offsets produce a crisp outer silhouette.")]
    [SerializeField] private float outlineThickness = 0.04f;
    [SerializeField] private int sortingOrderOffset = -1;

    private readonly HashSet<GameObject> highlightedObjects = new();
    private float checkTimer;

    private void Update()
    {
        checkTimer += Time.deltaTime;

        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            CheckForCollectibles();
        }
    }

    private void CheckForCollectibles()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            detectionRadius
        );

        HashSet<GameObject> objectsInRange = new();

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            GameObject collectible = hit.gameObject;

            // Check tag "collectible" or presence of collectible component
            bool isCollectible = collectible.CompareTag("collectible") || collectible.GetComponent<collectible>() != null;
            if (!isCollectible)
                continue;

            objectsInRange.Add(collectible);

            if (highlightedObjects.Add(collectible))
                CreateOutline(collectible);
        }

        highlightedObjects.RemoveWhere(obj =>
        {
            if (obj == null)
                return true;

            if (objectsInRange.Contains(obj))
                return false;

            RemoveOutline(obj);
            return true;
        });
    }

    private void CreateOutline(GameObject collectibleObj)
    {
        if (collectibleObj.transform.Find("ScoutOutline") != null)
            return;

        SpriteRenderer original = collectibleObj.GetComponent<SpriteRenderer>();
        if (original == null || original.sprite == null)
            return;

        EnsureSilhouetteMaterial();
        if (silhouetteMaterial == null)
            return;

        GameObject outlineContainer = new GameObject("ScoutOutline");
        outlineContainer.transform.SetParent(collectibleObj.transform, false);
        outlineContainer.transform.localPosition = Vector3.zero;
        outlineContainer.transform.localRotation = Quaternion.identity;
        outlineContainer.transform.localScale = Vector3.one;

        // 4 cardinal offsets create an even, crisp outline around any sprite geometry
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(-outlineThickness, 0f),
            new Vector2(outlineThickness, 0f),
            new Vector2(0f, -outlineThickness),
            new Vector2(0f, outlineThickness)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject sub = new GameObject("Outline_" + i);
            sub.transform.SetParent(outlineContainer.transform, false);
            sub.transform.localPosition = offsets[i];
            sub.transform.localRotation = Quaternion.identity;
            sub.transform.localScale = Vector3.one;

            SpriteRenderer sr = sub.AddComponent<SpriteRenderer>();
            sr.sprite = original.sprite;
            sr.sharedMaterial = silhouetteMaterial;
            sr.color = outlineColor;

            // Exactly match original renderer's sorting layer and render just behind it
            sr.sortingLayerID = original.sortingLayerID;
            sr.sortingOrder = original.sortingOrder + sortingOrderOffset;

            sr.flipX = original.flipX;
            sr.flipY = original.flipY;
        }
    }

    private static void EnsureSilhouetteMaterial()
    {
        if (silhouetteMaterial != null)
            return;

        // GUI/Text Shader replaces texture rgb with pure solid color while preserving sprite alpha
        Shader shader = Shader.Find("Hidden/SolidWhite");
        if (shader == null)
            shader = Shader.Find("GUI/Text Shader");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            silhouetteMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private void RemoveOutline(GameObject collectibleObj)
    {
        if (collectibleObj == null) return;
        Transform outline = collectibleObj.transform.Find("ScoutOutline");
        if (outline != null)
            Destroy(outline.gameObject);
    }

    private void OnDisable()
    {
        foreach (GameObject obj in highlightedObjects)
        {
            if (obj != null)
                RemoveOutline(obj);
        }

        highlightedObjects.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = outlineColor;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    private void OnDestroy()
    {
        if (silhouetteMaterial != null)
        {
            Destroy(silhouetteMaterial);
            silhouetteMaterial = null;
        }
    }
}
