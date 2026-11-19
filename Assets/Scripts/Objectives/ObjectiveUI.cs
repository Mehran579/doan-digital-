using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ObjectiveUI : MonoBehaviour
{
    [Header("Panel & Visibility")]
    [Tooltip("The main container GameObject for the objective UI")]
    [SerializeField] private GameObject panel;

    [Tooltip("CanvasGroup on the panel for smooth toggling without disabling GameObject/Update")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Whether the panel starts open on game start")]
    [SerializeField] private bool startVisible = true;

    [Header("Input (New Input System)")]
    [Tooltip("Optional InputActionReference for toggling the objective window. If unassigned, defaults to Keyboard Tab.")]
    [SerializeField] private InputActionReference toggleAction;

    [Header("UI Text References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text stepsText;
    [SerializeField] private TMP_Text statusBadgeText;

    [Header("Display Options")]
    [Tooltip("Show completed objectives at the bottom of the list")]
    [SerializeField] private bool showCompletedObjectives = true;

    private bool isVisible = true;

    private void Awake()
    {
        if (panel == null)
        {
            panel = gameObject;
        }

        if (canvasGroup == null && panel != null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnEnable()
    {
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnToggleActionPerformed;
        }
    }

    private void OnDisable()
    {
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed -= OnToggleActionPerformed;
            toggleAction.action.Disable();
        }
    }

    private void Start()
    {
        SetVisible(startVisible);

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.OnObjectiveUpdated += OnObjectiveDataEvent;
            ObjectiveManager.Instance.OnObjectiveCompleted += OnObjectiveDataEvent;
            RefreshUI();
        }
        else
        {
            RefreshUI();
        }
    }

    private void OnDestroy()
    {
        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.OnObjectiveUpdated -= OnObjectiveDataEvent;
            ObjectiveManager.Instance.OnObjectiveCompleted -= OnObjectiveDataEvent;
        }
    }

    private void OnObjectiveDataEvent(ObjectiveData data)
    {
        RefreshUI();
    }

    private void Update()
    {
        if (toggleAction == null || toggleAction.action == null)
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleUI();
            }
        }
    }

    private void OnToggleActionPerformed(InputAction.CallbackContext context)
    {
        ToggleUI();
    }

    public void ToggleUI()
    {
        SetVisible(!isVisible);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = isVisible;
            canvasGroup.interactable = isVisible;
        }
        else if (panel != null)
        {
            if (panel != gameObject)
            {
                panel.SetActive(isVisible);
            }
        }

        if (isVisible)
        {
            RefreshUI();
        }
    }

    public void RefreshUI()
    {
        ObjectiveManager mgr = ObjectiveManager.Instance;
        IReadOnlyList<ObjectiveManager.ObjectiveProgress> activeList = mgr != null ? mgr.GetActiveObjectives() : null;

        if (activeList == null || activeList.Count == 0)
        {
            if (titleText != null) titleText.text = "No Active Objectives";
            if (descriptionText != null)
            {
                descriptionText.text = "Explore the surroundings...";
                descriptionText.gameObject.SetActive(true);
            }
            if (stepsText != null) stepsText.text = "";
            if (statusBadgeText != null) statusBadgeText.text = "";
            return;
        }

        // Hide old single-objective title/desc fields to avoid duplicating with the list
        if (titleText != null) titleText.gameObject.SetActive(false);
        if (descriptionText != null) descriptionText.gameObject.SetActive(false);

        var sb = new StringBuilder();
        int activeCount = 0;
        int completedCount = 0;

        for (int o = 0; o < activeList.Count; o++)
        {
            var item = activeList[o];
            if (item == null || item.objectiveData == null) continue;

            bool isCompleted = item.IsCompleted;
            if (isCompleted) completedCount++;
            else activeCount++;

            if (isCompleted && !showCompletedObjectives)
                continue;

            // Objective Header
            string titleColor = isCompleted ? "#8FD16C" : "#E8B923";
            string titleBadge = isCompleted ? "[DONE]" : "[ACTIVE]";
            string titleName = string.IsNullOrWhiteSpace(item.objectiveData.title) ? item.objectiveData.objectiveID : item.objectiveData.title.Trim();

            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine($"<color={titleColor}><b>{titleName}</b></color> <size=10><color=#AAAAAA>{titleBadge}</color></size>");

            // Optional description
            if (!string.IsNullOrWhiteSpace(item.objectiveData.description))
            {
                sb.AppendLine($"<size=11><color=#BBBBBB><i>{item.objectiveData.description.Trim()}</i></color></size>");
            }

            // Steps
            if (item.objectiveData.steps != null)
            {
                for (int i = 0; i < item.objectiveData.steps.Length; i++)
                {
                    var step = item.objectiveData.steps[i];
                    int prog = (item.stepProgress != null && i < item.stepProgress.Length) ? item.stepProgress[i] : 0;
                    bool stepDone = (step.requiredAmount <= 0) || (prog >= step.requiredAmount);

                    string marker = stepDone ? "<color=#72D572>[X]</color>" : "<color=#AAAAAA>[ ]</color>";
                    string stepDesc = string.IsNullOrWhiteSpace(step.description) ? step.stepID : step.description;

                    if (step.requiredAmount > 1)
                    {
                        sb.AppendLine($"  {marker} {stepDesc} ({prog}/{step.requiredAmount})");
                    }
                    else
                    {
                        sb.AppendLine($"  {marker} {stepDesc}");
                    }
                }
            }
        }

        if (stepsText != null)
        {
            stepsText.text = sb.ToString().TrimEnd();
        }

        if (statusBadgeText != null)
        {
            if (activeCount > 0)
            {
                statusBadgeText.text = $"<color=#E8B923>[{activeCount} ACTIVE]</color>";
            }
            else
            {
                statusBadgeText.text = "<color=#72D572>[ALL COMPLETED]</color>";
            }
        }
    }
}
