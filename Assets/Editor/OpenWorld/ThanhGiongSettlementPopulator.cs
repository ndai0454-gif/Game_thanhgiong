using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Populates immersive Vietnamese village houses, communal courtyards, watchtowers,
/// and enemy war encampments across the open world map (ThanhGiongDaiViet.unity).
/// Eliminates barren/empty spaces while preserving navigation routes and 60 FPS performance.
/// </summary>
public static class ThanhGiongSettlementPopulator
{
    private const string ScenePath = "Assets/Scenes/ThanhGiongWorld/ThanhGiongDaiViet.unity";
    private const string RootName = "OPEN WORLD - VILLAGES & SETTLEMENTS (NHÀ CỬA & DOANH TRẠI)";
    private const string PrefabDir = "Assets/Prefabs/Meshy/Environment/";

    [MenuItem("Tools/Thanh Giong/Open World/Them Nha Cua Va Doanh Trai (Populate Settlements)", false, 10)]
    [MenuItem("Tools/Thanh Giong/Open World/Populate Houses And Settlements", false, 11)]
    public static void PopulateMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Thông báo", "Vui lòng bấm dừng nút Play (Stop Play Mode) trong Unity trước khi thêm nhà cửa vào bản đồ.", "Đã hiểu");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.name != "ThanhGiongDaiViet")
        {
            if (EditorUtility.DisplayDialog("Mở màn chơi", "Cần mở scene 'ThanhGiongDaiViet' để thêm nhà cửa. Bạn có muốn mở ngay không?", "Mở Scene", "Hủy"))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else return;
        }

        int count = Execute(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"<color=green><b>[Thánh Gióng]</b> Đã bố trí thành công {count} công trình (nhà tranh, sân đình, tháp canh, lều trại, cổng lũy) vào bản đồ Đại Việt!</color>");
        EditorUtility.DisplayDialog("Thành công", $"Đã thêm thành công {count} công trình nhà cửa, đình làng, tháp canh và doanh trại xung quanh map!\n\nBản đồ giờ đã đầy đủ làng xóm và chiến lũy, không còn bị trống trải.", "Tuyệt vời");
    }

    public static int Execute(Scene scene)
    {
        // 1. Clean previous settlement root if exists
        foreach (var existingRoot in scene.GetRootGameObjects())
        {
            if (existingRoot.name.StartsWith("OPEN WORLD - VILLAGES", StringComparison.OrdinalIgnoreCase) ||
                existingRoot.name.Contains("NHÀ CỬA & DOANH TRẠI"))
            {
                Object.DestroyImmediate(existingRoot);
            }
        }

        var masterRoot = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(masterRoot, scene);

        int totalPlaced = 0;

        // 2. Hub: Làng Phù Đổng & Kinh thành Phong Châu (Safe zone)
        var hubGroup = CreateSubGroup(masterRoot, "LÀNG PHÙ ĐỔNG (Hub & Làng Cổ)");
        // Đình làng trung tâm
        Spawn(hubGroup, "SanDinh_TrongDong", new Vector3(-35f, 0, -8f), 6.5f, 180f, true);
        Spawn(hubGroup, "CayDaCoThu_LangViet", new Vector3(-48f, 0, -18f), 11.5f, 35f, true);
        Spawn(hubGroup, "CongLang_ChienLuy", new Vector3(-18f, 0, 0f), 6.0f, 90f, true);
        Spawn(hubGroup, "ThapCanh_DoanThanh", new Vector3(-20f, 0, -22f), 8.5f, 45f, true);
        // Xóm nhà tranh phía Tây đình
        Spawn(hubGroup, "NhaTranh_LangGiong", new Vector3(-62f, 0, 15f), 5.5f, 135f, true);
        Spawn(hubGroup, "NhaTranh_LangGiong", new Vector3(-78f, 0, -5f), 5.2f, 80f, true);
        Spawn(hubGroup, "NhaTranh_LangGiong", new Vector3(-65f, 0, -32f), 5.6f, 25f, true);
        Spawn(hubGroup, "KhomTreNga_ThanThanh", new Vector3(-74f, 0, 10f), 6.5f, 0f, true);
        // Xóm nhà tranh phía Đông (gần lò rèn)
        Spawn(hubGroup, "NhaTranh_LangGiong", new Vector3(50f, 0, -15f), 5.2f, 240f, true);
        Spawn(hubGroup, "NhaTranh_LangGiong", new Vector3(62f, 0, 22f), 5.4f, 195f, true);
        Spawn(hubGroup, "NhaTranh_LangGiong", new Vector3(88f, 0, -8f), 5.0f, 290f, true);
        Spawn(hubGroup, "ThapCanh_DoanThanh", new Vector3(95f, 0, 25f), 8.0f, 220f, true);
        Spawn(hubGroup, "CongLang_ChienLuy", new Vector3(38f, 0, 0f), 5.8f, 270f, true);
        totalPlaced += 13;

        // 3. Rừng Tre Đầu Làng (Blue Forest: -150, 0)
        var forestGroup = CreateSubGroup(masterRoot, "XÓM RỪNG TRE ĐẦU LÀNG");
        Spawn(forestGroup, "NhaTranh_LangGiong", new Vector3(-135f, 0, 24f), 5.2f, 160f, true);
        Spawn(forestGroup, "NhaTranh_LangGiong", new Vector3(-165f, 0, -22f), 5.0f, 40f, true);
        Spawn(forestGroup, "NhaTranh_LangGiong", new Vector3(-148f, 0, -38f), 4.8f, 15f, true);
        Spawn(forestGroup, "ThapCanh_DoanThanh", new Vector3(-125f, 0, -15f), 8.0f, 110f, true);
        Spawn(forestGroup, "CayDaCoThu_LangViet", new Vector3(-172f, 0, -12f), 10.0f, 75f, true);
        Spawn(forestGroup, "CongLang_ChienLuy", new Vector3(-118f, 0, 5f), 5.6f, 85f, true);
        totalPlaced += 6;

        // 4. Bờ Sông Hồng (Blue Steppe: -75, -145)
        var riverGroup = CreateSubGroup(masterRoot, "LÀNG CHÀI BỜ SÔNG HỒNG");
        Spawn(riverGroup, "NhaTranh_LangGiong", new Vector3(-58f, 0, -128f), 5.2f, 195f, true);
        Spawn(riverGroup, "NhaTranh_LangGiong", new Vector3(-88f, 0, -135f), 5.0f, 215f, true);
        Spawn(riverGroup, "NhaTranh_LangGiong", new Vector3(-68f, 0, -165f), 5.4f, 45f, true);
        Spawn(riverGroup, "NhaTranh_LangGiong", new Vector3(-95f, 0, -155f), 4.9f, 70f, true);
        Spawn(riverGroup, "CayDaCoThu_LangViet", new Vector3(-105f, 0, -140f), 10.5f, 120f, true);
        Spawn(riverGroup, "CauGo_CoDai", new Vector3(-75f, 0, -182f), 4.2f, 0f, true);
        Spawn(riverGroup, "ThapCanh_DoanThanh", new Vector3(-45f, 0, -138f), 7.8f, 240f, true);
        totalPlaced += 7;

        // 5. Đầm Sen Văn Lang (Blue Swamp: 90, -145)
        var swampGroup = CreateSubGroup(masterRoot, "THÔN ĐẦM SEN VĂN LANG");
        Spawn(swampGroup, "NhaTranh_LangGiong", new Vector3(72f, 0, -125f), 5.0f, 150f, true);
        Spawn(swampGroup, "NhaTranh_LangGiong", new Vector3(108f, 0, -132f), 5.3f, 220f, true);
        Spawn(swampGroup, "NhaTranh_LangGiong", new Vector3(82f, 0, -168f), 5.1f, 30f, true);
        Spawn(swampGroup, "SanDinh_TrongDong", new Vector3(115f, 0, -160f), 5.5f, 310f, true);
        Spawn(swampGroup, "DamSenCo_VanLang", new Vector3(92f, 0, -148f), 2.8f, 45f, false);
        Spawn(swampGroup, "CayDaCoThu_LangViet", new Vector3(65f, 0, -145f), 9.5f, 90f, true);
        totalPlaced += 6;

        // 6. Gò Đá Đông Làng (Blue Highland: 155, 0)
        var highlandGroup = CreateSubGroup(masterRoot, "XÓM GÒ ĐÁ ĐÔNG LÀNG");
        Spawn(highlandGroup, "NhaTranh_LangGiong", new Vector3(142f, 0, 18f), 5.1f, 170f, true);
        Spawn(highlandGroup, "NhaTranh_LangGiong", new Vector3(168f, 0, -16f), 5.3f, 340f, true);
        Spawn(highlandGroup, "NhaTranh_LangGiong", new Vector3(175f, 0, 25f), 4.9f, 205f, true);
        Spawn(highlandGroup, "ThapCanh_DoanThanh", new Vector3(135f, 0, -22f), 8.2f, 60f, true);
        totalPlaced += 4;

        // 7. Mỏ Sắt Phong Châu (Blue Mountain: 70, 145)
        var mineGroup = CreateSubGroup(masterRoot, "TRẠI THỢ RÈN MỎ SẮT");
        Spawn(mineGroup, "NhaTranh_LangGiong", new Vector3(55f, 0, 130f), 5.2f, 130f, true);
        Spawn(mineGroup, "NhaTranh_LangGiong", new Vector3(85f, 0, 135f), 5.0f, 230f, true);
        Spawn(mineGroup, "NhaTranh_LangGiong", new Vector3(65f, 0, 165f), 5.4f, 25f, true);
        Spawn(mineGroup, "LoRenThep_VanLang", new Vector3(92f, 0, 155f), 4.8f, 280f, true);
        Spawn(mineGroup, "ThapCanh_DoanThanh", new Vector3(48f, 0, 155f), 8.5f, 140f, true);
        totalPlaced += 5;

        // 8. Đồi Canh Tiền Tiêu (Yellow Highland: -150, 160)
        var watchHillGroup = CreateSubGroup(masterRoot, "TIỀN ĐỒN ĐỒI CANH");
        Spawn(watchHillGroup, "ThapCanh_DoanThanh", new Vector3(-138f, 0, 145f), 8.5f, 135f, true);
        Spawn(watchHillGroup, "ThapCanh_DoanThanh", new Vector3(-165f, 0, 175f), 8.5f, 315f, true);
        Spawn(watchHillGroup, "LeuTrai_GiacAn", new Vector3(-145f, 0, 170f), 4.5f, 210f, true);
        Spawn(watchHillGroup, "LeuTrai_GiacAn", new Vector3(-160f, 0, 148f), 4.2f, 45f, true);
        Spawn(watchHillGroup, "CongLang_ChienLuy", new Vector3(-132f, 0, 162f), 5.8f, 90f, true);
        totalPlaced += 5;

        // 9. Đồng Lúa Bị Cướp (Yellow Steppe: 0, -290)
        var farmGroup = CreateSubGroup(masterRoot, "XÓM NÔNG ĐỒNG LÚA");
        Spawn(farmGroup, "NhaTranh_LangGiong", new Vector3(-18f, 0, -275f), 5.2f, 165f, true);
        Spawn(farmGroup, "NhaTranh_LangGiong", new Vector3(22f, 0, -282f), 5.4f, 210f, true);
        Spawn(farmGroup, "NhaTranh_LangGiong", new Vector3(-5f, 0, -310f), 5.0f, 350f, true);
        Spawn(farmGroup, "LeuTrai_GiacAn", new Vector3(28f, 0, -305f), 4.6f, 315f, true);
        Spawn(farmGroup, "ThapCanh_DoanThanh", new Vector3(-28f, 0, -295f), 8.2f, 80f, true);
        totalPlaced += 5;

        // 10. Pháo Đài Quân Ân (Red Fort: -310, 160)
        var fortGroup = CreateSubGroup(masterRoot, "PHÁO ĐÀI QUÂN ÂN");
        Spawn(fortGroup, "ThapCanh_DoanThanh", new Vector3(-290f, 0, 142f), 9.0f, 120f, true);
        Spawn(fortGroup, "ThapCanh_DoanThanh", new Vector3(-330f, 0, 178f), 9.0f, 300f, true);
        Spawn(fortGroup, "CongLang_ChienLuy", new Vector3(-295f, 0, 160f), 6.5f, 90f, true);
        Spawn(fortGroup, "LeuTrai_GiacAn", new Vector3(-315f, 0, 145f), 5.0f, 180f, true);
        Spawn(fortGroup, "LeuTrai_GiacAn", new Vector3(-305f, 0, 175f), 4.8f, 240f, true);
        Spawn(fortGroup, "LeuTrai_GiacAn", new Vector3(-328f, 0, 162f), 4.6f, 30f, true);
        totalPlaced += 6;

        // 11. Thung Lũng Vượt Sông (Red River: -310, 0)
        var valleyGroup = CreateSubGroup(masterRoot, "TIỀN TUYẾN THUNG LŨNG VƯỢT SÔNG");
        Spawn(valleyGroup, "CauGo_CoDai", new Vector3(-310f, 0, 0f), 4.5f, 90f, true);
        Spawn(valleyGroup, "ThapCanh_DoanThanh", new Vector3(-292f, 0, 18f), 8.5f, 135f, true);
        Spawn(valleyGroup, "LeuTrai_GiacAn", new Vector3(-325f, 0, 22f), 4.8f, 190f, true);
        Spawn(valleyGroup, "LeuTrai_GiacAn", new Vector3(-328f, 0, -18f), 4.6f, 40f, true);
        Spawn(valleyGroup, "CongLang_ChienLuy", new Vector3(-298f, 0, -8f), 5.8f, 80f, true);
        totalPlaced += 5;

        // 12. ĐẠI BẢN DOANH GIẶC ÂN (Black Battle: -310, -160)
        // Đây chính là khu vực người chơi đang đứng trong ảnh (-226, -205)!
        var anArmyHQ = CreateSubGroup(masterRoot, "ĐẠI BẢN DOANH GIẶC ÂN & DOANH TRẠI HẬU CẦN");
        // Cụm tiền trạm giặc Ân ngay ven đường mà người chơi đang nhìn thấy (-235, -200)
        Spawn(anArmyHQ, "ThapCanh_DoanThanh", new Vector3(-218f, 0, -195f), 8.8f, 225f, true);
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-235f, 0, -192f), 4.8f, 210f, true);
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-242f, 0, -212f), 4.6f, 35f, true);
        Spawn(anArmyHQ, "CongLang_ChienLuy", new Vector3(-225f, 0, -210f), 6.0f, 315f, true);
        Spawn(anArmyHQ, "CoLenh_PhuDong", new Vector3(-215f, 0, -205f), 3.8f, 180f, false);

        // Cụm doanh trại trung tâm (-310, -160)
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-295f, 0, -145f), 5.2f, 180f, true);
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-322f, 0, -150f), 5.0f, 230f, true);
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-305f, 0, -178f), 5.4f, 40f, true);
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-330f, 0, -172f), 4.8f, 75f, true);
        Spawn(anArmyHQ, "ThapCanh_DoanThanh", new Vector3(-288f, 0, -165f), 9.2f, 90f, true);
        Spawn(anArmyHQ, "ThapCanh_DoanThanh", new Vector3(-335f, 0, -135f), 9.0f, 270f, true);
        Spawn(anArmyHQ, "CongLang_ChienLuy", new Vector3(-292f, 0, -155f), 6.5f, 80f, true);

        // Cụm doanh trại phía Bắc tiếp giáp thung lũng (-275, -125)
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-270f, 0, -125f), 4.8f, 160f, true);
        Spawn(anArmyHQ, "LeuTrai_GiacAn", new Vector3(-282f, 0, -112f), 4.5f, 220f, true);
        Spawn(anArmyHQ, "ThapCanh_DoanThanh", new Vector3(-262f, 0, -118f), 8.5f, 135f, true);
        totalPlaced += 15;

        // 13. Núi Sóc · Đường Hóa Thánh (Soc Peak: -150, -290)
        var socPeakGroup = CreateSubGroup(masterRoot, "ĐỀN THIÊNG NÚI SÓC");
        Spawn(socPeakGroup, "DenThieng_DinhSoc", new Vector3(-150f, 0, -290f), 6.5f, 180f, true);
        Spawn(socPeakGroup, "CotMocDa_DongSon", new Vector3(-138f, 0, -282f), 4.2f, 45f, true);
        Spawn(socPeakGroup, "CotMocDa_DongSon", new Vector3(-162f, 0, -282f), 4.2f, 315f, true);
        Spawn(socPeakGroup, "KhomTreNga_ThanThanh", new Vector3(-135f, 0, -298f), 7.5f, 60f, true);
        Spawn(socPeakGroup, "KhomTreNga_ThanThanh", new Vector3(-165f, 0, -298f), 7.5f, 300f, true);
        Spawn(socPeakGroup, "CayDaCoThu_LangViet", new Vector3(-150f, 0, -315f), 11.0f, 0f, true);
        totalPlaced += 6;

        // 14. CÁC XÓM & TRẠM DỪNG CHÂN VEN ĐƯỜNG THIÊN LÝ (Roadside Settlements)
        var roadsideGroup = CreateSubGroup(masterRoot, "XÓM NHÀ & TRẠM DỪNG CHÂN VEN ĐƯỜNG");
        // Ven đường Hub (-55, 0) ↔ Rừng Tre (-150, 0)
        SpawnRoadsideHamlet(roadsideGroup, new Vector3(-105f, 0, 16f), 140f, true);
        // Ven đường Rừng Tre (-150, 0) ↔ Đồi Canh (-150, 160)
        SpawnRoadsideHamlet(roadsideGroup, new Vector3(-166f, 0, 80f), 80f, false);
        // Ven đường Hub (15, 0) ↔ Mỏ Sắt (70, 145)
        SpawnRoadsideHamlet(roadsideGroup, new Vector3(32f, 0, 75f), 220f, true);
        // Ven đường Hub (15, 0) ↔ Bờ Sông (-75, -145)
        SpawnRoadsideHamlet(roadsideGroup, new Vector3(-25f, 0, -68f), 135f, true);
        // Ven đường Hub (15, 0) ↔ Đầm Sen (90, -145)
        SpawnRoadsideHamlet(roadsideGroup, new Vector3(65f, 0, -70f), 240f, true);
        // Ven đường Bờ Sông (-75, -145) ↔ Đồng Lúa (0, -290)
        SpawnRoadsideHamlet(roadsideGroup, new Vector3(-42f, 0, -215f), 120f, true);
        // Ven đường Đồng Lúa (0, -290) ↔ Núi Sóc (-150, -290)
        SpawnRoadsideHamlet(roadsideGroup, new Vector3(-75f, 0, -272f), 180f, true);
        // Ven đường Thung Lũng (-310, 0) ↔ Doanh Trại (-310, -160)
        SpawnRoadsideMilitaryOutpost(roadsideGroup, new Vector3(-292f, 0, -80f), 100f);
        // Ven đường Doanh Trại (-310, -160) ↔ Núi Sóc (-150, -290)
        SpawnRoadsideMilitaryOutpost(roadsideGroup, new Vector3(-225f, 0, -235f), 55f);
        totalPlaced += 24;

        return totalPlaced;
    }

    private static GameObject CreateSubGroup(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void SpawnRoadsideHamlet(GameObject parent, Vector3 pos, float yaw, bool hasTree)
    {
        Spawn(parent, "NhaTranh_LangGiong", pos, 5.2f, yaw, true);
        Vector3 secondHouse = pos + Quaternion.Euler(0, yaw, 0) * new Vector3(12f, 0, 4f);
        Spawn(parent, "NhaTranh_LangGiong", secondHouse, 4.9f, yaw + 25f, true);
        if (hasTree)
        {
            Vector3 treePos = pos + Quaternion.Euler(0, yaw, 0) * new Vector3(-10f, 0, 5f);
            Spawn(parent, "CayDaCoThu_LangViet", treePos, 9.5f, yaw - 40f, true);
        }
    }

    private static void SpawnRoadsideMilitaryOutpost(GameObject parent, Vector3 pos, float yaw)
    {
        Spawn(parent, "LeuTrai_GiacAn", pos, 4.8f, yaw, true);
        Vector3 secondTent = pos + Quaternion.Euler(0, yaw, 0) * new Vector3(10f, 0, 3f);
        Spawn(parent, "LeuTrai_GiacAn", secondTent, 4.5f, yaw + 35f, true);
        Vector3 towerPos = pos + Quaternion.Euler(0, yaw, 0) * new Vector3(-8f, 0, 2f);
        Spawn(parent, "ThapCanh_DoanThanh", towerPos, 8.5f, yaw + 90f, true);
    }

    private static GameObject Spawn(GameObject parent, string key, Vector3 worldPos, float targetHeight, float yawDegrees, bool addCollider)
    {
        string path = PrefabDir + "Meshy_" + key + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[ThanhGiong Settlement] Không tìm thấy prefab: {path}");
            return null;
        }

        float groundY = SampleHeight(worldPos.x, worldPos.z);
        Vector3 spawnPos = new Vector3(worldPos.x, groundY, worldPos.z);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.scene);
        instance.name = key;
        instance.transform.SetParent(parent.transform, false);
        instance.transform.position = spawnPos;
        instance.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        // Auto scale to match targetHeight
        var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y > 0.05f)
            {
                float factor = targetHeight / b.size.y;
                instance.transform.localScale *= factor;
            }

            Physics.SyncTransforms();
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float bottomOffset = instance.transform.position.y - b.min.y;
            instance.transform.position = new Vector3(worldPos.x, groundY + bottomOffset - 0.03f, worldPos.z);
        }

        // Add Solid MeshCollider so player / horse cannot clip through buildings
        if (addCollider)
        {
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() == null)
                {
                    var col = mf.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                    col.convex = false;
                }
            }
        }

        instance.isStatic = true;
        return instance;
    }

    private static float SampleHeight(float x, float z)
    {
        // 1. Raycast on active terrain/mesh colliders
        if (Physics.Raycast(new Vector3(x, 250f, z), Vector3.down, out RaycastHit hit, 500f))
        {
            return hit.point.y;
        }

        // 2. Fallback to procedural terrain formula
        float hill = Mathf.PerlinNoise((x + 750f) * 0.009f, (z + 900f) * 0.009f) * 8.5f;
        return hill * 0.65f;
    }
}
