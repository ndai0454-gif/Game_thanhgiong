using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ArenaInspection
{
    public static string Visuals()
    {
        var flow = Object.FindAnyObjectByType<ThanhGiongArenaFlow>();
        return Newtonsoft.Json.JsonConvert.SerializeObject(flow.GetComponentsInChildren<Renderer>(true).Select(r => new {
            r.name, enabled = r.enabled, active = r.gameObject.activeInHierarchy, type = r.GetType().Name,
            center = r.bounds.center.ToString(), size = r.bounds.size.ToString(),
            materials = r.sharedMaterials.Select(m => m != null ? m.name : "missing").ToArray()
        }), Newtonsoft.Json.Formatting.Indented);
    }
    public static string Audit()
    {
        var flow = Object.FindAnyObjectByType<ThanhGiongArenaFlow>();
        var player = flow.GetComponent<MountedHorseController>();
        var rig = flow.GetComponent<MountedHorseMeshMotion>();
        var skin = flow.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == "Horse_AnimatedMesh");
        var campaign = flow.GetComponent<ThanhGiongCampaignController>();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new {
            scene = flow.gameObject.scene.name, stage = flow.CurrentStage.ToString(), health = campaign.Health,
            weapon = campaign.CurrentWeapon.ToString(), flow.ElapsedSeconds, campaign.Kills, flow.BambooPulls,
            wave = flow.waveManager.WaveIndex, alive = flow.waveManager.AliveCount, spawned = flow.waveManager.Spawned,
            grounded = player.Grounded, position = flow.transform.position.ToString(), originalSkinVertices = skin.sharedMesh.vertexCount,
            rigReady = rig.IsReady, feet = rig.hooves.Select(f => f.position.ToString()).ToArray(),
            footPlaneSpread = rig.hooves.Max(f => f.position.y) - rig.hooves.Min(f => f.position.y),
            cameraPerspective = !Camera.main.orthographic,
            renderers = flow.GetComponentsInChildren<Renderer>().Length,
            bambooBounds = flow.bamboo.GetComponentsInChildren<Renderer>().Select(r => new { r.name, center = r.bounds.center.ToString(), size = r.bounds.size.ToString() }).ToArray()
        }, Newtonsoft.Json.Formatting.Indented);
    }
    public static string Capture()
    {
        Directory.CreateDirectory("Temp/ArenaValidation");
        Camera camera = Camera.main;
        var rt = new RenderTexture(1280, 720, 24);
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        RenderPipeline.SubmitRenderRequest(camera, request);
        var old = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        string path = Path.GetFullPath("Temp/ArenaValidation/arena-camera.png");
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image); RenderTexture.active = old; rt.Release(); Object.DestroyImmediate(rt);
        return path;
    }
}

