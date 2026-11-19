using UnityEngine;

/// <summary>Completes an objective step and optionally starts the next objective when the player enters a trigger.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ObjectiveProgressTrigger : MonoBehaviour
{
    [SerializeField] private ObjectiveData objectiveToProgress;
    [SerializeField, ObjectiveStepId(nameof(objectiveToProgress))] private string stepID;
    [SerializeField] private ObjectiveData objectiveToStart;
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered;

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryTrigger(other);

    private void OnTriggerStay2D(Collider2D other) => TryTrigger(other);

    private void TryTrigger(Collider2D other)
    {
        if (hasTriggered || other == null || GetPlayerTransform(other.gameObject) == null)
            return;

        ObjectiveManager manager = ObjectiveManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"[ObjectiveProgressTrigger] ObjectiveManager is missing for {name}.", this);
            return;
        }

        // Wait until the player has reached the "Find the River" objective.
        // This prevents an early visit from starting the bridge quest out of order.
        if (objectiveToProgress != null &&
            manager.GetObjectiveProgress(objectiveToProgress.objectiveID) == null)
            return;

        if (objectiveToProgress != null && !string.IsNullOrEmpty(stepID))
            manager.AddProgress(objectiveToProgress, stepID);

        if (objectiveToStart != null)
            manager.StartObjective(objectiveToStart);

        hasTriggered = true;
    }

    private Transform GetPlayerTransform(GameObject candidate)
    {
        if (!string.IsNullOrEmpty(playerTag) && candidate.CompareTag(playerTag))
            return candidate.transform;

        PlayerMovement playerMovement = candidate.GetComponentInParent<PlayerMovement>();
        return playerMovement != null ? playerMovement.transform : null;
    }
}
