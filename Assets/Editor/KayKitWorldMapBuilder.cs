using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KayKitWorldMapBuilder
{
    private const string Output = "Assets/Scenes/ThanhGiongWorld";
    private const string Generated = "Assets/Materials/World";
    private const string Adventurers = "Assets/Plugins/KayKit_Adventurers_2.0_FREE";
    private const string Dungeon = "Assets/Plugins/KayKit_Dungeon_Pack_1.1_FREE";
    private const string Forest = "Assets/Plugins/KayKit_Forest_Nature_Pack_1.0_FREE";
    private const string Medieval = "Assets/Plugins/KayKit_Medieval_Hexagon_Pack_1.0_FREE";
    private const string KenneyForest = "Assets/Plugins/kenney_mini-forest_1.0";
    private const string BuildVersion = "2026.10.02-kaykit-forest-remaster-v6";
    private static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, Material> ConvertedMaterials = new Dictionary<string, Material>();

    [InitializeOnLoadMethod]
    private static void BuildWhenReady()
    {
        EditorApplication.update -= TryBuild;
        EditorApplication.update += TryBuild;
    }

    private static void TryBuild()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!AssetDatabase.IsValidFolder(Adventurers) || !AssetDatabase.IsValidFolder(Dungeon) ||
            !AssetDatabase.IsValidFolder(Forest) || !AssetDatabase.IsValidFolder(Medieval) ||
            !AssetDatabase.IsValidFolder(KenneyForest)) return;
        EditorApplication.update -= TryBuild;
        string versionKey = "ThanhGiong.KayKitWorlds." + Application.dataPath;
        // Existing authored maps must not be regenerated when the checkout path or preferences change.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Output + "/DinhSocHoaThanh.unity") != null)
        { EditorPrefs.SetString(versionKey, BuildVersion); return; }
        try { BuildAllMaps(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    [MenuItem("Tools/Thanh Giong/Build KayKit World Maps %#k")]
    public static void BuildAllMaps()
    {
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(Generated + "/Materials");
        Models.Clear();
        ConvertedMaterials.Clear();
        BuildVillage();
        BuildRoyalForge();
        BuildDungeon();
        BuildRiverCampaign();
        BuildSocMountain();
        BuildAscensionPeak();
        AddScenesToBuild();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(Output + "/LangGiongTienTuyen.unity", OpenSceneMode.Single);
        EditorPrefs.SetString("ThanhGiong.KayKitWorlds." + Application.dataPath, BuildVersion);
        Debug.Log("[KayKit Worlds] Built 6 maps with fortress, river crossings, villages, battlefield and ascension peak.");
    }

    private static void BuildVillage()
    {
        Scene scene = NewScene("LÀNG GIÓNG TIỀN TUYẾN", "Không gian làng mở, chợ trung tâm, lò rèn và đường xuất quân.", "KinhThanhRenThep");
        GameObject world = new GameObject("ARCHITECTURE - Vietnamese Frontier Village");
        Ground(world.transform, new Vector3(0f, -.55f, 0f), new Vector3(105f, 1f, 105f), new Color(.32f, .58f, .18f));

        string[] buildings = { "building_market_green", "building_blacksmith_green", "building_home_A_green", "building_home_B_green", "building_barracks_green", "building_lumbermill_green", "building_well_green", "building_tavern_green" };
        Vector3[] positions = { new(-12,0,12), new(12,0,12), new(-20,0,-5), new(20,0,-5), new(-15,0,-22), new(16,0,-22), new(0,0,0), new(0,0,24) };
        for (int i = 0; i < buildings.Length; i++) Place(Medieval, buildings[i], world.transform, positions[i], Quaternion.Euler(0f, Mathf.Atan2(-positions[i].x, -positions[i].z) * Mathf.Rad2Deg, 0f));
        for (int i = -4; i <= 4; i++) Place(Medieval, "hex_road_A", world.transform, new Vector3(0f, -.03f, i * 7f), Quaternion.identity);
        Place(Medieval, "building_bridge_A", world.transform, new Vector3(0f, 0f, -36f), Quaternion.identity);
        BuildKenneyOutpost(world.transform, new Vector3(-34f, 0f, 19f), 35f, true);
        BuildKenneyTrainingGround(world.transform, new Vector3(30f, 0f, -16f), -35f);
        BuildKayKitForestGate(world.transform, new Vector3(0f, 0f, -39f), 0f, false);

        ScatterForest(world.transform, 44f, 46, 101);
        // Unnecessary dummy character placement removed
        // PlaceVillagers(world.transform, 10, 16f, 310);
        CreateLandmark("Cổng xuất quân", new Vector3(0f, 0f, -42f), new Vector3(10f, 5f, 2f), world.transform, new Color(.38f, .18f, .06f));
        SetupPlayerAndCamera(scene, new Vector3(0f, .15f, 7f), false);
        Save(scene, "LangGiongTienTuyen");
    }

    private static void BuildDungeon()
    {
        Scene scene = NewScene("PHÁO ĐÀI NGẦM QUÂN ÂN", "Dungeon nhiều tầng: cổng giam, kho binh khí và đại sảnh chỉ huy.", "ThungLungVuotSong");
        GameObject world = new GameObject("ARCHITECTURE - An Invader Underground Fortress");
        Ground(world.transform, new Vector3(0f, -.65f, 0f), new Vector3(72f, 1f, 58f), new Color(.08f, .09f, .12f));

        for (int x = -6; x <= 6; x++)
            for (int z = -4; z <= 4; z++)
                Place(Dungeon, (x + z) % 7 == 0 ? "floor_tile_small_broken_A" : "floor_tile_small", world.transform, new Vector3(x * 4f, 0f, z * 4f), Quaternion.identity);

        for (int x = -6; x <= 6; x++)
        {
            Place(Dungeon, "wall", world.transform, new Vector3(x * 4f, 0f, -20f), Quaternion.identity);
            Place(Dungeon, x == 0 ? "wall_doorway" : "wall", world.transform, new Vector3(x * 4f, 0f, 20f), Quaternion.Euler(0f, 180f, 0f));
        }
        for (int z = -4; z <= 4; z++)
        {
            Place(Dungeon, "wall", world.transform, new Vector3(-28f, 0f, z * 4f), Quaternion.Euler(0f, 90f, 0f));
            Place(Dungeon, "wall", world.transform, new Vector3(28f, 0f, z * 4f), Quaternion.Euler(0f, -90f, 0f));
        }
        for (int z = -2; z <= 2; z++)
            if (z != 0) Place(Dungeon, "wall_cracked", world.transform, new Vector3(0f, 0f, z * 4f), Quaternion.Euler(0f, 90f, 0f));

        for (int i = 0; i < 8; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Place(Dungeon, "column", world.transform, new Vector3(side * 12f, 0f, -14f + i * 4f), Quaternion.identity);
            Place(Dungeon, "torch_lit", world.transform, new Vector3(side * 12f, 1.7f, -14f + i * 4f), Quaternion.identity, .85f);
        }
        Place(Dungeon, "stairs_wide", world.transform, new Vector3(0f, 0f, 12f), Quaternion.identity);
        Place(Dungeon, "chest_gold", world.transform, new Vector3(0f, 0f, 16f), Quaternion.identity);
        Place(Dungeon, "barrel_small_stack", world.transform, new Vector3(-20f, 0f, 12f), Quaternion.identity);
        Place(Dungeon, "barrel_large_decorated", world.transform, new Vector3(20f, 0f, 12f), Quaternion.identity);
        BuildKayKitForestGate(world.transform, new Vector3(0f, 0f, -23f), 0f, true);
        // Unnecessary dummy character placement removed
        // PlaceGuards(world.transform, 12, 18f, 601);
        SetupPlayerAndCamera(scene, new Vector3(0f, .1f, -15f), true);
        Save(scene, "PhaoDaiNgamQuanAn");
    }

    private static void BuildSocMountain()
    {
        Scene scene = NewScene("TRẬN TUYẾN NÚI SÓC", "Chiến trường mở với pháo đài đỏ, rừng cháy và quảng trường boss.", "DinhSocHoaThanh");
        GameObject world = new GameObject("ARCHITECTURE - Soc Mountain Battlefield");
        Ground(world.transform, new Vector3(0f, -1f, 0f), new Vector3(130f, 2f, 110f), new Color(.2f, .28f, .12f));

        for (int i = -6; i <= 6; i++) Place(Medieval, "hex_road_A", world.transform, new Vector3(0f, 0f, i * 7f), Quaternion.identity);
        Place(Medieval, "building_castle_red", world.transform, new Vector3(0f, 0f, 40f), Quaternion.Euler(0f, 180f, 0f), 1.25f);
        Place(Medieval, "building_barracks_red", world.transform, new Vector3(-22f, 0f, 25f), Quaternion.Euler(0f, 155f, 0f));
        Place(Medieval, "building_archeryrange_red", world.transform, new Vector3(22f, 0f, 25f), Quaternion.Euler(0f, 205f, 0f));
        Place(Medieval, "building_tower_A_red", world.transform, new Vector3(-32f, 0f, 38f), Quaternion.identity);
        Place(Medieval, "building_tower_B_red", world.transform, new Vector3(32f, 0f, 38f), Quaternion.identity);
        Place(Medieval, "building_destroyed", world.transform, new Vector3(-18f, 0f, -8f), Quaternion.Euler(0f, 35f, 0f));
        Place(Medieval, "building_scaffolding", world.transform, new Vector3(20f, 0f, -12f), Quaternion.Euler(0f, -25f, 0f));
        BuildKenneyOutpost(world.transform, new Vector3(-42f, 0f, 15f), 70f, true);
        BuildKayKitForestGate(world.transform, new Vector3(0f, 0f, -42f), 0f, true);
        ScatterForest(world.transform, 50f, 58, 990);
        // Unnecessary dummy character placement removed
        // PlaceGuards(world.transform, 18, 34f, 881);
        CreateLandmark("Bãi nhổ Tre Ngà", new Vector3(-30f, 0f, -25f), new Vector3(7f, 6f, 7f), world.transform, new Color(.22f, .65f, .12f));
        SetupPlayerAndCamera(scene, new Vector3(0f, .15f, -38f), false);
        Save(scene, "TranTuyenNuiSoc");
    }

    private static void BuildRoyalForge()
    {
        Scene scene = NewScene("KINH THÀNH RÈN THÉP", "Thành trung tâm theo bố cục sa bàn: lâu đài, tường thành, xưởng rèn, ruộng và khu dân cư.", "PhaoDaiNgamQuanAn");
        GameObject world = new GameObject("ARCHITECTURE - Van Lang Royal Forge Citadel");
        Ground(world.transform, new Vector3(0f, -.65f, 0f), new Vector3(125f, 1f, 105f), new Color(.28f, .46f, .17f));
        BuildRiver(world.transform, -42f, -45f, 11);
        BuildFortressWalls(world.transform, 31f, 25f, "blue");

        Place(Medieval, "building_castle_blue", world.transform, new Vector3(13f, 0f, 13f), Quaternion.Euler(0f, 180f, 0f), 1.2f);
        Place(Medieval, "building_blacksmith_blue", world.transform, new Vector3(-15f, 0f, 12f), Quaternion.Euler(0f, 145f, 0f));
        Place(Medieval, "building_market_blue", world.transform, new Vector3(0f, 0f, 3f), Quaternion.identity);
        Place(Medieval, "building_barracks_blue", world.transform, new Vector3(18f, 0f, -10f), Quaternion.Euler(0f, 220f, 0f));
        Place(Medieval, "building_home_A_blue", world.transform, new Vector3(-18f, 0f, -9f), Quaternion.Euler(0f, 35f, 0f));
        Place(Medieval, "building_home_B_blue", world.transform, new Vector3(-6f, 0f, -17f), Quaternion.Euler(0f, 15f, 0f));
        Place(Medieval, "building_well_blue", world.transform, new Vector3(2f, 0f, -10f), Quaternion.identity);
        Place(Medieval, "building_bridge_A", world.transform, new Vector3(-42f, 0f, -18f), Quaternion.Euler(0f, 90f, 0f));
        Place(Medieval, "building_bridge_B", world.transform, new Vector3(-42f, 0f, 23f), Quaternion.Euler(0f, 90f, 0f));
        BuildFarm(world.transform, new Vector3(-12f, .04f, 29f), 5, 5);
        BuildKenneyOutpost(world.transform, new Vector3(43f, 0f, 34f), -125f, false);
        BuildKayKitForestGate(world.transform, new Vector3(-42f, 0f, -34f), 90f, false);
        ScatterForest(world.transform, 52f, 55, 1401);
        // Unnecessary dummy character placement removed
        // PlaceVillagers(world.transform, 18, 23f, 1402);
        SetupPlayerAndCamera(scene, new Vector3(0f, .15f, -34f), false);
        Save(scene, "KinhThanhRenThep");
    }

    private static void BuildRiverCampaign()
    {
        Scene scene = NewScene("THUNG LŨNG VƯỢT SÔNG", "Hai đạo quân tranh chấp cầu đá; lều trại và máy bắn đá tạo tuyến bao vây.", "TranTuyenNuiSoc");
        GameObject world = new GameObject("ARCHITECTURE - River Crossing Siege");
        Ground(world.transform, new Vector3(0f, -.7f, 0f), new Vector3(135f, 1f, 105f), new Color(.24f, .4f, .16f));
        BuildRiver(world.transform, 0f, -52f, 15);
        Place(Medieval, "building_bridge_A", world.transform, new Vector3(0f, 0f, -18f), Quaternion.identity);
        Place(Medieval, "building_bridge_B", world.transform, new Vector3(0f, 0f, 22f), Quaternion.identity);

        for (int i = 0; i < 6; i++)
        {
            Place(Medieval, "tent", world.transform, new Vector3(-28f + (i % 3) * 8f, 0f, -28f + (i / 3) * 9f), Quaternion.Euler(0f, 25f, 0f));
            Place(Medieval, "tent", world.transform, new Vector3(15f + (i % 3) * 8f, 0f, 26f - (i / 3) * 9f), Quaternion.Euler(0f, 205f, 0f));
        }
        Place(Medieval, "building_tower_catapult_blue", world.transform, new Vector3(-35f, 0f, -5f), Quaternion.Euler(0f, 65f, 0f));
        Place(Medieval, "building_tower_catapult_red", world.transform, new Vector3(35f, 0f, 5f), Quaternion.Euler(0f, 245f, 0f));
        Place(Medieval, "projectile_catapult", world.transform, new Vector3(-32f, 0f, -17f), Quaternion.identity);
        Place(Medieval, "projectile_catapult", world.transform, new Vector3(32f, 0f, 17f), Quaternion.Euler(0f, 180f, 0f));
        BuildKenneyCamp(world.transform, new Vector3(-43f, 0f, -27f), 28f);
        BuildKenneyCamp(world.transform, new Vector3(43f, 0f, 27f), 208f);
        BuildKayKitForestGate(world.transform, new Vector3(0f, 0f, -43f), 0f, false);
        ScatterForest(world.transform, 50f, 65, 1701);
        // Unnecessary dummy character placement removed
        // PlaceGuards(world.transform, 24, 43f, 1702);
        SetupPlayerAndCamera(scene, new Vector3(-24f, .15f, -34f), false);
        Save(scene, "ThungLungVuotSong");
    }

    private static void BuildAscensionPeak()
    {
        Scene scene = NewScene("ĐỈNH SÓC HÓA THÁNH", "Đường núi xoắn lên đền thiêng; giáp sắt trở thành di tích và Ngựa Sắt bay qua biển mây.", "LangGiongTienTuyen");
        GameObject world = new GameObject("ARCHITECTURE - Sacred Soc Ascension Peak");
        Ground(world.transform, new Vector3(0f, -1f, 0f), new Vector3(105f, 2f, 105f), new Color(.18f, .31f, .16f));
        Material stone = GetColorMaterial("Sacred_Mountain_Stone", new Color(.3f, .34f, .36f));
        for (int tier = 0; tier < 5; tier++)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "Bậc núi thiêng " + (tier + 1); platform.transform.SetParent(world.transform);
            platform.transform.position = new Vector3(tier * 5f - 10f, tier * 2.2f - .5f, tier * 4f - 8f);
            float radius = 20f - tier * 2.7f; platform.transform.localScale = new Vector3(radius, 1.1f, radius);
            platform.GetComponent<Renderer>().sharedMaterial = stone;
        }
        Vector3 summit = new Vector3(10f, 10.5f, 8f);
        Place(Medieval, "building_church_yellow", world.transform, summit, Quaternion.Euler(0f, 180f, 0f));
        Place(Medieval, "building_tower_A_yellow", world.transform, summit + new Vector3(-13f, 0f, 4f), Quaternion.identity);
        Place(Medieval, "building_tower_B_yellow", world.transform, summit + new Vector3(13f, 0f, 4f), Quaternion.identity);
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            Place(Forest, "Rock_3_A_Color1", world.transform, new Vector3(Mathf.Cos(a) * 33f, i % 3, Mathf.Sin(a) * 33f), Quaternion.Euler(0f, i * 31f, 0f), 1.3f + (i % 4) * .25f);
        }
        BuildKenneyPilgrimPath(world.transform, new Vector3(-18f, 1.2f, -12f), 32f);
        BuildKayKitForestGate(world.transform, new Vector3(-10f, 0f, -30f), 18f, false);
        ScatterForest(world.transform, 43f, 42, 1901);
        // Unnecessary dummy character placement removed
        // PlaceVillagers(world.transform, 8, 17f, 1902);
        SetupPlayerAndCamera(scene, new Vector3(-10f, .15f, -27f), false);
        Save(scene, "DinhSocHoaThanh");
    }

    private static void BuildFortressWalls(Transform parent, float halfX, float halfZ, string faction)
    {
        for (int x = -4; x <= 4; x++)
        {
            string south = x == 0 ? "wall_straight_gate" : "wall_straight";
            Place(Medieval, south, parent, new Vector3(x * 7f, 0f, -halfZ), Quaternion.identity);
            Place(Medieval, "wall_straight", parent, new Vector3(x * 7f, 0f, halfZ), Quaternion.Euler(0f, 180f, 0f));
        }
        for (int z = -3; z <= 3; z++)
        {
            Place(Medieval, "wall_straight", parent, new Vector3(-halfX, 0f, z * 7f), Quaternion.Euler(0f, 90f, 0f));
            Place(Medieval, "wall_straight", parent, new Vector3(halfX, 0f, z * 7f), Quaternion.Euler(0f, -90f, 0f));
        }
        string tower = "building_tower_A_" + faction;
        Place(Medieval, tower, parent, new Vector3(-halfX, 0f, -halfZ), Quaternion.identity);
        Place(Medieval, tower, parent, new Vector3(halfX, 0f, -halfZ), Quaternion.identity);
        Place(Medieval, tower, parent, new Vector3(-halfX, 0f, halfZ), Quaternion.identity);
        Place(Medieval, tower, parent, new Vector3(halfX, 0f, halfZ), Quaternion.identity);
    }

    private static void BuildRiver(Transform parent, float x, float startZ, int count)
    {
        for (int i = 0; i < count; i++)
            Place(Medieval, i % 4 == 2 ? "hex_river_A_curvy" : "hex_river_A", parent, new Vector3(x, -.05f, startZ + i * 7f), Quaternion.identity);
    }

    private static void BuildFarm(Transform parent, Vector3 origin, int rows, int columns)
    {
        Material soil = GetColorMaterial("Farm_Soil", new Color(.32f, .17f, .07f));
        Material crop = GetColorMaterial("Farm_Crops", new Color(.48f, .72f, .12f));
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cube); patch.name = "Luống lương thực"; patch.transform.SetParent(parent);
                patch.transform.position = origin + new Vector3(column * 1.6f, 0f, row * 2f); patch.transform.localScale = new Vector3(1.25f, .12f, 1.5f);
                patch.GetComponent<Renderer>().sharedMaterial = row % 2 == 0 ? crop : soil;
            }
    }

    private static void BuildKenneyOutpost(Transform parent, Vector3 origin, float yaw, bool trainingArea)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Place(KenneyForest, "building-structure", parent, origin, rotation);
        Place(KenneyForest, "building-platform", parent, origin + rotation * new Vector3(-5.8f, 0f, 3.2f), rotation);
        Place(KenneyForest, "building-roof", parent, origin + rotation * new Vector3(5.4f, 0f, 2.8f), rotation);
        Place(KenneyForest, "bridge", parent, origin + rotation * new Vector3(0f, 0f, 3.1f), rotation);
        Place(KenneyForest, "ladder", parent, origin + rotation * new Vector3(-2.1f, 0f, -2.2f), rotation);
        Place(KenneyForest, "flag", parent, origin + rotation * new Vector3(-5.8f, 0f, 3.2f), rotation);
        Place(KenneyForest, "rocks-high", parent, origin + rotation * new Vector3(-6.2f, 0f, 3.4f), rotation);
        BuildKenneyFence(parent, origin + rotation * new Vector3(0f, 0f, -5.2f), yaw, 5);
        if (trainingArea) BuildKenneyTrainingGround(parent, origin + rotation * new Vector3(8f, 0f, -5f), yaw);
    }

    private static void BuildKenneyCamp(Transform parent, Vector3 origin, float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Place(KenneyForest, "tent", parent, origin, rotation);
        Place(KenneyForest, "tent", parent, origin + rotation * new Vector3(5.2f, 0f, 2.8f), rotation * Quaternion.Euler(0f, 18f, 0f));
        Place(KenneyForest, "platform", parent, origin + rotation * new Vector3(-5f, 0f, 2f), rotation);
        Place(KenneyForest, "flag", parent, origin + rotation * new Vector3(-5f, 0f, 2f), rotation);
        Place(KenneyForest, "patch-dirt", parent, origin + rotation * new Vector3(1.8f, .01f, -2f), rotation, 1.4f);
        BuildKenneyFence(parent, origin + rotation * new Vector3(0f, 0f, -5f), yaw, 4);
    }

    private static void BuildKenneyTrainingGround(Transform parent, Vector3 origin, float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Place(KenneyForest, "target", parent, origin + rotation * new Vector3(-2.7f, 0f, 0f), rotation);
        Place(KenneyForest, "target", parent, origin, rotation);
        Place(KenneyForest, "target", parent, origin + rotation * new Vector3(2.7f, 0f, 0f), rotation);
        Place(KenneyForest, "weapon-bow", parent, origin + rotation * new Vector3(0f, .15f, -3.2f), rotation, 1.6f);
        Place(KenneyForest, "weapon-arrow", parent, origin + rotation * new Vector3(.7f, .15f, -3.1f), rotation, 1.6f);
        BuildKenneyFence(parent, origin + rotation * new Vector3(0f, 0f, 3.5f), yaw, 3);
    }

    private static void BuildKenneyFence(Transform parent, Vector3 center, float yaw, int count)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        for (int i = 0; i < count; i++)
            Place(KenneyForest, "fence", parent, center + rotation * new Vector3((i - (count - 1) * .5f) * 2.1f, 0f, 0f), rotation);
    }

    private static void BuildKenneyPilgrimPath(Transform parent, Vector3 origin, float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Place(KenneyForest, "rocks-ramp", parent, origin, rotation);
        Place(KenneyForest, "platform", parent, origin + rotation * new Vector3(5f, 1.4f, 2f), rotation);
        Place(KenneyForest, "bridge", parent, origin + rotation * new Vector3(9f, 2.1f, 3.5f), rotation);
        Place(KenneyForest, "flag", parent, origin + rotation * new Vector3(12f, 2.1f, 4.5f), rotation);
    }

    private static Scene NewScene(string title, string description, string next)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject systems = new GameObject("WORLD SYSTEMS");
        KayKitMapGuide guide = systems.AddComponent<KayKitMapGuide>();
        FantasyUIInstaller.Apply(guide);
        guide.mapTitle = title; guide.description = description; guide.nextScene = next;
        GameObject lightGo = new GameObject("Sun");
        Light light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.15f; light.color = new Color(1f, .9f, .72f);
        light.shadows = LightShadows.Soft; lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        RenderSettings.ambientLight = new Color(.42f, .48f, .54f);
        return scene;
    }

    private static void SetupPlayerAndCamera(Scene scene, Vector3 position, bool onFoot)
    {
        string playerPath = onFoot ? "Assets/Prefabs/Player/ThanhGiongKnight.prefab" : "Assets/Prefabs/Player/ThanhGiong_Mounted.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        GameObject player = prefab != null ? PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = onFoot ? "Thánh Gióng - Bộ Chiến" : "Thánh Gióng - Ngựa Sắt";
        player.transform.SetPositionAndRotation(position, Quaternion.identity);

        GameObject cameraGo = new GameObject("Main Camera"); cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.AddComponent<Camera>(); camera.fieldOfView = 52f; camera.farClipPlane = 260f;
        cameraGo.AddComponent<AudioListener>();
        IsometricCameraFollow follow = cameraGo.AddComponent<IsometricCameraFollow>(); follow.target = player.transform; follow.offset = new Vector3(-16f, 19f, -16f); follow.lookHeight = 1.7f;
        cameraGo.transform.position = position + follow.offset;
    }

    private static void ScatterForest(Transform parent, float radius, int count, int seed)
    {
        System.Random random = new System.Random(seed);
        string[] trees = { "Tree_1_A_Color1", "Tree_1_C_Color1", "Tree_2_B_Color1", "Tree_2_D_Color1", "Tree_3_A_Color1", "Tree_3_C_Color1", "Tree_4_A_Color1", "Tree_4_C_Color1", "Tree_Bare_1_A_Color1" };
        string[] rocks = { "Rock_1_A_Color1", "Rock_1_H_Color1", "Rock_2_C_Color1", "Rock_2_G_Color1", "Rock_3_E_Color1" };
        string[] bushes = { "Bush_1_A_Color1", "Bush_1_D_Color1", "Bush_2_A_Color1", "Bush_2_E_Color1", "Bush_3_B_Color1", "Bush_4_C_Color1" };
        string[] grasses = { "Grass_1_A_Color1", "Grass_1_C_Color1", "Grass_2_B_Color1", "Grass_2_D_Color1" };
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(radius * .66f, radius, (float)random.NextDouble());
            Vector3 p = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            bool canopy = i % 5 != 0;
            string model = canopy ? trees[random.Next(trees.Length)] : rocks[random.Next(rocks.Length)];
            float scale = canopy ? Mathf.Lerp(.82f, 1.28f, (float)random.NextDouble()) : Mathf.Lerp(.7f, 1.25f, (float)random.NextDouble());
            Place(Forest, model, parent, p, Quaternion.Euler(0f, random.Next(0, 360), 0f), scale);

            if (i % 2 == 0)
            {
                Vector3 undergrowth = p + new Vector3(Mathf.Lerp(-2.1f, 2.1f, (float)random.NextDouble()), 0f, Mathf.Lerp(-2.1f, 2.1f, (float)random.NextDouble()));
                Place(Forest, bushes[random.Next(bushes.Length)], parent, undergrowth, Quaternion.Euler(0f, random.Next(0, 360), 0f), Mathf.Lerp(.7f, 1.15f, (float)random.NextDouble()));
            }
            if (i % 3 == 0)
            {
                Vector3 grass = p + new Vector3(Mathf.Lerp(-2.6f, 2.6f, (float)random.NextDouble()), 0f, Mathf.Lerp(-2.6f, 2.6f, (float)random.NextDouble()));
                Place(Forest, grasses[random.Next(grasses.Length)], parent, grass, Quaternion.Euler(0f, random.Next(0, 360), 0f), Mathf.Lerp(.75f, 1.25f, (float)random.NextDouble()));
            }
        }
    }

    private static void BuildKayKitForestGate(Transform parent, Vector3 center, float yaw, bool scorched)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        string leftTree = scorched ? "Tree_Bare_1_A_Color1" : "Tree_3_A_Color1";
        string rightTree = scorched ? "Tree_Bare_2_C_Color1" : "Tree_3_C_Color1";
        Place(Forest, "Rock_3_E_Color1", parent, center + rotation * new Vector3(-5.8f, 0f, 0f), rotation, 1.15f);
        Place(Forest, "Rock_3_M_Color1", parent, center + rotation * new Vector3(5.8f, 0f, 0f), rotation * Quaternion.Euler(0f, 160f, 0f), 1.05f);
        Place(Forest, leftTree, parent, center + rotation * new Vector3(-8.2f, 0f, 1.4f), rotation, 1.15f);
        Place(Forest, rightTree, parent, center + rotation * new Vector3(8.2f, 0f, 1.1f), rotation * Quaternion.Euler(0f, 120f, 0f), 1.05f);
        Place(Forest, scorched ? "Bush_1_F_Color1" : "Bush_4_C_Color1", parent, center + rotation * new Vector3(-4.1f, 0f, -1.4f), rotation, 1.1f);
        Place(Forest, scorched ? "Bush_1_G_Color1" : "Bush_2_E_Color1", parent, center + rotation * new Vector3(4.2f, 0f, -1.2f), rotation, 1f);
    }

    private static void PlaceVillagers(Transform parent, int count, float radius, int seed)
    {
        string[] characters = { "Knight", "Ranger", "Mage", "Rogue", "Barbarian" };
        System.Random random = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f;
            Place(Adventurers, characters[i % characters.Length], parent, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius), Quaternion.Euler(0f, random.Next(0, 360), 0f));
        }
    }

    private static void PlaceGuards(Transform parent, int count, float radius, int seed)
    {
        string[] characters = { "Rogue_Hooded", "Barbarian", "Knight" };
        System.Random random = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float a = i * 2.399963f;
            float r = radius * (.3f + .65f * (float)random.NextDouble());
            Place(Adventurers, characters[i % characters.Length], parent, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), Quaternion.Euler(0f, random.Next(0, 360), 0f));
        }
    }

    private static GameObject Place(string root, string exactName, Transform parent, Vector3 position, Quaternion rotation, float scale = 1f)
    {
        GameObject model = FindModel(root, exactName);
        if (model == null) { Debug.LogWarning("[KayKit Worlds] Missing model: " + exactName); return null; }
        GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (instance == null) instance = UnityEngine.Object.Instantiate(model);
        instance.name = exactName;
        instance.transform.SetParent(parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.one * scale;
        NormalizeWorldHeight(instance, root, exactName, position.y, scale);
        EnsureUrpMaterials(instance);
        return instance;
    }

    private static void NormalizeWorldHeight(GameObject instance, string root, string modelName, float groundY, float authoredScale)
    {
        float targetHeight = TargetWorldHeight(root, modelName) * authoredScale;
        if (targetHeight <= 0f) return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y < .001f) return;

        float factor = targetHeight / bounds.size.y;
        instance.transform.localScale *= factor;

        // Keep the base of each normalized asset on the authored ground plane.
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        instance.transform.position += Vector3.up * (groundY - bounds.min.y);
    }

    private static float TargetWorldHeight(string root, string modelName)
    {
        if (root == Adventurers) return 0f;
        string name = modelName.ToLowerInvariant();
        if (root == Forest)
        {
            if (name.StartsWith("tree_bare")) return 6.2f;
            if (name.StartsWith("tree_1")) return 7.2f;
            if (name.StartsWith("tree_2")) return 6.6f;
            if (name.StartsWith("tree_3")) return 7.8f;
            if (name.StartsWith("tree_4")) return 6.9f;
            if (name.StartsWith("rock_3")) return 2.4f;
            if (name.StartsWith("rock_2")) return 1.55f;
            if (name.StartsWith("rock_1")) return .9f;
            if (name.StartsWith("bush_")) return 1.15f;
            if (name.StartsWith("grass_")) return .55f;
            return 0f;
        }
        if (root == KenneyForest)
        {
            if (name == "building-platform") return 7.5f;
            if (name == "building-structure") return 6.5f;
            if (name == "building-roof") return 6f;
            if (name == "platform") return 4.5f;
            if (name == "bridge") return 2.8f;
            if (name == "tent") return 4.2f;
            if (name == "flag") return 4.4f;
            if (name == "ladder") return 3.8f;
            if (name == "target") return 2.2f;
            if (name == "fence") return 1.35f;
            if (name.StartsWith("rocks-")) return 3.2f;
            return 0f;
        }
        if (name.StartsWith("building_castle")) return 24f;
        if (name.StartsWith("building_tower")) return 16f;
        if (name.Contains("bridge")) return 4f;
        if (name.StartsWith("building_")) return 10f;
        if (name.StartsWith("wall_") || name.StartsWith("fence_")) return 5f;
        if (name == "tent") return 4f;
        if (root == Dungeon && name.StartsWith("wall")) return 4.5f;
        if (root == Dungeon && (name.StartsWith("stairs") || name == "column")) return 4f;
        return 0f;
    }

    private static GameObject FindModel(string root, string exactName)
    {
        string key = root + "|" + exactName;
        if (Models.TryGetValue(key, out GameObject cached)) return cached;
        string[] guids = AssetDatabase.FindAssets(exactName + " t:Model", new[] { root });
        string path = guids.Select(AssetDatabase.GUIDToAssetPath)
            .Where(item => string.Equals(Path.GetFileNameWithoutExtension(item), exactName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Replace('\\', '/').Contains("/fbx(unity)/"))
            .FirstOrDefault();
        GameObject model = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Models[key] = model;
        return model;
    }

    private static void EnsureUrpMaterials(GameObject instance)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || source.shader == null || source.shader.name.Contains("Universal Render Pipeline")) continue;
                string sourcePath = AssetDatabase.GetAssetPath(source);
                string key = sourcePath + "|" + source.name;
                if (!ConvertedMaterials.TryGetValue(key, out Material converted))
                {
                    string safe = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                    string hash = StableHash(key).ToString("X8");
                    string output = Generated + "/Materials/" + safe + "_" + hash + ".mat";
                    converted = AssetDatabase.LoadAssetAtPath<Material>(output);
                    if (converted == null)
                    {
                        Color color = source.HasProperty("_Color") ? source.color : Color.white;
                        Texture texture = source.HasProperty("_MainTex") ? source.mainTexture : null;
                        converted = new Material(lit) { name = source.name + " URP", color = color };
                        if (texture != null) converted.SetTexture("_BaseMap", texture);
                        if (converted.HasProperty("_Smoothness")) converted.SetFloat("_Smoothness", .22f);
                        AssetDatabase.CreateAsset(converted, output);
                    }
                    ConvertedMaterials[key] = converted;
                }
                materials[i] = converted;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void Ground(Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Walkable Ground"; ground.transform.SetParent(parent);
        ground.transform.position = position; ground.transform.localScale = scale;
        Material material = GetColorMaterial("World_Ground_" + ColorUtility.ToHtmlStringRGB(color), color);
        ground.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreateLandmark(string name, Vector3 position, Vector3 scale, Transform parent, Color color)
    {
        GameObject landmark = GameObject.CreatePrimitive(PrimitiveType.Cylinder); landmark.name = name; landmark.transform.SetParent(parent);
        landmark.transform.position = position + Vector3.up * scale.y; landmark.transform.localScale = scale;
        landmark.GetComponent<Renderer>().sharedMaterial = GetColorMaterial(name.Replace(" ", "_"), color);
    }

    private static Material GetColorMaterial(string name, Color color)
    {
        string path = Generated + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261u;
        for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619u;
        return hash;
    }

    private static void Save(Scene scene, string file)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, Output + "/" + file + ".unity");
    }

    private static void AddScenesToBuild()
    {
        string[] newPaths = { Output + "/LangGiongTienTuyen.unity", Output + "/KinhThanhRenThep.unity", Output + "/PhaoDaiNgamQuanAn.unity", Output + "/ThungLungVuotSong.unity", Output + "/TranTuyenNuiSoc.unity", Output + "/DinhSocHoaThanh.unity" };
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        foreach (string path in newPaths)
            if (!scenes.Any(item => item.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
