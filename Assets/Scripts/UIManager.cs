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
        kickButton.onClick.AddListener(KickNearby);
        autoKickButton.onClick.AddListener(AutoKick);
        resetButton.onClick.AddListener(ResetScene);
        Refresh();
    }

    private void OnDisable()
    {
        kickButton.onClick.RemoveListener(KickNearby);
        autoKickButton.onClick.RemoveListener(AutoKick);
        resetButton.onClick.RemoveListener(ResetScene);
    }

    private void Update() => Refresh();

    public void Refresh()
    {
        Transform nearby = kickSystem.FindNearbyBall();
        bool ready = !kickSystem.IsBusy && kickSystem.HasGoals;

        kickButton.gameObject.SetActive(nearby != null);
        kickButton.interactable = ready;

        autoKickButton.gameObject.SetActive(true);
        autoKickButton.interactable = ready;
    }

    private void KickNearby()
    {
        kickSystem.TryKickNearby();
        Refresh();
    }

    private void AutoKick()
    {
        autoKickSystem.KickFarthest();
        Refresh();
    }

    public void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
