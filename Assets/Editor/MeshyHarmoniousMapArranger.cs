using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThanhGiong.Editor
{
    public static class MeshyHarmoniousMapArranger
    {
        private const string EnvPrefabDir = "Assets/Prefabs/Meshy/Environment";
        private const string ContainerName = "MESHY 3D WORLD ENVIRONMENT & VFX";

        private static readonly string[] CampaignScenes =
        {
            "Assets/Scenes/ThanhGiongWorld/LangGiongTienTuyen.unity",
            "Assets/Scenes/ThanhGiongWorld/KinhThanhRenThep.unity",
            "Assets/Scenes/ThanhGiongWorld/PhaoDaiNgamQuanAn.unity",
            "Assets/Scenes/ThanhGiongWorld/ThungLungVuotSong.unity",
            "Assets/Scenes/ThanhGiongWorld/TranTuyenNuiSoc.unity",
            "Assets/Scenes/ThanhGiongWorld/DinhSocHoaThanh.unity"
        };

        private static Dictionary<string, GameObject> _prefabs;
        private static Material _riverWaterMat;

        [MenuItem("Thanh Giong/Meshy AI/Arrange All Maps Harmoniously (Bridges & Bamboo Groves)")]
        public static void ArrangeAllMapsMenu()
        {
            Debug.Log(ArrangeAllMaps());
        }

        public static string ArrangeAllMaps()
        {
            LoadPrefabsAndMaterials();

            int totalObjectsPlaced = 0;
            int totalBridges = 0;
            int totalBamboos = 0;

            foreach (string scenePath in CampaignScenes)
            {
                if (!System.IO.File.Exists(scenePath)) continue;

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                CleanUpCharacterAndEnemies(scene);
                FixInteractiveBambooGroves(scene, scene.name, ref totalBamboos);
                ArrangeFoodPickupsNeatly(scene);

                GameObject container = ResetEnvironmentContainer(scene);

                switch (scene.name)
                {
                    case "LangGiongTienTuyen":
                        BuildStage1_LangGiong(container, ref totalObjectsPlaced, ref totalBridges, ref totalBamboos);
                        break;
                    case "KinhThanhRenThep":
                        BuildStage2_KinhThanh(container, ref totalObjectsPlaced, ref totalBridges, ref totalBamboos);
                        break;
                    case "PhaoDaiNgamQuanAn":
                        BuildStage3_PhaoDai(container, ref totalObjectsPlaced, ref totalBridges, ref totalBamboos);
                        break;
                    case "ThungLungVuotSong":
                        BuildStage4_VuotSong(container, ref totalObjectsPlaced, ref totalBridges, ref totalBamboos);
                        break;
                    case "TranTuyenNuiSoc":
                        BuildStage5_NuiSoc(container, ref totalObjectsPlaced, ref totalBridges, ref totalBamboos);
                        break;
                    case "DinhSocHoaThanh":
                        BuildStage6_DinhSoc(container, ref totalObjectsPlaced, ref totalBridges, ref totalBamboos);
                        break;
                }

                AddRegionalAtmosphere(container, scene.name);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            return $"Arranged all 6 campaign maps harmoniously: {totalObjectsPlaced} grounded 3D objects, {totalBridges} river bridges, {totalBamboos} golden bamboo groves.";
        }

        private static void LoadPrefabsAndMaterials()
        {
            _prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            string[] keys =
            {
                "NhaTranh_LangGiong",
                "SanDinh_TrongDong",
                "LoRenThep_VanLang",
                "KhomTreNga_ThanThanh",
                "CongLang_ChienLuy",
                "CoLenh_PhuDong",
                "ThapCanh_DoanThanh",
                "LeuTrai_GiacAn",
                "CauGo_CoDai",
                "DaNui_RungThieng",
                "MamLuongThuc_LangGiong",
                "DenThieng_DinhSoc"
            };

            foreach (string k in keys)
            {
                string path = $"{EnvPrefabDir}/Meshy_{k}.prefab";
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (pf != null) _prefabs[k] = pf;
            }

            // Ensure river water material exists
            const string matPath = "Assets/Materials/Meshy/Environment/Mat_DongSong_NuocBiec.mat";
            _riverWaterMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (_riverWaterMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _riverWaterMat = new Material(sh)
                {
                    name = "Mat_DongSong_NuocBiec",
                    color = new Color(0.14f, 0.58f, 0.68f, 1f)
                };
                if (_riverWaterMat.HasProperty("_BaseColor"))
                    _riverWaterMat.SetColor("_BaseColor", new Color(0.14f, 0.58f, 0.68f, 1f));
                if (_riverWaterMat.HasProperty("_Smoothness"))
                    _riverWaterMat.SetFloat("_Smoothness", 0.85f);
                if (_riverWaterMat.HasProperty("_Metallic"))
                    _riverWaterMat.SetFloat("_Metallic", 0.15f);
                AssetDatabase.CreateAsset(_riverWaterMat, matPath);
            }
        }

        private static void CleanUpCharacterAndEnemies(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // Remove old disabled/missing visuals inside Thanh Giong root (never delete active Visual or Meshy_Mounted_ThanhGiong)
                if (root.name.StartsWith("Thánh Gióng", StringComparison.OrdinalIgnoreCase))
                {
                    var toDelete = new List<GameObject>();
                    foreach (Transform child in root.transform)
                    {
                        if (child.Find("Meshy_Mounted_ThanhGiong") != null) continue;
                        if (child.name.Contains("Previous Visual") || child.name.Contains("Missing Prefab"))
                        {
                            toDelete.Add(child.gameObject);
                        }
                    }
                    foreach (GameObject go in toDelete)
                        UnityEngine.Object.DestroyImmediate(go);
                }

                // Lift Quan An Gameplay visual meshes so they are not half-buried in the ground
                if (root.name == "GAMEPLAY - QUAN AN")
                {
                    foreach (Transform enemy in root.transform)
                    {
                        Transform vis = enemy.Find("Visual");
                        if (vis != null && vis.localPosition.y < 0.5f)
                        {
                            vis.localPosition = new Vector3(0f, 0.88f, 0f);
                        }
                        Transform spear = enemy.Find("Giáo");
                        if (spear != null && spear.localPosition.y < 0.8f)
                        {
                            spear.localPosition = new Vector3(0.55f, 1.15f, 0f);
                        }
                    }
                }
            }
        }

        private static void FixInteractiveBambooGroves(Scene scene, string sceneName, ref int totalBamboos)
        {
            GameObject treRoot = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == "GAMEPLAY - TRE NGA")
                {
                    treRoot = r;
                    break;
                }
            }

            if (treRoot == null)
            {
                treRoot = new GameObject("GAMEPLAY - TRE NGA");
                SceneManager.MoveGameObjectToScene(treRoot, scene);
            }

            // Desired positions for interactive bamboo groves along the main route in each scene
            Vector3[] interactivePositions;
            switch (sceneName)
            {
                case "LangGiongTienTuyen":
                    interactivePositions = new[]
                    {
                        new Vector3(-7.5f, 0f, 3.5f),
                        new Vector3(7.5f, 0f, 3.5f),
                        new Vector3(-7.5f, 0f, -11.0f),
                        new Vector3(7.5f, 0f, -11.0f)
                    };
                    break;
                case "KinhThanhRenThep":
                    interactivePositions = new[]
                    {
                        new Vector3(-7.5f, 0f, -30.5f),
                        new Vector3(7.5f, 0f, -30.5f),
                        new Vector3(-7.5f, 0f, -15.0f),
                        new Vector3(7.5f, 0f, -15.0f)
                    };
                    break;
                case "PhaoDaiNgamQuanAn":
                    interactivePositions = new[]
                    {
                        new Vector3(-8.5f, 0f, -1.0f),
                        new Vector3(13.5f, 0f, -1.0f),
                        new Vector3(-9.5f, 0f, 12.0f),
                        new Vector3(11.5f, 0f, 12.0f)
                    };
                    break;
                case "ThungLungVuotSong":
                    interactivePositions = new[]
                    {
                        new Vector3(-31.0f, 0f, -35.0f),
                        new Vector3(-16.5f, 0f, -35.0f),
                        new Vector3(-32.5f, 0f, -17.0f),
                        new Vector3(-15.5f, 0f, -17.0f)
                    };
                    break;
                case "TranTuyenNuiSoc":
                    interactivePositions = new[]
                    {
                        new Vector3(-8.5f, 0f, -29.0f),
                        new Vector3(8.5f, 0f, -29.0f),
                        new Vector3(-9.5f, 0f, -16.0f),
                        new Vector3(9.5f, 0f, -16.0f)
                    };
                    break;
                default: // DinhSocHoaThanh
                    interactivePositions = new[]
                    {
                        new Vector3(-14.5f, 0f, -23.0f),
                        new Vector3(2.5f, 0f, -23.0f),
                        new Vector3(-10.5f, 0f, -11.0f),
                        new Vector3(8.5f, 0f, -11.0f)
                    };
                    break;
            }

            // Fix existing children first
            for (int i = 0; i < interactivePositions.Length; i++)
            {
                GameObject groveObj;
                if (i < treRoot.transform.childCount)
                {
                    groveObj = treRoot.transform.GetChild(i).gameObject;
                }
                else
                {
                    // Duplicate first child if it has gameplay components, or create a new one
                    if (treRoot.transform.childCount > 0)
                    {
                        groveObj = UnityEngine.Object.Instantiate(treRoot.transform.GetChild(0).gameObject, treRoot.transform);
                    }
                    else
                    {
                        groveObj = new GameObject($"Khóm Tre Ngà {i + 1}");
                        groveObj.transform.SetParent(treRoot.transform, false);
                        var col = groveObj.AddComponent<BoxCollider>();
                        col.isTrigger = true;
                        col.size = new Vector3(3.2f, 4.0f, 3.2f);
                        col.center = new Vector3(0f, 2.0f, 0f);
                    }
                }

                groveObj.name = $"Khóm Tre Ngà {i + 1}";
                groveObj.transform.position = interactivePositions[i];
                groveObj.transform.rotation = Quaternion.Euler(0f, (i * 75f) % 360f, 0f);
                // Critical fix: reset squished (0.14, 2.80, 0.14) scale to (1, 1, 1)!
                groveObj.transform.localScale = Vector3.one;

                var mr = groveObj.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;

                var box = groveObj.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.size = new Vector3(3.2f, 4.0f, 3.2f);
                    box.center = new Vector3(0f, 2.0f, 0f);
                }
                var cap = groveObj.GetComponent<CapsuleCollider>();
                if (cap != null)
                {
                    cap.radius = 1.6f;
                    cap.height = 4.0f;
                    cap.center = new Vector3(0f, 2.0f, 0f);
                }

                // Replace MeshyVisual child with full-scale bamboo grove
                Transform oldVis = groveObj.transform.Find("MeshyVisual");
                if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);

                if (_prefabs.TryGetValue("KhomTreNga_ThanThanh", out GameObject bambooPf))
                {
                    GameObject vis = (GameObject)PrefabUtility.InstantiatePrefab(bambooPf, groveObj.transform);
                    vis.name = "MeshyVisual";
                    vis.transform.localPosition = Vector3.zero;
                    vis.transform.localRotation = Quaternion.identity;
                    vis.transform.localScale = Vector3.one * 0.85f;
                    StripColliders(vis);
                    totalBamboos++;
                }
            }
        }

        private static void ArrangeFoodPickupsNeatly(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "GAMEPLAY - LUONG THUC") continue;

                // Ground food pickups neatly at y = 0.20f and ensure MeshyVisual is properly scaled
                int idx = 0;
                foreach (Transform item in root.transform)
                {
                    Vector3 p = item.position;
                    item.position = new Vector3(p.x, 0.18f, p.z);
                    item.localScale = Vector3.one;

                    Transform vis = item.Find("MeshyVisual");
                    if (vis == null && _prefabs.TryGetValue("MamLuongThuc_LangGiong", out GameObject foodPf))
                    {
                        vis = ((GameObject)PrefabUtility.InstantiatePrefab(foodPf, item)).transform;
                        vis.name = "MeshyVisual";
                        StripColliders(vis.gameObject);
                    }
                    if (vis != null)
                    {
                        vis.localPosition = Vector3.zero;
                        vis.localRotation = Quaternion.Euler(0f, (idx * 45f) % 360f, 0f);
                        vis.localScale = Vector3.one * 0.72f;
                    }
                    idx++;
                }
            }
        }

        private static GameObject ResetEnvironmentContainer(Scene scene)
        {
            var toDestroy = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == ContainerName)
                    toDestroy.Add(root);
            }
            foreach (GameObject go in toDestroy)
                UnityEngine.Object.DestroyImmediate(go);

            var container = new GameObject(ContainerName);
            SceneManager.MoveGameObjectToScene(container, scene);
            return container;
        }

        // =====================================================================
        // STAGE 1: LÀNG GIÓNG TIỀN TUYẾN (Player at (0, 0, 7))
        // =====================================================================
        private static void BuildStage1_LangGiong(GameObject c, ref int total, ref int bridges, ref int bamboos)
        {
            // 1. Collider-backed landmarks (exact match with Godot Environment Collisions)
            Spawn(c, "NhaTranh_LangGiong", "NhaTranh_DongBac", new Vector3(20f, 0f, 17f), 210f, 1.0f, ref total);
            Spawn(c, "NhaTranh_LangGiong", "NhaTranh_TayBac", new Vector3(-20f, 0f, 18f), 150f, 1.0f, ref total);
            Spawn(c, "SanDinh_TrongDong", "SanDinhVaTrongDong_Chinh", new Vector3(16f, 0f, 5f), 270f, 1.0f, ref total);

            // 2. Harmonious Village Courtyard & Houses framing the road
            Spawn(c, "SanDinh_TrongDong", "SanDinh_Tay", new Vector3(-16f, 0f, 5f), 90f, 0.92f, ref total);
            Spawn(c, "LoRenThep_VanLang", "LoRenLang_Dong", new Vector3(15f, 0f, -6f), 270f, 0.95f, ref total);
            Spawn(c, "LoRenThep_VanLang", "LoRenLang_Tay", new Vector3(-15f, 0f, -6f), 90f, 0.95f, ref total);
            Spawn(c, "NhaTranh_LangGiong", "NhaTranh_BacGiua", new Vector3(0f, 0f, 27f), 180f, 1.0f, ref total);
            Spawn(c, "NhaTranh_LangGiong", "NhaTranh_NamDong", new Vector3(17f, 0f, -24f), 270f, 0.95f, ref total);
            Spawn(c, "NhaTranh_LangGiong", "NhaTranh_NamTay", new Vector3(-17f, 0f, -24f), 90f, 0.95f, ref total);

            // 3. Scenic Village River Stream (z = -15.5) & Wooden Bridge (CauGo_CoDai) across the main road
            CreateScenicRiver(c, "DongSong_SuoiLangGiong", new Vector3(0f, 0.04f, -15.5f), new Vector3(76f, 0.08f, 6.2f));
            SpawnBridge(c, "CauGo_QuaSongLangGiong", new Vector3(0f, -2.78f, -15.5f), 90f, new Vector3(2.15f, 0.95f, 2.1f), ref total, ref bridges);
            SpawnBridge(c, "CauGo_PhuTay", new Vector3(-22f, -2.78f, -15.5f), 90f, new Vector3(1.85f, 0.95f, 1.6f), ref total, ref bridges);

            // 4. Southern Village Gate & Watchtowers
            Spawn(c, "CongLang_ChienLuy", "CongLang_XuatQuan", new Vector3(0f, 0f, -34f), 0f, 1.05f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_CongTay", new Vector3(-11f, 0f, -33f), 0f, 0.90f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_CongDong", new Vector3(11f, 0f, -33f), 0f, 0.90f, ref total);

            // 5. Ceremonial War Flags along the road & bridge
            float[] flagZ = { 15f, 0f, -10f, -21f, -30f };
            foreach (float z in flagZ)
            {
                Spawn(c, "CoLenh_PhuDong", $"CoLenh_Tay_{z}", new Vector3(-5.5f, 0f, z), 90f, 0.90f, ref total);
                Spawn(c, "CoLenh_PhuDong", $"CoLenh_Dong_{z}", new Vector3(5.5f, 0f, z), 270f, 0.90f, ref total);
            }

            // 6. Lush Golden Bamboo Groves framing the village, riverbanks & road
            Vector3[] bambooCoords =
            {
                // Riverbank bamboo clusters (right next to the bridge!)
                new Vector3(-8.5f, 0f, -11.5f), new Vector3(8.5f, 0f, -11.5f),
                new Vector3(-8.5f, 0f, -19.5f), new Vector3(8.5f, 0f, -19.5f),
                new Vector3(-15.0f, 0f, -12.0f), new Vector3(15.0f, 0f, -12.0f),
                new Vector3(-15.0f, 0f, -19.0f), new Vector3(15.0f, 0f, -19.0f),
                // Village courtyard bamboo clusters
                new Vector3(-9.5f, 0f, 15.5f), new Vector3(9.5f, 0f, 15.5f),
                new Vector3(-11.0f, 0f, 10.0f), new Vector3(11.0f, 0f, 10.0f),
                new Vector3(-22.0f, 0f, 11.0f), new Vector3(22.0f, 0f, 11.0f),
                new Vector3(-22.0f, 0f, -1.0f), new Vector3(22.0f, 0f, -1.0f),
                // Southern road bamboo avenue
                new Vector3(-9.5f, 0f, -27.5f), new Vector3(9.5f, 0f, -27.5f),
                new Vector3(-22.0f, 0f, -29.0f), new Vector3(22.0f, 0f, -29.0f),
                // Outer village perimeter bamboo hedge
                new Vector3(-12.0f, 0f, 25.0f), new Vector3(12.0f, 0f, 25.0f),
                new Vector3(-28.0f, 0f, 20.0f), new Vector3(28.0f, 0f, 20.0f),
                new Vector3(-28.0f, 0f, 4.0f), new Vector3(28.0f, 0f, 4.0f),
                new Vector3(-28.0f, 0f, -12.0f), new Vector3(28.0f, 0f, -12.0f),
                new Vector3(-28.0f, 0f, -26.0f), new Vector3(28.0f, 0f, -26.0f)
            };
            SpawnBambooArray(c, bambooCoords, ref total, ref bamboos);

            // 7. Decorative Boulders along riverbanks & outer edges
            Vector3[] rocks =
            {
                new Vector3(-5.8f, 0f, -13.0f), new Vector3(5.8f, 0f, -13.0f),
                new Vector3(-5.8f, 0f, -18.0f), new Vector3(5.8f, 0f, -18.0f),
                new Vector3(-25.0f, 0f, 14.0f), new Vector3(25.0f, 0f, 14.0f),
                new Vector3(-25.0f, 0f, -22.0f), new Vector3(25.0f, 0f, -22.0f)
            };
            SpawnRockArray(c, rocks, ref total);
        }

        // =====================================================================
        // STAGE 2: KINH THÀNH RÈN THÉP (Player at (0, 0, -34))
        // =====================================================================
        private static void BuildStage2_KinhThanh(GameObject c, ref int total, ref int bridges, ref int bamboos)
        {
            // 1. Collider-backed landmarks
            Spawn(c, "LoRenThep_VanLang", "LoRenChinh_Dong", new Vector3(8.46f, 0f, -26.63f), 240f, 1.05f, ref total);
            Spawn(c, "LoRenThep_VanLang", "LoRenPhu_Tay", new Vector3(-8.37f, 0f, -21.63f), 120f, 1.05f, ref total);
            Spawn(c, "NhaTranh_LangGiong", "NhaThoRen_Dong", new Vector3(18f, 0f, -9f), 270f, 1.0f, ref total);
            Spawn(c, "NhaTranh_LangGiong", "NhaThoRen_Tay", new Vector3(-18f, 0f, -10f), 90f, 1.0f, ref total);

            // 2. Royal Citadel Canal (z = -14.5) & Grand Wooden Bridges
            CreateScenicRiver(c, "DongSong_HaoKinhThanh", new Vector3(0f, 0.04f, -14.5f), new Vector3(76f, 0.08f, 6.2f));
            SpawnBridge(c, "CauGo_KinhThanh_Chinh", new Vector3(0f, -2.78f, -14.5f), 90f, new Vector3(2.15f, 0.95f, 2.2f), ref total, ref bridges);
            SpawnBridge(c, "CauGo_KinhThanh_Tay", new Vector3(-25f, -2.78f, -14.5f), 90f, new Vector3(1.85f, 0.95f, 1.6f), ref total, ref bridges);
            SpawnBridge(c, "CauGo_KinhThanh_Dong", new Vector3(25f, -2.78f, -14.5f), 90f, new Vector3(1.85f, 0.95f, 1.6f), ref total, ref bridges);

            // 3. Citadel Gate, Additional Forges & Royal Pavilions
            Spawn(c, "CongLang_ChienLuy", "CongKinhThanh_Chinh", new Vector3(0f, 0f, -5.5f), 0f, 1.05f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_KinhThanh_Tay", new Vector3(-12f, 0f, -5.5f), 0f, 0.95f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_KinhThanh_Dong", new Vector3(12f, 0f, -5.5f), 0f, 0.95f, ref total);
            Spawn(c, "SanDinh_TrongDong", "DienTrieuDinh_Giua", new Vector3(0f, 0f, 8f), 180f, 1.08f, ref total);
            Spawn(c, "LoRenThep_VanLang", "LoRen_BacTay", new Vector3(-14f, 0f, 4f), 120f, 0.95f, ref total);
            Spawn(c, "LoRenThep_VanLang", "LoRen_BacDong", new Vector3(14f, 0f, 4f), 240f, 0.95f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_NamTay", new Vector3(-16f, 0f, -38f), 0f, 0.90f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_NamDong", new Vector3(16f, 0f, -38f), 0f, 0.90f, ref total);

            // 4. Royal Flags along the Forge Avenue
            float[] flagZ = { -36f, -28f, -19f, -10f, 0f };
            foreach (float z in flagZ)
            {
                Spawn(c, "CoLenh_PhuDong", $"CoLenh_Tay_{z}", new Vector3(-5.2f, 0f, z), 90f, 0.90f, ref total);
                Spawn(c, "CoLenh_PhuDong", $"CoLenh_Dong_{z}", new Vector3(5.2f, 0f, z), 270f, 0.90f, ref total);
            }

            // 5. Lush Bamboo Groves framing the Forge Courtyard & Canal Banks
            Vector3[] bambooCoords =
            {
                new Vector3(-9.5f, 0f, -38.0f), new Vector3(9.5f, 0f, -38.0f),
                new Vector3(-15.5f, 0f, -33.0f), new Vector3(15.5f, 0f, -33.0f),
                new Vector3(-16.5f, 0f, -25.0f), new Vector3(16.5f, 0f, -25.0f),
                // Canal bank bamboo groves
                new Vector3(-8.5f, 0f, -18.2f), new Vector3(8.5f, 0f, -18.2f),
                new Vector3(-8.5f, 0f, -10.8f), new Vector3(8.5f, 0f, -10.8f),
                new Vector3(-14.5f, 0f, -18.2f), new Vector3(14.5f, 0f, -18.2f),
                // Northern royal courtyard bamboo groves
                new Vector3(-22.0f, 0f, -3.0f), new Vector3(22.0f, 0f, -3.0f),
                new Vector3(-20.0f, 0f, 9.0f), new Vector3(20.0f, 0f, 9.0f),
                new Vector3(-10.0f, 0f, 15.0f), new Vector3(10.0f, 0f, 15.0f),
                // Outer perimeter
                new Vector3(-26.0f, 0f, -36.0f), new Vector3(26.0f, 0f, -36.0f),
                new Vector3(-26.0f, 0f, -20.0f), new Vector3(26.0f, 0f, -20.0f),
                new Vector3(-26.0f, 0f, 4.0f), new Vector3(26.0f, 0f, 4.0f)
            };
            SpawnBambooArray(c, bambooCoords, ref total, ref bamboos);

            Vector3[] rocks =
            {
                new Vector3(-5.8f, 0f, -17.0f), new Vector3(5.8f, 0f, -17.0f),
                new Vector3(-5.8f, 0f, -12.0f), new Vector3(5.8f, 0f, -12.0f),
                new Vector3(-22.0f, 0f, -30.0f), new Vector3(22.0f, 0f, -30.0f)
            };
            SpawnRockArray(c, rocks, ref total);
        }

        // =====================================================================
        // STAGE 3: PHÁO ĐÀI NGẦM QUÂN ÂN (Player at (8, 0, -8), Army at z = 3..10)
        // =====================================================================
        private static void BuildStage3_PhaoDai(GameObject c, ref int total, ref int bridges, ref int bamboos)
        {
            // 1. Collider-backed landmark: CongTraiGiacAn at (0.70, 0, 17.69)
            Spawn(c, "CongLang_ChienLuy", "CongTraiGiacAn_Chinh", new Vector3(0.70f, 0f, 17.69f), 0f, 1.05f, ref total);

            // 2. Fortress Moat River (z = -3.2) & Two Wooden Bridges leading into the Invader Camp
            CreateScenicRiver(c, "DongSong_HaoPhaoDai", new Vector3(0f, 0.04f, -3.2f), new Vector3(76f, 0.08f, 5.8f));
            SpawnBridge(c, "CauGo_PhaoDai_Chinh", new Vector3(0f, -2.78f, -3.2f), 90f, new Vector3(2.05f, 0.95f, 2.0f), ref total, ref bridges);
            SpawnBridge(c, "CauGo_PhaoDai_Dong", new Vector3(8.0f, -2.78f, -3.2f), 90f, new Vector3(2.05f, 0.95f, 2.0f), ref total, ref bridges);

            // 3. Symmetrical Invader War Tents & Watchtowers flanking the army formation
            float[] tentZ = { -12f, 2f, 11f };
            foreach (float z in tentZ)
            {
                Spawn(c, "LeuTrai_GiacAn", $"LeuTrai_Tay_{z}", new Vector3(-16.5f, 0f, z), 90f, 0.95f, ref total);
                Spawn(c, "LeuTrai_GiacAn", $"LeuTrai_Dong_{z}", new Vector3(17.5f, 0f, z), 270f, 0.95f, ref total);
            }
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_PhaoDai_Tay", new Vector3(-11.5f, 0f, 18.0f), 0f, 0.95f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_PhaoDai_Dong", new Vector3(12.5f, 0f, 18.0f), 0f, 0.95f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_TienTuyen_Tay", new Vector3(-18.0f, 0f, -18.0f), 0f, 0.90f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_TienTuyen_Dong", new Vector3(18.0f, 0f, -18.0f), 0f, 0.90f, ref total);

            // 4. War Banners
            float[] flagZ = { -15f, -7f, 1f, 15f };
            foreach (float z in flagZ)
            {
                Spawn(c, "CoLenh_PhuDong", $"CoTrai_Tay_{z}", new Vector3(-12.0f, 0f, z), 90f, 0.90f, ref total);
                Spawn(c, "CoLenh_PhuDong", $"CoTrai_Dong_{z}", new Vector3(13.0f, 0f, z), 270f, 0.90f, ref total);
            }

            // 5. Lush Golden Bamboo Groves framing the moat & fortress flanks
            Vector3[] bambooCoords =
            {
                // Moat bank bamboo groves
                new Vector3(-6.5f, 0f, -7.2f), new Vector3(14.5f, 0f, -7.2f),
                new Vector3(-12.5f, 0f, -7.2f), new Vector3(20.5f, 0f, -7.2f),
                new Vector3(-12.5f, 0f, 0.5f), new Vector3(15.5f, 0f, 0.5f),
                // Southern approach bamboo forest
                new Vector3(-8.0f, 0f, -16.0f), new Vector3(15.0f, 0f, -16.0f),
                new Vector3(-15.0f, 0f, -22.0f), new Vector3(8.0f, 0f, -22.0f),
                new Vector3(0.0f, 0f, -22.0f),
                // Fortress flank bamboo clusters
                new Vector3(-23.0f, 0f, -6.0f), new Vector3(24.0f, 0f, -6.0f),
                new Vector3(-23.0f, 0f, 6.0f), new Vector3(24.0f, 0f, 6.0f),
                new Vector3(-22.0f, 0f, 18.0f), new Vector3(23.0f, 0f, 18.0f),
                new Vector3(-8.0f, 0f, 25.0f), new Vector3(9.0f, 0f, 25.0f)
            };
            SpawnBambooArray(c, bambooCoords, ref total, ref bamboos);

            Vector3[] rocks =
            {
                new Vector3(-5.0f, 0f, -5.5f), new Vector3(13.0f, 0f, -5.5f),
                new Vector3(-15.0f, 0f, 15.0f), new Vector3(16.0f, 0f, 15.0f),
                new Vector3(-20.0f, 0f, -12.0f), new Vector3(21.0f, 0f, -12.0f)
            };
            SpawnRockArray(c, rocks, ref total);
        }

        // =====================================================================
        // STAGE 4: THUNG LŨNG VƯỢT SÔNG (Player at (-24, 0, -34), River DongSong at z = -30)
        // =====================================================================
        private static void BuildStage4_VuotSong(GameObject c, ref int total, ref int bridges, ref int bamboos)
        {
            // 1. Collider-backed landmarks:
            // CauGoQuaSong at (-24, 0, -30) spanning DongSong (z = -34..-26)
            SpawnBridge(c, "Meshy_CauGoQuaSong_CauGo_CoDai", new Vector3(-24.0f, -2.78f, -30.0f), 90f, new Vector3(2.35f, 0.95f, 2.25f), ref total, ref bridges);
            // Second eastern Wooden Bridge across DongSong
            SpawnBridge(c, "Meshy_CauGoQuaSong_Dong_CauGo_CoDai", new Vector3(-4.0f, -2.78f, -30.0f), 90f, new Vector3(2.35f, 0.95f, 2.05f), ref total, ref bridges);
            // Third far-east Wooden Bridge
            SpawnBridge(c, "Meshy_CauGoQuaSong_VienDong_CauGo_CoDai", new Vector3(12.0f, -2.78f, -30.0f), 90f, new Vector3(2.20f, 0.95f, 1.85f), ref total, ref bridges);

            // Collider-backed NhaTranh at (20, 0, -17)
            Spawn(c, "NhaTranh_LangGiong", "Meshy_NhaTranh_NhaTranh_LangGiong", new Vector3(20f, 0f, -17f), 240f, 1.0f, ref total);

            // 2. North Bank Invader Encampment (cleanly NORTH of z = -24, NEVER in the river!)
            Spawn(c, "LeuTrai_GiacAn", "LeuTrai_BoBac_1", new Vector3(-36.5f, 0f, -19.5f), 135f, 0.95f, ref total);
            Spawn(c, "LeuTrai_GiacAn", "LeuTrai_BoBac_2", new Vector3(-11.5f, 0f, -19.5f), 225f, 0.95f, ref total);
            Spawn(c, "LeuTrai_GiacAn", "LeuTrai_BoBac_3", new Vector3(-36.5f, 0f, -10.0f), 120f, 0.95f, ref total);
            Spawn(c, "LeuTrai_GiacAn", "LeuTrai_BoBac_4", new Vector3(-11.5f, 0f, -10.0f), 240f, 0.95f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_BoBac_Tay", new Vector3(-34.0f, 0f, -13.5f), 0f, 0.92f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_BoBac_Dong", new Vector3(-14.0f, 0f, -13.5f), 0f, 0.92f, ref total);
            Spawn(c, "CongLang_ChienLuy", "CongChienLuy_BoBac", new Vector3(-24.0f, 0f, -8.5f), 0f, 1.0f, ref total);

            // Eastern Village / Camp on North Bank
            Spawn(c, "SanDinh_TrongDong", "SanDinh_ThungLung", new Vector3(4.0f, 0f, -14.0f), 180f, 0.95f, ref total);
            Spawn(c, "NhaTranh_LangGiong", "NhaTranh_ThungLung_2", new Vector3(14.0f, 0f, -20.0f), 210f, 0.95f, ref total);

            // 3. War Flags flanking the Main River Bridge & Road
            Spawn(c, "CoLenh_PhuDong", "CoLenh_NamCau_Tay", new Vector3(-29.0f, 0f, -35.5f), 0f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoLenh_NamCau_Dong", new Vector3(-19.0f, 0f, -35.5f), 0f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoLenh_BacCau_Tay", new Vector3(-29.0f, 0f, -24.5f), 0f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoLenh_BacCau_Dong", new Vector3(-19.0f, 0f, -24.5f), 0f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoLenh_CauDong_1", new Vector3(-8.5f, 0f, -24.5f), 0f, 0.90f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoLenh_CauDong_2", new Vector3(0.5f, 0f, -24.5f), 0f, 0.90f, ref total);

            // 4. Lush Golden Bamboo Groves along South Bank & North Bank of the River
            Vector3[] bambooCoords =
            {
                // South riverbank (around player spawn (-24, 0, -34))
                new Vector3(-34.5f, 0f, -35.5f), new Vector3(-13.5f, 0f, -35.5f),
                new Vector3(-39.5f, 0f, -35.5f), new Vector3(-9.0f, 0f, -35.5f),
                new Vector3(-32.0f, 0f, -41.5f), new Vector3(-16.0f, 0f, -41.5f),
                new Vector3(-24.0f, 0f, -44.0f),
                new Vector3(2.0f, 0f, -36.0f), new Vector3(8.0f, 0f, -36.0f),
                // North riverbank
                new Vector3(-36.0f, 0f, -24.5f), new Vector3(-12.0f, 0f, -24.5f),
                new Vector3(-42.0f, 0f, -24.5f), new Vector3(-1.0f, 0f, -24.5f),
                new Vector3(6.5f, 0f, -24.5f), new Vector3(18.0f, 0f, -24.5f),
                // Valley road bamboo groves
                new Vector3(-33.0f, 0f, -6.0f), new Vector3(-15.0f, 0f, -6.0f),
                new Vector3(-29.0f, 0f, 2.0f), new Vector3(-19.0f, 0f, 2.0f),
                new Vector3(-8.0f, 0f, -12.0f), new Vector3(12.0f, 0f, -10.0f)
            };
            SpawnBambooArray(c, bambooCoords, ref total, ref bamboos);

            // 5. Riverbank Boulders (strictly on the banks z = -35.2 or z = -24.8, never in water)
            Vector3[] rocks =
            {
                new Vector3(-29.5f, 0f, -34.8f), new Vector3(-18.5f, 0f, -34.8f),
                new Vector3(-29.5f, 0f, -25.2f), new Vector3(-18.5f, 0f, -25.2f),
                new Vector3(-8.5f, 0f, -34.8f), new Vector3(0.5f, 0f, -34.8f),
                new Vector3(-40.0f, 0f, -16.0f), new Vector3(22.0f, 0f, -12.0f)
            };
            SpawnRockArray(c, rocks, ref total);
        }

        // =====================================================================
        // STAGE 5: TRẬN TUYẾN NÚI SÓC (Player at (0, 0, -38), Enemies at z = -28..-14)
        // =====================================================================
        private static void BuildStage5_NuiSoc(GameObject c, ref int total, ref int bridges, ref int bamboos)
        {
            // 1. Collider-backed landmark: ChienLuyNuiSoc at (0.70, 0, 23.69)
            Spawn(c, "CongLang_ChienLuy", "Meshy_ChienLuyNuiSoc_CongLang_ChienLuy", new Vector3(0.70f, 0f, 23.69f), 0f, 1.05f, ref total);

            // 2. Mountain Gorge River (z = -33.2) between Player (z = -38) and Enemies (z = -28)
            CreateScenicRiver(c, "DongSong_KheNuiSoc", new Vector3(0f, 0.04f, -33.2f), new Vector3(76f, 0.08f, 5.5f));
            SpawnBridge(c, "CauGo_NuiSoc_Chinh", new Vector3(0f, -2.78f, -33.2f), 90f, new Vector3(2.10f, 0.95f, 2.25f), ref total, ref bridges);
            SpawnBridge(c, "CauGo_NuiSoc_Tay", new Vector3(-18f, -2.78f, -33.2f), 90f, new Vector3(1.85f, 0.95f, 1.65f), ref total, ref bridges);
            SpawnBridge(c, "CauGo_NuiSoc_Dong", new Vector3(18f, -2.78f, -33.2f), 90f, new Vector3(1.85f, 0.95f, 1.65f), ref total, ref bridges);

            // 3. Symmetrical Mountain Pass Fortifications, Tents & Watchtowers
            float[] campZ = { -24f, -12f, 2f, 14f };
            foreach (float z in campZ)
            {
                Spawn(c, "LeuTrai_GiacAn", $"LeuTrai_NuiSoc_Tay_{z}", new Vector3(-17.5f, 0f, z), 90f, 0.95f, ref total);
                Spawn(c, "LeuTrai_GiacAn", $"LeuTrai_NuiSoc_Dong_{z}", new Vector3(17.5f, 0f, z), 270f, 0.95f, ref total);
            }
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_NuiSoc_NamTay", new Vector3(-13.5f, 0f, -18.0f), 0f, 0.95f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_NuiSoc_NamDong", new Vector3(13.5f, 0f, -18.0f), 0f, 0.95f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_NuiSoc_BacTay", new Vector3(-12.5f, 0f, 22.0f), 0f, 1.0f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapCanh_NuiSoc_BacDong", new Vector3(12.5f, 0f, 22.0f), 0f, 1.0f, ref total);

            // 4. War Banners lining the Mountain Pass
            float[] flagZ = { -36f, -30f, -18f, -6f, 8f, 20f };
            foreach (float z in flagZ)
            {
                Spawn(c, "CoLenh_PhuDong", $"CoLenh_Tay_{z}", new Vector3(-9.5f, 0f, z), 90f, 0.90f, ref total);
                Spawn(c, "CoLenh_PhuDong", $"CoLenh_Dong_{z}", new Vector3(9.5f, 0f, z), 270f, 0.90f, ref total);
            }

            // 5. Lush Golden Bamboo Groves framing the Gorge Bridge & Mountain Flanks
            Vector3[] bambooCoords =
            {
                // Gorge riverbank bamboo clusters
                new Vector3(-8.5f, 0f, -37.2f), new Vector3(8.5f, 0f, -37.2f),
                new Vector3(-14.5f, 0f, -37.2f), new Vector3(14.5f, 0f, -37.2f),
                new Vector3(-11.5f, 0f, -29.5f), new Vector3(11.5f, 0f, -29.5f),
                // Mountain pass bamboo groves
                new Vector3(-13.5f, 0f, -22.0f), new Vector3(13.5f, 0f, -22.0f),
                new Vector3(-13.5f, 0f, -8.0f), new Vector3(13.5f, 0f, -8.0f),
                new Vector3(-13.5f, 0f, 6.0f), new Vector3(13.5f, 0f, 6.0f),
                new Vector3(-15.5f, 0f, 18.0f), new Vector3(15.5f, 0f, 18.0f),
                // Outer mountain forest
                new Vector3(-24.0f, 0f, -28.0f), new Vector3(24.0f, 0f, -28.0f),
                new Vector3(-24.0f, 0f, -14.0f), new Vector3(24.0f, 0f, -14.0f),
                new Vector3(-24.0f, 0f, 0.0f), new Vector3(24.0f, 0f, 0.0f),
                new Vector3(-24.0f, 0f, 14.0f), new Vector3(24.0f, 0f, 14.0f)
            };
            SpawnBambooArray(c, bambooCoords, ref total, ref bamboos);

            // 6. Mountain Boulders
            Vector3[] rocks =
            {
                new Vector3(-21.0f, 0f, -24.0f), new Vector3(21.0f, 0f, -24.0f),
                new Vector3(-21.0f, 0f, -8.0f), new Vector3(21.0f, 0f, -8.0f),
                new Vector3(-21.0f, 0f, 10.0f), new Vector3(21.0f, 0f, 10.0f),
                new Vector3(-6.0f, 0f, -35.8f), new Vector3(6.0f, 0f, -35.8f)
            };
            SpawnRockArray(c, rocks, ref total);
        }

        // =====================================================================
        // STAGE 6: ĐỈNH SÓC HÓA THÁNH (Player at (-10, 0, -27), Shrine DinhSoc at (-1, 0, -15))
        // =====================================================================
        private static void BuildStage6_DinhSoc(GameObject c, ref int total, ref int bridges, ref int bamboos)
        {
            // 1. Collider-backed landmark: DinhSoc at (-1, 0, -15)
            Spawn(c, "DenThieng_DinhSoc", "Meshy_DinhSoc_DenThieng_DinhSoc", new Vector3(-1.0f, 0f, -15.0f), 180f, 1.05f, ref total);

            // 2. Sacred Mountain Stream (z = -24.5) & Ancient Wooden Bridge leading from Player (-10, 0, -27) to Shrine (-1, 0, -15)
            CreateScenicRiver(c, "DongSong_SuoiTienDinhSoc", new Vector3(-5.0f, 0.04f, -24.5f), new Vector3(72f, 0.08f, 5.2f));
            SpawnBridge(c, "CauGo_SuoiTien_Chinh", new Vector3(-6.5f, -2.78f, -24.5f), 90f, new Vector3(2.0f, 0.95f, 2.0f), ref total, ref bridges);
            SpawnBridge(c, "CauGo_SuoiTien_Dong", new Vector3(6.5f, -2.78f, -24.5f), 90f, new Vector3(1.9f, 0.95f, 1.8f), ref total, ref bridges);

            // 3. Upper Ascension Sanctuary Temple & Grounded Ceremonial Pavilions (NO floating y=10.5 objects!)
            Spawn(c, "DenThieng_DinhSoc", "DenThuong_HoaThanh", new Vector3(-1.0f, 0f, 4.0f), 180f, 1.15f, ref total);
            Spawn(c, "SanDinh_TrongDong", "SanTrongDong_Tay", new Vector3(-15.0f, 0f, -14.0f), 90f, 0.90f, ref total);
            Spawn(c, "SanDinh_TrongDong", "SanTrongDong_Dong", new Vector3(13.0f, 0f, -14.0f), 270f, 0.90f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapChuong_Tay", new Vector3(-15.0f, 0f, 2.0f), 0f, 0.92f, ref total);
            Spawn(c, "ThapCanh_DoanThanh", "ThapChuong_Dong", new Vector3(13.0f, 0f, 2.0f), 0f, 0.92f, ref total);
            Spawn(c, "CongLang_ChienLuy", "CongTamQuan_DinhSoc", new Vector3(-6.5f, 0f, -35.0f), 0f, 1.0f, ref total);

            // 4. Sacred Phù Đổng Banners lining the Ascension Path
            Spawn(c, "CoLenh_PhuDong", "CoThieng_CauTay", new Vector3(-11.0f, 0f, -28.0f), 90f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoThieng_CauDong", new Vector3(-2.0f, 0f, -28.0f), 270f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoThieng_DenTay", new Vector3(-6.5f, 0f, -19.5f), 90f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoThieng_DenDong", new Vector3(4.5f, 0f, -19.5f), 270f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoThieng_DinhTay", new Vector3(-7.0f, 0f, -4.0f), 90f, 0.95f, ref total);
            Spawn(c, "CoLenh_PhuDong", "CoThieng_DinhDong", new Vector3(5.0f, 0f, -4.0f), 270f, 0.95f, ref total);

            // 5. Lush Golden Bamboo Groves framing the Sacred Stream, Bridge & Ascension Altar
            Vector3[] bambooCoords =
            {
                // Stream & bridge bamboo groves
                new Vector3(-15.5f, 0f, -25.5f), new Vector3(1.5f, 0f, -25.5f),
                new Vector3(-16.5f, 0f, -18.5f), new Vector3(11.5f, 0f, -18.5f),
                new Vector3(-19.5f, 0f, -31.0f), new Vector3(6.5f, 0f, -31.0f),
                // Around Dinh Soc Shrine
                new Vector3(-10.5f, 0f, -8.5f), new Vector3(8.5f, 0f, -8.5f),
                new Vector3(-18.5f, 0f, -7.5f), new Vector3(16.5f, 0f, -7.5f),
                // Upper sanctuary bamboo semicircle
                new Vector3(-11.0f, 0f, 9.0f), new Vector3(9.0f, 0f, 9.0f),
                new Vector3(-19.0f, 0f, 8.0f), new Vector3(17.0f, 0f, 8.0f),
                new Vector3(-5.0f, 0f, 14.0f), new Vector3(3.0f, 0f, 14.0f),
                // Outer peak bamboo ring
                new Vector3(-25.0f, 0f, -20.0f), new Vector3(21.0f, 0f, -20.0f),
                new Vector3(-25.0f, 0f, -6.0f), new Vector3(21.0f, 0f, -6.0f),
                new Vector3(-25.0f, 0f, 6.0f), new Vector3(21.0f, 0f, 6.0f)
            };
            SpawnBambooArray(c, bambooCoords, ref total, ref bamboos);

            // 6. Grounded Sacred Mountain Boulders (y = 0, NEVER floating!)
            Vector3[] rocks =
            {
                new Vector3(-12.0f, 0f, -19.5f), new Vector3(2.0f, 0f, -19.5f),
                new Vector3(-22.0f, 0f, -14.0f), new Vector3(18.0f, 0f, -14.0f),
                new Vector3(-22.0f, 0f, 0.0f), new Vector3(18.0f, 0f, 0.0f),
                new Vector3(-14.0f, 0f, 13.0f), new Vector3(12.0f, 0f, 13.0f)
            };
            SpawnRockArray(c, rocks, ref total);
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static GameObject Spawn(GameObject parent, string key, string name, Vector3 pos, float rotY, float uniformScale, ref int total)
        {
            if (!_prefabs.TryGetValue(key, out GameObject pf) || pf == null) return null;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent.transform);
            inst.name = $"Meshy_{name}_{key}";
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            inst.transform.localScale = Vector3.one * uniformScale;
            StripColliders(inst);
            total++;
            return inst;
        }

        private static GameObject SpawnBridge(GameObject parent, string name, Vector3 pos, float rotY, Vector3 scale, ref int total, ref int bridges)
        {
            if (!_prefabs.TryGetValue("CauGo_CoDai", out GameObject pf) || pf == null) return null;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent.transform);
            inst.name = name;
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            inst.transform.localScale = scale;
            StripColliders(inst);
            total++;
            bridges++;
            return inst;
        }

        private static void SpawnBambooArray(GameObject parent, Vector3[] coords, ref int total, ref int bamboos)
        {
            for (int i = 0; i < coords.Length; i++)
            {
                float rotY = (i * 53f) % 360f;
                float scale = 0.88f + (i % 4) * 0.07f;
                Spawn(parent, "KhomTreNga_ThanThanh", $"LuyTre_{i + 1:00}", coords[i], rotY, scale, ref total);
                bamboos++;
            }
        }

        private static void SpawnRockArray(GameObject parent, Vector3[] coords, ref int total)
        {
            for (int i = 0; i < coords.Length; i++)
            {
                float rotY = (i * 67f) % 360f;
                float scale = 0.85f + (i % 3) * 0.12f;
                Spawn(parent, "DaNui_RungThieng", $"DaNui_{i + 1:00}", coords[i], rotY, scale, ref total);
            }
        }

        private static void CreateScenicRiver(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            GameObject river = GameObject.CreatePrimitive(PrimitiveType.Cube);
            river.name = name;
            river.transform.SetParent(parent.transform, false);
            river.transform.position = center;
            river.transform.localScale = size;

            var col = river.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            var mr = river.GetComponent<MeshRenderer>();
            if (mr != null && _riverWaterMat != null)
            {
                mr.sharedMaterial = _riverWaterMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void AddRegionalAtmosphere(GameObject container, string sceneName)
        {
            var go = new GameObject("Meshy_RegionalAtmosphere_VFX");
            go.transform.SetParent(container.transform, false);
            go.transform.position = new Vector3(0f, 3.5f, -15f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.maxParticles = 90;

            Color c1 = new Color(1.0f, 0.88f, 0.38f, 0.65f);
            Color c2 = new Color(0.55f, 0.92f, 0.45f, 0.50f);
            if (sceneName.Contains("KinhThanh") || sceneName.Contains("PhaoDai") || sceneName.Contains("NuiSoc"))
            {
                c1 = new Color(1.0f, 0.48f, 0.15f, 0.75f);
                c2 = new Color(1.0f, 0.80f, 0.25f, 0.65f);
            }
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(65f, 6f, 65f);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (pShader != null)
            {
                var mat = new Material(pShader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c1);
                psr.sharedMaterial = mat;
            }
        }

        private static void StripColliders(GameObject root)
        {
            foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(col);
            }
        }
    }
}
