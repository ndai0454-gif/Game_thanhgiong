using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Installer helper: attaches existing KayKit animated art to an existing gameplay actor.</summary>
public static class LegendEnemyArt
{
    public const string SoldierAsset = "Assets/Prefabs/Enemies/QinSoldier_Meshy.prefab";
    public const string BossAsset = "Assets/Prefabs/Enemies/GiacAnGeneral_Meshy.prefab";
    const string ModelName = "Legend Enemy Model";

    public static bool Apply(ThanhGiongEnemy enemy)
    {
        if (enemy == null) return false;
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply enemy art in Edit mode before gameplay Awake caches its Visual pivot.");
        if (enemy.isBoss) return ThanhGiong.EditorTools.GiacAnGeneralCampaignInstaller.ApplyBoss(enemy);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SoldierAsset) != null) return ThanhGiong.EditorTools.QinSoldierCampaignInstaller.ApplySoldier(enemy);
        Transform visual = enemy.transform.Find("Visual");
        if (visual == null) { GameObject pivot = new GameObject("Visual"); pivot.transform.SetParent(enemy.transform, false); visual = pivot.transform; }
        LegendEnemyPresentation presentation = enemy.GetComponent<LegendEnemyPresentation>();
        if (presentation == null) presentation = enemy.gameObject.AddComponent<LegendEnemyPresentation>();
        GameObject model;
        Transform installed = visual.Find(ModelName);
        string path = enemy.isBoss ? BossAsset : SoldierAsset;
        if (installed != null) model = installed.gameObject;
        else
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Required authored enemy asset missing: " + path);
            model = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = ModelName;
            // Source soldier is a complete gameplay prefab; keep only its rig, renderers, Animator and held art.
            foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                if (child.name == "Boss_Ground_Aura") child.gameObject.SetActive(false);
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            { animator.applyRootMotion = false; animator.updateMode = AnimatorUpdateMode.Normal; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            FitFeet(enemy, visual, model.transform);
            AddClothRecognition(model.transform, enemy.isBoss);
        }
        var placeholders = new List<Renderer>();
        foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(model.transform) || renderer is LineRenderer || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>() != null) continue;
            renderer.enabled = false; placeholders.Add(renderer);
        }
        presentation.model = model.transform;
        presentation.sourceAsset = path;
        presentation.placeholders = placeholders.ToArray();
        EditorUtility.SetDirty(enemy); EditorUtility.SetDirty(presentation);
        return presentation.IsUsingAuthoredModel;
    }

    static void FitFeet(ThanhGiongEnemy enemy, Transform visual, Transform model)
    {
        bool found = false; Bounds bounds = new Bounds();
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Bounds world = renderer.bounds;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                Vector3 point = visual.InverseTransformPoint(new Vector3(x == 0 ? world.min.x : world.max.x, y == 0 ? world.min.y : world.max.y, z == 0 ? world.min.z : world.max.z));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
            }
        }
        if (!found || bounds.size.y < .01f) throw new InvalidOperationException("Authored enemy has no usable skin bounds: " + enemy.name);
        CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
        float height = capsule != null ? capsule.height : 2f;
        float scale = Mathf.Clamp(height / bounds.size.y, .1f, 4f);
        model.localScale = Vector3.one * scale;
        float bottom = capsule != null ? capsule.center.y - capsule.height * .5f : -1f;
        // Convert physical capsule feet to the existing Visual pivot rather than moving the enemy or collider.
        Vector3 feet = visual.InverseTransformPoint(enemy.transform.TransformPoint(new Vector3(0, bottom, 0)) + Vector3.up * .03f);
        model.localPosition = new Vector3(-bounds.center.x * scale, feet.y - bounds.min.y * scale, -bounds.center.z * scale);
    }

    static void AddClothRecognition(Transform model, bool boss)
    {
        // Small maroon cloth panel follows the actual waist bone; the imported axe/shield remain the only weapons.
        Transform pelvis = null;
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            string name = bone.name.ToLowerInvariant();
            if (name == "pelvis" || name == "hips" || name == "hip") { pelvis = bone; break; }
        }
        if (pelvis == null) return;
        GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cloth.name = "Invader Maroon Cloth"; UnityEngine.Object.DestroyImmediate(cloth.GetComponent<Collider>());
        cloth.transform.SetParent(pelvis, false);
        cloth.transform.localPosition = new Vector3(0, -.16f, .14f);
        cloth.transform.localScale = new Vector3(.35f, .35f, .035f);
        // The primitive's built-in serialized material is replaced by a maroon painted material at Awake.
        // No temporary unsaved material is stored in the scene or written over a shared KayKit asset.
    }
}
