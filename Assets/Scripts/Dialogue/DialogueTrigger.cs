using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueTrigger : MonoBehaviour
{
    public DialogueData dialogue;
    [SerializeField] private ObjectiveData startObjectiveAfterDialogue;
    public bool requireInput = true;
    [Tooltip("If enabled, this trigger starts its dialogue only once after successfully opening it.")]
    [SerializeField] bool triggerOnlyOnce;
    public InputAction interact;

    bool playerInRange;
    bool hasTriggered;

    void Reset()
    {
        interact = new InputAction("Interact", InputActionType.Button);
        interact.AddBinding("<Keyboard>/e");
        interact.AddBinding("<Gamepad>/buttonSouth");
    }

    void OnEnable()
    {
        EnsureInteractAction();
        interact.Enable();
    }

    void OnDisable()
    {
        if (interact != null)
            interact.Disable();
    }

    void EnsureInteractAction()
    {
        if (interact != null && interact.bindings.Count > 0)
            return;

        interact = new InputAction("Interact", InputActionType.Button);
        interact.AddBinding("<Keyboard>/e");
        interact.AddBinding("<Gamepad>/buttonSouth");
    }

    void Update()
    {
        if (requireInput && playerInRange && interact.WasPressedThisFrame())
            TriggerDialogue();
    }

    public void TriggerDialogue()
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        DialogueManager manager = DialogueManager.Instance;
        if (manager == null || manager.IsActive || dialogue == null)
            return;

        System.Action onDialogueComplete = null;
        if (startObjectiveAfterDialogue != null)
        {
            onDialogueComplete = () =>
            {
                ObjectiveManager objectiveManager = ObjectiveManager.Instance;
                if (objectiveManager != null)
                    objectiveManager.StartObjective(startObjectiveAfterDialogue);
                else
                    Debug.LogWarning($"[DialogueTrigger] ObjectiveManager is missing; cannot start '{startObjectiveAfterDialogue.name}'.", this);
            };
        }

        manager.StartDialogue(dialogue, onDialogueComplete);
        if (manager.IsActive)
            hasTriggered = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        if (!requireInput) TriggerDialogue();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

}
