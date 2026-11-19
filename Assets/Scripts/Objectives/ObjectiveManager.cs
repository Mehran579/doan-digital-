using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    private static ObjectiveManager _instance;

    public static ObjectiveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ObjectiveManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Serializable]
    public class ObjectiveProgress
    {
        public ObjectiveData objectiveData;
        public int[] stepProgress;

        public bool IsCompleted
        {
            get
            {
                if (objectiveData == null || objectiveData.steps == null)
                    return false;

                if (stepProgress == null || stepProgress.Length != objectiveData.steps.Length)
                    return false;

                for (int i = 0; i < objectiveData.steps.Length; i++)
                {
                    if (stepProgress[i] < objectiveData.steps[i].requiredAmount)
                        return false;
                }

                return true;
            }
        }
    }

    [Header("Initial / Active Objectives")]
    [Tooltip("Starting objectives can be assigned in Inspector or started dynamically at runtime")]
    [SerializeField]
    private List<ObjectiveData> startingObjectives = new();

    [SerializeField]
    private List<ObjectiveProgress> activeObjectives = new();

    // Progress can arrive before an objective is revealed. Keep it for this
    // play session, then transfer it into active progress when that objective starts.
    private readonly List<PendingObjectiveProgress> pendingObjectives = new();

    private class PendingObjectiveProgress
    {
        public ObjectiveData objectiveData;
        public readonly Dictionary<string, int> stepProgress = new();
    }

    public event Action<ObjectiveData> OnObjectiveUpdated;
    public event Action<ObjectiveData> OnObjectiveCompleted;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // Initialize any starting objectives assigned in the Inspector
        if (startingObjectives != null)
        {
            foreach (var obj in startingObjectives)
            {
                if (obj != null)
                {
                    StartObjective(obj);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void StartObjective(ObjectiveData objective)
    {
        if (objective == null || objective.steps == null)
            return;

        if (activeObjectives.Exists(x =>
            x.objectiveData != null &&
            x.objectiveData.objectiveID == objective.objectiveID))
            return;

        ObjectiveProgress progress = new ObjectiveProgress
        {
            objectiveData = objective,
            stepProgress = new int[objective.steps.Length]
        };

        ApplyPendingProgress(objective, progress);

        activeObjectives.Add(progress);

        OnObjectiveUpdated?.Invoke(objective);
        if (progress.IsCompleted)
            OnObjectiveCompleted?.Invoke(objective);
    }

    /// <summary>
    /// Adds progress to an objective even when it has not been revealed yet.
    /// Inactive progress stays hidden until StartObjective is called.
    /// </summary>
    public void AddProgress(ObjectiveData objective, string stepID, int amount = 1)
    {
        if (objective == null || string.IsNullOrEmpty(objective.objectiveID) ||
            objective.steps == null || string.IsNullOrEmpty(stepID) || amount <= 0)
            return;

        ObjectiveProgress activeProgress = activeObjectives.Find(x =>
            x.objectiveData != null &&
            x.objectiveData.objectiveID == objective.objectiveID);

        if (activeProgress != null)
        {
            AddProgress(activeProgress, stepID, amount);
            return;
        }

        ObjectiveStep step = Array.Find(objective.steps, x => x != null && x.stepID == stepID);
        if (step == null)
        {
            Debug.LogWarning($"Step ID '{stepID}' not found in objective '{objective.objectiveID}'.");
            return;
        }

        PendingObjectiveProgress pending = pendingObjectives.Find(x =>
            x.objectiveData != null &&
            x.objectiveData.objectiveID == objective.objectiveID);

        if (pending == null)
        {
            pending = new PendingObjectiveProgress { objectiveData = objective };
            pendingObjectives.Add(pending);
        }

        pending.stepProgress.TryGetValue(stepID, out int currentAmount);
        pending.stepProgress[stepID] = Mathf.Min(currentAmount + amount, Mathf.Max(0, step.requiredAmount));
    }

    public void AddProgress(string objectiveID, string stepID, int amount = 1)
    {
        ObjectiveProgress progress = activeObjectives.Find(x =>
            x.objectiveData != null &&
            x.objectiveData.objectiveID == objectiveID);

        if (progress == null)
            return;

        AddProgress(progress, stepID, amount);
    }

    private void AddProgress(ObjectiveProgress progress, string stepID, int amount)
    {
        if (progress == null || progress.IsCompleted || amount <= 0)
            return;

        if (progress.stepProgress == null || progress.stepProgress.Length != progress.objectiveData.steps.Length)
        {
            progress.stepProgress = new int[progress.objectiveData.steps.Length];
        }

        for (int i = 0; i < progress.objectiveData.steps.Length; i++)
        {
            ObjectiveStep step = progress.objectiveData.steps[i];

            if (step.stepID != stepID)
                continue;

            progress.stepProgress[i] = Mathf.Min(
                progress.stepProgress[i] + amount,
                Mathf.Max(0, step.requiredAmount)
            );

            OnObjectiveUpdated?.Invoke(progress.objectiveData);

            if (progress.IsCompleted)
                OnObjectiveCompleted?.Invoke(progress.objectiveData);

            return;
        }

        Debug.LogWarning(
            $"Step ID '{stepID}' not found in objective '{progress.objectiveData.objectiveID}'."
        );
    }

    private void ApplyPendingProgress(ObjectiveData objective, ObjectiveProgress progress)
    {
        int pendingIndex = pendingObjectives.FindIndex(x =>
            x.objectiveData != null &&
            x.objectiveData.objectiveID == objective.objectiveID);

        if (pendingIndex < 0)
            return;

        PendingObjectiveProgress pending = pendingObjectives[pendingIndex];
        for (int i = 0; i < objective.steps.Length; i++)
        {
            ObjectiveStep step = objective.steps[i];
            if (step != null && pending.stepProgress.TryGetValue(step.stepID, out int amount))
                progress.stepProgress[i] = Mathf.Min(amount, Mathf.Max(0, step.requiredAmount));
        }

        pendingObjectives.RemoveAt(pendingIndex);
    }

    public void RemoveObjective(string objectiveID)
    {
        activeObjectives.RemoveAll(x =>
            x.objectiveData != null &&
            x.objectiveData.objectiveID == objectiveID);

        OnObjectiveUpdated?.Invoke(null);
    }

    public IReadOnlyList<ObjectiveProgress> GetActiveObjectives()
    {
        return activeObjectives;
    }

    public ObjectiveProgress GetObjectiveProgress(string objectiveID)
    {
        return activeObjectives.Find(x =>
            x.objectiveData != null &&
            x.objectiveData.objectiveID == objectiveID);
    }
}
