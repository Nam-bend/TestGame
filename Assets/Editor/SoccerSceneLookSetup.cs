using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SoccerSceneLookSetup
{
    private const string ScenePath = "Assets/Scenes/Location soccer field.unity";

    [MenuItem("Tools/Jammo/Improve Camera")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before changing the scene.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = Camera.main;
        Light sun = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>())
            .FirstOrDefault(light => light.type == LightType.Directional);
        if (camera == null || sun == null)
            throw new InvalidOperationException("The scene needs a Main Camera and a Directional Light.");

        // A pitched orthographic view keeps both goals visible without perspective distortion.
        Vector3 focus = new Vector3(0f, 4.5f, 0f);
        camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
        camera.transform.position = focus - camera.transform.forward * 34f;
        camera.orthographic = true;
        camera.orthographicSize = 12.5f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 150f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.65f, 0.76f, 0.81f);
        camera.allowMSAA = true;

        // Preserve the scene's original lighting settings.

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the soccer scene.");
        Directory.CreateDirectory("Logs");
        SavePreview(camera, scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player_Jammo"));
        File.WriteAllText("Logs/scene-look-result.txt", "PASS: scene saved; camera tilt 58 degrees, orthographic size 12.5; original scene lighting preserved.\n");
        Debug.Log("SCENE_LOOK_PASS: camera saved; lighting preserved.");
    }

    internal static void SavePreview(Camera camera, GameObject player, string outputPath = "Logs/scene-look-preview.png")
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        RenderTexture target = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        bool sampling = false;
        try
        {
            Animator animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                AnimationClip idle = animator.runtimeAnimatorController.animationClips.FirstOrDefault(clip => clip.name == "a_Idle");
                if (idle != null)
                {
                    AnimationMode.StartAnimationMode();
                    sampling = true;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, idle, 0f);
                    AnimationMode.EndSampling();
                }
            }
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
        }
        finally
        {
            if (sampling) AnimationMode.StopAnimationMode();
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
