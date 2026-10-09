using UnityEngine;

[DisallowMultipleComponent]
public class GoalTarget : MonoBehaviour
{
    [SerializeField] private Transform aimPoint;
    [SerializeField] private GameObject confettiPrefab;
    [SerializeField, Min(0.01f)] private float effectScale = 1f;

    public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * 0.65f;

    public void Celebrate()
    {
        if (confettiPrefab == null) return;
        GameObject effect = Instantiate(confettiPrefab, AimPosition + Vector3.up * 0.8f, Quaternion.identity);
        effect.name = "Goal Confetti Stars";
        effect.transform.localScale = Vector3.one * effectScale;
        float lifetime = 0f;
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.loop = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            // Preserve readable star sizes while fitting the explosion to the football pitch.
            main.startSpeedMultiplier *= 0.18f;
            main.startSizeMultiplier *= 1.25f;
            main.gravityModifierMultiplier *= 0.35f;
            lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            particles.Play(false);
        }
        Destroy(effect, Mathf.Max(2f, lifetime + 1f));
    }
}
