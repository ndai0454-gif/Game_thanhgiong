using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    public static class MeshyMountedInstaller
    {
        public const string PrefabPath = "Assets/Prefabs/Player/Meshy_ThanhGiong_Mounted.prefab";
        const string MeshFolder = "Assets/Art/Meshy";
        const string Generated = MeshFolder + "/Generated";
        static readonly string[][] BoneMap = {
            new[]{"Hips","pelvis"},new[]{"Spine","spine"},new[]{"Spine2","chest"},new[]{"Head","head"},
            new[]{"RightArm","upperarm_r"},new[]{"RightForeArm","forearm_r"},new[]{"RightHand","hand_r"},
            new[]{"LeftArm","upperarm_l"},new[]{"LeftForeArm","forearm_l"},new[]{"LeftHand","hand_l"},
            new[]{"RightUpLeg","thigh_r"},new[]{"RightLeg","shin_r"},new[]{"LeftUpLeg","thigh_l"},new[]{"LeftLeg","shin_l"}
        };

        [MenuItem("Tools/Thánh Gióng/Meshy/Build Mounted Hero")]
        public static void BuildPrefab()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Dừng Play Mode trước khi dựng prefab.");
            Directory.CreateDirectory(Generated);
            var root = new GameObject("Meshy Mounted Giong");
            root.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var mount = Child(root.transform, "Mount", Vector3.zero);
                BuildHorse(mount);
                var heroRoot = Child(mount, "ThanhGiong", Vector3.zero);
                var hero = UnityEngine.Object.Instantiate(LoadModel("ThanhGiong_Meshy"), heroRoot, false);
                hero.name = "Meshy Hero";
                foreach (var animator in hero.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                var controls = new Dictionary<string, Transform>();
                var deform = new Dictionary<string, Transform>();
                foreach (var row in BoneMap)
                {
                    var bone = FindMixamo(hero.transform, row[0]);
                    if (bone == null) throw new InvalidOperationException("Thiếu xương Meshy " + row[0]);
                    deform[row[1]] = bone;
                    var parent = bone.parent;
                    var control = new GameObject(row[1]).transform;
                    control.SetParent(parent, false);
                    control.SetPositionAndRotation(bone.position, Quaternion.identity);
                    bone.SetParent(control, true);
                    controls[row[1]] = control;
                }
                // Put the hips on the saddle, then bend actual limbs toward the stirrups.
                heroRoot.localPosition += new Vector3(0f, 1.30f - controls["pelvis"].position.y, -.12f);
                SeatLeg(hero.transform, controls["thigh_r"], controls["shin_r"], true);
                SeatLeg(hero.transform, controls["thigh_l"], controls["shin_l"], false);

                var socket = new GameObject("Right Hand Weapon Socket").transform;
                socket.SetParent(deform["hand_r"], false);
                socket.position = deform["hand_r"].position;
                socket.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, .97f, .24f).normalized);
                BuildWeapon(socket, "Weapon_GoldenBamboo", 1.20f);
                BuildWeapon(socket, "Weapon_IronSword", .96f);
                var animatorRoot = hero.GetComponent<Animator>();
                if (animatorRoot == null) animatorRoot = hero.AddComponent<Animator>();
                animatorRoot.runtimeAnimatorController = MeshyActionForgeAnimator.Build(hero);
                // Calibrate the grip against the authored idle, rather than the T pose.
                var poseBones = hero.GetComponentsInChildren<Transform>(true);
                var poseRotations = poseBones.Select(x => x.localRotation).ToArray();
                MeshyActionForgeAnimator.GetClip("Idle").SampleAnimation(hero, 0f);
                socket.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(0f, .97f, .24f).normalized);
                Quaternion gripRotation = socket.localRotation;
                for (int i = 0; i < poseBones.Length; i++) poseBones[i].localRotation = poseRotations[i];
                socket.localRotation = gripRotation;
                animatorRoot.applyRootMotion = false;
                animatorRoot.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var bridge = root.AddComponent<MeshyMountedRig>();
                bridge.heroAnimator = animatorRoot;
                bridge.armControls = new[]{"upperarm_r","forearm_r","hand_r","upperarm_l","forearm_l","hand_l"}.Select(x => controls[x]).ToArray();
                bridge.attackClipLengths = new[] { MeshyActionForgeAnimator.GetClip("AttackA").length, MeshyActionForgeAnimator.GetClip("AttackB").length, MeshyActionForgeAnimator.GetClip("AttackC").length };
                bridge.spearClipLength = MeshyActionForgeAnimator.GetClip("BambooAttack").length;
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                root.hideFlags = HideFlags.None;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("MESHY_BUILD PASS: new rider, skinned horse, hand-mounted spear and ActionForge controller.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static GameObject LoadModel(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MeshFolder + "/" + name + ".glb");
            if (model == null) throw new InvalidOperationException("Chưa import model: " + name);
            return model;
        }

        static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = position;
            return child;
        }

        static Transform FindMixamo(Transform root, string suffix)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.Replace("mixamorig:", "").Replace("mixamorig_", "") == suffix);
        }

        static void SeatLeg(Transform hero, Transform thigh, Transform shin, bool right)
        {
            var knee = FindMixamo(hero, right ? "RightLeg" : "LeftLeg");
            var hip = FindMixamo(hero, right ? "RightUpLeg" : "LeftUpLeg");
            var foot = FindMixamo(hero, right ? "RightFoot" : "LeftFoot");
            float side = right ? 1f : -1f;
            var direction = new Vector3(side * .22f, -.16f, .33f).normalized;
            thigh.rotation = Quaternion.FromToRotation(knee.position - hip.position, direction) * thigh.rotation;
            direction = new Vector3(side * .025f, -.42f, -.06f).normalized;
            shin.rotation = Quaternion.FromToRotation(foot.position - knee.position, direction) * shin.rotation;
        }

        static void BuildWeapon(Transform socket, string name, float scale)
        {
            var weapon = new GameObject(name).transform;
            weapon.SetParent(socket, false);
            var model = UnityEngine.Object.Instantiate(LoadModel("BambooSpear_Meshy"), weapon, false);
            model.name = "Meshy Spear";
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = Vector3.up * (.27f * scale);
            if (name == "Weapon_GoldenBamboo") weapon.gameObject.SetActive(false);
        }

        static void BuildHorse(Transform mount)
        {
            var root = Child(mount, "IronHorse", Vector3.zero);
            var model = UnityEngine.Object.Instantiate(LoadModel("IronHorse_Meshy"), root, false);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 1) throw new InvalidOperationException("Ngựa cần một mesh tối ưu để dựng skin.");
            var source = filters[0];
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            mesh.name = "Meshy Iron Horse Skinned";
            var matrix = root.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            // Meshy model is 1.415m high, centered on the origin. Normalize its ground.
            const float scale = 1.3f, ground = .714621f;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = (vertices[i] + Vector3.up * ground) * scale;
            mesh.vertices = vertices;
            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = matrix.MultiplyVector(normals[i]).normalized;
            mesh.normals = normals;
            var bones = new List<Transform>();
            var body = Child(root, "body", new Vector3(0f, 1.17f, 0f)); bones.Add(body);
            var neck = Child(root, "neck", new Vector3(0f, 1.38f, .56f)); neck.SetParent(body, true); bones.Add(neck);
            var head = Child(root, "head", new Vector3(0f, 1.68f, .88f)); head.SetParent(neck, true); bones.Add(head);
            var tail = Child(root, "tail", new Vector3(0f, 1.28f, -.98f)); tail.SetParent(body, true); bones.Add(tail);
            var tip = Child(root, "tail_tip", new Vector3(0f, 1.02f, -1.15f)); tip.SetParent(tail, true); bones.Add(tip);
            // Anchor estimates from the source geometry, including its raised foreleg.
            Vector3[] hip = { new Vector3(.16f,1.02f,.68f),new Vector3(-.16f,1.02f,.66f),new Vector3(.20f,1.03f,-.56f),new Vector3(-.21f,1.03f,-.43f) };
            Vector3[] knee = { new Vector3(.16f,.49f,.76f),new Vector3(-.16f,.49f,.79f),new Vector3(.20f,.48f,-.61f),new Vector3(-.21f,.48f,-.39f) };
            Vector3[] hoof = { new Vector3(.157f,.03f,.798f),new Vector3(-.170f,.222f,.631f),new Vector3(.187f,.077f,-.638f),new Vector3(-.217f,.102f,-.230f) };
            string[] names = { "fl", "fr", "rl", "rr" };
            for (int i = 0; i < 4; i++)
            {
                var upper = Child(root, names[i] + "_upper", hip[i]); upper.SetParent(body, true); bones.Add(upper);
                var lower = Child(root, names[i] + "_lower", knee[i]); lower.SetParent(upper, true); bones.Add(lower);
                var foot = Child(root, names[i] + "_hoof", hoof[i]); foot.SetParent(lower, true); bones.Add(foot);
            }
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                float legBlend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.83f, 1.22f, v.y));
                if (v.y < 1.22f && Mathf.Abs(v.x) > .055f && legBlend > .001f)
                {
                    int best = 0; float distance = float.MaxValue;
                    for (int l = 0; l < 4; l++) { float d = new Vector2(v.x - knee[l].x, v.z - knee[l].z).sqrMagnitude; if (d < distance) { distance = d; best = l; } }
                    float upper = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,.83f,v.y));
                    float foot = 1f - Mathf.SmoothStep(0,1,Mathf.InverseLerp(hoof[best].y + .03f,hoof[best].y + .23f,v.y));
                    foot *= 1f - upper;
                    weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - legBlend, boneIndex1 = 5 + best * 3, weight1 = upper * legBlend, boneIndex2 = 6 + best * 3, weight2 = (1f - upper - foot) * legBlend, boneIndex3 = 7 + best * 3, weight3 = foot * legBlend };
                }
                else if (v.z > .42f && v.y > 1.2f)
                {
                    float n = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.72f,v.z)) * Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.18f,1.40f,v.y));
                    float h = Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.48f,1.73f,v.y));
                    weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - n, boneIndex1 = 1, weight1 = n * (1f - h), boneIndex2 = 2, weight2 = n * h };
                }
                else if (v.z < -.9f && v.y > .72f)
                {
                    float t = Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.88f,-1.10f,v.z));
                    weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f-t, boneIndex1 = 3, weight1 = t*.65f, boneIndex2 = 4, weight2 = t*.35f };
                }
                else weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            var skinObject = Child(root, "Meshy Horse Skin", Vector3.zero);
            var skin = skinObject.gameObject.AddComponent<SkinnedMeshRenderer>();
            mesh.boneWeights = weights;
            mesh.bindposes = bones.Select(x => x.worldToLocalMatrix * skinObject.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            string path = Generated + "/IronHorseSkinned.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
            skin.sharedMesh = mesh; skin.sharedMaterials = sourceRenderer.sharedMaterials;
            skin.bones = bones.ToArray(); skin.rootBone = body; skin.updateWhenOffscreen = true;
            skin.localBounds = new Bounds(new Vector3(0,1f,0),new Vector3(2.5f,3.2f,3.8f));
            UnityEngine.Object.DestroyImmediate(model);
        }

        [MenuItem("Tools/Thánh Gióng/Meshy/Install In All Campaign Maps")]
        public static void InstallAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Dừng Play Mode trước khi cài vào scene.");
            BuildPrefab();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.SaveOpenScenes();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            string backup = "MeshyVerification/SceneBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            int count = 0;
            try
            {
                foreach (string map in GodotPortImporter.Maps)
                {
                    string path = "Assets/Scenes/ThanhGiongWorld/" + map + ".unity";
                    File.Copy(path, backup + "/" + map + ".unity", false);
                    var scene = EditorSceneManager.OpenScene(path);
                    var campaign = UnityEngine.Object.FindAnyObjectByType<ThanhGiongCampaignController>(FindObjectsInactive.Include);
                    if (campaign == null) throw new InvalidOperationException("Thiếu actor trong " + map);
                    InstallOnActor(campaign, prefab);
                    EditorSceneManager.SaveScene(scene); count++;
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("MESHY_INSTALL PASS maps=" + count + " backup=" + backup);
        }

        public static void InstallOnActor(ThanhGiongCampaignController campaign, GameObject prefab)
        {
            var controller = campaign.GetComponent<MountedHorseController>();
            var old = controller.visual != null ? controller.visual : campaign.transform.Find("Visual");
            Vector3 position = old != null ? old.localPosition : Vector3.zero;
            Vector3 scale = old != null ? old.localScale : Vector3.one;
            Quaternion rotation = old != null ? old.localRotation : Quaternion.identity;
            if (old != null && old != campaign.transform)
            {
                if (old.GetComponentInChildren<MeshyMountedRig>(true) != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                else { old.name = "Previous Visual - Before Meshy"; old.gameObject.SetActive(false); }
            }
            var visual = Child(campaign.transform, "Visual", position);
            visual.localScale = scale; visual.localRotation = rotation; visual.SetSiblingIndex(0);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, campaign.gameObject.scene);
            model.transform.SetParent(visual, false);
            controller.visual = visual;
            controller.frontLeftLeg = controller.frontRightLeg = controller.rearLeftLeg = controller.rearRightLeg = null;
            var motion = campaign.GetComponent<GodotMountedMotion>();
            if (motion == null) motion = campaign.gameObject.AddComponent<GodotMountedMotion>();
            motion.visual = visual;
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(motion);
        }
    }
}
