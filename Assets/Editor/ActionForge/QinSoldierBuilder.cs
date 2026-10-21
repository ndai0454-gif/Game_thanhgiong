using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThanhGiong.EditorTools
{
    /// <summary>
    /// Builds the Meshy AI + ActionForge Qin Soldier (Lính Tần) character, PBR materials,
    /// 3D Qin weapons (Trường Thương Tần, Thanh Đồng Kiếm Tần, Khiên Sư Tử Tần, Cung Đồng Tần),
    /// exact-rig ActionForge animations (including sword_regular_a_rec & locomotion),
    /// AnimatorControllers, ActionForgeEnemyMotionSet, and reusable enemy prefabs.
    /// </summary>
    public static class QinSoldierBuilder
    {
        public const string ModelPath = "Assets/Art/Meshy/QinSoldier/QinSoldier_Rigged.fbx";
        public const string MeshyWalkPath = "Assets/Art/Meshy/QinSoldier/QinSoldier_Meshy_Walk.fbx";
        public const string MeshyRunPath = "Assets/Art/Meshy/QinSoldier/QinSoldier_Meshy_Run.fbx";
        public const string TextureFolder = "Assets/Art/Meshy/QinSoldier/Textures";
        public const string WeaponMeshFolder = "Assets/Art/Meshy/QinSoldier/Weapons";
        public const string MaterialFolder = "Assets/Materials/Enemies/QinSoldier";
        public const string BodyMaterialPath = MaterialFolder + "/QinSoldier_PBR.mat";
        public const string ShieldMaterialPath = MaterialFolder + "/QinSoldier_Shield_PBR.mat";
        public const string BronzeMaterialPath = MaterialFolder + "/QinSoldier_WeaponBronze.mat";
        public const string TasselMaterialPath = MaterialFolder + "/QinSoldier_RedTassel.mat";

        public const string OutputAnimFolder = "Assets/Animations/ActionForge/Enemies/QinSoldier";
        public const string StandaloneClipsFolder = "Assets/Animations/ActionForge/QinSoldier/Clips";
        public const string MotionSetPath = "Assets/Resources/ActionForge/EnemyMotionSets/QinSoldier.asset";
        public const string BarbarianMotionSetPath = "Assets/Resources/ActionForge/EnemyMotionSets/Barbarian.asset";

        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/QinSoldier_Meshy.prefab";
        public const string GameplayPrefabPath = "Assets/Prefabs/Enemies/QinSoldier_Enemy.prefab";
        public const string LegacySoldierPrefabPath = "Assets/Prefabs/Enemies/KayKit_Barbarian_Soldier.prefab";

        private static readonly string[] TargetBoneNames =
        {
            "Hips", "Spine02", "Spine01", "Spine", "neck", "Head",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
            "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase"
        };

        private static readonly string[] PhaseNames = { "Windup", "Strike", "Stuck", "Recovery", "Hit", "Death" };

        public sealed class QinRoleSpec
        {
            public readonly string Name;
            public readonly string Field;
            public readonly string SourceFbx;
            public readonly string AttackClip;
            public readonly string IdleClip;
            public readonly float WindupStart;
            public readonly float SourceWindupEnd;
            public readonly float SourceContact;
            public readonly float SourceStrikeEnd;
            public readonly float StrikeContact;

            public QinRoleSpec(
                string name,
                string field,
                string sourceFbx,
                string attackClip,
                string idleClip,
                float windupStart,
                float windupEnd,
                float contact,
                float strikeEnd,
                float strikeContact)
            {
                Name = name;
                Field = field;
                SourceFbx = sourceFbx;
                AttackClip = attackClip;
                IdleClip = idleClip;
                WindupStart = windupStart;
                SourceWindupEnd = windupEnd;
                SourceContact = contact;
                SourceStrikeEnd = strikeEnd;
                StrikeContact = strikeContact;
            }
        }

        // Spearman and Raider use ActionForge sword_regular_a_rec (sword_regular_a_fit + recovery to sword_idle_fit).
        public static readonly QinRoleSpec[] Roles =
        {
            new QinRoleSpec("Spearman", "spearman", GiacAnGeneralBuilder.SourcePath, "sword_regular_a_fit", "sword_idle_fit", 0.00f, 0.28f, 0.52f, 0.92f, 0.25f),
            new QinRoleSpec("ShieldBearer", "shieldBearer", ActionForgeEnemyBuilder.SourcePath, "shield_oneshot_fit", "idle_shield_loop_fit", 0.00f, 0.22f, 0.45f, 0.78f, 0.25f),
            new QinRoleSpec("Archer", "archer", ActionForgeEnemyBuilder.SourcePath, "overhandthrow_fit", "idle_loop_fit", 0.00f, 0.20f, 0.32f, 0.60f, 0.30f),
            new QinRoleSpec("Raider", "raider", GiacAnGeneralBuilder.SourcePath, "sword_regular_a_fit", "sword_idle_fit", 0.00f, 0.26f, 0.48f, 0.88f, 0.25f),
            new QinRoleSpec("Boss", "boss", GiacAnGeneralBuilder.SourcePath, "sword_attack_fit", "sword_idle_fit", 0.00f, 0.20f, 0.35f, 0.65f, 0.35484f)
        };

        [MenuItem("Tools/Thánh Gióng/ActionForge/Build Qin Soldier (Lính Tần) Assets")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building Qin Soldier assets.");
            if (!File.Exists(ModelPath))
                throw new FileNotFoundException("Missing rigged Qin Soldier FBX.", ModelPath);

            EnsureFolder(MaterialFolder);
            EnsureFolder(WeaponMeshFolder);
            EnsureFolder(OutputAnimFolder);
            EnsureFolder(StandaloneClipsFolder);
            EnsureFolder(Path.GetDirectoryName(MotionSetPath).Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(VisualPrefabPath).Replace('\\', '/'));

            ConfigureModelImporter(ModelPath, false);
            if (File.Exists(MeshyWalkPath)) ConfigureModelImporter(MeshyWalkPath, true);
            if (File.Exists(MeshyRunPath)) ConfigureModelImporter(MeshyRunPath, true);

            var targetModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (targetModel == null)
                throw new InvalidOperationException("Unity could not import " + ModelPath);

            // 1. Build PBR materials for Body, Lion Shield, Bronze Weapons, and Red Tassel
            Material bodyMat = BuildBodyMaterial();
            Material shieldMat = BuildShieldMaterial();
            Material bronzeMat = BuildSolidPbrMaterial(BronzeMaterialPath, "QinSoldier_WeaponBronze", new Color(0.32f, 0.28f, 0.23f, 1f), 0.88f, 0.68f);
            Material tasselMat = BuildSolidPbrMaterial(TasselMaterialPath, "QinSoldier_RedTassel", new Color(0.68f, 0.11f, 0.11f, 1f), 0.05f, 0.32f);

            // 2. Build 3D Weapon & Shield Meshes
            BuildQinWeaponMeshes();

            // 3. Bake exact-rig ActionForge animations (including sword_regular_a_rec & locomotion)
            var report = new List<string>
            {
                "=== QIN SOLDIER (LÍNH TẦN) MESHY AI + ACTIONFORGE BUILD REPORT ===",
                "Timestamp: " + DateTime.UtcNow.ToString("O"),
                "Model: " + ModelPath
            };

            BakeAllAnimationsAndControllers(targetModel, report);

            // 4. Build Visual Prefab & Gameplay Prefabs (including updating KayKit_Barbarian_Soldier.prefab)
            var spearmanController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath("Spearman"));
            var motionSet = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(MotionSetPath);
            var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath("Spearman", "Idle"));

            BuildVisualPrefab(targetModel, spearmanController, bodyMat, shieldMat, bronzeMat, tasselMat, idleClip);
            BuildGameplayPrefabs(motionSet, spearmanController);

            AssetDatabase.SaveAssets();
            report.Add(Verify());
            Directory.CreateDirectory("QinSoldierVerification");
            File.WriteAllLines("QinSoldierVerification/build-report.txt", report);
            return string.Join("\n", report);
        }

        public static string ControllerPath(string role) => OutputAnimFolder + "/" + role + ".controller";
        public static string ClipPath(string role, string phase) =>
            OutputAnimFolder + (string.IsNullOrEmpty(role) ? "" : "/" + role) + "/" + phase + ".anim";

        private static void ConfigureModelImporter(string path, bool importAnim)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as ModelImporter;
            }
            if (importer == null) throw new InvalidOperationException("Asset is not a ModelImporter: " + path);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = importAnim;
            importer.isReadable = true;
            importer.weldVertices = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.maxBonesPerVertex = 4;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, bool srgb, bool normal = false)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Missing texture: " + path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) throw new InvalidOperationException("Texture has not imported: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material BuildBodyMaterial()
        {
            ConfigureTexture(TextureFolder + "/base_color.png", true);
            ConfigureTexture(TextureFolder + "/metallic.png", false);
            ConfigureTexture(TextureFolder + "/roughness.png", false);
            ConfigureTexture(TextureFolder + "/normal.png", false, true);

            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/metallic.png");
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/roughness.png");
            string packedPath = MaterialFolder + "/QinSoldier_MetallicSmoothness.png";
            PackMetallicSmoothness(metallic, roughness, packedPath);

            return CreateOrUpdatePbrMaterial(
                BodyMaterialPath,
                "QinSoldier_PBR",
                AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/base_color.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/normal.png"),
                Color.white,
                0.65f,
                0.62f);
        }

        private static Material BuildShieldMaterial()
        {
            ConfigureTexture(TextureFolder + "/shield_base_color.png", true);
            ConfigureTexture(TextureFolder + "/shield_metallic.png", false);
            ConfigureTexture(TextureFolder + "/shield_roughness.png", false);
            ConfigureTexture(TextureFolder + "/shield_normal.png", false, true);

            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/shield_metallic.png");
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/shield_roughness.png");
            string packedPath = MaterialFolder + "/QinShield_MetallicSmoothness.png";
            PackMetallicSmoothness(metallic, roughness, packedPath);

            return CreateOrUpdatePbrMaterial(
                ShieldMaterialPath,
                "QinSoldier_Shield_PBR",
                AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/shield_base_color.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/shield_normal.png"),
                new Color(0.45f, 0.36f, 0.25f, 1f),
                0.58f,
                0.42f);
        }

        private static Material CreateOrUpdatePbrMaterial(
            string path, string matName, Texture2D baseMap, Texture2D metallicGloss, Texture2D normalMap,
            Color baseTint, float metallicScale, float smoothnessScale)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = matName };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", baseTint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", baseTint);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MetallicGlossMap", metallicGloss);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_Metallic", metallicScale);
            material.SetFloat("_Smoothness", smoothnessScale);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildSolidPbrMaterial(string path, string matName, Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = matName };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
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

        // =====================================================================
        // 3D QIN WEAPON & LION SHIELD PROCEDURAL MESH GENERATION
        // =====================================================================
        private static void BuildQinWeaponMeshes()
        {
            SaveOrUpdateMesh(CreateLionShieldMesh(), WeaponMeshFolder + "/Qin_LionShield.asset");
            SaveOrUpdateMesh(CreateQinSpearMesh(), WeaponMeshFolder + "/Qin_Spear.asset");
            SaveOrUpdateMesh(CreateQinSwordMesh(), WeaponMeshFolder + "/Qin_Sword.asset");
            SaveOrUpdateMesh(CreateQinBowMesh(), WeaponMeshFolder + "/Qin_Bow.asset");
        }

        private static Mesh CreateLionShieldMesh()
        {
            const int segs = 36;
            const int rings = 6;
            const float radius = 0.34f;
            const float domeDepth = 0.052f;
            const float thickness = 0.016f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Front domed surface (mapped to shield_base_color.png [0..1]x[0..1])
            verts.Add(new Vector3(0f, 0f, domeDepth));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int r = 1; r <= rings; r++)
            {
                float frac = (float)r / rings;
                float rad = frac * radius;
                float z = domeDepth * (1f - frac * frac * 0.85f);
                if (r == rings - 1) z += 0.008f; // Raised bronze rim ridge
                for (int s = 0; s < segs; s++)
                {
                    float ang = s * Mathf.PI * 2f / segs;
                    float cos = Mathf.Cos(ang);
                    float sin = Mathf.Sin(ang);
                    verts.Add(new Vector3(cos * rad, sin * rad, z));
                    uvs.Add(new Vector2(0.5f - cos * frac * 0.46f, 0.5f + sin * frac * 0.46f));
                }
            }

            // Center fan (+Z outward normal in Unity left-handed space)
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(0);
                tris.Add(1 + s);
                tris.Add(1 + next);
            }

            // Front rings (+Z outward normal)
            for (int r = 1; r < rings; r++)
            {
                int rowA = 1 + (r - 1) * segs;
                int rowB = 1 + r * segs;
                for (int s = 0; s < segs; s++)
                {
                    int next = (s + 1) % segs;
                    tris.Add(rowA + s);
                    tris.Add(rowB + s);
                    tris.Add(rowB + next);
                    tris.Add(rowA + s);
                    tris.Add(rowB + next);
                    tris.Add(rowA + next);
                }
            }

            // Back surface for solid 3D silhouette (-Z outward normal)
            int backCenter = verts.Count;
            verts.Add(new Vector3(0f, 0f, domeDepth - thickness));
            uvs.Add(new Vector2(0.08f, 0.08f));
            int backRingStart = verts.Count;
            for (int s = 0; s < segs; s++)
            {
                float ang = s * Mathf.PI * 2f / segs;
                verts.Add(new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, -thickness));
                uvs.Add(new Vector2(0.08f, 0.08f));
            }
            int outerFrontStart = 1 + (rings - 1) * segs;
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                // Back cap (-Z normal)
                tris.Add(backCenter);
                tris.Add(backRingStart + next);
                tris.Add(backRingStart + s);
                // Rim edge (radially outward normal)
                tris.Add(outerFrontStart + s);
                tris.Add(backRingStart + s);
                tris.Add(backRingStart + next);
                tris.Add(outerFrontStart + s);
                tris.Add(backRingStart + next);
                tris.Add(outerFrontStart + next);
            }

            var mesh = new Mesh { name = "Qin_LionShield" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateQinSpearMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var bronzeTris = new List<int>();
            var tasselTris = new List<int>();

            // Submesh 0: Shaft (-0.62m to +0.98m), Bronze Collar (+0.98m to +1.05m), Butt Spike (-0.72m to -0.62m), Diamond Spearhead (+1.05m to +1.42m)
            AddCylinderSection(verts, uvs, bronzeTris, -0.62f, 0.98f, 0.016f, 0.015f, 12);
            AddCylinderSection(verts, uvs, bronzeTris, -0.72f, -0.62f, 0.004f, 0.020f, 10);
            AddCylinderSection(verts, uvs, bronzeTris, 0.96f, 1.05f, 0.022f, 0.026f, 12);

            // Diamond-section Qin Bronze Spearhead (+1.05m to +1.42m)
            AddDiamondBlade(verts, uvs, bronzeTris, 1.05f, 1.42f, 0.042f, 0.016f);

            // Submesh 1: Flared Crimson Horsehair Tassel (+0.85m to +1.03m)
            AddCylinderSection(verts, uvs, tasselTris, 0.85f, 1.03f, 0.052f, 0.024f, 14);

            var mesh = new Mesh { name = "Qin_Spear", subMeshCount = 2 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(bronzeTris, 0);
            mesh.SetTriangles(tasselTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateQinSwordMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var bronzeTris = new List<int>();
            var tasselTris = new List<int>();

            // Submesh 0: Grip (-0.16m to +0.02m), Qin Crossguard (+0.02m to +0.06m), Octagonal Jian Blade (+0.06m to +0.84m)
            AddCylinderSection(verts, uvs, bronzeTris, -0.16f, 0.02f, 0.015f, 0.016f, 10);
            AddCylinderSection(verts, uvs, bronzeTris, -0.19f, -0.16f, 0.026f, 0.018f, 10);
            AddDiamondBlade(verts, uvs, bronzeTris, 0.02f, 0.06f, 0.068f, 0.024f);
            AddDiamondBlade(verts, uvs, bronzeTris, 0.06f, 0.84f, 0.034f, 0.012f);

            // Submesh 1: Crimson Grip & Pommel Tassel
            AddCylinderSection(verts, uvs, tasselTris, -0.28f, -0.18f, 0.024f, 0.012f, 10);

            var mesh = new Mesh { name = "Qin_Sword", subMeshCount = 2 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(bronzeTris, 0);
            mesh.SetTriangles(tasselTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateQinBowMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var bronzeTris = new List<int>();
            var tasselTris = new List<int>();

            // Curved bow limbs from y = -0.58m to +0.58m curving along +Z
            const int steps = 16;
            const int segs = 8;
            int baseIdx = verts.Count;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps * 2f - 1f; // -1 to +1
                float y = t * 0.58f;
                float z = (1f - t * t) * 0.16f - 0.04f;
                float rad = Mathf.Lerp(0.018f, 0.008f, Mathf.Abs(t));
                for (int s = 0; s < segs; s++)
                {
                    float ang = s * Mathf.PI * 2f / segs;
                    verts.Add(new Vector3(Mathf.Cos(ang) * rad, y, z + Mathf.Sin(ang) * rad));
                    uvs.Add(new Vector2((float)s / segs, (t + 1f) * 0.5f));
                }
            }
            for (int i = 0; i < steps; i++)
            {
                int rA = baseIdx + i * segs;
                int rB = baseIdx + (i + 1) * segs;
                var targetList = (i >= 6 && i <= 9) ? tasselTris : bronzeTris;
                for (int s = 0; s < segs; s++)
                {
                    int next = (s + 1) % segs;
                    targetList.Add(rA + s);
                    targetList.Add(rB + s);
                    targetList.Add(rB + next);
                    targetList.Add(rA + s);
                    targetList.Add(rB + next);
                    targetList.Add(rA + next);
                }
            }

            var mesh = new Mesh { name = "Qin_Bow", subMeshCount = 2 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(bronzeTris, 0);
            mesh.SetTriangles(tasselTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCylinderSection(
            List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            float y0, float y1, float r0, float r1, int segs)
        {
            int start = verts.Count;
            for (int ring = 0; ring <= 1; ring++)
            {
                float y = ring == 0 ? y0 : y1;
                float r = ring == 0 ? r0 : r1;
                for (int s = 0; s < segs; s++)
                {
                    float ang = s * Mathf.PI * 2f / segs;
                    verts.Add(new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r));
                    uvs.Add(new Vector2((float)s / segs, ring));
                }
            }
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(start + s);
                tris.Add(start + segs + s);
                tris.Add(start + segs + next);
                tris.Add(start + s);
                tris.Add(start + segs + next);
                tris.Add(start + next);
            }
        }

        private static void AddDiamondBlade(
            List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            float y0, float y1, float halfWidth, float halfThick)
        {
            float yMid = Mathf.Lerp(y0, y1, 0.72f);
            Vector3[] ring0 =
            {
                new Vector3(0f, y0, halfWidth),
                new Vector3(halfThick, y0, 0f),
                new Vector3(0f, y0, -halfWidth),
                new Vector3(-halfThick, y0, 0f)
            };
            Vector3[] ring1 =
            {
                new Vector3(0f, yMid, halfWidth * 0.65f),
                new Vector3(halfThick * 0.65f, yMid, 0f),
                new Vector3(0f, yMid, -halfWidth * 0.65f),
                new Vector3(-halfThick * 0.65f, yMid, 0f)
            };
            Vector3 tip = new Vector3(0f, y1, 0f);

            int b0 = verts.Count;
            foreach (var v in ring0) { verts.Add(v); uvs.Add(new Vector2(0.5f, 0.2f)); }
            int b1 = verts.Count;
            foreach (var v in ring1) { verts.Add(v); uvs.Add(new Vector2(0.5f, 0.7f)); }
            int bTip = verts.Count;
            verts.Add(tip);
            uvs.Add(new Vector2(0.5f, 1f));

            for (int s = 0; s < 4; s++)
            {
                int next = (s + 1) % 4;
                tris.Add(b0 + s);
                tris.Add(b1 + s);
                tris.Add(b1 + next);
                tris.Add(b0 + s);
                tris.Add(b1 + next);
                tris.Add(b0 + next);

                tris.Add(b1 + s);
                tris.Add(bTip);
                tris.Add(b1 + next);
            }
        }

        private static void SaveOrUpdateMesh(Mesh generated, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
            }
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(generated);
            }
        }

        // =====================================================================
        // EXACT-RIG ACTIONFORGE ANIMATION BAKER FOR QIN SOLDIER
        // =====================================================================
        private sealed class BonePair
        {
            public readonly string Source, Target, SourceChild, TargetChild;
            public readonly bool AlignChild;
            public BonePair(string src, string tgt, string srcChild, string tgtChild, bool alignChild)
            {
                Source = src;
                Target = tgt;
                SourceChild = srcChild;
                TargetChild = tgtChild;
                AlignChild = alignChild;
            }
        }

        private static readonly BonePair[] GeneralSourcePairs =
        {
            new BonePair("pelvis", "Hips", "spine_01", "Spine02", false),
            new BonePair("spine_01", "Spine02", "spine_02", "Spine01", false),
            new BonePair("spine_02", "Spine01", "spine_03", "Spine", false),
            new BonePair("spine_03", "Spine", "neck_01", "neck", false),
            new BonePair("neck_01", "neck", "Head", "Head", false),
            new BonePair("Head", "Head", null, null, false),
            new BonePair("clavicle_l", "LeftShoulder", "upperarm_l", "LeftArm", false),
            new BonePair("upperarm_l", "LeftArm", "lowerarm_l", "LeftForeArm", true),
            new BonePair("lowerarm_l", "LeftForeArm", "hand_l", "LeftHand", true),
            new BonePair("hand_l", "LeftHand", null, null, false),
            new BonePair("clavicle_r", "RightShoulder", "upperarm_r", "RightArm", false),
            new BonePair("upperarm_r", "RightArm", "lowerarm_r", "RightForeArm", true),
            new BonePair("lowerarm_r", "RightForeArm", "hand_r", "RightHand", true),
            new BonePair("hand_r", "RightHand", null, null, false),
            new BonePair("thigh_l", "LeftUpLeg", "calf_l", "LeftLeg", true),
            new BonePair("calf_l", "LeftLeg", "foot_l", "LeftFoot", true),
            new BonePair("foot_l", "LeftFoot", "ball_l", "LeftToeBase", true),
            new BonePair("ball_l", "LeftToeBase", null, null, false),
            new BonePair("thigh_r", "RightUpLeg", "calf_r", "RightLeg", true),
            new BonePair("calf_r", "RightLeg", "foot_r", "RightFoot", true),
            new BonePair("foot_r", "RightFoot", "ball_r", "RightToeBase", true),
            new BonePair("ball_r", "RightToeBase", null, null, false)
        };

        private static readonly BonePair[] EnemySourcePairs =
        {
            new BonePair("hips", "Hips", "spine", "Spine02", false),
            new BonePair("spine", "Spine02", "chest", "Spine01", false),
            new BonePair("spine", "Spine01", "chest", "Spine", false),
            new BonePair("chest", "Spine", "head", "neck", false),
            new BonePair("chest", "neck", "head", "Head", false),
            new BonePair("head", "Head", null, null, false),
            new BonePair("chest", "LeftShoulder", "upperarm.l", "LeftArm", false),
            new BonePair("upperarm.l", "LeftArm", "lowerarm.l", "LeftForeArm", true),
            new BonePair("lowerarm.l", "LeftForeArm", "hand.l", "LeftHand", true),
            new BonePair("hand.l", "LeftHand", null, null, false),
            new BonePair("chest", "RightShoulder", "upperarm.r", "RightArm", false),
            new BonePair("upperarm.r", "RightArm", "lowerarm.r", "RightForeArm", true),
            new BonePair("lowerarm.r", "RightForeArm", "hand.r", "RightHand", true),
            new BonePair("hand.r", "RightHand", null, null, false),
            new BonePair("upperleg.l", "LeftUpLeg", "lowerleg.l", "LeftLeg", true),
            new BonePair("lowerleg.l", "LeftLeg", "foot.l", "LeftFoot", true),
            new BonePair("foot.l", "LeftFoot", "toes.l", "LeftToeBase", true),
            new BonePair("toes.l", "LeftToeBase", null, null, false),
            new BonePair("upperleg.r", "RightUpLeg", "lowerleg.r", "RightLeg", true),
            new BonePair("lowerleg.r", "RightLeg", "foot.r", "RightFoot", true),
            new BonePair("foot.r", "RightFoot", "toes.r", "RightToeBase", true),
            new BonePair("toes.r", "RightToeBase", null, null, false)
        };

        private sealed class QinRigBaker : IDisposable
        {
            private readonly GameObject sourceModel, targetModel;
            private readonly BonePair[] pairs;
            private readonly Dictionary<string, Transform> sourceBones, targetBones;
            private readonly Dictionary<string, Quaternion> sourceBindRot, targetAlignedBindRot, sourceRestLocal, targetRestLocal;
            private readonly Dictionary<string, string> targetPaths;
            private readonly string[] topologicalTargets;
            public float MaxAlignmentError { get; private set; }

            public QinRigBaker(GameObject sourceAsset, GameObject targetAsset, BonePair[] mapping)
            {
                pairs = mapping;
                sourceModel = Object.Instantiate(sourceAsset);
                targetModel = Object.Instantiate(targetAsset);
                sourceModel.hideFlags = targetModel.hideFlags = HideFlags.HideAndDontSave;
                foreach (var a in sourceModel.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                foreach (var a in targetModel.GetComponentsInChildren<Animator>(true)) a.enabled = false;

                var srcAll = sourceModel.GetComponentsInChildren<Transform>(true);
                var tgtAll = targetModel.GetComponentsInChildren<Transform>(true);

                sourceBones = pairs.Select(p => p.Source).Concat(pairs.Where(p => p.SourceChild != null).Select(p => p.SourceChild))
                    .Distinct().ToDictionary(name => name, name => srcAll.First(t => t.name == name));
                targetBones = TargetBoneNames.ToDictionary(name => name, name => tgtAll.First(t => t.name == name));

                sourceRestLocal = sourceBones.ToDictionary(kv => kv.Key, kv => kv.Value.localRotation);
                targetRestLocal = targetBones.ToDictionary(kv => kv.Key, kv => kv.Value.localRotation);

                ExtractBindPose(sourceModel, sourceBones, out sourceBindRot, out var srcBindPos);
                ExtractBindPose(targetModel, targetBones, out var tgtBindRot, out var tgtBindPos);

                targetAlignedBindRot = new Dictionary<string, Quaternion>();
                var limbAlignments = new Dictionary<string, Quaternion>();
                foreach (var pair in pairs)
                {
                    if (pair.AlignChild && pair.SourceChild != null && pair.TargetChild != null)
                    {
                        Vector3 srcDir = (srcBindPos[pair.SourceChild] - srcBindPos[pair.Source]).normalized;
                        Vector3 tgtDir = (tgtBindPos[pair.TargetChild] - tgtBindPos[pair.Target]).normalized;
                        Quaternion align = Quaternion.FromToRotation(tgtDir, srcDir);
                        limbAlignments[pair.Target] = align;
                        targetAlignedBindRot[pair.Target] = align * tgtBindRot[pair.Target];
                    }
                    else if (pair.Target == "LeftHand" && limbAlignments.TryGetValue("LeftForeArm", out var lAlign))
                    {
                        targetAlignedBindRot[pair.Target] = lAlign * tgtBindRot[pair.Target];
                    }
                    else if (pair.Target == "RightHand" && limbAlignments.TryGetValue("RightForeArm", out var rAlign))
                    {
                        targetAlignedBindRot[pair.Target] = rAlign * tgtBindRot[pair.Target];
                    }
                    else
                    {
                        targetAlignedBindRot[pair.Target] = tgtBindRot[pair.Target];
                    }
                }

                topologicalTargets = TargetBoneNames.OrderBy(k => Depth(targetBones[k])).ToArray();
                targetPaths = targetBones.ToDictionary(kv => kv.Key, kv => AnimationUtility.CalculateTransformPath(kv.Value, targetModel.transform));
            }

            private static void ExtractBindPose(
                GameObject model,
                Dictionary<string, Transform> bones,
                out Dictionary<string, Quaternion> bindRot,
                out Dictionary<string, Vector3> bindPos)
            {
                bindRot = new Dictionary<string, Quaternion>();
                bindPos = new Dictionary<string, Vector3>();
                Quaternion invRoot = Quaternion.Inverse(model.transform.rotation);

                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null) continue;
                    var bindposes = skin.sharedMesh.bindposes;
                    for (int i = 0; i < Mathf.Min(bindposes.Length, skin.bones.Length); i++)
                    {
                        var b = skin.bones[i];
                        if (b == null || !bones.TryGetValue(b.name, out var expected) || b != expected) continue;
                        Matrix4x4 m = skin.localToWorldMatrix * bindposes[i].inverse;
                        bindRot[b.name] = (invRoot * m.rotation).normalized;
                        bindPos[b.name] = model.transform.InverseTransformPoint(m.GetColumn(3));
                    }
                }

                foreach (var kv in bones.OrderBy(p => Depth(p.Value)))
                {
                    if (!bindRot.ContainsKey(kv.Key))
                    {
                        bindRot[kv.Key] = (invRoot * kv.Value.rotation).normalized;
                        bindPos[kv.Key] = model.transform.InverseTransformPoint(kv.Value.position);
                    }
                }
            }

            public AnimationClip Bake(
                AnimationClip source,
                string clipName,
                Func<float, float> sourceTime,
                bool loop,
                AnimationClip recoveryIdle = null,
                float explicitKey = -1f,
                float customDuration = -1f,
                float blendStartPhase = 0.72f)
            {
                float duration = customDuration > 0f ? customDuration :
                    (loop || clipName.EndsWith(" Death", StringComparison.Ordinal) ? source.length : 1f);
                int count = Mathf.Max(2, Mathf.CeilToInt(duration * 30f) + 1);
                var phases = Enumerable.Range(0, count).Select(i => (float)i / (count - 1)).ToList();
                if (explicitKey > 0f && explicitKey < 1f) phases.Add(explicitKey);
                phases = phases.Distinct().OrderBy(p => p).ToList();

                var curves = TargetBoneNames.ToDictionary(
                    k => k,
                    k => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
                var previous = new Dictionary<string, Quaternion>();
                var expected = new List<Dictionary<string, Quaternion>>();

                foreach (float phase in phases)
                {
                    var desired = EvaluateDesired(source, sourceTime(phase));
                    if (recoveryIdle != null && phase > blendStartPhase)
                    {
                        var idlePose = EvaluateDesired(recoveryIdle, 0f);
                        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(blendStartPhase, 1f, phase));
                        foreach (var key in TargetBoneNames)
                            desired[key] = Quaternion.Slerp(desired[key], idlePose[key], blend);
                    }

                    foreach (var key in TargetBoneNames) targetBones[key].localRotation = targetRestLocal[key];
                    foreach (var key in topologicalTargets)
                    {
                        targetBones[key].rotation = desired[key];
                        Quaternion local = targetBones[key].localRotation.normalized;
                        if (previous.TryGetValue(key, out var last) && Quaternion.Dot(last, local) < 0f)
                            local = new Quaternion(-local.x, -local.y, -local.z, -local.w);
                        previous[key] = local;
                        for (int c = 0; c < 4; c++)
                            curves[key][c].AddKey(phase * duration, local[c]);
                    }
                    expected.Add(desired);
                }

                var clip = new AnimationClip
                {
                    name = clipName,
                    frameRate = 30f,
                    legacy = false,
                    wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever
                };

                foreach (var key in TargetBoneNames)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        var curve = curves[key][c];
                        for (int k = 0; k < curve.length; k++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                        }
                        AnimationUtility.SetEditorCurve(
                            clip,
                            EditorCurveBinding.FloatCurve(targetPaths[key], typeof(Transform), "m_LocalRotation." + "xyzw"[c]),
                            curve);
                    }
                }

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                settings.loopBlend = loop;
                settings.startTime = 0f;
                settings.stopTime = duration;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.EnsureQuaternionContinuity();

                foreach (int idx in new[] { 0, phases.Count / 4, phases.Count / 2, phases.Count * 3 / 4, phases.Count - 1 }.Distinct())
                {
                    foreach (var key in TargetBoneNames) targetBones[key].localRotation = targetRestLocal[key];
                    clip.SampleAnimation(targetModel, phases[idx] * duration);
                    foreach (var key in TargetBoneNames)
                    {
                        float err = Quaternion.Angle(targetBones[key].rotation, expected[idx][key]);
                        MaxAlignmentError = Mathf.Max(MaxAlignmentError, err);
                    }
                }

                return clip;
            }

            private Dictionary<string, Quaternion> EvaluateDesired(AnimationClip clip, float normalizedPhase)
            {
                foreach (var kv in sourceRestLocal) sourceBones[kv.Key].localRotation = kv.Value;
                clip.SampleAnimation(sourceModel, Mathf.Clamp01(normalizedPhase) * clip.length);
                Quaternion invSourceRoot = Quaternion.Inverse(sourceModel.transform.rotation);
                var result = new Dictionary<string, Quaternion>();
                foreach (var pair in pairs)
                {
                    Quaternion srcRot = invSourceRoot * sourceBones[pair.Source].rotation;
                    Quaternion delta = srcRot * Quaternion.Inverse(sourceBindRot[pair.Source]);
                    result[pair.Target] = (targetModel.transform.rotation * delta * targetAlignedBindRot[pair.Target]).normalized;
                }
                return result;
            }

            private static int Depth(Transform t)
            {
                int d = 0;
                while (t.parent != null) { d++; t = t.parent; }
                return d;
            }

            public void Dispose()
            {
                if (sourceModel != null) Object.DestroyImmediate(sourceModel);
                if (targetModel != null) Object.DestroyImmediate(targetModel);
            }
        }

        private static void BakeAllAnimationsAndControllers(GameObject targetModel, List<string> report)
        {
            var generalSource = AssetDatabase.LoadAssetAtPath<GameObject>(GiacAnGeneralBuilder.SourcePath);
            var enemySource = AssetDatabase.LoadAssetAtPath<GameObject>(ActionForgeEnemyBuilder.SourcePath);
            if (generalSource == null) throw new InvalidOperationException("Missing " + GiacAnGeneralBuilder.SourcePath);
            if (enemySource == null) throw new InvalidOperationException("Missing " + ActionForgeEnemyBuilder.SourcePath);

            var generalClips = AssetDatabase.LoadAllAssetsAtPath(GiacAnGeneralBuilder.SourcePath)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal))
                .ToDictionary(c => c.name);
            var enemyClips = AssetDatabase.LoadAllAssetsAtPath(ActionForgeEnemyBuilder.SourcePath)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal))
                .ToDictionary(c => c.name);

            using (var generalBaker = new QinRigBaker(generalSource, targetModel, GeneralSourcePairs))
            using (var enemyBaker = new QinRigBaker(enemySource, targetModel, EnemySourcePairs))
            {
                // 1. Bake shared locomotion clips (Walk, Jog, Sprint) + standalone sword_regular_a_rec clip
                foreach (var move in new[] { "Walk", "Jog", "Sprint" })
                {
                    string srcName = move == "Walk" ? "walk_loop_fit" : move == "Jog" ? "jog_fwd_loop_fit" : "sprint_loop_fit";
                    var baked = generalBaker.Bake(generalClips[srcName], move, p => p, true);
                    var standaloneCopy = Object.Instantiate(baked);
                    standaloneCopy.name = move;
                    SaveClip(baked, ClipPath(null, move));
                    SaveClip(standaloneCopy, StandaloneClipsFolder + "/" + move + ".anim");
                }

                // Standalone sword_regular_a_rec.anim (0.9667s = 0.4333s sword_regular_a_fit + recovery to sword_idle_fit)
                var swordRegARec = generalBaker.Bake(
                    generalClips["sword_regular_a_fit"],
                    "sword_regular_a_rec",
                    p => Mathf.Clamp01(p / 0.62f),
                    false,
                    generalClips["sword_idle_fit"],
                    0.32f,
                    0.9667f,
                    0.48f);
                SaveClip(swordRegARec, StandaloneClipsFolder + "/sword_regular_a_rec.anim");
                SaveClip(generalBaker.Bake(generalClips["sword_idle_fit"], "Idle", p => p, true), StandaloneClipsFolder + "/Idle.anim");

                // 2. Bake all 5 role sets (Spearman, ShieldBearer, Archer, Raider, Boss)
                foreach (var role in Roles)
                {
                    EnsureFolder(OutputAnimFolder + "/" + role.Name);
                    bool useGeneral = role.SourceFbx == GiacAnGeneralBuilder.SourcePath;
                    var baker = useGeneral ? generalBaker : enemyBaker;
                    var clipDict = useGeneral ? generalClips : enemyClips;

                    var action = clipDict[role.AttackClip];
                    var idle = clipDict[role.IdleClip];

                    SaveClip(baker.Bake(idle, role.Name + " Idle", p => p, true), ClipPath(role.Name, "Idle"));
                    SaveClip(baker.Bake(action, role.Name + " Windup", p => Mathf.Lerp(role.WindupStart, role.SourceWindupEnd, p), false), ClipPath(role.Name, "Windup"));
                    SaveClip(baker.Bake(action, role.Name + " Strike", p => p <= role.StrikeContact
                        ? Mathf.Lerp(role.SourceWindupEnd, role.SourceContact, p / role.StrikeContact)
                        : Mathf.Lerp(role.SourceContact, role.SourceStrikeEnd, (p - role.StrikeContact) / (1f - role.StrikeContact)),
                        false, null, role.StrikeContact), ClipPath(role.Name, "Strike"));
                    SaveClip(baker.Bake(action, role.Name + " Stuck", p => role.SourceContact, false), ClipPath(role.Name, "Stuck"));
                    SaveClip(baker.Bake(action, role.Name + " Recovery", p => Mathf.Lerp(role.SourceStrikeEnd, 1f, p), false, idle), ClipPath(role.Name, "Recovery"));
                    SaveClip(baker.Bake(clipDict["hit_chest_fit"], role.Name + " Hit", p => p, false), ClipPath(role.Name, "Hit"));
                    SaveClip(baker.Bake(clipDict["death01_fit"], role.Name + " Death", p => p, false), ClipPath(role.Name, "Death"));

                    BuildRoleController(role);
                    report.Add($"PASS Role [{role.Name}] source={role.AttackClip} idle={role.IdleClip} contact={role.StrikeContact:F3}");
                }

                float maxErr = Mathf.Max(generalBaker.MaxAlignmentError, enemyBaker.MaxAlignmentError);
                report.Add($"PASS Max exact-rig rotation alignment error: {maxErr:F5} degrees.");
            }

            SaveMotionSet(MotionSetPath, "QinSoldier");
            SaveMotionSet(BarbarianMotionSetPath, "Barbarian");
        }

        private static void BuildRoleController(QinRoleSpec role)
        {
            string path = ControllerPath(role.Name);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.parameters = new[]
            {
                new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float },
                new AnimatorControllerParameter { name = "MoveRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f },
                new AnimatorControllerParameter { name = "State", type = AnimatorControllerParameterType.Int },
                new AnimatorControllerParameter { name = "Phase", type = AnimatorControllerParameterType.Float }
            };

            var machine = controller.layers[0].stateMachine;
            string[] names = new[] { "Locomotion" }.Concat(PhaseNames).ToArray();
            var states = new Dictionary<string, AnimatorState>();

            foreach (var name in names)
            {
                var matches = machine.states.Where(s => s.state.name == name).ToArray();
                var state = matches.Length == 0 ? machine.AddState(name) : matches[0].state;
                foreach (var extra in matches.Skip(1)) machine.RemoveState(extra.state);
                states[name] = state;
                state.writeDefaultValues = false;
                state.speed = 1f;
                state.speedParameter = "MoveRate";
                state.speedParameterActive = name == "Locomotion";
                state.timeParameter = "Phase";
                state.timeParameterActive = name != "Locomotion" && name != "Death";
                if (name != "Locomotion")
                    state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(role.Name, name));
                foreach (var tr in state.transitions) state.RemoveTransition(tr);
                EditorUtility.SetDirty(state);
            }

            foreach (var obs in machine.states.Where(s => !states.ContainsKey(s.state.name)).ToArray())
                machine.RemoveState(obs.state);
            foreach (var child in machine.stateMachines)
                machine.RemoveStateMachine(child.stateMachine);

            var trees = AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().ToArray();
            var tree = trees.FirstOrDefault(t => t.name == "Enemy Locomotion");
            if (tree == null)
            {
                tree = new BlendTree { name = "Enemy Locomotion", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, controller);
            }
            foreach (var extra in trees.Where(t => t != tree)) Object.DestroyImmediate(extra, true);

            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.children = new[]
            {
                new ChildMotion { motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(role.Name, "Idle")), threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(null, "Walk")), threshold = 0.4f, timeScale = 1f },
                new ChildMotion { motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(null, "Jog")), threshold = 0.75f, timeScale = 1f },
                new ChildMotion { motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(null, "Sprint")), threshold = 1f, timeScale = 1f }
            };
            states["Locomotion"].motion = tree;
            machine.defaultState = states["Locomotion"];

            foreach (var tr in machine.anyStateTransitions) machine.RemoveAnyStateTransition(tr);
            var indices = new Dictionary<string, int>
            {
                { "Windup", 2 }, { "Strike", 3 }, { "Stuck", 4 }, { "Recovery", 5 }, { "Hit", 6 }, { "Death", 7 }
            };

            foreach (var name in new[] { "Death", "Hit", "Windup", "Strike", "Stuck", "Recovery", "Locomotion" })
            {
                var tr = machine.AddAnyStateTransition(states[name]);
                tr.conditions = new[]
                {
                    new AnimatorCondition
                    {
                        parameter = "State",
                        mode = name == "Locomotion" ? AnimatorConditionMode.Less : AnimatorConditionMode.Equals,
                        threshold = name == "Locomotion" ? 2f : indices[name]
                    }
                };
                tr.hasExitTime = false;
                tr.hasFixedDuration = true;
                tr.duration = name == "Hit" || name == "Strike" ? 0.04f : name == "Locomotion" || name == "Death" ? 0.08f : 0.06f;
                tr.canTransitionToSelf = false;
                tr.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                tr.orderedInterruption = false;
                EditorUtility.SetDirty(tr);
            }

            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }

        private static ActionForgeEnemyMotionSet SaveMotionSet(string path, string setName)
        {
            var set = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ActionForgeEnemyMotionSet>();
                set.name = setName;
                AssetDatabase.CreateAsset(set, path);
            }
            var serialized = new SerializedObject(set);
            foreach (var role in Roles)
            {
                serialized.FindProperty(role.Field).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(role.Name));
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            return set;
        }

        // =====================================================================
        // PREFAB CONSTRUCTION & WEAPON ATTACHMENT
        // =====================================================================
        public static void AttachRoleEquipment(
            Transform visualRoot,
            ThanhGiongEnemy.EnemyRole role,
            Material shieldMat,
            Material bronzeMat,
            Material tasselMat)
        {
            // Sample the role's combat Idle pose first so weapon & shield sockets align to the combat guard stance
            string roleName = role == ThanhGiongEnemy.EnemyRole.Auto ? "Spearman" : role.ToString();
            AnimationClip roleIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(roleName, "Idle"));
            if (roleIdle != null)
            {
                roleIdle.SampleAnimation(visualRoot.gameObject, 0f);
            }

            Transform rightHand = FindExactBone(visualRoot, "RightHand");
            Transform leftHand = FindExactBone(visualRoot, "LeftHand");
            Transform leftForeArm = FindExactBone(visualRoot, "LeftForeArm");

            // Clean old attached props
            foreach (Transform t in visualRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == visualRoot) continue;
                if (t.name.StartsWith("Qin_", StringComparison.Ordinal) ||
                    t.name == "handslot.r" || t.name == "handslot.l" ||
                    t.name == "ActionForge Weapon Tip" ||
                    t.name.Contains("Barbarian ") || t.name.Contains("Invader Maroon Cloth"))
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            Mesh spearMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WeaponMeshFolder + "/Qin_Spear.asset");
            Mesh swordMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WeaponMeshFolder + "/Qin_Sword.asset");
            Mesh bowMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WeaponMeshFolder + "/Qin_Bow.asset");
            Mesh shieldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WeaponMeshFolder + "/Qin_LionShield.asset");

            if (rightHand != null)
            {
                var slotR = new GameObject("handslot.r").transform;
                slotR.SetParent(rightHand, false);
                slotR.localPosition = new Vector3(-0.02f, 0.02f, 0.01f);

                if (role == ThanhGiongEnemy.EnemyRole.Archer)
                {
                    // Bow limbs along +Y, bow curve facing forward (+Z)
                    slotR.rotation = Quaternion.LookRotation(visualRoot.forward, visualRoot.up);
                }
                else
                {
                    // Spear/Sword shaft & blade extend along local +Y; orient +Y forward-and-up in combat guard
                    Vector3 weaponShaftDir = (visualRoot.forward * 0.84f + visualRoot.up * 0.54f).normalized;
                    Vector3 bladeEdgeDir = Vector3.Cross(visualRoot.right, weaponShaftDir).normalized;
                    slotR.rotation = Quaternion.LookRotation(bladeEdgeDir, weaponShaftDir);
                }

                Mesh weaponMesh = role == ThanhGiongEnemy.EnemyRole.Archer ? bowMesh :
                    (role == ThanhGiongEnemy.EnemyRole.ShieldBearer || role == ThanhGiongEnemy.EnemyRole.Raider) ? swordMesh : spearMesh;
                string weaponName = role == ThanhGiongEnemy.EnemyRole.Archer ? "Qin_Bow" :
                    (role == ThanhGiongEnemy.EnemyRole.ShieldBearer || role == ThanhGiongEnemy.EnemyRole.Raider) ? "Qin_Sword" : "Qin_Spear";

                if (weaponMesh != null)
                {
                    var weaponGo = new GameObject(weaponName);
                    weaponGo.transform.SetParent(slotR, false);
                    weaponGo.transform.localPosition = Vector3.zero;
                    weaponGo.transform.localRotation = Quaternion.identity;
                    var mf = weaponGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = weaponMesh;
                    var mr = weaponGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = weaponMesh.subMeshCount > 1
                        ? new[] { bronzeMat, tasselMat }
                        : new[] { bronzeMat };
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }

                float tipDist = role == ThanhGiongEnemy.EnemyRole.Archer ? 0.55f :
                    (role == ThanhGiongEnemy.EnemyRole.ShieldBearer || role == ThanhGiongEnemy.EnemyRole.Raider) ? 0.84f : 1.40f;
                var tip = new GameObject("ActionForge Weapon Tip").transform;
                tip.SetParent(rightHand, false);
                tip.position = slotR.TransformPoint(new Vector3(0f, tipDist, 0f));
            }

            if (leftHand != null && role != ThanhGiongEnemy.EnemyRole.Archer && shieldMesh != null)
            {
                var slotL = new GameObject("handslot.l").transform;
                Transform shieldParent = leftForeArm != null ? leftForeArm : leftHand;
                slotL.SetParent(shieldParent, false);

                // Shield front face (+Z) faces forward-left to guard the soldier's torso; lion top (+Y) is upright
                Vector3 shieldOutward = (visualRoot.forward * 0.78f - visualRoot.right * 0.62f).normalized;
                Vector3 anchorPos = leftForeArm != null
                    ? Vector3.Lerp(leftForeArm.position, leftHand.position, 0.68f)
                    : leftHand.position;
                slotL.position = anchorPos + shieldOutward * 0.085f;
                slotL.rotation = Quaternion.LookRotation(shieldOutward, visualRoot.up);

                var shieldGo = new GameObject("Qin_LionShield");
                shieldGo.transform.SetParent(slotL, false);
                shieldGo.transform.localPosition = Vector3.zero;
                shieldGo.transform.localRotation = Quaternion.identity;
                float shieldScale = role == ThanhGiongEnemy.EnemyRole.ShieldBearer ? 1.18f : 1.0f;
                shieldGo.transform.localScale = Vector3.one * shieldScale;
                var mf = shieldGo.AddComponent<MeshFilter>();
                mf.sharedMesh = shieldMesh;
                var mr = shieldGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = shieldMat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
        }

        private static Transform FindExactBone(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        private static void BuildVisualPrefab(
            GameObject source,
            RuntimeAnimatorController controller,
            Material bodyMat,
            Material shieldMat,
            Material bronzeMat,
            Material tasselMat,
            AnimationClip idle)
        {
            var root = new GameObject("QinSoldier_Meshy");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "QinSoldier_Visual";
                model.transform.SetParent(root.transform, false);

                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = null; // Exact-path Generic curves drive the rig directly
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(bodyMat, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.updateWhenOffscreen = true;
                }

                // Sample combat Idle pose, normalize height to 1.85m, and ground soles at Y = 0
                if (idle != null)
                {
                    idle.SampleAnimation(model, 0f);
                }

                Bounds bounds = BakedSkinnedBounds(model);
                if (bounds.size.y > 0.01f)
                {
                    model.transform.localScale *= 1.85f / bounds.size.y;
                    bounds = BakedSkinnedBounds(model);
                }
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                AttachRoleEquipment(model.transform, ThanhGiongEnemy.EnemyRole.Spearman, shieldMat, bronzeMat, tasselMat);

                PrefabUtility.SaveAsPrefabAsset(root, VisualPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildGameplayPrefabs(ActionForgeEnemyMotionSet motionSet, AnimatorController spearmanController)
        {
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (visualAsset == null) throw new InvalidOperationException("Missing visual prefab: " + VisualPrefabPath);

            var root = new GameObject("QinSoldier_Enemy");
            try
            {
                var visualPivot = new GameObject("Visual").transform;
                visualPivot.SetParent(root.transform, false);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset, visualPivot);
                model.name = "QinSoldier Model";
                model.transform.localPosition = new Vector3(0f, -0.97f, 0f);
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = Vector3.zero;
                capsule.height = 2.0f;
                capsule.radius = 0.45f;

                var body = root.AddComponent<Rigidbody>();
                body.mass = 85f;
                body.linearDamping = 0.8f;
                body.angularDamping = 2.5f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                var enemy = root.AddComponent<ThanhGiongEnemy>();
                enemy.enemyName = "Chiến Binh Quân Tần";
                enemy.maxHealth = 75f;
                enemy.moveSpeed = 2.75f;
                enemy.engageDistance = 28f;
                enemy.isBoss = false;

                var presentation = root.AddComponent<LegendEnemyPresentation>();
                presentation.model = model.transform;
                presentation.sourceAsset = VisualPrefabPath;
                presentation.placeholders = Array.Empty<Renderer>();
                presentation.preserveAuthoredMaterials = true;
                presentation.modelOriginIsFeet = true;

                var driver = root.AddComponent<ThanhGiongEnemyAnimationDriver>();
                driver.actionForgeMotions = motionSet;

                root.AddComponent<ActionForgeEnemyEffects>();

                var animator = model.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.runtimeAnimatorController = spearmanController;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefabPath);
                if (!File.Exists(LegacySoldierPrefabPath))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, LegacySoldierPrefabPath);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            if (File.Exists(LegacySoldierPrefabPath))
            {
                var legacyRoot = PrefabUtility.LoadPrefabContents(LegacySoldierPrefabPath);
                try
                {
                    foreach (var e in legacyRoot.GetComponentsInChildren<ThanhGiongEnemy>(true))
                    {
                        if (!e.isBoss)
                        {
                            e.role = ThanhGiongEnemy.EnemyRole.Spearman;
                            QinSoldierCampaignInstaller.ApplySoldier(e);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(legacyRoot, LegacySoldierPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(legacyRoot);
                }
            }
        }

        private static Bounds BakedSkinnedBounds(GameObject model)
        {
            bool hasVertex = false;
            var bounds = new Bounds();
            var baked = new Mesh();
            try
            {
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh == null) continue;
                    renderer.BakeMesh(baked, true);
                    foreach (var v in baked.vertices)
                    {
                        Vector3 world = renderer.transform.TransformPoint(v);
                        if (!hasVertex) { bounds = new Bounds(world, Vector3.zero); hasVertex = true; }
                        else bounds.Encapsulate(world);
                    }
                    baked.Clear();
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
            if (!hasVertex) throw new InvalidOperationException("Model has no skinned vertices.");
            return bounds;
        }

        private static void SaveClip(AnimationClip generated, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
            }
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(generated);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static string Verify()
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            var gameplay = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPrefabPath);
            var legacy = AssetDatabase.LoadAssetAtPath<GameObject>(LegacySoldierPrefabPath);
            var set = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(MotionSetPath);
            var regARec = AssetDatabase.LoadAssetAtPath<AnimationClip>(StandaloneClipsFolder + "/sword_regular_a_rec.anim");

            if (visual == null || gameplay == null || legacy == null || set == null || regARec == null)
                throw new InvalidOperationException("Missing generated QinSoldier assets.");

            int clipCount = 0;
            foreach (var move in new[] { "Walk", "Jog", "Sprint" })
            {
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(null, move));
                if (c == null || c.length <= 0f) throw new InvalidOperationException("Missing locomotion clip: " + move);
                clipCount++;
            }

            foreach (var role in Roles)
            {
                foreach (var phase in new[] { "Idle" }.Concat(PhaseNames))
                {
                    var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(role.Name, phase));
                    if (c == null || c.length <= 0f)
                        throw new InvalidOperationException($"Missing role clip: {role.Name}/{phase}");
                    clipCount++;
                }
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(role.Name));
                if (ctrl == null || ctrl.layers.Length != 1 || ctrl.layers[0].stateMachine.states.Length != 7)
                    throw new InvalidOperationException("Invalid role controller: " + role.Name);
            }

            return $"PASS QinSoldier Verify: {clipCount} role/locomotion clips + sword_regular_a_rec ({regARec.length:F3}s), 5 controllers, QinSoldier.asset motion set, 4 Qin weapon meshes, PBR materials, and prefabs.";
        }
    }
}
