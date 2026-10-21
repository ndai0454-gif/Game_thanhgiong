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
    /// <summary>
    /// Installs the Meshy AI + ActionForge Qin Soldier (Lính Tần) across all enemy prefabs,
    /// the 6 campaign scenes, and the seamless open-world scene (ThanhGiongDaiViet.unity).
    /// </summary>
    public static class QinSoldierCampaignInstaller
    {
        public const string ModelPath = QinSoldierBuilder.VisualPrefabPath;
        public const string MotionSetPath = QinSoldierBuilder.MotionSetPath;
        public const string GameplayPrefabPath = QinSoldierBuilder.GameplayPrefabPath;
        public const string LegacyPrefabPath = QinSoldierBuilder.LegacySoldierPrefabPath;
        public const string ModelName = "QinSoldier Model";

        [MenuItem("Tools/Thánh Gióng/ActionForge/Build and Install All Qin Soldiers (Lính Tần)")]
        public static void BuildAndInstallAllMenu()
        {
            string buildReport = QinSoldierBuilder.Build();
            string installReport = InstallAll();
            string verifyReport = VerifyAllScenes();
            Debug.Log(buildReport + "\n\n" + installReport + "\n\n" + verifyReport);
        }

        [MenuItem("Tools/Thánh Gióng/ActionForge/Replace Campaign Soldiers With Qin Soldiers (Lính Tần)")]
        public static void InstallMenu() => Debug.Log(InstallAll());

        public static bool ApplySoldier(ThanhGiongEnemy enemy)
        {
            if (enemy == null || enemy.isBoss) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install Qin Soldier art in Edit Mode.");

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var set = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(MotionSetPath);
            if (asset == null || set == null || set.spearman == null)
                throw new InvalidOperationException("Build QinSoldier assets first via QinSoldierBuilder.Build().");

            var shieldMat = AssetDatabase.LoadAssetAtPath<Material>(QinSoldierBuilder.ShieldMaterialPath);
            var bronzeMat = AssetDatabase.LoadAssetAtPath<Material>(QinSoldierBuilder.BronzeMaterialPath);
            var tasselMat = AssetDatabase.LoadAssetAtPath<Material>(QinSoldierBuilder.TasselMaterialPath);

            // Unpack scene prefab instances so legacy prefab overrides cannot duplicate "Visual" children
            if (enemy.gameObject.scene.IsValid() && PrefabUtility.IsPartOfPrefabInstance(enemy.gameObject))
            {
                var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(enemy.gameObject);
                if (outermost != null)
                {
                    PrefabUtility.UnpackPrefabInstance(outermost, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
            }

            if (enemy.name.IndexOf("Barbarian", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enemy.name = enemy.name.Replace("Barbarian", "QinSoldier").Replace("barbarian", "QinSoldier");
            }

            ThanhGiongEnemy.EnemyRole effectiveRole = ResolveSoldierRole(enemy);
            enemy.role = effectiveRole;
            UpdateSoldierDisplayName(enemy, effectiveRole);

            var presentation = enemy.GetComponent<LegendEnemyPresentation>() ?? enemy.gameObject.AddComponent<LegendEnemyPresentation>();

            // Remove legacy Barbarian visuals and duplicate Visual containers on the enemy root
            Transform primaryVisual = null;
            for (int i = enemy.transform.childCount - 1; i >= 0; i--)
            {
                var child = enemy.transform.GetChild(i);
                if (child == null) continue;
                if (child.name == "Visual")
                {
                    if (primaryVisual == null)
                    {
                        primaryVisual = child;
                    }
                    else
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }
                else if (child.name.IndexOf("Barbarian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         child.name.IndexOf("Previous", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         child.name == "Legend Enemy Model")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            if (primaryVisual == null)
            {
                primaryVisual = new GameObject("Visual").transform;
                primaryVisual.SetParent(enemy.transform, false);
            }
            primaryVisual.gameObject.SetActive(true);

            // Clean all legacy models inside the primary Visual container
            for (int i = primaryVisual.childCount - 1; i >= 0; i--)
            {
                var child = primaryVisual.GetChild(i);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(asset, primaryVisual);
            PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            modelInstance.name = ModelName;
            Transform model = modelInstance.transform;

            var capsule = enemy.GetComponent<CapsuleCollider>();
            float height = capsule != null ? capsule.height : 2.0f;
            float bottom = capsule != null ? capsule.center.y - capsule.height * 0.5f : -1.0f;
            float artHeight = primaryVisual.InverseTransformVector(enemy.transform.TransformVector(Vector3.up * height)).magnitude;

            model.localScale = Vector3.one * (artHeight / 1.85f);
            model.localRotation = Quaternion.identity;
            model.position = enemy.transform.TransformPoint(new Vector3(0f, bottom, 0f)) + Vector3.up * 0.03f;
            model.gameObject.SetActive(true);

            var visualChild = model.Find("QinSoldier_Visual") ?? model;
            if (shieldMat != null && bronzeMat != null && tasselMat != null)
            {
                QinSoldierBuilder.AttachRoleEquipment(visualChild, effectiveRole, shieldMat, bronzeMat, tasselMat);
            }

            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
                throw new InvalidOperationException("QinSoldier prefab has no Animator component.");

            animator.runtimeAnimatorController = set.For(effectiveRole, false);
            animator.avatar = null;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;

            var hidden = new List<Renderer>();
            foreach (var r in enemy.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(model) ||
                    r is ParticleSystemRenderer ||
                    r is LineRenderer ||
                    r is TrailRenderer ||
                    r.GetComponent<TextMesh>() != null)
                {
                    continue;
                }
                r.enabled = false;
                hidden.Add(r);
            }

            presentation.model = model;
            presentation.sourceAsset = ModelPath;
            presentation.placeholders = hidden.ToArray();
            presentation.preserveAuthoredMaterials = true;
            presentation.modelOriginIsFeet = true;

            var driver = enemy.GetComponent<ThanhGiongEnemyAnimationDriver>() ?? enemy.gameObject.AddComponent<ThanhGiongEnemyAnimationDriver>();
            driver.actionForgeMotions = set;
            driver.RebindAnimator();

            var effects = enemy.GetComponent<ActionForgeEnemyEffects>() ?? enemy.gameObject.AddComponent<ActionForgeEnemyEffects>();

            EditorUtility.SetDirty(enemy);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(effects);
            return true;
        }

        private static ThanhGiongEnemy.EnemyRole ResolveSoldierRole(ThanhGiongEnemy enemy)
        {
            if (enemy.role != ThanhGiongEnemy.EnemyRole.Auto)
                return enemy.role;

            string combined = ((enemy.enemyName ?? "") + " " + (enemy.name ?? "")).ToLowerInvariant();
            if (combined.Contains("khiên") || combined.Contains("shield") || combined.Contains("commander") || combined.Contains("thủ lĩnh") || combined.Contains("đội trưởng"))
                return ThanhGiongEnemy.EnemyRole.ShieldBearer;
            if (combined.Contains("cung") || combined.Contains("archer") || combined.Contains("bow") || combined.Contains("xạ"))
                return ThanhGiongEnemy.EnemyRole.Archer;
            if (combined.Contains("kỵ") || combined.Contains("raider") || combined.Contains("đột kích") || combined.Contains("do thám"))
                return ThanhGiongEnemy.EnemyRole.Raider;
            if (combined.Contains("giáo") || combined.Contains("spear") || combined.Contains("thương"))
                return ThanhGiongEnemy.EnemyRole.Spearman;

            int slot = enemy.transform.GetSiblingIndex();
            return (slot % 6) switch
            {
                0 => ThanhGiongEnemy.EnemyRole.ShieldBearer,
                2 => ThanhGiongEnemy.EnemyRole.Archer,
                4 => ThanhGiongEnemy.EnemyRole.Raider,
                _ => ThanhGiongEnemy.EnemyRole.Spearman
            };
        }

        private static void UpdateSoldierDisplayName(ThanhGiongEnemy enemy, ThanhGiongEnemy.EnemyRole role)
        {
            string current = enemy.enemyName ?? "";
            if (current.IndexOf("Thủ Lĩnh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                enemy.name.IndexOf("Commander", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enemy.enemyName = "Đội Trưởng Quân Tần";
                return;
            }

            if (string.IsNullOrWhiteSpace(current) ||
                current.IndexOf("Barbarian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                current.IndexOf("Binh Ân", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enemy.enemyName = role switch
                {
                    ThanhGiongEnemy.EnemyRole.ShieldBearer => "Khiên Binh Quân Tần",
                    ThanhGiongEnemy.EnemyRole.Archer => "Cung Binh Quân Tần",
                    ThanhGiongEnemy.EnemyRole.Raider => "Đột Kích Binh Quân Tần",
                    _ => "Trường Thương Binh Quân Tần"
                };
            }
        }

        public static string InstallAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing Qin Soldiers.");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var openScene = SceneManager.GetSceneAt(i);
                if (openScene.isDirty)
                    EditorSceneManager.SaveScene(openScene);
            }

            string previous = SceneManager.GetActiveScene().path;
            string backup = "QinSoldierVerification/CampaignBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var report = new List<string>
            {
                "=== QIN SOLDIER (LÍNH TẦN) CAMPAIGN & OPEN-WORLD INSTALLATION ===",
                "Timestamp: " + DateTime.UtcNow.ToString("O"),
                "Backups: " + backup
            };

            int totalSoldiers = 0;
            try
            {
                if (File.Exists(LegacyPrefabPath))
                {
                    Backup(LegacyPrefabPath, backup);
                    var root = PrefabUtility.LoadPrefabContents(LegacyPrefabPath);
                    try
                    {
                        foreach (var enemy in root.GetComponentsInChildren<ThanhGiongEnemy>(true))
                        {
                            if (!enemy.isBoss)
                            {
                                enemy.role = ThanhGiongEnemy.EnemyRole.Spearman;
                                ApplySoldier(enemy);
                            }
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, LegacyPrefabPath);
                        report.Add("PASS Updated legacy soldier prefab in-place: " + LegacyPrefabPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }

                var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/ThanhGiongWorld" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(p => p)
                    .ToArray();

                foreach (var path in scenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var soldiers = scene.GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<ThanhGiongEnemy>(true))
                        .Where(e => !e.isBoss)
                        .ToArray();

                    foreach (var rootGo in scene.GetRootGameObjects())
                    {
                        foreach (var tr in rootGo.GetComponentsInChildren<Transform>(true))
                        {
                            if (tr.name == "DOI QUAN BARBARIAN")
                                tr.name = "DOI QUAN TAN (QIN ARMY)";
                        }
                    }

                    report.Add($"{path} | soldiers={soldiers.Length}");
                    if (soldiers.Length == 0) continue;

                    Backup(path, backup);
                    foreach (var enemy in soldiers)
                    {
                        ApplySoldier(enemy);
                        VerifySoldier(enemy);
                        totalSoldiers++;
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Unable to save scene: " + path);
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                    EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }

            report.Add("Total campaign & open-world Qin Soldiers replaced = " + totalSoldiers);
            Directory.CreateDirectory("QinSoldierVerification");
            File.WriteAllLines("QinSoldierVerification/campaign-install.txt", report);
            return string.Join("\n", report);
        }

        public static string VerifyAllScenes()
        {
            string previous = SceneManager.GetActiveScene().path;
            var report = new List<string>
            {
                "=== QIN SOLDIER SCENE VERIFICATION ==="
            };
            int verifiedCount = 0;
            try
            {
                var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/ThanhGiongWorld" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(p => p)
                    .ToArray();

                foreach (var path in scenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var soldiers = scene.GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<ThanhGiongEnemy>(true))
                        .Where(e => !e.isBoss)
                        .ToArray();

                    foreach (var s in soldiers)
                    {
                        VerifySoldier(s);
                        verifiedCount++;
                    }
                    report.Add($"{path} | soldiers={soldiers.Length} | PASS");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                    EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }

            report.Add($"Total verified Qin Soldiers across all scenes = {verifiedCount} | ALL PASS");
            Directory.CreateDirectory("QinSoldierVerification");
            File.WriteAllLines("QinSoldierVerification/campaign-scene-verification.txt", report);
            return string.Join("\n", report);
        }

        public static void VerifySoldier(ThanhGiongEnemy enemy)
        {
            var p = enemy.GetComponent<LegendEnemyPresentation>();
            var d = enemy.GetComponent<ThanhGiongEnemyAnimationDriver>();
            var a = p != null && p.model != null ? p.model.GetComponentInChildren<Animator>(true) : null;
            if (p == null || p.sourceAsset != ModelPath || !p.preserveAuthoredMaterials || !p.modelOriginIsFeet ||
                a == null || d == null || d.actionForgeMotions == null || a.runtimeAnimatorController == null || a.applyRootMotion)
            {
                throw new InvalidOperationException("Qin Soldier installation invalid on: " + enemy.name);
            }

            var skins = p.model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0 || skins.Any(r => r.sharedMesh == null || r.bones.Any(b => b == null)))
            {
                throw new InvalidOperationException("Missing Qin Soldier skeleton/mesh bindings on: " + enemy.name);
            }

            // Ensure no legacy Barbarian or Legend Enemy Model children remain
            var allAnimators = enemy.GetComponentsInChildren<Animator>(true);
            if (allAnimators.Length != 1)
            {
                throw new InvalidOperationException($"Expected exactly 1 Animator on {enemy.name}, found {allAnimators.Length}.");
            }
        }

        [MenuItem("Tools/Thánh Gióng/ActionForge/Capture Qin Soldier Showcase Screenshots")]
        public static void CaptureShowcaseScreenshotsMenu() => Debug.Log(CaptureShowcaseScreenshots());

        public static string CaptureShowcaseScreenshots()
        {
            const string scenePath = "Assets/Scenes/ThanhGiongWorld/PhaoDaiNgamQuanAn.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var soldiers = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<ThanhGiongEnemy>(true))
                .Where(e => !e.isBoss && e.gameObject.activeInHierarchy)
                .OrderBy(e => e.name)
                .ToArray();

            if (soldiers.Length == 0)
                throw new InvalidOperationException("No active Qin Soldiers found in " + scenePath);

            string outDir = "QinSoldierVerification/Screenshots";
            Directory.CreateDirectory(outDir);

            // Compute front row center (first 6 soldiers in row 01)
            var frontRow = soldiers.Take(Mathf.Min(6, soldiers.Length)).ToArray();
            Vector3 rowCenter = Vector3.zero;
            foreach (var s in frontRow) rowCenter += s.transform.position;
            rowCenter /= frontRow.Length;

            // Soldiers in PhaoDaiNgamQuanAn face -Z (rotation Y = 180)
            Vector3 frontForward = frontRow[0].transform.forward;

            // Ensure all soldiers start in their role's ActionForge Idle combat guard pose
            foreach (var s in soldiers)
            {
                var vis = s.transform.Find("Visual/QinSoldier Model/QinSoldier_Visual");
                string rName = s.role == ThanhGiongEnemy.EnemyRole.Auto ? "Spearman" : s.role.ToString();
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(QinSoldierBuilder.ClipPath(rName, "Idle"));
                if (vis != null && idle != null)
                    idle.SampleAnimation(vis.gameObject, 0f);
            }

            // 1. Army Formation Shot (Idle Combat Guard)
            RenderCameraShot(
                rowCenter + frontForward * 5.2f + Vector3.up * 2.3f,
                rowCenter + Vector3.up * 1.15f,
                38f,
                outDir + "/QinSoldier_Army_Formation.png");

            // 2. Hero Close-up Shot (Spearman + ShieldBearer + Archer)
            Vector3 pairCenter = frontRow.Length >= 3
                ? (frontRow[0].transform.position + frontRow[1].transform.position + frontRow[2].transform.position) / 3f
                : frontRow[0].transform.position;
            RenderCameraShot(
                pairCenter + frontForward * 3.2f + Vector3.up * 1.45f,
                pairCenter + Vector3.up * 1.05f,
                34f,
                outDir + "/QinSoldier_CloseUp_Front.png");

            // 3. ActionForge sword_regular_a_rec Pose Progression Shot across all soldiers
            var swordClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                QinSoldierBuilder.StandaloneClipsFolder + "/sword_regular_a_rec.anim");
            if (swordClip != null)
            {
                float[] sampleTimes = { 0.16f, 0.28f, 0.38f, 0.52f, 0.68f, 0.85f };
                for (int i = 0; i < soldiers.Length; i++)
                {
                    var vis = soldiers[i].transform.Find("Visual/QinSoldier Model/QinSoldier_Visual");
                    if (vis != null)
                        swordClip.SampleAnimation(vis.gameObject, sampleTimes[i % sampleTimes.Length]);
                }
                RenderCameraShot(
                    rowCenter + frontForward * 4.8f + Vector3.up * 2.0f,
                    rowCenter + Vector3.up * 1.1f,
                    38f,
                    outDir + "/QinSoldier_ActionForge_sword_regular_a_rec.png");
            }

            // 4. ActionForge Locomotion (Walk, Jog, Sprint) Shot across all soldiers
            var walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(QinSoldierBuilder.ClipPath(null, "Walk"));
            var jogClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(QinSoldierBuilder.ClipPath(null, "Jog"));
            var sprintClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(QinSoldierBuilder.ClipPath(null, "Sprint"));
            AnimationClip[] moveClips = { walkClip, jogClip, sprintClip, walkClip, jogClip, sprintClip };
            float[] movePhases = { 0.15f, 0.35f, 0.55f, 0.65f, 0.80f, 0.25f };
            for (int i = 0; i < soldiers.Length; i++)
            {
                var vis = soldiers[i].transform.Find("Visual/QinSoldier Model/QinSoldier_Visual");
                var clip = moveClips[i % moveClips.Length];
                if (vis != null && clip != null)
                    clip.SampleAnimation(vis.gameObject, movePhases[i % movePhases.Length] * clip.length);
            }
            RenderCameraShot(
                rowCenter + frontForward * 4.8f + Vector3.up * 2.0f,
                rowCenter + Vector3.up * 1.1f,
                38f,
                outDir + "/QinSoldier_ActionForge_Locomotion.png");

            // Restore clean role Idle poses on all soldiers
            foreach (var s in soldiers)
            {
                var vis = s.transform.Find("Visual/QinSoldier Model/QinSoldier_Visual");
                string rName = s.role == ThanhGiongEnemy.EnemyRole.Auto ? "Spearman" : s.role.ToString();
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(QinSoldierBuilder.ClipPath(rName, "Idle"));
                if (vis != null && idle != null)
                    idle.SampleAnimation(vis.gameObject, 0f);
            }

            return "PASS Captured 4 showcase screenshots in " + outDir;
        }

        private static void RenderCameraShot(Vector3 camPos, Vector3 lookTarget, float fov, string outputPath)
        {
            const int width = 1280;
            const int height = 720;
            var camGo = new GameObject("TempShowcaseCamera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var prevActive = RenderTexture.active;
            try
            {
                var mainCam = Camera.main;
                var cam = camGo.AddComponent<Camera>();
                if (mainCam != null)
                {
                    cam.CopyFrom(mainCam);
                }
                cam.orthographic = false;
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 400f;
                camGo.transform.position = camPos;
                camGo.transform.LookAt(lookTarget, Vector3.up);

                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prevActive;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
        }

        private static void Backup(string path, string root)
        {
            string target = root + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(path, target, true);
        }
    }
}
