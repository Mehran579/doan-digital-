using UnityEngine;

/// <summary>Explicitly marks an object's collider footprint as unwalkable for companions.</summary>
[DisallowMultipleComponent]
public sealed class CompanionNavMeshObstacle : MonoBehaviour
{
    public Collider2D obstacleCollider;

    public bool TryGetWorldBounds(out Bounds bounds)
    {
        if (obstacleCollider == null)
            obstacleCollider = GetComponent<Collider2D>();

        if (obstacleCollider != null && obstacleCollider.enabled)
        {
            bounds = obstacleCollider.bounds;
            return true;
        }

        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null && sprite.enabled)
        {
            bounds = sprite.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    void Reset()
    {
        obstacleCollider = GetComponent<Collider2D>();
    }
}
