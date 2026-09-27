using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Taiyaki.EditorTools
{
    /// <summary>
    /// 빌드 메뉴 / 배치 빌드.
    /// - 에디터: Taiyaki/Build/Windows (x64), Taiyaki/Build/Android (APK)
    /// - 배치:  Unity -batchmode -quit -projectPath . -executeMethod Taiyaki.EditorTools.BuildScript.BuildAndroid -logFile build.log
    /// 결과물은 Builds/ 아래 (gitignore 대상).
    /// </summary>
    public static class BuildScript
    {
        const string ExeName = "TaiyakiRomance";

        [MenuItem("Taiyaki/Build/Windows (x64)")]
        public static void BuildWindows()
        {
            PlayerSettings.resizableWindow = true;
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Win64/" + ExeName + ".exe");
        }

        [MenuItem("Taiyaki/Build/Android (APK)")]
        public static void BuildAndroid()
        {
            // 640×360 가로 전용 게임 — 가로 두 방향만 회전 허용
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) == "com.Company.ProductName")
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.taiyakiromance.prototype");
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/" + ExeName + ".apk");
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { TaiyakiMenu.MainScene },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string msg = string.Format("[Build] {0} {1} — {2}, {3:0.0} MB, {4:0}초", target, s.result, Path.GetFullPath(path), (File.Exists(path) ? new FileInfo(path).Length : (long)s.totalSize) / 1048576.0, s.totalTime.TotalSeconds);
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError(msg + " (에러 " + s.totalErrors + ")");
                if (Application.isBatchMode) throw new Exception(msg);
                return;
            }
            Debug.Log(msg);
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(path);
        }
    }
}
