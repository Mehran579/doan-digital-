using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Interaction point for rescuing a survivor hidden here. Add a trigger Collider2D to the same object.</summary>
[RequireComponent(typeof(Collider2D))]
public class RescueHidingSpot : MonoBehaviour
{
    static readonly System.Collections.Generic.List<RescueHidingSpot> spots = new System.Collections.Generic.List<RescueHidingSpot>();
    [SerializeField] InputActionReference interactAction;
    [SerializeField] string spotName;
    bool playerInRange;

    public static System.Collections.Generic.IReadOnlyList<RescueHidingSpot> AllSpots => spots;
    public string SpotName => string.IsNullOrWhiteSpace(spotName) ? name : spotName;

    void Reset() { GetComponent<Collider2D>().isTrigger = true; }
    void OnEnable() { if (!spots.Contains(this)) spots.Add(this); }
    void OnDisable() { spots.Remove(this); }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    void Update()
    {
        if (!playerInRange) return;
        bool pressed = interactAction != null
            ? interactAction.action.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (pressed && TaskAssigner.Instance != null)
            TaskAssigner.Instance.TryRescueAt(this);
    }
}
