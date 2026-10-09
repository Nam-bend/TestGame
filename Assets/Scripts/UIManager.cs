using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private KickSystem kickSystem;
    [SerializeField] private AutoKickSystem autoKickSystem;
    [SerializeField] private Button kickButton;
    [SerializeField] private Button autoKickButton;
    [SerializeField] private Button resetButton;

    private void OnEnable()
    {
        if (kickButton != null) kickButton.onClick.AddListener(KickNearby);
        if (autoKickButton != null) autoKickButton.onClick.AddListener(AutoKick);
        if (resetButton != null) resetButton.onClick.AddListener(ResetScene);
        Refresh();
    }

    private void OnDisable()
    {
        if (kickButton != null) kickButton.onClick.RemoveListener(KickNearby);
        if (autoKickButton != null) autoKickButton.onClick.RemoveListener(AutoKick);
        if (resetButton != null) resetButton.onClick.RemoveListener(ResetScene);
    }

    private void Update() => Refresh();

    public void Refresh()
    {
        if (kickSystem == null) return;
        Transform nearby = kickSystem.FindNearbyBall();
        bool ready = !kickSystem.IsBusy && kickSystem.HasGoals;
        if (kickButton != null)
        {
            kickButton.gameObject.SetActive(nearby != null);
            kickButton.interactable = ready;
        }
        if (autoKickButton != null)
        {
            autoKickButton.gameObject.SetActive(true);
            autoKickButton.interactable = true;
        }
    }

    private void KickNearby()
    {
        if (kickSystem != null) kickSystem.TryKickNearby();
        Refresh();
    }

    private void AutoKick()
    {
        if (autoKickSystem != null) autoKickSystem.KickFarthest();
        Refresh();
    }

    public void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
