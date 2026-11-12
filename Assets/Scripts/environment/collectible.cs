using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class collectible : MonoBehaviour
{
    public enum ObjectiveAction
    {
        AddProgress,            // Adds progress to stepID in the specified targetObjective
        StartNewObjective,      // Starts targetObjective in ObjectiveManager
        StartAndAddProgress     // Starts targetObjective (if not already started) AND adds progress to stepID
    }

    [Header("Prompt Visuals")]
    [Tooltip("The 'E' key prompt sprite shown when player is nearby")]
    [SerializeField] private Sprite promptSprite;

    [Tooltip("Target size in world units for the prompt sprite")]
    [SerializeField] private Vector3 promptWorldScale = new Vector3(1f, 1f, 1f);

    [Tooltip("Vertical world offset above the player's head")]
    [SerializeField] private Vector3 promptPlayerOffset = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Sorting order for the prompt sprite (keep higher than world objects)")]
    [SerializeField] private int promptSortingOrder = 50;

    [Header("Animation & Floating")]
    [Tooltip("Duration of pop-in animation in seconds")]
    [SerializeField] private float popDuration = 0.2f;

    [Tooltip("Overshoot multiplier for bouncy pop effect")]
    [SerializeField] private float popOvershoot = 1.25f;

    [Tooltip("Amplitude of the sine-wave floating effect")]
    [SerializeField] private float floatAmplitude = 0.12f;

    [Tooltip("Frequency/speed of the sine-wave floating effect")]
    [SerializeField] private float floatFrequency = 3.5f;

    [Header("Interaction Settings")]
    [Tooltip("Tag used to identify the player. Leave empty to allow any player detected by PlayerMovement component or 'Player' tag.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Destroy this collectible GameObject once collected")]
    [SerializeField] private bool destroyOnCollect = true;

    [Tooltip("Disable collider and renderer instead of destroying immediately")]
    [SerializeField] private bool disableOnCollect = false;

    [Header("Objective Settings")]
    [Tooltip("What action to perform with the ObjectiveManager on collect")]
    [SerializeField] private ObjectiveAction objectiveAction = ObjectiveAction.AddProgress;

    [Tooltip("Target ObjectiveData")]
    [SerializeField] private ObjectiveData targetObjective;

    [Tooltip("Step ID to increment in targetObjective (used with AddProgress or StartAndAddProgress)")]
    [SerializeField] private string targetStepID = "";

    [Tooltip("Amount of progress to add")]
    [SerializeField] private int progressAmount = 1;

    private bool isPlayerInRange = false;
    private Transform playerTransform;

    private GameObject promptInstance;
    private SpriteRenderer promptRenderer;
    private Coroutine popCoroutine;
    private float floatTimer = 0f;

    private void Awake()
    {
        CreatePromptInstance();
    }

    private void Reset()
    {
#if UNITY_EDITOR
        if (promptSprite == null)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("Assets/e.png");
            foreach (var asset in assets)
            {
                if (asset is Sprite s)
                {
                    promptSprite = s;
                    break;
                }
            }
        }
#endif
    }

    private void CreatePromptInstance()
    {
        if (promptInstance != null) return;

        promptInstance = new GameObject("KeyPrompt_E_Floating");
        promptInstance.transform.rotation = Quaternion.identity;
        promptInstance.transform.localScale = Vector3.zero;

        promptRenderer = promptInstance.AddComponent<SpriteRenderer>();
        promptRenderer.sortingOrder = promptSortingOrder;
        if (promptSprite != null)
        {
            promptRenderer.sprite = promptSprite;
        }

        promptInstance.SetActive(false);
    }

    private void Update()
    {
        if (!isPlayerInRange) return;

        UpdatePromptPosition();

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Collect();
        }
    }

    private void LateUpdate()
    {
        if (isPlayerInRange)
        {
            UpdatePromptPosition();
        }
    }

    private void UpdatePromptPosition()
    {
        if (promptInstance == null || playerTransform == null) return;

        floatTimer += Time.deltaTime;
        float ySineOffset = Mathf.Sin(floatTimer * floatFrequency) * floatAmplitude;

        Vector3 targetPos = playerTransform.position + promptPlayerOffset + new Vector3(0f, ySineOffset, 0f);
        promptInstance.transform.position = targetPos;
        promptInstance.transform.rotation = Quaternion.identity;
    }

    public void Collect()
    {
        ApplyObjectiveUpdate();
        HidePrompt();

        if (destroyOnCollect)
        {
            if (promptInstance != null)
            {
                Destroy(promptInstance);
            }
            Destroy(gameObject);
        }
        else if (disableOnCollect)
        {
            var col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.enabled = false;

            var rend = GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            enabled = false;
        }
    }

    private void ShowPrompt(Transform player)
    {
        playerTransform = player;
        isPlayerInRange = true;
        floatTimer = 0f;

        if (promptInstance == null)
        {
            CreatePromptInstance();
        }

        if (promptRenderer != null && promptSprite != null)
        {
            promptRenderer.sprite = promptSprite;
        }

        promptInstance.SetActive(true);
        UpdatePromptPosition();

        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(AnimatePopIn());
    }

    private void HidePrompt()
    {
        isPlayerInRange = false;
        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
            popCoroutine = null;
        }

        if (promptInstance != null)
        {
            promptInstance.SetActive(false);
            promptInstance.transform.localScale = Vector3.zero;
        }
    }

    private IEnumerator AnimatePopIn()
    {
        float elapsed = 0f;
        Vector3 initialScale = Vector3.zero;
        Vector3 targetScale = promptWorldScale;
        Vector3 overshootScale = promptWorldScale * popOvershoot;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);

            Vector3 currentScale;
            if (t < 0.7f)
            {
                float subT = t / 0.7f;
                currentScale = Vector3.Lerp(initialScale, overshootScale, Mathf.Sin(subT * Mathf.PI * 0.5f));
            }
            else
            {
                float subT = (t - 0.7f) / 0.3f;
                currentScale = Vector3.Lerp(overshootScale, targetScale, Mathf.Sin(subT * Mathf.PI * 0.5f));
            }

            if (promptInstance != null)
            {
                promptInstance.transform.localScale = currentScale;
            }

            yield return null;
        }

        if (promptInstance != null)
        {
            promptInstance.transform.localScale = targetScale;
        }
        popCoroutine = null;
    }

    private void ApplyObjectiveUpdate()
    {
        ObjectiveManager mgr = ObjectiveManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[collectible] ObjectiveManager.Instance is null. Cannot update objective.");
            return;
        }

        if (targetObjective == null)
        {
            Debug.LogWarning("[collectible] No targetObjective assigned on " + gameObject.name);
            return;
        }

        string objID = targetObjective.objectiveID;

        switch (objectiveAction)
        {
            case ObjectiveAction.AddProgress:
                if (!string.IsNullOrEmpty(targetStepID))
                {
                    mgr.AddProgress(objID, targetStepID, progressAmount);
                }
                else
                {
                    Debug.LogWarning("[collectible] targetStepID is empty! Specify a step ID to add progress.");
                }
                break;

            case ObjectiveAction.StartNewObjective:
                mgr.StartObjective(targetObjective);
                break;

            case ObjectiveAction.StartAndAddProgress:
                mgr.StartObjective(targetObjective);
                if (!string.IsNullOrEmpty(targetStepID))
                {
                    mgr.AddProgress(objID, targetStepID, progressAmount);
                }
                break;
        }
    }

    private Transform GetPlayerTransform(GameObject go)
    {
        if (!string.IsNullOrEmpty(playerTag) && go.CompareTag(playerTag))
        {
            return go.transform;
        }

        var pm = go.GetComponentInParent<PlayerMovement>();
        if (pm != null)
        {
            return pm.transform;
        }

        return null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Transform p = GetPlayerTransform(collision.gameObject);
        if (p != null)
        {
            ShowPrompt(p);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Transform p = GetPlayerTransform(collision.gameObject);
        if (p != null)
        {
            HidePrompt();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Transform p = GetPlayerTransform(collision.gameObject);
        if (p != null)
        {
            ShowPrompt(p);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Transform p = GetPlayerTransform(collision.gameObject);
        if (p != null)
        {
            HidePrompt();
        }
    }

    private void OnDisable()
    {
        HidePrompt();
    }

    private void OnDestroy()
    {
        if (promptInstance != null)
        {
            Destroy(promptInstance);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + promptPlayerOffset, 0.2f);
    }
}
