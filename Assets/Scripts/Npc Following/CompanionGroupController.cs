
using UnityEngine;

public class CompanionGroupController : MonoBehaviour
{
    enum GroupState
    {
        Following,
        Scattering
    }

    [Header("References")]
    [SerializeField] Transform player;
    [SerializeField] CompanionAgent[] companions;

    [Header("Following")]
    [SerializeField] float followRadius = 2.2f;
    [SerializeField] float followUpdateInterval = 0.25f;

    [Header("Scattering")]
    [SerializeField] float idleRadiusMin = 2.5f;
    [SerializeField] float idleRadiusMax = 4f;
    [SerializeField] float idleRetargetInterval = 3.5f;
    [SerializeField] float minNpcSeparation = 1.2f;

    [Header("Movement Detection")]
    [SerializeField] float movementThreshold = 0.15f;
    [SerializeField] float stopDelay = 0.4f;

    GroupState currentState;

    Vector3 lastPlayerPosition;
    float lastPlayerMoveTime;
    float nextFollowUpdate;
    float[] nextIdleUpdate;

    void Start()
    {
        if (player == null)
            player = transform;

        lastPlayerPosition = player.position;

        nextIdleUpdate = new float[companions.Length];

        for (int i = 0; i < companions.Length; i++)
        {
            if (companions[i] != null)
                companions[i].InitializeOnNavMesh();
        }

        lastPlayerMoveTime = Time.time;
        currentState = GroupState.Following;
    }

    void Update()
    {
        DetectPlayerMovement();

        bool playerMoving =
            Time.time - lastPlayerMoveTime < stopDelay;

        GroupState newState = playerMoving
            ? GroupState.Following
            : GroupState.Scattering;

        if (newState != currentState)
        {
            currentState = newState;

            if (currentState == GroupState.Scattering)
                ScatterCompanions();
        }

        if (currentState == GroupState.Following)
            UpdateFollowing();
        else
            UpdateScattering();
    }

    void DetectPlayerMovement()
    {
        float distance =
            Vector3.Distance(player.position, lastPlayerPosition);

        if (distance > movementThreshold * Time.deltaTime)
        {
            lastPlayerMoveTime = Time.time;
        }

        lastPlayerPosition = player.position;
    }

    void UpdateFollowing()
    {
        if (Time.time < nextFollowUpdate)
            return;

        nextFollowUpdate = Time.time + followUpdateInterval;

        int count = companions.Length;

        for (int i = 0; i < count; i++)
        {
            if (companions[i] == null)
                continue;

            float angle = (360f / count) * i;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad),
                0f
            ) * followRadius;

            Vector3 targetPosition = player.position + offset;

            companions[i].SetDestination(targetPosition);
        }
    }

    void ScatterCompanions()
    {
        for (int i = 0; i < companions.Length; i++)
        {
            nextIdleUpdate[i] = 0f;
        }

        UpdateScattering();
    }

    void UpdateScattering()
    {
        for (int i = 0; i < companions.Length; i++)
        {
            CompanionAgent companion = companions[i];

            if (companion == null)
                continue;

            if (Time.time < nextIdleUpdate[i])
                continue;

            if (!companion.HasReachedDestination &&
                nextIdleUpdate[i] != 0f)
                continue;

            if (TryFindIdlePosition(i, out Vector3 idlePosition))
            {
                companion.SetDestination(idlePosition);

                nextIdleUpdate[i] =
                    Time.time + idleRetargetInterval;
            }
            else
            {
                nextIdleUpdate[i] = Time.time + 1f;
            }
        }
    }

    bool TryFindIdlePosition(int index, out Vector3 position)
    {
        CompanionAgent current = companions[index];

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            float distance = Random.Range(
                idleRadiusMin,
                idleRadiusMax
            );

            Vector3 candidate = player.position +
                new Vector3(
                    randomDirection.x,
                    randomDirection.y,
                    0f
                ) * distance;

            if (!current.TryGetValidPosition(candidate, out position))
                continue;

            bool valid = true;

            for (int j = 0; j < companions.Length; j++)
            {
                if (j == index || companions[j] == null)
                    continue;

                float separation = Vector3.Distance(
                    position,
                    companions[j].Position
                );

                if (separation < minNpcSeparation)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return true;
        }

        position = Vector3.zero;
        return false;
    }
}
