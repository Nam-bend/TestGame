using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Opt-in standalone verification only: JammoFootball.exe -jammo-smoke-test.
public class JammoBuildSmokeCheck : MonoBehaviour
{
    private float started;

    private void Awake() { started = Time.realtimeSinceStartup; }

    private void Update()
    {
        if (Time.realtimeSinceStartup - started < 45f) return;
        Debug.LogError("JAMMO_STANDALONE_SMOKE_FAIL: timeout");
        Application.Quit(1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfRequested()
    {
        if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("-jammo-smoke-test")) return;
        if (FindFirstObjectByType<JammoBuildSmokeCheck>() != null) return;
        var check = new GameObject("Standalone smoke check");
        DontDestroyOnLoad(check);
        check.AddComponent<JammoBuildSmokeCheck>();
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return new WaitForSeconds(2);
        var player = FindFirstObjectByType<PlayerController>();
        if (!Check(player != null && player.GetComponent<CharacterController>().isGrounded, "Player grounded after startup")) yield break;
        var kick = FindFirstObjectByType<KickSystem>();
        var ui = FindFirstObjectByType<UIManager>();
        var camera = Camera.main.GetComponent<CameraFollow>();
        Transform expected = kick.FindFarthestBall();
        ui.transform.Find("Auto Kick").GetComponent<Button>().onClick.Invoke();
        if (!Check(kick.ActiveBall == expected && expected != null, "Standalone Auto Kick chooses farthest ball")) yield break;
        float deadline = Time.realtimeSinceStartup + 15;
        while (kick.GoalsScored == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Check(kick.GoalsScored == 1, "Standalone scores goal")) yield break;
        if (!Check(camera.State == CameraFollow.FollowState.GoalPause, "Standalone goal camera pause")) yield break;
        yield return new WaitForSeconds(2.2f);
        if (!Check(camera.State == CameraFollow.FollowState.Player, "Standalone camera returns")) yield break;
        ui.transform.Find("Reset").GetComponent<Button>().onClick.Invoke();
        yield return null;
        yield return new WaitForSeconds(1);
        kick = FindFirstObjectByType<KickSystem>();
        if (!Check(kick.GoalsScored == 0 && kick.ActiveBall == null, "Standalone Reset restores scene")) yield break;
        Debug.Log("JAMMO_STANDALONE_SMOKE_PASS");
        Application.Quit(0);
    }

    private bool Check(bool condition, string message)
    {
        if (condition) { Debug.Log("SMOKE_PASS: " + message); return true; }
        Debug.LogError("JAMMO_STANDALONE_SMOKE_FAIL: " + message);
        Application.Quit(1);
        return false;
    }
}
