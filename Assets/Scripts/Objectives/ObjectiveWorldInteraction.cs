using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>One-time world interaction that advances an objective without picking up the object.</summary>
[DisallowMultipleComponent]
public class ObjectiveWorldInteraction : MonoBehaviour
{
    public enum DialogueCondition
    {
        NoObjectivesStarted,
        ObjectiveNotStarted,
        ObjectiveStepIncomplete,
        ObjectiveStepComplete,
        ObjectiveComplete,
        Always
    }

    [Serializable]
    public class DialogueStage
    {
        public string stageID;
        public DialogueCondition condition;
        public ObjectiveData conditionObjective;
        [ObjectiveStepId(nameof(conditionObjective), "targetObjective")]
        public string conditionStepID;
        public DialogueData dialogue;
        public bool playOnce = true;
        public ObjectiveData startObjectiveAfterDialogue;
        public ObjectiveData progressObjectiveAfterDialogue;
        [ObjectiveStepId(nameof(progressObjectiveAfterDialogue))]
        public string progressStepID;
        public int progressAmount = 1;
    }

    [Header("Prompt Visuals")]
    [SerializeField] private Sprite promptSprite;
    [SerializeField] private Vector3 promptWorldScale = Vector3.one;
    [SerializeField] private Vector3 promptPlayerOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private int promptSortingOrder = 50;

    [Header("Prompt Animation")]
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float popOvershoot = 1.25f;
    [SerializeField] private float floatAmplitude = 0.12f;
    [SerializeField] private float floatFrequency = 3.5f;

    [Header("Interaction")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool startObjectiveOnInteract;
    [SerializeField] private ObjectiveData targetObjective;
    [SerializeField, ObjectiveStepId(nameof(targetObjective))] private string targetStepID = "";
    [SerializeField] private int progressAmount = 1;
    [SerializeField] private ObjectiveData interactionRequiredObjective;
    [SerializeField, ObjectiveStepId(nameof(interactionRequiredObjective))] private string interactionRequiredStepID;
    [SerializeField, Min(1)] private int interactionRequiredAmount = 1;

    [Header("Optional Staged Dialogues")]
    [Tooltip("Stages are checked top to bottom. The first matching stage is played when the player interacts.")]
    [SerializeField] private DialogueStage[] dialogueStages;

    private bool isPlayerInRange;
    private bool hasInteracted;
    private readonly HashSet<string> completedDialogueStages = new();
    private readonly HashSet<Collider2D> playerContactColliders = new();
    private int interactionBlockedFrame = -1;
    private Transform playerTransform;
    private GameObject promptInstance;
    private SpriteRenderer promptRenderer;
    private Coroutine popCoroutine;
    private float floatTimer;

    private void Awake()
    {
        CreatePromptInstance();
    }

    private void Reset()
    {
#if UNITY_EDITOR
        if (promptSprite == null)
        {
            // Qualify UnityEngine.Object because this script also imports System for Serializable.
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/UI/e.png"))
            {
                if (asset is Sprite sprite)
                {
                    promptSprite = sprite;
                    break;
                }
            }
        }
#endif
    }

    private void CreatePromptInstance()
    {
        if (promptInstance != null)
            return;

        promptInstance = new GameObject("KeyPrompt_E_Floating");
        promptInstance.transform.rotation = Quaternion.identity;
        promptInstance.transform.localScale = Vector3.zero;
        promptRenderer = promptInstance.AddComponent<SpriteRenderer>();
        promptRenderer.sortingOrder = promptSortingOrder;
        promptRenderer.sprite = promptSprite;
        promptInstance.SetActive(false);
    }

    private void Update()
    {
        if (!isPlayerInRange || hasInteracted)
            return;

        DialogueManager dialogueManager = DialogueManager.Instance;
        if (dialogueManager != null && dialogueManager.IsActive)
            return;

        if (!IsInteractionRequirementMet())
        {
            if (promptInstance != null && promptInstance.activeSelf)
                HidePrompt(clearPlayerRange: false);
            return;
        }

        if (promptInstance == null || !promptInstance.activeSelf)
            ShowPrompt(playerTransform);

        if (Time.frameCount == interactionBlockedFrame)
            return;

        UpdatePromptPosition();
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Interact();
    }

    private void LateUpdate()
    {
        if (isPlayerInRange && !hasInteracted)
            UpdatePromptPosition();
    }

    private void UpdatePromptPosition()
    {
        if (promptInstance == null || playerTransform == null)
            return;

        floatTimer += Time.deltaTime;
        float yOffset = Mathf.Sin(floatTimer * floatFrequency) * floatAmplitude;
        promptInstance.transform.position = playerTransform.position + promptPlayerOffset + new Vector3(0f, yOffset, 0f);
        promptInstance.transform.rotation = Quaternion.identity;
    }

    private void Interact()
    {
        if (!IsInteractionRequirementMet())
            return;

        ObjectiveManager manager = ObjectiveManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"[ObjectiveWorldInteraction] ObjectiveManager is missing for {name}.", this);
            return;
        }

        if (dialogueStages != null && dialogueStages.Length > 0)
        {
            InteractThroughDialogueStage(manager);
            return;
        }

        if (targetObjective == null)
        {
            Debug.LogWarning($"[ObjectiveWorldInteraction] No objective assigned for {name}.", this);
            return;
        }

        if (startObjectiveOnInteract)
            manager.StartObjective(targetObjective);

        if (!string.IsNullOrEmpty(targetStepID))
            manager.AddProgress(targetObjective, targetStepID, progressAmount);
        else if (!startObjectiveOnInteract)
        {
            Debug.LogWarning($"[ObjectiveWorldInteraction] No objective step assigned for {name}.", this);
            return;
        }

        hasInteracted = true;
        HidePrompt();
    }

    private void InteractThroughDialogueStage(ObjectiveManager objectiveManager)
    {
        DialogueManager dialogueManager = DialogueManager.Instance;
        if (dialogueManager != null && dialogueManager.IsActive)
            return;

        for (int i = 0; i < dialogueStages.Length; i++)
        {
            DialogueStage stage = dialogueStages[i];
            if (stage == null || !StageMatches(stage, objectiveManager))
                continue;

            string stageKey = GetStageKey(stage, i);
            if (stage.playOnce && completedDialogueStages.Contains(stageKey))
                continue;

            Transform interactingPlayer = playerTransform;
            if (stage.dialogue == null)
            {
                if (!HasStageAction(stage))
                {
                    Debug.LogWarning($"[ObjectiveWorldInteraction] Dialogue stage '{stageKey}' has no dialogue or objective action.", this);
                    continue;
                }

                CompleteDialogueStage(stage, stageKey, objectiveManager, interactingPlayer);
                return;
            }

            if (dialogueManager == null)
            {
                Debug.LogWarning($"[ObjectiveWorldInteraction] DialogueManager is missing for {name}.", this);
                return;
            }

            if (stage.dialogue.lines == null || stage.dialogue.lines.Length == 0)
            {
                Debug.LogWarning($"[ObjectiveWorldInteraction] Dialogue stage '{stageKey}' has no lines.", this);
                return;
            }

            dialogueManager.StartDialogue(stage.dialogue, () =>
                CompleteDialogueStage(stage, stageKey, objectiveManager, interactingPlayer));

            if (dialogueManager.IsActive)
                HidePrompt(clearPlayerRange: false);
            return;
        }
    }

    private bool StageMatches(DialogueStage stage, ObjectiveManager manager)
    {
        ObjectiveData conditionObjective = GetConditionObjective(stage);
        switch (stage.condition)
        {
            case DialogueCondition.NoObjectivesStarted:
                return manager.GetActiveObjectives().Count == 0;

            case DialogueCondition.ObjectiveNotStarted:
                return conditionObjective != null &&
                       manager.GetObjectiveProgress(conditionObjective.objectiveID) == null;

            case DialogueCondition.ObjectiveStepIncomplete:
            case DialogueCondition.ObjectiveStepComplete:
            {
                if (conditionObjective == null)
                    return false;

                ObjectiveManager.ObjectiveProgress progress = manager.GetObjectiveProgress(conditionObjective.objectiveID);
                if (!TryGetStepProgress(progress, stage.conditionStepID, out int current, out int required))
                    return false;

                return stage.condition == DialogueCondition.ObjectiveStepComplete
                    ? current >= required
                    : current < required;
            }

            case DialogueCondition.ObjectiveComplete:
                return conditionObjective != null &&
                       manager.GetObjectiveProgress(conditionObjective.objectiveID)?.IsCompleted == true;

            case DialogueCondition.Always:
                return true;

            default:
                return false;
        }
    }

    private ObjectiveData GetConditionObjective(DialogueStage stage)
    {
        return stage.conditionObjective != null ? stage.conditionObjective : targetObjective;
    }

    private static bool TryGetStepProgress(
        ObjectiveManager.ObjectiveProgress progress,
        string stepID,
        out int current,
        out int required)
    {
        current = 0;
        required = 0;
        if (progress?.objectiveData?.steps == null || string.IsNullOrEmpty(stepID))
            return false;

        for (int i = 0; i < progress.objectiveData.steps.Length; i++)
        {
            ObjectiveStep step = progress.objectiveData.steps[i];
            if (step == null || step.stepID != stepID)
                continue;

            required = Mathf.Max(0, step.requiredAmount);
            if (progress.stepProgress != null && i < progress.stepProgress.Length)
                current = progress.stepProgress[i];
            return true;
        }

        return false;
    }

    private bool IsInteractionRequirementMet()
    {
        if (interactionRequiredObjective != null && !string.IsNullOrEmpty(interactionRequiredStepID))
        {
            ObjectiveManager manager = ObjectiveManager.Instance;
            ObjectiveManager.ObjectiveProgress progress = manager != null
                ? manager.GetObjectiveProgress(interactionRequiredObjective.objectiveID)
                : null;

            if (!TryGetStepProgress(progress, interactionRequiredStepID, out int current, out int required) ||
                current < Mathf.Max(interactionRequiredAmount, required))
                return false;
        }

        return HasAvailableDialogueStage();
    }

    private bool HasAvailableDialogueStage()
    {
        if (dialogueStages == null || dialogueStages.Length == 0)
            return true;

        ObjectiveManager manager = ObjectiveManager.Instance;
        if (manager == null)
            return false;

        for (int i = 0; i < dialogueStages.Length; i++)
        {
            DialogueStage stage = dialogueStages[i];
            if (stage == null || !StageMatches(stage, manager))
                continue;

            string stageKey = GetStageKey(stage, i);
            if (stage.playOnce && completedDialogueStages.Contains(stageKey))
                continue;

            if (stage.dialogue != null && stage.dialogue.lines != null && stage.dialogue.lines.Length > 0)
                return true;

            if (stage.dialogue == null && HasStageAction(stage))
                return true;
        }

        return false;
    }

    private static bool HasStageAction(DialogueStage stage)
    {
        return stage.startObjectiveAfterDialogue != null ||
               (stage.progressObjectiveAfterDialogue != null && !string.IsNullOrEmpty(stage.progressStepID));
    }

    private static string GetStageKey(DialogueStage stage, int index)
    {
        return string.IsNullOrEmpty(stage.stageID) ? $"stage_{index}" : stage.stageID;
    }

    private void CompleteDialogueStage(
        DialogueStage stage,
        string stageKey,
        ObjectiveManager objectiveManager,
        Transform interactingPlayer)
    {
        // The same E press that closes dialogue must not immediately start the next stage.
        interactionBlockedFrame = Time.frameCount;

        if (stage.startObjectiveAfterDialogue != null)
            objectiveManager.StartObjective(stage.startObjectiveAfterDialogue);

        if (stage.progressObjectiveAfterDialogue != null && !string.IsNullOrEmpty(stage.progressStepID))
            objectiveManager.AddProgress(
                stage.progressObjectiveAfterDialogue,
                stage.progressStepID,
                stage.progressAmount);

        if (stage.playOnce)
            completedDialogueStages.Add(stageKey);

        if (isPlayerInRange && interactingPlayer != null)
            ShowPrompt(interactingPlayer);
    }

    private void ShowPrompt(Transform player)
    {
        if (hasInteracted)
            return;

        playerTransform = player;
        isPlayerInRange = true;
        if (!IsInteractionRequirementMet())
        {
            if (promptInstance != null)
                HidePrompt(clearPlayerRange: false);
            return;
        }

        floatTimer = 0f;
        if (promptInstance == null)
            CreatePromptInstance();

        if (promptRenderer != null)
        {
            promptRenderer.sprite = promptSprite;
            promptRenderer.sortingOrder = promptSortingOrder;
        }

        promptInstance.SetActive(true);
        UpdatePromptPosition();
        if (popCoroutine != null)
            StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(AnimatePopIn());
    }

    private void HidePrompt(bool clearPlayerRange = true)
    {
        if (clearPlayerRange)
        {
            isPlayerInRange = false;
            playerTransform = null;
        }
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
        Vector3 targetScale = promptWorldScale;
        Vector3 overshootScale = promptWorldScale * popOvershoot;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(popDuration, 0.0001f));
            Vector3 scale;
            if (t < 0.7f)
            {
                float phase = t / 0.7f;
                scale = Vector3.Lerp(Vector3.zero, overshootScale, Mathf.Sin(phase * Mathf.PI * 0.5f));
            }
            else
            {
                float phase = (t - 0.7f) / 0.3f;
                scale = Vector3.Lerp(overshootScale, targetScale, Mathf.Sin(phase * Mathf.PI * 0.5f));
            }

            if (promptInstance != null)
                promptInstance.transform.localScale = scale;
            yield return null;
        }

        if (promptInstance != null)
            promptInstance.transform.localScale = targetScale;
        popCoroutine = null;
    }

    private Transform GetPlayerTransform(GameObject other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return other.transform;

        PlayerMovement movement = other.GetComponentInParent<PlayerMovement>();
        return movement != null ? movement.transform : null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerContactEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HandlePlayerContactExit(other);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        HandlePlayerContactEnter(other.collider);
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        HandlePlayerContactExit(other.collider);
    }

    private void HandlePlayerContactEnter(Collider2D other)
    {
        if (other == null)
            return;

        Transform player = GetPlayerTransform(other.gameObject);
        if (player == null)
            return;

        playerContactColliders.Add(other);
        ShowPrompt(player);
    }

    private void HandlePlayerContactExit(Collider2D other)
    {
        if (other == null || GetPlayerTransform(other.gameObject) == null)
            return;

        playerContactColliders.Remove(other);
        if (playerContactColliders.Count == 0)
            HidePrompt();
    }

    private void OnDisable()
    {
        playerContactColliders.Clear();
        HidePrompt();
    }

    private void OnDestroy()
    {
        if (promptInstance != null)
            Destroy(promptInstance);
    }
}
