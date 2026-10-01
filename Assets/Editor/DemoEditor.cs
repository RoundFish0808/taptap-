using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReverseDefense.Editor
{
    public static class DemoEditor
    {
        [MenuItem("演示/逆塔防/打开并运行")]
        public static void PlayDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/ReverseDefenseDemo.unity");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("演示/逆塔防/构建 Windows 演示")]
        public static void BuildDemo()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-demo-build-output");
            string target = index >= 0 && index + 1 < args.Length ? args[index + 1] : "Builds/ReverseDefenseDemo/逆塔防演示.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/ReverseDefenseDemo.unity" }, locationPathName = target,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("演示构建失败：" + report.summary.result + "，错误数：" + report.summary.totalErrors);
            Debug.Log("逆塔防演示构建成功：" + target);
        }
    }
}
