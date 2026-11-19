
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;

public class CompanionGroupController : MonoBehaviour
{
    enum GroupState
    {
        Following,
        Scattering
    }

    [Header("References")]
    [SerializeField] Transform player;
    [SerializeField] Transform survivorsRoot;
    [SerializeField] CompanionAgent[] companions;

    [Header("Following")]
    [SerializeField] float followRadius = 2.2f;
    [SerializeField] float followUpdateInterval = 0.25f;
    [SerializeField, Min(1f)] float catchUpSpeedMultiplier = 1.6f;
    [SerializeField, Min(0f)] float catchUpDistance = 3f;

    [Header("Scattering")]
    [SerializeField] float idleRadiusMin = 2.5f;
    [SerializeField] float idleRadiusMax = 4f;
    [SerializeField] float idleRetargetInterval = 3.5f;
    [SerializeField] float minNpcSeparation = 1.2f;

    [Header("Movement Detection")]
    [SerializeField] float movementThreshold = 0.15f;
    [SerializeField] float stopDelay = 0.4f;

    GroupState currentState;

    Vector3 lastPlayerPosition;
    float lastPlayerMoveTime;
    float nextFollowUpdate;
    float[] nextIdleUpdate;
    NavMeshSurface navMeshSurface;
    CompanionColliderNavMeshCutout navMeshCutout;
    Tilemap poisonousBushTilemap;
    bool fightFormationActive;
    bool dialoguePauseActive;
    public bool FightFormationGathering { get; private set; }
    public bool FightFormationNearlyGathered { get; private set; }

    public Transform SurvivorsRoot => survivorsRoot;
    public CompanionAgent[] Companions => companions;

    void Start()
    {
        if (player == null)
            player = transform;

        EnsureNavMesh();
        DiscoverCompanions();

        TaskAssigner assigner = TaskAssigner.Instance != null
            ? TaskAssigner.Instance
            : FindFirstObjectByType<TaskAssigner>();
        if (assigner != null)
        {
            assigner.OnResumeExploration += RefreshNavMeshForBotanist;
            assigner.OnSurvivorRescued += RefreshNavMeshAfterRescue;
        }

        lastPlayerPosition = player.position;

        nextIdleUpdate = new float[companions.Length];

        for (int i = 0; i < companions.Length; i++)
        {
            if (companions[i] != null)
                companions[i].InitializeOnNavMesh();
        }

        lastPlayerMoveTime = Time.time;
        currentState = GroupState.Following;
    }

    void Update()
    {
        if (fightFormationActive || dialoguePauseActive)
            return;

        DetectPlayerMovement();

        bool playerMoving =
            Time.time - lastPlayerMoveTime < stopDelay;

        GroupState newState = playerMoving
            ? GroupState.Following
            : GroupState.Scattering;

        if (newState != currentState)
        {
            currentState = newState;

            if (currentState == GroupState.Scattering)
                ScatterCompanions();
        }

        if (currentState == GroupState.Following)
            UpdateFollowing();
        else
            UpdateScattering();
    }

    public IEnumerator BeginFightFormation(Vector3 center, float radius, float gatherSpeed)
    {
        DiscoverCompanions();
        if (companions == null || companions.Length == 0)
        {
            Debug.LogWarning("[CompanionGroup] Fight formation has no CompanionAgents under the survivors root.", this);
            yield break;
        }

        fightFormationActive = true;
        FightFormationGathering = true;
        FightFormationNearlyGathered = false;
        Vector3[] formationTargets = new Vector3[companions.Length];
        bool[] participating = new bool[companions.Length];
        int presentCount = 0;
        foreach (CompanionAgent companion in companions)
            if (companion != null && companion.gameObject.activeInHierarchy)
                presentCount++;

        // Very small inspector radii can pack six NavMeshAgents closer than
        // their collision radii allow. Give each active survivor enough room
        // to reach their own point instead of stopping in a crowd.
        float largestAgentRadius = 0.5f;
        foreach (CompanionAgent companion in companions)
            if (companion != null && companion.gameObject.activeInHierarchy)
                largestAgentRadius = Mathf.Max(largestAgentRadius, companion.AgentRadius);
        float minFormationRadius = presentCount > 1
            ? (largestAgentRadius + 0.08f) / Mathf.Sin(Mathf.PI / presentCount)
            : 0f;
        float usableRadius = Mathf.Max(radius, minFormationRadius);

        int formationIndex = 0;
        for (int i = 0; i < companions.Length; i++)
        {
            CompanionAgent companion = companions[i];
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;

            companion.SetExternallyControlled(true);
            companion.ClearCachedDestination();
            if (!companion.IsOnNavMesh && !companion.InitializeOnNavMesh())
            {
                Debug.LogWarning($"[CompanionGroup] {companion.name} could not join the NavMesh for fight formation.", companion);
                continue;
            }
            companion.SetFightPaused(false);
            companion.SetRuntimeSpeed(gatherSpeed);

            float angle = presentCount > 0 ? 360f * formationIndex / presentCount - 90f : -90f;
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * usableRadius;
            formationTargets[i] = center + offset;
            participating[i] = companion.TrySetFightDestination(formationTargets[i]);
            if (!participating[i])
            {
                // Fall back to the normal NavMesh destination path if a strict
                // complete-path query rejected this point.
                companion.SetDestination(formationTargets[i]);
                participating[i] = !companion.HasReachedDestination ||
                                   Vector3.Distance(companion.Position, formationTargets[i]) <= usableRadius + 0.5f;
                if (!participating[i])
                    Debug.LogWarning($"[CompanionGroup] {companion.name} could not path into the fight formation.", companion);
            }
            formationIndex++;
        }

        // Keep their NavMeshAgents moving until all survivors reach the circle.
        // Timeout prevents a blocked path from holding the fight coroutine forever.
        float timeout = 8f;
        while (timeout > 0f)
        {
            bool allArrived = true;
            bool nearlyGathered = true;
            for (int i = 0; i < companions.Length; i++)
            {
                CompanionAgent companion = companions[i];
                if (!participating[i])
                    continue;
                if (companion == null || !companion.gameObject.activeInHierarchy)
                    continue;
                if (Vector3.Distance(companion.Position, formationTargets[i]) > 0.8f)
                    nearlyGathered = false;
                if (!companion.HasReachedDestination)
                {
                    allArrived = false;
                }
            }

            FightFormationNearlyGathered = nearlyGathered;
            if (allArrived)
                break;

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        foreach (CompanionAgent companion in companions)
        {
            if (companion != null && companion.gameObject.activeInHierarchy)
                companion.SetFightPaused(true);
        }
        FightFormationGathering = false;
    }

    public void StopForFight()
    {
        if (fightFormationActive || companions == null)
            return;

        fightFormationActive = true;
        FightFormationGathering = false;
        FightFormationNearlyGathered = false;
        foreach (CompanionAgent companion in companions)
        {
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;

            companion.SetExternallyControlled(true);
            companion.SetFightPaused(true);
        }
    }

    public void EndFightFormation()
    {
        if (!fightFormationActive)
            return;

        fightFormationActive = false;
        if (companions == null)
            return;

        foreach (CompanionAgent companion in companions)
        {
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;
            companion.SetFightPaused(dialoguePauseActive);
            if (!dialoguePauseActive)
                companion.SetExternallyControlled(false);
        }
    }

    public void BeginDialoguePause(Camera viewCamera, Vector3 playerPosition)
    {
        if (companions == null)
            return;

        dialoguePauseActive = true;
        Vector3[] rallyTargets = new Vector3[companions.Length];
        bool[] participating = new bool[companions.Length];
        int presentCount = 0;
        foreach (CompanionAgent companion in companions)
            if (companion != null && companion.gameObject.activeInHierarchy)
                presentCount++;

        int rallyIndex = 0;
        float rallyRadius = Mathf.Min(1.35f, followRadius * 0.6f);
        for (int i = 0; i < companions.Length; i++)
        {
            CompanionAgent companion = companions[i];
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;

            companion.SetExternallyControlled(true);
            float angle = presentCount > 0 ? 360f * rallyIndex / presentCount - 90f : -90f;
            Vector3 offset = new(
                Mathf.Cos(angle * Mathf.Deg2Rad) * rallyRadius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * rallyRadius,
                0f);
            Vector3 destination = playerPosition + offset;
            if (viewCamera != null)
            {
                Vector3 viewport = viewCamera.WorldToViewportPoint(destination);
                viewport.x = Mathf.Clamp(viewport.x, 0.08f, 0.92f);
                viewport.y = Mathf.Clamp(viewport.y, 0.08f, 0.92f);
                if (viewport.z > 0f)
                {
                    destination = viewCamera.ViewportToWorldPoint(viewport);
                    destination.z = playerPosition.z;
                }
            }

            rallyTargets[i] = destination;
            participating[i] = true;
            companion.SetRuntimeSpeed(companion.DefaultMoveSpeed);
            companion.SetFightPaused(false);
            companion.ClearCachedDestination();
            if (!companion.TrySetFightDestination(destination))
            {
                Debug.LogWarning($"{companion.name} could not path to the dialogue rally point near the player.", companion);
                companion.SetFightPaused(true);
            }
            rallyIndex++;
        }

        StartCoroutine(WaitForDialogueRallyThenPause(viewCamera, rallyTargets, participating));
    }

    public void EndDialoguePause()
    {
        if (!dialoguePauseActive)
            return;

        dialoguePauseActive = false;
        if (companions == null)
            return;

        foreach (CompanionAgent companion in companions)
        {
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;

            companion.SetFightPaused(fightFormationActive);
            if (!fightFormationActive)
                companion.SetExternallyControlled(false);
        }
    }

    IEnumerator WaitForDialogueRallyThenPause(Camera viewCamera, Vector3[] rallyTargets, bool[] participating)
    {
        float timeout = 6f;
        while (dialoguePauseActive && timeout > 0f)
        {
            bool allSettled = true;
            for (int i = 0; i < companions.Length; i++)
            {
                CompanionAgent companion = companions[i];
                if (!participating[i])
                    continue;
                if (companion == null || !companion.gameObject.activeInHierarchy)
                    continue;
                bool visible = viewCamera == null || IsInsideCameraView(viewCamera, companion.Position, 0.03f);
                bool closeToRally = Vector3.Distance(companion.Position, rallyTargets[i]) <= 0.65f;
                if (!visible || !closeToRally)
                {
                    allSettled = false;
                    break;
                }
            }

            if (allSettled)
                break;

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!dialoguePauseActive || companions == null)
            yield break;

        foreach (CompanionAgent companion in companions)
            if (companion != null && companion.gameObject.activeInHierarchy)
                companion.SetFightPaused(true);
    }

    static bool IsInsideCameraView(Camera viewCamera, Vector3 worldPosition, float margin)
    {
        Vector3 viewport = viewCamera.WorldToViewportPoint(worldPosition);
        return viewport.z >= 0f &&
               viewport.x >= margin && viewport.x <= 1f - margin &&
               viewport.y >= margin && viewport.y <= 1f - margin;
    }

    void RefreshNavMeshForBotanist()
    {
        TaskAssigner assigner = TaskAssigner.Instance;
        bool botanistReady = assigner != null && assigner.IsSurvivorReady("Botanist");
        if (navMeshCutout == null || navMeshSurface == null)
            return;

        navMeshCutout.SetPoisonousBushTilemap(poisonousBushTilemap, botanistReady);
        navMeshSurface.BuildNavMesh();
    }

    void RefreshNavMeshAfterRescue(DoNotStray.Survivor survivor)
    {
        if (survivor != null && survivor.Name == "Botanist")
            RefreshNavMeshForBotanist();
    }

    void OnDestroy()
    {
        TaskAssigner assigner = TaskAssigner.Instance;
        if (assigner == null)
            return;
        assigner.OnResumeExploration -= RefreshNavMeshForBotanist;
        assigner.OnSurvivorRescued -= RefreshNavMeshAfterRescue;
    }

    void DetectPlayerMovement()
    {
        float distance =
            Vector3.Distance(player.position, lastPlayerPosition);

        if (distance > movementThreshold * Time.deltaTime)
        {
            lastPlayerMoveTime = Time.time;
        }

        lastPlayerPosition = player.position;
    }

    public void OnPlayerTeleported()
    {
        if (player == null)
            return;

        for (int i = 0; i < companions.Length; i++)
        {
            CompanionAgent companion = companions[i];
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;

            // The teleporter warped this independent companion to its
            // player-relative destination. Recalculate its next follow target.
            companion.ClearCachedDestination();
        }

        lastPlayerPosition = player.position;
        lastPlayerMoveTime = Time.time;
        nextFollowUpdate = 0f;
        currentState = GroupState.Following;
    }

    void UpdateFollowing()
    {
        if (Time.time < nextFollowUpdate)
            return;

        nextFollowUpdate = Time.time + followUpdateInterval;

        int count = companions.Length;
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();

        for (int i = 0; i < count; i++)
        {
            if (companions[i] == null)
                continue;
            if (!companions[i].gameObject.activeInHierarchy)
                continue;
            if (companions[i].IsExternallyControlled)
                continue;

            float angle = (360f / count) * i;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad),
                0f
            ) * followRadius;

            Vector3 targetPosition = player.position + offset;
            float normalFollowSpeed = playerMovement != null
                ? playerMovement.CurrentMoveSpeed
                : companions[i].DefaultMoveSpeed;
            bool catchingUp = Vector3.Distance(companions[i].Position, targetPosition) > catchUpDistance;
            companions[i].SetRuntimeSpeed(catchingUp
                ? normalFollowSpeed * catchUpSpeedMultiplier
                : normalFollowSpeed);

            companions[i].SetDestination(targetPosition);
        }
    }

    void ScatterCompanions()
    {
        for (int i = 0; i < companions.Length; i++)
        {
            nextIdleUpdate[i] = 0f;
            if (companions[i] != null && !companions[i].IsExternallyControlled)
                companions[i].SetRuntimeSpeed(companions[i].DefaultMoveSpeed);
        }

        UpdateScattering();
    }

    void UpdateScattering()
    {
        for (int i = 0; i < companions.Length; i++)
        {
            CompanionAgent companion = companions[i];

            if (companion == null)
                continue;
            if (!companion.gameObject.activeInHierarchy)
                continue;
            if (companion.IsExternallyControlled)
                continue;

            if (Time.time < nextIdleUpdate[i])
                continue;

            if (!companion.HasReachedDestination &&
                nextIdleUpdate[i] != 0f)
                continue;

            if (TryFindIdlePosition(i, out Vector3 idlePosition))
            {
                companion.SetDestination(idlePosition);

                nextIdleUpdate[i] =
                    Time.time + idleRetargetInterval;
            }
            else
            {
                nextIdleUpdate[i] = Time.time + 1f;
            }
        }
    }

    void DiscoverCompanions()
    {
        if (companions != null && companions.Length > 0)
            return;

        if (survivorsRoot == null)
        {
            GameObject root = GameObject.Find("survivors");
            survivorsRoot = root != null ? root.transform : player;
        }

        var found = new System.Collections.Generic.List<CompanionAgent>();
        for (int i = 0; i < survivorsRoot.childCount; i++)
        {
            Transform child = survivorsRoot.GetChild(i);
            if (child.name.IndexOf("Camera", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (child.GetComponent<SpriteRenderer>() == null)
                continue;

            CompanionAgent agent = child.GetComponent<CompanionAgent>();
            if (agent == null) agent = child.gameObject.AddComponent<CompanionAgent>();
            found.Add(agent);
        }
        companions = found.ToArray();
    }

    void EnsureNavMesh()
    {
        navMeshSurface = FindFirstObjectByType<NavMeshSurface>();
        if (navMeshSurface == null)
        {
            GameObject navMeshObject = new GameObject("Level 1 NavMesh");
            navMeshObject.transform.rotation = Quaternion.Euler(270f, 0f, 0f);
            navMeshSurface = navMeshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.agentTypeID = NavMesh.GetSettingsByID(0).agentTypeID;
            navMeshSurface.collectObjects = CollectObjects.Children;
            navMeshSurface.layerMask = ~0;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            navMeshSurface.hideEditorLogs = true;
        }

        // Let the custom extension provide every source. With All/Volume,
        // Unity collects scene renderers and makes tree art walkable too.
        navMeshSurface.collectObjects = CollectObjects.Children;
        CollectSources2d sceneWideCollector = navMeshSurface.GetComponent<CollectSources2d>();
        if (sceneWideCollector != null)
        {
            // Destroy is deferred until end-of-frame. Unregister the extension
            // immediately or this same rebuild still collects every tree sprite.
            navMeshSurface.NavMeshExtensions.Remove(sceneWideCollector);
            Destroy(sceneWideCollector);
        }

        navMeshSurface.defaultArea = 0;

        // Only the specifically named "grass" floor supplies walkable cells.
        // Do not select similarly named overlays such as "grass unwalkable".
        Tilemap grass = FindGrassTilemap();
        if (grass == null)
        {
            Debug.LogError("Companion NavMesh requires an active Tilemap named exactly 'grass'. No NavMesh was built.", this);
            return;
        }

        // Add area-1 sources from solid 2D colliders. NavMesh's built-in
        // Not Walkable area cuts these shapes out of the grass navigation mesh.
        CompanionColliderNavMeshCutout cutout = navMeshSurface.GetComponent<CompanionColliderNavMeshCutout>();
        if (cutout == null)
            cutout = navMeshSurface.gameObject.AddComponent<CompanionColliderNavMeshCutout>();
        navMeshCutout = cutout;
        cutout.SetWalkableTilemap(grass);
        BushDetector bushDetector = FindFirstObjectByType<BushDetector>();
        poisonousBushTilemap = bushDetector != null ? bushDetector.poisonousBushTilemap : null;
        TaskAssigner assigner = TaskAssigner.Instance != null
            ? TaskAssigner.Instance
            : FindFirstObjectByType<TaskAssigner>();
        bool botanistReady = assigner == null || assigner.IsSurvivorReady("Botanist");
        cutout.SetPoisonousBushTilemap(poisonousBushTilemap, botanistReady);
        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // Rebuild every time this level starts so current Tilemap and collider
        // geometry is reflected in the runtime navigation data.
        navMeshSurface.BuildNavMesh();
    }

    Tilemap FindGrassTilemap()
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null || !tilemap.gameObject.activeInHierarchy)
                continue;
            if (string.Equals(tilemap.gameObject.name, "grass", System.StringComparison.Ordinal))
                return tilemap;
        }
        return null;
    }

    bool TryFindIdlePosition(int index, out Vector3 position)
    {
        CompanionAgent current = companions[index];

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            float distance = Random.Range(
                idleRadiusMin,
                idleRadiusMax
            );

            Vector3 candidate = player.position +
                new Vector3(
                    randomDirection.x,
                    randomDirection.y,
                    0f
                ) * distance;

            if (!current.TryGetValidPosition(candidate, out position))
                continue;

            bool valid = true;

            for (int j = 0; j < companions.Length; j++)
            {
                if (j == index || companions[j] == null)
                    continue;
                if (!companions[j].gameObject.activeInHierarchy)
                    continue;

                float separation = Vector3.Distance(
                    position,
                    companions[j].Position
                );

                if (separation < minNpcSeparation)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return true;
        }

        position = Vector3.zero;
        return false;
    }
}
