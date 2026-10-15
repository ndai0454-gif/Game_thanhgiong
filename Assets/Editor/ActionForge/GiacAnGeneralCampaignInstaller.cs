using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThanhGiong.EditorTools
{
    public static class GiacAnGeneralCampaignInstaller
    {
        public const string ModelPath = "Assets/Prefabs/Enemies/GiacAnGeneral_Meshy.prefab";
        public const string MotionSetPath = "Assets/Resources/ActionForge/EnemyMotionSets/GiacAnGeneral.asset";
        public const string GameplayPrefabPath = "Assets/Prefabs/Enemies/GiacAnGeneral_Boss_An.prefab";
        const string LegacyPrefabPath = "Assets/Prefabs/Enemies/KayKit_Knight_Boss_An.prefab";
        const string ModelName = "GiacAnGeneral Model";

        public static bool ApplyBoss(ThanhGiongEnemy enemy)
        {
            if (enemy == null || !enemy.isBoss) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install boss art in Edit Mode.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var set = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(MotionSetPath);
            if (asset == null || set == null || set.boss == null) throw new InvalidOperationException("Build GiacAnGeneral combat assets before installing.");
            var presentation = enemy.GetComponent<LegendEnemyPresentation>() ?? enemy.gameObject.AddComponent<LegendEnemyPresentation>();
            var visual = enemy.transform.Find("Visual");
            if (visual == null) { visual = new GameObject("Visual").transform; visual.SetParent(enemy.transform, false); }
            var model = visual.Find(ModelName);
            if (model == null)
            {
                if (presentation.model != null && presentation.model != visual)
                {
                    presentation.model.name = "Previous Boss Model - Before Meshy";
                    presentation.model.gameObject.SetActive(false);
                }
                var old = visual.Find("Legend Enemy Model");
                if (old != null) { old.name = "Previous Boss Model - Before Meshy"; old.gameObject.SetActive(false); }
                model = ((GameObject)PrefabUtility.InstantiatePrefab(asset, visual)).transform;
                model.name = ModelName;
            }
            var capsule = enemy.GetComponent<CapsuleCollider>();
            float height = capsule != null ? capsule.height : 3.6f;
            float bottom = capsule != null ? capsule.center.y - capsule.height * .5f : -1.8f;
            float artHeight = visual.InverseTransformVector(enemy.transform.TransformVector(Vector3.up * height)).magnitude;
            model.localScale = Vector3.one * (artHeight / 2.15f);
            model.localRotation = Quaternion.identity;
            model.position = enemy.transform.TransformPoint(new Vector3(0, bottom, 0)) + Vector3.up * .03f;
            model.gameObject.SetActive(true);
            var animator = model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = set.boss;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AddSwordTip(model, animator);
            var hidden = new List<Renderer>();
            foreach (var r in enemy.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(model) || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r.GetComponent<TextMesh>() != null) continue;
                r.enabled = false; hidden.Add(r);
            }
            presentation.model = model;
            presentation.sourceAsset = ModelPath;
            presentation.placeholders = hidden.ToArray();
            presentation.preserveAuthoredMaterials = true;
            presentation.modelOriginIsFeet = true;
            var driver = enemy.GetComponent<ThanhGiongEnemyAnimationDriver>() ?? enemy.gameObject.AddComponent<ThanhGiongEnemyAnimationDriver>();
            driver.actionForgeMotions = set;
            if (enemy.GetComponent<ActionForgeEnemyEffects>() == null) enemy.gameObject.AddComponent<ActionForgeEnemyEffects>();
            EditorUtility.SetDirty(enemy); EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(driver);
            return true;
        }

        [MenuItem("Tools/Thánh Gióng/ActionForge/Replace Campaign Bosses With Giac An General")]
        public static void InstallMenu() { Debug.Log(InstallAll()); }

        public static string InstallAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save existing scene edits before installing.");
            string previous = SceneManager.GetActiveScene().path;
            string backup = "GiacAnGeneralVerification/CampaignBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var report = new List<string> { "Boss replacement: " + DateTime.UtcNow.ToString("O"), "Backups: " + backup };
            int total = 0;
            try
            {
                var importer = AssetImporter.GetAtPath(GiacAnGeneralBuilder.SourcePath) as ModelImporter;
                if (importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
                if (File.Exists(LegacyPrefabPath))
                {
                    Backup(LegacyPrefabPath, backup);
                    var root = PrefabUtility.LoadPrefabContents(LegacyPrefabPath);
                    try
                    {
                        foreach (var enemy in root.GetComponentsInChildren<ThanhGiongEnemy>(true)) if (enemy.isBoss) ApplyBoss(enemy);
                        PrefabUtility.SaveAsPrefabAsset(root, LegacyPrefabPath);
                        PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefabPath);
                        report.Add("Gameplay prefab updated: " + LegacyPrefabPath + " and " + GameplayPrefabPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/ThanhGiongWorld" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var bosses = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ThanhGiongEnemy>(true)).Where(e => e.isBoss).ToArray();
                    report.Add(path + " | bosses=" + bosses.Length);
                    if (bosses.Length == 0) continue;
                    Backup(path, backup);
                    foreach (var enemy in bosses) { ApplyBoss(enemy); VerifyBoss(enemy); total++; }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unable to save " + path);
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single); }
            report.Add("Total campaign bosses replaced=" + total);
            Directory.CreateDirectory("GiacAnGeneralVerification");
            File.WriteAllLines("GiacAnGeneralVerification/campaign-install.txt", report);
            return string.Join("\n", report);
        }

        public static string VerifyAllScenes()
        {
            string previous = SceneManager.GetActiveScene().path;
            var report = new List<string>();
            try
            {
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/ThanhGiongWorld" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var bosses = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ThanhGiongEnemy>(true)).Where(e => e.isBoss).ToArray();
                    foreach (var e in bosses) VerifyBoss(e);
                    report.Add(path + " | bosses=" + bosses.Length + " | PASS");
                }
            }
            finally { EditorSceneManager.OpenScene(previous, OpenSceneMode.Single); }
            File.WriteAllLines("GiacAnGeneralVerification/campaign-scene-verification.txt", report);
            return string.Join("\n", report);
        }

        static void VerifyBoss(ThanhGiongEnemy enemy)
        {
            var p = enemy.GetComponent<LegendEnemyPresentation>();
            var d = enemy.GetComponent<ThanhGiongEnemyAnimationDriver>();
            var a = p != null && p.model != null ? p.model.GetComponentInChildren<Animator>(true) : null;
            if (p == null || p.sourceAsset != ModelPath || !p.preserveAuthoredMaterials || !p.modelOriginIsFeet || a == null ||
                d == null || d.actionForgeMotions == null || a.runtimeAnimatorController != d.actionForgeMotions.boss || a.applyRootMotion)
                throw new InvalidOperationException("Boss installation invalid: " + enemy.name);
            if (p.model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.bones.Any(b => b == null)))
                throw new InvalidOperationException("Missing boss skeleton bindings.");
        }

        static void AddSwordTip(Transform model, Animator animator)
        {
            var hand = animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "hand_r");
            if (hand == null) throw new InvalidOperationException("New boss has no right hand.");
            var marker = hand.Find("ActionForge Weapon Tip");
            if (marker == null) { marker = new GameObject("ActionForge Weapon Tip").transform; marker.SetParent(hand, false); }
            Vector3 tip = Vector3.zero; float farthest = 0;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int bone = Array.IndexOf(skin.bones, hand);
                if (bone < 0) continue;
                var mesh = skin.sharedMesh;
                var vertices = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes[bone];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    bool rigid = (w.boneIndex0 == bone && w.weight0 > .95f) || (w.boneIndex1 == bone && w.weight1 > .95f) ||
                        (w.boneIndex2 == bone && w.weight2 > .95f) || (w.boneIndex3 == bone && w.weight3 > .95f);
                    if (!rigid) continue;
                    var p = bind.MultiplyPoint3x4(vertices[i]);
                    if (p.sqrMagnitude > farthest) { farthest = p.sqrMagnitude; tip = p; }
                }
            }
            if (farthest <= .00001f) throw new InvalidOperationException("Unable to locate weighted sword tip.");
            marker.localPosition = tip;
        }

        static void Backup(string path, string root)
        {
            string target = root + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(path, target, true);
        }
    }
}
