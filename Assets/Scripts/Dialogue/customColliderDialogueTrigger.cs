using UnityEngine;
using UnityEngine.InputSystem;

public class customColliderDialogueTrigger : MonoBehaviour
{
    public DialogueData dialogue;
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

        manager.StartDialogue(dialogue);
        if (manager.IsActive)
            hasTriggered = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;
        playerInRange = true;
        if (!requireInput)
            TriggerDialogue();
    }
    
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            playerInRange = false;
    }

}
