
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] InputActionReference moveAction;
    [SerializeField] float moveSpeed = 5f;

    Rigidbody2D rb;
    Vector2 movement;
    float movementMultiplier = 1f;
    bool movementLocked;

    public float BaseMoveSpeed => moveSpeed;
    public float CurrentMoveSpeed => movementLocked ? 0f : moveSpeed * movementMultiplier;
    public float MovementMultiplier => movementMultiplier;
    public bool IsMovementLocked => movementLocked;

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked && rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    public void SetMovementMultiplier(float multiplier)
    {
        movementMultiplier = Mathf.Max(0f, multiplier);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        moveAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
    }

    void Update()
    {
        movement = moveAction.action.ReadValue<Vector2>().normalized;
    }

    void FixedUpdate()
    {
        rb.linearVelocity = movementLocked ? Vector2.zero : movement * moveSpeed * movementMultiplier;
    }
}
