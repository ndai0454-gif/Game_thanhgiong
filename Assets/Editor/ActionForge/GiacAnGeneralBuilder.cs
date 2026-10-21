using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThanhGiong.EditorTools
{
    /// <summary>Builds isolated assets; never installs the new general into an existing scene.</summary>
    public static class GiacAnGeneralBuilder
    {
        public const string SourcePath = "Assets/Art/Meshy/GiacAnGeneral/GiacAnGeneral_ActionForge.fbx";
        public const string TextureFolder = "Assets/Art/Meshy/GiacAnGeneral/Textures";
        public const string OutputFolder = "Assets/Animations/ActionForge/GiacAnGeneral";
        public const string ControllerPath = OutputFolder + "/GiacAnGeneral.controller";
        public const string MaterialPath = "Assets/Materials/Enemies/GiacAnGeneral/GiacAnGeneral_PBR.mat";
        public const string PrefabPath = "Assets/Prefabs/Enemies/GiacAnGeneral_Meshy.prefab";
        public const string ScenePath = "Assets/Scenes/Showcase/GiacAnGeneral_Showcase.unity";

        [MenuItem("Tools/Thánh Gióng/ActionForge/Build Giac An General")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building the general.");
            if (!File.Exists(SourcePath)) throw new FileNotFoundException("Missing ActionForge model export.", SourcePath);
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
                throw new InvalidOperationException("Close the generated showcase before rebuilding it.");
            foreach (string name in new[] { "base_color", "metallic", "roughness", "normal" })
                if (!File.Exists(TextureFolder + "/" + name + ".png"))
                    throw new FileNotFoundException("Missing original PBR texture: " + name);

            EnsureFolder(OutputFolder);
            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            ConfigureModelImport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new InvalidOperationException("Unity could not import the source FBX.");
            var clips = ResolveClips();
            ValidateBindings(source, clips);
            var material = BuildMaterial();
            var controller = BuildController(clips);
            BuildPrefab(source, controller, material, clips["sword_idle"]);
            BuildScene();
            AssetDatabase.SaveAssets();
            return Verify();
        }

        private static void ConfigureModelImport()
        {
            var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Source asset is not a ModelImporter.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.isReadable = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            // Preserve the exported skeleton paths and the full cape/armour animation curves.
            importer.optimizeGameObjects = false;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string name = FindStateName(clip.name);
                clip.loopTime = name == "sword_idle" || name == "sword_block" ||
                    name == "walk_loop" || name == "jog_fwd_loop" || name == "sprint_loop";
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static string FindStateName(string name)
        {
            if (name.EndsWith("_fit", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return GiacAnGeneralShowcase.StateNames.FirstOrDefault(state =>
                name.Equals(state, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("|" + state, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(":" + state, StringComparison.OrdinalIgnoreCase)) ?? name;
        }

        private static Dictionary<string, AnimationClip> ResolveClips()
        {
            var available = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview", StringComparison.Ordinal)).ToArray();
            var result = new Dictionary<string, AnimationClip>();
            foreach (string name in GiacAnGeneralShowcase.StateNames)
            {
                var matches = available.Where(clip => FindStateName(clip.name) == name).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Expected one ActionForge clip " + name +
                    "; found " + matches.Length + ". Available: " + string.Join(", ", available.Select(c => c.name)));
                if (matches[0].length <= 0f) throw new InvalidOperationException("Empty ActionForge clip: " + name);
                result[name] = matches[0];
            }
            return result;
        }

        private static void ValidateBindings(GameObject source, Dictionary<string, AnimationClip> clips)
        {
            foreach (var entry in clips)
            {
                var curves = AnimationUtility.GetCurveBindings(entry.Value).Where(b => b.type == typeof(Transform)).ToArray();
                if (curves.Length == 0) throw new InvalidOperationException("Clip has no Generic transform curves: " + entry.Key);
                var missing = curves.Select(b => b.path).Distinct().Where(path =>
                    !string.IsNullOrEmpty(path) && source.transform.Find(path) == null).ToArray();
                if (missing.Length > 0) throw new InvalidOperationException("Unbound skeleton paths in " + entry.Key +
                    ": " + string.Join(", ", missing));
            }
        }

        private static void ConfigureTexture(string path, bool srgb, bool normal = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture has not imported: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial()
        {
            ConfigureTexture(TextureFolder + "/base_color.png", true);
            ConfigureTexture(TextureFolder + "/metallic.png", false);
            ConfigureTexture(TextureFolder + "/roughness.png", false);
            ConfigureTexture(TextureFolder + "/normal.png", false, true);
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/metallic.png");
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/roughness.png");
            string packedPath = Path.GetDirectoryName(MaterialPath).Replace('\\', '/') + "/MetallicSmoothness.png";
            PackMetallicSmoothness(metallic, roughness, packedPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "GiacAnGeneral_PBR" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/base_color.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/normal.png"));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void PackMetallicSmoothness(Texture2D metallic, Texture2D roughness, string path)
        {
            int width = Mathf.Max(metallic.width, roughness.width);
            int height = Mathf.Max(metallic.height, roughness.height);
            Texture2D m = null, r = null, packed = null;
            try
            {
                m = ReadLinearTexture(metallic, width, height);
                r = ReadLinearTexture(roughness, width, height);
                var metalPixels = m.GetPixels32();
                var roughPixels = r.GetPixels32();
                for (int i = 0; i < metalPixels.Length; i++)
                    metalPixels[i] = new Color32(metalPixels[i].r, 0, 0, (byte)(255 - roughPixels[i].r));
                packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(metalPixels);
                packed.Apply(false, false);
                File.WriteAllBytes(path, packed.EncodeToPNG());
            }
            finally
            {
                if (m != null) Object.DestroyImmediate(m);
                if (r != null) Object.DestroyImmediate(r);
                if (packed != null) Object.DestroyImmediate(packed);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(path, false);
        }

        private static Texture2D ReadLinearTexture(Texture source, int width, int height)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readable.Apply(false, false);
                return readable;
            }
            catch
            {
                if (readable != null) Object.DestroyImmediate(readable);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            for (int i = 0; i < GiacAnGeneralShowcase.StateNames.Length; i++)
            {
                string name = GiacAnGeneralShowcase.StateNames[i];
                var state = machine.AddState(name, new Vector3(260 + i % 3 * 230, 70 + i / 3 * 90));
                state.motion = clips[name];
                state.writeDefaultValues = true;
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
                var transition = machine.AddAnyStateTransition(state);
                transition.hasExitTime = false;
                transition.duration = 0.08f;
                transition.canTransitionToSelf = true;
                transition.AddCondition(AnimatorConditionMode.If, 0f, name);
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Bounds BakedBounds(GameObject model)
        {
            bool hasVertex = false;
            var bounds = new Bounds();
            var baked = new Mesh();
            try
            {
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    // This FBX has a large imported unit scale. Including scale in the bake
                    // compensates the skinning matrices before TransformPoint converts to world.
                    renderer.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        Vector3 world = renderer.transform.TransformPoint(vertex);
                        if (!hasVertex) { bounds = new Bounds(world, Vector3.zero); hasVertex = true; }
                        else bounds.Encapsulate(world);
                    }
                    baked.Clear();
                }
            }
            finally { Object.DestroyImmediate(baked); }
            if (!hasVertex) throw new InvalidOperationException("Model has no posed skinned vertices.");
            return bounds;
        }

        private static void BuildPrefab(GameObject source, RuntimeAnimatorController controller, Material material, AnimationClip idle)
        {
            var root = new GameObject("GiacAnGeneral_Meshy");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "GiacAnGeneral_Visual";
                model.transform.SetParent(root.transform, false);
                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<Avatar>().FirstOrDefault();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                }
                // Measure the rest mesh, not a combat stance, before setting a consistent boss height.
                var bounds = RendererBounds(model);
                if (bounds.size.y < 0.01f) throw new InvalidOperationException("Model bounds are invalid.");
                model.transform.localScale *= 2.15f / bounds.size.y;
                // Renderer bounds describe the rest mesh and do not put animated boots on the floor.
                // Sample the actual idle vertices, then restore rest transforms before saving.
                var transforms = model.GetComponentsInChildren<Transform>(true);
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                var scales = transforms.Select(t => t.localScale).ToArray();
                Vector3 posedScale = model.transform.localScale;
                try
                {
                    idle.SampleAnimation(model, Mathf.Min(0.3f, idle.length));
                    bounds = BakedBounds(model);
                    if (bounds.size.y < 0.01f) throw new InvalidOperationException("Idle mesh bounds are invalid.");
                    model.transform.localScale *= 2.15f / bounds.size.y;
                    posedScale = model.transform.localScale;
                    bounds = BakedBounds(model);
                }
                finally
                {
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        transforms[i].localPosition = positions[i];
                        transforms[i].localRotation = rotations[i];
                        transforms[i].localScale = scales[i];
                    }
                }
                model.transform.localScale = posedScale;
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildScene()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var general = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var controls = new GameObject("Animation Preview Controls").AddComponent<GiacAnGeneralShowcase>();
                controls.TargetAnimator = general.GetComponentInChildren<Animator>();
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Showcase Ground";
                floor.transform.localScale = Vector3.one * 1.2f;
                Object.DestroyImmediate(floor.GetComponent<Collider>());
                string floorPath = Path.GetDirectoryName(MaterialPath).Replace('\\', '/') + "/ShowcaseGround.mat";
                var floorMat = AssetDatabase.LoadAssetAtPath<Material>(floorPath);
                if (floorMat == null)
                {
                    floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(floorMat, floorPath);
                }
                floorMat.SetColor("_BaseColor", new Color(0.13f, 0.14f, 0.17f));
                floorMat.SetFloat("_Smoothness", 0.2f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMat;
                EditorUtility.SetDirty(floorMat);
                var camera = new GameObject("Showcase Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.045f, 0.055f, 0.08f);
                camera.fieldOfView = 35f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 40f;
                camera.transform.position = new Vector3(2.6f, 1.8f, 4.2f);
                camera.transform.LookAt(new Vector3(0f, 1.05f, 0f));
                camera.gameObject.AddComponent<AudioListener>();
                AddLight("Key Light", new Vector3(35f, -35f, 0f), new Color(1f, 0.97f, 0.92f), 2.2f);
                AddLight("Cool Fill", new Vector3(45f, 120f, 0f), new Color(0.6f, 0.75f, 1f), 1.3f);
                AddLight("Red Rim", new Vector3(20f, 185f, 0f), new Color(1f, 0.2f, 0.16f), 0.6f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.3f, 0.34f, 0.42f);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Could not save the generated showcase scene.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AddLight(string name, Vector3 rotation, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(rotation);
            light.shadows = name == "Key Light" ? LightShadows.Soft : LightShadows.None;
        }

        public static string Verify()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (prefab == null || controller == null) throw new InvalidOperationException("Generated assets are missing.");
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || animator.avatar.isHuman ||
                animator.applyRootMotion || animator.runtimeAnimatorController != controller)
                throw new InvalidOperationException("Expected a valid Generic avatar, generated controller and disabled root motion.");
            var states = controller.layers[0].stateMachine.states;
            foreach (string name in GiacAnGeneralShowcase.StateNames)
                if (states.Count(s => s.state.name == name && s.state.motion is AnimationClip) != 1)
                    throw new InvalidOperationException("Controller state is missing or duplicated: " + name);
            ValidateBindings(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath), ResolveClips());
            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length == 0 || skinned.Any(r => r.sharedMesh == null || r.bones.Length == 0 || r.bones.Any(b => b == null)))
                throw new InvalidOperationException("Model has missing skinned meshes or skeleton bindings.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Showcase scene is missing.");
            int vertices = skinned.Sum(r => r.sharedMesh.vertexCount);
            return "GiacAnGeneral verified: " + states.Length + " ActionForge states, " + skinned.Length +
                " skinned renderer(s), " + vertices + " vertices; Generic avatar; root motion disabled.\n" +
                "Prefab: " + PrefabPath + "\nShowcase: " + ScenePath + "\nController: " + ControllerPath;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
