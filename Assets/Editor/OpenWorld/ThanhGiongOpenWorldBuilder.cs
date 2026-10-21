using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Authors the seamless world from the six existing scenes. Source scenes are only opened
/// additively and are never saved. There are no InitializeOnLoad hooks: regeneration is explicit.
/// </summary>
public static class ThanhGiongOpenWorldBuilder
{
    public const string OutputPath = "Assets/Scenes/ThanhGiongWorld/ThanhGiongDaiViet.unity";
    private const string SceneFolder = "Assets/Scenes/ThanhGiongWorld/";
    private const string Generated = "Assets/Generated/OpenWorld";
    private const string Verification = "OpenWorldVerification";
    private const float West = -430f, East = 300f, South = -420f, North = 300f;
    private const float RoadWidth = 12f;
    private static readonly List<OpenWorldZone> Plan = CreatePlan();
    private static readonly List<OpenWorldConnection> Links = CreateConnections();
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static readonly List<OpenWorldEncounter> Encounters = new List<OpenWorldEncounter>();
    private static readonly List<Transform> Scenery = new List<Transform>();
    private static readonly List<Vector3> ReservedResourcePositions = new List<Vector3>();
    private static readonly Vector3[] AuthoredClearings = { new Vector3(-55, 0, 0), new Vector3(75, 0, 0), new Vector3(-310, 0, 160), new Vector3(-310, 0, 0), new Vector3(-310, 0, -160), new Vector3(-150, 0, -290) };
    private static readonly StringBuilder Report = new StringBuilder();
    private static System.Random random;
    private static Scene destination;
    private static Transform worldRoot, battleRoot, bambooRoot;
    private static ThanhGiongCampaignController campaign;
    private static int resourceCount, sceneryCount, corridorColliders, importedRoots, relocatedProps;

    [MenuItem("Tools/Thanh Giong/Open World/Build Seamless Dai Viet World")]
    public static void BuildFromMenu() { Debug.Log(Build()); }

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play Mode and finish compilation before building the world.");
        for (int i = 0; i < SceneManager.sceneCount; i++) {
            var s = SceneManager.GetSceneAt(i);
            if (s.isDirty && s.path != OutputPath)
                throw new InvalidOperationException("Save or discard all currently dirty scenes before building. No source scenes were changed.");
        }

        string[] sources = { "LangGiongTienTuyen", "KinhThanhRenThep", "PhaoDaiNgamQuanAn", "ThungLungVuotSong", "TranTuyenNuiSoc", "DinhSocHoaThanh" };
        foreach (var source in sources)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFolder + source + ".unity") == null)
                throw new FileNotFoundException("Required source scene is missing: " + source);
        EnsureAssetFolder(Generated);
        Directory.CreateDirectory(Verification);
        BackupOutputAndBuildSettings();
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Meshy/Environment/Meshy_DamSenCo_VanLang.prefab") == null ||
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Meshy/Environment/Mat_DongSong_NuocBiec.mat") == null) {
            ThanhGiong.EditorTools.MeshyEnvironmentInstaller.BuildAllPrefabsAndMaterialsOnly();
        }
        Materials.Clear(); Encounters.Clear(); Scenery.Clear(); ReservedResourcePositions.Clear(); Report.Clear();
        resourceCount = sceneryCount = corridorColliders = importedRoots = relocatedProps = 0;
        random = new System.Random(15102026);
        Report.AppendLine("THÁNH GIÓNG – ĐẠI VIỆT LIỀN MẠCH (ZELDA × MESHY AI EDITION)");
        Report.AppendLine("Deterministic world seed: 15102026. Sources are opened read-only and never saved.");
        Dictionary<string, string> sourceHashes = sources.ToDictionary(s => s, s => HashFile(SceneFolder + s + ".unity"));
        try {
            EditorUtility.DisplayProgressBar("Đại Việt", "Copying the village into a new scene…", .02f);
            destination = EditorSceneManager.OpenScene(SceneFolder + sources[0] + ".unity", OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(destination, OutputPath, false))
                throw new IOException("Could not save the village copy at " + OutputPath);
            SceneManager.SetActiveScene(destination);
            campaign = FindInScene<ThanhGiongCampaignController>(destination).FirstOrDefault();
            if (campaign == null) throw new InvalidOperationException("The village source has no campaign player.");
            worldRoot = NewRoot("ĐẠI VIỆT · 13 VÙNG LIỀN MẠCH").transform;
            battleRoot = NewRoot("OPEN WORLD · ENCOUNTERS").transform;
            bambooRoot = NewRoot("OPEN WORLD · ALL BAMBOO GROVES").transform;
            var villageOffset = new Vector3(-55f, 0f, 0f);
            var baseRoots = destination.GetRootGameObjects();
            var village = ZoneRoot("hub", "Làng Phù Đổng · làng cũ");
            ExtractGameplay(campaign, "hub", villageOffset);
            MoveEnvironmentRoots(baseRoots, village, villageOffset, campaign, true);
            campaign.transform.position += villageOffset;
            campaign.startingChapter = ThanhGiongCampaignController.Chapter.Prologue;
            var baseCamera = FindInScene<IsometricCameraFollow>(destination).FirstOrDefault();
            if (baseCamera != null) {
                baseCamera.target = campaign.transform;
                baseCamera.targetBody = campaign.GetComponent<Rigidbody>();
                baseCamera.transform.position += villageOffset;
                var camera = baseCamera.GetComponent<Camera>();
                if (camera != null) {
                    camera.farClipPlane = 750f;
                    camera.backgroundColor = new Color(0.66f, 0.83f, 0.96f);
                }
            }

            ImportSource(sources[1], "hub", new Vector3(75f, 0f, 0f), false);
            ImportSource(sources[2], "red_fort", new Vector3(-310f, 0f, 160f), false);
            ImportSource(sources[3], "red_river", new Vector3(-310f, 0f, 0f), false);
            ImportSource(sources[4], "black_battle", new Vector3(-310f, 0f, -160f), false);
            Vector3 peakPlayer = ImportSource(sources[5], "soc_peak", new Vector3(-150f, 0f, -290f), true);
            campaign.battleRoot = battleRoot.gameObject;
            campaign.bambooRoot = bambooRoot.gameObject;
            HarmonizeAuthoredClearings();
            ConfigureZeldaAtmosphere();
            BuildYellowEncounters();
            EditorUtility.DisplayProgressBar("Đại Việt", "Authoring continuous Zelda terrain and connected roads…", .58f);
            BuildTerrain();
            BuildRoads();
            BuildCoastAndBorders();
            BuildZonesAndResources();
            BuildForge();
            ClearRoadVisuals();
            ClearRoadCorridors();
            CleanLegacyComponents();

            var world = campaign.GetComponent<ThanhGiongOpenWorld>();
            if (world == null) world = campaign.gameObject.AddComponent<ThanhGiongOpenWorld>();
            world.campaign = campaign; world.player = campaign.transform;
            world.safeSpawn = campaign.transform.position;
            world.zones = CreatePlan(); world.connections = CreateConnections();
            var seamless = campaign.GetComponent<ThanhGiongSeamlessCampaign>();
            if (seamless == null) seamless = campaign.gameObject.AddComponent<ThanhGiongSeamlessCampaign>();
            seamless.world = world; seamless.campaign = campaign;
            seamless.forgeAnchor = new Vector3(75f, 0f, -34f);
            seamless.peakAnchor = peakPlayer;
            seamless.encounters = Encounters.ToArray();
            // Authored towns and cloud physics remain active. Only generated decorative rings cull.
            seamless.regionalScenery = Array.Empty<Transform>();
            seamless.persistenceEnabled = true;
            seamless.restoreSaveOnStart = true;
            campaign.openWorldCampaign = seamless;
            battleRoot.gameObject.SetActive(false);
            bambooRoot.gameObject.SetActive(false);

            WritePlanAsset();
            Report.AppendLine(ThanhGiongWorldSolids.Repair(destination));
            ValidateAuthoredScene(world, sources, sourceHashes);
            EditorSceneManager.MarkSceneDirty(destination);
            if (!EditorSceneManager.SaveScene(destination, OutputPath, false)) throw new IOException("Final scene save failed.");
            AssetDatabase.SaveAssets();
            SetBuildScene();
            Report.AppendLine("Result: authored and saved " + OutputPath);
            Report.AppendLine("13 metadata zones; " + Links.Count + " physical road connections; " + resourceCount + " gathering nodes.");
            Report.AppendLine(Encounters.Count + " encounter groups; " + battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true).Length + " enemies; " + sceneryCount + " generated decorations using Meshy AI & painterly art.");
            Report.AppendLine(importedRoots + " imported environment/gameplay roots; " + corridorColliders + " authored obstacle colliders disabled inside marked road corridors.");
            Report.AppendLine(relocatedProps + " individual authored props moved out of road corridors; combined environments and floors remain intact.");
            Report.AppendLine("Legacy checkpoint components disabled. No PlayerPrefs calls. No NavMesh bake. One enabled build scene.");
            Report.AppendLine("Editor authoring checks passed. Play Mode, traversal, progression, performance and screenshots require coordinator verification.");
            File.WriteAllText(Verification + "/build-report.txt", Report.ToString(), new UTF8Encoding(false));
            return Report.ToString();
        } catch (Exception exception) {
            Report.AppendLine("BUILD FAILED: " + exception);
            File.WriteAllText(Verification + "/build-report.txt", Report.ToString(), new UTF8Encoding(false));
            throw;
        } finally { EditorUtility.ClearProgressBar(); }
    }

    private static List<OpenWorldZone> CreatePlan()
    {
        var list = new List<OpenWorldZone>();
        Add(list, "hub", "Làng Phù Đổng · Kinh thành Phong Châu", 15, 0, 260, 150, OpenWorldRisk.Safe, OpenWorldBiome.Steppe, 0);
        Add(list, "blue_forest", "Rừng Tre Đầu Làng", -150, 0, 120, 120, OpenWorldRisk.Blue, OpenWorldBiome.Forest, 0);
        Add(list, "blue_steppe", "Bờ Sông Hồng", -75, -145, 120, 120, OpenWorldRisk.Blue, OpenWorldBiome.Steppe, 0);
        Add(list, "blue_swamp", "Đầm Sen Văn Lang", 90, -145, 120, 120, OpenWorldRisk.Blue, OpenWorldBiome.Swamp, 0);
        Add(list, "blue_highland", "Gò Đá Đông Làng", 155, 0, 120, 120, OpenWorldRisk.Blue, OpenWorldBiome.Highland, 0);
        Add(list, "blue_mountain", "Mỏ Sắt Phong Châu", 70, 145, 120, 120, OpenWorldRisk.Blue, OpenWorldBiome.Mountain, 0);
        Add(list, "yellow_mountain", "Đèo Quặng Tranh Chấp", 190, 190, 120, 120, OpenWorldRisk.Yellow, OpenWorldBiome.Mountain, 2);
        Add(list, "yellow_highland", "Đồi Canh Tiền Tiêu", -150, 160, 120, 120, OpenWorldRisk.Yellow, OpenWorldBiome.Highland, 2);
        Add(list, "yellow_steppe", "Đồng Lúa Bị Cướp", 0, -290, 120, 120, OpenWorldRisk.Yellow, OpenWorldBiome.Steppe, 2);
        Add(list, "red_fort", "Pháo Đài Quân Ân", -310, 160, 125, 125, OpenWorldRisk.Red, OpenWorldBiome.Highland, 2);
        Add(list, "red_river", "Thung Lũng Vượt Sông", -310, 0, 120, 120, OpenWorldRisk.Red, OpenWorldBiome.Swamp, 2);
        Add(list, "black_battle", "Đại Bản Doanh Giặc Ân", -310, -160, 150, 150, OpenWorldRisk.Black, OpenWorldBiome.Highland, 2);
        Add(list, "soc_peak", "Núi Sóc · Đường Hóa Thánh", -150, -290, 135, 135, OpenWorldRisk.Black, OpenWorldBiome.Mountain, 3);
        return list;
    }

    private static void Add(List<OpenWorldZone> list, string id, string name, float x, float z, float sx, float sz, OpenWorldRisk risk, OpenWorldBiome biome, int chapter)
    {
        list.Add(new OpenWorldZone { id = id, displayName = name, center = new Vector3(x, 0, z), size = new Vector3(sx, 1000, sz), risk = risk, biome = biome, chapter = chapter });
    }

    private static List<OpenWorldConnection> CreateConnections()
    {
        string[] pairs = { "hub blue_forest", "hub blue_steppe", "hub blue_swamp", "hub blue_highland", "hub blue_mountain", "blue_forest yellow_highland", "blue_forest red_river", "blue_mountain yellow_mountain", "blue_mountain yellow_highland", "blue_highland yellow_mountain", "blue_steppe yellow_steppe", "blue_swamp yellow_steppe", "yellow_highland red_fort", "red_fort red_river", "red_river black_battle", "yellow_steppe black_battle", "yellow_steppe soc_peak", "black_battle soc_peak" };
        return pairs.Select(pair => { var ids = pair.Split(' '); return new OpenWorldConnection(ids[0], ids[1]); }).ToList();
    }

    private static Vector3 ImportSource(string name, string zoneId, Vector3 offset, bool ascension)
    {
        EditorUtility.DisplayProgressBar("Đại Việt", "Preserving " + name + "…", .12f + importedRoots * .006f);
        var source = EditorSceneManager.OpenScene(SceneFolder + name + ".unity", OpenSceneMode.Additive);
        try {
            var actor = FindInScene<ThanhGiongCampaignController>(source).FirstOrDefault();
            var root = ZoneRoot(zoneId, name);
            Vector3 playerPosition = actor != null ? actor.transform.position + offset : offset;
            Transform cloudTarget = ascension && actor != null ? actor.ascensionTarget : null;
            if (actor != null) ExtractGameplay(actor, zoneId, offset);
            MoveEnvironmentRoots(source.GetRootGameObjects(), root, offset, actor, false);
            if (cloudTarget != null) {
                if (cloudTarget.gameObject.scene == source) MoveObject(cloudTarget.gameObject, root, offset);
                campaign.ascensionTarget = cloudTarget;
            }
            Report.AppendLine(name + " → " + zoneId + " offset " + offset + "; player anchor " + playerPosition);
            return playerPosition;
        } finally {
            // Discard duplicate players, presentation, camera, sun, HUD and old systems, without saving.
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(destination);
        }
    }

    private static void ExtractGameplay(ThanhGiongCampaignController actor, string zoneId, Vector3 offset)
    {
        if (actor.battleRoot != null && actor.battleRoot != actor.gameObject) {
            var group = new GameObject("Encounter · " + zoneId);
            SceneManager.MoveGameObjectToScene(group, destination); group.transform.SetParent(battleRoot, false);
            var encounter = group.AddComponent<OpenWorldEncounter>(); encounter.zoneId = zoneId; encounter.content = group;
            Encounters.Add(encounter);
            MoveObject(actor.battleRoot, group.transform, offset);
            actor.battleRoot.SetActive(true);
        }
        if (actor.bambooRoot != null && actor.bambooRoot != actor.gameObject) {
            MoveObject(actor.bambooRoot, bambooRoot, offset);
            actor.bambooRoot.SetActive(true);
        }
    }

    private static void MoveEnvironmentRoots(GameObject[] roots, Transform zoneRoot, Vector3 offset, ThanhGiongCampaignController actor, bool isBase)
    {
        foreach (var root in roots) {
            if (root == null || root == worldRoot.gameObject || root == battleRoot.gameObject || root == bambooRoot.gameObject) continue;
            if (root.transform.parent != null) continue; // Gameplay extracted above is already parented.
            if (actor != null && (root == actor.transform.root.gameObject || root.GetComponentInChildren<ThanhGiongCampaignController>(true) != null)) continue;
            string n = root.name;
            if (n.IndexOf("NavMesh", StringComparison.OrdinalIgnoreCase) >= 0) { Object.DestroyImmediate(root); continue; }
            bool bamboo = n.IndexOf("TRE NGA", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("BAMBOO RESUPPLY", StringComparison.OrdinalIgnoreCase) >= 0;
            if (bamboo) { MoveObject(root, bambooRoot, offset); root.SetActive(true); continue; }
            if (root.GetComponentInChildren<ThanhGiongEnemy>(true) != null && !n.StartsWith("ARCHITECTURE", StringComparison.Ordinal) && !n.StartsWith("MESHY", StringComparison.Ordinal)) {
                var group = new GameObject("Encounter · " + ZoneId(zoneRoot)); SceneManager.MoveGameObjectToScene(group, destination); group.transform.SetParent(battleRoot, false);
                var encounter = group.AddComponent<OpenWorldEncounter>(); encounter.zoneId = ZoneId(zoneRoot); encounter.content = group; Encounters.Add(encounter);
                MoveObject(root, group.transform, offset); root.SetActive(true); continue;
            }
            bool world = n.StartsWith("ARCHITECTURE", StringComparison.Ordinal) || n.StartsWith("GAMEPLAY", StringComparison.Ordinal) ||
                n.StartsWith("COMBAT", StringComparison.Ordinal) || n.StartsWith("DOI QUAN", StringComparison.Ordinal) ||
                n.StartsWith("Godot Legend Environment", StringComparison.Ordinal) || n.StartsWith("Godot Environment", StringComparison.Ordinal) ||
                n.StartsWith("MESHY 3D WORLD", StringComparison.Ordinal) || n.IndexOf("Cloud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Stairway", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("hóa thánh", StringComparison.OrdinalIgnoreCase) >= 0;
            if (world) MoveObject(root, zoneRoot, offset);
            // Base globals stay at scene root. Imported globals stay in their source and get discarded.
        }
    }

    private static void HarmonizeAuthoredClearings()
    {
        Physics.SyncTransforms();
        Material waterMat = ZeldaWaterMaterial();
        int hiddenPerimeterWalls = 0, blendedGroundSlabs = 0, waterUpgrades = 0;
        foreach (var tr in worldRoot.GetComponentsInChildren<Transform>(true)) {
            if (tr == null) continue;
            string n = tr.name;
            if (n.StartsWith("Godot Legend Environment", StringComparison.Ordinal)) {
                foreach (Transform child in tr) {
                    if (child.name.IndexOf("DongSong", StringComparison.OrdinalIgnoreCase) >= 0) {
                        var mr = child.GetComponent<MeshRenderer>();
                        if (mr != null && waterMat != null) { mr.sharedMaterial = waterMat; waterUpgrades++; }
                    } else {
                        foreach (var r in child.GetComponentsInChildren<Renderer>(true)) {
                            if (r.enabled) { r.enabled = false; blendedGroundSlabs++; }
                        }
                    }
                }
                continue;
            }
            if (n.StartsWith("Godot Environment Collisions", StringComparison.Ordinal)) {
                foreach (Transform child in tr) {
                    if (child.name.IndexOf("GioiHanMap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.StartsWith("@MeshInstance3D@", StringComparison.Ordinal) ||
                        child.name.StartsWith("_MeshInstance3D_", StringComparison.Ordinal)) {
                        foreach (var c in child.GetComponentsInChildren<Collider>(true)) {
                            if (c.enabled) { c.enabled = false; hiddenPerimeterWalls++; }
                        }
                    }
                }
                continue;
            }
            if (n.IndexOf("DongSong", StringComparison.OrdinalIgnoreCase) >= 0) {
                var mr = tr.GetComponent<MeshRenderer>();
                if (mr != null && waterMat != null) { mr.sharedMaterial = waterMat; waterUpgrades++; }
            }
            if (n == "WarmGlowLight") {
                var light = tr.GetComponent<Light>();
                if (light != null) {
                    light.range = Mathf.Max(light.range, 10.5f);
                    light.intensity = Mathf.Max(light.intensity, 1.85f);
                }
            }
        }
        Report.AppendLine("Harmonized authored clearings: " + hiddenPerimeterWalls + " legacy single-scene boundary wall colliders disabled; " + blendedGroundSlabs + " legacy Godot flat slabs/walls hidden; " + waterUpgrades + " river segments upgraded to Zelda water.");
    }

    private static void ConfigureZeldaAtmosphere()
    {
        var sun = FindInScene<Light>(destination).FirstOrDefault(l => l.type == LightType.Directional && l.enabled);
        if (sun != null) {
            sun.color = new Color(1.00f, 0.95f, 0.82f);
            sun.intensity = 1.38f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.70f, 0.85f, 0.96f);
        RenderSettings.fogStartDistance = 135f;
        RenderSettings.fogEndDistance = 620f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.74f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.70f, 0.68f, 0.56f);
        RenderSettings.ambientGroundColor = new Color(0.34f, 0.38f, 0.26f);
    }

    private static string ZoneId(Transform region)
    {
        string[] parts = region.name.Split(new[] { " · " }, StringSplitOptions.None);
        return parts.Length > 1 ? parts[1] : "hub";
    }

    private static void MoveObject(GameObject obj, Transform parent, Vector3 offset)
    {
        obj.transform.SetParent(null, true);
        if (obj.scene != destination) SceneManager.MoveGameObjectToScene(obj, destination);
        obj.transform.position += offset;
        obj.transform.SetParent(parent, true);
        importedRoots++;
    }

    private static Transform ZoneRoot(string id, string source)
    {
        var result = new GameObject("REGION · " + id + " · " + source);
        SceneManager.MoveGameObjectToScene(result, destination); result.transform.SetParent(worldRoot, false);
        return result.transform;
    }

    private static GameObject NewRoot(string name)
    {
        var obj = new GameObject(name); SceneManager.MoveGameObjectToScene(obj, destination); return obj;
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static void BuildYellowEncounters()
    {
        var templates = battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true).Where(enemy => !enemy.isBoss).Take(3).ToArray();
        foreach (var zone in Plan.Where(z => z.risk == OpenWorldRisk.Yellow)) {
            var root = new GameObject("Encounter · " + zone.id); SceneManager.MoveGameObjectToScene(root, destination);
            root.transform.SetParent(battleRoot, false);
            var encounter = root.AddComponent<OpenWorldEncounter>(); encounter.zoneId = zone.id; encounter.content = root; Encounters.Add(encounter);
            for (int i = 0; i < templates.Length; i++) {
                var enemy = Object.Instantiate(templates[i].gameObject, root.transform);
                enemy.name = "Toán do thám Ân · " + (i + 1);
                enemy.transform.position = zone.center + new Vector3(15f + i * 3f, .2f, 16f);
                enemy.SetActive(true);
                var controller = enemy.GetComponent<ThanhGiongEnemy>();
                controller.isBoss = false; controller.engageDistance = 30f;
                controller.maxHealth = 45f; controller.slamDamage = 18f;
            }
            Vector3 campPos = zone.center + new Vector3(22f, 0f, 25f);
            campPos.y = HeightAt(campPos.x, campPos.z);
            MeshyArt("LeuTrai_GiacAn", root.transform, campPos, 4.2f, 210f, true, "Lều quân Ân");
            MeshyArt("ThapCanh_DoanThanh", root.transform, campPos + new Vector3(6.5f, 0f, -3.5f), 6.2f, 195f, true, "Tháp canh quân Ân");
            MeshyArt("CoLenh_PhuDong", root.transform, campPos + new Vector3(-4.5f, 0f, -2.5f), 3.4f, 180f, false, "Cờ hiệu doanh trại");
        }
    }

    private static void BuildTerrain()
    {
        const int nx = 146, nz = 144;
        var vertices = new Vector3[(nx + 1) * (nz + 1)];
        var colors = new Color[vertices.Length]; var uv = new Vector2[vertices.Length];
        var triangles = new int[nx * nz * 6];
        for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++) {
            int i = z * (nx + 1) + x;
            float px = Mathf.Lerp(West, East, x / (float)nx), pz = Mathf.Lerp(South, North, z / (float)nz);
            vertices[i] = new Vector3(px, HeightAt(px, pz), pz);
            colors[i] = GroundColor(px, pz); uv[i] = new Vector2(px / 25f, pz / 25f);
        }
        int ti = 0;
        for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++) {
            int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
            triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
            triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
        }
        var mesh = new Mesh { name = "DaiVietContinuousGround", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = SaveMesh(mesh, "ContinuousGround");
        var ground = new GameObject("CONTINUOUS GROUND · 730 × 720m"); ground.transform.SetParent(worldRoot, false);
        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        ground.AddComponent<MeshRenderer>().sharedMaterial = VertexGroundMaterial();
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;
        ground.isStatic = true;
    }

    private static float HeightAt(float x, float z)
    {
        Vector3 p = new Vector3(x, 0, z);
        float road = RoadDistance(p);
        float center = Plan.Min(zone => Vector2.Distance(new Vector2(x, z), new Vector2(zone.center.x, zone.center.z)));
        float clearing = AuthoredClearings.Min(c => Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.z)));
        float flatDist = Mathf.Min(road - 12f, Mathf.Min(center - 55f, clearing - 54f));
        float factor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flatDist / 24f));
        float hill = Mathf.PerlinNoise((x + 750f) * .009f, (z + 900f) * .009f) * 8.5f;
        return -.02f + hill * factor;
    }

    private static Color GroundColor(float x, float z)
    {
        var result = Color.black; float total = 0f;
        foreach (var zone in Plan) {
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(zone.center.x, zone.center.z));
            float weight = 1f / Mathf.Pow(Mathf.Max(20f, distance), 3f);
            result += BiomeColor(zone.biome) * weight; total += weight;
        }
        result /= total;
        float variation = Mathf.Lerp(.90f, 1.10f, Mathf.PerlinNoise((x + 400f) * .042f, (z + 500f) * .042f));
        result *= variation; result.a = 1f; return result;
    }

    private static Color BiomeColor(OpenWorldBiome biome)
    {
        switch (biome) {
            case OpenWorldBiome.Forest: return new Color(.24f, .54f, .23f);
            case OpenWorldBiome.Mountain: return new Color(.46f, .50f, .45f);
            case OpenWorldBiome.Highland: return new Color(.54f, .58f, .30f);
            case OpenWorldBiome.Swamp: return new Color(.27f, .52f, .38f);
            default: return new Color(.42f, .67f, .26f);
        }
    }

    private static void BuildRoads()
    {
        var root = NewRoot("OPEN WORLD · CONNECTED ROADS / CHOKEPOINTS").transform;
        root.SetParent(worldRoot, false);
        Material roadMat = ZeldaRoadMaterial();
        foreach (var connection in Links) {
            var a = Plan.Find(z => z.id == connection.from); var b = Plan.Find(z => z.id == connection.to);
            Vector3 start = a.center, end = b.center;
            var road = new GameObject("Road · " + a.id + " ↔ " + b.id); road.transform.SetParent(root, false);
            int sections = Mathf.CeilToInt(Vector3.Distance(start, end) / 6f);
            var vertices = new Vector3[(sections + 1) * 2]; var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[sections * 6];
            Vector3 dir = (end - start).normalized;
            var across = Vector3.Cross(dir, Vector3.up) * (RoadWidth * .5f);
            Color riskTint = ThanhGiongOpenWorld.RiskColor(b.risk);
            for (int i = 0; i <= sections; i++) {
                float tNorm = i / (float)sections;
                Vector3 point = Vector3.Lerp(start, end, tNorm);
                point.y = HeightAt(point.x, point.z) + .045f;
                vertices[i * 2] = point - across; vertices[i * 2 + 1] = point + across;
                uv[i * 2] = new Vector2(0, i * 0.75f); uv[i * 2 + 1] = new Vector2(1, i * 0.75f);
                colors[i * 2] = riskTint; colors[i * 2 + 1] = riskTint;
                if (i == sections) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh { name = road.name };
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh = SaveMesh(mesh, "Road_" + a.id + "_" + b.id);
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = roadMat;
            // Terrain provides the collision plane underneath; road overlays never create seams.
            road.isStatic = true;

            Vector3 gatePos = FindSafeRoadGatePosition(start, end);
            Vector3 side = across.normalized;
            float roadYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2) {
                Vector3 rockPos = gatePos + side * 9.8f * s;
                rockPos.y = HeightAt(rockPos.x, rockPos.z);
                MeshyArt("DaNui_RungThieng", road.transform, rockPos, 3.6f, roadYaw + s * 45f, true, "Chokepoint stone");

                Vector3 bannerPos = gatePos + side * 7.4f * s;
                bannerPos.y = HeightAt(bannerPos.x, bannerPos.z);
                MeshyArt("CoLenh_PhuDong", road.transform, bannerPos, 3.4f, roadYaw + (s > 0 ? -90f : 90f), false, "Risk boundary marker");
            }
            Vector3 signPos = gatePos + side * 10.6f + dir * 2.8f;
            signPos.y = HeightAt(signPos.x, signPos.z);
            Signpost(road.transform, signPos, b.displayName, ThanhGiongOpenWorld.RiskColor(b.risk));
        }
        AddCourtyardRoadStrip(root, roadMat, "VillageMainPath_PhuDong", new Vector3(-55f, 0f, -32f), new Vector3(-55f, 0f, 22f), 9.5f);
        AddCourtyardRoadStrip(root, roadMat, "CitadelMainPath_PhongChau", new Vector3(75f, 0f, -36f), new Vector3(75f, 0f, 18f), 10.5f);
    }

    private static void AddCourtyardRoadStrip(Transform parent, Material roadMat, string name, Vector3 start, Vector3 end, float width)
    {
        var road = new GameObject(name); road.transform.SetParent(parent, false);
        int sections = Mathf.CeilToInt(Vector3.Distance(start, end) / 4f);
        var vertices = new Vector3[(sections + 1) * 2]; var uv = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length]; var triangles = new int[sections * 6];
        Vector3 dir = (end - start).normalized;
        var across = Vector3.Cross(dir, Vector3.up) * (width * .5f);
        Color tint = new Color(0.85f, 0.78f, 0.65f, 1f);
        for (int i = 0; i <= sections; i++) {
            Vector3 point = Vector3.Lerp(start, end, i / (float)sections);
            point.y = HeightAt(point.x, point.z) + .042f;
            vertices[i * 2] = point - across; vertices[i * 2 + 1] = point + across;
            uv[i * 2] = new Vector2(0, i * 0.65f); uv[i * 2 + 1] = new Vector2(1, i * 0.65f);
            colors[i * 2] = tint; colors[i * 2 + 1] = tint;
            if (i == sections) continue;
            int t = i * 6, v = i * 2;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }
        var mesh = new Mesh { name = name };
        mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh = SaveMesh(mesh, "Road_" + name);
        road.AddComponent<MeshFilter>().sharedMesh = mesh;
        road.AddComponent<MeshRenderer>().sharedMaterial = roadMat;
        road.isStatic = true;
    }

    private static Vector3 FindSafeRoadGatePosition(Vector3 start, Vector3 end)
    {
        float bestT = 0.5f;
        float bestClearance = -1f;
        float[] candidates = { 0.50f, 0.56f, 0.44f, 0.62f, 0.38f, 0.68f, 0.32f, 0.74f, 0.26f, 0.78f, 0.22f };
        foreach (float t in candidates) {
            Vector3 pt = Vector3.Lerp(start, end, t);
            float minClearing = AuthoredClearings.Min(c => Vector2.Distance(new Vector2(pt.x, pt.z), new Vector2(c.x, c.z)));
            if (minClearing >= 56f) return pt;
            if (minClearing > bestClearance) {
                bestClearance = minClearing;
                bestT = t;
            }
        }
        return Vector3.Lerp(start, end, bestT);
    }

    private static float RoadDistance(Vector3 point)
    {
        float distance = float.MaxValue;
        foreach (var connection in Links) {
            Vector3 a = Plan.Find(z => z.id == connection.from).center, b = Plan.Find(z => z.id == connection.to).center;
            Vector3 line = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point, line) != 0 ? Vector3.Dot(point - a, line) / line.sqrMagnitude : 0f);
            distance = Mathf.Min(distance, Vector3.Distance(point, a + line * t));
        }
        return distance;
    }

    private static void BuildCoastAndBorders()
    {
        var root = NewRoot("OPEN WORLD · COAST / BOUNDARY CLIFFS").transform; root.SetParent(worldRoot, false);
        Primitive("Biển Đông · horizon water", PrimitiveType.Cube, root, new Vector3(-65f, -4.5f, -60f), new Vector3(1700f, 2f, 1700f), ZeldaWaterMaterial(), false);
        Material cliff = Mat("CoastalCliff", new Color(.34f, .40f, .35f));
        // Unbroken invisible collision sides prevent falling off the authored land mass.
        BoxCollider west = root.gameObject.AddComponent<BoxCollider>(); west.center = new Vector3(West - 3, 5, (South + North) * .5f); west.size = new Vector3(6, 24, North - South + 12);
        BoxCollider east = root.gameObject.AddComponent<BoxCollider>(); east.center = new Vector3(East + 3, 5, (South + North) * .5f); east.size = west.size;
        BoxCollider south = root.gameObject.AddComponent<BoxCollider>(); south.center = new Vector3((West + East) * .5f, 5, South - 3); south.size = new Vector3(East - West + 12, 24, 6);
        BoxCollider north = root.gameObject.AddComponent<BoxCollider>(); north.center = new Vector3((West + East) * .5f, 5, North + 3); north.size = south.size;
        for (int i = 0; i < 48; i++) {
            float t = i / 47f;
            AddCliff(root, new Vector3(Mathf.Lerp(West, East, t), 0f, South + 2), cliff);
            AddCliff(root, new Vector3(Mathf.Lerp(West, East, t), 0f, North - 2), cliff);
            if (i < 47) {
                AddCliff(root, new Vector3(West + 2, 0f, Mathf.Lerp(South, North, t)), cliff);
                AddCliff(root, new Vector3(East - 2, 0f, Mathf.Lerp(South, North, t)), cliff);
            }
        }
    }

    private static void AddCliff(Transform parent, Vector3 position, Material material)
    {
        position.y = HeightAt(position.x, position.z) - 0.5f;
        float height = 9.5f + (float)random.NextDouble() * 5.5f;
        MeshyArt("DaNui_RungThieng", parent, position, height, random.Next(0, 360), false, "Coastal escarpment");
    }

    private static void BuildZonesAndResources()
    {
        // Reserve every zone's resources before placing any neighboring decorative rings.
        ReservedResourcePositions.Clear();
        foreach (var definition in Plan.Where(z => z.risk != OpenWorldRisk.Safe))
            for (int rank = 0; rank < 3; rank++)
                for (int index = 0; index < (rank == 0 ? 6 : rank == 1 ? 3 : 1); index++) {
                    var point = ResourcePosition(definition, rank, index);
                    ReservedResourcePositions.Add(new Vector3(point.x, 0, point.z));
                }
        foreach (var zone in Plan) {
            var root = ZoneRoot(zone.id, "sinh cảnh + tài nguyên");
            var decor = new GameObject("Decorative ring · " + zone.id); decor.transform.SetParent(root, false);
            decor.transform.position = zone.center; Scenery.Add(decor.transform);
            Vector3 zoneSignPos = zone.center + new Vector3(-17f, 0f, -20f);
            if (zone.risk == OpenWorldRisk.Safe) zoneSignPos = new Vector3(12f, 0f, -18f);
            zoneSignPos.y = HeightAt(zoneSignPos.x, zoneSignPos.z);
            Signpost(root, zoneSignPos, zone.displayName + "\n" + ThanhGiongOpenWorld.RiskLabel(zone.risk), ThanhGiongOpenWorld.RiskColor(zone.risk));
            if (zone.risk != OpenWorldRisk.Safe) {
                var zoneMarker = Primitive("Minimap / region color marker · " + zone.id, PrimitiveType.Cylinder, root,
                    zone.center + new Vector3(-22, .12f, -22), new Vector3(4f, .08f, 4f), Mat("Risk_" + zone.risk, ThanhGiongOpenWorld.RiskColor(zone.risk)), false);
                zoneMarker.isStatic = true;
                int[] counts = { 6, 3, 1 };
                for (int abundance = 0; abundance < 3; abundance++) {
                    var resource = ThanhGiongOpenWorld.ResourceFor(zone.biome, abundance);
                    var cluster = new GameObject((abundance == 0 ? "Primary" : abundance == 1 ? "Secondary" : "Rare") + " · " + resource);
                    cluster.transform.SetParent(root, false);
                    for (int i = 0; i < counts[abundance]; i++) {
                        Vector3 position = ResourcePosition(zone, abundance, i);
                        var node = new GameObject(ThanhGiongOpenWorld.ResourceLabel(resource) + " · G để thu thập"); node.transform.SetParent(cluster.transform, false); node.transform.position = position;
                        var resourceNode = node.AddComponent<ThanhGiongResourceNode>(); resourceNode.resource = resource; resourceNode.yieldOverride = 0; resourceNode.respawnSeconds = 45f;
                        ResourceVisual(node.transform, resource);
                        resourceCount++;
                    }
                }
            }
            BuildBiomeDensity(zone, decor.transform);
            BuildBiomeLandmarks(zone, decor.transform);
            BuildGroundCover(zone, decor.transform);
            CombineDecorativeMeshes(zone.id, decor.transform);
        }
    }

    private static void BuildBiomeDensity(OpenWorldZone zone, Transform decor)
    {
        bool wooded = zone.biome == OpenWorldBiome.Forest || zone.biome == OpenWorldBiome.Swamp;
        bool rocky = zone.biome == OpenWorldBiome.Mountain || zone.biome == OpenWorldBiome.Highland;
        int target = wooded ? 105 : rocky ? 82 : 68;
        int placed = 0;
        // A shared golden-angle template fills inner, middle and outer bands. Varying heights
        // gives a canopy and understory silhouette instead of a few isolated clumps on a plain.
        for (int attempt = 0; attempt < target * 9 && placed < target; attempt++) {
            float angle = (attempt * 137.508f + (int)zone.biome * 23f) * Mathf.Deg2Rad;
            float maxRadius = zone.risk == OpenWorldRisk.Safe ? 115f : 69f;
            float radius = 12f + Mathf.Sqrt((attempt % 97) / 96f) * maxRadius;
            Vector3 position = zone.center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            float footprint = rocky ? 5.5f : wooded ? 5f : 4f;
            if (!DecorationAllowed(position, footprint)) continue;
            position.y = HeightAt(position.x, position.z);
            if (rocky) {
                float height = zone.biome == OpenWorldBiome.Mountain ? 3.2f + placed % 6 : 2.2f + placed % 4;
                if (placed % 7 == 0 && zone.biome == OpenWorldBiome.Mountain) {
                    MeshyArt("MoQuangSat_PhongChau", decor, position, height * 0.85f, placed * 53f, true, "Biome ridge outcrop");
                } else {
                    MeshyArt("DaNui_RungThieng", decor, position, height, placed * 53f, true, "Biome ridge outcrop");
                }
                if (placed % 5 == 0) ExistingArt("TreeCluster_Pine_Mossy", decor, position + new Vector3(4, 0, 2), 6.5f + placed % 4);
                else if (placed % 8 == 0) ExistingArt("TreeCluster_PuffyCrown_Painterly", decor, position + new Vector3(-3.5f, 0, 2.5f), 6.5f + placed % 3);
            } else if (zone.biome == OpenWorldBiome.Forest) {
                if (placed % 11 == 0) {
                    MeshyArt("CayDaCoThu_LangViet", decor, position, 8.5f + placed % 3, placed * 47f, true);
                } else if (placed % 5 == 0) {
                    MeshyArt("KhomTreNga_ThanThanh", decor, position, 6.2f + placed % 3, placed * 61f, true);
                } else {
                    string prefab = placed % 4 == 0 ? "TreeCluster_PuffyCrown_Painterly" : placed % 4 == 1 ? "TreeCluster_Broadleaf_Oak" : "BambooGrove_Vietnamese_Legend";
                    ExistingArt(prefab, decor, position, placed % 4 < 2 ? 8.5f + placed % 5 : 6.5f + placed % 4);
                }
                if (placed % 4 == 0) ExistingArt("FoliageCluster_Undergrowth_Wild", decor, position + new Vector3(2, 0, -2), 1.2f);
            } else if (zone.biome == OpenWorldBiome.Swamp) {
                if (placed % 9 == 0) {
                    MeshyArt("DamSenCo_VanLang", decor, position, 1.9f + (placed % 3) * 0.3f, placed * 43f, false);
                } else if (placed % 14 == 0) {
                    MeshyArt("CayDaCoThu_LangViet", decor, position, 7.8f + placed % 3, placed * 37f, true);
                } else {
                    string prefab = placed % 4 == 0 ? "TreeCluster_PuffyCrown_Painterly" : placed % 4 == 1 ? "TreeCluster_Broadleaf_Oak" : "FoliageCluster_Undergrowth_Wild";
                    ExistingArt(prefab, decor, position, placed % 4 < 2 ? 7.2f + placed % 3 : 1.45f + placed % 3 * .35f);
                }
            } else {
                if (placed % 14 == 0) {
                    MeshyArt("CayDaCoThu_LangViet", decor, position, 8.2f + placed % 3, placed * 59f, true);
                } else if (placed % 4 == 0) {
                    ExistingArt("TreeCluster_PuffyCrown_Painterly", decor, position, 7.0f + placed % 3);
                } else if (placed % 5 == 0) {
                    ExistingArt("TreeCluster_Broadleaf_Oak", decor, position, 6.5f + placed % 3);
                } else {
                    ExistingArt("FoliageCluster_Undergrowth_Wild", decor, position, .95f + placed % 3 * .18f);
                }
            }
            placed++; sceneryCount++;
        }
        Report.AppendLine(zone.id + " biome decoration: " + placed + "/" + target + " clusters, with roads / resources / original locations reserved.");
    }

    private static bool DecorationAllowed(Vector3 position, float radius)
    {
        if (position.x < West + 20 || position.x > East - 20 || position.z < South + 20 || position.z > North - 20) return false;
        if (RoadDistance(position) < RoadWidth * .5f + radius + 6f) return false;
        // The imported authored locations retain their floors, courtyards and original set dressing.
        foreach (var center in AuthoredClearings) if (Vector3.Distance(position, center) < 53f + radius) return false;
        foreach (var p in ReservedResourcePositions) {
            if (Vector3.Distance(position, p) < radius + 4f) return false;
        }
        return true;
    }

    private static void BuildBiomeLandmarks(OpenWorldZone zone, Transform decor)
    {
        Material waterMat = ZeldaWaterMaterial();
        if (zone.biome == OpenWorldBiome.Swamp) {
            int pools = 0;
            for (int i = 0; i < 60 && pools < 5; i++) {
                float angle = (i * 97f + 35f) * Mathf.Deg2Rad;
                Vector3 p = zone.center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (37f + i % 4 * 10f);
                float radius = 5.5f + i % 3 * 1.4f;
                if (!DecorationAllowed(p, radius + 2f)) continue;
                p.y = HeightAt(p.x, p.z) + .035f;
                Primitive("Đầm nước / ao sen · outside walking routes", PrimitiveType.Cylinder, decor, p,
                    new Vector3(radius * 2f, .022f, radius * 1.45f), waterMat, false);
                MeshyArt("DamSenCo_VanLang", decor, p + Vector3.up * 0.02f, 2.2f, i * 67f, false);
                for (int j = 0; j < 6; j++) {
                    float a = j * Mathf.PI / 3f; Vector3 reed = p + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius * .68f);
                    reed.y = HeightAt(reed.x, reed.z);
                    ExistingArt("FoliageCluster_Undergrowth_Wild", decor, reed, 1.3f);
                }
                pools++;
            }
        } else if (zone.biome == OpenWorldBiome.Steppe) {
            int fields = 0;
            for (int i = 0; i < 70 && fields < 7; i++) {
                float angle = (i * 113f + 15f) * Mathf.Deg2Rad;
                Vector3 p = zone.center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (34f + i % 4 * 14f);
                if (!DecorationAllowed(p, 9.5f)) continue;
                p.y = HeightAt(p.x, p.z) + .028f;
                Primitive("Thửa ruộng vàng", PrimitiveType.Cube, decor, p, new Vector3(11.4f, .025f, 13.4f), waterMat, false);
                for (int row = 0; row < 7; row++) {
                    Vector3 rowPos = p + new Vector3(-4.2f + row * 1.4f, 0f, 0f);
                    ExistingArt("FoliageCluster_Undergrowth_Wild", decor, rowPos + new Vector3(0f, 0f, -3.2f), 1.05f);
                    ExistingArt("FoliageCluster_Undergrowth_Wild", decor, rowPos + new Vector3(0f, 0f, 3.2f), 1.05f);
                }
                if (fields == 0) {
                    MeshyArt("SanDinh_TrongDong", decor, p + new Vector3(6.8f, 0f, 0f), 3.6f, 225f, true);
                }
                fields++;
            }
        } else if (zone.biome == OpenWorldBiome.Mountain || zone.biome == OpenWorldBiome.Highland) {
            int ridges = 0;
            for (int i = 0; i < 80 && ridges < 12; i++) {
                float angle = (i * 79f + 130f) * Mathf.Deg2Rad;
                Vector3 p = zone.center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (62f + i % 3 * 5f);
                if (!DecorationAllowed(p, 9f)) continue;
                float height = zone.biome == OpenWorldBiome.Mountain ? 8.5f + i % 4 * 2f : 5.5f + i % 3;
                p.y = HeightAt(p.x, p.z);
                MeshyArt("DaNui_RungThieng", decor, p, height, i * 41f, true, "Natural ridgeline / rocky silhouette");
                if (ridges == 0) {
                    MeshyArt("DenThieng_DinhSoc", decor, p + new Vector3(-5.2f, 0f, 3.5f), 3.8f, 145f, true);
                }
                ridges++;
            }
        }
    }

    private static void BuildGroundCover(OpenWorldZone zone, Transform decor)
    {
        var vertices = new List<Vector3>(); var colors = new List<Color>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
        int desired = zone.risk == OpenWorldRisk.Safe ? 1600 : zone.biome == OpenWorldBiome.Swamp ? 1100 : zone.biome == OpenWorldBiome.Forest ? 950 : 780;
        Color baseGrass = zone.biome == OpenWorldBiome.Steppe ? new Color(.44f, .69f, .25f) : zone.biome == OpenWorldBiome.Swamp ? new Color(.29f, .55f, .33f) : new Color(.32f, .58f, .24f);
        float maxRadius = zone.risk == OpenWorldRisk.Safe ? 128f : 76f;
        for (int i = 0; i < desired; i++) {
            float angle = i * 137.508f * Mathf.Deg2Rad;
            float radius = 6f + Mathf.Sqrt((i % 397) / 396f) * maxRadius;
            Vector3 p = zone.center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            if (!GroundCoverAllowed(p)) continue;
            p.y = HeightAt(p.x, p.z) + .02f;
            float height = zone.biome == OpenWorldBiome.Swamp ? 1.05f + i % 4 * .22f : .52f + i % 3 * .14f;
            bool isWildflower = (i % 11 == 0);
            Color flowerTip = (i % 3 == 0) ? new Color(0.98f, 0.86f, 0.32f) : (i % 3 == 1) ? new Color(0.96f, 0.58f, 0.72f) : new Color(0.55f, 0.84f, 0.98f);
            for (int blade = 0; blade < 3; blade++) {
                float a = angle + blade * 2.094f; Vector3 side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .42f;
                int v = vertices.Count; Vector3 local = decor.InverseTransformPoint(p);
                vertices.Add(local - side); vertices.Add(local + side); vertices.Add(local + Vector3.up * height + side * .22f);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0.5f, 1f));
                Color tint = baseGrass * (.88f + i % 5 * .045f); tint.a = 1;
                Color tipColor = isWildflower ? flowerTip : Color.Lerp(tint, new Color(.88f, .86f, .42f), .38f);
                colors.Add(tint * 0.78f); colors.Add(tint * 0.78f); colors.Add(tipColor);
                triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                triangles.Add(v + 2); triangles.Add(v + 1); triangles.Add(v);
            }
        }
        var mesh = new Mesh { name = "GroundCover_" + zone.id, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
        mesh.normals = Enumerable.Repeat(Vector3.up, vertices.Count).ToArray(); mesh.RecalculateBounds();
        mesh = SaveMesh(mesh, "GroundCover_" + zone.id);
        var obj = new GameObject(zone.biome == OpenWorldBiome.Swamp ? "Batched reeds / sedges" : "Batched grasses / rice stubble"); obj.transform.SetParent(decor, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = ZeldaGrassMaterial();
        obj.isStatic = true;
    }

    private static bool GroundCoverAllowed(Vector3 position)
    {
        if (position.x < West + 16 || position.x > East - 16 || position.z < South + 16 || position.z > North - 16) return false;
        if (RoadDistance(position) < RoadWidth * .5f + 1.2f) return false;
        if (Mathf.Abs(position.x - (-55f)) < 6.2f && position.z >= -34f && position.z <= 25f) return false;
        if (Mathf.Abs(position.x - 75f) < 6.8f && position.z >= -38f && position.z <= 22f) return false;
        foreach (var center in AuthoredClearings) if (Vector3.Distance(position, center) < 16f) return false;
        return true;
    }

    private static void CombineDecorativeMeshes(string zoneId, Transform decor)
    {
        // Keep original prefab colliders and skip high-poly Meshy 3D FBX instances (which batch via SRP Batcher / GPU Instancing).
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var originals = new List<MeshRenderer>();
        foreach (var filter in decor.GetComponentsInChildren<MeshFilter>(true)) {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null || filter.sharedMesh.name.StartsWith("GroundCover_", StringComparison.Ordinal)) continue;
            if (IsMeshyHierarchy(filter.transform)) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int sub = 0; sub < Mathf.Min(filter.sharedMesh.subMeshCount, materials.Length); sub++) {
                if (materials[sub] == null) continue;
                List<CombineInstance> group;
                if (!groups.TryGetValue(materials[sub], out group)) { group = new List<CombineInstance>(); groups[materials[sub]] = group; }
                group.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = decor.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            originals.Add(renderer);
        }
        int index = 0;
        foreach (var group in groups) {
            var mesh = new Mesh { name = "BiomeDecor_" + zoneId + "_" + index, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds(); mesh = SaveMesh(mesh, mesh.name);
            var obj = new GameObject("Batched biome art · " + group.Key.name); obj.transform.SetParent(decor, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = group.Key; obj.isStatic = true; index++;
        }
        foreach (var renderer in originals) renderer.enabled = false;
        Report.AppendLine(zoneId + " static scenery batches: " + groups.Count + " material renderers + SRP-batched Meshy 3D props + one ground-cover renderer.");
    }

    private static bool IsMeshyHierarchy(Transform t)
    {
        for (Transform cur = t; cur != null; cur = cur.parent) {
            if (cur.GetComponent<ThanhGiong.Environment.MeshyPropVfxAnimator>() != null) return true;
            if (cur.name.StartsWith("Meshy_", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static Vector3 ResourcePosition(OpenWorldZone zone, int rank, int index)
    {
        for (int trial = 0; trial < 80; trial++) {
            float angle = (rank * 115f + index * 39f + trial * 31f + 16f) * Mathf.Deg2Rad;
            float radius = 24f + rank * 8f + (index % 3) * 6f;
            Vector3 point = zone.center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            if (RoadDistance(point) < 11f) continue;
            // Keep resources away from the imported village, fortress interiors and peak cloud stairs.
            if (zone.id == "soc_peak" && point.z > zone.center.z - 15f) continue;
            point.y = HeightAt(point.x, point.z) + .05f; return point;
        }
        return zone.center + new Vector3(40, .1f, 32 + index * 3);
    }

    private static void ResourceVisual(Transform parent, OpenWorldResource resource)
    {
        Vector3 basePos = parent.position;
        basePos.y = HeightAt(basePos.x, basePos.z);
        switch (resource) {
            case OpenWorldResource.Wood:
                MeshyArt("KhomTreNga_ThanThanh", parent, basePos, 4.4f, random.Next(0, 360), true, "Tre ngà Đại Việt");
                ExistingArt("BambooGrove_Vietnamese_Legend", parent, basePos + new Vector3(1.1f, 0f, 0.7f), 4.0f);
                break;
            case OpenWorldResource.Stone:
                MeshyArt("DaNui_RungThieng", parent, basePos, 2.2f, random.Next(0, 360), true, "Đá núi Phong Châu");
                MeshyArt("DaNui_RungThieng", parent, basePos + new Vector3(1.1f, 0f, -0.6f), 1.3f, random.Next(0, 360), false, "Đá tảng phụ");
                break;
            case OpenWorldResource.Ore:
                MeshyArt("MoQuangSat_PhongChau", parent, basePos, 2.25f, random.Next(0, 360), true, "Vỉa quặng sắt Phong Châu");
                break;
            case OpenWorldResource.Hide:
                MeshyArt("LeuTrai_GiacAn", parent, basePos, 2.45f, random.Next(0, 360), true, "Lều thuộc da / săn bắn");
                MeshyArt("MamLuongThuc_LangGiong", parent, basePos + new Vector3(1.25f, 0f, 0.75f), 0.95f, random.Next(0, 360), false, "Kiện da thú");
                break;
            case OpenWorldResource.Fiber:
                MeshyArt("DamSenCo_VanLang", parent, basePos, 1.75f, random.Next(0, 360), true, "Khóm sen & gai sợi Văn Lang");
                ExistingArt("FoliageCluster_Undergrowth_Wild", parent, basePos + new Vector3(0.85f, 0f, -0.65f), 1.4f);
                break;
        }
    }

    private static GameObject MeshyArt(string key, Transform parent, Vector3 position, float targetHeight, float yawDegrees, bool collision = false, string customName = null)
    {
        string path = "Assets/Prefabs/Meshy/Environment/Meshy_" + key + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException("Required Meshy 3D environment prefab: " + path);
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, destination);
        obj.name = string.IsNullOrEmpty(customName) ? "Meshy_" + key : customName + " (Meshy_" + key + ")";
        obj.transform.position = Vector3.zero;
        obj.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        var renderers = obj.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length > 0) {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y > 0.01f) {
                float factor = targetHeight / b.size.y;
                obj.transform.localScale *= factor;
            }
            Physics.SyncTransforms();
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float bottomOffset = obj.transform.position.y - b.min.y;
            obj.transform.position = new Vector3(position.x, position.y + bottomOffset - 0.04f, position.z);
        } else {
            obj.transform.position = position;
        }
        obj.transform.SetParent(parent, true);
        foreach (var col in obj.GetComponentsInChildren<Collider>(true)) {
            if (!collision) Object.DestroyImmediate(col);
        }
        if (collision && obj.GetComponentInChildren<Collider>(true) == null) {
            var box = obj.AddComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 1.4f, 1.2f);
            box.center = new Vector3(0f, 0.7f, 0f);
        }
        return obj;
    }

    private static void ExistingArt(string prefabName, Transform parent, Vector3 position, float height)
    {
        position.y = HeightAt(position.x, position.z);
        if (prefabName == "TreeCluster_PuffyCrown_Painterly" || prefabName == "TreeCluster_Broadleaf_Oak" || prefabName == "TreeCluster_Pine_Mossy") {
            MeshyArt("CayDaCoThu_LangViet", parent, position, height, random.Next(0, 360), false, prefabName);
            return;
        }
        if (prefabName == "FoliageCluster_Undergrowth_Wild") {
            string bushKey = random.Next(0, 3) == 0 ? "DamSenCo_VanLang" : "KhomTreNga_ThanThanh";
            float bushHeight = bushKey == "DamSenCo_VanLang" ? Mathf.Clamp(height * 1.15f, 1.2f, 2.1f) : Mathf.Clamp(height * 1.9f, 2.0f, 3.4f);
            MeshyArt(bushKey, parent, position, bushHeight, random.Next(0, 360), false, prefabName);
            return;
        }
        string path = "Assets/Prefabs/Environment/Foliage/" + prefabName + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException("Required existing environment art: " + path);
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, destination);
        obj.transform.position = position; obj.transform.rotation = Quaternion.Euler(0, random.Next(0, 360), 0);
        obj.transform.SetParent(parent, true);
        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0) {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y > .01f) obj.transform.localScale *= height / bounds.size.y;
        }
        obj.isStatic = true;
    }

    private static void BuildForge()
    {
        var root = new GameObject("LÒ RÈN PHONG CHÂU · G rèn / B cất đồ"); root.transform.SetParent(worldRoot, false);
        Vector3 forgePos = new Vector3(75f, HeightAt(75f, -34f), -34f);
        root.transform.position = new Vector3(75f, 0f, -34f);
        root.AddComponent<ThanhGiongForgeStation>();
        MeshyArt("LoRenThep_VanLang", root.transform, forgePos, 3.2f, 180f, true, "Lò rèn sắt Phong Châu");
        MeshyArt("MamLuongThuc_LangGiong", root.transform, forgePos + new Vector3(-2.6f, 0f, 1.2f), 1.05f, 35f, false, "Bàn khuôn đúc");
        MeshyArt("CoLenh_PhuDong", root.transform, forgePos + new Vector3(-3.2f, 0f, -1.5f), 3.6f, 160f, false, "Cờ hiệu lò rèn");
        Signpost(root.transform, forgePos + new Vector3(3.4f, 0f, 0f), "LÒ RÈN · G\n6 quặng + 3 tre/gỗ\nB · Cất tài nguyên", new Color(.87f, .69f, .34f));
    }

    private static void Signpost(Transform parent, Vector3 position, string text, Color color)
    {
        position.y = HeightAt(position.x, position.z);
        MeshyArt("CotMocDa_DongSon", parent, position, 2.55f, 180f, false, "Signpost");
        var board = Primitive("Direction sign", PrimitiveType.Cube, parent, position + Vector3.up * 2.85f, new Vector3(4.6f, 1.05f, .18f), Mat("Sign_" + ColorUtility.ToHtmlStringRGB(color), color * .55f), false);
        var label = new GameObject("Label"); label.transform.SetParent(board.transform, false); label.transform.localPosition = new Vector3(0, 0, -.6f);
        var mesh = label.AddComponent<TextMesh>(); mesh.text = text; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
        mesh.characterSize = .075f; mesh.fontSize = 40; mesh.color = new Color(1f, .96f, .82f);
        label.transform.localScale = new Vector3(1f / 4.6f, 1f / 1.05f, 4f);
        label.transform.localRotation = Quaternion.identity;
    }

    private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision)
    {
        var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.position = position; obj.transform.localScale = scale;
        obj.transform.SetParent(parent, true); obj.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.isStatic = true; return obj;
    }

    private static void ClearRoadCorridors()
    {
        Physics.SyncTransforms();
        // Keep source art and its floors. Remove only movement barriers inside the 12m paths;
        // bridges, ground slabs, steps, enemy hitboxes and the ascension path keep collision.
        foreach (var root in worldRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("REGION ·") && !t.name.Contains("sinh cảnh"))) {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) {
                if (collider == null || collider.isTrigger || !collider.enabled) continue;
                string name = collider.name.ToLowerInvariant(); Bounds bounds = collider.bounds;
                if (name.Contains("ground") || name.Contains("floor") || name.Contains("terrain") || name.Contains("step") || name.Contains("bridge") || name.Contains("cloud") || name.Contains("mây") || name.Contains("datlang")) continue;
                if (bounds.size.y < .8f || bounds.min.y > 2f || bounds.max.y < .5f) continue;
                if (!BoundsTouchesRoad(bounds, RoadWidth * .5f)) continue;
                collider.enabled = false; corridorColliders++;
            }
        }
    }

    private static void ClearRoadVisuals()
    {
        // Move complete small props, never individual roof meshes or a combined environment.
        var processed = new HashSet<GameObject>();
        var authoredRegions = worldRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("REGION ·") && !t.name.Contains("sinh cảnh")).ToArray();
        foreach (var region in authoredRegions) foreach (var renderer in region.GetComponentsInChildren<Renderer>(true)) {
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer) continue;
            GameObject prop = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
            if (prop == null) {
                // Non-prefab procedural primitives remain a single prop; complex children stay grouped.
                Transform candidate = renderer.transform;
                while (candidate.parent != null && candidate.parent != region && candidate.parent.name != "Godot Environment Collisions" &&
                       !candidate.parent.name.StartsWith("ARCHITECTURE", StringComparison.Ordinal) && !candidate.parent.name.StartsWith("MESHY 3D WORLD", StringComparison.Ordinal))
                    candidate = candidate.parent;
                prop = candidate.gameObject;
            }
            if (prop == null || !prop.transform.IsChildOf(region) || processed.Contains(prop)) continue;
            processed.Add(prop);
            string identity = prop.name.ToLowerInvariant();
            if (identity.Contains("ground") || identity.Contains("floor") || identity.Contains("bridge") || identity.Contains("cloud") || identity.Contains("stair") || identity.Contains("step") || identity.Contains("mây") || identity.Contains("hóa thánh") || identity.Contains("datlang") || identity.Contains("dongsong")) continue;
            var visuals = prop.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (visuals.Length == 0) continue;
            Bounds bounds = visuals[0].bounds;
            foreach (var visual in visuals) bounds.Encapsulate(visual.bounds);
            if (bounds.size.x > 28f || bounds.size.z > 28f || bounds.size.y < 1.25f || bounds.min.y > 2f || bounds.max.y < 1.5f) continue;
            Vector3 flatCenter = bounds.center; flatCenter.y = 0;
            if (!BoundsTouchesRoad(bounds, RoadWidth * .5f + 1f)) continue;
            Vector3 nearest, side; NearestRoad(flatCenter, out nearest, out side);
            float sign = Vector3.Dot(flatCenter - nearest, side) >= 0f ? 1f : -1f;
            float radius = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);
            Vector3 target = nearest + side * sign * (RoadWidth * .5f + radius + 2f);
            // At intersections use the side with the most room. No props relocate onto another road.
            if (RoadDistance(target) < RoadWidth * .5f + radius + .5f) {
                target = nearest - side * sign * (RoadWidth * .5f + radius + 3f);
                if (RoadDistance(target) < RoadWidth * .5f + radius + .5f) continue;
            }
            Vector3 delta = target - flatCenter; delta.y = 0;
            prop.transform.position += delta; relocatedProps++;
        }
        Physics.SyncTransforms();
    }

    private static void NearestRoad(Vector3 point, out Vector3 nearest, out Vector3 side)
    {
        nearest = point; side = Vector3.right; float best = float.MaxValue;
        foreach (var connection in Links) {
            Vector3 a = Plan.Find(z => z.id == connection.from).center, b = Plan.Find(z => z.id == connection.to).center;
            Vector3 line = b - a; float t = Mathf.Clamp01(Vector3.Dot(point - a, line) / line.sqrMagnitude);
            Vector3 candidate = a + line * t; float distance = Vector3.Distance(point, candidate);
            if (distance >= best) continue;
            best = distance; nearest = candidate; side = Vector3.Cross(line.normalized, Vector3.up);
        }
    }

    private static bool BoundsTouchesRoad(Bounds bounds, float extraWidth)
    {
        foreach (var connection in Links) {
            Vector3 a = Plan.Find(z => z.id == connection.from).center, b = Plan.Find(z => z.id == connection.to).center;
            int count = Mathf.CeilToInt(Vector3.Distance(a, b) / 4f);
            for (int i = 0; i <= count; i++) {
                Vector3 point = Vector3.Lerp(a, b, i / (float)count);
                if (point.x >= bounds.min.x - extraWidth && point.x <= bounds.max.x + extraWidth && point.z >= bounds.min.z - extraWidth && point.z <= bounds.max.z + extraWidth) return true;
            }
        }
        return false;
    }

    /// <summary>Read-only editor audit. Call after Build, before Play Mode, to inspect ground support and road barriers.</summary>
    public static string Audit()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run the authored-scene audit outside Play Mode.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != OutputPath) throw new InvalidOperationException("Open the authored Dai Viet scene before auditing.");
        Physics.SyncTransforms();
        var lines = new StringBuilder(); var blockers = new HashSet<string>();
        int samples = 0, unsupported = 0;
        foreach (var connection in Links) {
            Vector3 a = Plan.Find(z => z.id == connection.from).center, b = Plan.Find(z => z.id == connection.to).center;
            int count = Mathf.CeilToInt(Vector3.Distance(a, b) / 6f);
            for (int i = 0; i <= count; i++) {
                var p = Vector3.Lerp(a, b, i / (float)count); samples++;
                bool support = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(hit => hit.collider.gameObject.scene == scene && hit.normal.y > .6f && hit.point.y >= -.4f && hit.point.y <= .6f);
                if (!support) { unsupported++; lines.AppendLine("Unsupported road sample " + connection.from + " → " + connection.to + " " + p); }
                foreach (var collider in Physics.OverlapCapsule(p + Vector3.up * .9f, p + Vector3.up * 2.1f, .65f, ~0, QueryTriggerInteraction.Ignore)) {
                    if (collider.gameObject.scene != scene || collider.GetComponentInParent<ThanhGiongCampaignController>() != null || collider.GetComponentInParent<ThanhGiongEnemy>() != null) continue;
                    if (collider.bounds.max.y < .35f) continue;
                    blockers.Add(collider.transform.root.name + "/" + collider.name + " near " + p);
                }
            }
        }
        lines.Insert(0, "Road audit: " + samples + " samples; " + unsupported + " unsupported; " + blockers.Count + " center-line barriers.\n");
        foreach (string blocker in blockers.Take(60)) lines.AppendLine("Barrier: " + blocker);
        File.WriteAllText(Verification + "/road-audit.txt", lines.ToString(), new UTF8Encoding(false));
        return lines.ToString();
    }

    private static void CleanLegacyComponents()
    {
        foreach (var checkpoint in FindInScene<LegendCheckpoint>(destination)) checkpoint.enabled = false;
        foreach (var guide in FindInScene<KayKitMapGuide>(destination).ToArray()) Object.DestroyImmediate(guide);
        foreach (var component in FindInScene<MonoBehaviour>(destination).ToArray()) {
            if (component == null) continue;
            string type = component.GetType().Name;
            if (type.IndexOf("NavMeshSurface", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("SceneGateway", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("SceneExit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.EndsWith("RuntimeVerification", StringComparison.Ordinal) || type.EndsWith("RuntimeVerifier", StringComparison.Ordinal))
                Object.DestroyImmediate(component);
        }
    }

    private static Material Mat(string key, Color color)
    {
        Material material;
        if (Materials.TryGetValue(key, out material)) return material;
        string path = Generated + "/" + key + ".mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("ThanhGiong/Adventure/Cel Rim Lit")
                        ?? Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
        if (material == null) {
            material = new Material(shader) { name = key }; AssetDatabase.CreateAsset(material, path);
        } else if (shader != null) {
            material.shader = shader;
        }
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .14f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material); Materials[key] = material; return material;
    }

    private static Material ZeldaRoadMaterial()
    {
        const string key = "ZeldaRoadEarth";
        Material material;
        if (Materials.TryGetValue(key, out material) && material != null) return material;
        string path = Generated + "/" + key + ".mat";
        Shader shader = Shader.Find("ThanhGiong/Adventure/ZeldaRoadPath")
                        ?? Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) {
            material = new Material(shader) { name = key };
            AssetDatabase.CreateAsset(material, path);
        } else {
            material.shader = shader;
        }
        if (material.HasProperty("_DirtColor")) material.SetColor("_DirtColor", new Color(0.74f, 0.59f, 0.39f, 1f));
        if (material.HasProperty("_TrackColor")) material.SetColor("_TrackColor", new Color(0.56f, 0.43f, 0.27f, 1f));
        if (material.HasProperty("_VergeGrassColor")) material.SetColor("_VergeGrassColor", new Color(0.39f, 0.62f, 0.26f, 1f));
        if (material.HasProperty("_StoneColor")) material.SetColor("_StoneColor", new Color(0.66f, 0.63f, 0.56f, 1f));
        EditorUtility.SetDirty(material);
        Materials[key] = material;
        return material;
    }

    private static Material ZeldaWaterMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Meshy/Environment/Mat_DongSong_NuocBiec.mat");
        if (existing != null) return existing;
        return Mat("CoastalWater", new Color(.16f, .58f, .64f));
    }

    private static Material ZeldaGrassMaterial()
    {
        const string key = "ZeldaWindGrass";
        Material material;
        if (Materials.TryGetValue(key, out material) && material != null) return material;
        string path = Generated + "/" + key + ".mat";
        Shader shader = Shader.Find("ThanhGiong/Adventure/Interactive Foliage")
                        ?? AssetDatabase.LoadAssetAtPath<Shader>(Generated + "/OpenWorldBiomeGround.shader");
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) {
            material = new Material(shader) { name = key };
            AssetDatabase.CreateAsset(material, path);
        } else if (shader != null) {
            material.shader = shader;
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.45f, 0.72f, 0.28f, 1f));
        if (material.HasProperty("_TipColor")) material.SetColor("_TipColor", new Color(0.84f, 0.88f, 0.38f, 1f));
        if (material.HasProperty("_ShadowTint")) material.SetColor("_ShadowTint", new Color(0.20f, 0.38f, 0.26f, 1f));
        if (material.HasProperty("_WindAmplitude")) material.SetFloat("_WindAmplitude", 0.14f);
        if (material.HasProperty("_WindSpeed")) material.SetFloat("_WindSpeed", 1.85f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        Materials[key] = material;
        return material;
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator); EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }

    private static Material VertexGroundMaterial()
    {
        const string shaderSource = @"Shader ""ThanhGiong/OpenWorldBiomeGround""
{
    SubShader
    {
        Tags { ""RenderPipeline""=""UniversalPipeline"" ""RenderType""=""Opaque"" ""Queue""=""Geometry"" }
        Pass
        {
            Name ""ForwardLit""
            Tags { ""LightMode""=""UniversalForward"" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  color      : COLOR;
                float  fogFactor  : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.color      = v.color;
                o.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 N = normalize(i.normalWS);
                float2 wp = i.positionWS.xz;

                // Multi-octave painterly BotW/TotK meadow variation
                float macroPatch = ValueNoise(wp * 0.035);
                float midBrush   = ValueNoise(wp * 0.16 + float2(macroPatch * 1.7, -macroPatch * 1.3));
                float microBlade = ValueNoise(wp * 1.35);

                half3 baseAlbedo = i.color.rgb;
                half3 sunlitMeadow = baseAlbedo * half3(1.16h, 1.18h, 0.88h);
                half3 coolClover   = baseAlbedo * half3(0.82h, 0.96h, 0.90h);
                half3 meadowCol    = lerp(coolClover, sunlitMeadow, smoothstep(0.25, 0.78, macroPatch * 0.65 + midBrush * 0.35));
                meadowCol *= lerp(0.93h, 1.07h, (half)microBlade);

                // Subtle wildflower & golden clover speckles in lush grassland
                float flowerNoise = ValueNoise(wp * 0.85 + 19.3);
                half flowerMask = saturate((flowerNoise - 0.82) * 5.5) * saturate(N.y * 2.0 - 1.0);
                meadowCol = lerp(meadowCol, half3(0.95h, 0.88h, 0.38h), flowerMask * 0.32h);

                // Slope-based karst limestone cliff blending
                half slope = 1.0h - saturate(N.y);
                half cliffMask = smoothstep(0.16h, 0.42h, slope);
                float rockStrata = ValueNoise(float2(wp.x * 0.18 + i.positionWS.y * 0.55, wp.y * 0.18));
                half3 cliffRock = lerp(half3(0.42h, 0.44h, 0.40h), half3(0.58h, 0.55h, 0.47h), (half)rockStrata);
                half3 surfaceAlbedo = lerp(meadowCol, cliffRock, cliffMask);

                // Drifting Zelda cloud shadow dapple across the plains
                float2 cloudUV = wp * 0.0085 + float2(_Time.y * 0.06, _Time.y * 0.035);
                float cloudDapple = lerp(0.86, 1.04, smoothstep(0.30, 0.72, ValueNoise(cloudUV)));

                // Main directional light + real-time shadow cascades
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(N, mainLight.direction));
                half lit = ndl * mainLight.shadowAttenuation * (half)cloudDapple;

                // 3-band Zelda cel-shaded terrain lighting with warm golden sunlight & cool sky-blue shadows
                half band1 = smoothstep(0.18h, 0.36h, lit);
                half band2 = smoothstep(0.58h, 0.82h, lit);
                half celLight = band1 * 0.72h + band2 * 0.28h;

                half3 shadowColor = surfaceAlbedo * half3(0.52h, 0.62h, 0.72h);
                half3 litColor    = surfaceAlbedo * mainLight.color * 1.08h;
                half3 color       = lerp(shadowColor, litColor, celLight);

                // Soft horizon aerial perspective
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}";
        string shaderPath = Generated + "/OpenWorldBiomeGround.shader";
        File.WriteAllText(shaderPath, shaderSource, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) throw new InvalidOperationException("Could not import biome ground shader.");
        string materialPath = Generated + "/BiomeGround.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
        else { material.shader = shader; EditorUtility.SetDirty(material); }
        return material;
    }

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = Generated + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
    }

    private static void WritePlanAsset()
    {
        var text = new StringBuilder("# Đại Việt – 13 vùng trên một scene\n\n");
        text.AppendLine("Seed 15102026. 730 × 720m ground. Roads 12m wide. M opens the map; G gathers/forges; B stores at the safe hub.");
        text.AppendLine("\n| Zone | Risk | Biome | Center X,Z | Resources primary / secondary / rare |\n| --- | --- | --- | --- | --- |");
        foreach (var zone in Plan) text.AppendLine("| " + zone.displayName + " | " + zone.risk + " | " + zone.biome + " | " + zone.center.x + ", " + zone.center.z + " | " + (zone.risk == OpenWorldRisk.Safe ? "Safe hub / forge / storage" : string.Join(" / ", Enumerable.Range(0, 3).Select(i => ThanhGiongOpenWorld.ResourceFor(zone.biome, i).ToString()))) + " |");
        text.AppendLine("\nEach biome uses exactly three resource types, with 6 primary, 3 secondary and 1 rare node. Modular decorative rings rotate by biome; original authored locations are preserved.");
        text.AppendLine("\nConnections (physical continuous roads; no exits or loading screens):");
        foreach (var link in Links) text.AppendLine("- " + link.from + " ↔ " + link.to);
        File.WriteAllText(Verification + "/world-plan.md", text.ToString(), new UTF8Encoding(false));
    }

    private static void ValidateAuthoredScene(ThanhGiongOpenWorld world, string[] sources, Dictionary<string, string> hashes)
    {
        if (FindInScene<ThanhGiongCampaignController>(destination).Count() != 1) throw new InvalidOperationException("World must have exactly one campaign player.");
        if (FindInScene<Camera>(destination).Count(c => c.enabled) != 1) throw new InvalidOperationException("World must have exactly one enabled camera.");
        if (FindInScene<ThanhGiongCampaignHUD>(destination).Count() != 1) throw new InvalidOperationException("World must have exactly one campaign HUD.");
        if (FindInScene<ThanhGiongCampaignVFX>(destination).Count() != 1) throw new InvalidOperationException("World must have exactly one campaign VFX system.");
        if (FindInScene<Light>(destination).Count(l => l.type == LightType.Directional && l.enabled) != 1) throw new InvalidOperationException("World must have exactly one sun.");
        if (world.zones.Count != 13 || world.connections.Count != 18 || resourceCount != 120) throw new InvalidOperationException("Zone/resource/connection counts do not match the plan.");
        if (campaign.ascensionTarget == null || campaign.ascensionTarget.gameObject.scene != destination) throw new InvalidOperationException("Ascension target was not preserved.");
        foreach (var source in sources) if (HashFile(SceneFolder + source + ".unity") != hashes[source]) throw new InvalidOperationException("Source was unexpectedly modified: " + source);
        foreach (var zone in Plan.Where(z => z.risk != OpenWorldRisk.Safe)) {
            var nodes = FindInScene<ThanhGiongResourceNode>(destination).Where(n => zone.Contains(n.transform.position)).ToArray();
            if (nodes.Length != 10 || nodes.Select(n => n.resource).Distinct().Count() != 3) throw new InvalidOperationException("Resource matrix mismatch in " + zone.id);
        }
        var visited = new HashSet<string> { "hub" };
        for (int i = 0; i < Plan.Count; i++) foreach (var link in Links) {
            if (visited.Contains(link.from)) visited.Add(link.to);
            if (visited.Contains(link.to)) visited.Add(link.from);
        }
        if (visited.Count != 13) throw new InvalidOperationException("World has disconnected regions.");
        Report.AppendLine("Authoring checks: one player / camera / HUD / VFX / sun; 13 reachable zones; exact biome matrix; all original scene SHA256 hashes unchanged.");
    }

    private static string HashFile(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    private static void BackupOutputAndBuildSettings()
    {
        string folder = Verification + "/Backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(folder);
        if (File.Exists(OutputPath)) File.Copy(OutputPath, folder + "/ThanhGiongDaiViet.unity", false);
        if (File.Exists(OutputPath + ".meta")) File.Copy(OutputPath + ".meta", folder + "/ThanhGiongDaiViet.unity.meta", false);
        const string settings = "ProjectSettings/EditorBuildSettings.asset";
        if (File.Exists(settings)) File.Copy(settings, folder + "/EditorBuildSettings.asset", false);
    }

    private static void SetBuildScene()
    {
        var existing = EditorBuildSettings.scenes.Where(s => s.path != OutputPath).Select(s => new EditorBuildSettingsScene(s.path, false));
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(OutputPath, true) }.Concat(existing).ToArray();
    }
}
