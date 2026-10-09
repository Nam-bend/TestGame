using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class JammoGameplaySetup
{
    internal const string ScenePath = "Assets/Scenes/Location soccer field.unity";

    [MenuItem("Tools/Jammo/Setup Kick, Goal and UI")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up gameplay.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject player = roots.FirstOrDefault(root => root.name == "Player_Jammo");
        Camera camera = Camera.main;
        CameraFollow follow = camera != null ? camera.GetComponent<CameraFollow>() : null;
        if (player == null || follow == null) throw new InvalidOperationException("Set up Jammo and camera first.");
        ConfigurePlayerPhysics(player);
        JammoGroundingSetup.Align(player);
        GameObject effect = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effect/Prefabs/Confetti Explosion - Stars.prefab");
        if (effect == null) throw new InvalidOperationException("Confetti Explosion - Stars prefab missing.");
        Transform[] balls = roots.Where(root => root.name.StartsWith("Soccer Ball", StringComparison.Ordinal)).Select(root => root.transform).ToArray();
        Transform[] goalObjects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>())
            .Where(item => item.name == "soccer goal" || item.name == "soccer goal (1)").ToArray();
        if (balls.Length == 0 || goalObjects.Length != 2) throw new InvalidOperationException("Expected balls and two goals.");
        GoalTarget[] goals = new GoalTarget[goalObjects.Length];
        for (int i = 0; i < goals.Length; i++)
        {
            goals[i] = Component<GoalTarget>(goalObjects[i].gameObject);
            Transform aim = goalObjects[i].Find("ShotTarget");
            if (aim == null)
            {
                aim = new GameObject("ShotTarget").transform;
                aim.SetParent(goalObjects[i], false);
            }
            aim.localPosition = new Vector3(0f, 0.65f, 0f);
            Set(goals[i], "aimPoint", aim);
            Set(goals[i], "confettiPrefab", effect);
            var goalSettings = new SerializedObject(goals[i]);
            goalSettings.FindProperty("effectScale").floatValue = 1f;
            goalSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(goals[i]);
        }
        // Keep the initial nearby ball visible beside Jammo instead of overlapping his feet.
        foreach (Transform ball in balls)
        {
            Vector3 offset = ball.position - player.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.25f)
            {
                ball.position += new Vector3(1.3f, 0f, -1.2f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ball);
            }
        }

        GameObject gameplay = roots.FirstOrDefault(root => root.name == "Gameplay") ?? new GameObject("Gameplay");
        KickSystem kick = Component<KickSystem>(gameplay);
        AutoKickSystem autoKick = Component<AutoKickSystem>(gameplay);
        Set(kick, "player", player.transform);
        Set(kick, "followCamera", follow);
        Set(kick, "kickAnimation", player.GetComponentInChildren<JammoKickAnimation>());
        Set(kick, "playerMovement", player.GetComponent<PlayerController>());
        SetArray(kick, "balls", balls);
        SetArray(kick, "goals", goals);
        Set(autoKick, "kickSystem", kick);

        GameObject hud = roots.FirstOrDefault(root => root.name == "Soccer HUD")
            ?? new GameObject("Soccer HUD", typeof(RectTransform));
        Canvas canvas = Component<Canvas>(hud);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = Component<CanvasScaler>(hud);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Component<GraphicRaycaster>(hud);
        // Replace the previous custom HUD with Unity's stock buttons.
        foreach (string name in new[] { "Score Panel", "Status Panel", "Controls", "Reset", "Auto Kick", "Kick" })
        {
            Transform old = hud.transform.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        Button reset = StockButton(hud.transform, "Reset", Vector2.one, new Vector2(-20, -20));
        Button auto = StockButton(hud.transform, "Auto Kick", new Vector2(1, 0), new Vector2(-20, 20));
        Button nearby = StockButton(hud.transform, "Kick", new Vector2(1, 0), new Vector2(-20, 70));
        UIManager ui = Component<UIManager>(hud);
        Set(ui, "kickSystem", kick);
        Set(ui, "autoKickSystem", autoKick);

        Set(ui, "kickButton", nearby);
        Set(ui, "autoKickButton", auto);
        Set(ui, "resetButton", reset);


        ui.Refresh();

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var buildScenes = EditorBuildSettings.scenes.ToList();
        int index = buildScenes.FindIndex(entry => entry.path == ScenePath);
        if (index >= 0) buildScenes[index] = new EditorBuildSettingsScene(ScenePath, true);
        else buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        follow.SnapToPlayer();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save gameplay scene.");
        Directory.CreateDirectory("Logs");
        SoccerSceneLookSetup.SavePreview(camera, player, "Logs/gameplay-preview.png");
        Debug.Log("GAMEPLAY_SETUP_PASS: Kick, Auto Kick, Goal Stars and Reset saved to scene.");
    }

    internal static void ConfigurePlayerPhysics(GameObject player)
    {
        var oldBody = player.GetComponent<Rigidbody>();
        if (oldBody != null) UnityEngine.Object.DestroyImmediate(oldBody);
        var oldCapsule = player.GetComponent<CapsuleCollider>();
        if (oldCapsule != null) UnityEngine.Object.DestroyImmediate(oldCapsule);
        var capsule = Component<CharacterController>(player);
        capsule.height = 2.2f;
        capsule.radius = 0.35f;
        capsule.center = Vector3.up * 1.1f;
        capsule.skinWidth = 0.01f;
        capsule.minMoveDistance = 0f;
        capsule.stepOffset = 0.2f;
        var land = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .First(x => x.name == "land soccer field");
        var ground = Component<MeshCollider>(land.gameObject);
        ground.sharedMesh = land.sharedMesh;
        ground.isTrigger = false;
        // The prefab's bounding box sits above the actual grass surface.
        // Keep only the mesh as the solid ground, otherwise the controller floats.
        foreach (var box in land.GetComponents<BoxCollider>())
        {
            box.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(ground);
    }

    private static T Component<T>(GameObject owner) where T : UnityEngine.Component
    {
        T component = owner.GetComponent<T>();
        return component != null ? component : owner.AddComponent<T>();
    }

    private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
    {
        var settings = new SerializedObject(owner);
        settings.FindProperty(name).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(UnityEngine.Object owner, string name, UnityEngine.Object[] values)
    {
        var settings = new SerializedObject(owner);
        SerializedProperty array = settings.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button StockButton(Transform parent, string name, Vector2 anchor, Vector2 position)
    {
        var resources = new DefaultControls.Resources {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
        };
        GameObject item = DefaultControls.CreateButton(resources);
        item.name = name;
        item.transform.SetParent(parent, false);
        var rect = item.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(160, 40);
        item.GetComponentInChildren<Text>().text = name;
        var button = item.GetComponent<Button>();
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }
}
