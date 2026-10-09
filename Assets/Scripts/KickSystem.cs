using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class KickSystem : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] balls = new Transform[0];
    [SerializeField] private GoalTarget[] goals = new GoalTarget[0];
    [SerializeField] private CameraFollow followCamera;
    [SerializeField] private JammoKickAnimation kickAnimation;
    [SerializeField] private PlayerController playerMovement;
    [SerializeField, Min(0.1f)] private float kickRange = 2.2f;
    [SerializeField, Min(1f)] private float shotSpeed = 12f;
    [SerializeField, Min(0.1f)] private float approachTimeout = 5f;

    private readonly HashSet<Transform> scoredBalls = new HashSet<Transform>();
    private GoalTarget activeGoal;
    private bool shotInProgress;
    private float approachElapsed;
    private enum ShotPhase { Approach, Windup, Flight }
    private ShotPhase phase;

    public Transform ActiveBall { get; private set; }
    public int GoalsScored { get; private set; }
    public float KickRange => kickRange;
    public bool IsBusy => shotInProgress || (kickAnimation != null && kickAnimation.IsPlaying)
        || (followCamera != null && followCamera.State != CameraFollow.FollowState.Player);
    public bool IsPreparingShot => shotInProgress && phase != ShotPhase.Flight;
    public bool HasGoals => FindNearestGoal(player != null ? player.position : Vector3.zero) != null;

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public Transform FindNearbyBall()
    {
        if (player == null) return null;
        Transform result = null;
        float bestDistance = kickRange * kickRange;
        foreach (Transform ball in balls)
        {
            if (!IsAvailable(ball)) continue;
            float distance = (ball.position - player.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                result = ball;
            }
        }
        return result;
    }

    public Transform FindFarthestBall()
    {
        if (player == null) return null;
        Transform result = null;
        float bestDistance = -1f;
        foreach (Transform ball in balls)
        {
            if (!IsAvailable(ball)) continue;
            float distance = (ball.position - player.position).sqrMagnitude;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                result = ball;
            }
        }
        return result;
    }

    public GoalTarget FindNearestGoal(Vector3 position)
    {
        GoalTarget result = null;
        float bestDistance = float.PositiveInfinity;
        foreach (GoalTarget goal in goals)
        {
            if (goal == null || !goal.isActiveAndEnabled) continue;
            float distance = (goal.AimPosition - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                result = goal;
            }
        }
        return result;
    }

    public bool TryKickNearby() => TryKick(FindNearbyBall(), true);
    public bool TryAutoKick() => TryKick(FindFarthestBall(), false);

    private bool IsAvailable(Transform ball)
    {
        return ball != null && ball.gameObject.activeInHierarchy && ball != ActiveBall && !scoredBalls.Contains(ball);
    }

    private bool TryKick(Transform ball, bool approach)
    {
        if (IsBusy || !IsAvailable(ball)) return false;
        GoalTarget goal = FindNearestGoal(ball.position);
        if (goal == null) return false;
        // The requested straight flight is scripted, so gravity/collisions cannot deflect it.
        Rigidbody body = ball.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        ActiveBall = ball;
        activeGoal = goal;
        shotInProgress = true;
        approachElapsed = 0f;
        if (kickAnimation != null && playerMovement != null)
        {
            playerMovement.SetActionLocked(true);
            phase = approach ? ShotPhase.Approach : ShotPhase.Windup;
            if (!approach && !StartWindup())
            {
                CancelShot();
                return false;
            }
        }
        else StartFlight();
        return true;
    }

    private bool StartWindup()
    {
        phase = ShotPhase.Windup;
        return kickAnimation.PlayKick(activeGoal.AimPosition - ActiveBall.position);
    }

    private void StartFlight()
    {
        phase = ShotPhase.Flight;
        if (followCamera != null) followCamera.FollowBall(ActiveBall, false);
    }

    private void CancelShot()
    {
        shotInProgress = false;
        ActiveBall = null;
        activeGoal = null;
        if (kickAnimation != null) kickAnimation.Cancel();
        if (playerMovement != null) playerMovement.SetActionLocked(false);
        if (followCamera != null) followCamera.ReturnToPlayer();
    }

    private void Tick(float deltaTime)
    {
        if (!shotInProgress || deltaTime <= 0f) return;
        if (ActiveBall == null || !ActiveBall.gameObject.activeInHierarchy || activeGoal == null || !activeGoal.isActiveAndEnabled)
        {
            CancelShot();
            return;
        }
        if (phase == ShotPhase.Approach)
        {
            approachElapsed += deltaTime;
            if (playerMovement == null || !playerMovement.isActiveAndEnabled || approachElapsed >= approachTimeout)
            {
                CancelShot();
                return;
            }
            Vector3 direction = activeGoal.AimPosition - ActiveBall.position;
            direction.y = 0f;
            direction.Normalize();
            Vector3 stance = ActiveBall.position - direction * 0.75f - Vector3.Cross(Vector3.up, direction) * 0.18f;
            if (playerMovement.MoveToKickPosition(stance, deltaTime) && !StartWindup()) CancelShot();
            return;
        }
        if (phase == ShotPhase.Windup)
        {
            if (kickAnimation.ContactReached) StartFlight();
            else if (!kickAnimation.IsPlaying) CancelShot();
            return;
        }
        Vector3 destination = activeGoal.AimPosition;
        ActiveBall.position = Vector3.MoveTowards(ActiveBall.position, destination, shotSpeed * deltaTime);
        ActiveBall.Rotate(Vector3.right, 600f * deltaTime, Space.Self);
        if ((ActiveBall.position - destination).sqrMagnitude > 0.000001f) return;

        scoredBalls.Add(ActiveBall);
        GoalsScored++;
        activeGoal.Celebrate();
        if (followCamera != null) followCamera.NotifyGoal(ActiveBall);
        ActiveBall = null;
        activeGoal = null;
        shotInProgress = false;
    }

    private void OnDisable()
    {
        if (shotInProgress) CancelShot();
    }
}
