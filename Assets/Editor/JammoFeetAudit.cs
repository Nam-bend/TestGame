using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Measures the rendered mesh as well as the controller; grounded alone is not a visual check.
[InitializeOnLoad]
public static class JammoFeetAudit
{
    private const string Key = "Jammo.FeetAudit";
    private static int sample;
    private static double deadline;
    static JammoFeetAudit()
    {
        if (SessionState.GetBool(Key, false)) BeginPolling();
    }

    public static void RunSavedScene()
    {
        EditorSceneManager.OpenScene(JammoGameplaySetup.ScenePath);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/feet-audit.txt", "Feet measurements in world metres\n");
        SessionState.SetBool(Key, true);
        BeginPolling();
        EditorApplication.EnterPlaymode();
    }

    public static void FixSavedGroundAndRun()
    {
        var scene = EditorSceneManager.OpenScene(JammoGameplaySetup.ScenePath);
        var land = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
            .First(x => x.name == "land soccer field");
        foreach (var box in land.GetComponents<BoxCollider>())
        {
            box.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }
        JammoGroundingSetup.Align(GameObject.Find("Player_Jammo"));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RunSavedScene();
    }

    private static void BeginPolling()
    {
        sample = 0;
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(1); return; }
        if (!EditorApplication.isPlaying || Time.time < 1f + sample) return;
        try
        {
            var player = GameObject.Find("Player_Jammo");
            var animator = player.GetComponentInChildren<Animator>();
            var controller = player.GetComponent<CharacterController>();
            var movement = player.GetComponent<PlayerController>();
            if (controller == null || !controller.enabled || movement == null || !movement.enabled)
                throw new InvalidOperationException("Player root must have enabled CharacterController and PlayerController.");
            if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null
                || new SerializedObject(movement).FindProperty("animator").objectReferenceValue != animator)
                throw new InvalidOperationException("Player Animator assignment is missing or incorrect.");
            var land = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .First(x => x.name == "land soccer field");
            if (!land.enabled || land.isTrigger || land.GetComponents<BoxCollider>().Any(c => c.enabled))
                throw new InvalidOperationException("Ground must use the mesh collider without the raised box collider.");
            if (!land.Raycast(new Ray(player.transform.position + Vector3.up * 10, Vector3.down), out var hit, 20))
                throw new InvalidOperationException("No grass surface under player.");
            if (!controller.isGrounded || Mathf.Abs(player.transform.position.y - hit.point.y) > 0.03f)
                throw new InvalidOperationException("Player is not grounded at actual grass height.");
            var text = new StringBuilder();
            if (sample == 0)
            {
                text.AppendLine("scene=" + player.scene.path);
                text.AppendLine("player=" + player.name + "; components=" + string.Join(",", player.GetComponents<Component>().Select(c => c.GetType().Name)));
                text.AppendLine("visual=" + AnimationUtility.CalculateTransformPath(animator.transform, player.transform)
                    + "; animator=" + animator.runtimeAnimatorController.name + "; movement Animator reference=assigned");
                text.AppendLine($"controller height={controller.height} radius={controller.radius} center={controller.center}; ground mesh enabled={land.enabled}");
            }
            text.AppendLine($"sample={sample} time={Time.time:F3} grounded={controller.isGrounded} playerY={player.transform.position.y:F5} groundY={hit.point.y:F5} visualLocal={animator.transform.localPosition:F5}");
            foreach (var contact in Physics.SphereCastAll(player.transform.position + Vector3.up, 0.34f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                text.AppendLine($"support={contact.collider.name} type={contact.collider.GetType().Name} point={contact.point:F5}");
            if (sample == 0)
                foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    text.AppendLine($"collider={collider.name} type={collider.GetType().Name} bounds={collider.bounds} trigger={collider.isTrigger}");
            foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.name == "feet_low"))
            {
                var mesh = new Mesh();
                renderer.BakeMesh(mesh, true);
                float lowest = mesh.vertices.Min(v => renderer.transform.TransformPoint(v).y);
                text.AppendLine($"mesh={renderer.name} lowestY={lowest:F5} gap={lowest-hit.point.y:F5}");
                UnityEngine.Object.DestroyImmediate(mesh);
                if (Mathf.Abs(lowest - hit.point.y) > 0.005f)
                    throw new InvalidOperationException("Idle sole differs from grass surface by more than 5 mm.");
            }
            foreach (var bone in player.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("ToeBase") || t.name.EndsWith("Foot")))
                text.AppendLine($"{bone.name} y={bone.position.y:F5} gap={bone.position.y-hit.point.y:F5}");
            text.AppendLine("clips=" + string.Join(",", animator.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name + ":" + c.weight)));
            File.AppendAllText("Logs/feet-audit.txt", text.ToString());
            if (sample == 1) SoccerSceneLookSetup.SavePreview(Camera.main, null, "Logs/feet-idle.png");
            if (++sample == 3)
            {
                File.AppendAllText("Logs/feet-audit.txt", "PASS: scene component assignments, actual grass contact and idle sole clearance in all three samples.\n");
                Finish(0);
            }
        }
        catch (Exception e)
        {
            File.AppendAllText("Logs/feet-audit.txt", e.ToString());
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.ExitPlaymode();
    }
}
