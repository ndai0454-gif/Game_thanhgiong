using System.Collections.Generic;
using System.IO;
using ThanhGiong.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThanhGiong.EditorTools
{
    public static class MeshyEnvironmentInstaller
    {
        private const string EnvArtRoot = "Assets/Art/Meshy/Environment";
        private const string EnvPrefabDir = "Assets/Prefabs/Meshy/Environment";
        private const string EnvMaterialDir = "Assets/Materials/Meshy/Environment";

        private static readonly string[] AllScenes =
        {
            "Assets/Scenes/ThanhGiongWorld/LangGiongTienTuyen.unity",
            "Assets/Scenes/ThanhGiongWorld/KinhThanhRenThep.unity",
            "Assets/Scenes/ThanhGiongWorld/PhaoDaiNgamQuanAn.unity",
            "Assets/Scenes/ThanhGiongWorld/ThungLungVuotSong.unity",
            "Assets/Scenes/ThanhGiongWorld/TranTuyenNuiSoc.unity",
            "Assets/Scenes/ThanhGiongWorld/DinhSocHoaThanh.unity",
            "Assets/Scenes/AlbionForestMap.unity"
        };

        public struct EnvSpec
        {
            public string key;
            public float targetHeight;
            public MeshyPropVfxAnimator.PropEffectKind effectKind;
            public Color vfxPrimaryColor;
            public Color vfxSecondaryColor;
            public Color lightColor;
            public float lightIntensity;
            public float lightRange;

            public EnvSpec(
                string key,
                float targetHeight,
                MeshyPropVfxAnimator.PropEffectKind effectKind,
                Color vfxPrimaryColor,
                Color vfxSecondaryColor,
                Color lightColor,
                float lightIntensity,
                float lightRange)
            {
                this.key = key;
                this.targetHeight = targetHeight;
                this.effectKind = effectKind;
                this.vfxPrimaryColor = vfxPrimaryColor;
                this.vfxSecondaryColor = vfxSecondaryColor;
                this.lightColor = lightColor;
                this.lightIntensity = lightIntensity;
                this.lightRange = lightRange;
            }
        }

        private static readonly EnvSpec[] Specs =
        {
            new EnvSpec(
                "NhaTranh_LangGiong", 5.8f,
                MeshyPropVfxAnimator.PropEffectKind.HearthLantern,
                new Color(0.85f, 0.82f, 0.76f, 0.28f),
                new Color(1.0f, 0.78f, 0.32f, 0.85f),
                new Color(1.0f, 0.72f, 0.32f, 1f), 2.2f, 10f),

            new EnvSpec(
                "SanDinh_TrongDong", 6.8f,
                MeshyPropVfxAnimator.PropEffectKind.SacredDrumShrine,
                new Color(1.0f, 0.84f, 0.28f, 0.75f),
                new Color(1.0f, 0.55f, 0.15f, 0.65f),
                new Color(1.0f, 0.80f, 0.30f, 1f), 3.2f, 15f),

            new EnvSpec(
                "LoRenThep_VanLang", 5.8f,
                MeshyPropVfxAnimator.PropEffectKind.MoltenForge,
                new Color(1.0f, 0.42f, 0.08f, 0.85f),
                new Color(1.0f, 0.82f, 0.20f, 0.95f),
                new Color(1.0f, 0.45f, 0.10f, 1f), 3.8f, 14f),

            new EnvSpec(
                "KhomTreNga_ThanThanh", 6.4f,
                MeshyPropVfxAnimator.PropEffectKind.GoldenBambooWind,
                new Color(0.95f, 0.88f, 0.32f, 0.80f),
                new Color(0.45f, 0.88f, 0.38f, 0.75f),
                new Color(0.95f, 0.88f, 0.35f, 1f), 1.4f, 7f),

            new EnvSpec(
                "CongLang_ChienLuy", 7.8f,
                MeshyPropVfxAnimator.PropEffectKind.EnemyWatchtowerFire,
                new Color(1.0f, 0.52f, 0.12f, 0.85f),
                new Color(0.35f, 0.32f, 0.28f, 0.35f),
                new Color(1.0f, 0.58f, 0.18f, 1f), 2.8f, 14f),

            new EnvSpec(
                "CoLenh_PhuDong", 6.4f,
                MeshyPropVfxAnimator.PropEffectKind.WarBannerFlutter,
                new Color(1.0f, 0.78f, 0.22f, 0.85f),
                new Color(0.95f, 0.22f, 0.15f, 0.75f),
                new Color(1.0f, 0.72f, 0.25f, 1f), 1.8f, 8f),

            new EnvSpec(
                "ThapCanh_DoanThanh", 8.2f,
                MeshyPropVfxAnimator.PropEffectKind.EnemyWatchtowerFire,
                new Color(0.95f, 0.25f, 0.08f, 0.80f),
                new Color(0.22f, 0.18f, 0.18f, 0.42f),
                new Color(1.0f, 0.32f, 0.10f, 1f), 3.0f, 13f),

            new EnvSpec(
                "LeuTrai_GiacAn", 4.8f,
                MeshyPropVfxAnimator.PropEffectKind.EnemyWatchtowerFire,
                new Color(0.95f, 0.35f, 0.10f, 0.75f),
                new Color(0.28f, 0.24f, 0.22f, 0.35f),
                new Color(1.0f, 0.38f, 0.12f, 1f), 2.0f, 9f),

            new EnvSpec(
                "CauGo_CoDai", 3.6f,
                MeshyPropVfxAnimator.PropEffectKind.RiverBridgeMist,
                new Color(0.72f, 0.92f, 0.98f, 0.32f),
                new Color(0.88f, 0.96f, 1.0f, 0.45f),
                new Color(0.65f, 0.88f, 0.95f, 1f), 1.2f, 8f),

            new EnvSpec(
                "DaNui_RungThieng", 2.2f,
                MeshyPropVfxAnimator.PropEffectKind.HearthLantern,
                new Color(0.75f, 0.92f, 0.65f, 0.25f),
                new Color(0.95f, 0.85f, 0.42f, 0.55f),
                new Color(0.85f, 0.92f, 0.55f, 1f), 0.9f, 6f),

            new EnvSpec(
                "MamLuongThuc_LangGiong", 0.48f,
                MeshyPropVfxAnimator.PropEffectKind.ResourceFeastHover,
                new Color(1.0f, 0.96f, 0.85f, 0.38f),
                new Color(1.0f, 0.85f, 0.28f, 0.90f),
                new Color(1.0f, 0.84f, 0.32f, 1f), 1.8f, 6f),

            new EnvSpec(
                "DenThieng_DinhSoc", 6.8f,
                MeshyPropVfxAnimator.PropEffectKind.CelestialAscensionAltar,
                new Color(1.0f, 0.88f, 0.35f, 0.90f),
                new Color(0.85f, 0.95f, 1.0f, 0.55f),
                new Color(1.0f, 0.86f, 0.38f, 1f), 4.2f, 20f),

            new EnvSpec(
                "MoQuangSat_PhongChau", 2.4f,
                MeshyPropVfxAnimator.PropEffectKind.MoltenForge,
                new Color(1.0f, 0.68f, 0.22f, 0.85f),
                new Color(1.0f, 0.88f, 0.45f, 0.65f),
                new Color(1.0f, 0.65f, 0.22f, 1f), 2.2f, 8f),

            new EnvSpec(
                "CotMocDa_DongSon", 3.6f,
                MeshyPropVfxAnimator.PropEffectKind.SacredDrumShrine,
                new Color(1.0f, 0.86f, 0.36f, 0.78f),
                new Color(0.75f, 0.95f, 0.82f, 0.50f),
                new Color(1.0f, 0.82f, 0.38f, 1f), 2.0f, 9f),

            new EnvSpec(
                "CayDaCoThu_LangViet", 8.6f,
                MeshyPropVfxAnimator.PropEffectKind.GoldenBambooWind,
                new Color(0.95f, 0.90f, 0.38f, 0.80f),
                new Color(0.48f, 0.92f, 0.42f, 0.75f),
                new Color(1.0f, 0.78f, 0.35f, 1f), 2.4f, 12f),

            new EnvSpec(
                "DamSenCo_VanLang", 1.35f,
                MeshyPropVfxAnimator.PropEffectKind.RiverBridgeMist,
                new Color(1.0f, 0.78f, 0.88f, 0.65f),
                new Color(0.78f, 0.96f, 0.92f, 0.45f),
                new Color(0.95f, 0.78f, 0.86f, 1f), 1.4f, 7f)
        };

        [MenuItem("Tools/Thánh Gióng/Build All 16 Meshy Zelda Prefabs & Materials Only", false, 5)]
        public static Dictionary<string, GameObject> BuildAllPrefabsAndMaterialsOnly()
        {
            EnsureDir(EnvPrefabDir);
            EnsureDir(EnvMaterialDir);
            AssetDatabase.Refresh();

            Material particleAdditiveMat = CreateOrUpdateParticleMaterial("Mat_MeshyVfx_Additive", true);
            Material particleAlphaMat = CreateOrUpdateParticleMaterial("Mat_MeshyVfx_Alpha", false);
            UpdateRiverWaterMaterial();

            Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

            foreach (EnvSpec spec in Specs)
            {
                string folder = $"{EnvArtRoot}/{spec.key}";
                string fbxPath = $"{folder}/{spec.key}.fbx";
                if (!File.Exists(fbxPath))
                {
                    Debug.LogWarning($"[MeshyEnvironmentInstaller] Missing FBX: {fbxPath}");
                    continue;
                }

                ConfigureStaticFbxImporter(fbxPath);
                Material mat = CreatePbrMaterial($"Mat_Env_{spec.key}_Meshy", $"{folder}/{spec.key}");
                GameObject prefab = BuildEnvPrefabWithVfx(spec, fbxPath, mat, particleAdditiveMat, particleAlphaMat);
                if (prefab != null)
                {
                    prefabs[spec.key] = prefab;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MeshyEnvironmentInstaller] Built {prefabs.Count}/{Specs.Length} Meshy Zelda 3D Prefabs & Materials.");
            return prefabs;
        }

        private static void UpdateRiverWaterMaterial()
        {
            const string matPath = "Assets/Materials/Meshy/Environment/Mat_DongSong_NuocBiec.mat";
            Shader waterShader = Shader.Find("ThanhGiong/Adventure/ZeldaStylizedWater")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(waterShader) { name = "Mat_DongSong_NuocBiec" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = waterShader;
            }
            if (mat.HasProperty("_ShallowColor")) mat.SetColor("_ShallowColor", new Color(0.18f, 0.74f, 0.68f, 1f));
            if (mat.HasProperty("_DeepColor")) mat.SetColor("_DeepColor", new Color(0.08f, 0.40f, 0.58f, 1f));
            if (mat.HasProperty("_FoamColor")) mat.SetColor("_FoamColor", new Color(0.95f, 0.99f, 1.0f, 1f));
            if (mat.HasProperty("_CausticColor")) mat.SetColor("_CausticColor", new Color(0.52f, 0.94f, 0.86f, 1f));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        [MenuItem("Tools/Thánh Gióng/Install & Wire All 12 Meshy 3D Map Props + VFX Across All Scenes", false, 6)]
        public static string InstallAllEnvironmentAndVfx()
        {
            Dictionary<string, GameObject> prefabs = BuildAllPrefabsAndMaterialsOnly();
            Material particleAdditiveMat = CreateOrUpdateParticleMaterial("Mat_MeshyVfx_Additive", true);
            Material particleAlphaMat = CreateOrUpdateParticleMaterial("Mat_MeshyVfx_Alpha", false);

            int totalSpawned = PopulateAllScenes(prefabs, particleAdditiveMat, particleAlphaMat);
            AssetDatabase.SaveAssets();

            string summary = $"[MeshyEnvironmentInstaller] Built {prefabs.Count}/{Specs.Length} Meshy 3D Environment Prefabs with VFX & populated {totalSpawned} instances across {AllScenes.Length} scenes.";
            Debug.Log(summary);
            return summary;
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static void ConfigureStaticFbxImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            bool changed = false;
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.animationType != ModelImporterAnimationType.None)
            {
                importer.animationType = ModelImporterAnimationType.None;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        private static Material CreatePbrMaterial(string matName, string texPrefix)
        {
            string matPath = $"{EnvMaterialDir}/{matName}.mat";
            Shader shader = Shader.Find("ThanhGiong/Adventure/Cel Rim Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D albedo = LoadTexture(texPrefix + "_Albedo.png", false);
            Texture2D normal = LoadTexture(texPrefix + "_Normal.png", true);
            Texture2D metallic = LoadTexture(texPrefix + "_Metallic.png", false);

            mat.SetColor("_BaseColor", new Color(1.02f, 1.0f, 0.96f, 1f));
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.mainTexture = albedo;
            }
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.SetFloat("_Metallic", metallic != null ? 0.28f : 0.14f);
            mat.SetFloat("_Smoothness", 0.42f);

            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_ShadowTint")) mat.SetColor("_ShadowTint", new Color(0.56f, 0.63f, 0.72f, 1f));
            if (mat.HasProperty("_BandThreshold")) mat.SetFloat("_BandThreshold", 0.42f);
            if (mat.HasProperty("_BandSoftness")) mat.SetFloat("_BandSoftness", 0.16f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(1.0f, 0.88f, 0.54f, 1f));
            if (mat.HasProperty("_RimIntensity")) mat.SetFloat("_RimIntensity", 0.22f);
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", 3.4f);
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D LoadTexture(string path, bool isNormal)
        {
            if (!File.Exists(path)) return null;
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                bool dirty = false;
                if (isNormal && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
                if (ti.maxTextureSize < 4096)
                {
                    ti.maxTextureSize = 4096;
                    dirty = true;
                }
                if (dirty) ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateOrUpdateParticleMaterial(string name, bool additive)
        {
            string matPath = $"{EnvMaterialDir}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D tex = Resources.Load<Texture2D>(additive ? "GodotPortParticles/spark_01" : "GodotPortParticles/smoke_01");
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
            }

            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", additive ? 2f : 0f); // 2 = Additive, 0 = Alpha
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = 3000;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject BuildEnvPrefabWithVfx(
            EnvSpec spec,
            string fbxPath,
            Material mat,
            Material additiveMat,
            Material alphaMat)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null) return null;

            GameObject root = new GameObject($"Meshy_{spec.key}");
            GameObject modelHolder = new GameObject("MeshyModel");
            modelHolder.transform.SetParent(root.transform, false);

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            inst.name = spec.key + "_Mesh";
            inst.transform.SetParent(modelHolder.transform, false);

            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            // Compute world bounds and normalize height + bottom pivot
            Bounds b = ComputeBounds(inst);
            if (b.size.y > 0.001f)
            {
                float scaleFactor = spec.targetHeight / b.size.y;
                modelHolder.transform.localScale = Vector3.one * scaleFactor;
                Bounds scaledBounds = ComputeBounds(modelHolder);
                modelHolder.transform.localPosition = new Vector3(
                    -scaledBounds.center.x,
                    -scaledBounds.min.y,
                    -scaledBounds.center.z);
            }

            // Add dynamic light + VFX
            GameObject vfxRoot = new GameObject("Meshy_VFX");
            vfxRoot.transform.SetParent(root.transform, false);

            GameObject lightGo = new GameObject("AuraLight");
            lightGo.transform.SetParent(vfxRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, spec.targetHeight * 0.48f, 0f);
            Light ptLight = lightGo.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.color = spec.lightColor;
            ptLight.intensity = spec.lightIntensity;
            ptLight.range = spec.lightRange;
            ptLight.shadows = LightShadows.None;

            // Primary emitter (sparks / embers / leaves / divine motes)
            CreatePropParticleEmitter(
                vfxRoot.transform,
                "VFX_PrimarySparksOrMotes",
                spec,
                true,
                additiveMat);

            // Secondary emitter (smoke / mist / steam / aura)
            CreatePropParticleEmitter(
                vfxRoot.transform,
                "VFX_SecondaryMistOrSmoke",
                spec,
                false,
                alphaMat);

            MeshyPropVfxAnimator anim = root.AddComponent<MeshyPropVfxAnimator>();
            anim.effectKind = spec.effectKind;
            anim.propLight = ptLight;
            anim.animatedMeshRoot = modelHolder.transform;
            anim.baseLightIntensity = spec.lightIntensity;

            string prefabPath = $"{EnvPrefabDir}/Meshy_{spec.key}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void CreatePropParticleEmitter(
            Transform parent,
            string name,
            EnvSpec spec,
            bool isPrimary,
            Material particleMat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            float yOffset = isPrimary ? spec.targetHeight * 0.45f : spec.targetHeight * 0.72f;
            if (spec.effectKind == MeshyPropVfxAnimator.PropEffectKind.RiverBridgeMist)
            {
                yOffset = 0.35f;
            }
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            if (isPrimary)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.startColor = spec.vfxPrimaryColor;
                main.maxParticles = 35;

                var emission = ps.emission;
                emission.rateOverTime = spec.effectKind == MeshyPropVfxAnimator.PropEffectKind.MoltenForge ? 14f
                    : spec.effectKind == MeshyPropVfxAnimator.PropEffectKind.CelestialAscensionAltar ? 18f
                    : 6f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = Mathf.Clamp(spec.targetHeight * 0.28f, 0.4f, 2.2f);
            }
            else
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.10f);
                main.startColor = spec.vfxSecondaryColor;
                main.maxParticles = 24;

                var emission = ps.emission;
                emission.rateOverTime = spec.effectKind == MeshyPropVfxAnimator.PropEffectKind.MoltenForge ? 6f
                    : spec.effectKind == MeshyPropVfxAnimator.PropEffectKind.EnemyWatchtowerFire ? 5f
                    : 3f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 18f;
                shape.radius = Mathf.Clamp(spec.targetHeight * 0.18f, 0.25f, 1.4f);
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            Color baseCol = isPrimary ? spec.vfxPrimaryColor : spec.vfxSecondaryColor;
            grad.SetKeys(
                new[] { new GradientColorKey(baseCol, 0f), new GradientColorKey(baseCol, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(baseCol.a, 0.25f),
                    new GradientAlphaKey(baseCol.a * 0.8f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            if (particleMat != null)
            {
                psr.sharedMaterial = particleMat;
            }
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                b.Encapsulate(rends[i].bounds);
            }
            return b;
        }

        private static int PopulateAllScenes(
            Dictionary<string, GameObject> prefabs,
            Material additiveMat,
            Material alphaMat)
        {
            if (prefabs.Count == 0) return 0;

            string originalScene = SceneManager.GetActiveScene().path;
            int totalSpawned = 0;

            foreach (string scenePath in AllScenes)
            {
                if (!File.Exists(scenePath)) continue;

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                GameObject existingContainer = GameObject.Find("MESHY 3D WORLD ENVIRONMENT & VFX");
                if (existingContainer != null)
                {
                    Object.DestroyImmediate(existingContainer);
                }

                GameObject container = new GameObject("MESHY 3D WORLD ENVIRONMENT & VFX");
                SceneManager.MoveGameObjectToScene(container, scene);

                int sceneCount = 0;

                // 1. Replace Godot Legend Environment props while preserving hierarchy & colliders
                GameObject godotEnv = GameObject.Find("Godot Legend Environment");
                if (godotEnv != null)
                {
                    foreach (Transform child in godotEnv.transform)
                    {
                        string cn = child.name.ToLowerInvariant();
                        if (cn.StartsWith("plant_bush") || cn.StartsWith("pot_large") || cn.StartsWith("crops_wheat"))
                        {
                            HideLegacyRenderers(child);
                            continue;
                        }

                        string mappedKey = MapGodotNodeToMeshyKey(child.name, scene.name);
                        if (string.IsNullOrEmpty(mappedKey) || !prefabs.TryGetValue(mappedKey, out GameObject prefab))
                        {
                            continue;
                        }

                        // Compute bounds center and ground Y before hiding legacy renderers
                        Vector3 spawnPos = GetGroundSpawnPosition(child);
                        HideLegacyRenderers(child);

                        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        inst.name = $"Meshy_{child.name}_{mappedKey}";
                        inst.transform.SetParent(container.transform, false);
                        inst.transform.position = spawnPos;
                        inst.transform.rotation = child.rotation;
                        sceneCount++;
                    }
                }

                // 2. Populate ARCHITECTURE & FOLIAGE & GAMEPLAY root nodes
                foreach (GameObject rootGo in scene.GetRootGameObjects())
                {
                    if (rootGo.name.StartsWith("THANH GIONG HERO V2") || rootGo.name.StartsWith("UNDERSTORY INSPIRED"))
                    {
                        HideLegacyRenderers(rootGo.transform);
                    }
                    else if (rootGo.name.StartsWith("Stylized Foliage Clusters"))
                    {
                        foreach (Transform cluster in rootGo.transform)
                        {
                            if (cluster.name.Contains("VFX")) continue;
                            Vector3 spawnPos = GetGroundSpawnPosition(cluster);
                            HideLegacyRenderers(cluster);

                            if (cluster.name.StartsWith("FoliageCluster")) continue;

                            if (prefabs.TryGetValue("KhomTreNga_ThanThanh", out GameObject folPrefab))
                            {
                                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(folPrefab, scene);
                                inst.name = $"MeshyFoliage_{cluster.name}_KhomTreNga";
                                inst.transform.SetParent(container.transform, false);
                                inst.transform.position = spawnPos;
                                inst.transform.rotation = cluster.rotation;
                                sceneCount++;
                            }
                        }
                    }
                    else if (rootGo.name.StartsWith("ARCHITECTURE"))
                    {
                        foreach (Transform child in rootGo.transform)
                        {
                            string cn = child.name.ToLowerInvariant();
                            if (cn.StartsWith("tree_") || cn.StartsWith("bush_") || cn.StartsWith("rock_1") || cn.StartsWith("rock_2") || cn.StartsWith("bậc núi thiêng"))
                            {
                                HideLegacyRenderers(child);
                            }

                            string mappedKey = MapArchitectureNodeToMeshyKey(child.name, scene.name);
                            if (string.IsNullOrEmpty(mappedKey) || !prefabs.TryGetValue(mappedKey, out GameObject prefab))
                            {
                                continue;
                            }

                            Vector3 spawnPos = GetGroundSpawnPosition(child);
                            HideLegacyRenderers(child);

                            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                            inst.name = $"MeshyArch_{child.name}_{mappedKey}";
                            inst.transform.SetParent(container.transform, false);
                            inst.transform.position = spawnPos;
                            inst.transform.rotation = child.rotation;
                            sceneCount++;
                        }
                    }
                    else if (rootGo.name.StartsWith("GAMEPLAY - LUONG THUC"))
                    {
                        if (prefabs.TryGetValue("MamLuongThuc_LangGiong", out GameObject foodPrefab))
                        {
                            foreach (Transform item in rootGo.transform)
                            {
                                Transform vis = item.Find("Visual");
                                if (vis != null)
                                {
                                    HideLegacyRenderers(vis);
                                    Transform oldMeshy = vis.Find("Meshy_MamLuongThuc");
                                    if (oldMeshy != null) Object.DestroyImmediate(oldMeshy.gameObject);

                                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(foodPrefab, scene);
                                    inst.name = "Meshy_MamLuongThuc";
                                    inst.transform.SetParent(vis, false);
                                    inst.transform.localPosition = new Vector3(0f, -0.45f, 0f);
                                    inst.transform.localRotation = Quaternion.identity;
                                    sceneCount++;
                                }
                            }
                        }
                    }
                    else if (rootGo.name.StartsWith("GAMEPLAY - TRE NGA"))
                    {
                        if (prefabs.TryGetValue("KhomTreNga_ThanThanh", out GameObject bambooPrefab))
                        {
                            foreach (Transform item in rootGo.transform)
                            {
                                HideLegacyRenderers(item);
                                Transform oldMeshy = item.Find("Meshy_KhomTreNga");
                                if (oldMeshy != null) Object.DestroyImmediate(oldMeshy.gameObject);

                                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(bambooPrefab, scene);
                                inst.name = "Meshy_KhomTreNga";
                                inst.transform.SetParent(item, false);
                                inst.transform.localPosition = Vector3.zero;
                                inst.transform.localRotation = Quaternion.identity;
                                sceneCount++;
                            }
                        }
                    }
                }

                // 3. Create Regional Map-Wide Atmosphere VFX System
                CreateRegionalAtmosphereVfx(container.transform, scene.name, additiveMat, alphaMat);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                totalSpawned += sceneCount;
                Debug.Log($"[MeshyEnvironmentInstaller] Scene '{scene.name}': spawned {sceneCount} Meshy 3D map assets + Regional Atmosphere VFX.");
            }

            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
            {
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }

            return totalSpawned;
        }

        private static Vector3 GetGroundSpawnPosition(Transform node)
        {
            Renderer[] rends = node.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                {
                    b.Encapsulate(rends[i].bounds);
                }
                if (b.size.sqrMagnitude > 0.01f)
                {
                    return new Vector3(b.center.x, Mathf.Max(0f, b.min.y), b.center.z);
                }
            }
            if (node.childCount > 0)
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < node.childCount; i++)
                {
                    sum += node.GetChild(i).position;
                }
                Vector3 avg = sum / node.childCount;
                return new Vector3(avg.x, Mathf.Max(0f, Mathf.Min(node.position.y, avg.y)), avg.z);
            }
            return new Vector3(node.position.x, Mathf.Max(0f, node.position.y), node.position.z);
        }

        private static void HideLegacyRenderers(Transform root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null) continue;
                r.enabled = false;
                r.forceRenderingOff = true;
            }
        }

        private static string MapGodotNodeToMeshyKey(string name, string sceneName)
        {
            string n = name.ToLowerInvariant();
            if (n.StartsWith("nhatranh")) return "NhaTranh_LangGiong";
            if (n.StartsWith("sandinhvatrongdong")) return "SanDinh_TrongDong";
            if (n.StartsWith("loren")) return "LoRenThep_VanLang";
            if (n.StartsWith("luytre") || n.StartsWith("trenga") || n.StartsWith("tree_default")) return "KhomTreNga_ThanThanh";
            if (n.StartsWith("congtraigiacan") || n.StartsWith("chienluynuisoc")) return "CongLang_ChienLuy";
            if (n.StartsWith("cophudong") || n.StartsWith("cotraigiac")) return "CoLenh_PhuDong";
            if (n.StartsWith("tent_detailed")) return "LeuTrai_GiacAn";
            if (n.StartsWith("caugoquasong")) return "CauGo_CoDai";
            if (n.StartsWith("stone_large")) return "DaNui_RungThieng";
            if (n.StartsWith("dinhsoc")) return "DenThieng_DinhSoc";
            return null;
        }

        private static string MapArchitectureNodeToMeshyKey(string name, string sceneName)
        {
            string n = name.ToLowerInvariant();
            if (n.StartsWith("building_home") || n.StartsWith("building_tavern")) return "NhaTranh_LangGiong";
            if (n.StartsWith("building_market") || n.StartsWith("building_castle_blue")) return "SanDinh_TrongDong";
            if (n.StartsWith("building_blacksmith") || n.StartsWith("building_lumbermill")) return "LoRenThep_VanLang";
            if (n.StartsWith("cổng xuất quân") || n.StartsWith("wall_straight_gate")) return "CongLang_ChienLuy";
            if (n.StartsWith("building_tower") || n.StartsWith("building_barracks") || n.StartsWith("building_archeryrange") || n.StartsWith("building_castle_red")) return "ThapCanh_DoanThanh";
            if (n.StartsWith("tent")) return "LeuTrai_GiacAn";
            if (n.StartsWith("building_bridge") || n == "bridge") return "CauGo_CoDai";
            if (n.StartsWith("building_church")) return "DenThieng_DinhSoc";
            if (n == "flag") return "CoLenh_PhuDong";
            if (n.StartsWith("rocks-high") || n.StartsWith("rock_3_")) return "DaNui_RungThieng";
            return null;
        }

        private static void CreateRegionalAtmosphereVfx(
            Transform parent,
            string sceneName,
            Material additiveMat,
            Material alphaMat)
        {
            GameObject atmoRoot = new GameObject("Meshy_RegionalAtmosphere_VFX");
            atmoRoot.transform.SetParent(parent, false);
            atmoRoot.transform.position = new Vector3(0f, 3.5f, 0f);

            Color primaryCol = new Color(1.0f, 0.88f, 0.35f, 0.75f);
            Color mistCol = new Color(0.85f, 0.92f, 0.78f, 0.16f);

            string sn = sceneName.ToLowerInvariant();
            if (sn.Contains("kinhthanh") || sn.Contains("trantuyen"))
            {
                primaryCol = new Color(1.0f, 0.45f, 0.12f, 0.85f);
                mistCol = new Color(0.35f, 0.28f, 0.25f, 0.20f);
            }
            else if (sn.Contains("phaodai"))
            {
                primaryCol = new Color(0.95f, 0.28f, 0.15f, 0.80f);
                mistCol = new Color(0.25f, 0.32f, 0.38f, 0.22f);
            }
            else if (sn.Contains("thunglung"))
            {
                primaryCol = new Color(0.65f, 0.92f, 1.0f, 0.75f);
                mistCol = new Color(0.75f, 0.90f, 0.96f, 0.22f);
            }
            else if (sn.Contains("dinhsoc"))
            {
                primaryCol = new Color(1.0f, 0.92f, 0.42f, 0.92f);
                mistCol = new Color(0.96f, 0.94f, 0.82f, 0.26f);
            }

            // Wide-area drifting embers / fireflies / divine motes
            GameObject motesGo = new GameObject("MapWide_SpiritMotesAndEmbers");
            motesGo.transform.SetParent(atmoRoot.transform, false);
            ParticleSystem motesPs = motesGo.AddComponent<ParticleSystem>();
            var mMain = motesPs.main;
            mMain.loop = true;
            mMain.playOnAwake = true;
            mMain.simulationSpace = ParticleSystemSimulationSpace.World;
            mMain.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            mMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            mMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            mMain.startColor = primaryCol;
            mMain.maxParticles = 160;
            var mEm = motesPs.emission;
            mEm.rateOverTime = 28f;
            var mShape = motesPs.shape;
            mShape.shapeType = ParticleSystemShapeType.Box;
            mShape.scale = new Vector3(68f, 8f, 68f);
            if (additiveMat != null)
            {
                motesGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = additiveMat;
            }
        }
    }
}
