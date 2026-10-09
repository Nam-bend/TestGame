using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JammoCameraSetup
{
    private const string ScenePath = "Assets/Scenes/Location soccer field.unity";

    [MenuItem("Tools/Jammo/Setup Group 2 Camera")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up the camera.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        GameObject player = roots.FirstOrDefault(root => root.name == "Player_Jammo");
        Camera camera = Camera.main;
        if (player == null || camera == null) throw new InvalidOperationException("Missing Jammo or Main Camera.");
        CameraFollow follow = camera.GetComponent<CameraFollow>();
        if (follow == null) follow = camera.gameObject.AddComponent<CameraFollow>();

        Transform[] goals = roots.SelectMany(root => root.GetComponentsInChildren<Transform>())
            .Where(item => item.name == "soccer goal" || item.name == "soccer goal (1)").ToArray();
        if (goals.Length != 2) throw new InvalidOperationException("Expected two soccer goals.");
        BoxCollider[] zones = new BoxCollider[2];
        for (int index = 0; index < goals.Length; index++)
        {
            Transform goal = goals[index];
            Transform zoneTransform = goal.Find("CameraGoalZone");
            if (zoneTransform == null)
            {
                zoneTransform = new GameObject("CameraGoalZone").transform;
                zoneTransform.SetParent(goal, false);
            }
            BoxCollider zone = zoneTransform.GetComponent<BoxCollider>();
            if (zone == null) zone = zoneTransform.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, 1.1f, 0f);
            zone.size = new Vector3(4f, 2.5f, 2f);
            zones[index] = zone;
        }
        var settings = new SerializedObject(follow);
        settings.FindProperty("playerTarget").objectReferenceValue = player.transform;
        var goalProperty = settings.FindProperty("goalZones");
        goalProperty.arraySize = zones.Length;
        for (int i = 0; i < zones.Length; i++) goalProperty.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
        GameObject[] balls = roots.Where(root => root.name.StartsWith("Soccer Ball", StringComparison.Ordinal)).ToArray();
        if (balls.Length == 0) throw new InvalidOperationException("No soccer balls found.");
        foreach (GameObject ball in balls)
        {
            BallCameraTarget tracker = ball.GetComponent<BallCameraTarget>();
            if (tracker == null) tracker = ball.AddComponent<BallCameraTarget>();
            var trackerSettings = new SerializedObject(tracker);
            trackerSettings.FindProperty("followCamera").objectReferenceValue = follow;
            trackerSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(tracker);
        }
        follow.SnapToPlayer();
        Physics.SyncTransforms();
        Validate(follow, zones);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save soccer scene.");
        Directory.CreateDirectory("Logs");
        SoccerSceneLookSetup.SavePreview(camera, player, "Logs/group2-preview.png");
        File.WriteAllText("Logs/group2-result.txt", "PASS: player tracking, fixed camera height/rotation, smooth tracking, frame bounds, fast goal crossing, unrelated goal ignored, 2-second freeze, return, timeout, disabled/destroyed ball recovery.\n" + balls.Length + " ball trackers, 2 goal zones. Scene saved.\n");
        Debug.Log("GROUP2_CAMERA_PASS: camera configured and checks passed.");
    }

    private static void Validate(CameraFollow source, BoxCollider[] zones)
    {
        GameObject cameraObject = new GameObject("Camera validation");
        GameObject player = new GameObject("Player validation");
        GameObject ball = new GameObject("Ball validation");
        GameObject unrelated = new GameObject("Unrelated ball validation");
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.aspect = 16f / 9f;
            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            EditorUtility.CopySerialized(source, follow);
            var settings = new SerializedObject(follow);
            settings.FindProperty("playerTarget").objectReferenceValue = player.transform;
            settings.ApplyModifiedPropertiesWithoutUndo();
            player.transform.position = new Vector3(0f, 4.61f, 0f);
            follow.SnapToPlayer();
            Vector3 start = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            player.transform.position = new Vector3(8f, 4.61f, 3f);
            Tick(follow, 0.1f);
            Require(camera.transform.position.x > start.x && camera.transform.position.x < 8f, "Smooth player tracking");
            Require(Mathf.Abs(camera.transform.position.y - start.y) < 0.001f, "Fixed camera height");
            Require(Quaternion.Angle(rotation, camera.transform.rotation) < 0.001f, "Fixed camera angle");
            foreach (float aspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f, 3f })
            {
                camera.aspect = aspect;
                player.transform.position = new Vector3(100f, 4.61f, 100f);
                follow.SnapToPlayer();
                Vector3 focus = camera.transform.position + camera.transform.forward * 32f;
                float halfWidth = 8f * aspect;
                Require(halfWidth > 20f ? Mathf.Abs(focus.x) < 0.001f : focus.x + halfWidth <= 20.001f, "Aspect-aware X bounds");
                Require(focus.z + 8f / Mathf.Sin(50f * Mathf.Deg2Rad) <= 16.001f, "Z bounds");
            }
            camera.aspect = 16f / 9f;
            player.transform.position = new Vector3(0f, 4.61f, 0f);
            follow.SnapToPlayer();
            ball.transform.position = new Vector3(2f, 12f, 0f);
            follow.FollowBall(ball.transform);
            Tick(follow, 0.2f);
            Require(follow.State == CameraFollow.FollowState.Ball && camera.transform.position.x > 0f, "Ball tracking");
            Require(Mathf.Abs(camera.transform.position.y - start.y) < 0.001f, "Airborne ball does not lift camera");
            follow.NotifyGoal(unrelated.transform);
            Require(follow.State == CameraFollow.FollowState.Ball, "Ignore other ball's goal");

            // Cross the entire goal in a single tick, with both endpoints outside it.
            Bounds goal = zones[0].bounds;
            ball.transform.position = goal.center - Vector3.right * 5f;
            follow.FollowBall(ball.transform);
            ball.transform.position = goal.center + Vector3.right * 5f;
            Tick(follow, 0.016f);
            Require(follow.State == CameraFollow.FollowState.GoalPause, "Fast goal detection");
            Vector3 frozen = camera.transform.position;
            Tick(follow, 0f);
            Tick(follow, 1f);
            follow.NotifyGoal(ball.transform);
            follow.FollowBall(unrelated.transform);
            Tick(follow, 0.99f);
            Require(follow.State == CameraFollow.FollowState.GoalPause && camera.transform.position == frozen, "Freeze for two seconds");
            Tick(follow, 0.02f);
            Require(follow.State == CameraFollow.FollowState.Player && follow.CurrentBall == null, "Return after two seconds");
            ball.transform.position = new Vector3(0f, 5f, 0f);
            follow.FollowBall(ball.transform);
            Tick(follow, 8.01f);
            Require(follow.State == CameraFollow.FollowState.Player, "Missed-shot timeout");
            follow.FollowBall(ball.transform);
            ball.SetActive(false);
            Tick(follow, 0.016f);
            Require(follow.State == CameraFollow.FollowState.Player, "Disabled ball recovery");
            ball.SetActive(true);
            follow.FollowBall(ball.transform);
            UnityEngine.Object.DestroyImmediate(ball);
            Tick(follow, 0.016f);
            Require(follow.State == CameraFollow.FollowState.Player, "Destroyed ball recovery");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(player);
            if (ball != null) UnityEngine.Object.DestroyImmediate(ball);
            UnityEngine.Object.DestroyImmediate(unrelated);
        }
    }

    private static void Tick(CameraFollow follow, float deltaTime)
    {
        typeof(CameraFollow).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(follow, new object[] { deltaTime });
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException("Camera check failed: " + check);
    }
}
