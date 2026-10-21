using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AdventureCampaignInstaller
{
    const string SceneFolder = "Assets/Scenes/ThanhGiongWorld";
    [MenuItem("Thanh Giong/Adventure/Install Camera HUD and Atmosphere in Campaign")]
    public static string InstallAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing.");
        string original = SceneManager.GetActiveScene().path;
        string folder = Path.GetFullPath("AdventureVerification/Backups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        var report = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), true);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var actor = UnityEngine.Object.FindFirstObjectByType<ThanhGiongCampaignController>();
            var follow = UnityEngine.Object.FindFirstObjectByType<IsometricCameraFollow>();
            if (actor == null || follow == null) throw new InvalidOperationException("Missing campaign player or camera: " + path);
            if (actor.GetComponent<AdventureAimController>() == null) actor.gameObject.AddComponent<AdventureAimController>();
            if (actor.GetComponent<AdventureStamina>() == null) actor.gameObject.AddComponent<AdventureStamina>();
            follow.target = actor.transform;
            follow.useShoulderCamera = true;
            follow.useOrthographicIsometric = false;
            follow.shoulderDistance = 5.8f;
            follow.exploreFov = 80f;
            follow.aimingFov = 60f;
            follow.orbitPitch = 15f;
            follow.shoulderOffset = .85f;
            follow.GetComponent<Camera>().orthographic = false;
            follow.GetComponent<Camera>().fieldOfView = 80f;
            follow.SnapToTarget();
            // Install optional visual module by reflected type so this editor tool remains usable independently.
            Type visuals = typeof(AdventureAimController).Assembly.GetType("AdventureVisualBootstrap");
            if (visuals != null && actor.GetComponent(visuals) == null) actor.gameObject.AddComponent(visuals);
            if (visuals != null) {
                var visualModule = new SerializedObject(actor.GetComponent(visuals));
                visualModule.FindProperty("celShader").objectReferenceValue = Shader.Find("ThanhGiong/Adventure/Cel Rim Lit");
                visualModule.FindProperty("foliageShader").objectReferenceValue = Shader.Find("ThanhGiong/Adventure/Interactive Foliage");
                visualModule.FindProperty("particleShader").objectReferenceValue = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                visualModule.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(actor); EditorUtility.SetDirty(follow);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add("PASS " + scene.name + " shoulder=5.8m fov=80 aim=60 stamina=bound visuals=" + (visuals != null));
        }
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        report.Add("Backups: " + folder);
        Directory.CreateDirectory("AdventureVerification");
        File.WriteAllLines("AdventureVerification/campaign-install.txt", report);
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }
}
