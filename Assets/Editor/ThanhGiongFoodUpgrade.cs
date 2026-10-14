using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThanhGiongFoodUpgrade
{
    private const string MatDir = "Assets/Materials/Food";
    private const string SackPath = "Assets/Plugins/KayKit_Medieval_Hexagon_Pack_1.0_FREE/KayKit_Medieval_Hexagon_Pack_1.0_FREE/Assets/fbx(unity)/decoration/props/sack.fbx";
    private const string PlatePath = "Assets/Plugins/KayKit_Dungeon_Pack_1.1_FREE/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)/plate.fbx";
    private const string CratePath = "Assets/Plugins/KayKit_Medieval_Hexagon_Pack_1.0_FREE/KayKit_Medieval_Hexagon_Pack_1.0_FREE/Assets/fbx(unity)/decoration/props/crate_open.fbx";
    private const string LumberPath = "Assets/Plugins/KayKit_Medieval_Hexagon_Pack_1.0_FREE/KayKit_Medieval_Hexagon_Pack_1.0_FREE/Assets/fbx(unity)/decoration/props/resource_lumber.fbx";
    private const string MeatPath = "Assets/Plugins/KayKit_Dungeon_Pack_1.1_FREE/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)/plate_food_A.fbx";
    private const string StoolPath = "Assets/Plugins/KayKit_Dungeon_Pack_1.1_FREE/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)/stool.fbx";

    [MenuItem("ThanhGiong/Upgrade Village Food Assets")]
    public static void UpgradeVillageFood()
    {
        EnsureMaterials(out Material matRice, out Material matCaPhao, out Material matStem);

        GameObject sackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SackPath);
        GameObject platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatePath);
        GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePath);
        GameObject lumberPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LumberPath);
        GameObject meatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeatPath);
        GameObject stoolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StoolPath);

        ThanhGiongCollectible[] allItems = Object.FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include);
        int upgradedCount = 0;

        foreach (var item in allItems)
        {
            if (item.kind > ThanhGiongCollectible.Kind.Meat) continue;

            // Clean legacy primitive components
            var mf = item.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);
            var mr = item.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
            var col = item.GetComponent<BoxCollider>();
            if (col != null) Object.DestroyImmediate(col);

            // Clean previous children
            while (item.transform.childCount > 0)
            {
                Object.DestroyImmediate(item.transform.GetChild(0).gameObject);
            }

            // Create Visual Root
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(item.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Color auraColor = Color.yellow;
            string labelText = "";
            string objName = "";

            switch (item.kind)
            {
                case ThanhGiongCollectible.Kind.Rice:
                    objName = "Lương thực - Cơm Gạo (7 Nong Cơm)";
                    labelText = "7 NONG CƠM GẠO  +35";
                    auraColor = new Color(1.0f, 0.88f, 0.25f);
                    item.displayName = "7 Nong Cơm Gạo";
                    BuildRiceAssembly(visual.transform, sackPrefab, platePrefab, matRice);
                    break;

                case ThanhGiongCollectible.Kind.Eggplant:
                    objName = "Lương thực - Cà Pháo (3 Nong Cà)";
                    labelText = "3 NONG CÀ PHÁO  +35";
                    auraColor = new Color(0.35f, 0.95f, 0.55f);
                    item.displayName = "3 Nong Cà Pháo";
                    BuildEggplantAssembly(visual.transform, cratePrefab, matCaPhao, matStem);
                    break;

                case ThanhGiongCollectible.Kind.Firewood:
                    objName = "Lương thực - Bó Củi Nhóm Lửa";
                    labelText = "BÓ CỦI NHÓM LỬA  +35";
                    auraColor = new Color(1.0f, 0.62f, 0.15f);
                    item.displayName = "Bó Củi Nhóm Lửa";
                    BuildFirewoodAssembly(visual.transform, lumberPrefab);
                    break;

                case ThanhGiongCollectible.Kind.Meat:
                    objName = "Lương thực - Mâm Thịt Khao Quân";
                    labelText = "MÂM THỊT KHAO QUÂN  +35";
                    auraColor = new Color(1.0f, 0.32f, 0.25f);
                    item.displayName = "Mâm Thịt Khao Quân";
                    BuildMeatAssembly(visual.transform, stoolPrefab, meatPrefab);
                    break;
            }

            item.name = objName;
            item.foodValue = 35f;
            item.spinSpeed = 32f;
            item.visualRoot = visual.transform;

            // Trigger SphereCollider for smooth, satisfying collection
            SphereCollider sphereCol = item.GetComponent<SphereCollider>();
            if (sphereCol == null) sphereCol = item.gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 1.6f;
            sphereCol.center = new Vector3(0f, 0.45f, 0f);

            // Point Light for atmospheric glow
            GameObject lightGo = new GameObject("AuraLight");
            lightGo.transform.SetParent(item.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            Light pLight = lightGo.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = auraColor;
            pLight.range = 4.2f;
            pLight.intensity = 2.4f;
            pLight.shadows = LightShadows.None;

            // 3D Billboard Text Label
            GameObject labelGo = new GameObject("BillboardLabel");
            labelGo.transform.SetParent(item.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            TextMesh tm = labelGo.AddComponent<TextMesh>();
            tm.text = labelText;
            tm.fontSize = 28;
            tm.characterSize = 0.075f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = auraColor;

            item.labelText = tm;
            item.gameObject.SetActive(true);
            upgradedCount++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"ThanhGiong: Upgraded {upgradedCount} village food items using actual project 3D assets!");
    }

    private static void BuildRiceAssembly(Transform parent, GameObject sackPrefab, GameObject platePrefab, Material matRice)
    {
        // Sack 1
        if (sackPrefab != null)
        {
            GameObject s1 = (GameObject)PrefabUtility.InstantiatePrefab(sackPrefab, parent);
            s1.name = "Rice_Sack_Main";
            s1.transform.localPosition = new Vector3(-0.15f, 0f, 0f);
            s1.transform.localRotation = Quaternion.Euler(0f, 22f, 10f);
            s1.transform.localScale = Vector3.one * 7.8f;

            GameObject s2 = (GameObject)PrefabUtility.InstantiatePrefab(sackPrefab, parent);
            s2.name = "Rice_Sack_Side";
            s2.transform.localPosition = new Vector3(0.20f, 0f, -0.06f);
            s2.transform.localRotation = Quaternion.Euler(4f, -38f, -8f);
            s2.transform.localScale = Vector3.one * 7.2f;
        }

        // Steaming winnowing plate of rice
        if (platePrefab != null)
        {
            GameObject plate = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab, parent);
            plate.name = "Rice_Plate";
            plate.transform.localPosition = new Vector3(0.08f, 0.04f, 0.32f);
            plate.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            plate.transform.localScale = Vector3.one * 0.92f;
        }

        // Fluffy white rice mound
        GameObject riceMound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        riceMound.name = "Cooked_Rice_Mound";
        riceMound.transform.SetParent(parent, false);
        riceMound.transform.localPosition = new Vector3(0.08f, 0.14f, 0.32f);
        riceMound.transform.localScale = new Vector3(0.72f, 0.36f, 0.72f);
        var c = riceMound.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        var mr = riceMound.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = matRice;
    }

    private static void BuildEggplantAssembly(Transform parent, GameObject cratePrefab, Material matCaPhao, Material matStem)
    {
        // Wooden Crate / Winnowing Basket
        if (cratePrefab != null)
        {
            GameObject crate = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, parent);
            crate.name = "CaPhao_Crate";
            crate.transform.localPosition = Vector3.zero;
            crate.transform.localRotation = Quaternion.identity;
            crate.transform.localScale = new Vector3(2.9f, 2.2f, 2.2f);
        }

        // Mound of 8 glistening round cà pháo (pickled eggplants)
        Vector3[] offsets = {
            new Vector3(0f, 0.22f, 0f),
            new Vector3(0.18f, 0.22f, 0.12f),
            new Vector3(-0.18f, 0.22f, -0.10f),
            new Vector3(0.16f, 0.22f, -0.12f),
            new Vector3(-0.16f, 0.22f, 0.14f),
            new Vector3(0f, 0.34f, 0.05f),
            new Vector3(-0.10f, 0.32f, -0.06f),
            new Vector3(0.12f, 0.32f, 0.02f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            egg.name = $"CaPhao_{i + 1}";
            egg.transform.SetParent(parent, false);
            egg.transform.localPosition = offsets[i];
            egg.transform.localScale = Vector3.one * 0.24f;
            var c = egg.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            var mr = egg.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = matCaPhao;

            // Green stem cap
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(egg.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            stem.transform.localScale = new Vector3(0.38f, 0.14f, 0.38f);
            var sc = stem.GetComponent<Collider>();
            if (sc != null) Object.DestroyImmediate(sc);
            var smr = stem.GetComponent<MeshRenderer>();
            if (smr != null) smr.sharedMaterial = matStem;
        }
    }

    private static void BuildFirewoodAssembly(Transform parent, GameObject lumberPrefab)
    {
        if (lumberPrefab == null) return;

        // Bundle 1 (Ground)
        GameObject l1 = (GameObject)PrefabUtility.InstantiatePrefab(lumberPrefab, parent);
        l1.name = "Firewood_Bundle_Base";
        l1.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        l1.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        l1.transform.localScale = Vector3.one * 1.65f;

        // Bundle 2 (Crossed on top)
        GameObject l2 = (GameObject)PrefabUtility.InstantiatePrefab(lumberPrefab, parent);
        l2.name = "Firewood_Bundle_Top";
        l2.transform.localPosition = new Vector3(-0.06f, 0.38f, 0.04f);
        l2.transform.localRotation = Quaternion.Euler(5f, -28f, 3f);
        l2.transform.localScale = Vector3.one * 1.45f;
    }

    private static void BuildMeatAssembly(Transform parent, GameObject stoolPrefab, GameObject meatPrefab)
    {
        // Rustic wooden pedestal stand
        if (stoolPrefab != null)
        {
            GameObject stool = (GameObject)PrefabUtility.InstantiatePrefab(stoolPrefab, parent);
            stool.name = "Meat_Table_Stool";
            stool.transform.localPosition = Vector3.zero;
            stool.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            stool.transform.localScale = new Vector3(0.85f, 0.65f, 0.85f);
        }

        // Loaded banquet feast platter
        if (meatPrefab != null)
        {
            GameObject platter = (GameObject)PrefabUtility.InstantiatePrefab(meatPrefab, parent);
            platter.name = "Meat_Feast_Platter";
            platter.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            platter.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
            platter.transform.localScale = Vector3.one * 1.15f;
        }
    }

    private static void EnsureMaterials(out Material matRice, out Material matCaPhao, out Material matStem)
    {
        if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");

        matRice = LoadOrCreateMaterial(Path.Combine(MatDir, "Rice_White.mat"), lit, new Color(0.98f, 0.98f, 0.96f), 0.25f);
        matCaPhao = LoadOrCreateMaterial(Path.Combine(MatDir, "Eggplant_CaPhao.mat"), lit, new Color(0.96f, 0.95f, 0.88f), 0.85f);
        matStem = LoadOrCreateMaterial(Path.Combine(MatDir, "Eggplant_Stem.mat"), lit, new Color(0.28f, 0.52f, 0.18f), 0.4f);
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader, Color color, float smoothness)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }
}
