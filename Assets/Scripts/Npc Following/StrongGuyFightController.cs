using System.Collections;
using UnityEngine;

/// <summary>Owns the Strong Guy's movement and presentation during wolf blocks.</summary>
[DisallowMultipleComponent]
public sealed class StrongGuyFightController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float interceptDistance = 1.15f;
    [SerializeField, Min(0.1f)] float moveSpeed = 7f;
    [SerializeField, Min(0f)] float outsideGroupClearance = 0.8f;
    [SerializeField] string blockSortingLayer = "UI";
    [SerializeField, Min(0)] int blockSortingOrder = 101;

    CompanionAgent companion;
    TaskAssigner taskAssigner;
    SpriteRenderer[] renderers;
    int[] originalSortingLayerIds;
    int[] originalSortingOrders;
    Vector3 homePosition;
    bool homePositionCaptured;
    public bool LastMoveSucceeded { get; private set; }

    public bool IsAvailable
    {
        get
        {
            companion ??= GetComponent<CompanionAgent>();
            return isActiveAndEnabled &&
                   gameObject.activeInHierarchy &&
                   companion != null &&
                   taskAssigner != null &&
                   taskAssigner.IsSurvivorReady("Strong guy");
        }
    }

    public void SetTaskAssigner(TaskAssigner assigner)
    {
        taskAssigner = assigner;
    }

    public void CaptureHomePosition()
    {
        companion ??= GetComponent<CompanionAgent>();
        if (companion == null)
            return;

        homePosition = companion.Position;
        homePositionCaptured = true;
        CacheRenderers();
    }

    public Vector3 GetInterceptPosition(
        Vector3 playerPosition,
        Vector3 wolfRingPosition,
        CompanionAgent[] group)
    {
        Vector3 fromWolfToPlayer = (playerPosition - wolfRingPosition).normalized;
        float outerGroupRadius = 0f;
        if (group != null)
        {
            foreach (CompanionAgent member in group)
            {
                if (member == null || !member.gameObject.activeInHierarchy || member == companion)
                    continue;
                outerGroupRadius = Mathf.Max(outerGroupRadius, Vector2.Distance(playerPosition, member.Position));
            }
        }

        // Keep the guard position outside the survivors' occupied circle.
        float safeRadius = Mathf.Max(interceptDistance, outerGroupRadius + outsideGroupClearance);
        Vector3 result = playerPosition - fromWolfToPlayer * safeRadius;
        result.z = transform.position.z;
        return result;
    }

    public void SetBlockingPresentation(bool blocking)
    {
        CacheRenderers();
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (blocking)
            {
                renderer.sortingLayerName = blockSortingLayer;
                renderer.sortingOrder = blockSortingOrder;
            }
            else
            {
                renderer.sortingLayerID = originalSortingLayerIds[i];
                renderer.sortingOrder = originalSortingOrders[i];
            }
        }
    }

    public IEnumerator MoveAroundGroupToIntercept(Vector3 center, Vector3 destination, float timeout)
    {
        Vector2 targetOffset = (Vector2)(destination - center);
        Vector2 startOffset = (Vector2)(transform.position - center);
        if (targetOffset.sqrMagnitude < 0.001f)
        {
            LastMoveSucceeded = false;
            yield break;
        }
        if (startOffset.sqrMagnitude < 0.001f)
            startOffset = -targetOffset;

        float routeRadius = targetOffset.magnitude;
        float startAngle = Mathf.Atan2(startOffset.y, startOffset.x);
        float targetAngle = Mathf.Atan2(targetOffset.y, targetOffset.x);
        float angleDelta = Mathf.DeltaAngle(startAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        int arcSteps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(angleDelta) / (30f * Mathf.Deg2Rad)));
        float segmentTimeout = timeout / (arcSteps + 1f);

        Vector3 outerStart = center + new Vector3(
            Mathf.Cos(startAngle) * routeRadius,
            Mathf.Sin(startAngle) * routeRadius,
            0f);
        outerStart.z = transform.position.z;
        Debug.Log($"{name}: routing around survivor formation at radius {routeRadius:0.00} via {arcSteps} NavMesh segments.", this);
        yield return MoveTo(outerStart, segmentTimeout);
        if (!LastMoveSucceeded)
            yield break;

        for (int step = 1; step <= arcSteps; step++)
        {
            float angle = startAngle + angleDelta * step / arcSteps;
            Vector3 waypoint = center + new Vector3(
                Mathf.Cos(angle) * routeRadius,
                Mathf.Sin(angle) * routeRadius,
                0f);
            waypoint.z = transform.position.z;
            yield return MoveTo(waypoint, segmentTimeout);
            if (!LastMoveSucceeded)
                yield break;
        }
    }

    public IEnumerator ReturnHome(float timeout)
    {
        if (!homePositionCaptured)
            yield break;

        yield return MoveTo(homePosition, timeout);
        if (companion != null)
            companion.SetRuntimeSpeed(companion.DefaultMoveSpeed);
    }

    public Vector3 GetWolfContactPoint(GameObject wolf)
    {
        if (wolf == null)
            return transform.position;

        Bounds protectorBounds = GetRendererBounds(transform);
        Bounds wolfBounds = GetRendererBounds(wolf.transform);
        Vector3 towardProtector = (transform.position - wolf.transform.position).normalized;
        Vector3 protectorSurface = protectorBounds.ClosestPoint(wolf.transform.position);
        float wolfExtentTowardProtector =
            Mathf.Abs(towardProtector.x) * wolfBounds.extents.x +
            Mathf.Abs(towardProtector.y) * wolfBounds.extents.y;
        Vector3 blockPoint = protectorSurface - towardProtector * wolfExtentTowardProtector;
        blockPoint.z = wolf.transform.position.z;
        return blockPoint;
    }

    public IEnumerator FlashImpact()
    {
        for (int flash = 0; flash < 3 && this != null; flash++)
        {
            HitBlinker.Flash(gameObject, 0.08f);
            yield return new WaitForSecondsRealtime(0.13f);
        }
    }

    public void RestoreAfterFight()
    {
        SetBlockingPresentation(false);
        if (companion != null && companion.gameObject.activeInHierarchy)
        {
            companion.SetFightPaused(true);
            companion.SetRuntimeSpeed(companion.DefaultMoveSpeed);
        }
    }

    void CacheRenderers()
    {
        if (renderers != null)
            return;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalSortingLayerIds = new int[renderers.Length];
        originalSortingOrders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            originalSortingLayerIds[i] = renderers[i].sortingLayerID;
            originalSortingOrders[i] = renderers[i].sortingOrder;
        }
    }

    IEnumerator MoveTo(Vector3 destination, float timeout)
    {
        LastMoveSucceeded = false;
        companion ??= GetComponent<CompanionAgent>();
        if (companion == null || !companion.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"{name}: Strong Guy cannot move for the block because its CompanionAgent is missing or inactive.", this);
            LastMoveSucceeded = false;
            yield break;
        }

        companion.SetFightPaused(false);
        companion.SetRuntimeSpeed(moveSpeed);
        companion.ClearCachedDestination();
        if (!companion.TrySetFightDestination(destination))
        {
            Debug.LogWarning($"{name}: Strong Guy block failed. No complete NavMesh path to {destination}; current position is {companion.Position}.", this);
            companion.SetFightPaused(true);
            LastMoveSucceeded = false;
            yield break;
        }

        Debug.Log($"{name}: Strong Guy moving to block position {destination} from {companion.Position} at speed {moveSpeed}.", this);
        float remaining = timeout;
        while (remaining > 0f && companion != null &&
               companion.gameObject.activeInHierarchy && !companion.HasReachedDestination)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        LastMoveSucceeded = companion != null &&
                            companion.gameObject.activeInHierarchy &&
                            companion.HasReachedDestination;
        if (companion != null && companion.gameObject.activeInHierarchy)
        {
            companion.SetFightPaused(true);
            Debug.Log(LastMoveSucceeded
                ? $"{name}: Strong Guy reached the block position at {companion.Position}."
                : $"{name}: Strong Guy timed out before reaching the block position. Current position: {companion.Position}.", this);
        }
    }

    static Bounds GetRendererBounds(Transform root)
    {
        SpriteRenderer[] childRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool found = false;
        Bounds bounds = new(root.position, Vector3.zero);
        foreach (SpriteRenderer renderer in childRenderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }
}
