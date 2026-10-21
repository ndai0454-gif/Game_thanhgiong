using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Turns the supplied static OBJ and reference texture into a reusable URP prefab.
/// The source mesh is intentionally kept separate from the controllable animated player.
/// </summary>
public static class MountedThanhGiongAssetBuilder
{
    private const string ModelPath = "Assets/Art/Models/base.obj";
    private const string TexturePath = "Assets/Art/Textures/ThanhGiong_Mounted_Albedo.png";
    private const string OutputFolder = "Assets/Prefabs/Player";
    private const string MaterialPath = "Assets/Materials/Characters/ThanhGiong_Mounted.mat";
    private const string PrefabPath = OutputFolder + "/ThanhGiong_Mounted.prefab";
    private const string ScenePath = "Assets/Scenes/AlbionForestMap.unity";
    private const string SceneObjectName = "Thanh Giong Mounted 3D";

    [InitializeOnLoadMethod]
    private static void BuildWhenReady()
    {
        EditorApplication.update -= TryBuild;
        EditorApplication.update += TryBuild;
    }

    private static void TryBuild()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null) return;

        EditorApplication.update -= TryBuild;
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            MountedHorseController controller = prefab != null ? prefab.GetComponent<MountedHorseController>() : null;
            bool needsRebuild = prefab == null || controller == null || controller.rigVersion < 3 || controller.frontLeftLeg == null;
            if (!needsRebuild) return;
            BuildPrefab();
            PlaceInMap();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Thanh Giong] Mounted OBJ asset is ready at " + PrefabPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Thanh Giong/Rebuild Mounted OBJ Asset")]
    public static void Rebuild()
    {
        BuildPrefab();
        PlaceInMap();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void BuildPrefab()
    {
        Directory.CreateDirectory(OutputFolder);
        ConfigureTexture();
        MountedHorseMeshRigBuilder.EnsureReadableSource();

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (source == null) throw new InvalidOperationException("Missing model: " + ModelPath);

        Material material = CreateMaterial();
        GameObject root = new GameObject("Thanh Giong Mounted");
        GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (visual == null) throw new InvalidOperationException("Could not instantiate model: " + ModelPath);

        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localRotation = Quaternion.identity;

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            int slots = Mathf.Max(1, renderer.sharedMaterials.Length);
            renderer.sharedMaterials = Enumerable.Repeat(material, slots).ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        // Normalize the generated OBJ to a useful in-game height while preserving proportions.
        Bounds initialBounds = CalculateBounds(visual);
        const float targetHeight = 3.25f;
        if (initialBounds.size.y > 0.0001f)
            visual.transform.localScale *= targetHeight / initialBounds.size.y;

        Bounds groundedBounds = CalculateBounds(visual);
        visual.transform.position += Vector3.up * -groundedBounds.min.y;

        Bounds finalBounds = CalculateBounds(visual);
        CharacterController collider = root.AddComponent<CharacterController>();
        collider.center = new Vector3(0f, finalBounds.size.y * 0.5f, 0f);
        collider.height = finalBounds.size.y;
        collider.radius = Mathf.Min(0.78f, finalBounds.size.x * 0.48f);
        collider.stepOffset = 0.35f;
        collider.slopeLimit = 48f;

        MountedHorseController movement = root.AddComponent<MountedHorseController>();
        movement.visual = visual.transform;
        MountedHorseMeshRigBuilder.Configure(movement);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (saved == null) throw new InvalidOperationException("Could not save prefab: " + PrefabPath);
    }

    private static void ConfigureTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }
        if (importer.maxTextureSize < 1024)
        {
            importer.maxTextureSize = 1024;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    private static Material CreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            shader ??= Shader.Find("Standard");
            material = new Material(shader) { name = "Thanh Giong Mounted Material" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.15f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void PlaceInMap()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == SceneObjectName) UnityEngine.Object.DestroyImmediate(root);

        Transform spawn = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "Player Spawn");

        Vector3 position = spawn != null ? spawn.position + new Vector3(3.5f, 0.05f, 1.5f) : new Vector3(-35.5f, 0.25f, -22.5f);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null) throw new InvalidOperationException("Could not place prefab in scene.");
        instance.name = SceneObjectName;
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 145f, 0f));

        // The mounted character becomes the active player. Disable the older controller
        // so one set of WASD input cannot move two characters at the same time.
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (AdventurerController oldController in root.GetComponentsInChildren<AdventurerController>(true))
                oldController.enabled = false;

        Camera camera = Camera.main;
        if (camera != null)
        {
            IsometricCameraFollow follow = camera.GetComponent<IsometricCameraFollow>();
            if (follow == null) follow = camera.gameObject.AddComponent<IsometricCameraFollow>();
            follow.target = instance.transform;
            follow.lookHeight = 1.55f;
            camera.transform.position = instance.transform.position + follow.offset;
            camera.transform.LookAt(instance.transform.position + Vector3.up * follow.lookHeight);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
    }
}
