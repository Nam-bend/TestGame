using UnityEngine;

// Detects horizontal shots whether the ball uses Rigidbody physics or Transform movement.
[DisallowMultipleComponent]
public class BallCameraTarget : MonoBehaviour
{
    [SerializeField] private CameraFollow followCamera;
    [SerializeField, Min(0.1f)] private float shotSpeedThreshold = 2f;
    private Vector3 previousPosition;
    private bool wasMoving;

    private void OnEnable()
    {
        previousPosition = transform.position;
        wasMoving = false;
    }

    private void Start()
    {
        if (followCamera == null && Camera.main != null)
            followCamera = Camera.main.GetComponent<CameraFollow>();
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        Vector3 displacement = transform.position - previousPosition;
        displacement.y = 0f;
        bool moving = displacement.magnitude / Time.deltaTime >= shotSpeedThreshold;
        if (moving && !wasMoving && followCamera != null && followCamera.State == CameraFollow.FollowState.Player)
            followCamera.FollowBall(transform);
        wasMoving = moving;
        previousPosition = transform.position;
    }
}
