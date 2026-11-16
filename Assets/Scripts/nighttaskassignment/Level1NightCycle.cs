using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>Times morning exploration and presents the existing TMP night-task canvas.</summary>
public class Level1NightCycle : MonoBehaviour
{
    [SerializeField, Min(1)] float minExplorationSeconds = 180f;
    [SerializeField, Min(1)] float maxExplorationSeconds = 240f;
    [SerializeField] TMP_Text countdownText;

    public Canvas HudCanvas => countdownText != null ? countdownText.canvas : null;

    public TaskAssigner assigner;
    GameObject nightCanvas;
    PlayerSurvivalHealth playerHealth;

    void Start()
    {
        if (assigner == null)
        {
#if UNITY_2023_1_OR_NEWER
            assigner = FindFirstObjectByType<TaskAssigner>(FindObjectsInactive.Include);
#else
            assigner = FindObjectOfType<TaskAssigner>(true);
#endif
        }
        if (assigner == null)
        {
            Debug.LogError("Place the NightTaskCanvas prefab, with its TaskAssigner, in level1draft. The canvas may be inactive.", this);
            enabled = false;
            return;
        }
        nightCanvas = assigner.gameObject;
        playerHealth = FindFirstObjectByType<PlayerSurvivalHealth>();
        assigner.OnResumeExploration += ResumeExploration;
        nightCanvas.SetActive(false);
        SetCountdownVisible(true);
        StartCoroutine(ExplorationTimer());
    }

    IEnumerator ExplorationTimer()
    {
        float seconds = Random.Range(minExplorationSeconds, maxExplorationSeconds);
        float remaining = seconds;
        while (remaining > 0f)
        {
            UpdateCountdown(remaining);
            yield return null;
            remaining -= Time.deltaTime;
        }

        UpdateCountdown(0f);
        SetCountdownVisible(false);
        Time.timeScale = 0f;
        nightCanvas.SetActive(true);
        yield return null; // allow TaskAssigner.Start to initialize its roster and inventory
        assigner.StartTimedEvening();
    }

    void ResumeExploration()
    {
        if (playerHealth != null)
            playerHealth.OnNightPassedCleanup();

        nightCanvas.SetActive(false);
        Time.timeScale = 1f;
        SetCountdownVisible(true);
        StartCoroutine(ExplorationTimer());
    }

    void UpdateCountdown(float remaining)
    {
        if (countdownText == null)
            return;

        int totalSeconds = Mathf.CeilToInt(remaining);
        countdownText.text = $"Night in {totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    void SetCountdownVisible(bool visible)
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        if (assigner != null) assigner.OnResumeExploration -= ResumeExploration;
    }
}
