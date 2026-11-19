
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CompanionAgent : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float destinationThreshold = 0.35f;

    NavMeshAgent agent;
    float defaultMoveSpeed;
    Vector3 lastDestination;
    bool hasDestination;
    bool externallyControlled;

    

    public Vector3 Position => transform.position;
    public float DefaultMoveSpeed => defaultMoveSpeed;
    public bool IsExternallyControlled => externallyControlled;
    public float AgentRadius => agent != null ? agent.radius : 0.5f;
    public bool IsOnNavMesh => agent != null && agent.enabled && agent.isOnNavMesh;

    public void SetExternallyControlled(bool controlled)
    {
        externallyControlled = controlled;
        if (!controlled)
            ClearCachedDestination();
    }

    public void SetRuntimeSpeed(float speed)
    {
        if (agent != null)
            agent.speed = Mathf.Max(0f, speed);
    }

    public void SetFightPaused(bool paused)
    {
        if (agent == null || !agent.enabled)
            return;

        if (paused)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
        }
        else if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    public bool WarpForFight(Vector3 worldPosition)
    {
        if (agent == null || !agent.enabled)
            return false;

        return NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, 3f, agent.areaMask) &&
               agent.Warp(hit.position);
    }

    public bool HasReachedDestination =>
        agent != null &&
        !agent.pathPending &&
        (!agent.hasPath ||
        agent.remainingDistance <= agent.stoppingDistance + 0.15f);

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        defaultMoveSpeed = moveSpeed;

        agent.updateRotation = false;
        agent.updateUpAxis = false;

        agent.speed = moveSpeed;
        agent.stoppingDistance = 0.15f;
        agent.autoBraking = true;

        agent.obstacleAvoidanceType =
            ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        agent.avoidancePriority = Random.Range(30, 70);
    }

    public bool InitializeOnNavMesh()
    {
        if (NavMesh.SamplePosition(
            transform.position,
            out NavMeshHit hit,
            3f,
            agent.areaMask))
        {
            return agent.Warp(hit.position);
        }

        Debug.LogWarning(name + " could not find NavMesh nearby.");
        return false;
    }

    public void PrepareForTeleport()
    {
        hasDestination = false;
        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }
    }

    public void FinishTeleportAt(Vector3 worldPosition)
    {
        hasDestination = false;
        transform.position = worldPosition;
        agent.enabled = true;
        if (!agent.Warp(worldPosition))
            Debug.LogWarning($"{name}: NavMeshAgent could not rejoin the NavMesh at its teleport destination.", this);
    }

    public bool TryGetValidPosition(
        Vector3 position,
        out Vector3 validPosition)
    {
        if (NavMesh.SamplePosition(
            position,
            out NavMeshHit hit,
            2f,
            agent.areaMask))
        {
            validPosition = hit.position;
            return true;
        }

        validPosition = Vector3.zero;
        return false;
    }

    public void ClearCachedDestination()
    {
        hasDestination = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    public void SetDestination(Vector3 destination)
    {
        if (!agent.isOnNavMesh)
            return;

        if (hasDestination &&
            (lastDestination - destination).sqrMagnitude <
            destinationThreshold * destinationThreshold)
        {
            return;
        }

        if (TryGetValidPosition(destination, out Vector3 validPosition))
        {
            agent.SetDestination(validPosition);

            lastDestination = validPosition;
            hasDestination = true;
        }
    }

    public bool TrySetFightDestination(Vector3 destination)
    {
        if (agent == null || !agent.enabled)
            return false;
        if (!agent.isOnNavMesh && !InitializeOnNavMesh())
            return false;

        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, agent.areaMask))
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        bool assigned = agent.SetPath(path);
        if (assigned)
        {
            hasDestination = true;
            lastDestination = hit.position;
        }
        return assigned;
    }
}
