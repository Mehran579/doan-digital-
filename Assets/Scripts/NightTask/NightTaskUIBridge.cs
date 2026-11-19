using UnityEngine;
using UnityEngine.UI;

namespace DoNotStray
{
    public class NightTaskUIBridge : MonoBehaviour
    {
        [Header("Task Selection (0: Firewood, 1: Water, 2: Food)")]
        [SerializeField] Button btnTaskWood;
        [SerializeField] Button btnTaskWater;
        [SerializeField] Button btnTaskFood;

        [Header("Survivor Buttons (0..5)")]
        [SerializeField] Button[] btnSurvivors;

        [Header("Quick Assign Buttons")]
        [SerializeField] Button btnAssignWood;
        [SerializeField] Button btnAssignWater;
        [SerializeField] Button btnAssignFood;
        [SerializeField] Button btnUnassign;

        [Header("Progression Buttons")]
        [SerializeField] Button btnStartNight;
        [SerializeField] Button btnNextEvening;

        [Header("Target Assigner")]
        [SerializeField] TaskAssigner assigner;

        void Awake()
        {
            if (assigner == null)
            {
#if UNITY_2023_1_OR_NEWER
                assigner = Object.FindFirstObjectByType<TaskAssigner>();
#else
                assigner = Object.FindObjectOfType<TaskAssigner>();
#endif
            }

            if (assigner == null)
            {
                Debug.LogWarning("[NightTaskUIBridge] TaskAssigner not found in scene!");
                return;
            }

            if (btnTaskWood != null)
                btnTaskWood.onClick.AddListener(() => assigner.SelectTask(0));

            if (btnTaskWater != null)
                btnTaskWater.onClick.AddListener(() => assigner.SelectTask(1));

            if (btnTaskFood != null)
                btnTaskFood.onClick.AddListener(() => assigner.SelectTask(2));

            if (btnSurvivors != null)
            {
                for (int i = 0; i < btnSurvivors.Length; i++)
                {
                    int idx = i;
                    if (btnSurvivors[idx] != null)
                        btnSurvivors[idx].onClick.AddListener(() => assigner.ClickSurvivorByIndex(idx));
                }
            }

            if (btnAssignWood != null)
                btnAssignWood.onClick.AddListener(() => assigner.AssignSelectedTo(0));

            if (btnAssignWater != null)
                btnAssignWater.onClick.AddListener(() => assigner.AssignSelectedTo(1));

            if (btnAssignFood != null)
                btnAssignFood.onClick.AddListener(() => assigner.AssignSelectedTo(2));

            if (btnUnassign != null)
                btnUnassign.onClick.AddListener(() => assigner.UnassignSelected());

            if (btnStartNight != null)
                btnStartNight.onClick.AddListener(() => assigner.StartNight());

            if (btnNextEvening != null)
                btnNextEvening.onClick.AddListener(() => assigner.NextEvening());
        }
    }
}
