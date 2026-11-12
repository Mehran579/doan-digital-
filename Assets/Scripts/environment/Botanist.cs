
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BushDetector : MonoBehaviour
{
    [Header("References")]
    public Tilemap poisonousBushTilemap;
    public Tilemap revealedBushTilemap;

    [Header("Detection")]
    public float detectionRadius = 5f;
    public float checkInterval = 0.15f;

    [Header("Fade Settings")]
    public Color revealedColor = new Color(0.65f, 0.3f, 0.85f);
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 1f;
    public float lingerDuration = 3f;

    private class BushState
    {
        public float alpha;
        public float lastSeen;
    }

    private Dictionary<Vector3Int, BushState> bushes = new();

    private float timer;

    void Start()
    {
        revealedBushTilemap.ClearAllTiles();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= checkInterval)
        {
            timer = 0f;
            DetectBushes();
        }

        UpdateBushFades();
    }

    void DetectBushes()
    {
        Vector3 position = transform.position;

        Vector3Int min = poisonousBushTilemap.WorldToCell(
            position - Vector3.one * detectionRadius
        );

        Vector3Int max = poisonousBushTilemap.WorldToCell(
            position + Vector3.one * detectionRadius
        );

        int z = poisonousBushTilemap.origin.z;

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, z);

                if (!poisonousBushTilemap.HasTile(cell))
                    continue;

                Vector3 worldPos =
                    poisonousBushTilemap.GetCellCenterWorld(cell);

                if (Vector2.Distance(position, worldPos) > detectionRadius)
                    continue;

                if (!bushes.ContainsKey(cell))
                {
                    TileBase tile = poisonousBushTilemap.GetTile(cell);

                    revealedBushTilemap.SetTile(cell, tile);
                    revealedBushTilemap.SetTileFlags(cell, TileFlags.None);

                    bushes.Add(cell, new BushState
                    {
                        alpha = 0f,
                        lastSeen = Time.time
                    });
                }

                bushes[cell].lastSeen = Time.time;
            }
        }
    }

    void UpdateBushFades()
    {
        List<Vector3Int> toRemove = new();

        foreach (var pair in bushes)
        {
            Vector3Int cell = pair.Key;
            BushState state = pair.Value;

            bool inRange = Time.time - state.lastSeen <= lingerDuration;

            float targetAlpha = inRange ? 1f : 0f;

            float duration = inRange
                ? fadeInDuration
                : fadeOutDuration;

            float speed = duration > 0f ? 1f / duration : 1000f;

            state.alpha = Mathf.MoveTowards(
                state.alpha,
                targetAlpha,
                speed * Time.deltaTime
            );

            Color color = revealedColor;
            color.a = state.alpha;

            revealedBushTilemap.SetColor(cell, color);

            if (!inRange && state.alpha <= 0.001f)
            {
                toRemove.Add(cell);
            }
        }

        foreach (Vector3Int cell in toRemove)
        {
            revealedBushTilemap.SetTile(cell, null);
            bushes.Remove(cell);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
