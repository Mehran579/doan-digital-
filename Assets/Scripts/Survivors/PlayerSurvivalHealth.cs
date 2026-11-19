using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>Shared party health and poisonous-bush effects for Level 1.</summary>
public sealed class PlayerSurvivalHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1)] int maxHealth = 100;
    [SerializeField, Min(0)] int startingHealth = 100;

    [Header("Poison")]
    [SerializeField] Tilemap poisonousBushTilemap;
    [SerializeField] Transform survivorsRoot;
    [SerializeField] BushDetector bushDetector;
    [SerializeField, Min(1f)] float poisonDuration = 60f;
    [SerializeField, Min(0.1f)] float poisonDamageInterval = 7f;
    [SerializeField, Min(1)] int poisonDamage = 1;
    [SerializeField, Range(0.05f, 1f)] float poisonedSpeedMultiplier = 0.5f;

    [SerializeField, Min(0.1f)] float healthRegenerationDuration = 60f;
    [SerializeField, Min(0.1f)] float healthRegenerationInterval = 3f;
    [SerializeField, Min(0.1f)] float healthFlashDuration = 0.45f;

    [Header("Health Bar")]
    [SerializeField] Vector2 healthBarSize = new(260f, 22f);
    [SerializeField] Vector2 healthBarOffset = new(0f, 24f);

    int currentHealth;
    float poisonTimeRemaining;
    float timeUntilPoisonDamage;
    bool isPoisoned;
    Transform poisonedPartyMember;
    bool wasTouchingBush;
    bool isRegeneratingHealth;
    float regenerationRemaining;
    float timeUntilHealthTick;
    float greenFlashRemaining;
    float purpleFlashRemaining;
    float nurseTreatmentCooldownRemaining;

    PlayerMovement playerMovement;
    Collider2D playerCollider;
    Collider2D[] partyColliders;
    SpriteRenderer[] partySprites;
    TilemapCollider2D poisonousBushCollider;
    Image healthFill;
    RectTransform healthFillRect;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsPoisoned => isPoisoned;
    public bool IsRegeneratingHealth => isRegeneratingHealth;
    public bool IsNurseTreatmentCoolingDown => nurseTreatmentCooldownRemaining > 0f;
    public event Action<Transform> OnPoisoned;
    public event Action OnNightPassed;

    void Awake()
    {
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        playerMovement = GetComponent<PlayerMovement>();
        playerCollider = GetComponent<Collider2D>();
        if (survivorsRoot == null)
        {
            GameObject root = GameObject.Find("survivors");
            if (root != null)
                survivorsRoot = root.transform;
        }
        if (survivorsRoot != null)
        {
            partyColliders = survivorsRoot.GetComponentsInChildren<Collider2D>(true);
            partySprites = survivorsRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    void Start()
    {
        EnableHiddenBushContactCollider();
        CreateHealthBar();
        UpdateHealthBar();
    }

    void EnableHiddenBushContactCollider()
    {
        if (poisonousBushTilemap == null)
        {
            Debug.LogError("PlayerSurvivalHealth has no poisonous bush Tilemap assigned.", this);
            return;
        }

        // The source Tilemap is inactive in the scene. Activate it so its
        // TilemapCollider2D can participate in physics, but keep its art hidden.
        poisonousBushTilemap.gameObject.SetActive(true);
        TilemapRenderer bushRenderer = poisonousBushTilemap.GetComponent<TilemapRenderer>();
        if (bushRenderer != null)
            bushRenderer.enabled = false;

        poisonousBushCollider = poisonousBushTilemap.GetComponent<TilemapCollider2D>();
        if (poisonousBushCollider != null)
        {
            poisonousBushCollider.isTrigger = true;
            poisonousBushCollider.enabled = true;
            poisonousBushCollider.ProcessTilemapChanges();
        }
        else
        {
            Debug.LogError("The poisonous bush Tilemap needs a TilemapCollider2D for contact detection.", this);
        }
    }

    void Update()
    {
        if (nurseTreatmentCooldownRemaining > 0f)
            nurseTreatmentCooldownRemaining = Mathf.Max(0f, nurseTreatmentCooldownRemaining - Time.deltaTime);

        bool touchingBush = TryGetPartyBushContact(out Vector3Int contactedCell, out Transform contactedMember);
        if (touchingBush && !wasTouchingBush)
        {
            MarkContactedBush(contactedCell);
            if (!isPoisoned)
                ApplyPoison(contactedMember);
        }
        wasTouchingBush = touchingBush;

        if (isPoisoned)
        {
            poisonTimeRemaining -= Time.deltaTime;
            timeUntilPoisonDamage -= Time.deltaTime;
            if (timeUntilPoisonDamage <= 0f)
            {
                TakeDamage(poisonDamage, "poison");
                timeUntilPoisonDamage = poisonDamageInterval;
            }

            if (poisonTimeRemaining <= 0f)
                ClearPoison();
        }

        UpdateHealthRegeneration();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == poisonousBushCollider)
        {
            Debug.Log($"[PlayerSurvivalHealth] A party member contacted poisonous bush. Already poisoned: {isPoisoned}.", this);
            if (TryGetPartyBushContact(out Vector3Int contactedCell, out Transform contactedMember))
            {
                MarkContactedBush(contactedCell);
                if (!isPoisoned)
                    ApplyPoison(contactedMember);
            }
        }
    }

    void MarkContactedBush(Vector3Int cell)
    {
        if (bushDetector != null)
            bushDetector.MarkPoisonousBush(cell);
    }

    bool TryGetPartyBushContact(out Vector3Int contactedCell, out Transform contactedMember)
    {
        contactedCell = default;
        contactedMember = null;
        if (poisonousBushTilemap == null)
            return false;

        if (playerCollider != null && BoundsTouchPoisonousBush(playerCollider.bounds, out contactedCell))
        {
            contactedMember = transform;
            return true;
        }

        if (partyColliders != null)
        {
            foreach (Collider2D partyCollider in partyColliders)
            {
                if (partyCollider == null || !partyCollider.enabled || !partyCollider.gameObject.activeInHierarchy)
                    continue;
                if (BoundsTouchPoisonousBush(partyCollider.bounds, out contactedCell))
                {
                    contactedMember = GetPartyMemberRoot(partyCollider.transform);
                    return true;
                }
            }
        }

        // Some survivor sprites have no 2D collider; use their rendered bounds
        // as a contact fallback so their bush contact still poisons the party.
        if (partySprites != null)
        {
            foreach (SpriteRenderer sprite in partySprites)
            {
                if (sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy)
                    continue;
                if (sprite.GetComponent<Collider2D>() != null)
                    continue;
                if (BoundsTouchPoisonousBush(sprite.bounds, out contactedCell))
                {
                    contactedMember = GetPartyMemberRoot(sprite.transform);
                    return true;
                }
            }
        }

        return false;
    }

    Transform GetPartyMemberRoot(Transform contactTransform)
    {
        if (survivorsRoot == null || contactTransform == null || !contactTransform.IsChildOf(survivorsRoot))
            return transform;

        Transform memberRoot = contactTransform;
        while (memberRoot.parent != null && memberRoot.parent != survivorsRoot)
            memberRoot = memberRoot.parent;
        return memberRoot;
    }

    bool BoundsTouchPoisonousBush(Bounds partyBounds, out Vector3Int contactedCell)
    {
        contactedCell = default;
        Vector3Int min = poisonousBushTilemap.WorldToCell(partyBounds.min);
        Vector3Int max = poisonousBushTilemap.WorldToCell(partyBounds.max);
        Vector3 cellSize = poisonousBushTilemap.layoutGrid.cellSize;
        Vector3 worldCellSize = Vector3.Scale(cellSize, poisonousBushTilemap.transform.lossyScale);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector3Int cell = new(x, y, poisonousBushTilemap.origin.z);
                if (!poisonousBushTilemap.HasTile(cell))
                    continue;

                Bounds bushBounds = new(poisonousBushTilemap.GetCellCenterWorld(cell), worldCellSize);
                if (partyBounds.Intersects(bushBounds))
                {
                    contactedCell = cell;
                    return true;
                }
            }
        }

        return false;
    }

    void ApplyPoison(Transform poisonedMember)
    {
        // Re-poisoning during the Nurse's recovery window cancels healing, but
        // the original treatment cooldown continues to run.
        if (isRegeneratingHealth)
        {
            isRegeneratingHealth = false;
            regenerationRemaining = 0f;
            greenFlashRemaining = 0f;
            SetHealthFillColor(NormalHealthColor);
        }

        isPoisoned = true;
        poisonedPartyMember = poisonedMember != null ? poisonedMember : transform;
        poisonTimeRemaining = poisonDuration;
        timeUntilPoisonDamage = poisonDamageInterval;
        if (playerMovement != null)
            playerMovement.SetMovementMultiplier(poisonedSpeedMultiplier);
        Debug.Log($"[PlayerSurvivalHealth] Player poisoned for {poisonDuration:0.#} seconds. Speed multiplier: {poisonedSpeedMultiplier:0.##}.", this);
        OnPoisoned?.Invoke(poisonedPartyMember);
    }

    void ClearPoison()
    {
        bool hadPoison = isPoisoned;
        isPoisoned = false;
        poisonTimeRemaining = 0f;
        timeUntilPoisonDamage = 0f;
        poisonedPartyMember = null;
        if (playerMovement != null)
            playerMovement.SetMovementMultiplier(1f);
        if (hadPoison)
            Debug.Log("[PlayerSurvivalHealth] Poison wore off or was treated.", this);
    }

    public bool CompleteNurseTreatment()
    {
        if (!isPoisoned || isRegeneratingHealth)
            return false;

        ClearPoison();
        isRegeneratingHealth = true;
        regenerationRemaining = healthRegenerationDuration;
        nurseTreatmentCooldownRemaining = healthRegenerationDuration;
        timeUntilHealthTick = healthRegenerationInterval;
        return true;
    }

    public void OnNightPassedCleanup()
    {
        ClearPoison();
        isRegeneratingHealth = false;
        regenerationRemaining = 0f;
        nurseTreatmentCooldownRemaining = 0f;
        timeUntilHealthTick = 0f;
        greenFlashRemaining = 0f;
        purpleFlashRemaining = 0f;
        SetHealthFillColor(NormalHealthColor);
        OnNightPassed?.Invoke();
    }

    void UpdateHealthRegeneration()
    {
        if (greenFlashRemaining > 0f)
        {
            greenFlashRemaining -= Time.deltaTime;
            if (greenFlashRemaining <= 0f && healthFill != null)
                SetHealthFillColor(NormalHealthColor);
        }

        if (purpleFlashRemaining > 0f)
        {
            purpleFlashRemaining -= Time.deltaTime;
            if (purpleFlashRemaining <= 0f && healthFill != null)
                SetHealthFillColor(NormalHealthColor);
        }

        if (!isRegeneratingHealth)
            return;

        regenerationRemaining -= Time.deltaTime;
        timeUntilHealthTick -= Time.deltaTime;
        if (timeUntilHealthTick <= 0f)
        {
            if (currentHealth < maxHealth)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + 1);
                UpdateHealthBar();
                if (healthFill != null)
                    SetHealthFillColor(new Color(0.1f, 1f, 0.22f, 1f));
                greenFlashRemaining = healthFlashDuration;
            }
            timeUntilHealthTick = healthRegenerationInterval;
        }

        if (regenerationRemaining <= 0f)
        {
            isRegeneratingHealth = false;
            regenerationRemaining = 0f;
        }
    }

    public void TakeDamage(int amount, string source = "damage")
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        UpdateHealthBar();
        SetHealthFillColor(new Color(0.72f, 0.25f, 1f, 1f));
        purpleFlashRemaining = healthFlashDuration;
        Debug.Log($"[PlayerSurvivalHealth] Player took {amount} damage from {source}. Health: {currentHealth}/{maxHealth}.", this);
    }

    static readonly Color NormalHealthColor = new(0.72f, 0.18f, 0.16f, 1f);

    void SetHealthFillColor(Color color)
    {
        if (healthFill != null)
            healthFill.color = color;
    }

    void CreateHealthBar()
    {
        Level1NightCycle nightCycle = FindFirstObjectByType<Level1NightCycle>();
        Canvas canvas = nightCycle != null ? nightCycle.HudCanvas : null;
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("PlayerSurvivalHealth needs a Canvas for the health bar.", this);
            return;
        }

        GameObject backgroundObject = new("Party Health Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(canvas.transform, false);
        RectTransform backgroundRect = (RectTransform)backgroundObject.transform;
        backgroundRect.anchorMin = new Vector2(0.5f, 0f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0f);
        backgroundRect.anchoredPosition = healthBarOffset;
        backgroundRect.sizeDelta = healthBarSize;

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.12f, 0.1f, 0.1f, 0.9f);
        background.raycastTarget = false;

        GameObject fillObject = new("Health Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);
        healthFillRect = (RectTransform)fillObject.transform;
        healthFillRect.anchorMin = Vector2.zero;
        healthFillRect.anchorMax = Vector2.one;
        healthFillRect.offsetMin = new Vector2(3f, 3f);
        healthFillRect.offsetMax = new Vector2(-3f, -3f);

        healthFill = fillObject.GetComponent<Image>();
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.color = new Color(0.72f, 0.18f, 0.16f, 1f);
        healthFill.raycastTarget = false;
    }

    void UpdateHealthBar()
    {
        float healthRatio = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        if (healthFill != null)
            healthFill.fillAmount = healthRatio;

        // Also resize the UI rect itself so the bar visibly shrinks even when
        // the Image has no sprite configured for Filled rendering.
        if (healthFillRect != null)
        {
            Vector2 anchors = healthFillRect.anchorMax;
            anchors.x = healthRatio;
            healthFillRect.anchorMax = anchors;
        }
    }
}
