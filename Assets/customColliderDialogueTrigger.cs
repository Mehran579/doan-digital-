using UnityEngine;
using UnityEngine.InputSystem;

public class customColliderDialogueTrigger : MonoBehaviour
{
    public DialogueData dialogue;
    public bool requireInput = true;
    public InputAction interact;

    bool playerInRange;

    void Reset()
    {
        interact = new InputAction("Interact", InputActionType.Button);
        interact.AddBinding("<Keyboard>/e");
        interact.AddBinding("<Gamepad>/buttonSouth");
    }

    void OnEnable() => interact.Enable();
    void OnDisable() => interact.Disable();

    void Update()
    {
        if (requireInput && playerInRange && interact.WasPressedThisFrame())
            TriggerDialogue();
    }

    public void TriggerDialogue()
    {

        DialogueManager.Instance.StartDialogue(dialogue);
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