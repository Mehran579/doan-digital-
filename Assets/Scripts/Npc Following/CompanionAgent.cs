
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CompanionAgent : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float destinationThreshold = 0.35f;

    NavMeshAgent agent;
    Vector3 lastDestination;
    bool hasDestination;

    

    public Vector3 Position => transform.position;

    public bool HasReachedDestination =>
        agent != null &&
        !agent.pathPending &&
        (!agent.hasPath ||
        agent.remainingDistance <= agent.stoppingDistance + 0.15f);

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

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
}
