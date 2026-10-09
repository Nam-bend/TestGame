using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Blend";
    [SerializeField, Min(0f)] private float runBlendValue = 0.6f;

    [Header("Field boundary (world X/Z; adjust to your scene)")]
    [SerializeField] private Vector2 xLimits = new Vector2(-10f, 10f);
    [SerializeField] private Vector2 zLimits = new Vector2(-15f, 15f);

    private int speedParameterId;
    private bool actionLocked;
    private CharacterController body;
    private float verticalSpeed;

    public void SetActionLocked(bool locked)
    {
        actionLocked = locked;
        if (locked) animator.SetFloat(speedParameterId, 0f);
    }

    public bool MoveToKickPosition(Vector3 destination, float deltaTime)
    {
        destination = ClampToField(destination);
        destination.y = transform.position.y;
        Vector3 direction = destination - transform.position;
        MoveHorizontal(Vector3.MoveTowards(transform.position, destination, moveSpeed * deltaTime));
        if (direction.sqrMagnitude > 0.0004f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), turnSpeed * deltaTime);
        bool arrived = (destination - transform.position).sqrMagnitude <= 0.0004f;
        animator.SetFloat(speedParameterId, arrived ? 0f : runBlendValue, 0.12f, deltaTime);
        return arrived;
    }

    private void Awake()
    {
        body = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        speedParameterId = Animator.StringToHash(speedParameter);
        animator.applyRootMotion = false;
        transform.position = ClampToField(transform.position);
    }

    private void Update()
    {
        // CharacterController resolves collisions; gravity must be applied explicitly.
        verticalSpeed = body.isGrounded && verticalSpeed < 0f ? -2f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
        CollisionFlags gravityContact = body.Move(Vector3.up * verticalSpeed * Time.deltaTime);
        if ((gravityContact & CollisionFlags.Below) != 0) verticalSpeed = -2f;
        if (actionLocked) return;
        float horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                         - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float vertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                       - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);

        // Normalize so diagonal movement is no faster than straight movement.
        Vector3 direction = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f);
        MoveHorizontal(transform.position + direction * moveSpeed * Time.deltaTime);

        if (direction.sqrMagnitude > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        // Keep Run while input is held, including at the edge of the field.
        animator.SetFloat(speedParameterId, direction.magnitude * runBlendValue, 0.12f, Time.deltaTime);
    }

    private Vector3 ClampToField(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, xLimits.x, xLimits.y);
        position.z = Mathf.Clamp(position.z, zLimits.x, zLimits.y);
        return position;
    }

    private void MoveHorizontal(Vector3 destination)
    {
        destination = ClampToField(destination);
        destination.y = transform.position.y;
        Vector3 displacement = destination - transform.position;
        // A zero-length second Move would clear the grounded result from gravity.
        if (displacement.sqrMagnitude > 0.00000001f) body.Move(displacement);
    }

    private void OnValidate()
    {
        xLimits.y = Mathf.Max(xLimits.x, xLimits.y);
        zLimits.y = Mathf.Max(zLimits.x, zLimits.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((xLimits.x + xLimits.y) * 0.5f, transform.position.y,
                                    (zLimits.x + zLimits.y) * 0.5f);
        Gizmos.DrawWireCube(center, new Vector3(xLimits.y - xLimits.x, 0.05f, zLimits.y - zLimits.x));
    }
}
