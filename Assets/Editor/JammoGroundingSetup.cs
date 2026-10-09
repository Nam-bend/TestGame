using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class JammoGroundingSetup
{
    private const string Request = "Logs/jammo-grounding.request";
    static JammoGroundingSetup() { EditorApplication.update += ApplyPending; }

    private static void ApplyPending()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().isDirty) return;
        File.Delete(Request);
        EditorApplication.update -= ApplyPending;
        Apply();
    }

    [MenuItem("Tools/Jammo/Align Feet to Pitch")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        var player = scene.GetRootGameObjects().First(x => x.name == "Player_Jammo");
        JammoGameplaySetup.ConfigurePlayerPhysics(player);
        Align(player);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save grounded player.");
        SoccerSceneLookSetup.SavePreview(Camera.main, player, "Logs/jammo-grounded-preview.png");
    }

    internal static void Align(GameObject player)
    {
        var land = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .First(x => x.name == "land soccer field");
        var probe = new GameObject("Pitch height probe");
        float ground;
        try
        {
            probe.transform.SetPositionAndRotation(land.transform.position, land.transform.rotation);
            probe.transform.localScale = land.transform.lossyScale;
            var collider = probe.AddComponent<MeshCollider>();
            collider.sharedMesh = land.sharedMesh;
            Physics.SyncTransforms();
            var ray = new Ray(player.transform.position + Vector3.up * 100, Vector3.down);
            if (!collider.Raycast(ray, out var hit, 200)) throw new InvalidOperationException("Pitch surface not found below Jammo.");
            ground = hit.point.y;
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); }

        var animator = player.GetComponentInChildren<Animator>();
        var idle = animator.runtimeAnimatorController.animationClips.First(x => x.name == "a_Idle");
        float sole = SampleSoles(animator, idle);

        float oldHeight = player.transform.position.y;
        var controller = player.GetComponent<CharacterController>();
        // Move() keeps this contact margin above the floor in Play Mode.
        float contactMargin = controller != null ? controller.skinWidth * player.transform.lossyScale.y : 0f;
        float visualOffset = sole - oldHeight + contactMargin;
        Undo.RecordObjects(new UnityEngine.Object[] { player.transform, animator.transform }, "Align Jammo feet");
        player.transform.position = new Vector3(player.transform.position.x, ground, player.transform.position.z);
        animator.transform.position -= Vector3.up * visualOffset;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator.transform);
        float corrected = SampleSoles(animator, idle);
        if (Mathf.Abs(corrected + contactMargin - ground) > 0.001f) throw new InvalidOperationException("Foot alignment failed.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/jammo-grounding.txt", $"PASS: pitch={ground:F4}, previous soles={sole:F4}, previous gap={sole-ground:F4}, corrected soles={corrected:F4}\n");
        Debug.Log(File.ReadAllText("Logs/jammo-grounding.txt"));
    }

    private static float SampleSoles(Animator animator, AnimationClip clip)
    {
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, 0);
            AnimationMode.EndSampling();
            return LowestVertex(animator.gameObject);
        }
        finally { AnimationMode.StopAnimationMode(); }
    }

    private static float LowestVertex(GameObject model)
    {
        var feet = model.GetComponentsInChildren<SkinnedMeshRenderer>()
            .Where(r => r.name == "feet_low" || r.name == "heels_low").ToArray();
        if (feet.Length == 0) throw new InvalidOperationException("Jammo foot meshes missing.");
        float lowest = float.PositiveInfinity;
        var mesh = new Mesh();
        try
        {
            foreach (var foot in feet)
            {
                // Compensate renderer scale so TransformPoint yields world positions.
                foot.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices)
                    lowest = Mathf.Min(lowest, foot.transform.TransformPoint(vertex).y);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
        return lowest;
    }
}
