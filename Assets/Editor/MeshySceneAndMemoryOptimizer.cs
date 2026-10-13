using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThanhGiong.EditorTools
{
    public static class MeshySceneAndMemoryOptimizer
    {
        private static readonly string[] OfficialCampaignScenes =
        {
            "Assets/Scenes/ThanhGiongWorld/LangGiongTienTuyen.unity",
            "Assets/Scenes/ThanhGiongWorld/KinhThanhRenThep.unity",
            "Assets/Scenes/ThanhGiongWorld/PhaoDaiNgamQuanAn.unity",
            "Assets/Scenes/ThanhGiongWorld/ThungLungVuotSong.unity",
            "Assets/Scenes/ThanhGiongWorld/TranTuyenNuiSoc.unity",
            "Assets/Scenes/ThanhGiongWorld/DinhSocHoaThanh.unity"
        };

        private static readonly string[] ObsoleteAssetPathsToDelete =
        {
            // Old prototype & recovery scenes
            "Assets/Scenes/AlbionForestMap.unity",
            "Assets/_Recovery",
            // Old pre-Meshy & test asset folders
            "Assets/fbcd2ed9-1e6d-4f6a-b216-a1d89c359a7d",
            "Assets/Art/Generated",
            "Assets/Art/Models",
            "Assets/Art/Textures",
            "Assets/Animations/ActionForge/Meshy_ActionForge.fbx",
            // Duplicate / unused raw GLB & duplicate PNG files in Assets/Art/Meshy
            "Assets/Art/Meshy/BambooSpear_Meshy.glb",
            "Assets/Art/Meshy/IronHorse_Meshy.glb",
            "Assets/Art/Meshy/ThanhGiong_Meshy.glb",
            "Assets/Art/Meshy/Generated",
            "Assets/Art/Meshy/BambooSpear/BambooSpear_Albedo.png",
            "Assets/Art/Meshy/BambooSpear/BambooSpear_Normal.png",
            "Assets/Art/Meshy/BambooSpear/BambooSpear_Meshy_Metallic.png",
            "Assets/Art/Meshy/BambooSpear/BambooSpear_Meshy_Roughness.png",
            "Assets/Art/Meshy/IronHorse/IronHorse_Albedo.png",
            "Assets/Art/Meshy/IronHorse/IronHorse_Normal.png",
            "Assets/Art/Meshy/IronHorse/IronHorse_Meshy_Metallic.png",
            "Assets/Art/Meshy/IronHorse/IronHorse_Meshy_Roughness.png",
            "Assets/Art/Meshy/ThanhGiong_Hero/ThanhGiong_Hero_Albedo.png",
            "Assets/Art/Meshy/ThanhGiong_Hero/ThanhGiong_Hero_Normal.png",
            "Assets/Art/Meshy/ThanhGiong_Hero/ThanhGiong_Hero_Meshy_Metallic.png",
            "Assets/Art/Meshy/ThanhGiong_Hero/ThanhGiong_Hero_Meshy_Roughness.png"
        };

        [MenuItem("Tools/Thánh Gióng/Remove Old Scenes & Optimize Memory (RAM/VRAM/Disk)", false, 7)]
        public static string OptimizeAll()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            // 1. Delete obsolete scenes & redundant asset folders inside Assets/
            int deletedAssets = 0;
            foreach (string path in ObsoleteAssetPathsToDelete)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || Directory.Exists(path) || File.Exists(path))
                {
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        deletedAssets++;
                    }
                }
            }

            // 2. Update EditorBuildSettings.scenes to ONLY include the 6 official campaign scenes
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
            foreach (string sp in OfficialCampaignScenes)
            {
                if (File.Exists(sp))
                {
                    buildScenes.Add(new EditorBuildSettingsScene(sp, true));
                }
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();

            // 3. Purge hidden/legacy GameObjects and unused hierarchy roots from all 6 campaign scenes
            int purgedObjects = PurgeLegacyObjectsInScenes();

            // 4. Optimize FBX ModelImporters (disable Read/Write CPU buffer duplication, enable mesh compression, strip unused animation materials)
            int optimizedModels = OptimizeAllModelImporters();

            // 5. Optimize TextureImporters (Crunch compression + optimal maxTextureSize for VRAM reduction)
            int optimizedTextures = OptimizeAllTextureImporters();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.UnloadUnusedAssetsImmediate(true);
            GC.Collect();

            string report = $"[MeshySceneAndMemoryOptimizer] Deleted {deletedAssets} obsolete scenes/asset bundles, purged {purgedObjects} hidden legacy GameObjects across 6 campaign scenes, optimized {optimizedModels} FBX meshes (Read/Write OFF + MeshCompression), and Crunch-compressed {optimizedTextures} PBR textures.";
            Debug.Log(report);
            return report;
        }

        private static int PurgeLegacyObjectsInScenes()
        {
            string activeScenePath = SceneManager.GetActiveScene().path;
            int totalPurged = 0;

            foreach (string scenePath in OfficialCampaignScenes)
            {
                if (!File.Exists(scenePath)) continue;
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                List<GameObject> toDestroy = new List<GameObject>();

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    string rn = root.name;

                    // Destroy obsolete root containers completely
                    if (rn.StartsWith("THANH GIONG HERO V2") ||
                        rn.StartsWith("UNDERSTORY INSPIRED") ||
                        rn.StartsWith("UNDERSTORY VISUAL") ||
                        rn.StartsWith("Stylized Foliage Clusters"))
                    {
                        toDestroy.Add(root);
                        continue;
                    }

                    // Clean ARCHITECTURE root: keep Walkable Ground & colliders, remove disabled/empty decorative placeholders
                    if (rn.StartsWith("ARCHITECTURE"))
                    {
                        for (int i = root.transform.childCount - 1; i >= 0; i--)
                        {
                            Transform child = root.transform.GetChild(i);
                            if (child.name.Contains("Walkable Ground")) continue;
                            if (child.GetComponentInChildren<Collider>(true) != null) continue;
                            toDestroy.Add(child.gameObject);
                        }
                    }
                    // Clean Godot Legend Environment: keep ground/road/river surfaces and forge animation anchors, destroy disabled legacy props
                    else if (rn == "Godot Legend Environment")
                    {
                        for (int i = root.transform.childCount - 1; i >= 0; i--)
                        {
                            Transform child = root.transform.GetChild(i);
                            string cn = child.name.ToLowerInvariant();

                            // Keep ground, road, river, and forge motion hierarchy
                            if (cn.StartsWith("datlang") ||
                                cn.StartsWith("duongdatuon") ||
                                cn.StartsWith("dongsong") ||
                                cn.StartsWith("baichien") ||
                                cn.StartsWith("sandat") ||
                                cn.StartsWith("_meshinstance3d_") ||
                                cn.StartsWith("loren"))
                            {
                                if (cn.StartsWith("loren"))
                                {
                                    // Strip only disabled MeshRenderer/MeshFilter on forge while keeping WorkingHammer/FurnaceFlames/ChimneySmoke/HammerSparks
                                    foreach (MeshRenderer mr in child.GetComponentsInChildren<MeshRenderer>(true))
                                    {
                                        if (!mr.enabled || mr.forceRenderingOff)
                                        {
                                            MeshFilter mf = mr.GetComponent<MeshFilter>();
                                            if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
                                            UnityEngine.Object.DestroyImmediate(mr);
                                        }
                                    }
                                }
                                continue;
                            }

                            // Destroy all other hidden legacy props (NhaTranh, LuyTre, TreNga, grass_large, plant_bushLarge, crops_wheat, etc.)
                            toDestroy.Add(child.gameObject);
                        }
                    }
                    // Strip legacy SkinnedMeshRenderer meshes on hidden MountedGiong while preserving bone hierarchy for verifiers
                    else if (rn.StartsWith("Thánh Gióng"))
                    {
                        Transform legacyMounted = root.transform.Find("Visual/MountedGiong");
                        if (legacyMounted != null)
                        {
                            foreach (SkinnedMeshRenderer smr in legacyMounted.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            {
                                smr.enabled = false;
                                smr.forceRenderingOff = true;
                            }
                        }
                    }
                    // Clean GAMEPLAY - LUONG THUC & GAMEPLAY - TRE NGA hidden primitive children
                    else if (rn.StartsWith("GAMEPLAY - LUONG THUC") || rn.StartsWith("GAMEPLAY - TRE NGA"))
                    {
                        foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            if (mr.transform.GetComponentInParent<ThanhGiong.Environment.MeshyPropVfxAnimator>() != null)
                            {
                                continue;
                            }
                            if (!mr.enabled || mr.forceRenderingOff)
                            {
                                MeshFilter mf = mr.GetComponent<MeshFilter>();
                                if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
                                UnityEngine.Object.DestroyImmediate(mr);
                                totalPurged++;
                            }
                        }
                    }
                }

                foreach (GameObject go in toDestroy)
                {
                    if (go != null)
                    {
                        UnityEngine.Object.DestroyImmediate(go);
                        totalPurged++;
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }
            else if (OfficialCampaignScenes.Length > 0 && File.Exists(OfficialCampaignScenes[0]))
            {
                EditorSceneManager.OpenScene(OfficialCampaignScenes[0], OpenSceneMode.Single);
            }

            return totalPurged;
        }

        private static int OptimizeAllModelImporters()
        {
            int count = 0;
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Meshy", "Assets/Animations/ActionForge" });
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;

                bool changed = false;
                bool isAnimFbx = path.StartsWith("Assets/Animations/ActionForge");

                if (mi.isReadable)
                {
                    mi.isReadable = false;
                    changed = true;
                }
                if (mi.meshCompression != ModelImporterMeshCompression.Medium)
                {
                    mi.meshCompression = ModelImporterMeshCompression.Medium;
                    changed = true;
                }
                if (mi.importBlendShapes)
                {
                    mi.importBlendShapes = false;
                    changed = true;
                }
                if (mi.importCameras)
                {
                    mi.importCameras = false;
                    changed = true;
                }
                if (mi.importLights)
                {
                    mi.importLights = false;
                    changed = true;
                }
                if (isAnimFbx && mi.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    changed = true;
                }

                if (changed)
                {
                    mi.SaveAndReimport();
                    count++;
                }
            }
            return count;
        }

        private static int OptimizeAllTextureImporters()
        {
            int count = 0;
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Meshy" });
            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                bool isEnv = path.Contains("/Environment/");
                int targetMaxSize = isEnv ? 1024 : 2048;

                bool changed = false;
                if (ti.maxTextureSize != targetMaxSize)
                {
                    ti.maxTextureSize = targetMaxSize;
                    changed = true;
                }
                if (ti.textureCompression != TextureImporterCompression.Compressed)
                {
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    changed = true;
                }
                if (!ti.crunchedCompression)
                {
                    ti.crunchedCompression = true;
                    changed = true;
                }
                if (ti.compressionQuality != 75)
                {
                    ti.compressionQuality = 75;
                    changed = true;
                }
                if (!ti.mipmapEnabled)
                {
                    ti.mipmapEnabled = true;
                    changed = true;
                }

                if (changed)
                {
                    ti.SaveAndReimport();
                    count++;
                }
            }
            return count;
        }
    }
}
