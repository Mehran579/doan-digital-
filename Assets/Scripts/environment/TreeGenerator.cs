
using System.Collections.Generic;
using UnityEngine;

public class TreeGenerator : MonoBehaviour
{
    [Header("References")]
    // Kept as a fallback for existing scene objects that still use one prefab.
    public GameObject treePrefab;
    public GameObject[] treePrefabs;
    public Collider2D spawnBoundary;

    [Header("Tree Distribution")]
    [Range(0f, 1f)]
    public float density = 0.7f;

    public float minDistance = 1.5f;

    [Header("Area Calculation")]
    public int areaSamples = 10000;

    [ContextMenu("Generate Trees")]
    void GenerateTrees()
    {
        List<GameObject> availableTreePrefabs = new List<GameObject>();
        if (treePrefabs != null)
        {
            foreach (GameObject prefab in treePrefabs)
            {
                if (prefab != null)
                    availableTreePrefabs.Add(prefab);
            }
        }

        if (availableTreePrefabs.Count == 0 && treePrefab != null)
            availableTreePrefabs.Add(treePrefab);

        if (availableTreePrefabs.Count == 0 || spawnBoundary == null)
        {
            Debug.LogError("Assign at least one Tree Prefab and a Spawn Boundary!");
            return;
        }

        Transform parent = transform.Find("Generated Trees");

        if (parent == null)
        {
            GameObject container = new GameObject("Generated Trees");
            parent = container.transform;
            parent.SetParent(transform);
        }

        ClearTrees(parent);

        float area = CalculateArea();

        // Calculate desired number of trees
        int targetCount = Mathf.RoundToInt(
            (area / (minDistance * minDistance)) * density * 0.65f
        );

        Vector2[] positions = new Vector2[targetCount];
        Bounds bounds = spawnBoundary.bounds;

        int count = 0;
        int maxAttempts = targetCount * 50;

        for (int i = 0; i < maxAttempts && count < targetCount; i++)
        {
            Vector2 pos = new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y)
            );

            if (!spawnBoundary.OverlapPoint(pos))
                continue;

            bool valid = true;

            for (int j = 0; j < count; j++)
            {
                if (Vector2.Distance(pos, positions[j]) < minDistance)
                {
                    valid = false;
                    break;
                }
            }

            if (!valid)
                continue;

            positions[count++] = pos;

            Instantiate(
                availableTreePrefabs[Random.Range(0, availableTreePrefabs.Count)],
                new Vector3(pos.x, pos.y, transform.position.z),
                Quaternion.identity,
                parent
            );
        }

        Debug.Log($"Generated {count}/{targetCount} trees in {area:F1} square units.");
    }

    float CalculateArea()
    {
        Bounds bounds = spawnBoundary.bounds;

        int inside = 0;

        for (int i = 0; i < areaSamples; i++)
        {
            Vector2 point = new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y)
            );

            if (spawnBoundary.OverlapPoint(point))
                inside++;
        }

        float boundsArea = bounds.size.x * bounds.size.y;

        return boundsArea * ((float)inside / areaSamples);
    }

    [ContextMenu("Clear Trees")]
    void ClearGeneratedTrees()
    {
        Transform parent = transform.Find("Generated Trees");

        if (parent != null)
            ClearTrees(parent);
    }

    void ClearTrees(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
