using UnityEngine;

// Plays one cycle of an existing motion, without adding events to the source clip.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class JammoKickAnimation : MonoBehaviour
{
    [SerializeField] private PlayerController movement;
    private Animator animator;
    private float elapsed;
    private static readonly int KickState = Animator.StringToHash("Base Layer.Kick");

    public bool IsPlaying { get; private set; }
    public bool ContactReached { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (movement == null) movement = GetComponentInParent<PlayerController>();
    }

    public bool PlayKick(Vector3 direction)
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (IsPlaying || !animator.HasState(0, KickState)) return false;
        direction.y = 0f;
        if (movement != null)
        {
            movement.SetActionLocked(true);
            if (direction.sqrMagnitude > 0.0001f)
                movement.transform.rotation = Quaternion.LookRotation(direction);
        }
        IsPlaying = true;
        ContactReached = false;
        elapsed = 0f;
        animator.CrossFadeInFixedTime(KickState, 0.06f, 0, 0f);
        return true;
    }

    // Release the ball during the reused motion.
    public void OnKickContact()
    {
        if (IsPlaying) ContactReached = true;
    }

    public void OnKickComplete()
    {
        IsPlaying = false;
        if (movement != null) movement.SetActionLocked(false);
    }

    public void Cancel()
    {
        ContactReached = false;
        OnKickComplete();
        if (animator != null && animator.isActiveAndEnabled)
            animator.CrossFadeInFixedTime("Base Layer.NormalStatus", 0.1f);
    }

    private void Update()
    {
        if (!IsPlaying) return;
        elapsed += Time.deltaTime;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash == KickState)
        {
            if (state.normalizedTime >= 0.4f) OnKickContact();
            if (state.normalizedTime >= 0.95f) OnKickComplete();
        }
        else if (ContactReached && !animator.IsInTransition(0)) OnKickComplete();
        // Recover if another animation interrupts the action.
        if (elapsed > 2f) Cancel();
    }

    private void OnDisable()
    {
        ContactReached = false;
        OnKickComplete();
    }
}
