using System.IO;
using UnityEditor;
using UnityEngine.SceneManagement;

// One-shot handoff to the already-open Editor; never discard unsaved scene work.
[InitializeOnLoad]
public static class JammoPendingReview
{
    private const string Request = "Logs/stock-ui-review.request";
    static JammoPendingReview() { EditorApplication.update += RunPending; }

    private static void RunPending()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                File.WriteAllText("Logs/stock-ui-review-status.txt", "Waiting: save the open scene, then use Tools/Jammo/Apply UI and Review.");
                return;
            }
        Run();
    }

    [MenuItem("Tools/Jammo/Apply UI and Review")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= RunPending;
        File.Delete(Request);
        File.WriteAllText("Logs/stock-ui-review-status.txt", "Running setup and Play Mode checks.");
        JammoGameplayPlayCheck.SetupAndRun();
    }
}
