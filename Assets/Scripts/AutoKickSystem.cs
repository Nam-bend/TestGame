using UnityEngine;

public class AutoKickSystem : MonoBehaviour
{
    [SerializeField] private KickSystem kickSystem;

    public void KickFarthest()
    {
        kickSystem.TryAutoKick();
    }
}
