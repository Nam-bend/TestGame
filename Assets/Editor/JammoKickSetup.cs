using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JammoKickSetup
{
    private const string Folder = "Assets/Jammo-Character/Animations/";

    [MenuItem("Tools/Jammo/Setup Kick Animation")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(JammoGameplaySetup.ScenePath);
        var player = scene.GetRootGameObjects().First(x => x.name == "Player_Jammo");
        var animator = player.GetComponentInChildren<Animator>();
        // Reuse the original running motion without creating or modifying any clip.
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Default/a_Running.anim");
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(x => x.state).FirstOrDefault(x => x.name == "Kick") ?? machine.AddState("Kick");
        state.motion = clip;
        foreach (var transition in state.transitions) state.RemoveTransition(transition);
        var exit = state.AddTransition(machine.states.First(x => x.state.name == "NormalStatus").state);
        exit.hasExitTime = true;
        exit.exitTime = 0.95f;
        exit.duration = 0.08f;
        exit.hasFixedDuration = true;
        var receiver = animator.GetComponent<JammoKickAnimation>() ?? animator.gameObject.AddComponent<JammoKickAnimation>();
        Set(receiver, "movement", player.GetComponent<PlayerController>());
        var kick = UnityEngine.Object.FindFirstObjectByType<KickSystem>();
        Set(kick, "kickAnimation", receiver);
        Set(kick, "playerMovement", player.GetComponent<PlayerController>());

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save scene.");
        AssetDatabase.DeleteAsset(Folder + "Kick.anim");
        Directory.CreateDirectory("Logs");
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { Folder.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
        File.WriteAllLines("Logs/jammo-animation-inventory.txt", clips);
        Debug.Log("KICK_SETUP_PASS: " + clips.Length + " clip assets; " + controller.animationClips.Distinct().Count() + " controller clips.");
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
