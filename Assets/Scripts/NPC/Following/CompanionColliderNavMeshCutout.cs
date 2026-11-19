using System.Collections.Generic;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds walkability from the grass Tilemap only, then marks solid 2D
/// collider outlines as Not Walkable so companions route around obstacles.
/// </summary>
public sealed class CompanionColliderNavMeshCutout : NavMeshExtension
{
    const int NotWalkableArea = 1;

    Tilemap walkableTilemap;
    Tilemap poisonousBushTilemap;
    bool blockPoisonousBushes;
    readonly Dictionary<Collider2D, Mesh> colliderMeshes = new();

    public void SetWalkableTilemap(Tilemap tilemap)
    {
        walkableTilemap = tilemap;
    }

    public void SetPoisonousBushTilemap(Tilemap tilemap, bool blockBushes)
    {
        poisonousBushTilemap = tilemap;
        blockPoisonousBushes = blockBushes;
    }

    public override void CollectSources(
        NavMeshSurface surface,
        List<NavMeshBuildSource> sources,
        NavMeshBuilderState navMeshState)
    {
        var treeSpawnLimits = new List<Collider2D>();
        foreach (TreeGenerator generator in FindObjectsByType<TreeGenerator>(FindObjectsSortMode.None))
        {
            if (generator != null && generator.isActiveAndEnabled &&
                generator.spawnBoundary != null && generator.spawnBoundary.enabled)
                treeSpawnLimits.Add(generator.spawnBoundary);
        }

        var explicitObstacleBounds = new List<Bounds>();
        foreach (CompanionNavMeshObstacle obstacle in FindObjectsByType<CompanionNavMeshObstacle>(FindObjectsSortMode.None))
        {
            if (obstacle == null || !obstacle.isActiveAndEnabled)
                continue;

            if (obstacle.TryGetWorldBounds(out Bounds obstacleBounds))
                explicitObstacleBounds.Add(obstacleBounds);
        }

        // Do not use CollectSources2d's scene-wide SpriteRenderer collection:
        // that makes tree sprites and other art become walkable. Emit walkable
        // geometry only for painted grass cells outside tree spawn polygons.
        if (walkableTilemap != null)
        {
            BoundsInt bounds = walkableTilemap.cellBounds;
            Vector3 cellSize = walkableTilemap.layoutGrid.cellSize;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, bounds.zMin);
                    if (!walkableTilemap.HasTile(cell))
                        continue;

                    Vector3 center = walkableTilemap.GetCellCenterWorld(cell);
                    Vector3 worldCellSize = Vector3.Scale(cellSize, walkableTilemap.transform.lossyScale);
                    Bounds worldCellBounds = new Bounds(center, new Vector3(worldCellSize.x, worldCellSize.y, 0.2f));
                    bool overlapsExplicitObstacle = false;
                    foreach (Bounds obstacleBounds in explicitObstacleBounds)
                    {
                        if (!obstacleBounds.Intersects(worldCellBounds))
                            continue;
                        overlapsExplicitObstacle = true;
                        break;
                    }
                    if (overlapsExplicitObstacle)
                        continue;

                    if (blockPoisonousBushes && poisonousBushTilemap != null &&
                        poisonousBushTilemap.HasTile(poisonousBushTilemap.WorldToCell(center)))
                        continue;

                    // Remove cells covered by any TreeGenerator polygon from
                    // the walkable source set. This creates an actual hole in
                    // the grass NavMesh rather than overlaying a blocked mesh.
                    bool insideTreeZone = false;
                    foreach (Collider2D treeZone in treeSpawnLimits)
                    {
                        if (treeZone.OverlapPoint(center))
                        {
                            insideTreeZone = true;
                            break;
                        }
                    }
                    if (insideTreeZone)
                        continue;

                    sources.Add(new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Box,
                        transform = Matrix4x4.TRS(center, walkableTilemap.transform.rotation, walkableTilemap.transform.lossyScale),
                        size = new Vector3(cellSize.x, cellSize.y, 0.2f),
                        area = 0
                    });
                }
            }
        }

        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        var collected = new HashSet<Collider2D>();

        foreach (Collider2D collider in colliders)
        {
            if (collider == null || !collider.enabled)
                continue;
            if (collider.isTrigger)
                continue;
            if (collider.GetComponent<Tilemap>() == walkableTilemap)
                continue;
            if (collider.transform == transform || collider.transform.IsChildOf(transform))
                continue;
            if (collider.transform.root.CompareTag("Player"))
                continue;

            // A TilemapCollider using a CompositeCollider contributes the
            // composite once, rather than duplicating every source tile.
            Collider2D sourceCollider = collider;
            if (collider.compositeOperation != Collider2D.CompositeOperation.None && collider.composite != null)
                sourceCollider = collider.composite;
            if (!collected.Add(sourceCollider))
                continue;

            Mesh mesh = GetColliderMesh(sourceCollider);
            if (mesh == null)
                continue;

            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = mesh,
                component = sourceCollider,
                // Collider2D.CreateMesh(false, false) already bakes the
                // transform for static colliders. Applying localToWorld again
                // shifts/scales the obstacle away from its scene position.
                // Match NavMeshPlus's 2D collider source convention: identity
                // for static colliders, rigidbody pose only for dynamic ones.
                transform = sourceCollider.attachedRigidbody != null
                    ? Matrix4x4.TRS(
                        sourceCollider.attachedRigidbody.transform.position,
                        sourceCollider.attachedRigidbody.transform.rotation,
                        Vector3.one)
                    : Matrix4x4.identity,
                area = NotWalkableArea
            });
        }
    }

    Mesh GetColliderMesh(Collider2D collider)
    {
        if (!colliderMeshes.TryGetValue(collider, out Mesh mesh) || mesh == null)
        {
            mesh = collider.CreateMesh(false, false);
            if (mesh != null)
                colliderMeshes[collider] = mesh;
        }
        return mesh;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (Mesh mesh in colliderMeshes.Values)
        {
            if (mesh == null)
                continue;
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }
        colliderMeshes.Clear();
    }
}
