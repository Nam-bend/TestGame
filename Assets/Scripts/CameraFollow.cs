using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    public enum FollowState { Player, Ball, GoalPause }

    [Header("Targets")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private BoxCollider[] goalZones = new BoxCollider[0];

    [Header("Broadcast view")]
    [SerializeField, Range(30f, 80f)] private float pitch = 50f;
    [SerializeField, Min(1f)] private float orthographicSize = 8f;
    [SerializeField, Min(1f)] private float cameraDistance = 32f;
    [SerializeField] private float fieldHeight = 4.61f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.25f;
    [SerializeField, Min(0f)] private float deadZone = 0.6f;

    [Header("Visible area limits (world X/Z)")]
    [SerializeField] private Vector2 visibleX = new Vector2(-20f, 20f);
    [SerializeField] private Vector2 visibleZ = new Vector2(-16f, 16f);

    [Header("Shot tracking")]
    [SerializeField, Min(0f)] private float goalWaitSeconds = 2f;
    [SerializeField, Min(1f)] private float maxBallFollowSeconds = 8f;
    [SerializeField, Min(0f)] private float ballRadius = 0.2f;

    public FollowState State { get; private set; }
    public Transform CurrentBall { get; private set; }

    private Camera view;
    private Vector3 focus;
    private Vector3 velocity;
    private Vector3 lastBallPosition;
    private float stateTime;
    private bool detectGoalAutomatically = true;

    private void Awake()
    {
        view = GetComponent<Camera>();
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTarget = player.transform;
        }
        SnapToPlayer();
    }

    private void LateUpdate()
    {
        Tick(Time.deltaTime);
    }

    // KickSystem can call this directly at the start of a shot.
    public void FollowBall(Transform ball, bool detectGoal = true)
    {
        if (ball == null || State == FollowState.GoalPause) return;
        CurrentBall = ball;
        lastBallPosition = ball.position;
        stateTime = 0f;
        State = FollowState.Ball;
        detectGoalAutomatically = detectGoal;
    }

    // GoalSystem can also report a goal directly; ignore unrelated balls.
    public void NotifyGoal(Transform ball)
    {
        if (State != FollowState.Ball || ball != CurrentBall) return;
        State = FollowState.GoalPause;
        stateTime = 0f;
        velocity = Vector3.zero;
    }

    public void ReturnToPlayer()
    {
        State = FollowState.Player;
        CurrentBall = null;
        stateTime = 0f;
        velocity = Vector3.zero;
    }

    public void SnapToPlayer()
    {
        ReturnToPlayer();
        ConfigureView();
        focus = ClampFocus(playerTarget.position);
        PlaceCamera();
    }

    private void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        stateTime += deltaTime;
        if (State == FollowState.GoalPause)
        {
            if (stateTime < goalWaitSeconds) return;
            ReturnToPlayer();
        }
        if (State == FollowState.Ball)
        {
            if (CurrentBall == null || !CurrentBall.gameObject.activeInHierarchy || stateTime >= maxBallFollowSeconds)
                ReturnToPlayer();
            else
            {
                Vector3 ballPosition = CurrentBall.position;
                if (detectGoalAutomatically && ReachedGoal(lastBallPosition, ballPosition))
                {
                    NotifyGoal(CurrentBall);
                    return;
                }
                lastBallPosition = ballPosition;
            }
        }

        ConfigureView();
        Transform target = State == FollowState.Ball ? CurrentBall : playerTarget;
        if (target == null) return;
        Vector3 desired = target.position;
        desired.y = fieldHeight;
        if (State == FollowState.Player)
        {
            Vector3 difference = desired - focus;
            desired = difference.magnitude <= deadZone ? focus : desired - difference.normalized * deadZone;
        }
        desired = ClampFocus(desired);
        focus = Vector3.SmoothDamp(focus, desired, ref velocity, smoothTime, Mathf.Infinity, deltaTime);
        focus = ClampFocus(focus);
        PlaceCamera();
    }

    private void ConfigureView()
    {
        view.orthographic = true;
        view.orthographicSize = orthographicSize;
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private Vector3 ClampFocus(Vector3 position)
    {
        // Account for the viewport footprint on the pitch, including aspect ratio.
        float halfWidth = orthographicSize * view.aspect;
        float halfDepth = orthographicSize / Mathf.Sin(pitch * Mathf.Deg2Rad);
        position.x = ClampAxis(position.x, visibleX, halfWidth);
        position.z = ClampAxis(position.z, visibleZ, halfDepth);
        position.y = fieldHeight;
        return position;
    }

    private static float ClampAxis(float value, Vector2 limits, float halfView)
    {
        float min = limits.x + halfView;
        float max = limits.y - halfView;
        return min <= max ? Mathf.Clamp(value, min, max) : (limits.x + limits.y) * 0.5f;
    }

    private void PlaceCamera()
    {
        transform.position = focus - transform.forward * cameraDistance;
    }

    private bool ReachedGoal(Vector3 from, Vector3 to)
    {
        Vector3 segment = to - from;
        float distance = segment.magnitude;
        foreach (BoxCollider zone in goalZones)
        {
            if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy) continue;
            Bounds bounds = zone.bounds;
            bounds.Expand(ballRadius * 2f);
            if (bounds.Contains(from) || bounds.Contains(to)) return true;
            // Segment detection also catches fast shots that cross a goal between frames.
            if (distance > 0.0001f && bounds.IntersectRay(new Ray(from, segment / distance), out float hit)
                && hit <= distance) return true;
        }
        return false;
    }

    private void OnValidate()
    {
        visibleX.y = Mathf.Max(visibleX.x, visibleX.y);
        visibleZ.y = Mathf.Max(visibleZ.x, visibleZ.y);
    }
}
