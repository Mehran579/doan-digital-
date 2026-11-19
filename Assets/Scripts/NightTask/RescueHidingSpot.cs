using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>Interaction point for rescuing a survivor hidden here. Add a trigger Collider2D to the same object.</summary>
[RequireComponent(typeof(Collider2D))]
public class RescueHidingSpot : MonoBehaviour
{
    static readonly System.Collections.Generic.List<RescueHidingSpot> spots = new System.Collections.Generic.List<RescueHidingSpot>();
    [SerializeField] InputActionReference interactAction;
    [SerializeField] string spotName;
    readonly HashSet<Collider2D> playerCollidersInRange = new();
    TaskAssigner taskAssigner;
    bool playerInRange => playerCollidersInRange.Count > 0;

    public static System.Collections.Generic.IReadOnlyList<RescueHidingSpot> AllSpots => spots;
    public string SpotName => string.IsNullOrWhiteSpace(spotName) ? name : spotName;

    void Reset() { GetComponent<Collider2D>().isTrigger = true; }
    void OnEnable() { if (!spots.Contains(this)) spots.Add(this); }
    void OnDisable()
    {
        spots.Remove(this);
        playerCollidersInRange.Clear();
    }

    void Start()
    {
        taskAssigner = TaskAssigner.Instance;
        if (taskAssigner == null)
        {
#if UNITY_2023_1_OR_NEWER
            taskAssigner = FindFirstObjectByType<TaskAssigner>(FindObjectsInactive.Include);
#else
            taskAssigner = FindObjectOfType<TaskAssigner>(true);
#endif
        }
    }

    Transform GetPlayerTransform(GameObject candidate)
    {
        if (candidate.CompareTag("Player"))
            return candidate.transform;

        PlayerMovement movement = candidate.GetComponentInParent<PlayerMovement>();
        return movement != null ? movement.transform : null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && GetPlayerTransform(other.gameObject) != null)
            playerCollidersInRange.Add(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other != null)
            playerCollidersInRange.Remove(other);
    }

    void Update()
    {
        if (!playerInRange) return;
        bool pressed = interactAction != null && interactAction.action != null && interactAction.action.enabled
            ? interactAction.action.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (!pressed)
            return;

        if (taskAssigner == null)
            taskAssigner = TaskAssigner.Instance;
        if (taskAssigner == null)
        {
            Debug.LogWarning($"Cannot rescue at {SpotName}: TaskAssigner is not initialized.", this);
            return;
        }

        taskAssigner.TryRescueAt(this);
    }

}
