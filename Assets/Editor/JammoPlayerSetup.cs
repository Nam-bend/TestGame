using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class JammoPlayerSetup
{
    private const string ScenePath = "Assets/Scenes/Location soccer field.unity";
    private const string ModelPath = "Assets/Jammo-Character/Models/Jammo_LowPoly.fbx";
    private const string ControllerPath = "Assets/Jammo-Character/Animations/AnimatorController_Jamo.controller";

    [MenuItem("Tools/Jammo/Setup Group 1")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up Jammo.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (model == null || controller == null)
            throw new InvalidOperationException("Jammo model or Animator Controller is missing.");

        GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player_Jammo");
        if (player == null)
        {
            player = new GameObject("Player_Jammo");
            Undo.RegisterCreatedObjectUndo(player, "Create Jammo player");
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            visual.transform.SetParent(player.transform, false);
            visual.name = "Jammo_Visual";

            // Keep Jammo visible at the scale of this field, with feet at the root.
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException("Jammo model has no renderer.");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y > 0.001f)
                visual.transform.localScale *= 2.2f / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.localPosition -= Vector3.up * bounds.min.y;
        }

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null) animator = player.transform.GetChild(0).gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        if (animator.avatar == null)
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

        // The goal mouths are at X = +/-12.863. Keep the body inside the pitch.
        // The land mesh includes the surrounding landscape; its 45m bounds are not the pitch.
        player.transform.position = new Vector3(0f, 4.61f, 0f);
        player.transform.rotation = Quaternion.identity;
        player.tag = "Player";
        PlayerController movement = player.GetComponent<PlayerController>();
        if (movement == null) movement = Undo.AddComponent<PlayerController>(player);
        SerializedObject settings = new SerializedObject(movement);
        settings.FindProperty("animator").objectReferenceValue = animator;
        settings.FindProperty("moveSpeed").floatValue = 5f;
        settings.FindProperty("turnSpeed").floatValue = 720f;
        settings.FindProperty("speedParameter").stringValue = "Blend";
        settings.FindProperty("runBlendValue").floatValue = 0.6f;
        settings.FindProperty("xLimits").vector2Value = new Vector2(-12f, 12f);
        settings.FindProperty("zLimits").vector2Value = new Vector2(-8.3f, 8.3f);
        settings.ApplyModifiedPropertiesWithoutUndo();

        JammoGameplaySetup.ConfigurePlayerPhysics(player);
        JammoGroundingSetup.Align(player);
        Validate(player, animator, movement);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Could not save the soccer scene.");
        Selection.activeGameObject = player;
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/group1-result.txt", "PASS: scene saved; one Jammo player; Idle/Run animation bindings; Blend float; X/Z clamp checks.\nPosition=" + player.transform.position + "\nX=[-12,12], Z=[-8.3,8.3]\n");
        SavePreview();
        Debug.Log("GROUP1_SETUP_PASS: Jammo is ready in " + ScenePath);
    }

    private static void Validate(GameObject player, Animator animator, PlayerController movement)
    {
        // This asset uses a Generic rig and transform curves, not Humanoid retargeting.
        if (!animator.parameters.Any(p => p.name == "Blend" && p.type == AnimatorControllerParameterType.Float))
            throw new InvalidOperationException("Missing Blend Float parameter.");
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (!clips.Any(c => c.name == "a_Idle") || !clips.Any(c => c.name == "a_Running"))
            throw new InvalidOperationException("Missing Idle or Run animation.");
        foreach (AnimationClip clip in clips.Where(c => c.name == "a_Idle" || c.name == "a_Running"))
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!string.IsNullOrEmpty(binding.path) && animator.transform.Find(binding.path) == null)
                    throw new InvalidOperationException("Animation bone missing: " + binding.path);
            }
        }
        MethodInfo clamp = typeof(PlayerController).GetMethod("ClampToField", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (Vector3 input in new[] { new Vector3(-100, 7, -100), new Vector3(100, 7, 100), new Vector3(0, 7, 0) })
        {
            Vector3 actual = (Vector3)clamp.Invoke(movement, new object[] { input });
            Vector3 expected = new Vector3(Mathf.Clamp(input.x, -12f, 12f), 7f, Mathf.Clamp(input.z, -8.3f, 8.3f));
            if (actual != expected) throw new InvalidOperationException("Player boundary check failed.");
        }
        if (player.GetComponentsInChildren<PlayerController>().Length != 1)
            throw new InvalidOperationException("Duplicate PlayerControllers found.");
    }

    private static void SavePreview()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/group1-preview.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
