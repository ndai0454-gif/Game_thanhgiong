using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    // Calibrated against the normalized Meshy mesh, not a symmetric placeholder horse.
    public static class HorseSkinRepair
    {
        static readonly string[] Prefix = { "fl", "fr", "rl", "rr" };
        static readonly Vector3[] RawHip = { new Vector3(-.16f,.88f,.63f), new Vector3(.16f,1.02f,.68f), new Vector3(-.21f,1.03f,-.43f), new Vector3(.20f,1.03f,-.56f) };
        static readonly Vector3[] RawKnee = { new Vector3(-.16f,.57f,.80f), new Vector3(.16f,.49f,.76f), new Vector3(-.21f,.48f,-.39f), new Vector3(.20f,.48f,-.61f) };
        static readonly Vector3[] RawFoot = { new Vector3(-.165f,.310f,.715f), new Vector3(.157f,.03f,.798f), new Vector3(-.217f,.102f,-.230f), new Vector3(.187f,.077f,-.638f) };

        static readonly Vector3[] Hip = { new Vector3(-.160f,1.020f,.660f), new Vector3(.160f,1.020f,.680f), new Vector3(-.210f,1.030f,-.430f), new Vector3(.200f,1.030f,-.560f) };
        static readonly Vector3[] Knee = { new Vector3(-.160f,.485f,.700f), new Vector3(.160f,.490f,.720f), new Vector3(-.210f,.482f,-.496f), new Vector3(.200f,.478f,-.572f) };
        static readonly Vector3[] Foot = { new Vector3(-.170f,.040f,.750f), new Vector3(.157f,.030f,.765f), new Vector3(-.217f,.040f,-.500f), new Vector3(.187f,.040f,-.610f) };

        static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(low,high,value));
        static Vector3 Center(Vector3[] hip, Vector3[] knee, Vector3[] foot, int leg, float y) => y > knee[leg].y
            ? Vector3.Lerp(knee[leg], hip[leg], Mathf.InverseLerp(knee[leg].y,hip[leg].y,y))
            : Vector3.Lerp(foot[leg], knee[leg], Mathf.InverseLerp(foot[leg].y,knee[leg].y,y));

        static bool TryLoadRawHorseGeometry(int expectedVertexCount, out Vector3[] rawVerts, out Vector3[] rawNorms)
        {
            rawVerts = null; rawNorms = null;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Meshy/IronHorse/IronHorse_Meshy.fbx")
                   ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Meshy/IronHorse_Meshy.glb");
            if (src == null) return false;
            var tempRoot = new GameObject("TempHorseRawLoader");
            try
            {
                var temp = UnityEngine.Object.Instantiate(src, tempRoot.transform);
                var mf = temp.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount != expectedVertexCount) return false;
                Matrix4x4 m = tempRoot.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                rawVerts = mf.sharedMesh.vertices;
                rawNorms = mf.sharedMesh.normals;
                for (int i = 0; i < rawVerts.Length; i++)
                {
                    rawVerts[i] = (m.MultiplyPoint3x4(rawVerts[i]) + Vector3.up * 0.714621f) * 1.3f;
                    if (rawNorms != null && i < rawNorms.Length) rawNorms[i] = m.MultiplyVector(rawNorms[i]).normalized;
                }
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(tempRoot); }
        }

        public static void Repair(SkinnedMeshRenderer skin, bool rebuildWeights = true)
        {
            if (skin == null || skin.sharedMesh == null || skin.bones.Length != 17) return;
            var bones = skin.bones;
            Transform root = skin.transform.parent;
            Mesh mesh = skin.sharedMesh;

            if (rebuildWeights)
            {
                Vector3[] rawVerts, rawNorms;
                if (TryLoadRawHorseGeometry(mesh.vertexCount, out rawVerts, out rawNorms))
                {
                    mesh.vertices = rawVerts;
                    if (rawNorms != null && rawNorms.Length == rawVerts.Length) mesh.normals = rawNorms;
                }
                var checkVerts = mesh.vertices;
                float flMinY = checkVerts.Where(v => v.y < .5f && v.x < -.05f && v.z > .2f).Select(v => v.y).DefaultIfEmpty(0f).Min();
                if (flMinY > .11f)
                {
                    // Un-bend the raw Meshy stepping pose into a grounded, balanced 4-leg warhorse stance using pure rigid rotation + translation.
                    PlaceLegBones(bones, root, RawHip, RawKnee, RawFoot);
                    mesh.boneWeights = BuildHorseWeights(checkVerts, mesh.triangles, bones, RawHip, RawKnee, RawFoot);
                    mesh.bindposes = bones.Select(x => x.worldToLocalMatrix * skin.transform.localToWorldMatrix).ToArray();

                    var flU = bones.First(b => b.name == "fl_upper");
                    var flL = bones.First(b => b.name == "fl_lower");
                    var flH = bones.First(b => b.name == "fl_hoof");
                    flU.localRotation = Quaternion.Euler(22f, 0f, 0f);
                    flL.localRotation = Quaternion.Euler(-38f, 0f, 0f);
                    flL.position += root.TransformVector(new Vector3(0f, -.075f, .03f));
                    flH.localRotation = Quaternion.Euler(-45f, 0f, 0f);
                    flH.position += root.TransformVector(new Vector3(0f, -.030f, .055f));

                    var rlU = bones.First(b => b.name == "rl_upper");
                    var rlL = bones.First(b => b.name == "rl_lower");
                    var rlH = bones.First(b => b.name == "rl_hoof");
                    rlU.localRotation = Quaternion.Euler(11f, 0f, 0f);
                    rlL.localRotation = Quaternion.Euler(14f, 0f, 0f);
                    rlL.position += root.TransformVector(new Vector3(0f, -.032f, 0f));
                    rlH.localRotation = Quaternion.Euler(-25f, 0f, 0f);

                    var rrU = bones.First(b => b.name == "rr_upper");
                    var rrL = bones.First(b => b.name == "rr_lower");
                    rrU.localRotation = Quaternion.Euler(-4f, 0f, 0f);
                    rrL.localRotation = Quaternion.Euler(4f, 0f, 0f);
                    rrL.position += root.TransformVector(new Vector3(0f, -.022f, 0f));

                    var baked = new Mesh();
                    skin.BakeMesh(baked);
                    float ls = Mathf.Max(.0001f, skin.transform.lossyScale.x);
                    var bv = baked.vertices;
                    Vector3 pasternPivot = new Vector3(-.17f, .11f, .74f);
                    for (int i = 0; i < bv.Length; i++)
                    {
                        Vector3 v = bv[i] / ls;
                        if (v.x < -.05f && v.z > .50f && v.y < .16f)
                        {
                            float w = 1f - Smooth(.05f, .16f, v.y);
                            v = pasternPivot + Quaternion.Euler(-22f * w, 0f, 0f) * (v - pasternPivot);
                        }
                        bv[i] = v;
                    }
                    mesh.vertices = bv;
                    if (baked.normals != null && baked.normals.Length == bv.Length) mesh.normals = baked.normals;
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }

            // Only rotations drive gait. Joint translations/scales stay at the grounded bind pose.
            PlaceLegBones(bones, root, Hip, Knee, Foot);
            if (!rebuildWeights)
            {
                mesh.bindposes = bones.Select(x => x.worldToLocalMatrix * skin.transform.localToWorldMatrix).ToArray();
                EditorUtility.SetDirty(skin); return;
            }
            mesh.boneWeights = BuildHorseWeights(mesh.vertices, mesh.triangles, bones, Hip, Knee, Foot);
            mesh.bindposes = bones.Select(x => x.worldToLocalMatrix * skin.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); EditorUtility.SetDirty(skin);
        }

        static void PlaceLegBones(Transform[] bones, Transform root, Vector3[] hip, Vector3[] knee, Vector3[] foot)
        {
            for (int leg = 0; leg < 4; leg++)
            {
                string prefix = Prefix[leg];
                Transform upper = bones.First(x => x.name == prefix + "_upper");
                Transform lower = bones.First(x => x.name == prefix + "_lower");
                Transform hoof = bones.First(x => x.name == prefix + "_hoof");
                upper.localRotation = lower.localRotation = hoof.localRotation = Quaternion.identity;
                upper.localScale = lower.localScale = hoof.localScale = Vector3.one;
                upper.position = root.TransformPoint(hip[leg]);
                lower.position = root.TransformPoint(knee[leg]);
                hoof.position = root.TransformPoint(foot[leg]);
            }
        }

        static BoneWeight[] BuildHorseWeights(Vector3[] vertices, int[] triangles, Transform[] bones, Vector3[] hip, Vector3[] knee, Vector3[] foot)
        {
            var weights = new BoneWeight[vertices.Length];
            int body = Array.FindIndex(bones, x => x.name == "body");
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i]; int best = 0; float distance = float.MaxValue;
                for (int leg = 0; leg < 4; leg++)
                {
                    Vector3 center = Center(hip, knee, foot, leg, v.y);
                    float d = new Vector2(v.x - center.x, v.z - center.z).magnitude;
                    if (d < distance) { distance = d; best = leg; }
                }
                // Saddle cloth, chest armor, belly straps and tail stay out of leg influence.
                float radialLimit = v.y < .68f ? (1f - Smooth(.19f, .28f, distance)) : (1f - Smooth(.14f, .23f, distance));
                float legWeight = (1f - Smooth(.86f, 1.06f, v.y)) * radialLimit;
                if (legWeight > .001f && v.y < 1.06f && Mathf.Abs(v.x) > .055f && v.z > -.82f)
                {
                    int u = Array.FindIndex(bones, x => x.name == Prefix[best] + "_upper");
                    float upper = Smooth(knee[best].y - .05f, knee[best].y + .11f, v.y);
                    float hoof = (1f - Smooth(foot[best].y + .045f, foot[best].y + .14f, v.y)) * (1f - upper);
                    weights[i] = new BoneWeight { boneIndex0 = body, weight0 = 1f - legWeight, boneIndex1 = u, weight1 = upper * legWeight, boneIndex2 = u + 1, weight2 = (1f - upper - hoof) * legWeight, boneIndex3 = u + 2, weight3 = hoof * legWeight };
                }
                else if (v.z > .42f && v.y > 1.2f)
                {
                    float n = Smooth(.4f, .72f, v.z) * Smooth(1.18f, 1.4f, v.y), h = Smooth(1.48f, 1.73f, v.y);
                    weights[i] = new BoneWeight { boneIndex0 = body, weight0 = 1f - n, boneIndex1 = 1, weight1 = n * (1f - h), boneIndex2 = 2, weight2 = n * h };
                }
                else if (v.z < -.88f && v.y > .25f)
                {
                    float t = Smooth(.86f, 1.1f, -v.z);
                    weights[i] = new BoneWeight { boneIndex0 = body, weight0 = 1f - t, boneIndex1 = 3, weight1 = t * .65f, boneIndex2 = 4, weight2 = t * .35f };
                }
                else weights[i] = new BoneWeight { boneIndex0 = body, weight0 = 1f };
            }
            return SmoothSurfaceWeights(vertices, triangles, weights, body);
        }

        static BoneWeight[] SmoothSurfaceWeights(Vector3[] vertices, int[] triangles, BoneWeight[] initial, int bodyBone)
        {
            // FBX duplicates vertices at UV/normal seams. Weld their positions for
            // weight diffusion so an armor plate cannot tear along an import seam.
            var lookup = new System.Collections.Generic.Dictionary<Vector3Int, int>();
            var map = new int[vertices.Length]; var positions = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 10000f); int node;
                if (!lookup.TryGetValue(key, out node)) { node = positions.Count; lookup.Add(key, node); positions.Add(vertices[i]); }
                map[i] = node;
            }
            int count = positions.Count; var neighbors = new System.Collections.Generic.HashSet<int>[count];
            var current = new float[count, 17]; var next = new float[count, 17]; var samples = new int[count];
            for (int i = 0; i < count; i++) neighbors[i] = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                int n = map[i]; samples[n]++; var w = initial[i];
                current[n, w.boneIndex0] += w.weight0; current[n, w.boneIndex1] += w.weight1; current[n, w.boneIndex2] += w.weight2; current[n, w.boneIndex3] += w.weight3;
            }
            for (int n = 0; n < count; n++) for (int b = 0; b < 17; b++) current[n, b] /= samples[n];
            for (int i = 0; i < triangles.Length; i += 3) for (int e = 0; e < 3; e++)
            { int a = map[triangles[i + e]], b = map[triangles[i + (e + 1) % 3]]; if (a != b) { neighbors[a].Add(b); neighbors[b].Add(a); } }
            for (int iteration = 0; iteration < 96; iteration++)
            {
                for (int n = 0; n < count; n++) for (int b = 0; b < 17; b++)
                {
                    float sum = 0f; foreach (int neighbor in neighbors[n]) sum += current[neighbor, b];
                    next[n, b] = positions[n].y < 1.04f && neighbors[n].Count > 0 ? Mathf.Lerp(current[n, b], sum / neighbors[n].Count, .60f) : current[n, b];
                }
                var swap = current; current = next; next = swap;
            }
            var output = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                int n = map[i];
                // Prevent torso/body weight from shearing the lower legs and knees below y=0.58
                if (positions[n].y < .58f && positions[n].z > -.82f)
                {
                    float legSum = 0f;
                    for (int b = 5; b < 17; b++) legSum += current[n, b];
                    if (legSum > .2f) current[n, bodyBone] = 0f;
                }
                int[] indices = { 0, 0, 0, 0 }; float[] values = { 0, 0, 0, 0 };
                for (int b = 0; b < 17; b++) for (int slot = 0; slot < 4; slot++) if (current[n, b] > values[slot])
                { for (int k = 3; k > slot; k--) { values[k] = values[k - 1]; indices[k] = indices[k - 1]; } values[slot] = current[n, b]; indices[slot] = b; break; }
                float total = Mathf.Max(.0001f, values.Sum());
                output[i] = new BoneWeight { boneIndex0 = indices[0], weight0 = values[0] / total, boneIndex1 = indices[1], weight1 = values[1] / total, boneIndex2 = indices[2], weight2 = values[2] / total, boneIndex3 = indices[3], weight3 = values[3] / total };
            }
            return output;
        }

        public static void RepairHeroSkin(SkinnedMeshRenderer heroSmr)
        {
            if (heroSmr == null || heroSmr.sharedMesh == null || heroSmr.bones == null || heroSmr.bones.Length < 20) return;
            const string assetDir = "Assets/Art/Meshy/Generated";
            const string assetPath = assetDir + "/ThanhGiongHeroSkinned.asset";
            Directory.CreateDirectory(assetDir);

            Mesh source = heroSmr.sharedMesh;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = "ThanhGiong_Hero_Cleaned";
                MeshyMountedCombatPose.CleanHeroMeshWeights(mesh, heroSmr.bones);
                AssetDatabase.CreateAsset(mesh, assetPath);
            }
            else
            {
                MeshyMountedCombatPose.CleanHeroMeshWeights(mesh, heroSmr.bones);
                EditorUtility.SetDirty(mesh);
            }
            heroSmr.sharedMesh = mesh;
            EditorUtility.SetDirty(heroSmr);

            var bamboo = heroSmr.transform.parent != null
                ? heroSmr.transform.parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Weapon_GoldenBamboo")
                : null;
            if (bamboo != null && bamboo.parent != null && bamboo.parent.name == "RightHand")
            {
                bamboo.localPosition = new Vector3(-.005f, .105f, -.012f);
                bamboo.localRotation = Quaternion.Euler(78f, 0f, 0f);
                EditorUtility.SetDirty(bamboo);
            }
        }

        [MenuItem("Tools/Thánh Gióng/Meshy/Repair Horse Skin In Campaign Maps")]
        public static void RepairAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before repairing skin.");
            EditorSceneManager.SaveOpenScenes(); var setup = EditorSceneManager.GetSceneManagerSetup();
            string backup = "HorseRepairVerification/SceneBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup);
            int count = 0;
            var repairedMeshes = new System.Collections.Generic.HashSet<Mesh>();
            try
            {
                foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Meshy", "Assets/Prefabs/Player" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var prefab = PrefabUtility.LoadPrefabContents(path); bool changed = false;
                    try
                    {
                        foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            if (skin.name == "Meshy Horse Skin" && skin.bones.Length == 17) { Repair(skin, repairedMeshes.Add(skin.sharedMesh)); changed = true; }
                            else if (skin.bones != null && skin.bones.Any(b => b != null && b.name == "LeftHand")) { RepairHeroSkin(skin); changed = true; }
                        }
                        if (changed) { File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), true); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }
                foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/ThanhGiongWorld" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), true);
                    var scene = EditorSceneManager.OpenScene(path);
                    foreach (var skin in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (skin.name == "Meshy Horse Skin" && skin.bones.Length == 17) { Repair(skin, repairedMeshes.Add(skin.sharedMesh)); count++; }
                        else if (skin.bones != null && skin.bones.Any(b => b != null && b.name == "LeftHand")) { RepairHeroSkin(skin); }
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets(); Debug.Log("HORSE_SKIN_REPAIR PASS sceneRigs=" + count + " backup=" + backup);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
