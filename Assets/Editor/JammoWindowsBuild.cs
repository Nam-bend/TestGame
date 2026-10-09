using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class JammoWindowsBuild
{
    [MenuItem("Tools/Jammo/Build Windows")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before building.");
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        string output = Environment.GetEnvironmentVariable("JAMMO_BUILD_OUTPUT");
        if (string.IsNullOrEmpty(output)) output = "Builds/Windows/JammoFootball.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { JammoGameplaySetup.ScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/windows-build.txt", $"{report.summary.result}: {output}\nBytes: {report.summary.totalSize}\nErrors: {report.summary.totalErrors}\n");
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed: " + report.summary.result);
    }
}
