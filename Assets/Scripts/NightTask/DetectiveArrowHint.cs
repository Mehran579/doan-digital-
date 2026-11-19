using UnityEngine;

/// <summary>Periodically pops an arrow above the Detective, pointing toward a kidnapped survivor.</summary>
public class DetectiveArrowHint : MonoBehaviour
{
    [SerializeField] Sprite arrowSprite;
    [SerializeField] Vector3 detectiveHeadOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] Vector3 arrowWorldScale = new Vector3(0.45f, 0.45f, 1f);
    [SerializeField, Min(0.1f)] float hintInterval = 10f;
    [SerializeField, Min(0.1f)] float hintDuration = 2f;
    [SerializeField] int sortingOrder = 50;

    GameObject arrowObject;
    SpriteRenderer arrowRenderer;
    float nextHintTime;
    float hideAt;
    Transform currentTarget;

    void Awake()
    {
        arrowObject = new GameObject("Detective Rescue Direction Arrow");
        arrowObject.transform.localScale = arrowWorldScale;
        arrowRenderer = arrowObject.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = arrowSprite;
        arrowRenderer.sortingOrder = sortingOrder;
        arrowObject.SetActive(false);
    }

    void Update()
    {
        TaskAssigner assigner = TaskAssigner.Instance;
        if (assigner == null || !assigner.CanDetectiveHint())
        {
            arrowObject.SetActive(false);
            currentTarget = null;
            nextHintTime = Time.time + hintInterval;
            return;
        }

        if (Time.time >= nextHintTime)
        {
            if (assigner.TryGetDetectiveHint(out GameObject kidnappedCopy) && kidnappedCopy != null)
            {
                currentTarget = kidnappedCopy.transform;
                hideAt = Time.time + hintDuration;
                arrowObject.SetActive(true);
            }
            nextHintTime = Time.time + hintInterval;
        }

        if (!arrowObject.activeSelf) return;
        if (currentTarget == null || Time.time >= hideAt)
        {
            arrowObject.SetActive(false);
            currentTarget = null;
            return;
        }

        arrowObject.transform.position = transform.position + detectiveHeadOffset;
        Vector2 direction = currentTarget.position - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            arrowObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void OnDestroy()
    {
        if (arrowObject != null) Destroy(arrowObject);
    }
}
