using UnityEngine;

public class AutoKickSystem : MonoBehaviour
{
    [SerializeField] private KickSystem kickSystem;

    public void KickFarthest()
    {
        if (kickSystem != null) kickSystem.TryAutoKick();
    }
}
