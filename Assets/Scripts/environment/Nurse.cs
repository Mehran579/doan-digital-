using System.Collections;
using UnityEngine;

/// <summary>Controls the Nurse's delayed NavMesh approach and poison treatment.</summary>
public sealed class Nurse : MonoBehaviour
{
    [SerializeField] PlayerSurvivalHealth health;
    [SerializeField] GameObject healEffectPrefab;
    [SerializeField, Min(0f)] float delayMin = 20f;
    [SerializeField, Min(0f)] float delayMax = 35f;
    [SerializeField, Min(0.1f)] float approachDistance = 0.9f;
    [SerializeField, Min(0.1f)] float healDistance = 1.1f;
    [SerializeField, Min(0.05f)] float destinationRefreshInterval = 0.3f;

    CompanionAgent companionAgent;
    Coroutine treatmentRoutine;
    GameObject activeHealEffect;

    void Awake()
    {
        companionAgent = GetComponent<CompanionAgent>();
    }

    void OnEnable()
    {
        SubscribeToHealth();
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnPoisoned -= HandlePoisoned;
            health.OnNightPassed -= HandleNightPassed;
        }

        CancelTreatment();
        ClearHealEffect();
    }

    void SubscribeToHealth()
    {
        if (health == null)
            health = FindFirstObjectByType<PlayerSurvivalHealth>();
        if (health == null)
            return;

        health.OnPoisoned -= HandlePoisoned;
        health.OnPoisoned += HandlePoisoned;
        health.OnNightPassed -= HandleNightPassed;
        health.OnNightPassed += HandleNightPassed;
    }

    void HandlePoisoned(Transform patient)
    {
        CancelTreatment();
        bool useInitialDelay = !health.IsNurseTreatmentCoolingDown;
        treatmentRoutine = StartCoroutine(TreatPatient(patient, useInitialDelay));
    }

    IEnumerator TreatPatient(Transform patient, bool useInitialDelay)
    {
        if (useInitialDelay)
        {
            float delay = Random.Range(Mathf.Min(delayMin, delayMax), Mathf.Max(delayMin, delayMax));
            yield return new WaitForSeconds(delay);
        }

        // A re-poison interrupts regeneration, but the original cooldown still
        // prevents an immediate second treatment.
        while (health != null && health.IsPoisoned && health.IsNurseTreatmentCoolingDown)
            yield return null;

        if (health == null || !health.IsPoisoned || patient == null || !patient.gameObject.activeInHierarchy)
        {
            treatmentRoutine = null;
            yield break;
        }

        if (companionAgent == null)
            companionAgent = GetComponent<CompanionAgent>();
        if (companionAgent == null)
        {
            Debug.LogError("Nurse needs a CompanionAgent to reach the poisoned party member.", this);
            treatmentRoutine = null;
            yield break;
        }

        companionAgent.SetExternallyControlled(true);
        float nextDestinationUpdate = 0f;

        while (health != null && health.IsPoisoned && patient != null && patient.gameObject.activeInHierarchy &&
               companionAgent != null && companionAgent.gameObject.activeInHierarchy)
        {
            if (Vector2.Distance(companionAgent.Position, patient.position) <= healDistance)
                break;

            if (Time.time >= nextDestinationUpdate)
            {
                Vector2 awayFromNurse = (Vector2)(companionAgent.Position - patient.position);
                if (awayFromNurse.sqrMagnitude < 0.001f)
                    awayFromNurse = Vector2.right;

                Vector3 approachPosition = patient.position + (Vector3)(awayFromNurse.normalized * approachDistance);
                companionAgent.SetDestination(approachPosition);
                nextDestinationUpdate = Time.time + destinationRefreshInterval;
            }

            yield return null;
        }

        bool reachedPatient = health != null && health.IsPoisoned && patient != null && patient.gameObject.activeInHierarchy &&
                              companionAgent != null && companionAgent.gameObject.activeInHierarchy &&
                              Vector2.Distance(companionAgent.Position, patient.position) <= healDistance;
        if (reachedPatient && health.CompleteNurseTreatment())
        {
            if (healEffectPrefab != null)
            {
                ClearHealEffect();
                activeHealEffect = Instantiate(healEffectPrefab, patient.position, Quaternion.identity);
                Destroy(activeHealEffect, 4f);
            }
        }

        if (companionAgent != null)
            companionAgent.SetExternallyControlled(false);
        treatmentRoutine = null;
    }

    void CancelTreatment()
    {
        if (treatmentRoutine != null)
            StopCoroutine(treatmentRoutine);
        treatmentRoutine = null;

        if (companionAgent != null)
            companionAgent.SetExternallyControlled(false);
    }

    void HandleNightPassed()
    {
        CancelTreatment();
        ClearHealEffect();
    }

    void ClearHealEffect()
    {
        if (activeHealEffect != null)
            Destroy(activeHealEffect);
        activeHealEffect = null;
    }
}
