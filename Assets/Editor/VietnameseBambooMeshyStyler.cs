using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VietnameseBambooMeshyStyler
{
    public const string SourcePath = "Assets/Prefabs/Meshy/Environment/Meshy_KhomTreNga_ThanThanh.prefab";
    public const string TargetPath = "Assets/Prefabs/Environment/Foliage/BambooGrove_Vietnamese_Legend.prefab";

    [MenuItem("Tools/Thánh Gióng/Environment/Restyle Vietnamese Bamboo with Meshy")]
    public static void Restyle()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (source == null) throw new InvalidOperationException("Missing Meshy bamboo source: " + SourcePath);

        var root = new GameObject("BambooGrove_Vietnamese_Legend");
        try
        {
            Vector3[] positions =
            {
                new Vector3(-1.05f, 0f, .20f),
                new Vector3(1.00f, 0f, -.28f),
                new Vector3(.05f, 0f, 1.18f)
            };
            float[] scales = { .66f, .59f, .52f };
            float[] rotations = { -18f, 106f, 224f };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject cluster = (GameObject)PrefabUtility.InstantiatePrefab(source);
                cluster.name = "Meshy Bamboo Cluster " + (i + 1);
                cluster.transform.SetParent(root.transform, false);
                cluster.transform.localPosition = positions[i];
                cluster.transform.localRotation = Quaternion.Euler(0f, rotations[i], 0f);
                cluster.transform.localScale = Vector3.one * scales[i];

                // One strong aura reads clearly; secondary clusters keep only their authored mesh.
                if (i > 0)
                    foreach (ParticleSystem ps in cluster.GetComponentsInChildren<ParticleSystem>(true))
                        ps.gameObject.SetActive(false);
            }

            var motion = root.AddComponent<VietnameseBambooMotion>();
            motion.windFrequency = 1.12f;
            motion.swayDegrees = 2.2f;
            motion.gustDegrees = 1.25f;
            motion.pullResponse = 6.5f;

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (ThanhGiongCollectible collectible in root.GetComponentsInChildren<ThanhGiongCollectible>(true))
                UnityEngine.Object.DestroyImmediate(collectible);

            PrefabUtility.SaveAsPrefabAsset(root, TargetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("VIETNAMESE_BAMBOO_MESHY PASS clusters=3 motionNodes=" +
                AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath).GetComponentsInChildren<Transform>(true).Count(x => x.name == "MeshyModel"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
