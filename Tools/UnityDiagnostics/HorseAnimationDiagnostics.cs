using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;
using System.Threading.Tasks;

public static class HorseAnimationDiagnostics
{
    static string PathOf(Transform transform)
    {
        return transform == null ? null : transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;
    }

    public static string Inspect()
    {
        var report = new {
            scene = SceneManager.GetActiveScene().path,
            dirty = SceneManager.GetActiveScene().isDirty,
            players = UnityEngine.Object.FindObjectsByType<MountedHorseController>(FindObjectsInactive.Include).Select(p => new {
                name = p.name,
                visual = PathOf(p.visual),
                legs = new[] { p.frontLeftLeg, p.frontRightLeg, p.rearLeftLeg, p.rearRightLeg }.Select(PathOf).ToArray(),
                components = p.GetComponents<Component>().Select(c => c == null ? "Missing" : c.GetType().Name).ToArray(),
                meshes = p.GetComponentsInChildren<MeshFilter>(true).Select(f => new {
                    path = PathOf(f.transform),
                    mesh = f.sharedMesh == null ? null : f.sharedMesh.name,
                    asset = AssetDatabase.GetAssetPath(f.sharedMesh),
                    readable = f.sharedMesh != null && f.sharedMesh.isReadable,
                    bounds = f.sharedMesh == null ? null : f.sharedMesh.bounds.ToString("F3"),
                    scale = f.transform.lossyScale.ToString("F3"),
                    position = f.transform.localPosition.ToString("F3"),
                    rendererEnabled = f.GetComponent<Renderer>() != null && f.GetComponent<Renderer>().enabled
                }).ToArray(),
                skins = p.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => new { path = PathOf(s.transform), boneCount = s.bones.Length }).ToArray(),
                motion = p.GetComponentsInChildren<GeneratedCharacterMotion>(true).Select(m => new { path = PathOf(m.transform), kind = m.kind.ToString(), enabled = m.enabled }).ToArray()
            }).ToArray()
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(report);
    }

    public static string PrepareAndPreview()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Art/Models/base.obj");
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ThanhGiong_Mounted.prefab");
        var preview = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        preview.transform.position = new Vector3(10000, 0, 0);
        var meshFilter = preview.GetComponentsInChildren<MeshFilter>().First(f => AssetDatabase.GetAssetPath(f.sharedMesh) == "Assets/Art/Models/base.obj");
        var vertices = meshFilter.sharedMesh.vertices;
        var renderer = meshFilter.GetComponent<Renderer>();
        var bounds = renderer.bounds;
        var cameraObject = new GameObject("Horse rig diagnostic camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = bounds.size.y * .62f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.11f, .13f, .16f);
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 25f;
        string folder = System.IO.Path.Combine(Application.dataPath, "../Temp/CLIInspection/HorseRig");
        System.IO.Directory.CreateDirectory(folder);
        var shots = new System.Collections.Generic.List<string>();
        try {
            foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, new Vector3(1, .25f, 1) }) {
                camera.transform.position = bounds.center + direction.normalized * 7f;
                camera.transform.LookAt(bounds.center);
                var target = new RenderTexture(900, 900, 24);
                target.Create();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(900, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                texture.Apply();
                int version = preview.GetComponent<MountedHorseController>().rigVersion;
                string stage = version >= 5 ? "level" : version >= 4 ? "after" : "before";
                string path = System.IO.Path.Combine(folder, stage + "-" + shots.Count + ".png");
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                shots.Add(System.IO.Path.GetFullPath(path));
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { shots, vertexCount = vertices.Length, slices = Enumerable.Range(0, 10).Select(i => {
                var slice = vertices.Where(v => v.y >= i * .1f && v.y < (i + 1) * .1f).ToArray();
                return new { y = i * .1f, count = slice.Length, xMin = slice.Length == 0 ? 0 : slice.Min(v => v.x), xMax = slice.Length == 0 ? 0 : slice.Max(v => v.x), zMin = slice.Length == 0 ? 0 : slice.Min(v => v.z), zMax = slice.Length == 0 ? 0 : slice.Max(v => v.z) };
            }).ToArray() });
        }
        finally { UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(preview); }
    }

    public static string InspectSkinScale()
    {
        var player = UnityEngine.Object.FindAnyObjectByType<MountedHorseController>();
        var skin = player.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name == "Horse_AnimatedMesh");
        var filter = player.GetComponentsInChildren<MeshFilter>(true).First(f => AssetDatabase.GetAssetPath(f.sharedMesh) == "Assets/Art/Models/base.obj");
        Mesh unscaled = new Mesh(), scaled = new Mesh();
        try {
            skin.BakeMesh(unscaled, false); skin.BakeMesh(scaled, true);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new {
                sourceBounds = filter.sharedMesh.bounds.ToString("F4"),
                sourceWorld = filter.GetComponent<Renderer>().bounds.ToString("F4"),
                skinWorld = skin.bounds.ToString("F4"),
                skinScale = skin.transform.lossyScale.ToString("F4"),
                boneScale = skin.bones[0].lossyScale.ToString("F4"),
                unscaledBounds = unscaled.bounds.ToString("F4"), scaledBounds = scaled.bounds.ToString("F4"),
                original = filter.sharedMesh.vertices[0].ToString("F4"),
                unscaled = unscaled.vertices[0].ToString("F4"), scaled = scaled.vertices[0].ToString("F4")
            });
        }
        finally { UnityEngine.Object.DestroyImmediate(unscaled); UnityEngine.Object.DestroyImmediate(scaled); }
    }

    public static async Task<string> VerifyWalkAndCapture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode before verifying the gait.");
        var player = UnityEngine.Object.FindAnyObjectByType<MountedHorseController>();
        var skin = player.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name == "Horse_AnimatedMesh");
        var source = player.GetComponentsInChildren<MeshFilter>(true).First(f => AssetDatabase.GetAssetPath(f.sharedMesh) == "Assets/Art/Models/base.obj");
        var legs = new[] { player.frontLeftLeg, player.frontRightLeg, player.rearLeftLeg, player.rearRightLeg };
        var initialRotations = legs.Select(l => l.localRotation).ToArray();
        var weights = skin.sharedMesh.boneWeights;
        var sourceVertices = source.sharedMesh.vertices;
        var velocity = typeof(MountedHorseController).GetField("planarVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        var damp = typeof(MountedHorseController).GetField("planarVelocityDamp", BindingFlags.Instance | BindingFlags.NonPublic);
        var baked = new Mesh();
        skin.BakeMesh(baked, true);
        var initial = baked.vertices;
        var displacement = new float[initial.Length];
        var angles = new float[4];
        var start = player.transform.position;
        var direction = player.transform.forward;
        var cameraObject = new GameObject("Horse gait diagnostic camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = source.GetComponent<Renderer>().bounds.size.y * .62f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .13f, .16f);
        camera.nearClipPlane = .1f; camera.farClipPlane = 25f;
        var paths = new System.Collections.Generic.List<string>();
        var target = new RenderTexture(900, 900, 24); target.Create();
        var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Temp/CLIInspection/HorseRig"));
        System.IO.Directory.CreateDirectory(folder);
        try {
            for (int frame = 0; frame < 24; frame++) {
                // Feed velocity into the real controller's Update/CharacterController
                // path; the normal gait solver drives the authored skin bones.
                velocity.SetValue(player, direction * player.walkSpeed);
                damp.SetValue(player, Vector3.zero);
                await Task.Delay(60);
                skin.BakeMesh(baked, true);
                var current = baked.vertices;
                for (int i = 0; i < current.Length; i++) displacement[i] = Mathf.Max(displacement[i], Vector3.Distance(initial[i], current[i]));
                for (int i = 0; i < 4; i++) angles[i] = Mathf.Max(angles[i], Quaternion.Angle(initialRotations[i], legs[i].localRotation));
                if (frame % 6 == 0) {
                    Bounds bounds = source.GetComponent<Renderer>().bounds;
                    camera.transform.position = bounds.center + player.transform.TransformDirection(new Vector3(1, .25f, 1)).normalized * 7f;
                    camera.transform.LookAt(bounds.center);
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var texture = new Texture2D(900, 900, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); texture.Apply();
                    string path = System.IO.Path.Combine(folder, "walk-" + paths.Count + ".png");
                    System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); paths.Add(path);
                    RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            var moved = Enumerable.Range(1, 4).Select(leg => Enumerable.Range(0, initial.Length).Count(i =>
                weights[i].boneIndex1 == leg && weights[i].weight1 + weights[i].weight2 > .6f && displacement[i] > .005f)).ToArray();
            float riderError = Enumerable.Range(0, initial.Length).Where(i => sourceVertices[i].y > 1.2f).Max(i => displacement[i]);
            float travelled = Vector3.Distance(start, player.transform.position);
            var report = new { pass = travelled > .1f && moved.All(n => n > 30) && angles.All(a => a > 5f) && riderError < .001f,
                distance = travelled, legAngles = angles, originalLegVerticesMoved = moved, riderDeformation = riderError, paths };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(report);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "walk-verification.json"), json);
            return json;
        }
        finally {
            velocity.SetValue(player, Vector3.zero); damp.SetValue(player, Vector3.zero);
            UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(cameraObject);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    public static string AuditSceneInstances()
    {
        var active = SceneManager.GetActiveScene();
        var rows = new System.Collections.Generic.List<object>();
        foreach (string scenePath in System.IO.Directory.GetFiles("Assets/Scenes/ThanhGiongWorld", "*.unity")) {
            string path = scenePath.Replace('\\', '/');
            var loaded = SceneManager.GetSceneByPath(path);
            bool opened = !loaded.IsValid() || !loaded.isLoaded;
            var scene = opened ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive) : loaded;
            try {
                foreach (var player in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MountedHorseController>(true))) {
                    bool usesOriginal = player.GetComponentsInChildren<MeshFilter>(true).Any(f => AssetDatabase.GetAssetPath(f.sharedMesh) == "Assets/Art/Models/base.obj");
                    if (!usesOriginal) continue;
                    var legs = new[] { player.frontLeftLeg, player.frontRightLeg, player.rearLeftLeg, player.rearRightLeg };
                    rows.Add(new { scene = scene.name, pass = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s => s.name == "Horse_AnimatedMesh") && legs.All(l => l != null && l.IsChildOf(player.visual)) &&
                        !new[] { "Front Left Leg", "Front Right Leg", "Rear Left Leg", "Rear Right Leg" }.Any(n => player.transform.Find(n) != null) });
                }
            }
            finally { if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }
        SceneManager.SetActiveScene(active);
        return Newtonsoft.Json.JsonConvert.SerializeObject(rows);
    }
}
