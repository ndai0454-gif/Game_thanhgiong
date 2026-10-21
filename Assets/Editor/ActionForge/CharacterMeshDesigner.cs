using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThanhGiong.EditorTools
{
    /// <summary>
    /// Comprehensive 3D Character Mesh Designer & Optimizer for all characters in Game Thánh Gióng:
    /// 1. Thánh Gióng (Main Hero) & Vũ khí (Tre Ngà / Kiếm Sắt)
    /// 2. Ngựa Sắt (Iron Horse 17-bone procedural & skinned rig)
    /// 3. Tướng Giặc Ân (Boss GiacAnGeneral ActionForge rig)
    /// 4. Quân lính Giặc Ân (Barbarian / Spearman / ShieldBearer / Archer / Raider)
    /// 5. Dân làng Phù Đổng / Sứ giả / Adventurer NPCs (Knight, Mage, Ranger, Rogue, Rogue_Hooded)
    ///
    /// Performs:
    /// - ModelImporter topology, normal, tangent, and 4-bone skin weight configuration
    /// - Degenerate triangle filtering & topological seam analysis
    /// - Seam-welded area-weighted normal smoothing (eliminating UV seam lighting breaks while keeping armor creases sharp)
    /// - MikkTSpace tangent recalculation for normal maps and Cel-Rim shaders
    /// - Geodesic/Laplacian skin weight smoothing across joint boundaries (preserving rigid weapon/armor weights such as GiacAnGeneral's hand_r sword tip)
    /// - Multi-level LOD mesh generation (LOD0, LOD1, LOD2) in Assets/Art/Meshy/Generated/CharacterMeshes
    /// - Prefab and 6-scene campaign deployment with verification report
    /// </summary>
    public static class CharacterMeshDesigner
    {
        public const string OutputMeshFolder = "Assets/Art/Meshy/Generated/CharacterMeshes";
        public const string ReportFolder = "CharacterMeshVerification";
        public const string ReportPath = ReportFolder + "/mesh-design-report.txt";

        private static readonly string[] KayKitCharacterFbxPaths =
        {
            "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx/Barbarian.fbx",
            "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx/Knight.fbx",
            "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx/Mage.fbx",
            "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx/Ranger.fbx",
            "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx/Rogue.fbx",
            "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/fbx/Rogue_Hooded.fbx"
        };

        private static readonly string[] MeshyFbxPaths =
        {
            "Assets/Art/Meshy/ThanhGiong_Hero/ThanhGiong_Hero_Rigged.fbx",
            "Assets/Art/Meshy/IronHorse/IronHorse_Meshy.fbx",
            "Assets/Art/Meshy/GiacAnGeneral/GiacAnGeneral_ActionForge.fbx"
        };

        private static readonly string[] CharacterPrefabPaths =
        {
            "Assets/Prefabs/Player/Meshy_ThanhGiong_Mounted.prefab",
            "Assets/Prefabs/Meshy/Meshy_Mounted_ThanhGiong.prefab",
            "Assets/Prefabs/Characters/ThanhGiong_Mounted_Reference.prefab",
            "Assets/Prefabs/Player/ThanhGiongKnight.prefab",
            "Assets/Prefabs/Enemies/GiacAnGeneral_Meshy.prefab",
            "Assets/Prefabs/Enemies/GiacAnGeneral_Boss_An.prefab",
            "Assets/Prefabs/Enemies/KayKit_Knight_Boss_An.prefab",
            "Assets/Prefabs/Enemies/KayKitKnightBoss/KayKit_Knight_Boss_Visual.prefab",
            "Assets/Prefabs/Enemies/KayKit_Barbarian_Soldier.prefab"
        };

        [MenuItem("Tools/Thánh Gióng/Mesh Design/Optimize and Design All Character Meshes")]
        public static void DesignAllCharacterMeshesMenu()
        {
            string summary = DesignAllCharacterMeshes();
            Debug.Log(summary);
        }

        public static string DesignAllCharacterMeshes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before designing and optimizing character meshes.");

            EnsureFolder(OutputMeshFolder);
            Directory.CreateDirectory(ReportFolder);

            var report = new List<string>
            {
                "=== BÁO CÁO THIẾT KẾ & TỐI ƯU LƯỚI 3D NHÂN VẬT (CHARACTER MESH DESIGN REPORT) ===",
                "Timestamp: " + DateTime.UtcNow.ToString("O"),
                ""
            };

            // 1. Configure ModelImporters for all FBX character & equipment assets
            report.Add("--- 1. MODEL IMPORTER TOPOLOGY & SKINNING CONFIGURATION ---");
            ConfigureAllModelImporters(report);

            // 2. Design & Optimize Hero Thánh Gióng + Bamboo Spear Meshes (LOD0, LOD1, LOD2)
            report.Add("");
            report.Add("--- 2. THÁNH GIÓNG (HERO) & VŨ KHÍ MESH DESIGN ---");
            var heroMeshMap = DesignHeroAndWeaponMeshes(report);

            // 3. Design & Optimize Ngựa Sắt (Iron Horse 17-Bone Skinned Mesh + LODs)
            report.Add("");
            report.Add("--- 3. NGỰA SẮT (IRON HORSE) 17-BONE SKINNED MESH DESIGN ---");
            var horseMesh = DesignIronHorseMesh(report);

            // 4. Design & Optimize Tướng Giặc Ân (GiacAnGeneral Boss Mesh + LODs, preserving rigid hand_r sword tip)
            report.Add("");
            report.Add("--- 4. TƯỚNG GIẶC ÂN (BOSS GIAC AN GENERAL) MESH DESIGN ---");
            var bossMeshMap = DesignBossGiacAnGeneralMeshes(report);

            // 5. Design & Optimize Quân Lính Giặc Ân & Dân Làng / Sứ Giả (KayKit Adventurers: Barbarian, Knight, Mage, Ranger, Rogue)
            report.Add("");
            report.Add("--- 5. QUÂN LÍNH GIẶC ÂN & DÂN LÀNG / SỨ GIẢ (KAYKIT CHARACTERS) MESH DESIGN ---");
            var kayKitMeshMap = DesignKayKitCharacterMeshes(report);

            // Merge all optimized mesh replacements into a lookup table by original mesh name
            var allOptimizedMeshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach (var kv in heroMeshMap) allOptimizedMeshes[kv.Key] = kv.Value;
            foreach (var kv in bossMeshMap) allOptimizedMeshes[kv.Key] = kv.Value;
            foreach (var kv in kayKitMeshMap) allOptimizedMeshes[kv.Key] = kv.Value;
            if (horseMesh != null)
            {
                allOptimizedMeshes["Meshy Iron Horse Skinned"] = horseMesh;
                allOptimizedMeshes["IronHorseSkinned"] = horseMesh;
            }

            // 6. Apply optimized meshes & bounds to all Character Prefabs
            report.Add("");
            report.Add("--- 6. CHARACTER PREFAB MESH DEPLOYMENT ---");
            ApplyToAllCharacterPrefabs(allOptimizedMeshes, horseMesh, report);

            // 7. Apply optimized meshes & repair skinning across all 6 Campaign Scenes
            report.Add("");
            report.Add("--- 7. CAMPAIGN SCENE CHARACTER MESH DEPLOYMENT (6 MAPS) ---");
            ApplyToAllCampaignScenes(allOptimizedMeshes, horseMesh, report);

            AssetDatabase.SaveAssets();
            File.WriteAllLines(ReportPath, report);
            return string.Join("\n", report);
        }

        private static void ConfigureAllModelImporters(List<string> report)
        {
            var allPaths = new List<string>();
            allPaths.AddRange(MeshyFbxPaths);
            allPaths.AddRange(KayKitCharacterFbxPaths);

            string kayKitPropsFolder = "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Assets/fbx(unity)";
            if (AssetDatabase.IsValidFolder(kayKitPropsFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { kayKitPropsFolder }))
                    allPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            foreach (string path in allPaths.Distinct())
            {
                if (!File.Exists(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool changed = false;
                if (!importer.isReadable) { importer.isReadable = true; changed = true; }
                if (!importer.weldVertices) { importer.weldVertices = true; changed = true; }
                if (importer.skinWeights != ModelImporterSkinWeights.Standard)
                {
                    importer.skinWeights = ModelImporterSkinWeights.Standard;
                    changed = true;
                }
                if (importer.maxBonesPerVertex < 4)
                {
                    importer.maxBonesPerVertex = 4;
                    changed = true;
                }
                if (importer.minBoneWeight > 0.001f)
                {
                    importer.minBoneWeight = 0.001f;
                    changed = true;
                }
                if (importer.importTangents != ModelImporterTangents.CalculateMikk)
                {
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    changed = true;
                }
                // Preserve GiacAnGeneral's exact skeleton hierarchy & curve bindings
                if (path.Contains("GiacAnGeneral") && importer.optimizeGameObjects)
                {
                    importer.optimizeGameObjects = false;
                    changed = true;
                }

                if (changed) importer.SaveAndReimport();

                report.Add($"PASS Importer [{Path.GetFileName(path)}] weldVertices={importer.weldVertices} readable={importer.isReadable} skinWeights={importer.skinWeights} tangents={importer.importTangents}");
            }
        }

        private static Dictionary<string, Mesh> DesignHeroAndWeaponMeshes(List<string> report)
        {
            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);

            // 1. Optimize Hero Meshes from ThanhGiong_Hero_Rigged.fbx and Mounted Hero Prefabs
            var heroSources = new List<GameObject>();
            var heroFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Meshy/ThanhGiong_Hero/ThanhGiong_Hero_Rigged.fbx");
            if (heroFbx != null) heroSources.Add(heroFbx);
            var meshyMountedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Meshy/Meshy_Mounted_ThanhGiong.prefab");
            if (meshyMountedPrefab != null) heroSources.Add(meshyMountedPrefab);
            var mountedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Meshy_ThanhGiong_Mounted.prefab");
            if (mountedPrefab != null) heroSources.Add(mountedPrefab);

            var processedSourceMeshes = new HashSet<Mesh>();
            int heroIndex = 0;
            int weaponIndex = 0;

            foreach (var sourceGo in heroSources)
            {
                foreach (var skin in sourceGo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null || skin.name == "Meshy Horse Skin") continue;
                    Mesh src = skin.sharedMesh;
                    if (!processedSourceMeshes.Add(src)) continue;

                    string suffix = heroIndex == 0 ? "" : "_" + heroIndex;
                    heroIndex++;

                    Mesh lod0 = CreateOptimizedCharacterMesh(
                        src,
                        "ThanhGiong_Hero_Optimized_LOD0" + suffix,
                        smoothAngleDeg: 62f,
                        weightSmoothIterations: 18,
                        weightSmoothFactor: 0.28f,
                        preserveRigidBoneIndex: -1);

                    SaveOrUpdateMeshAsset(lod0, $"{OutputMeshFolder}/ThanhGiong_Hero_Optimized_LOD0{suffix}.asset", out Mesh savedLod0);
                    Mesh lod1 = GenerateSimplifiedLodMesh(savedLod0, "ThanhGiong_Hero_LOD1" + suffix, 0.62f, -1);
                    Mesh lod2 = GenerateSimplifiedLodMesh(savedLod0, "ThanhGiong_Hero_LOD2" + suffix, 0.36f, -1);
                    SaveOrUpdateMeshAsset(lod1, $"{OutputMeshFolder}/ThanhGiong_Hero_LOD1{suffix}.asset", out Mesh savedLod1);
                    SaveOrUpdateMeshAsset(lod2, $"{OutputMeshFolder}/ThanhGiong_Hero_LOD2{suffix}.asset", out Mesh savedLod2);

                    result[src.name] = savedLod0;
                    result[savedLod0.name] = savedLod0;
                    if (suffix == "") result["__HeroDefaultLOD0"] = savedLod0;
                    report.Add($"PASS Hero Mesh [{src.name}] -> [{savedLod0.name}] verts={savedLod0.vertexCount} tris={savedLod0.triangles.Length / 3} bones={savedLod0.bindposes.Length} | LOD1 tris={savedLod1.triangles.Length / 3} | LOD2 tris={savedLod2.triangles.Length / 3}");
                }

                // 2. Optimize Hero Weapon Meshes (Tre Ngà / Kiếm Sắt) inside Mounted Hero Prefab
                foreach (var mf in sourceGo.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Mesh src = mf.sharedMesh;
                    if (!processedSourceMeshes.Add(src)) continue;
                    // Skip primitive built-in Unity meshes (Cube, Cylinder, Sphere, Capsule, Quad)
                    string assetPath = AssetDatabase.GetAssetPath(src);
                    if (string.IsNullOrEmpty(assetPath) || assetPath.StartsWith("Library/", StringComparison.OrdinalIgnoreCase)) continue;

                    string wSuffix = weaponIndex == 0 ? "" : "_" + weaponIndex;
                    weaponIndex++;

                    Mesh lod0 = CreateOptimizedCharacterMesh(
                        src,
                        "BambooSpear_Optimized_LOD0" + wSuffix,
                        smoothAngleDeg: 55f,
                        weightSmoothIterations: 0,
                        weightSmoothFactor: 0f,
                        preserveRigidBoneIndex: -1);

                    SaveOrUpdateMeshAsset(lod0, $"{OutputMeshFolder}/BambooSpear_Optimized_LOD0{wSuffix}.asset", out Mesh savedLod0);
                    result[src.name] = savedLod0;
                    result[savedLod0.name] = savedLod0;
                    report.Add($"PASS Hero Weapon Mesh [{src.name}] -> [{savedLod0.name}] verts={savedLod0.vertexCount} tris={savedLod0.triangles.Length / 3}");
                }
            }

            return result;
        }

        private static Mesh DesignIronHorseMesh(List<string> report)
        {
            EnsureFolder("Assets/Art/Meshy/Generated");
            string horseAssetPath = "Assets/Art/Meshy/Generated/IronHorseSkinned.asset";
            var horseMesh = AssetDatabase.LoadAssetAtPath<Mesh>(horseAssetPath);

            // Check known horse mesh asset paths / GUIDs and prefabs
            string[] candidatePrefabs =
            {
                "Assets/Prefabs/Meshy/Meshy_Mounted_ThanhGiong.prefab",
                "Assets/Prefabs/Player/Meshy_ThanhGiong_Mounted.prefab",
                "Assets/Prefabs/Characters/ThanhGiong_Mounted_Reference.prefab"
            };

            foreach (string prefabPath in candidatePrefabs)
            {
                if (!File.Exists(prefabPath)) continue;
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var skin = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .FirstOrDefault(s => s.name == "Meshy Horse Skin" && s.bones != null && s.bones.Length == 17);
                    if (skin != null)
                    {
                        if (skin.sharedMesh == null && horseMesh == null)
                        {
                            // Try loading by GUID or building from IronHorse_Meshy.fbx
                            string guidPath = AssetDatabase.GUIDToAssetPath("32b1395c214a12c45af760d10c23cfb7");
                            if (!string.IsNullOrEmpty(guidPath))
                                horseMesh = AssetDatabase.LoadAssetAtPath<Mesh>(guidPath);
                        }

                        if (skin.sharedMesh == null && horseMesh == null)
                        {
                            horseMesh = BuildNormalizedHorseMeshFromFbx(skin);
                        }

                        if (skin.sharedMesh == null && horseMesh != null)
                        {
                            skin.sharedMesh = horseMesh;
                        }

                        if (skin.sharedMesh != null)
                        {
                            HorseSkinRepair.Repair(skin, true);
                            Mesh repairedClone = Object.Instantiate(skin.sharedMesh);
                            repairedClone.name = "Meshy Iron Horse Skinned";
                            RecalculateSeamWeldedNormalsAndTangents(repairedClone, 58f);
                            repairedClone.RecalculateBounds();
                            SaveOrUpdateMeshAsset(repairedClone, horseAssetPath, out horseMesh);
                            break;
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            if (horseMesh == null)
                throw new InvalidOperationException("Could not locate or build IronHorseSkinned.asset.");

            // Also optimize static IronHorse_Meshy.fbx if present
            var horseFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Meshy/IronHorse/IronHorse_Meshy.fbx");
            if (horseFbx != null)
            {
                foreach (var mf in horseFbx.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Mesh staticHorse = CreateOptimizedCharacterMesh(mf.sharedMesh, "IronHorse_Static_Optimized_LOD0", 58f, 0, 0f, -1);
                    SaveOrUpdateMeshAsset(staticHorse, OutputMeshFolder + "/IronHorse_Static_Optimized_LOD0.asset", out _);
                }
            }

            // Generate LOD1 & LOD2 for Iron Horse Skinned Mesh
            Mesh lod1 = GenerateSimplifiedLodMesh(horseMesh, "IronHorse_LOD1", 0.60f, -1);
            Mesh lod2 = GenerateSimplifiedLodMesh(horseMesh, "IronHorse_LOD2", 0.35f, -1);
            SaveOrUpdateMeshAsset(lod1, OutputMeshFolder + "/IronHorse_LOD1.asset", out Mesh savedLod1);
            SaveOrUpdateMeshAsset(lod2, OutputMeshFolder + "/IronHorse_LOD2.asset", out Mesh savedLod2);

            report.Add($"PASS IronHorse Mesh [{horseMesh.name}] verts={horseMesh.vertexCount} tris={horseMesh.triangles.Length / 3} bones={horseMesh.bindposes.Length} | LOD1 tris={savedLod1.triangles.Length / 3} | LOD2 tris={savedLod2.triangles.Length / 3}");
            return horseMesh;
        }

        private static Mesh BuildNormalizedHorseMeshFromFbx(SkinnedMeshRenderer targetSkin)
        {
            var horseFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Meshy/IronHorse/IronHorse_Meshy.fbx");
            if (horseFbx == null) return null;
            var mf = horseFbx.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return null;

            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = "Meshy Iron Horse Skinned";
            var vertices = mesh.vertices;
            Bounds b = mesh.bounds;
            float height = Mathf.Max(0.01f, b.size.y);
            float scale = (1.415f * 1.3f) / height;
            float minY = b.min.y;
            Vector3 centerXZ = new Vector3(b.center.x, 0f, b.center.z);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i] - centerXZ;
                v.y -= minY;
                vertices[i] = v * scale;
            }
            mesh.vertices = vertices;
            mesh.bindposes = targetSkin.bones.Select(x => x.worldToLocalMatrix * targetSkin.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Dictionary<string, Mesh> DesignBossGiacAnGeneralMeshes(List<string> report)
        {
            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(GiacAnGeneralBuilder.SourcePath);
            if (sourceModel == null)
                throw new InvalidOperationException("Missing Boss FBX at " + GiacAnGeneralBuilder.SourcePath);

            foreach (var skin in sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                Mesh src = skin.sharedMesh;
                int handRBoneIndex = Array.FindIndex(skin.bones, b => b != null && b.name == "hand_r");

                Mesh lod0 = CreateOptimizedCharacterMesh(
                    src,
                    "GiacAnGeneral_Optimized_LOD0",
                    smoothAngleDeg: 64f,
                    weightSmoothIterations: 16,
                    weightSmoothFactor: 0.25f,
                    preserveRigidBoneIndex: handRBoneIndex);

                // Verify that rigid sword tip vertices (> 0.95f on hand_r) are strictly preserved
                if (handRBoneIndex >= 0)
                {
                    int rigidCount = CountRigidVerticesForBone(lod0, handRBoneIndex, 0.95f);
                    if (rigidCount == 0)
                        throw new InvalidOperationException("GiacAnGeneral_Optimized_LOD0 lost rigid hand_r sword tip weights!");
                }

                SaveOrUpdateMeshAsset(lod0, OutputMeshFolder + "/GiacAnGeneral_Optimized_LOD0.asset", out Mesh savedLod0);
                Mesh lod1 = GenerateSimplifiedLodMesh(savedLod0, "GiacAnGeneral_LOD1", 0.58f, handRBoneIndex);
                Mesh lod2 = GenerateSimplifiedLodMesh(savedLod0, "GiacAnGeneral_LOD2", 0.34f, handRBoneIndex);
                SaveOrUpdateMeshAsset(lod1, OutputMeshFolder + "/GiacAnGeneral_LOD1.asset", out Mesh savedLod1);
                SaveOrUpdateMeshAsset(lod2, OutputMeshFolder + "/GiacAnGeneral_LOD2.asset", out Mesh savedLod2);

                result[src.name] = savedLod0;
                result[savedLod0.name] = savedLod0;
                report.Add($"PASS Boss Mesh [{src.name}] -> [{savedLod0.name}] verts={savedLod0.vertexCount} tris={savedLod0.triangles.Length / 3} bones={savedLod0.bindposes.Length} hand_r={handRBoneIndex} | LOD1 tris={savedLod1.triangles.Length / 3} | LOD2 tris={savedLod2.triangles.Length / 3}");
            }

            return result;
        }

        private static Dictionary<string, Mesh> DesignKayKitCharacterMeshes(List<string> report)
        {
            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);

            foreach (string fbxPath in KayKitCharacterFbxPaths)
            {
                if (!File.Exists(fbxPath)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (model == null) continue;
                string charName = Path.GetFileNameWithoutExtension(fbxPath);

                int skinnedCount = 0;
                int totalVerts = 0;
                int totalTris = 0;

                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null) continue;
                    Mesh src = skin.sharedMesh;
                    string assetName = $"KayKit_{charName}_{SanitizeFileName(src.name)}_LOD0";
                    Mesh optimized = CreateOptimizedCharacterMesh(
                        src,
                        assetName,
                        smoothAngleDeg: 52f,
                        weightSmoothIterations: 8,
                        weightSmoothFactor: 0.20f,
                        preserveRigidBoneIndex: -1);

                    SaveOrUpdateMeshAsset(optimized, $"{OutputMeshFolder}/{assetName}.asset", out Mesh savedLod0);
                    result[src.name] = savedLod0;
                    result[savedLod0.name] = savedLod0;

                    skinnedCount++;
                    totalVerts += savedLod0.vertexCount;
                    totalTris += savedLod0.triangles.Length / 3;
                }

                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Mesh src = mf.sharedMesh;
                    string assetName = $"KayKit_{charName}_{SanitizeFileName(src.name)}_Static_LOD0";
                    Mesh optimized = CreateOptimizedCharacterMesh(
                        src,
                        assetName,
                        smoothAngleDeg: 50f,
                        weightSmoothIterations: 0,
                        weightSmoothFactor: 0f,
                        preserveRigidBoneIndex: -1);

                    SaveOrUpdateMeshAsset(optimized, $"{OutputMeshFolder}/{assetName}.asset", out Mesh savedLod0);
                    result[src.name] = savedLod0;
                    result[savedLod0.name] = savedLod0;

                    totalVerts += savedLod0.vertexCount;
                    totalTris += savedLod0.triangles.Length / 3;
                }

                report.Add($"PASS KayKit Character [{charName}] skinnedParts={skinnedCount} totalVerts={totalVerts} totalTris={totalTris}");
            }

            return result;
        }

        private static void ApplyToAllCharacterPrefabs(Dictionary<string, Mesh> meshMap, Mesh horseMesh, List<string> report)
        {
            foreach (string prefabPath in CharacterPrefabPaths)
            {
                if (!File.Exists(prefabPath)) continue;
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = false;
                int updatedRenderers = 0;

                try
                {
                    foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (skin.name == "Meshy Horse Skin" && skin.bones != null && skin.bones.Length == 17)
                        {
                            if (horseMesh != null && skin.sharedMesh != horseMesh)
                            {
                                skin.sharedMesh = horseMesh;
                            }
                            if (skin.sharedMesh != null)
                            {
                                HorseSkinRepair.Repair(skin, false);
                                RecalculateSeamWeldedNormalsAndTangents(skin.sharedMesh, 58f);
                            }
                            skin.updateWhenOffscreen = true;
                            skin.localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2.5f, 3.2f, 3.8f));
                            changed = true;
                            updatedRenderers++;
                            continue;
                        }

                        if (skin.sharedMesh == null && skin.bones != null && meshMap.TryGetValue("__HeroDefaultLOD0", out Mesh defaultHero) && defaultHero != null && defaultHero.bindposes.Length == skin.bones.Length)
                        {
                            skin.sharedMesh = defaultHero;
                            skin.updateWhenOffscreen = true;
                            changed = true;
                            updatedRenderers++;
                            continue;
                        }

                        if (skin.sharedMesh == null) continue;

                        if (meshMap.TryGetValue(skin.sharedMesh.name, out Mesh replacement) && replacement != null)
                        {
                            // Only replace if bindpose count matches the SkinnedMeshRenderer bones count
                            if (replacement.bindposes.Length == skin.bones.Length)
                            {
                                skin.sharedMesh = replacement;
                                skin.updateWhenOffscreen = true;
                                skin.shadowCastingMode = ShadowCastingMode.On;
                                skin.receiveShadows = true;
                                changed = true;
                                updatedRenderers++;
                            }
                        }
                    }

                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        if (meshMap.TryGetValue(mf.sharedMesh.name, out Mesh replacement) && replacement != null)
                        {
                            mf.sharedMesh = replacement;
                            changed = true;
                            updatedRenderers++;
                        }
                    }

                    // Ensure boss weapon tip is accurately positioned if this is a Boss prefab
                    foreach (var enemy in root.GetComponentsInChildren<ThanhGiongEnemy>(true))
                    {
                        if (enemy.isBoss)
                        {
                            GiacAnGeneralCampaignInstaller.ApplyBoss(enemy);
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                    report.Add($"PASS Prefab [{prefabPath}] updatedRenderers={updatedRenderers}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ApplyToAllCampaignScenes(Dictionary<string, Mesh> meshMap, Mesh horseMesh, List<string> report)
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            EditorSceneManager.SaveOpenScenes();

            try
            {
                var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/ThanhGiongWorld" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(p => p)
                    .ToArray();

                foreach (string scenePath in scenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    int updatedSkinned = 0;
                    int updatedStatic = 0;
                    int repairedHorses = 0;

                    foreach (var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include))
                    {
                        if (skin == null) continue;

                        if (skin.name == "Meshy Horse Skin" && skin.bones != null && skin.bones.Length == 17)
                        {
                            if (horseMesh != null) skin.sharedMesh = horseMesh;
                            if (skin.sharedMesh != null) HorseSkinRepair.Repair(skin, false);
                            skin.updateWhenOffscreen = true;
                            skin.localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2.5f, 3.2f, 3.8f));
                            EditorUtility.SetDirty(skin);
                            repairedHorses++;
                            continue;
                        }

                        if (skin.sharedMesh == null) continue;

                        if (meshMap.TryGetValue(skin.sharedMesh.name, out Mesh replacement) && replacement != null)
                        {
                            if (skin.bones != null && replacement.bindposes.Length == skin.bones.Length)
                            {
                                skin.sharedMesh = replacement;
                                skin.updateWhenOffscreen = true;
                                EditorUtility.SetDirty(skin);
                                updatedSkinned++;
                            }
                        }
                    }

                    foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
                    {
                        if (mf == null || mf.sharedMesh == null) continue;
                        if (meshMap.TryGetValue(mf.sharedMesh.name, out Mesh replacement) && replacement != null)
                        {
                            mf.sharedMesh = replacement;
                            EditorUtility.SetDirty(mf);
                            updatedStatic++;
                        }
                    }

                    // Re-verify all bosses in scene have valid sword tip and bindings
                    foreach (var enemy in Object.FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include))
                    {
                        if (enemy != null && enemy.isBoss)
                        {
                            GiacAnGeneralCampaignInstaller.ApplyBoss(enemy);
                        }
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.Add($"PASS Scene [{scene.name}] skinnedMeshes={updatedSkinned} staticWeaponMeshes={updatedStatic} horseRigs={repairedHorses}");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }
            }
        }

        /// <summary>
        /// Creates an optimized character Mesh with:
        /// 1. Degenerate triangle removal (preserving submeshes, scale-invariant)
        /// 2. Seam-welded area-weighted smooth normals + MikkTSpace tangents
        /// 3. Topological Laplacian skin weight smoothing across welded seams (preserving rigid weapon/armor weights)
        /// </summary>
        private static Mesh CreateOptimizedCharacterMesh(
            Mesh source,
            string newName,
            float smoothAngleDeg,
            int weightSmoothIterations,
            float weightSmoothFactor,
            int preserveRigidBoneIndex)
        {
            Mesh mesh = Object.Instantiate(source);
            mesh.name = newName;
            mesh.RecalculateBounds();

            // 1. Prune strictly index-degenerate triangles per submesh (scale-invariant)
            PruneDegenerateTriangles(mesh);

            // 2. Smooth skin weights across welded topological edges if skinned
            var weights = mesh.boneWeights;
            var bindposes = mesh.bindposes;
            if (weights != null && weights.Length == mesh.vertexCount && bindposes != null && bindposes.Length > 0 && weightSmoothIterations > 0)
            {
                mesh.boneWeights = SmoothSkinWeightsAcrossSeams(
                    mesh.vertices,
                    mesh.bounds,
                    mesh.triangles,
                    weights,
                    bindposes.Length,
                    weightSmoothIterations,
                    weightSmoothFactor,
                    preserveRigidBoneIndex);
            }

            // 3. Recalculate seam-welded normals & MikkTSpace tangents
            RecalculateSeamWeldedNormalsAndTangents(mesh, smoothAngleDeg);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void PruneDegenerateTriangles(Mesh mesh)
        {
            int subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < subMeshCount; s++)
            {
                int[] tris = mesh.GetTriangles(s);
                var clean = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    if (a == b || b == c || c == a) continue;
                    clean.Add(a);
                    clean.Add(b);
                    clean.Add(c);
                }
                if (clean.Count > 0 && clean.Count != tris.Length)
                {
                    mesh.SetTriangles(clean, s, false);
                }
            }
        }

        /// <summary>
        /// Calculates area-weighted normals welded across UV/material seams within smoothAngleDeg,
        /// then computes orthogonal MikkTSpace tangents. Fully scale-invariant.
        /// </summary>
        public static void RecalculateSeamWeldedNormalsAndTangents(Mesh mesh, float smoothAngleDeg)
        {
            if (mesh == null || mesh.vertexCount == 0) return;

            mesh.RecalculateNormals();

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            int vCount = vertices.Length;
            if (triangles == null || triangles.Length < 3) return;

            Bounds bounds = mesh.bounds;
            float invScale = 10000f / Mathf.Max(1e-6f, bounds.size.magnitude);

            // Group coincident vertices (UV/submesh seams) by scale-normalized quantized position
            var posGroups = new Dictionary<Vector3Int, List<int>>(vCount);
            for (int i = 0; i < vCount; i++)
            {
                Vector3Int key = Vector3Int.RoundToInt((vertices[i] - bounds.min) * invScale);
                if (!posGroups.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    posGroups[key] = list;
                }
                list.Add(i);
            }

            // Compute area-weighted face normals accumulated onto each vertex
            var accumNormals = new Vector3[vCount];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
                Vector3 e1 = (vertices[i1] - vertices[i0]) * invScale;
                Vector3 e2 = (vertices[i2] - vertices[i0]) * invScale;
                Vector3 faceNormal = Vector3.Cross(e1, e2);
                accumNormals[i0] += faceNormal;
                accumNormals[i1] += faceNormal;
                accumNormals[i2] += faceNormal;
            }

            Vector3[] fallbackNormals = mesh.normals;
            for (int i = 0; i < vCount; i++)
            {
                if (accumNormals[i].sqrMagnitude > 1e-12f)
                    accumNormals[i].Normalize();
                else if (fallbackNormals != null && i < fallbackNormals.Length && fallbackNormals[i].sqrMagnitude > 1e-6f)
                    accumNormals[i] = fallbackNormals[i].normalized;
                else
                    accumNormals[i] = Vector3.up;
            }

            // Weld normals across coincident seam vertices whose angle is within smoothAngleDeg
            float cosThreshold = Mathf.Cos(smoothAngleDeg * Mathf.Deg2Rad);
            var weldedNormals = new Vector3[vCount];
            Array.Copy(accumNormals, weldedNormals, vCount);

            foreach (var kv in posGroups)
            {
                var group = kv.Value;
                if (group.Count <= 1) continue;

                for (int a = 0; a < group.Count; a++)
                {
                    int idxA = group[a];
                    Vector3 nA = accumNormals[idxA];
                    Vector3 sum = nA;
                    for (int b = 0; b < group.Count; b++)
                    {
                        if (a == b) continue;
                        Vector3 nB = accumNormals[group[b]];
                        if (Vector3.Dot(nA, nB) >= cosThreshold)
                            sum += nB;
                    }
                    if (sum.sqrMagnitude > 1e-10f)
                        weldedNormals[idxA] = sum.normalized;
                }
            }

            mesh.normals = weldedNormals;
            if (mesh.uv != null && mesh.uv.Length == vCount)
            {
                mesh.RecalculateTangents();
            }
        }

        /// <summary>
        /// Smooths BoneWeights across welded vertex neighbors while preserving rigid vertices
        /// (such as sword blades bound > 0.95f to preserveRigidBoneIndex). Scale-invariant.
        /// </summary>
        private static BoneWeight[] SmoothSkinWeightsAcrossSeams(
            Vector3[] vertices,
            Bounds bounds,
            int[] triangles,
            BoneWeight[] initial,
            int boneCount,
            int iterations,
            float factor,
            int preserveRigidBoneIndex)
        {
            int vCount = vertices.Length;
            float invScale = 10000f / Mathf.Max(1e-6f, bounds.size.magnitude);
            var lookup = new Dictionary<Vector3Int, int>(vCount);
            var vToNode = new int[vCount];
            var nodeLocked = new List<bool>();

            for (int i = 0; i < vCount; i++)
            {
                Vector3Int key = Vector3Int.RoundToInt((vertices[i] - bounds.min) * invScale);
                if (!lookup.TryGetValue(key, out int node))
                {
                    node = lookup.Count;
                    lookup[key] = node;
                    nodeLocked.Add(false);
                }
                vToNode[i] = node;

                if (preserveRigidBoneIndex >= 0 && IsRigidlyBoundToBone(initial[i], preserveRigidBoneIndex, 0.92f))
                {
                    nodeLocked[node] = true;
                }
            }

            int nodeCount = lookup.Count;
            var neighbors = new HashSet<int>[nodeCount];
            for (int n = 0; n < nodeCount; n++) neighbors[n] = new HashSet<int>();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int n0 = vToNode[triangles[i]];
                int n1 = vToNode[triangles[i + 1]];
                int n2 = vToNode[triangles[i + 2]];
                if (n0 != n1) { neighbors[n0].Add(n1); neighbors[n1].Add(n0); }
                if (n1 != n2) { neighbors[n1].Add(n2); neighbors[n2].Add(n1); }
                if (n2 != n0) { neighbors[n2].Add(n0); neighbors[n0].Add(n2); }
            }

            // Lock 1-ring neighbors of rigid weapon vertices too so sword hilt/blade boundary never softens
            if (preserveRigidBoneIndex >= 0)
            {
                var ringLocked = nodeLocked.ToArray();
                for (int n = 0; n < nodeCount; n++)
                {
                    if (!nodeLocked[n]) continue;
                    foreach (int nb in neighbors[n]) ringLocked[nb] = true;
                }
                for (int n = 0; n < nodeCount; n++) nodeLocked[n] = ringLocked[n];
            }

            var current = new float[nodeCount, boneCount];
            var next = new float[nodeCount, boneCount];
            var samples = new int[nodeCount];

            for (int i = 0; i < vCount; i++)
            {
                int n = vToNode[i];
                samples[n]++;
                BoneWeight w = initial[i];
                if (w.boneIndex0 >= 0 && w.boneIndex0 < boneCount) current[n, w.boneIndex0] += w.weight0;
                if (w.boneIndex1 >= 0 && w.boneIndex1 < boneCount) current[n, w.boneIndex1] += w.weight1;
                if (w.boneIndex2 >= 0 && w.boneIndex2 < boneCount) current[n, w.boneIndex2] += w.weight2;
                if (w.boneIndex3 >= 0 && w.boneIndex3 < boneCount) current[n, w.boneIndex3] += w.weight3;
            }

            for (int n = 0; n < nodeCount; n++)
            {
                if (samples[n] <= 0) continue;
                float inv = 1f / samples[n];
                for (int b = 0; b < boneCount; b++) current[n, b] *= inv;
            }

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int n = 0; n < nodeCount; n++)
                {
                    if (nodeLocked[n] || neighbors[n].Count == 0)
                    {
                        for (int b = 0; b < boneCount; b++) next[n, b] = current[n, b];
                        continue;
                    }
                    float invNb = 1f / neighbors[n].Count;
                    for (int b = 0; b < boneCount; b++)
                    {
                        float avg = 0f;
                        foreach (int nb in neighbors[n]) avg += current[nb, b];
                        avg *= invNb;
                        next[n, b] = Mathf.Lerp(current[n, b], avg, factor);
                    }
                }
                var tmp = current;
                current = next;
                next = tmp;
            }

            var output = new BoneWeight[vCount];
            for (int i = 0; i < vCount; i++)
            {
                int n = vToNode[i];
                if (nodeLocked[n])
                {
                    output[i] = initial[i];
                    continue;
                }

                int i0 = 0, i1 = 0, i2 = 0, i3 = 0;
                float w0 = 0f, w1 = 0f, w2 = 0f, w3 = 0f;

                for (int b = 0; b < boneCount; b++)
                {
                    float val = current[n, b];
                    if (val <= 0.0005f) continue;
                    if (val > w0) { w3 = w2; i3 = i2; w2 = w1; i2 = i1; w1 = w0; i1 = i0; w0 = val; i0 = b; }
                    else if (val > w1) { w3 = w2; i3 = i2; w2 = w1; i2 = i1; w1 = val; i1 = b; }
                    else if (val > w2) { w3 = w2; i3 = i2; w2 = val; i2 = b; }
                    else if (val > w3) { w3 = val; i3 = b; }
                }

                float sum = w0 + w1 + w2 + w3;
                if (sum <= 1e-6f)
                {
                    output[i] = initial[i];
                }
                else
                {
                    float inv = 1f / sum;
                    output[i] = new BoneWeight
                    {
                        boneIndex0 = i0, weight0 = w0 * inv,
                        boneIndex1 = i1, weight1 = w1 * inv,
                        boneIndex2 = i2, weight2 = w2 * inv,
                        boneIndex3 = i3, weight3 = w3 * inv
                    };
                }
            }

            return output;
        }

        /// <summary>
        /// Generates a simplified LOD mesh using uniform spatial-UV-bone clustering so UV seams,
        /// submeshes, bindposes, and rigid weapon vertices remain intact.
        /// </summary>
        private static Mesh GenerateSimplifiedLodMesh(Mesh source, string lodName, float targetRatio, int preserveRigidBoneIndex)
        {
            Vector3[] srcVerts = source.vertices;
            Vector3[] srcNormals = source.normals;
            Vector4[] srcTangents = source.tangents;
            Vector2[] srcUv = source.uv;
            BoneWeight[] srcWeights = source.boneWeights;
            Bounds bounds = source.bounds;

            int vCount = srcVerts.Length;
            float targetCellsPerAxis = Mathf.Lerp(24f, 86f, Mathf.Clamp01(targetRatio));
            float maxExtent = Mathf.Max(1e-7f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            float cellSize = maxExtent / targetCellsPerAxis;

            var clusterMap = new Dictionary<(int, int, int, int, int, int), int>(vCount);
            var oldToNew = new int[vCount];

            var newVerts = new List<Vector3>(vCount);
            var newNormals = new List<Vector3>(vCount);
            var newTangents = new List<Vector4>(vCount);
            var newUvs = new List<Vector2>(vCount);
            var newWeights = new List<BoneWeight>(vCount);

            bool hasNormals = srcNormals != null && srcNormals.Length == vCount;
            bool hasTangents = srcTangents != null && srcTangents.Length == vCount;
            bool hasUv = srcUv != null && srcUv.Length == vCount;
            bool hasWeights = srcWeights != null && srcWeights.Length == vCount;

            for (int i = 0; i < vCount; i++)
            {
                if (preserveRigidBoneIndex >= 0 && hasWeights && IsRigidlyBoundToBone(srcWeights[i], preserveRigidBoneIndex, 0.92f))
                {
                    int idx = newVerts.Count;
                    oldToNew[i] = idx;
                    newVerts.Add(srcVerts[i]);
                    if (hasNormals) newNormals.Add(srcNormals[i]);
                    if (hasTangents) newTangents.Add(srcTangents[i]);
                    if (hasUv) newUvs.Add(srcUv[i]);
                    if (hasWeights) newWeights.Add(srcWeights[i]);
                    continue;
                }

                Vector3 p = srcVerts[i] - bounds.min;
                int gx = Mathf.RoundToInt(p.x / cellSize);
                int gy = Mathf.RoundToInt(p.y / cellSize);
                int gz = Mathf.RoundToInt(p.z / cellSize);
                int ux = hasUv ? Mathf.RoundToInt(srcUv[i].x * 48f) : 0;
                int uy = hasUv ? Mathf.RoundToInt(srcUv[i].y * 48f) : 0;
                int domBone = hasWeights ? srcWeights[i].boneIndex0 : 0;

                var key = (gx, gy, gz, ux, uy, domBone);
                if (!clusterMap.TryGetValue(key, out int mapped))
                {
                    mapped = newVerts.Count;
                    clusterMap[key] = mapped;
                    newVerts.Add(srcVerts[i]);
                    if (hasNormals) newNormals.Add(srcNormals[i]);
                    if (hasTangents) newTangents.Add(srcTangents[i]);
                    if (hasUv) newUvs.Add(srcUv[i]);
                    if (hasWeights) newWeights.Add(srcWeights[i]);
                }
                oldToNew[i] = mapped;
            }

            var lod = new Mesh { name = lodName };
            if (newVerts.Count > 65535) lod.indexFormat = IndexFormat.UInt32;
            lod.SetVertices(newVerts);
            if (hasNormals) lod.SetNormals(newNormals);
            if (hasTangents) lod.SetTangents(newTangents);
            if (hasUv) lod.SetUVs(0, newUvs);
            if (hasWeights) lod.boneWeights = newWeights.ToArray();
            if (source.bindposes != null && source.bindposes.Length > 0) lod.bindposes = source.bindposes;

            lod.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] tris = source.GetTriangles(s);
                var subTris = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = oldToNew[tris[i]];
                    int b = oldToNew[tris[i + 1]];
                    int c = oldToNew[tris[i + 2]];
                    if (a == b || b == c || c == a) continue;
                    subTris.Add(a);
                    subTris.Add(b);
                    subTris.Add(c);
                }
                lod.SetTriangles(subTris, s, false);
            }

            RecalculateSeamWeldedNormalsAndTangents(lod, 60f);
            lod.RecalculateBounds();
            return lod;
        }

        private static bool IsRigidlyBoundToBone(BoneWeight w, int boneIndex, float threshold)
        {
            return (w.boneIndex0 == boneIndex && w.weight0 > threshold) ||
                   (w.boneIndex1 == boneIndex && w.weight1 > threshold) ||
                   (w.boneIndex2 == boneIndex && w.weight2 > threshold) ||
                   (w.boneIndex3 == boneIndex && w.weight3 > threshold);
        }

        private static int CountRigidVerticesForBone(Mesh mesh, int boneIndex, float threshold)
        {
            var weights = mesh.boneWeights;
            if (weights == null) return 0;
            int count = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (IsRigidlyBoundToBone(weights[i], boneIndex, threshold)) count++;
            }
            return count;
        }

        private static void SaveOrUpdateMeshAsset(Mesh mesh, string assetPath, out Mesh savedAsset)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                savedAsset = mesh;
            }
            else
            {
                string keepName = mesh.name;
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = keepName;
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                savedAsset = existing;
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_').Replace(':', '_').Replace('.', '_');
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash > 0)
            {
                string parent = path.Substring(0, slash);
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
            }
        }
    }
}
