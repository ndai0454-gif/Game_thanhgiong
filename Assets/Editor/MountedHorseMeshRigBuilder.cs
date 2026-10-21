using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Skins the horse legs already present in the mounted OBJ, preserving its rider and materials.</summary>
public static class MountedHorseMeshRigBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Player/ThanhGiong_Mounted.prefab";
    public const string SourcePath = "Assets/Art/Models/base.obj";
    public const string SkinPath = "Assets/Art/Models/ThanhGiong_Mounted_HorseSkin.asset";
    public const int RigVersion = 5;

    // The generated mesh is asymmetric: its centre is at X=-0.09, not X=0.
    // Each source leg therefore needs its own joint and hoof landmarks.
    static readonly Vector3[] SourceHips = {
        new Vector3(-.235f, 1.01f, .53f), new Vector3(.045f, .98f, .48f),
        new Vector3(-.235f, .60f, -.20f), new Vector3(.06f, .60f, -.20f)
    };
    static readonly Vector3[] SourceKnees = {
        new Vector3(-.27f, .87f, .88f), new Vector3(.025f, .79f, .755f),
        new Vector3(-.245f, .29f, -.15f), new Vector3(.08f, .285f, -.255f)
    };
    static readonly Vector3[] SourceFeet = {
        new Vector3(-.255f, .71f, .88f), new Vector3(.02f, .59f, .755f),
        new Vector3(-.27f, .06f, .09f), new Vector3(.11f, .04f, -.105f)
    };

    public static void EnsureReadableSource()
    {
        var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    [MenuItem("Tools/Thanh Giong/Repair Original Horse Leg Animation")]
    public static void RepairPrefab()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before repairing the horse prefab.");
        EnsureReadableSource();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Configure(root.GetComponent<MountedHorseController>());
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    public static void Configure(MountedHorseController movement)
    {
        if (movement == null) throw new ArgumentNullException(nameof(movement));
        MeshFilter filter = movement.visual.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(f => AssetDatabase.GetAssetPath(f.sharedMesh) == SourcePath);
        if (filter == null || !filter.sharedMesh.isReadable)
            throw new InvalidOperationException("The mounted OBJ must have Read/Write enabled before creating its leg rig.");
        MeshRenderer sourceRenderer = filter.GetComponent<MeshRenderer>();
        if (sourceRenderer == null) throw new InvalidOperationException("The mounted OBJ has no source renderer.");

        // These are the old cube appendages, not the four legs in the source mesh.
        foreach (string name in new[] { "Front Left Leg", "Front Right Leg", "Rear Left Leg", "Rear Right Leg" })
        {
            Transform leg = movement.transform.Find(name);
            if (leg != null) UnityEngine.Object.DestroyImmediate(leg.gameObject);
        }
        foreach (string name in new[] { "HorseMeshRig", "Horse_AnimatedMesh" })
        {
            Transform old = filter.transform.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        Transform rig = Child(filter.transform, "HorseMeshRig", Vector3.zero);
        Transform body = Child(rig, "Body", Vector3.zero);
        Vector3 pivot = new Vector3(-.09f, .65f, -.26f);
        Quaternion level = Quaternion.Euler(28f, 0, 0);
        Matrix4x4 bodyPose = Matrix4x4.TRS(pivot + Vector3.up * .025f, level, Vector3.one)
            * Matrix4x4.Translate(-pivot);
        Vector3 saddle = new Vector3(-.09f, .92f, -.12f);
        Matrix4x4 riderPose = Matrix4x4.Translate(bodyPose.MultiplyPoint3x4(saddle) - saddle);
        Vector3[] hips = SourceHips.Select(bodyPose.MultiplyPoint3x4).ToArray();
        Vector3[] knees = new Vector3[4], feet = new Vector3[4];
        Matrix4x4[] unpose = new Matrix4x4[14];
        unpose[0] = bodyPose;
        unpose[13] = riderPose;
        string[] legNames = { "FrontLeft", "FrontRight", "RearLeft", "RearRight" };
        Transform[] bones = new Transform[14];
        bones[0] = body;
        for (int i = 0; i < 4; i++)
        {
            // A relaxed, slightly bent stance with all four soles on one plane.
            feet[i] = new Vector3(hips[i].x, .055f, hips[i].z + (i < 2 ? .015f : -.025f));
            knees[i] = Vector3.Lerp(hips[i], feet[i], .52f) + Vector3.forward * (i < 2 ? .105f : -.115f);
            unpose[1 + i] = MapSegment(SourceHips[i], SourceKnees[i], hips[i], knees[i]);
            unpose[5 + i] = MapSegment(SourceKnees[i], SourceFeet[i], knees[i], feet[i]);
            unpose[9 + i] = Matrix4x4.Translate(feet[i] - SourceFeet[i]);
            bones[1 + i] = Child(body, legNames[i], hips[i]);
            bones[5 + i] = Child(bones[1 + i], "Lower", knees[i] - hips[i]);
            bones[9 + i] = Child(bones[5 + i], "Hoof", feet[i] - knees[i]);
        }
        bones[13] = Child(rig, "Rider", Vector3.zero);

        Mesh mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
        mesh.name = "ThanhGiong Mounted Original Horse Skin";
        Vector3[] vertices = mesh.vertices;
        BoneWeight[] weights = vertices.Select(Weight).ToArray();
        mesh.vertices = vertices.Select((v, i) => PoseVertex(v, weights[i], unpose)).ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * filter.transform.localToWorldMatrix).ToArray();
        mesh.boneWeights = weights;
        Mesh stored = AssetDatabase.LoadAssetAtPath<Mesh>(SkinPath);
        if (stored == null) { AssetDatabase.CreateAsset(mesh, SkinPath); stored = mesh; }
        else
        {
            EditorUtility.CopySerialized(mesh, stored);
            EditorUtility.SetDirty(stored);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        Transform skinTransform = Child(filter.transform, "Horse_AnimatedMesh", Vector3.zero);
        var skin = skinTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = stored;
        skin.sharedMaterials = sourceRenderer.sharedMaterials;
        skin.bones = bones;
        skin.rootBone = body;
        skin.quality = SkinQuality.Bone4;
        skin.shadowCastingMode = sourceRenderer.shadowCastingMode;
        skin.receiveShadows = sourceRenderer.receiveShadows;
        Bounds bounds = stored.bounds;
        bounds.Expand(.6f);
        skin.localBounds = bounds;
        sourceRenderer.enabled = false;

        movement.frontLeftLeg = bones[1];
        movement.frontRightLeg = bones[2];
        movement.rearLeftLeg = bones[3];
        movement.rearRightLeg = bones[4];
        movement.rigVersion = RigVersion;
        var gait = movement.GetComponent<MountedHorseMeshMotion>();
        if (gait == null) gait = movement.gameObject.AddComponent<MountedHorseMeshMotion>();
        gait.Configure(rig, bones.Skip(1).Take(4).ToArray(), bones.Skip(5).Take(4).ToArray(),
            bones.Skip(9).Take(4).ToArray());
    }

    static Transform Child(Transform parent, string name, Vector3 position)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = position;
        return child;
    }

    static float Smooth(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

    static Matrix4x4 MapSegment(Vector3 a, Vector3 b, Vector3 targetA, Vector3 targetB)
    {
        float scale = Vector3.Distance(targetA, targetB) / Vector3.Distance(a, b);
        return Matrix4x4.TRS(targetA, Quaternion.FromToRotation(b - a, targetB - targetA), Vector3.one * scale)
            * Matrix4x4.Translate(-a);
    }

    static Vector3 PoseVertex(Vector3 vertex, BoneWeight w, Matrix4x4[] poses) =>
        poses[w.boneIndex0].MultiplyPoint3x4(vertex) * w.weight0 +
        poses[w.boneIndex1].MultiplyPoint3x4(vertex) * w.weight1 +
        poses[w.boneIndex2].MultiplyPoint3x4(vertex) * w.weight2 +
        poses[w.boneIndex3].MultiplyPoint3x4(vertex) * w.weight3;

    static BoneWeight Weight(Vector3 vertex)
    {
        // Keep the seated rider upright while the horse's back is levelled.
        float rider = Smooth((vertex.y - .83f) / .23f) * (1f - Smooth((vertex.z - .28f) / .16f));
        // The spear and the rider's boots are outside the horse's narrow chest.
        rider = Mathf.Max(rider, Smooth((vertex.x - .22f) / .055f) * (1f - Smooth((vertex.z - .12f) / .2f)));
        rider = Mathf.Max(rider, Smooth((Mathf.Abs(vertex.x + .09f) - .21f) / .045f)
            * Smooth((vertex.y - .62f) / .14f) * (1f - Smooth((vertex.z - .48f) / .1f)));
        if (rider > .999f) return new BoneWeight { boneIndex0 = 13, weight0 = 1f };

        float side = Smooth((Mathf.Abs(vertex.x + .115f) - .045f) / .035f);
        float front = side * Smooth((1.035f - vertex.y) / .14f) * Smooth((vertex.z - .52f) / .10f);
        float rear = Smooth((.64f - vertex.y) / .15f)
            * Smooth((vertex.z + .39f) / .09f) * Smooth((.27f - vertex.z) / .08f)
            * Smooth((.29f - Mathf.Abs(vertex.x + .09f)) / .055f);
        bool foreleg = front > rear;
        float influence = Mathf.Max(front, rear) * (1f - rider);
        int i = (foreleg ? 0 : 2) + (vertex.x < -.09f ? 0 : 1);
        // Blend at the measured joint, rather than using one height for both sides.
        Vector3 axis = SourceFeet[i] - SourceKnees[i];
        float along = Vector3.Dot(vertex - SourceKnees[i], axis.normalized);
        float lower = Smooth((along + .055f) / .11f);
        float hoof = Smooth((along - axis.magnitude + .095f) / .07f);
        float bodyWeight = 1f - rider - influence;
        // At the saddle seam the rider/body blend takes precedence over leg weights.
        if (rider > .001f) return new BoneWeight { boneIndex0 = 13, weight0 = rider,
            boneIndex1 = 0, weight1 = 1f - rider };
        return new BoneWeight {
            boneIndex0 = 0, weight0 = bodyWeight,
            boneIndex1 = 1 + i, weight1 = influence * (1f - lower),
            boneIndex2 = 5 + i, weight2 = influence * lower * (1f - hoof),
            boneIndex3 = 9 + i, weight3 = influence * lower * hoof
        };
    }
}
