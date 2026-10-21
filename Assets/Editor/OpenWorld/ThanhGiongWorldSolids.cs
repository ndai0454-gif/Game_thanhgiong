using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit, repeatable scene repair. Original FBXs and gameplay anchors stay intact.</summary>
public static class ThanhGiongWorldSolids
{
    private const string Marker = "WorldSolid · ";
    private static readonly string[] Buildings = {
        "NhaTranh_", "CongLang_", "ThapCanh_", "LoRenThep_", "DenThieng_", "LeuTrai_", "SanDinh_"
    };

    [MenuItem("Tools/Thanh Giong/Open World/Enlarge Buildings And Repair Collision")]
    public static void RepairActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "ThanhGiongDaiViet") throw new InvalidOperationException("Open ThanhGiongDaiViet first.");
        Debug.Log(Repair(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static string Repair(Scene scene)
    {
        int buildings = 0, surfaces = 0, trunks = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!path.StartsWith("Assets/Art/Meshy/Environment/", StringComparison.Ordinal)) continue;
            bool building = Array.Exists(Buildings, token => path.Contains(token));
            bool rock = path.Contains("DaNui_") || path.Contains("MoQuangSat_") || path.Contains("CotMocDa_");
            bool tree = path.Contains("CayDaCoThu_");
            if (!building && !rock && !tree) continue;
            if (filter.transform.Find(Marker + "collision") != null) continue;

            // Scale the model branch, not quest stations, lights, triggers or parent layout.
            if (building)
            {
                var model = filter.transform.parent;
                if (model != null && model.name == "MeshyModel") model.localScale *= 1.4f;
                else filter.transform.localScale *= 1.4f;
                buildings++;
            }

            var solid = new GameObject(Marker + "collision");
            solid.layer = 0;
            solid.transform.SetParent(filter.transform, false);
            if (tree)
            {
                // Block only the trunk; a canopy-sized box prevents walking under trees.
                var b = mesh.bounds;
                var capsule = solid.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = Mathf.Min(b.size.x, b.size.z) * .075f;
                capsule.height = b.size.y * .55f;
                capsule.center = new Vector3(b.center.x, b.min.y + capsule.height * .5f, b.center.z);
                trunks++;
            }
            else
            {
                // Non-convex surface preserves gate openings, roofs and bridge geometry.
                // Shared source meshes allow PhysX to reuse cooked collision data.
                var collider = solid.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = false;
                surfaces++;
            }
        }
        Physics.SyncTransforms();
        return $"WORLD_SOLIDS buildings enlarged={buildings}, mesh surfaces={surfaces}, tree trunks={trunks}";
    }
}
