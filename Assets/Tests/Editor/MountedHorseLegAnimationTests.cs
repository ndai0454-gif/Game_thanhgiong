using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class MountedHorseLegAnimationTests
{
    const string PrefabPath = "Assets/Prefabs/Player/ThanhGiong_Mounted.prefab";
    const string SourcePath = "Assets/Art/Models/base.obj";
    GameObject instance;

    [SetUp]
    public void SetUp()
    {
        instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
    }

    [TearDown]
    public void TearDown()
    {
        if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
    }

    SkinnedMeshRenderer Skin => instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
        .Single(s => s.name == "Horse_AnimatedMesh");
    MeshFilter Source => instance.GetComponentsInChildren<MeshFilter>(true)
        .Single(f => AssetDatabase.GetAssetPath(f.sharedMesh) == SourcePath);

    [Test]
    public void OriginalHorseMeshReplacesTheAddedCubeLegs()
    {
        foreach (string name in new[] { "Front Left Leg", "Front Right Leg", "Rear Left Leg", "Rear Right Leg" })
            Assert.IsNull(instance.transform.Find(name), "Added cube leg still present: " + name);
        Assert.IsFalse(Source.GetComponent<MeshRenderer>().enabled, "Static horse would overlap the animated skin.");
        Assert.AreEqual(Source.sharedMesh.vertexCount, Skin.sharedMesh.vertexCount);
        CollectionAssert.AreEqual(Source.sharedMesh.triangles, Skin.sharedMesh.triangles,
            "The animation must preserve the original model geometry.");
        CollectionAssert.AreEqual(Source.GetComponent<MeshRenderer>().sharedMaterials, Skin.sharedMaterials);
    }

    [Test]
    public void RestPoseMatchesTheOriginalHorseAtItsPrefabScale()
    {
        Mesh baked = new Mesh();
        try
        {
            Skin.BakeMesh(baked, true);
            Vector3[] original = Source.sharedMesh.vertices;
            Vector3[] current = baked.vertices;
            float error = original.Select((v, i) => Vector3.Distance(
                Source.transform.TransformPoint(v), Skin.transform.TransformPoint(current[i]))).Max();
            Assert.Less(error, .001f, "Skinning changed the original model's position or scale.");
        }
        finally { UnityEngine.Object.DestroyImmediate(baked); }
    }

    [Test]
    public void AllFourOriginalLegsDeformWhileTheRiderStaysStill()
    {
        SkinnedMeshRenderer skin = Skin;
        Mesh before = new Mesh();
        Mesh after = new Mesh();
        try
        {
            skin.BakeMesh(before, true);
            for (int leg = 1; leg <= 4; leg++)
            {
                skin.bones[leg].localRotation = Quaternion.Euler(leg % 2 == 0 ? 28f : -28f, 0, 0);
                skin.bones[leg + 4].localRotation = Quaternion.Euler(24f, 0, 0);
            }
            skin.BakeMesh(after, true);
            Vector3[] a = before.vertices, b = after.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            for (int leg = 1; leg <= 4; leg++)
            {
                int moved = Enumerable.Range(0, a.Length).Count(i =>
                    weights[i].boneIndex1 == leg && weights[i].weight1 + weights[i].weight2 > .6f &&
                    Vector3.Distance(a[i], b[i]) > .005f);
                Assert.Greater(moved, 30, "Original leg mesh did not deform: " + skin.bones[leg].name);
            }
            Vector3[] original = Source.sharedMesh.vertices;
            float riderError = Enumerable.Range(0, a.Length).Where(i => original[i].y > 1.2f)
                .Max(i => Vector3.Distance(a[i], b[i]));
            Assert.Less(riderError, .0001f, "Leg animation deformed the seated rider.");
        }
        finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
    }
}
