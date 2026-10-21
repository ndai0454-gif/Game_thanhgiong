using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThanhGiong.EditorTools
{
    public static class MeshyAssetInstaller
    {
        private const string MeshyRoot = "Assets/Art/Meshy";
        private const string AnimDir = "Assets/Animations/ActionForge";
        private const string PrefabDir = "Assets/Prefabs/Meshy";
        private const string MaterialDir = "Assets/Materials/Meshy";

        private static readonly string[] CampaignScenes =
        {
            "Assets/Scenes/ThanhGiongWorld/LangGiongTienTuyen.unity",
            "Assets/Scenes/ThanhGiongWorld/KinhThanhRenThep.unity",
            "Assets/Scenes/ThanhGiongWorld/PhaoDaiNgamQuanAn.unity",
            "Assets/Scenes/ThanhGiongWorld/ThungLungVuotSong.unity",
            "Assets/Scenes/ThanhGiongWorld/TranTuyenNuiSoc.unity",
            "Assets/Scenes/ThanhGiongWorld/DinhSocHoaThanh.unity",
            "Assets/Scenes/AlbionForestMap.unity"
        };

        [MenuItem("Tools/Thánh Gióng/Install & Wire Meshy 3D Models + Animations", false, 5)]
        public static string InstallAll()
        {
            EnsureDir(PrefabDir);
            EnsureDir(MaterialDir);

            AssetDatabase.Refresh();

            // 1. Create URP PBR Materials for Hero, IronHorse, and BambooSpear
            Material heroMat = CreatePbrMaterial(
                "Mat_ThanhGiong_Meshy",
                $"{MeshyRoot}/ThanhGiong_Hero/ThanhGiong_Hero_Meshy",
                new Color(1f, 1f, 1f, 1f));

            Material horseMat = CreatePbrMaterial(
                "Mat_IronHorse_Meshy",
                $"{MeshyRoot}/IronHorse/IronHorse_Meshy",
                new Color(1f, 1f, 1f, 1f));

            Material spearMat = CreatePbrMaterial(
                "Mat_BambooSpear_Meshy",
                $"{MeshyRoot}/BambooSpear/BambooSpear_Meshy",
                new Color(1f, 1f, 1f, 1f));

            // 2. Configure FBX Model Importers
            string heroRiggedFbx = $"{MeshyRoot}/ThanhGiong_Hero/ThanhGiong_Hero_Rigged.fbx";
            string horseFbx = $"{MeshyRoot}/IronHorse/IronHorse_Meshy.fbx";
            string spearFbx = $"{MeshyRoot}/BambooSpear/BambooSpear_Meshy.fbx";

            ConfigureModelImporter(heroRiggedFbx, true, false);
            ConfigureModelImporter(horseFbx, false, true);
            ConfigureModelImporter(spearFbx, false, true);

            // Configure all Meshy Animation FBXs in Assets/Animations/ActionForge
            string[] animFiles = Directory.Exists(AnimDir)
                ? Directory.GetFiles(AnimDir, "Meshy_ThanhGiong_*.fbx")
                : new string[0];

            foreach (string file in animFiles)
            {
                string assetPath = file.Replace('\\', '/');
                bool loop = assetPath.ToLower().Contains("idle") ||
                            assetPath.ToLower().Contains("walk") ||
                            assetPath.ToLower().Contains("run");
                ConfigureAnimationImporter(assetPath, loop);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 3. Build Animator Controller for Meshy Rigged Hero
            AnimatorController controller = BuildMeshyAnimatorController();

            // 4. Build Prefabs
            GameObject spearPrefab = BuildStaticPrefab(
                spearFbx,
                $"{PrefabDir}/Meshy_BambooSpear.prefab",
                spearMat,
                Vector3.one * 1.15f);

            GameObject horsePrefab = BuildStaticPrefab(
                horseFbx,
                $"{PrefabDir}/Meshy_IronHorse.prefab",
                horseMat,
                Vector3.one * 1.45f);

            GameObject heroPrefab = BuildHeroPrefab(
                heroRiggedFbx,
                $"{PrefabDir}/Meshy_ThanhGiong_Hero_Rigged.prefab",
                heroMat,
                spearPrefab,
                controller);

            GameObject mountedPrefab = BuildMountedComboPrefab(
                horseFbx,
                horseMat,
                heroPrefab,
                controller,
                $"{PrefabDir}/Meshy_Mounted_ThanhGiong.prefab");

            int updatedScenes = InstallIntoAllCampaignScenes(mountedPrefab, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = $"Installed Meshy Assets:\n- Hero Prefab: {(heroPrefab != null ? heroPrefab.name : "null")}\n- Horse Prefab: {(horsePrefab != null ? horsePrefab.name : "null")}\n- Spear Prefab: {(spearPrefab != null ? spearPrefab.name : "null")}\n- Mounted Combo (17-Bone Skinned Horse + Rigged Hero + VFX): {(mountedPrefab != null ? mountedPrefab.name : "null")}\n- Animations configured: {animFiles.Length}\n- Campaign Scenes Updated: {updatedScenes}";
            Debug.Log("<color=green>[MeshyAssetInstaller] " + summary + "</color>");
            return summary;
        }

        private static int InstallIntoAllCampaignScenes(GameObject mountedPrefab, AnimatorController controller)
        {
            if (mountedPrefab == null || EditorApplication.isPlaying) return 0;

            string activeScenePath = SceneManager.GetActiveScene().path;
            int count = 0;

            foreach (string scenePath in CampaignScenes)
            {
                if (!File.Exists(scenePath)) continue;
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ThanhGiongCampaignController campaign = Object.FindAnyObjectByType<ThanhGiongCampaignController>(FindObjectsInactive.Include);
                if (campaign == null) continue;

                MountedHorseController movement = campaign.GetComponent<MountedHorseController>();
                Transform visual = movement != null && movement.visual != null
                    ? movement.visual
                    : campaign.transform.Find("Visual");

                if (visual == null)
                {
                    GameObject vGo = new GameObject("Visual");
                    vGo.transform.SetParent(campaign.transform, false);
                    visual = vGo.transform;
                }

                visual.gameObject.SetActive(true);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one * 1.65f;

                if (movement != null)
                {
                    movement.visual = visual;
                }

                // Remove old Meshy instances and legacy MountedGiong so GodotMountedMotion binds to the new 17-bone Meshy IronHorse
                var toRemove = new List<GameObject>();
                foreach (Transform child in visual)
                {
                    if (child.name == "Meshy_Mounted_ThanhGiong" ||
                        child.name == "Meshy_ThanhGiong_Mounted" ||
                        child.name == "MountedGiong")
                    {
                        toRemove.Add(child.gameObject);
                    }
                }
                foreach (GameObject go in toRemove)
                {
                    Object.DestroyImmediate(go);
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(mountedPrefab, scene);
                instance.name = "Meshy_Mounted_ThanhGiong";
                instance.transform.SetParent(visual, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                // Ensure Hero Animator has the controller assigned in-scene
                Animator[] anims = instance.GetComponentsInChildren<Animator>(true);
                foreach (Animator a in anims)
                {
                    if (controller != null) a.runtimeAnimatorController = controller;
                    a.avatar = null;
                    a.enabled = true;
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                // Ensure GodotMountedMotion is present and wired to visual
                GodotMountedMotion mountedMotion = campaign.GetComponent<GodotMountedMotion>();
                if (mountedMotion == null) mountedMotion = campaign.gameObject.AddComponent<GodotMountedMotion>();
                mountedMotion.enabled = true;
                mountedMotion.visual = visual;

                // Ensure Ribbon Cape & Speed Ribbon VFX are present and enabled on player
                ThanhGiongRibbonCape cape = campaign.GetComponent<ThanhGiongRibbonCape>();
                if (cape == null) cape = campaign.gameObject.AddComponent<ThanhGiongRibbonCape>();
                cape.enabled = true;

                ThanhGiongSpeedRibbon speedRibbon = campaign.GetComponent<ThanhGiongSpeedRibbon>();
                if (speedRibbon == null) speedRibbon = campaign.gameObject.AddComponent<ThanhGiongSpeedRibbon>();
                speedRibbon.enabled = true;

                MeshyHeroVisualDriver driver = campaign.GetComponent<MeshyHeroVisualDriver>();
                if (driver == null) driver = campaign.gameObject.AddComponent<MeshyHeroVisualDriver>();
                driver.meshyRoot = instance.transform;
                driver.meshyHero = null;
                driver.meshyHorse = null;
                driver.meshySpear = null;
                driver.heroAnimator = null;
                driver.BindReferences();

                EditorUtility.SetDirty(campaign.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                count++;
            }

            if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }

            return count;
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static Material CreatePbrMaterial(string matName, string texBasePrefix, Color tint)
        {
            string matPath = $"{MaterialDir}/{matName}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

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

            Texture2D albedo = LoadTexture(texBasePrefix + "_Albedo.png", false);
            Texture2D normal = LoadTexture(texBasePrefix + "_Normal.png", false, true);

            mat.SetColor("_BaseColor", tint);
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.mainTexture = albedo;
            }
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetFloat("_Metallic", 0.25f);
            mat.SetFloat("_Smoothness", 0.55f);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D LoadTexture(string path, bool linear, bool isNormal = false)
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
                if (linear && ti.sRGBTexture)
                {
                    ti.sRGBTexture = false;
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

        private static void ConfigureModelImporter(string path, bool isRiggedGeneric, bool isReadable)
        {
            if (!File.Exists(path)) return;
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            importer.isReadable = isReadable;
            importer.useFileScale = true;
            importer.globalScale = 1.0f;
            importer.bakeAxisConversion = false;

            if (isRiggedGeneric)
            {
                // Generic rig is required because the extracted Meshy_ThanhGiong_*.anim clips animate Armature/Hips/Spine02Generic transform paths directly
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
            }
            importer.SaveAndReimport();
        }

        private static void ConfigureAnimationImporter(string path, bool loop)
        {
            if (!File.Exists(path)) return;
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults != null && defaults.Length > 0)
            {
                ModelImporterClipAnimation[] clips = new ModelImporterClipAnimation[defaults.Length];
                for (int i = 0; i < defaults.Length; i++)
                {
                    clips[i] = new ModelImporterClipAnimation
                    {
                        name = Path.GetFileNameWithoutExtension(path),
                        takeName = defaults[i].takeName,
                        firstFrame = defaults[i].firstFrame,
                        lastFrame = defaults[i].lastFrame,
                        loopTime = loop,
                        loopPose = loop,
                        lockRootRotation = true,
                        lockRootHeightY = true,
                        lockRootPositionXZ = true,
                        keepOriginalOrientation = true,
                        keepOriginalPositionY = true,
                        keepOriginalPositionXZ = true
                    };
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildMeshyAnimatorController()
        {
            string ctrlPath = "Assets/Animations/ThanhGiong/Meshy_ThanhGiong_Hero.controller";
            EnsureDir("Assets/Animations/ThanhGiong");

            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("BambooAttack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HeavySlam", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Ascension", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            AnimationClip idleClip = FindClip($"{AnimDir}/Meshy_ThanhGiong_Idle.fbx");
            AnimationClip walkClip = FindClip($"{AnimDir}/Meshy_ThanhGiong_Walk.fbx");
            AnimationClip runClip = FindClip($"{AnimDir}/Meshy_ThanhGiong_Run.fbx");
            AnimationClip swordClip = FindClip($"{AnimDir}/Meshy_ThanhGiong_SwordSlash.fbx");
            AnimationClip sweepClip = FindClip($"{AnimDir}/Meshy_ThanhGiong_BambooSweep360.fbx");
            AnimationClip slamClip = FindClip($"{AnimDir}/Meshy_ThanhGiong_BambooHeavySlam.fbx");
            AnimationClip victoryClip = FindClip($"{AnimDir}/Meshy_ThanhGiong_VictoryAscension.fbx");

            EnsureLoopTime(idleClip, true);
            EnsureLoopTime(walkClip, true);
            EnsureLoopTime(runClip, true);

            // BlendTree for Locomotion (Idle -> Walk -> Run)
            BlendTree tree;
            AnimatorState locoState = ctrl.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idleClip != null) tree.AddChild(idleClip, 0f);
            if (walkClip != null) tree.AddChild(walkClip, 0.5f);
            if (runClip != null) tree.AddChild(runClip, 1f);
            sm.defaultState = locoState;

            AddTriggerState(sm, locoState, "Attack", swordClip != null ? swordClip : sweepClip, "Attack");
            AddTriggerState(sm, locoState, "BambooSweep360", sweepClip, "BambooAttack");
            AddTriggerState(sm, locoState, "BambooHeavySlam", slamClip, "HeavySlam");
            AddTriggerState(sm, locoState, "VictoryAscension", victoryClip, "Ascension");

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static void EnsureLoopTime(AnimationClip clip, bool loop)
        {
            if (clip == null) return;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime != loop)
            {
                settings.loopTime = loop;
                settings.loopBlend = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
        }

        private static void AddTriggerState(AnimatorStateMachine sm, AnimatorState defaultState, string stateName, AnimationClip clip, string trigger)
        {
            if (clip == null) return;
            AnimatorState st = sm.AddState(stateName);
            st.motion = clip;

            AnimatorStateTransition anyT = sm.AddAnyStateTransition(st);
            anyT.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            anyT.duration = 0.08f;
            anyT.canTransitionToSelf = false;

            AnimatorStateTransition exitT = st.AddTransition(defaultState);
            exitT.hasExitTime = true;
            exitT.exitTime = 0.9f;
            exitT.duration = 0.15f;
        }

        private static AnimationClip FindClip(string fbxPath)
        {
            string baseName = Path.GetFileNameWithoutExtension(fbxPath);
            string animAssetPath = $"Assets/Animations/ThanhGiong/{baseName}.anim";
            AnimationClip extracted = AssetDatabase.LoadAssetAtPath<AnimationClip>(animAssetPath);
            if (extracted != null) return extracted;

            if (!File.Exists(fbxPath)) return null;
            Object[] objs = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (Object o in objs)
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    return c;
            }
            return null;
        }

        private static GameObject BuildStaticPrefab(string fbxPath, string prefabPath, Material mat, Vector3 scale)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null) return null;

            GameObject wrapper = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            GameObject inst = Object.Instantiate(src, wrapper.transform);
            inst.name = "Model";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.Scale(src.transform.localScale, scale);
            inst.transform.localRotation = src.transform.localRotation;

            foreach (Renderer r in wrapper.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            Object.DestroyImmediate(wrapper);
            return saved;
        }

        private static GameObject BuildHeroPrefab(string fbxPath, string prefabPath, Material heroMat, GameObject spearPrefab, AnimatorController ctrl)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null) return null;

            GameObject inst = Object.Instantiate(src);
            inst.name = Path.GetFileNameWithoutExtension(prefabPath);

            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = heroMat;
            }

            Animator anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.avatar = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Attach BambooSpear to right hand bone if present
            Transform rightHand = null;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLower();
                if (n.Contains("righthand") || n.Contains("hand_r") || n.Contains("hand.r") || n.Contains("r_hand"))
                {
                    rightHand = t;
                    break;
                }
            }

            if (rightHand != null && spearPrefab != null)
            {
                GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(spearPrefab);
                weapon.name = "Weapon_GoldenBamboo";
                weapon.transform.SetParent(rightHand, false);
                weapon.transform.localPosition = new Vector3(-0.005f, 0.105f, -0.012f);
                weapon.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
                weapon.transform.localScale = Vector3.one * 0.72f;

                AttachSpearTrailVfx(weapon.transform);
            }

            foreach (SkinnedMeshRenderer heroSmr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                ThanhGiong.EditorTools.HorseSkinRepair.RepairHeroSkin(heroSmr);
            }

            AttachHeroDivineAuraVfx(inst.transform);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
            Object.DestroyImmediate(inst);
            return saved;
        }

        private static GameObject BuildMountedComboPrefab(string horseFbxPath, Material horseMat, GameObject heroPrefab, AnimatorController ctrl, string prefabPath)
        {
            if (heroPrefab == null) return null;

            GameObject root = new GameObject("Meshy_Mounted_ThanhGiong");
            GameObject mount = new GameObject("Mount");
            mount.transform.SetParent(root.transform, false);

            // Build 17-bone procedurally-skinned IronHorse compatible with GodotMountedMotion
            GameObject horse = BuildSkinnedIronHorse(horseFbxPath, horseMat, mount.transform);
            if (horse != null)
            {
                horse.transform.localPosition = Vector3.zero;
                horse.transform.localScale = Vector3.one * 1.115f; // 1.3 * 1.115 = 1.45x world scale with hooves grounded at Y=0
            }

            GameObject hero = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab);
            hero.name = "ThanhGiong_Hero_Meshy";
            hero.transform.SetParent(mount.transform, false);
            hero.transform.localPosition = new Vector3(0f, 0.72f, -0.08f);

            Animator anim = hero.GetComponent<Animator>();
            if (anim == null) anim = hero.AddComponent<Animator>();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.avatar = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildSkinnedIronHorse(string horseFbxPath, Material horseMat, Transform parent)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(horseFbxPath);
            if (src == null) return null;

            GameObject horseRoot = new GameObject("IronHorse");
            horseRoot.transform.SetParent(parent, false);

            GameObject temp = Object.Instantiate(src, horseRoot.transform);
            MeshFilter mf = temp.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Object.DestroyImmediate(temp);
                return horseRoot;
            }

            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = "Meshy_IronHorse_Skinned";

            Matrix4x4 m = horseRoot.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = (m.MultiplyPoint3x4(verts[i]) + Vector3.up * 0.714621f) * 1.3f;
                if (norms != null && i < norms.Length)
                {
                    norms[i] = m.MultiplyVector(norms[i]).normalized;
                }
            }
            mesh.vertices = verts;
            if (norms != null && norms.Length == verts.Length) mesh.normals = norms;
            mesh.RecalculateBounds();
            Object.DestroyImmediate(temp);

            Transform body = MakeBone("body", horseRoot.transform, new Vector3(0f, 0.98f, -0.02f));
            Transform neck = MakeBone("neck", body, new Vector3(0f, 0.24f, 0.44f));
            Transform head = MakeBone("head", neck, new Vector3(0f, 0.22f, 0.30f));
            Transform tail = MakeBone("tail", body, new Vector3(0f, 0.08f, -0.78f));
            Transform tailTip = MakeBone("tail_tip", tail, new Vector3(0f, -0.22f, -0.18f));

            Transform flU = MakeBone("fl_upper", body, new Vector3(-0.21f, -0.13f, 0.37f));
            Transform flL = MakeBone("fl_lower", flU, new Vector3(0f, -0.42f, 0.02f));
            Transform flH = MakeBone("fl_hoof", flL, new Vector3(0f, -0.37f, 0.03f));

            Transform frU = MakeBone("fr_upper", body, new Vector3(0.21f, -0.13f, 0.37f));
            Transform frL = MakeBone("fr_lower", frU, new Vector3(0f, -0.42f, 0.02f));
            Transform frH = MakeBone("fr_hoof", frL, new Vector3(0f, -0.37f, 0.03f));

            Transform rlU = MakeBone("rl_upper", body, new Vector3(-0.22f, -0.10f, -0.50f));
            Transform rlL = MakeBone("rl_lower", rlU, new Vector3(0f, -0.44f, -0.03f));
            Transform rlH = MakeBone("rl_hoof", rlL, new Vector3(0f, -0.38f, 0.02f));

            Transform rrU = MakeBone("rr_upper", body, new Vector3(0.22f, -0.10f, -0.50f));
            Transform rrL = MakeBone("rr_lower", rrU, new Vector3(0f, -0.44f, -0.03f));
            Transform rrH = MakeBone("rr_hoof", rrL, new Vector3(0f, -0.38f, 0.02f));

            Transform[] bones =
            {
                body, neck, head, tail, tailTip,
                flU, flL, flH,
                frU, frL, frH,
                rlU, rlL, rlH,
                rrU, rrL, rrH
            };

            BoneWeight[] bw = new BoneWeight[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                if (v.z < -0.78f)
                {
                    float t = Mathf.InverseLerp(-0.78f, -1.16f, v.z);
                    bw[i] = BlendWeight(3, 1f - t, 4, t);
                }
                else if (v.z > 0.36f && v.y > 1.08f)
                {
                    float t = Mathf.Clamp01((v.z - 0.56f) * 2.2f + (v.y - 1.38f) * 1.8f);
                    bw[i] = BlendWeight(1, 1f - t, 2, t);
                }
                else if (v.y < 0.88f)
                {
                    bool front = v.z >= -0.06f;
                    bool left = v.x < 0f;
                    int baseIdx = front ? (left ? 5 : 8) : (left ? 11 : 14);
                    if (v.y > 0.50f)
                    {
                        float t = Mathf.InverseLerp(0.88f, 0.68f, v.y);
                        bw[i] = BlendWeight(0, 1f - t, baseIdx, t);
                    }
                    else if (v.y > 0.16f)
                    {
                        float t = Mathf.InverseLerp(0.54f, 0.36f, v.y);
                        bw[i] = BlendWeight(baseIdx, 1f - t, baseIdx + 1, t);
                    }
                    else
                    {
                        float t = Mathf.InverseLerp(0.20f, 0.08f, v.y);
                        bw[i] = BlendWeight(baseIdx + 1, 1f - t, baseIdx + 2, t);
                    }
                }
                else
                {
                    float n = (v.z > 0.18f && v.y > 0.98f) ? Mathf.InverseLerp(0.18f, 0.48f, v.z) * 0.45f : 0f;
                    bw[i] = BlendWeight(0, 1f - n, 1, n);
                }
            }
            mesh.boneWeights = bw;

            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bindPoses[i] = bones[i].worldToLocalMatrix * horseRoot.transform.localToWorldMatrix;
            }
            mesh.bindposes = bindPoses;

            string skinnedMeshPath = $"{MeshyRoot}/IronHorse/IronHorse_SkinnedMesh.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(skinnedMeshPath);
            if (existingMesh != null)
            {
                EditorUtility.CopySerialized(mesh, existingMesh);
                EditorUtility.SetDirty(existingMesh);
                mesh = existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, skinnedMeshPath);
            }

            GameObject skinGo = new GameObject("Meshy Horse Skin");
            skinGo.transform.SetParent(horseRoot.transform, false);
            SkinnedMeshRenderer smr = skinGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = body;
            smr.sharedMaterial = horseMat;
            smr.updateWhenOffscreen = true;
            HorseSkinRepair.Repair(smr);

            // Attach Hoof Fire VFX to all 4 hooves + Dragon Fire Breath to head
            AttachHoofFireVfx(flH);
            AttachHoofFireVfx(frH);
            AttachHoofFireVfx(rlH);
            AttachHoofFireVfx(rrH);
            AttachHorseBreathVfx(head);

            return horseRoot;
        }

        private static Transform MakeBone(string name, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static BoneWeight BlendWeight(int i0, float w0, int i1, float w1)
        {
            float sum = Mathf.Max(0.0001f, w0 + w1);
            w0 /= sum;
            w1 /= sum;
            return w0 >= w1
                ? new BoneWeight { boneIndex0 = i0, weight0 = w0, boneIndex1 = i1, weight1 = w1 }
                : new BoneWeight { boneIndex0 = i1, weight0 = w1, boneIndex1 = i0, weight1 = w0 };
        }

        private static Material GetOrCreateParticleMaterial()
        {
            string texPath = $"{MaterialDir}/Tex_SoftGlowParticle.png";
            if (!File.Exists(texPath))
            {
                int size = 64;
                Texture2D glowTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float maxR = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        float core = Mathf.Clamp01(1f - d * 1.4f);
                        glowTex.SetPixel(x, y, new Color(1f, Mathf.Lerp(0.85f, 1f, core), Mathf.Lerp(0.55f, 1f, core), a));
                    }
                }
                glowTex.Apply();
                File.WriteAllBytes(texPath, glowTex.EncodeToPNG());
                Object.DestroyImmediate(glowTex);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.alphaIsTransparency = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                }
            }

            Texture2D loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = $"{MaterialDir}/Mat_Meshy_FireParticle.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (mat == null)
            {
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = s;
            }

            mat.color = Color.white;
            if (loadedTex != null)
            {
                mat.mainTexture = loadedTex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", loadedTex);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void AttachHoofFireVfx(Transform hoofBone)
        {
            if (hoofBone == null) return;
            GameObject vfxGo = new GameObject($"VFX_HoofFire_{hoofBone.name}");
            vfxGo.transform.SetParent(hoofBone, false);
            vfxGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.25f, 0.95f),
                new Color(1f, 0.35f, 0.08f, 0.9f));
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 16f;
            emission.rateOverDistance = 6f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.05f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.6f), new GradientColorKey(new Color(0.3f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ParticleSystemRenderer psr = vfxGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = GetOrCreateParticleMaterial();
        }

        private static void AttachHorseBreathVfx(Transform headBone)
        {
            if (headBone == null) return;
            GameObject vfxGo = new GameObject("VFX_HorseFireBreath");
            vfxGo.transform.SetParent(headBone, false);
            vfxGo.transform.localPosition = new Vector3(0f, -0.04f, 0.26f);
            vfxGo.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.78f, 0.2f, 0.9f),
                new Color(1f, 0.28f, 0.05f, 0.85f));
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 22f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.04f;

            ParticleSystemRenderer psr = vfxGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = GetOrCreateParticleMaterial();
        }

        private static void AttachHeroDivineAuraVfx(Transform heroRoot)
        {
            if (heroRoot == null) return;
            GameObject vfxGo = new GameObject("VFX_HeroDivineAura");
            vfxGo.transform.SetParent(heroRoot, false);
            vfxGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);

            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.42f, 0.85f),
                new Color(1f, 0.62f, 0.18f, 0.8f));
            main.gravityModifier = -0.25f;
            main.maxParticles = 50;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.42f;

            ParticleSystemRenderer psr = vfxGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = GetOrCreateParticleMaterial();
        }

        private static void AttachSpearTrailVfx(Transform weaponRoot)
        {
            if (weaponRoot == null) return;
            GameObject tipGo = new GameObject("VFX_SpearTipTrail");
            tipGo.transform.SetParent(weaponRoot, false);
            tipGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);

            TrailRenderer tr = tipGo.AddComponent<TrailRenderer>();
            tr.time = 0.26f;
            tr.minVertexDistance = 0.03f;
            tr.startWidth = 0.18f;
            tr.endWidth = 0.0f;
            tr.emitting = true;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.sharedMaterial = GetOrCreateParticleMaterial();

            Gradient g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0f),
                    new GradientColorKey(new Color(1f, 0.65f, 0.15f), 0.55f),
                    new GradientColorKey(new Color(0.95f, 0.25f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.55f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            tr.colorGradient = g;
        }
    }
}
