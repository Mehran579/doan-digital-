
using UnityEngine;

public class TreeGenerator : MonoBehaviour
{
    [Header("References")]
    public GameObject treePrefab;
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
        if (treePrefab == null || spawnBoundary == null)
        {
            Debug.LogError("Assign Tree Prefab and Spawn Boundary!");
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
                treePrefab,
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
