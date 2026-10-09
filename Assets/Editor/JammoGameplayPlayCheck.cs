using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// End-to-end check in real Play Mode, including UI callbacks and scene reload.
[InitializeOnLoad]
public static class JammoGameplayPlayCheck
{
    private const string ActiveKey = "Jammo.GameplayPlayCheck.Active";
    private static int stage;
    private static double deadline;
    private static KickSystem kick;
    private static CameraFollow follow;
    private static UIManager ui;
    private static Transform player;
    private static Transform[] balls;
    private static Vector3 initialPlayer;
    private static Vector3[] initialBalls;
    private static Transform shotBall;
    private static Vector3 shotDestination;
    private static int expectedScore;
    private static float holdStarted;
    private static Vector3 frozenCamera;
    private static bool capturedGoal;
    private static Vector3 beforeKick;
    private static int corner;
    private static Quaternion cameraRotation;
    private static GameObject blockingWall;

    static JammoGameplayPlayCheck()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.isPaused = false;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Update;
        }
    }

    public static void SetupAndRun()
    {
        JammoGameplaySetup.Setup();
        RunCurrentScene();
    }

    [MenuItem("Tools/Jammo/Test Current Scene")]
    public static void RunCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the scene before running checks that reload it.");
        EditorApplication.isPaused = false;
        stage = 0;
        capturedGoal = false;
        SessionState.SetBool(ActiveKey, true);
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.EnterPlaymode();
    }

    public static void RunSavedScene()
    {
        EditorSceneManager.OpenScene(JammoGameplaySetup.ScenePath, OpenSceneMode.Single);
        RunCurrentScene();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Finish("Timeout at stage " + stage);
            return;
        }
        if (!EditorApplication.isPlaying || Time.frameCount < 4) return;
        try
        {
            switch (stage)
            {
                case 0:
                    Application.runInBackground = true;
                    Time.timeScale = 1f;
                    Resolve();
                    initialPlayer = player.position;
                    initialBalls = balls.Select(ball => ball.position).ToArray();
                    Require(balls.Length == 5 && kick.GoalsScored == 0, "Initial scene");
                    Require(player.GetComponent<CharacterController>() != null,
                        "Player has CharacterController");
                    var pitch = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                        .First(c => c.name == "land soccer field");
                    Require(!pitch.GetComponents<BoxCollider>().Any(c => c.enabled), "No raised bounding-box ground");
                    Require(pitch.Raycast(new Ray(player.position + Vector3.up * 10f, Vector3.down), out var grassHit, 20f)
                        && Mathf.Abs(player.position.y - grassHit.point.y) < 0.03f,
                        "Player stands on actual grass mesh, not an elevated collider");
                    Require(ui.transform.childCount == 3, "Only three stock UI buttons");
                    Require(Button("Reset").GetComponent<Image>().sprite != null, "Stock button sprite assigned");
                    Require(SceneManager.GetActiveScene().buildIndex >= 0, "Scene enabled in Build Settings");
                    player.position = new Vector3(-11f, initialPlayer.y, -8f);
                    ui.Refresh();
                    Require(!Button("Kick").gameObject.activeSelf, "Kick hides outside range");
                    Require(Button("Auto Kick").gameObject.activeSelf, "Auto Kick always visible");
                    player.position = balls[0].position + Vector3.left * 0.8f;
                    ui.Refresh();
                    Require(Button("Kick").gameObject.activeSelf && Button("Kick").interactable, "Kick appears nearby");
                    shotBall = balls.OrderBy(ball => (ball.position - player.position).sqrMagnitude).First();
                    shotDestination = NearestGoal(shotBall.position);
                    beforeKick = shotBall.position;
                    Click("Kick");
                    Require(kick.ActiveBall == shotBall && kick.IsPreparingShot, "Kick chooses nearby ball and prepares animation");
                    Require(Button("Auto Kick").interactable, "Auto Kick stays interactive");
                    Click("Auto Kick");
                    Require(kick.ActiveBall == shotBall, "Repeated clicks do not replace current shot");
                    expectedScore = 1;
                    stage = 1;
                    break;
                case 1:
                case 4:
                    var animation = player.GetComponentInChildren<JammoKickAnimation>();
                    Require(animation != null, "Kick animation receiver exists");
                    if (kick.IsPreparingShot)
                    {
                        Require(shotBall.position == beforeKick, "Ball waits for animated contact");
                        break;
                    }
                    if (kick.ActiveBall != null)
                    {
                        Require(animation.ContactReached, "Reused animation reaches ball release time");
                        Require(follow.CurrentBall == shotBall, "Camera follows ball after contact");
                    }
                    if (kick.GoalsScored < expectedScore) break;
                    Require(kick.GoalsScored == expectedScore, "Exactly one goal per shot");
                    Require(Vector3.Distance(shotBall.position, shotDestination) < 0.001f, "Ball reaches nearest goal");
                    Require(follow.State == CameraFollow.FollowState.GoalPause, "Camera starts goal pause");
                    Require(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(p => p.isPlaying), "Confetti plays");
                    holdStarted = Time.time;
                    frozenCamera = follow.transform.position;
                    stage = stage == 1 ? 2 : 5;
                    break;
                case 2:
                case 5:
                    if (follow.State == CameraFollow.FollowState.GoalPause)
                    {
                        Require(Vector3.Distance(follow.transform.position, frozenCamera) < 0.001f, "Camera stays still during celebration");
                        Require(Time.time - holdStarted < 2.25f, "Goal pause is not extended");
                        if (!capturedGoal && Time.time - holdStarted > 0.3f)
                        {
                            Require(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                                .Sum(p => p.particleCount) > 0, "Confetti emits visible particles");
                            SoccerSceneLookSetup.SavePreview(Camera.main, null, "Logs/gameplay-goal-preview.png");
                            capturedGoal = true;
                        }
                        break;
                    }
                    Require(Time.time - holdStarted >= 1.8f, "Goal pause lasts two seconds");
                    Require(follow.State == CameraFollow.FollowState.Player, "Return to Jammo");
                    ui.Refresh();
                    if (expectedScore < balls.Length)
                    {
                        stage = 3;
                        break;
                    }
                    Require(kick.FindFarthestBall() == null, "Scored balls are unavailable");
                    Require(Button("Auto Kick").gameObject.activeSelf && Button("Auto Kick").interactable, "Auto Kick remains interactive with no balls");
                    Click("Auto Kick");
                    Require(kick.ActiveBall == null, "No-ball click is safe");
                    Require(!Button("Kick").gameObject.activeSelf, "Kick hides when all balls scored");
                    Click("Reset");
                    stage = 6;
                    break;
                case 3:
                    shotBall = kick.FindFarthestBall();
                    Require(shotBall != null, "Available ball remains, including the single-ball case");
                    // Scored balls sit in the goals; the remaining balls still have their initial positions.
                    Transform expected = balls.Where((ball, i) => Vector3.Distance(ball.position, initialBalls[i]) < 0.001f)
                        .OrderByDescending(ball => (ball.position - player.position).sqrMagnitude).First();
                    Require(shotBall == expected, "Auto Kick selects farthest available ball");
                    shotDestination = NearestGoal(shotBall.position);
                    beforeKick = shotBall.position;
                    Click("Auto Kick");
                    Require(kick.ActiveBall == expected && kick.IsPreparingShot, "Auto Kick UI starts windup");
                    expectedScore++;
                    stage = 4;
                    break;
                case 6:
                case 7:
                    if (kick != null) break; // Wait until LoadScene has replaced the old objects.
                    Resolve();
                    Require(kick.GoalsScored == 0 && kick.ActiveBall == null, "Reset clears score and shot");
                    Require(Vector3.Distance(player.position, initialPlayer) < 0.03f, "Reset restores Jammo (controller skin tolerance)");
                    for (int i = 0; i < balls.Length; i++)
                        Require(Vector3.Distance(balls[i].position, initialBalls[i]) < 0.001f, "Reset restores balls");
                    Require(GameObject.Find("Goal Confetti Stars") == null, "Reset removes goal effects");
                    Require(follow.State == CameraFollow.FollowState.Player, "Reset restores camera");
                    ui.Refresh();
                    Require(Button("Auto Kick").interactable, "Reset restores buttons");
                    if (stage == 6)
                    {
                        Click("Auto Kick");
                        Require(kick.ActiveBall != null, "Shot before mid-flight reset");
                        stage = 12;
                    }
                    else
                    {
                        var body = player.GetComponent<CharacterController>();
                        body.enabled = false;
                        player.position += Vector3.up * 2f;
                        body.enabled = true;
                        holdStarted = Time.time;
                        stage = 8;
                    }
                    break;
                case 8:
                    if (Time.time - holdStarted < 1.5f) break;
                    Require(player.GetComponent<CharacterController>().isGrounded, "Gravity lands player on pitch collider");
                    Require(Mathf.Abs(player.position.y - initialPlayer.y) < 0.03f, "Drop returns player to pitch height");
                    player.GetComponent<PlayerController>().SetActionLocked(true);
                    cameraRotation = follow.transform.rotation;
                    corner = 0;
                    stage = 9;
                    break;
                case 9:
                    Vector3[] targets = { new Vector3(100, 0, 100), new Vector3(-100, 0, 100),
                        new Vector3(-100, 0, -100), new Vector3(100, 0, -100) };
                    bool arrived = player.GetComponent<PlayerController>().MoveToKickPosition(targets[corner], Time.deltaTime);
                    Require(Mathf.Abs(player.position.x) <= 12.02f && Mathf.Abs(player.position.z) <= 8.32f, "Boundary holds while moving");
                    Require(Mathf.Abs(player.position.y - initialPlayer.y) < 0.08f, "Movement stays on pitch");
                    Require(Quaternion.Angle(cameraRotation, follow.transform.rotation) < 0.01f, "Camera does not rotate with player");
                    if (!arrived) break;
                    Require(Mathf.Abs(Mathf.Abs(player.position.x) - 12f) < 0.03f && Mathf.Abs(Mathf.Abs(player.position.z) - 8.3f) < 0.03f,
                        "Movement reaches clamped pitch corner");
                    if (++corner == targets.Length)
                    {
                        Click("Reset");
                        stage = 10;
                    }
                    break;
                case 10:
                    if (kick != null) break;
                    Resolve();
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    Vector3 approachDirection = NearestGoal(balls[0].position) - balls[0].position;
                    approachDirection.y = 0f;
                    approachDirection.Normalize();
                    player.position = balls[0].position - approachDirection * 1.8f;
                    player.position = new Vector3(player.position.x, initialPlayer.y, player.position.z);
                    controller.enabled = true;
                    blockingWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blockingWall.name = "Audit approach obstacle";
                    blockingWall.transform.position = player.position + approachDirection * 0.6f + Vector3.up;
                    blockingWall.transform.rotation = Quaternion.LookRotation(approachDirection);
                    blockingWall.transform.localScale = new Vector3(3f, 3f, 0.2f);
                    Physics.SyncTransforms();
                    Require(kick.TryKickNearby(), "Blocked approach starts");
                    holdStarted = Time.time;
                    beforeKick = player.position;
                    stage = 11;
                    break;
                case 11:
                    Require(Vector3.Distance(player.position, beforeKick) < 0.5f, "Controller cannot cross obstacle");
                    if (Time.time - holdStarted < 5.5f) break;
                    Require(!kick.IsBusy && kick.ActiveBall == null && kick.GoalsScored == 0,
                        "Blocked approach cancels without scoring or staying busy");
                    var lockedField = typeof(PlayerController).GetField("actionLocked",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Require(!(bool)lockedField.GetValue(player.GetComponent<PlayerController>()), "Blocked approach unlocks movement");
                    UnityEngine.Object.DestroyImmediate(blockingWall);
                    Require(kick.TryKickNearby(), "Can retry after blocked approach");
                    kick.enabled = false;
                    Require(!kick.IsBusy && kick.ActiveBall == null, "Disabling kick cancels shot");
                    Require(!(bool)lockedField.GetValue(player.GetComponent<PlayerController>()), "Disabling kick unlocks movement");
                    Finish(null);
                    break;
                case 12:
                    if (kick.IsPreparingShot) break;
                    Require(kick.ActiveBall != null && follow.State == CameraFollow.FollowState.Ball,
                        "Reset test waits for actual ball flight");
                    Click("Reset");
                    stage = 7;
                    break;
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Resolve()
    {
        kick = UnityEngine.Object.FindFirstObjectByType<KickSystem>();
        ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        follow = Camera.main.GetComponent<CameraFollow>();
        player = GameObject.Find("Player_Jammo").transform;
        balls = SceneManager.GetActiveScene().GetRootGameObjects()
            .Where(root => root.name.StartsWith("Soccer Ball", StringComparison.Ordinal))
            .OrderBy(root => root.name, StringComparer.Ordinal).Select(root => root.transform).ToArray();
    }

    private static Vector3 NearestGoal(Vector3 position)
    {
        return UnityEngine.Object.FindObjectsByType<GoalTarget>(FindObjectsSortMode.None)
            .OrderBy(goal => (goal.AimPosition - position).sqrMagnitude).First().AimPosition;
    }

    private static Button Button(string name) => ui.transform.Find(name).GetComponent<Button>();

    private static void Click(string name)
    {
        Button button = Button(name);
        Require(button.gameObject.activeInHierarchy && button.interactable, name + " is clickable");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
    }

    private static void Finish(string error)
    {
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Update;
        Directory.CreateDirectory("Logs");
        string result = error == null
            ? "PASS: real Play Mode UI pointer clicks; near/far Kick visibility; nearby and farthest ball selection; five goals; nearest goal targeting; confetti; camera follow/freeze/return; last-ball and no-ball handling; Reset after goals and during shot; gravity drop onto pitch collider; movement to all four pitch boundaries; stable camera rotation; obstacle collision; blocked approach timeout and retry; disable releases movement lock."
            : "FAIL at stage " + stage + ": " + error;
        File.WriteAllText("Logs/gameplay-playcheck.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
