using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Diagnostic: when the player is started with "-tgRiderShots &lt;folder&gt;", photographs the mounted
/// Thanh Giong from several angles standing and galloping, writes PNGs to the folder, then quits.
/// Does nothing in a normal run.
/// </summary>
public sealed class ThanhGiongRiderShots : MonoBehaviour
{
    string dir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-tgRiderShots");
        if (i < 0 || i + 1 >= args.Length) return;
        var go = new GameObject("RiderShots");
        DontDestroyOnLoad(go);
        go.AddComponent<ThanhGiongRiderShots>().dir = args[i + 1];
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(dir);
        ThanhGiongCampaignController actor = null;
        for (int t = 0; t < 600 && actor == null; t++) { actor = FindAnyObjectByType<ThanhGiongCampaignController>(); yield return null; }
        if (actor == null) { Debug.LogError("[RiderShots] no player"); Application.Quit(2); yield break; }
        var movement = actor.GetComponent<MountedHorseController>();
        if (actor.openWorldCampaign != null) actor.openWorldCampaign.persistenceEnabled = false;
        var follow = IsometricCameraFollow.Instance;
        if (follow != null) follow.mouseLook = false;
        if (movement != null) movement.enabled = false;
        for (int f = 0; f < 90; f++) { movement?.DriveMovement(Vector2.zero, false); yield return null; }

        var camGo = new GameObject("RiderShotCam");
        var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 35f; cam.nearClipPlane = .05f; cam.farClipPlane = 1500f;
        var rt = new RenderTexture(1280, 960, 24); cam.targetTexture = rt;

        yield return Shoot(actor.transform, cam, rt, "idle");

        // Gallop forward and grab mid-stride frames.
        for (int f = 0; f < 70; f++) { movement?.DriveMovement(Vector2.up, true); yield return null; }
        yield return Shoot(actor.transform, cam, rt, "gallop", movement);

        // Swing the weapon from a standstill and photograph mid-strike.
        for (int f = 0; f < 40; f++) { movement?.DriveMovement(Vector2.zero, false); yield return null; }
        if (movement != null && movement.TryAttack())
        {
            for (int f = 0; f < 8; f++) { movement.DriveMovement(Vector2.zero, false); yield return null; }
            yield return Shoot(actor.transform, cam, rt, "attack");
        }
        else Debug.Log("[RiderShots] attack not available");

        LogPose(actor.transform);
        Debug.Log("[RiderShots] done");
        Application.Quit(0);
    }

    IEnumerator Shoot(Transform actor, Camera cam, RenderTexture rt, string tag, MountedHorseController keepRiding = null)
    {
        var views = new (string name, float yaw, float pitch)[] { ("side_left", -90, 8), ("side_right", 90, 8), ("front", 0, 10), ("back", 180, 14), ("three_quarter", -40, 18) };
        foreach (var v in views)
        {
            if (keepRiding != null) { keepRiding.DriveMovement(Vector2.up, true); }
            yield return new WaitForEndOfFrame();
            Bounds b = Bounds(actor);
            Vector3 viewDir = Quaternion.Euler(v.pitch, actor.eulerAngles.y + v.yaw, 0) * Vector3.forward;
            float dist = Mathf.Max(b.size.y, b.size.z, b.size.x) * 1.9f + 1f;
            cam.transform.position = b.center + viewDir * dist;
            cam.transform.LookAt(b.center);
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(System.IO.Path.Combine(dir, $"{tag}_{v.name}.png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }

    static Bounds Bounds(Transform root)
    {
        var rs = root.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(root.position + Vector3.up, Vector3.one); bool first = true;
        foreach (var r in rs)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.bounds.size.magnitude > 12f) continue; // skip effects or rings
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static void LogPose(Transform root)
    {
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            Debug.Log($"[RiderShots] {NodePath(smr.transform, root)} active={smr.gameObject.activeInHierarchy} bounds={smr.bounds.center - root.position} size={smr.bounds.size} root={smr.rootBone?.name}");
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name is "Meshy_Mounted_ThanhGiong" or "Mount" or "IronHorse" or "ThanhGiong_Hero_Meshy" or "Hips" or "Head" or "LeftFoot" or "RightFoot" or "LeftLeg" or "body" or "neck" or "Neck" or "head")
                Debug.Log($"[RiderShots] T {NodePath(t, root)} lpos={t.localPosition} lscale={t.localScale} lossy={t.lossyScale} wpos-root={t.position - root.position}");
        foreach (var a in root.GetComponentsInChildren<Animator>())
            Debug.Log($"[RiderShots] animator {NodePath(a.transform, root)} ctrl={a.runtimeAnimatorController?.name} avatar={a.avatar?.name} enabled={a.enabled}");
    }

    static string NodePath(Transform t, Transform root) { string p = t.name; while (t.parent != null && t != root) { t = t.parent; p = t.name + "/" + p; } return p; }
}
