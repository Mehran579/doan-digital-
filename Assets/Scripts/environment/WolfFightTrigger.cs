using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs a timed block and key-combo encounter when the player enters the wolf trigger.</summary>
public sealed class WolfFightTrigger : MonoBehaviour
{
    [Header("Survivor response")]
    [SerializeField] Sprite exclamationSprite;
    [Tooltip("Distance from the player to each survivor's circle position. Lower values form a tighter group.")]
    [SerializeField, Min(0.5f)] float survivorFormationRadius = 0.85f;
    [SerializeField, Min(0.1f)] float survivorGatherSpeed = 6f;
    [SerializeField, Min(0.1f)] float alertDuration = 0.7f;

    [Header("Wolves")]
    [SerializeField] GameObject wolfPrefab;
    [SerializeField, Range(3, 6)] int wolfCount = 4;
    [SerializeField, Min(1f)] float wolfSurroundRadius = 2.4f;
    [SerializeField, Min(2f)] float wolfSpawnMinRadius = 6f;
    [SerializeField, Min(2f)] float wolfSpawnMaxRadius = 10f;
    [SerializeField, Min(0.1f)] float wolfEntryDuration = 0.45f;
    [SerializeField, Min(0.1f)] float wolfLungeDuration = 0.22f;
    [SerializeField, Min(0.1f)] float wolfRetreatDuration = 0.28f;
    [SerializeField, Min(0f)] float wolfJumpHeight = 0.65f;
    [SerializeField, Min(0)] int wolfSortingOrder = 100;
    [SerializeField, Min(0)] int damagePerHit = 8;

    [Header("Block timing")]
    [SerializeField, Min(0.2f)] float blockWindowDuration = 0.9f;
    [SerializeField, Range(0.08f, 0.4f)] float greenZoneWidth = 0.2f;

    [Header("Counterattack combo")]
    [SerializeField, Min(1)] int comboLength = 5;
    [SerializeField, Min(0.5f)] float comboTimeLimit = 2.6f;
    [SerializeField, Min(0f)] float comboRetryPause = 0.75f;
    [SerializeField, Min(0f)] float comboRetrySliderInputGrace = 0.3f;
    [SerializeField, Min(0.1f)] float promptSpacing = 0.48f;
    [SerializeField, Min(0.1f)] float promptWorldSize = 0.38f;
    [SerializeField] float promptHeadOffset = 0.65f;
    [SerializeField] float timerBarOffset = 0.22f;
    [SerializeField] int fightSortingOrder = 120;

    [Header("Camera shake")]
    [SerializeField, Min(0.01f)] float cameraShakeDuration = 0.28f;
    [SerializeField, Min(0.01f)] float cameraShakeMagnitude = 0.12f;

    [Header("Aztech keyboard icons")]
    [SerializeField] Sprite wKey;
    [SerializeField] Sprite aKey;
    [SerializeField] Sprite sKey;
    [SerializeField] Sprite dKey;

    PlayerMovement playerMovement;
    PlayerSurvivalHealth playerHealth;
    TaskAssigner taskAssigner;
    CompanionGroupController companionGroup;
    BoxCollider2D triggerCollider;
    Transform playerTransform;
    SpriteRenderer playerRenderer;
    StrongGuyFightController strongGuyBlocker;
    Camera fightCamera;
    Vector3 cameraBaseLocalPosition;
    bool cameraShaking;
    Coroutine fightRoutine;
    Coroutine cameraShakeRoutine;
    GameObject promptRoot;
    SpriteRenderer[] promptRenderers;
    SpriteRenderer timerBackground;
    SpriteRenderer timerFill;
    SpriteRenderer blockBackground;
    SpriteRenderer blockGreenZone;
    SpriteRenderer blockSlider;
    Sprite runtimePixelSprite;
    GameObject[] activeWolves;
    Vector3[] wolfSpawnPositions;
    Vector3[] wolfRingPositions;
    int[] comboKeys;
    float blockGreenCenter;
    float pendingBlockInputGrace;
    bool hasStarted;
    bool strongGuyBlockedCurrentAttack;

    static readonly Color UpcomingKeyColor = new(1f, 1f, 1f, 0.55f);

    void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasStarted || !other.CompareTag("Player"))
            return;

        hasStarted = true;
        fightRoutine = StartCoroutine(RunFight(other.transform));
    }

    IEnumerator RunFight(Transform player)
    {
        playerTransform = player;
        playerMovement = player.GetComponent<PlayerMovement>();
        playerHealth = player.GetComponent<PlayerSurvivalHealth>();
        Level1NightCycle nightCycle = player.GetComponent<Level1NightCycle>();
        taskAssigner = nightCycle != null ? nightCycle.assigner : null;
        if (taskAssigner == null)
            taskAssigner = TaskAssigner.Instance;
        if (taskAssigner == null)
        {
#if UNITY_2023_1_OR_NEWER
            taskAssigner = FindFirstObjectByType<TaskAssigner>(FindObjectsInactive.Include);
#else
            taskAssigner = FindObjectOfType<TaskAssigner>(true);
#endif
        }
        if (taskAssigner != null)
            Debug.Log($"[WolfFight] Using TaskAssigner '{taskAssigner.name}' (active in hierarchy: {taskAssigner.gameObject.activeInHierarchy}).", this);
        else
            Debug.LogWarning("[WolfFight] TaskAssigner was not found; survivor roster checks will fail.", this);
        companionGroup = player.GetComponent<CompanionGroupController>();
        playerRenderer = player.GetComponentInChildren<SpriteRenderer>();
        fightCamera = Camera.main;

        if (playerMovement != null)
            playerMovement.SetMovementLocked(true);

        if (fightCamera != null)
            StartCameraShake(cameraShakeDuration, cameraShakeMagnitude);
        if (companionGroup != null)
            companionGroup.StopForFight();
        SpawnSurvivorAlerts();
        yield return new WaitForSecondsRealtime(alertDuration * 0.25f);

        // Survivors walk to the player and stop in a circle; the wolves begin
        // entering as soon as the group is nearly formed.
        if (companionGroup != null)
        {
            StartCoroutine(companionGroup.BeginFightFormation(player.position, survivorFormationRadius, survivorGatherSpeed));
            while (companionGroup.FightFormationGathering && !companionGroup.FightFormationNearlyGathered)
                yield return null;
        }
        else
        {
            yield return new WaitForSecondsRealtime(alertDuration * 0.25f);
        }

        yield return SpawnAndSurroundWolves();
        if (companionGroup != null)
        {
            while (companionGroup.FightFormationGathering)
                yield return null;
        }
        FindStrongGuy();

        BuildFightPrompts();

        for (int i = 0; i < activeWolves.Length; i++)
        {
            GameObject wolf = activeWolves[i];
            if (wolf == null)
                continue;

            bool defeated = false;
            while (!defeated && PlayerCanFight())
            {
                // The wolf telegraphs from its ring position while the marker
                // moves. It only lunges if the player misses the green zone.
                bool blocked = false;
                yield return RunBlockTiming(result => blocked = result);
                bool strongGuyAvailable = CanStrongGuyBlock();
                Debug.Log($"[WolfFight] Slider QTE {(blocked ? "SUCCEEDED" : "FAILED")}; Strong Guy available: {strongGuyAvailable}.", this);
                if (!blocked || !strongGuyAvailable)
                {
                    // A red/missed timing hit lands. Green only works while the
                    // Strong Guy is still alive and present in the survivor roster.
                    if (blocked)
                        Debug.LogWarning("[WolfFight] Green timing was hit, but Strong Guy is unavailable; player takes the wolf hit.", this);
                    yield return WolfAttack(wolf, i);
                    ApplyWolfDamage(wolf);
                    yield return new WaitForSecondsRealtime(0.18f);
                    yield return HopWolf(wolf, wolf.transform.position, wolfRingPositions[i], wolfRetreatDuration, wolfJumpHeight * 0.45f);
                }
                else
                {
                    // Strong Guy physically steps in, absorbs the stopped lunge,
                    // flashes on impact, and walks back before the combo prompt.
                    strongGuyBlockedCurrentAttack = false;
                    yield return StrongGuyIntercepts(wolf, i);
                    if (!strongGuyBlockedCurrentAttack)
                    {
                        Debug.LogWarning("[WolfFight] Strong Guy could not complete the block movement; wolf attack lands.", this);
                        yield return WolfAttack(wolf, i);
                        ApplyWolfDamage(wolf);
                        yield return new WaitForSecondsRealtime(0.18f);
                        yield return HopWolf(wolf, wolf.transform.position, wolfRingPositions[i], wolfRetreatDuration, wolfJumpHeight * 0.45f);
                    }
                }

                // The key combo only decides whether this wolf is defeated.
                // Failing it returns directly to the slider QTE on the next loop.
                bool comboComplete = false;
                yield return RunCounterCombo(result => comboComplete = result);
                if (!comboComplete)
                {
                    Debug.Log("[WolfFight] Key combo expired or was incomplete; next phase is the block slider.", this);
                    pendingBlockInputGrace = comboRetrySliderInputGrace;
                    yield return new WaitForSecondsRealtime(comboRetryPause);
                    continue;
                }

                if (comboComplete)
                {
                    defeated = true;
                    yield return FlashWolfOnHit(wolf);
                    yield return HopWolf(wolf, wolf.transform.position, wolfSpawnPositions[i], wolfEntryDuration * 0.8f, wolfJumpHeight);
                    if (wolf != null)
                        Destroy(wolf);
                }
            }

            if (!PlayerCanFight())
                break;
        }

        yield return new WaitForSecondsRealtime(0.35f);
        EndFight();
    }

    bool PlayerCanFight() => playerHealth == null || playerHealth.CurrentHealth > 0;

    void FindStrongGuy()
    {
        strongGuyBlocker = null;
        if (taskAssigner == null)
        {
            Debug.LogWarning("[WolfFight] Strong Guy block unavailable: no TaskAssigner reference was found on the player or in the scene.", this);
            return;
        }
        if (!taskAssigner.IsSurvivorReady("Strong guy"))
        {
            Debug.Log("[WolfFight] Strong Guy block unavailable: survivor is not in the ready roster.", this);
            return;
        }
        if (companionGroup == null || companionGroup.Companions == null)
        {
            Debug.LogWarning("[WolfFight] Strong Guy block unavailable: CompanionGroupController has no companion list.", this);
            return;
        }

        foreach (CompanionAgent companion in companionGroup.Companions)
        {
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;
            if (companion.name.IndexOf("strong guy", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            strongGuyBlocker = companion.GetComponent<StrongGuyFightController>();
            if (strongGuyBlocker == null)
                strongGuyBlocker = companion.gameObject.AddComponent<StrongGuyFightController>();
            strongGuyBlocker.SetTaskAssigner(taskAssigner);
            strongGuyBlocker.CaptureHomePosition();
            Debug.Log($"[WolfFight] Strong Guy blocker found on '{companion.name}' at {companion.Position}.", this);
            return;
        }
        Debug.LogWarning("[WolfFight] Strong Guy is ready in the roster, but no active scene CompanionAgent named 'strong guy' was found.", this);
    }

    bool CanStrongGuyBlock()
    {
        bool available = strongGuyBlocker != null && strongGuyBlocker.IsAvailable;
        if (!available)
            Debug.LogWarning("[WolfFight] Strong Guy cannot block: component, active sprite, or ready roster entry is missing.", this);
        return available;
    }

    IEnumerator StrongGuyIntercepts(GameObject wolf, int wolfIndex)
    {
        if (strongGuyBlocker == null || !strongGuyBlocker.IsAvailable || wolf == null)
        {
            Debug.LogWarning("[WolfFight] Block sequence could not start because the Strong Guy or wolf is unavailable.", this);
            yield break;
        }

        Vector3 interceptPosition = strongGuyBlocker.GetInterceptPosition(
            playerTransform.position, wolfRingPositions[wolfIndex], companionGroup.Companions);
        strongGuyBlocker.SetBlockingPresentation(true);
        yield return strongGuyBlocker.MoveAroundGroupToIntercept(
            playerTransform.position, interceptPosition, 5f);
        if (!strongGuyBlocker.LastMoveSucceeded)
        {
            strongGuyBlocker.SetBlockingPresentation(false);
            yield break;
        }

        Vector3 blockPoint = strongGuyBlocker.GetWolfContactPoint(wolf);
        Debug.Log($"[WolfFight] Strong Guy is blocking at {strongGuyBlocker.transform.position}; wolf will stop at {blockPoint}.", this);
        yield return HopWolf(wolf, wolf.transform.position, blockPoint, wolfLungeDuration, wolfJumpHeight * 0.35f);
        strongGuyBlockedCurrentAttack = true;
        Debug.Log("[WolfFight] Block succeeded: wolf reached Strong Guy contact point; player takes no damage.", this);
        StartCameraShake(cameraShakeDuration * 0.55f, cameraShakeMagnitude * 0.8f);
        yield return strongGuyBlocker.FlashImpact();
        yield return HopWolf(wolf, wolf.transform.position, wolfRingPositions[wolfIndex], wolfRetreatDuration, wolfJumpHeight * 0.45f);

        yield return strongGuyBlocker.ReturnHome(4f);
        strongGuyBlocker.SetBlockingPresentation(false);
    }

    void SpawnSurvivorAlerts()
    {
        if (companionGroup == null || exclamationSprite == null)
            return;

        foreach (CompanionAgent companion in companionGroup.Companions)
        {
            if (companion == null || !companion.gameObject.activeInHierarchy)
                continue;

            GameObject alert = new("Survivor Alert");
            alert.transform.SetParent(companion.transform, false);
            SpriteRenderer survivorRenderer = companion.GetComponentInChildren<SpriteRenderer>();
            float headHeight = survivorRenderer != null ? survivorRenderer.bounds.extents.y : 0.45f;
            alert.transform.localPosition = new Vector3(0f, headHeight + 0.18f, 0f);
            SpriteRenderer renderer = alert.AddComponent<SpriteRenderer>();
            renderer.sprite = exclamationSprite;
            renderer.sortingOrder = fightSortingOrder;
            StartCoroutine(PopAlert(alert));
        }
    }

    IEnumerator PopAlert(GameObject alert)
    {
        float elapsed = 0f;
        float popDuration = Mathf.Min(0.18f, alertDuration * 0.4f);
        while (elapsed < alertDuration && alert != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float scale = elapsed < popDuration
                ? Mathf.Lerp(0.1f, 0.48f, Mathf.Sin(Mathf.Clamp01(elapsed / popDuration) * Mathf.PI * 0.5f))
                : 0.48f;
            alert.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        if (alert != null)
            Destroy(alert);
    }

    IEnumerator ShakeCamera(float duration, float magnitude)
    {
        if (fightCamera == null)
            yield break;

        cameraBaseLocalPosition = fightCamera.transform.localPosition;
        cameraShaking = true;
        float elapsed = 0f;
        while (elapsed < duration && fightCamera != null)
        {
            elapsed += Time.unscaledDeltaTime;
            Vector2 offset = Random.insideUnitCircle * magnitude * (1f - elapsed / duration);
            fightCamera.transform.localPosition = cameraBaseLocalPosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        RestoreCameraPosition();
    }

    void StartCameraShake(float duration, float magnitude)
    {
        if (fightCamera == null)
            return;
        if (cameraShakeRoutine != null)
            StopCoroutine(cameraShakeRoutine);
        RestoreCameraPosition();
        cameraShakeRoutine = StartCoroutine(ShakeCamera(duration, magnitude));
    }

    void RestoreCameraPosition()
    {
        if (cameraShaking && fightCamera != null)
            fightCamera.transform.localPosition = cameraBaseLocalPosition;
        cameraShaking = false;
    }

    IEnumerator SpawnAndSurroundWolves()
    {
        int count = Mathf.Clamp(wolfCount, 3, 4);
        activeWolves = new GameObject[count];
        wolfSpawnPositions = new Vector3[count];
        wolfRingPositions = new Vector3[count];
        Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        for (int i = 0; i < count; i++)
        {
            Vector2 direction = directions[i];
            wolfSpawnPositions[i] = FindSpawnNearTrees(direction);
            wolfRingPositions[i] = playerTransform.position + (Vector3)(direction * wolfSurroundRadius);
            wolfRingPositions[i].z = playerTransform.position.z;

            if (wolfPrefab == null)
                continue;

            GameObject wolfRoot = new($"Wolf {i + 1}");
            wolfRoot.transform.position = wolfSpawnPositions[i];
            GameObject wolfVisual = Instantiate(wolfPrefab, wolfRoot.transform);
            wolfVisual.transform.localPosition = Vector3.zero;
            wolfVisual.transform.localRotation = Quaternion.identity;
            activeWolves[i] = wolfRoot;
            SpriteRenderer[] renderers = activeWolves[i].GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                SetRendererToTopSorting(renderer, wolfSortingOrder);
                renderer.flipX = direction.x > 0f;
            }
        }

        // Start all entrances together so the wolves visibly close in from four sides.
        for (int i = 0; i < count; i++)
        {
            if (activeWolves[i] != null)
                StartCoroutine(HopWolf(activeWolves[i], wolfSpawnPositions[i], wolfRingPositions[i], wolfEntryDuration, wolfJumpHeight));
        }
        yield return new WaitForSecondsRealtime(wolfEntryDuration + 0.05f);
    }

    static void SetRendererToTopSorting(SpriteRenderer renderer, int order)
    {
        if (renderer == null)
            return;

        SortingLayer[] layers = SortingLayer.layers;
        if (layers.Length > 0)
        {
            SortingLayer highest = layers[0];
            for (int i = 1; i < layers.Length; i++)
                if (layers[i].value > highest.value)
                    highest = layers[i];
            renderer.sortingLayerID = highest.id;
        }
        renderer.sortingOrder = order;
    }

    Vector3 FindSpawnNearTrees(Vector2 direction)
    {
        TreeGenerator[] generators = FindObjectsByType<TreeGenerator>(FindObjectsSortMode.None);
        Collider2D[] boundaries = new Collider2D[generators.Length];
        int boundaryCount = 0;
        foreach (TreeGenerator generator in generators)
        {
            if (generator != null && generator.isActiveAndEnabled && generator.spawnBoundary != null && generator.spawnBoundary.enabled)
                boundaries[boundaryCount++] = generator.spawnBoundary;
        }

        Vector3 best = playerTransform.position + (Vector3)(direction * wolfSpawnMaxRadius);
        float bestDistance = float.MaxValue;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            float angle = Random.Range(-42f, 42f) + Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float radius = Random.Range(wolfSpawnMinRadius, Mathf.Max(wolfSpawnMinRadius, wolfSpawnMaxRadius));
            Vector2 radial = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 candidate = (Vector2)playerTransform.position + radial * radius;
            float edgeDistance = float.MaxValue;

            for (int i = 0; i < boundaryCount; i++)
            {
                Collider2D boundary = boundaries[i];
                if (boundary.OverlapPoint(candidate))
                    continue; // Keep the spawn just outside the tree mass.
                edgeDistance = Mathf.Min(edgeDistance, Vector2.Distance(candidate, boundary.ClosestPoint(candidate)));
            }

            if (edgeDistance < bestDistance)
            {
                bestDistance = edgeDistance;
                best = candidate;
            }
            if (edgeDistance <= 1.8f)
                break;
        }

        best.z = playerTransform.position.z;
        return best;
    }

    IEnumerator HopWolf(GameObject wolf, Vector3 from, Vector3 to, float duration, float jumpHeight)
    {
        if (wolf == null)
            yield break;

        SpriteRenderer[] renderers = wolf.GetComponentsInChildren<SpriteRenderer>(true);
        Vector3[] baseLocalPositions = new Vector3[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            baseLocalPositions[i] = renderers[i].transform.localPosition;
            renderers[i].flipX = to.x < from.x;
        }
        float elapsed = 0f;
        while (elapsed < duration && wolf != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            wolf.transform.position = Vector3.Lerp(from, to, t);
            float jumpOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].transform.localPosition = baseLocalPositions[i] + Vector3.up * jumpOffset;
            yield return null;
        }
        if (wolf != null)
        {
            wolf.transform.position = to;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].transform.localPosition = baseLocalPositions[i];
        }
    }

    IEnumerator WolfAttack(GameObject wolf, int index)
    {
        if (wolf == null)
            yield break;

        Vector3 start = wolf.transform.position;
        Vector3 target = playerTransform.position + (wolfRingPositions[index] - playerTransform.position).normalized * 0.55f;
        target.z = start.z;
        yield return HopWolf(wolf, start, target, wolfLungeDuration, wolfJumpHeight * 0.45f);
    }

    IEnumerator RunBlockTiming(System.Action<bool> onComplete)
    {
        SetBlockBarVisible(true);
        float inputGrace = pendingBlockInputGrace;
        pendingBlockInputGrace = 0f;
        if (inputGrace > 0f)
            Debug.Log($"[WolfFight] Block slider shown; ignoring stop input for {inputGrace:0.00}s so the QTE cannot be skipped.", this);
        blockGreenCenter = Random.Range(-0.42f, 0.42f);
        float greenHalfWidth = greenZoneWidth * 0.5f;
        blockGreenZone.transform.localPosition = new Vector3(blockGreenCenter, 0f, -0.02f);
        blockGreenZone.transform.localScale = new Vector3(greenZoneWidth, 0.1f, 1f);

        bool blocked = false;
        bool movingRightToLeft = true;
        float elapsed = 0f;
        while (true)
        {
            UpdatePromptRootPosition();
            float t = Mathf.Clamp01(elapsed / blockWindowDuration);
            float sliderX = movingRightToLeft
                ? Mathf.Lerp(0.72f, -0.72f, t)
                : Mathf.Lerp(-0.72f, 0.72f, t);
            blockSlider.transform.localPosition = new Vector3(sliderX, 0f, -0.03f);

            if (inputGrace <= 0f && TryReadStopInput())
            {
                blocked = Mathf.Abs(sliderX - blockGreenCenter) <= greenHalfWidth;
                break;
            }

            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;
            inputGrace = Mathf.Max(0f, inputGrace - deltaTime);
            if (elapsed >= blockWindowDuration)
            {
                elapsed = 0f;
                movingRightToLeft = !movingRightToLeft;
            }
            yield return null;
        }

        SetBlockBarVisible(false);
        onComplete(blocked);
    }

    static bool TryReadStopInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;
        return TryReadPressedKey(out _);
    }

    IEnumerator RunCounterCombo(System.Action<bool> onComplete)
    {
        ShowComboPrompts(true);
        comboKeys = new int[comboLength];
        for (int i = 0; i < comboKeys.Length; i++)
            comboKeys[i] = Random.Range(0, 4);
        RefreshComboPrompts(0);

        float remaining = comboTimeLimit;
        int nextKey = 0;
        bool completed = false;
        while (remaining > 0f && nextKey < comboKeys.Length)
        {
            UpdatePromptRootPosition();
            UpdateTimerBar(remaining / comboTimeLimit);
            if (TryReadPressedKey(out int pressedKey) && pressedKey == comboKeys[nextKey])
            {
                nextKey++;
                RefreshComboPrompts(nextKey);
                if (nextKey >= comboKeys.Length)
                {
                    completed = true;
                    break;
                }
            }
            else if (pressedKey >= 0)
            {
                nextKey = 0;
                RefreshComboPrompts(nextKey);
            }

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        ShowComboPrompts(false);
        onComplete(completed);
    }

    void BuildFightPrompts()
    {
        promptRoot = new GameObject("Wolf Fight World Prompts");
        promptRenderers = new SpriteRenderer[Mathf.Max(3, comboLength)];
        for (int i = 0; i < promptRenderers.Length; i++)
        {
            GameObject keyObject = new($"Fight Key {i + 1}");
            keyObject.transform.SetParent(promptRoot.transform, false);
            keyObject.transform.localScale = Vector3.one * promptWorldSize;
            SpriteRenderer renderer = keyObject.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "UI";
            renderer.sortingOrder = fightSortingOrder;
            promptRenderers[i] = renderer;
        }

        runtimePixelSprite = CreateWhitePixelSprite();
        timerBackground = CreateBar("Combo Timer Background", promptRoot.transform, new Color(0.08f, 0.1f, 0.08f, 0.95f), fightSortingOrder - 1);
        timerBackground.transform.localScale = new Vector3(1.5f, 0.09f, 1f);
        timerFill = CreateBar("Combo Timer", promptRoot.transform, new Color(0.92f, 0.76f, 0.27f, 1f), fightSortingOrder);
        blockBackground = CreateBar("Block Timing Bar", promptRoot.transform, new Color(0.06f, 0.08f, 0.07f, 0.98f), fightSortingOrder - 1);
        blockBackground.transform.localScale = new Vector3(1.5f, 0.13f, 1f);
        blockGreenZone = CreateBar("Safe Block Zone", promptRoot.transform, new Color(0.12f, 0.95f, 0.32f, 1f), fightSortingOrder);
        blockSlider = CreateBar("Block Timing Slider", promptRoot.transform, Color.white, fightSortingOrder + 1);
        blockSlider.transform.localScale = new Vector3(0.055f, 0.2f, 1f);
        SetBlockBarVisible(false);
        ShowComboPrompts(false);
    }

    SpriteRenderer CreateBar(string objectName, Transform parent, Color color, int sortingOrder)
    {
        GameObject bar = new(objectName);
        bar.transform.SetParent(parent, false);
        SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
        renderer.sprite = runtimePixelSprite;
        renderer.color = color;
        renderer.sortingLayerName = "UI";
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    static Sprite CreateWhitePixelSprite()
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "Runtime Fight Pixel";
        return sprite;
    }

    void RefreshComboPrompts(int nextKey)
    {
        for (int i = 0; i < promptRenderers.Length; i++)
        {
            if (i >= comboKeys.Length)
            {
                promptRenderers[i].enabled = false;
                continue;
            }

            promptRenderers[i].enabled = true;
            promptRenderers[i].sprite = GetKeySprite(comboKeys[i]);
            promptRenderers[i].color = i < nextKey ? new Color(0.42f, 1f, 0.5f, 1f) : (i == nextKey ? Color.white : UpcomingKeyColor);
        }
    }

    Sprite GetKeySprite(int key) => key switch
    {
        0 => wKey,
        1 => aKey,
        2 => sKey,
        _ => dKey
    };

    static bool TryReadPressedKey(out int key)
    {
        key = -1;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;
        if (keyboard.wKey.wasPressedThisFrame) key = 0;
        else if (keyboard.aKey.wasPressedThisFrame) key = 1;
        else if (keyboard.sKey.wasPressedThisFrame) key = 2;
        else if (keyboard.dKey.wasPressedThisFrame) key = 3;
        return key >= 0;
    }

    void UpdatePromptRootPosition()
    {
        if (promptRoot == null || playerTransform == null)
            return;

        float headY = playerRenderer != null ? playerRenderer.bounds.max.y : playerTransform.position.y + 0.5f;
        promptRoot.transform.position = new Vector3(playerTransform.position.x, headY + promptHeadOffset, playerTransform.position.z);
        float spacing = promptSpacing;
        float width = (promptRenderers.Length - 1) * spacing;
        for (int i = 0; i < promptRenderers.Length; i++)
            promptRenderers[i].transform.localPosition = new Vector3(-width * 0.5f + i * spacing, 0.14f, 0f);

        if (timerBackground != null)
            timerBackground.transform.localPosition = new Vector3(0f, -timerBarOffset, 0f);
        if (timerFill != null && timerFill.enabled)
            timerFill.transform.localPosition = new Vector3(-0.74f, -timerBarOffset, -0.01f);
        if (blockBackground != null)
            blockBackground.transform.localPosition = new Vector3(0f, -0.02f, 0f);
    }

    void UpdateTimerBar(float ratio)
    {
        if (timerFill == null)
            return;
        ratio = Mathf.Clamp01(ratio);
        timerFill.enabled = true;
        timerFill.transform.localScale = new Vector3(1.48f * ratio, 0.07f, 1f);
        timerFill.transform.localPosition = new Vector3(-0.74f + 0.74f * ratio, -timerBarOffset, -0.01f);
    }

    void ShowComboPrompts(bool visible)
    {
        if (promptRenderers != null)
        {
            foreach (SpriteRenderer renderer in promptRenderers)
                renderer.enabled = visible && renderer.sprite != null;
        }
        if (timerBackground != null)
            timerBackground.enabled = visible;
        if (timerFill != null)
            timerFill.enabled = visible;
    }

    void SetBlockBarVisible(bool visible)
    {
        if (blockBackground != null) blockBackground.enabled = visible;
        if (blockGreenZone != null) blockGreenZone.enabled = visible;
        if (blockSlider != null) blockSlider.enabled = visible;
    }

    void ApplyWolfDamage(GameObject attackingWolf)
    {
        if (playerHealth != null && damagePerHit > 0)
            playerHealth.TakeDamage(damagePerHit, "wolf fight");
        if (fightCamera != null)
            StartCameraShake(0.16f, cameraShakeMagnitude * 0.65f);
        StartCoroutine(FlashFightPartyOnHit());
    }

    IEnumerator FlashFightPartyOnHit()
    {
        for (int flash = 0; flash < 3; flash++)
        {
            if (playerTransform != null)
                HitBlinker.Flash(playerTransform.gameObject, 0.08f);

            if (companionGroup != null && companionGroup.Companions != null)
            {
                foreach (CompanionAgent companion in companionGroup.Companions)
                    if (companion != null && companion.gameObject.activeInHierarchy)
                        HitBlinker.Flash(companion.gameObject, 0.08f);
            }

            yield return new WaitForSecondsRealtime(0.13f);
        }
    }

    IEnumerator FlashWolfOnHit(GameObject wolf)
    {
        for (int flash = 0; flash < 3 && wolf != null; flash++)
        {
            HitBlinker.Flash(wolf, 0.08f);
            yield return new WaitForSecondsRealtime(0.13f);
        }
    }

    void EndFight()
    {
        if (strongGuyBlocker != null)
            strongGuyBlocker.RestoreAfterFight();
        if (playerMovement != null)
            playerMovement.SetMovementLocked(false);
        if (companionGroup != null)
            companionGroup.EndFightFormation();
        if (activeWolves != null)
        {
            foreach (GameObject wolf in activeWolves)
                if (wolf != null)
                    Destroy(wolf);
        }
        if (promptRoot != null)
            Destroy(promptRoot);
        if (runtimePixelSprite != null)
        {
            Texture2D texture = runtimePixelSprite.texture;
            Destroy(runtimePixelSprite);
            if (texture != null)
                Destroy(texture);
            runtimePixelSprite = null;
        }
        if (triggerCollider != null)
            triggerCollider.enabled = false;
        if (cameraShakeRoutine != null)
            StopCoroutine(cameraShakeRoutine);
        RestoreCameraPosition();
        fightRoutine = null;
    }

    void OnDisable()
    {
        if (fightRoutine == null)
            return;
        StopCoroutine(fightRoutine);
        EndFight();
    }
}
