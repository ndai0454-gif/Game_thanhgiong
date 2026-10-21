using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ThanhGiongBuildRelease
{
    private static readonly string[] BuildScenes =
    {
        // Seamless open-world campaign (Docs/SEAMLESS_DAI_VIET_WORLD.md); matches the scene enabled in Build Settings.
        "Assets/Scenes/ThanhGiongWorld/ThanhGiongDaiViet.unity"
    };

    private const string OutputPath = "Builds/Windows/ThanhGiong.exe";

    [MenuItem("Tools/Thanh Giong/Build Release Player %#b")]
    public static void BuildReleaseMenu()
    {
        ExecuteBuild(false);
    }

    public static void BuildReleaseBatch()
    {
        ExecuteBuild(true);
    }

    private static void ExecuteBuild(bool isBatch)
    {
        Debug.Log("[ThanhGiong Release] Starting Standalone Windows 64 build pipeline...");

        // Ensure target directory exists
        string outputDir = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Configure branding and player settings
        PlayerSettings.productName = "ThanhGiong";
        PlayerSettings.companyName = "DaiStudio";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.defaultIsFullScreen = true;

        // Verify all scenes exist
        foreach (string scenePath in BuildScenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Debug.LogError($"[ThanhGiong Release] Missing scene: {scenePath}");
                if (isBatch) EditorApplication.Exit(2);
                return;
            }
        }

        // Prepare build options
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = BuildScenes,
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        DateTime startTime = DateTime.UtcNow;
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            Debug.Log($"<color=green>[ThanhGiong Release] BUILD SUCCEEDED in {duration.TotalSeconds:F1}s! Output: {OutputPath} ({summary.totalSize / (1024 * 1024):F1} MB)</color>");
            if (isBatch) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[ThanhGiong Release] BUILD FAILED with result: {summary.result}. Errors: {summary.totalErrors}, Warnings: {summary.totalWarnings}");
            if (isBatch) EditorApplication.Exit(1);
        }
    }
}
