using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Places the static Meshy character sculptures from the "nhanvat" branch
/// (Thanh Giong, Crimson Warlord) as landmarks in the open world.
/// Re-running replaces the previous statues.
/// </summary>
public static class ThanhGiongStatuePlacer
{
    const string ScenePath = "Assets/Scenes/ThanhGiongWorld/ThanhGiongDaiViet.unity";
    const string RootName = "Meshy statues · nhân vật";
    const string HeroGlb = "Assets/Art/Meshy/NhanVat/ThanhGiong_Statue.glb";
    const string WarlordGlb = "Assets/Art/Meshy/NhanVat/CrimsonWarlord_Statue.glb";
    const string ShotDir = "Artifacts/StatueShots";

    [MenuItem("Tools/Thanh Giong/Place Meshy Statues In Open World")]
    public static void Place()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName)) UnityEngine.Object.DestroyImmediate(old);
        Physics.SyncTransforms();
        var root = new GameObject(RootName).transform;
        StatueRoot = root;

        var world = UnityEngine.Object.FindFirstObjectByType<ThanhGiongOpenWorld>();
        var campaign = UnityEngine.Object.FindFirstObjectByType<ThanhGiongCampaignController>();
        Vector3 spawn = world != null ? world.safeSpawn : campaign != null ? campaign.transform.position : new Vector3(-55, 0, -20);
        Vector3 fwd = campaign != null ? Flat(campaign.transform.forward) : Vector3.forward;
        Debug.Log($"[Statues] spawn {spawn} forward {fwd}");

        var hero = Load(HeroGlb); var warlord = Load(WarlordGlb);
        // Thanh Giong greets the player near the start, and stands tall on Soc peak.
        Statue(hero, root, "Tượng Thánh Gióng · làng Gióng", spawn + fwd * 16f + Vector3.Cross(Vector3.up, fwd) * 7f, 4.5f, spawn);
        Statue(hero, root, "Tượng Thánh Gióng · đỉnh Sóc", new Vector3(-150f, 0f, -290f) + new Vector3(14f, 0, 14f), 8f, new Vector3(-55f, 0f, 0f));
        // The Crimson Warlord guards the enemy strongholds.
        Statue(warlord, root, "Tượng Tướng giặc Ân · đồn đỏ", new Vector3(-310f, 0f, 160f) + new Vector3(13f, 0, -30f), 5f, new Vector3(-310f, 0f, 120f));
        Statue(warlord, root, "Tượng Tướng giặc Ân · trận đen", new Vector3(-310f, 0f, -160f) + new Vector3(13f, 0, 30f), 5f, new Vector3(-310f, 0f, -120f));

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save " + ScenePath);
        Debug.Log("[Statues] Saved " + ScenePath);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < .001f ? Vector3.forward : v.normalized; }

    static GameObject Load(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Cannot load " + path);
    }

    static void Statue(GameObject asset, Transform root, string name, Vector3 wanted, float height, Vector3 faceTowards)
    {
        Vector3 pos = FindClearGround(wanted, height * .45f);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.gameObject.scene);
        go.name = name;
        go.transform.SetParent(root, true);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(Flat(faceTowards - pos), Vector3.up);
        var b = Bounds(go);
        go.transform.localScale *= height / Mathf.Max(b.size.y, .001f);
        b = Bounds(go);
        go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y - .05f, pos.z - b.center.z);
        b = Bounds(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
        var col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(b.center);
        var s = go.transform.lossyScale;
        col.size = new Vector3(b.size.x * .6f / s.x, b.size.y / s.y, b.size.z * .6f / s.z);
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
        // Attach atmospheric glow aura and animator for statue landmarks
        var lightGo = new GameObject(name + " · Hào Quang");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
        var ptLight = lightGo.AddComponent<Light>();
        ptLight.type = LightType.Point;
        ptLight.range = height * 2.8f;
        bool isHero = name.Contains("Gióng");
        ptLight.color = isHero ? new Color(1.0f, 0.88f, 0.45f) : new Color(0.95f, 0.22f, 0.15f);
        ptLight.intensity = isHero ? 2.5f : 1.8f;
        ptLight.shadows = LightShadows.None;

        var vfxAnim = go.AddComponent<ThanhGiong.Environment.MeshyPropVfxAnimator>();
        vfxAnim.effectKind = isHero ? ThanhGiong.Environment.MeshyPropVfxAnimator.PropEffectKind.SacredStatueAura : ThanhGiong.Environment.MeshyPropVfxAnimator.PropEffectKind.CrimsonWarlordGaze;
        vfxAnim.propLight = ptLight;
        vfxAnim.baseLightIntensity = ptLight.intensity;
        Debug.Log($"[Statues] {name}: {b.size} at {go.transform.position} (asked {wanted})");
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    // Walk outward from the wanted spot until the ground there has no other colliders in the footprint.
    static Transform StatueRoot;
    static Renderer[] SceneRenderers = Array.Empty<Renderer>();

    static Vector3 FindClearGround(Vector3 wanted, float radius)
    {
        SceneRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => !(r is ParticleSystemRenderer) && !r.transform.IsChildOf(StatueRoot)).ToArray();
        for (int ring = 0; ring < 10; ring++)
            for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
            {
                float a = k * Mathf.PI / 4f;
                var p = wanted + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ring * 4f;
                if (!Physics.Raycast(new Vector3(p.x, 600f, p.z), Vector3.down, out var hit, 1200f, ~0, QueryTriggerInteraction.Ignore)) continue;
                // Many props have no collider, so test rendered bounds too; huge ground/road meshes are ignored.
                var foot = new Bounds(hit.point + Vector3.up * (radius + .6f), Vector3.one * radius * 2f);
                if (SceneRenderers.Any(r => r && r.bounds.Intersects(foot) && r.bounds.size.x < 40f && r.bounds.size.z < 40f)) continue;
                var hits = Physics.OverlapBox(foot.center, foot.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (hits.All(h => h == hit.collider || h.bounds.size.x > 40f || h.bounds.size.z > 40f)) return hit.point;
            }
        Debug.LogWarning("[Statues] No clear ground near " + wanted + ", using raycast point");
        return Physics.Raycast(new Vector3(wanted.x, 600f, wanted.z), Vector3.down, out var h2, 1200f) ? h2.point : wanted;
    }

    /// <summary>Renders a picture of each statue so placement can be checked without opening the editor.</summary>
    public static void Capture()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
        Directory.CreateDirectory(ShotDir);
        var camGo = new GameObject("StatueShotCamera"); var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50; cam.farClipPlane = 2000;
        var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt;
        int i = 0;
        foreach (Transform t in root)
        {
            var b = Bounds(t.gameObject);
            Vector3 front = t.forward; float d = b.size.y * 2.2f;
            cam.transform.position = b.center + front * d + Vector3.up * b.size.y * .25f;
            cam.transform.LookAt(b.center);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes($"{ShotDir}/{++i}_{t.name.Split('·').Last().Trim().Replace(' ', '_')}.png", tex.EncodeToPNG());
        }
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(camGo);
        Debug.Log("[Statues] Captured " + i + " shots in " + ShotDir);
    }

    public static void PlaceAndCaptureOnly()
    {
        Place();
        Capture();
    }

    public static void PlaceCaptureAndBuild()
    {
        Place();
        try { Capture(); } catch (Exception e) { Debug.LogWarning("[Statues] Capture failed: " + e.Message); }
        ThanhGiongBuildRelease.BuildReleaseBatch();
    }
}
