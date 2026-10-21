#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Tự dựng bộ xương đơn giản (hông, chân, tay, đầu) cho mesh dân làng tĩnh của Meshy,
/// gán trọng số theo vùng cơ thể, lưu prefab rigged và thay dân làng trong scene.</summary>
public static class ThanhGiongVillagerRigger
{
    const string Dir = "Assets/Art/Villagers";
    const string RigDir = "Assets/Art/Villagers/Rigged";
    const float SizeBoost = 1.3f;
    const int TargetTris = 20000;   // mỗi dân làng ~20k tam giác (bản gốc Meshy 220–300k)

    struct Def { public string model; public float height; public Def(string m, float h) { model = m; height = h; } }
    static readonly Def[] Defs =
    {
        new Def("Villager_Farmer_Tex", 1.70f), new Def("Villager_Woman", 1.60f), new Def("Villager_Elder", 1.60f),
        new Def("Villager_Child", 1.20f), new Def("Villager_Blacksmith", 1.75f), new Def("Villager_Fisherman", 1.70f),
    };
    // slot -> model (khớp 10 dân làng trong scene)
    static readonly string[] Slots =
    {
        "Villager_Farmer_Tex","Villager_Woman","Villager_Elder","Villager_Child","Villager_Blacksmith",
        "Villager_Fisherman","Villager_Woman","Villager_Child","Villager_Elder","Villager_Woman",
    };

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    [MenuItem("Thanh Giong/Rig Villagers")]
    public static void Run()
    {
        Directory.CreateDirectory(RigDir);
        var prefabs = new Dictionary<string, GameObject>();
        foreach (var d in Defs) { var p = BuildPrefab(d); if (p != null) prefabs[d.model] = p; }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();

        var root = GameObject.Find("DÂN LÀNG");
        if (root == null) { Debug.LogError("Không thấy DÂN LÀNG"); return; }
        int i = 0;
        foreach (Transform g in root.transform)
        {
            for (int c = g.childCount - 1; c >= 0; c--) Object.DestroyImmediate(g.GetChild(c).gameObject);
            if (i >= Slots.Length) break;
            GameObject prefab; if (!prefabs.TryGetValue(Slots[i], out prefab)) { i++; continue; }
            var v = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            v.transform.localPosition = Vector3.zero; v.transform.localRotation = Quaternion.identity; v.transform.localScale = Vector3.one;
            g.gameObject.SetActive(true);
            i++;
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[VillagerRigger] xong " + i + " dân làng");
    }

    // Giản lược bằng gom cụm đỉnh: cùng ô vị trí VÀ cùng ô UV mới gộp, nên không làm nhòe đường nối texture.
    static void Simplify(ref Vector3[] v, ref Vector3[] n, ref Vector2[] uv, ref int[] t, float H, int targetTris)
    {
        float lo = 0.0005f, hi = 0.25f; int[] best = null; int bestCount = 0;
        for (int iter = 0; iter < 16; iter++)
        {
            float cell = (lo + hi) * 0.5f;
            int count; var map = Cluster(v, uv, H, cell, out count);
            int tc = CountTris(t, map);
            if (tc > targetTris) lo = cell; else { hi = cell; best = map; bestCount = count; }
        }
        if (best == null) { int c; best = Cluster(v, uv, H, hi, out c); bestCount = c; }
        var nv = new Vector3[bestCount]; var nn = new Vector3[bestCount]; var nu = new Vector2[bestCount]; var cnt = new int[bestCount];
        for (int i = 0; i < v.Length; i++) { int k = best[i]; nv[k] += v[i]; nn[k] += n[i]; nu[k] += uv[i]; cnt[k]++; }
        for (int k = 0; k < bestCount; k++) { float inv = 1f / Mathf.Max(1, cnt[k]); nv[k] *= inv; nu[k] *= inv; nn[k] = nn[k].sqrMagnitude > 1e-8f ? nn[k].normalized : Vector3.up; }
        var nt = new List<int>(t.Length / 4);
        for (int i = 0; i + 2 < t.Length; i += 3)
        {
            int a = best[t[i]], b = best[t[i + 1]], c = best[t[i + 2]];
            if (a == b || b == c || a == c) continue; nt.Add(a); nt.Add(b); nt.Add(c);
        }
        v = nv; n = nn; uv = nu; t = nt.ToArray();
    }
    static int[] Cluster(Vector3[] v, Vector2[] uv, float H, float cell, out int count)
    {
        var dict = new Dictionary<long, int>(v.Length / 4); var map = new int[v.Length];
        float inv = 1f / cell, uvInv = H / cell * 0.5f; count = 0;
        for (int i = 0; i < v.Length; i++)
        {
            long x = Mathf.FloorToInt(v[i].x * inv) & 0x3FFF, y = Mathf.FloorToInt(v[i].y * inv) & 0x3FFF, z = Mathf.FloorToInt(v[i].z * inv) & 0x3FFF;
            long u = Mathf.FloorToInt(uv[i].x * uvInv) & 0x3FF, w = Mathf.FloorToInt(uv[i].y * uvInv) & 0x3FF;
            long key = x | (y << 14) | (z << 28) | (u << 42) | (w << 52);
            int idx; if (!dict.TryGetValue(key, out idx)) { idx = count++; dict[key] = idx; }
            map[i] = idx;
        }
        return map;
    }
    static int CountTris(int[] t, int[] map)
    {
        int c = 0;
        for (int i = 0; i + 2 < t.Length; i += 3) { int a = map[t[i]], b = map[t[i + 1]], d = map[t[i + 2]]; if (a != b && b != d && a != d) c++; }
        return c;
    }

    static GameObject BuildPrefab(Def d)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/" + d.model + ".glb");
        if (asset == null) { Debug.LogWarning("Thiếu " + d.model); return null; }
        var inst = (GameObject)Object.Instantiate(asset);
        var mf = inst.GetComponentInChildren<MeshFilter>(); var mr = inst.GetComponentInChildren<MeshRenderer>();
        if (mf == null || mr == null) { Object.DestroyImmediate(inst); return null; }
        Mesh src = mf.sharedMesh;
        Matrix4x4 rel = inst.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        Vector3[] sv = src.vertices; Vector3[] sn = src.normals;
        var verts = new Vector3[sv.Length]; var norms = new Vector3[sv.Length];
        Bounds b = new Bounds(rel.MultiplyPoint3x4(sv[0]), Vector3.zero);
        for (int i = 0; i < sv.Length; i++) { verts[i] = rel.MultiplyPoint3x4(sv[i]); b.Encapsulate(verts[i]); }
        float s = d.height * SizeBoost / b.size.y;
        Vector3 off = new Vector3(b.center.x, b.min.y, b.center.z);
        for (int i = 0; i < verts.Length; i++) { verts[i] = (verts[i] - off) * s; if (sn.Length == sv.Length) norms[i] = rel.MultiplyVector(sn[i]).normalized; }
        float H = d.height * SizeBoost;
        Vector2[] uvs = src.uv; int[] tris = src.triangles;
        if (uvs.Length != sv.Length) uvs = new Vector2[sv.Length];
        Simplify(ref verts, ref norms, ref uvs, ref tris, H, TargetTris);

        // vị trí bản lề (không gian gốc, chân ở y=0)
        Vector3 hips = new Vector3(0, 0.50f * H, 0);
        Vector3 hipL = new Vector3(-0.06f * H, 0.50f * H, 0), hipR = new Vector3(0.06f * H, 0.50f * H, 0);
        Vector3 chest = new Vector3(0, 0.72f * H, 0);
        Vector3 shL = new Vector3(-0.14f * H, 0.80f * H, 0), shR = new Vector3(0.14f * H, 0.80f * H, 0);
        Vector3 head = new Vector3(0, 0.86f * H, 0);

        var rootGo = new GameObject(d.model + "_Rigged");
        var meshGo = new GameObject("Mesh"); meshGo.transform.SetParent(rootGo.transform, false);
        Transform Bone(string n, Transform parent, Vector3 pos) { var t = new GameObject(n).transform; t.SetParent(parent, false); t.position = pos; return t; }
        // tạm thời đặt rootGo ở gốc để world == local
        var bHips = Bone("Hips", rootGo.transform, hips);
        var bLegL = Bone("LegL", bHips, hipL); var bLegR = Bone("LegR", bHips, hipR);
        var bChest = Bone("Chest", bHips, chest);
        var bArmL = Bone("ArmL", bChest, shL); var bArmR = Bone("ArmR", bChest, shR);
        var bHead = Bone("Head", bChest, head);
        var bones = new[] { bHips, bLegL, bLegR, bChest, bArmL, bArmR, bHead };

        var weights = new BoneWeight[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 p = verts[i]; float t = p.y / H; float ax = Mathf.Abs(p.x) / H;
            float wLeg = Smooth(0.52f, 0.42f, t);                       // dưới hông
            float wArm = Smooth(0.14f, 0.20f, ax) * Smooth(0.30f, 0.45f, t) * Smooth(0.90f, 0.82f, t); // tay (xa thân)
            wLeg *= 1f - wArm;
            float wHead = Smooth(0.82f, 0.88f, t) * (1f - wArm);
            float wChest = Mathf.Clamp01(Smooth(0.45f, 0.58f, t)) * (1f - wLeg) * (1f - wArm) * (1f - wHead);
            float wHips = Mathf.Max(0f, 1f - wLeg - wArm - wHead - wChest);
            bool left = p.x < 0f; float side = Smooth(-0.025f, 0.025f, p.x / H);   // 0 = trái, 1 = phải
            var w = new float[7];
            w[0] = wHips; w[1] = wLeg * (1f - side); w[2] = wLeg * side; w[3] = wChest;
            w[4] = left ? wArm : 0f; w[5] = left ? 0f : wArm; w[6] = wHead;
            // chọn 4 lớn nhất
            int[] idx = { 0, 1, 2, 3, 4, 5, 6 };
            System.Array.Sort(idx, (a, c) => w[c].CompareTo(w[a]));
            float sum = w[idx[0]] + w[idx[1]] + w[idx[2]] + w[idx[3]]; if (sum < 1e-5f) { w[0] = 1f; idx[0] = 0; sum = 1f; }
            var bw = new BoneWeight();
            bw.boneIndex0 = idx[0]; bw.weight0 = w[idx[0]] / sum; bw.boneIndex1 = idx[1]; bw.weight1 = w[idx[1]] / sum;
            bw.boneIndex2 = idx[2]; bw.weight2 = w[idx[2]] / sum; bw.boneIndex3 = idx[3]; bw.weight3 = w[idx[3]] / sum;
            weights[i] = bw;
        }

        Mesh m = new Mesh();
        m.name = d.model + "_Rigged";
        if (verts.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts; m.uv = uvs; m.triangles = tris;
        if (sn.Length == sv.Length) m.normals = norms; else m.RecalculateNormals();
        m.RecalculateBounds();
        m.boneWeights = weights;
        var bind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * rootGo.transform.localToWorldMatrix;
        m.bindposes = bind;
        AssetDatabase.CreateAsset(m, RigDir + "/" + d.model + "_Rigged.asset");

        var smr = meshGo.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = m; smr.sharedMaterials = mr.sharedMaterials; smr.bones = bones; smr.rootBone = bHips;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smr.updateWhenOffscreen = false;
        smr.localBounds = new Bounds(new Vector3(0, H * 0.5f, 0), new Vector3(H, H * 1.1f, H));

        string path = RigDir + "/" + d.model + "_Rigged.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, path);
        Object.DestroyImmediate(rootGo); Object.DestroyImmediate(inst);
        return prefab;
    }
}
#endif
