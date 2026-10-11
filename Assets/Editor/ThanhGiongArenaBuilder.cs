using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Explicit authoring command. Never rebuilds scenes on editor startup.</summary>
public static class ThanhGiongArenaBuilder
{
    public const string ScenePath = "Assets/Scenes/ThanhGiongArena/PhuDongArena.unity";
    const string Materials = "Assets/Materials/ThanhGiongArena";

    [MenuItem("Tools/Thanh Giong/Build Phu Dong Arena")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building the arena.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before building the arena.");
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)); Directory.CreateDirectory(Materials);
        AssetDatabase.Refresh();
        GameObject playerPrefab = Load("Assets/Prefabs/Player/ThanhGiong_Mounted.prefab");
        GameObject soldierPrefab = Load("Assets/Prefabs/Enemies/KayKit_Barbarian_Soldier.prefab");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material grass = Mat("Grass", new Color(.28f, .43f, .17f));
        Material dirt = Mat("Earth", new Color(.32f, .28f, .19f));
        Material timber = Mat("Timber", new Color(.25f, .13f, .055f));
        Material wall = Mat("WovenWalls", new Color(.66f, .48f, .25f));
        Material thatch = Mat("Thatch", new Color(.59f, .43f, .17f));
        Material rod = Mat("IronRod", new Color(.18f, .22f, .24f), .7f);
        Material tre = Mat("Bamboo", new Color(.43f, .65f, .16f));
        tre.EnableKeyword("_EMISSION"); tre.SetColor("_EmissionColor", new Color(.04f, .13f, .025f));
        Material banner = Mat("Banner", new Color(.63f, .08f, .05f));
        Transform environment = new GameObject("Phu Dong · fields, bamboo and thatched village").transform;
        Box("Grass plain", environment, new Vector3(0, -.6f, 0), new Vector3(160, 1, 160), grass);
        var field = Primitive("Central battlefield", PrimitiveType.Cylinder, environment, new Vector3(0, -.12f, 0), new Vector3(52, .04f, 52), dirt);
        field.GetComponent<Collider>().enabled = false;
        Box("Walkable ground", environment, new Vector3(0, -.15f, 0), new Vector3(65, .1f, 65), dirt);
        for (int i = 0; i < 4; i++)
        {
            bool x = i < 2;
            var boundary = Box("Arena boundary", environment, x ? new Vector3(i == 0 ? -32 : 32, 3, 0) : new Vector3(0, 3, i == 2 ? -32 : 32), x ? new Vector3(1, 8, 65) : new Vector3(65, 8, 1), grass);
            boundary.GetComponent<Renderer>().enabled = false;
        }
        Hut(environment, new Vector3(-15, -.1f, 37), timber, wall, thatch);
        Hut(environment, new Vector3(0, -.1f, 40), timber, wall, thatch);
        Hut(environment, new Vector3(15, -.1f, 37), timber, wall, thatch);
        GameObject treePrefab = Load("Assets/Prefabs/Environment/Foliage/TreeCluster_PuffyCrown_Painterly.prefab");
        for (int i = 0; i < 28; i++)
        {
            float angle = i * Mathf.PI * 2 / 28;
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, scene);
            tree.transform.SetParent(environment); tree.transform.position = new Vector3(Mathf.Sin(angle) * (37 + i % 3 * 3), -.1f, Mathf.Cos(angle) * (39 + i % 3 * 4));
            tree.transform.localScale *= .65f + i % 3 * .12f;
        }
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * .25f;
            Vector3 p = new Vector3(Mathf.Sin(a) * 27, 0, Mathf.Cos(a) * 27);
            Box("Wooden banner pole", environment, p + Vector3.up * 2.5f, new Vector3(.14f, 5, .14f), timber);
            Box("Red battle pennant", environment, p + new Vector3(.65f, 4.1f, 0), new Vector3(1.3f, 1.3f, .045f), banner);
        }
        var grove = (GameObject)PrefabUtility.InstantiatePrefab(Load("Assets/Prefabs/Environment/Foliage/BambooGrove_Vietnamese_Legend.prefab"), scene);
        grove.name = "Khóm tre ngà · nhấn E ba lần"; grove.transform.SetParent(environment); grove.transform.position = new Vector3(-19, -.1f, 16);
        foreach (var old in grove.GetComponentsInChildren<ThanhGiongCollectible>(true)) UnityEngine.Object.DestroyImmediate(old);
        foreach (var c in grove.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        var bamboo = grove.AddComponent<ThanhGiongCollectible>(); bamboo.kind = ThanhGiongCollectible.Kind.Bamboo; bamboo.spinSpeed = 0; bamboo.displayName = "Khóm tre ngà";
        var trigger = grove.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 3.5f; trigger.center = Vector3.up;
        foreach (var r in grove.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = tre;

        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = "Thánh Gióng · arena player"; player.transform.SetPositionAndRotation(new Vector3(0, .08f, -8), Quaternion.identity);
        var movement = player.GetComponent<MountedHorseController>(); movement.walkSpeed = 5.2f; movement.runSpeed = 8.8f;
        var campaign = player.GetComponent<ThanhGiongCampaignController>();
        if (campaign == null) campaign = player.AddComponent<ThanhGiongCampaignController>();
        campaign.startingChapter = ThanhGiongCampaignController.Chapter.Battle; campaign.maxHealth = 180f; campaign.battlePressureScale = 1.15f; campaign.startingBattlePressure = 0f;
        if (player.GetComponent<ThanhGiongCampaignAudio>() == null) player.AddComponent<ThanhGiongCampaignAudio>();
        foreach (var c in player.GetComponentsInChildren<LegendCheckpoint>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in player.GetComponentsInChildren<LegendSeedSkills>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in player.GetComponentsInChildren<GodotMountedMotion>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var t in player.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Weapon_IronSword" || t.name == "Weapon_GoldenBamboo").ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
        Transform hand = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Rider") ?? movement.visual ?? player.transform;
        // The old prefab contains saved runtime ribbon geometry; create it afresh at runtime.
        foreach (var t in player.GetComponentsInChildren<Transform>(true).Where(t => t.name == "RibbonSilk_Left" || t.name == "RibbonSilk_Right" || t.name == "Dải Lụa Đỏ" || t.name == "Vệt Kiếm Vàng").ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
        var cape = player.GetComponent<ThanhGiongRibbonCape>();
        if (cape != null)
        {
            cape.anchorTransform = hand; cape.anchorOffset = new Vector3(0, 1.35f, -.2f);
            cape.segmentCount = 14; cape.segmentLength = .09f; cape.ribbonStartWidth = .08f; cape.ribbonMidWidth = .16f; cape.ribbonEndWidth = .06f;
            cape.windFlutter = .12f; cape.gallopWaveForce = .25f; cape.airDrag = 1f; cape.gravity = 2.8f;
        }
        Mesh arenaSkin = CreateArenaSkin();
        var smr = player.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null)
        {
            smr.sharedMesh = arenaSkin;
            Bounds b = arenaSkin.bounds;
            b.Expand(.6f);
            smr.localBounds = b;
        }
        var ironWeapon = Primitive("Weapon_IronSword", PrimitiveType.Cylinder, hand, Vector3.zero, new Vector3(.055f, .85f, .055f), rod);
        ironWeapon.transform.localPosition = new Vector3(.29f, 1.45f, .03f); ironWeapon.transform.localRotation = Quaternion.Euler(18, 0, -15);
        UnityEngine.Object.DestroyImmediate(ironWeapon.GetComponent<Collider>());
        var bambooWeapon = Primitive("Weapon_GoldenBamboo", PrimitiveType.Cylinder, hand, Vector3.zero, new Vector3(.09f, 1.25f, .09f), tre);
        bambooWeapon.transform.localPosition = new Vector3(.29f, 1.55f, .05f); bambooWeapon.transform.localRotation = Quaternion.Euler(18, 0, -15);
        UnityEngine.Object.DestroyImmediate(bambooWeapon.GetComponent<Collider>()); bambooWeapon.SetActive(false);
        var effects = new GameObject("Arena combat and victory effects").AddComponent<ThanhGiongCampaignVFX>(); campaign.vfx = effects;
        var waves = new GameObject("Arena wave manager").AddComponent<ThanhGiongArenaWaveManager>();
        waves.enemyRoot = new GameObject("Spawned enemies · round owned").transform; campaign.battleRoot = waves.enemyRoot.gameObject;
        waves.enemyPrefab = soldierPrefab.GetComponent<ThanhGiongEnemy>();
        if (waves.enemyPrefab == null) throw new InvalidOperationException("Soldier prefab has no ThanhGiongEnemy.");
        waves.spawnPoints = new Transform[6];
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3;
            var p = new GameObject("Enemy spawn " + (i + 1)).transform;
            p.position = new Vector3(Mathf.Sin(a) * 23, .2f, Mathf.Cos(a) * 23); p.LookAt(Vector3.zero); waves.spawnPoints[i] = p;
        }
        if (player.GetComponent<ThanhGiongArenaInput>() == null) player.AddComponent<ThanhGiongArenaInput>();
        var flow = player.AddComponent<ThanhGiongArenaFlow>(); flow.waveManager = waves; flow.bamboo = bamboo;
        new GameObject("Arena HUD").AddComponent<ThanhGiongArenaHUD>().arena = flow;
        var camera = new GameObject("Arena third person camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.nearClipPlane = .15f; camera.farClipPlane = 220; camera.backgroundColor = new Color(.55f, .72f, .78f); camera.clearFlags = CameraClearFlags.SolidColor;
        var follow = camera.gameObject.AddComponent<IsometricCameraFollow>(); follow.target = player.transform;
        follow.arenaPerspective = true; follow.useOrthographicIsometric = false; follow.fieldOfView = 58; follow.arenaDistance = 11; follow.lookHeight = 1.1f;
        follow.SnapToTarget();
        var sun = new GameObject("Late afternoon sunlight").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -32, 0); sun.color = new Color(1, .9f, .73f);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.64f, .7f, .65f); RenderSettings.fog = true; RenderSettings.fogColor = camera.backgroundColor; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 140;
        EditorSceneManager.SaveScene(scene, ScenePath);
        var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        previous.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = previous.ToArray();
        AssetDatabase.SaveAssets(); Selection.activeGameObject = player;
        Debug.Log("[Arena] Authored PhuDongArena and preserved all previous Build Settings entries.");
    }

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
    static Material Mat(string name, Color color, float metallic = 0)
    {
        string path = Materials + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", .15f); EditorUtility.SetDirty(m); return m;
    }
    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = position; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = material; return g;
    }
    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material) => Primitive(name, PrimitiveType.Cube, parent, position, scale, material);
    static void Hut(Transform parent, Vector3 p, Material timber, Material wall, Material thatch)
    {
        var root = new GameObject("Nhà tranh · village house").transform; root.SetParent(parent); root.position = p;
        Box("Raised wooden floor", root, new Vector3(0, .65f, 0), new Vector3(7, .25f, 5), timber);
        Box("Woven bamboo walls", root, new Vector3(0, 2.05f, .2f), new Vector3(6.6f, 2.7f, 4.3f), wall);
        Box("Dark doorway", root, new Vector3(0, 1.5f, -2), new Vector3(1.2f, 2, .06f), timber);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            Box("Wooden house post", root, new Vector3(x * 3.3f, 1.6f, z * 2.3f), new Vector3(.25f, 3.4f, .25f), timber);
        Box("Thatch left slope", root, new Vector3(-1.85f, 3.75f, 0), new Vector3(4.2f, .35f, 6), thatch).transform.localRotation = Quaternion.Euler(0, 0, 25);
        Box("Thatch right slope", root, new Vector3(1.85f, 3.75f, 0), new Vector3(4.2f, .35f, 6), thatch).transform.localRotation = Quaternion.Euler(0, 0, -25);
    }

    [MenuItem("Tools/Thanh Giong/Build Arena Windows Demo")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory("Builds/PhuDongArena");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = "Builds/PhuDongArena/ThanhGiongArena.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        Directory.CreateDirectory("Artifacts/ArenaValidation");
        File.WriteAllText("Artifacts/ArenaValidation/build-result.json", JsonUtility.ToJson(new BuildResult {
            result = report.summary.result.ToString(), errors = report.summary.totalErrors,
            warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize
        }, true));
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Arena build failed: " + report.summary.result);
    }
    public static Mesh CreateArenaSkin()
    {
        string arenaSkinPath = "Assets/Art/Models/ThanhGiong_Mounted_ArenaSkin.asset";
        var sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Models/ThanhGiong_Mounted_HorseSkin.asset");
        if (sourceMesh == null) sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Models/base.obj");
        if (sourceMesh == null) throw new InvalidOperationException("Source mesh missing for arena skin generation.");

        var vertices = sourceMesh.vertices;
        int[] comp = new int[vertices.Length];
        for (int i = 0; i < comp.Length; i++) comp[i] = i;
        int Find(int i) => comp[i] == i ? i : (comp[i] = Find(comp[i]));
        void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) comp[ra] = rb; }

        for (int s = 0; s < sourceMesh.subMeshCount; s++)
        {
            var tris = sourceMesh.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Union(tris[i], tris[i + 1]);
                Union(tris[i + 1], tris[i + 2]);
            }
        }

        var isSpearGroup = Enumerable.Range(0, vertices.Length)
            .GroupBy(Find)
            .ToDictionary(
                g => g.Key,
                g => {
                    var pts = g.Select(idx => vertices[idx]).ToArray();
                    var min = new Vector3(pts.Min(p => p.x), pts.Min(p => p.y), pts.Min(p => p.z));
                    return min.x > 0.15f && min.y >= 1.165f && min.z >= -0.08f;
                });

        var newMesh = UnityEngine.Object.Instantiate(sourceMesh);
        newMesh.name = "ThanhGiong_Mounted_ArenaSkin";
        newMesh.subMeshCount = sourceMesh.subMeshCount;
        for (int s = 0; s < sourceMesh.subMeshCount; s++)
        {
            var tris = sourceMesh.GetTriangles(s);
            var kept = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                int root = Find(tris[i]);
                if (!isSpearGroup[root])
                {
                    kept.Add(tris[i]);
                    kept.Add(tris[i + 1]);
                    kept.Add(tris[i + 2]);
                }
            }
            newMesh.SetTriangles(kept.ToArray(), s);
        }
        newMesh.RecalculateBounds();

        Mesh stored = AssetDatabase.LoadAssetAtPath<Mesh>(arenaSkinPath);
        if (stored == null)
        {
            AssetDatabase.CreateAsset(newMesh, arenaSkinPath);
            stored = newMesh;
        }
        else
        {
            EditorUtility.CopySerialized(newMesh, stored);
            EditorUtility.SetDirty(stored);
            UnityEngine.Object.DestroyImmediate(newMesh);
        }
        AssetDatabase.SaveAssets();
        return stored;
    }

    [MenuItem("Tools/Thanh Giong/Inspect Spear")]
    public static void InspectSpear()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Models/ThanhGiong_Mounted_HorseSkin.asset");
        if (mesh == null) mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Models/base.obj");
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        int[] comp = new int[vertices.Length];
        for (int i = 0; i < comp.Length; i++) comp[i] = i;
        int Find(int i) => comp[i] == i ? i : (comp[i] = Find(comp[i]));
        void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) comp[ra] = rb; }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Union(triangles[i], triangles[i + 1]);
            Union(triangles[i + 1], triangles[i + 2]);
        }
        var groups = Enumerable.Range(0, vertices.Length)
            .GroupBy(Find)
            .Select(g => {
                var pts = g.Select(idx => vertices[idx]).ToArray();
                var min = new Vector3(pts.Min(p => p.x), pts.Min(p => p.y), pts.Min(p => p.z));
                var max = new Vector3(pts.Max(p => p.x), pts.Max(p => p.y), pts.Max(p => p.z));
                return new {
                    root = g.Key, count = pts.Length,
                    min = min.ToString("F3"), max = max.ToString("F3"),
                    center = ((min + max) * 0.5f).ToString("F3"),
                    size = (max - min).ToString("F3")
                };
            })
            .OrderByDescending(g => g.count)
            .ToArray();
        string json = JsonUtility.ToJson(new ComponentList { items = groups.Select(g => $"{g.root}: count={g.count}, center={g.center}, size={g.size}, min={g.min}, max={g.max}").ToArray() }, true);
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/spear_components.txt", json);
        Debug.Log("[SpearInspection] Components:\n" + json);
    }
    [Serializable] sealed class ComponentList { public string[] items; }
    [Serializable] sealed class BuildResult { public string result; public int errors, warnings; public double seconds; public ulong bytes; }
}
