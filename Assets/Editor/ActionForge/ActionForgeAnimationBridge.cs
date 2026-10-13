using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    /// <summary>Retarget ActionForge's Knight exports; never bind mannequin paths blindly.</summary>
    public static class ActionForgeAnimationBridge
    {
        public const string Folder = "Assets/Animations/ActionForge";
        public const string HeroPrefab = "Assets/Prefabs/Player/KayKit_ThanhGiong_Hero.prefab";
        public const string MountedModel = "Assets/GodotPort/Models/MountedGiong.glb";
        public const string MountedClipPath = "Assets/Resources/ActionForge/SwordAttackMounted.asset";
        public const string HeroClipPath = Folder + "/Generated/SwordAttack_KayKit.anim";

        // Source bone, source child, mounted bone, mounted child. Parent-first order.
        static readonly string[][] UpperBody = {
            new[]{"spine", "chest", "spine", "chest"},
            new[]{"chest", "head", "chest", "head"},
            new[]{"head", "", "head", ""},
            new[]{"upperarm.r", "lowerarm.r", "upperarm_r", "forearm_r"},
            new[]{"lowerarm.r", "wrist.r", "forearm_r", "hand_r"},
            new[]{"hand.r", "", "hand_r", ""},
            new[]{"upperarm.l", "lowerarm.l", "upperarm_l", "forearm_l"},
            new[]{"lowerarm.l", "wrist.l", "forearm_l", "hand_l"},
            new[]{"hand.l", "", "hand_l", ""}
        };

        public static string IntegrateSwordAttack()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Dừng Play Mode trước khi tích hợp.");
            string sourcePath = null;
            AnimationClip sourceClip = null;
            foreach (string path in Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                string assetPath = path.Replace('\\', '/');
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (model == null || Find(model.transform, "hips") == null || Find(model.transform, "upperarm.r") == null) continue;
                var clip = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>()
                    .FirstOrDefault(x => !x.name.StartsWith("__preview") && x.name.ToLowerInvariant().Contains("sword") && x.name.ToLowerInvariant().Contains("attack"));
                if (clip == null) continue;
                sourcePath = assetPath; sourceClip = clip; break;
            }
            if (sourceClip == null) throw new InvalidOperationException("Cần FBX Sword Attack xuất trên Knight.glb (Own skeleton). FBX mannequin không dùng trực tiếp cho KayKit.");

            GameObject source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
            GameObject hero = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefab));
            GameObject mounted = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MountedModel));
            source.hideFlags = hero.hideFlags = mounted.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sourceAnimator = source.GetComponent<Animator>();
                if (sourceAnimator != null) sourceAnimator.enabled = false;
                var rider = Find(mounted.transform, "ThanhGiong");
                if (rider == null) throw new InvalidOperationException("Không tìm thấy rig người cưỡi ngựa.");
                Directory.CreateDirectory(Folder + "/Generated");
                Directory.CreateDirectory("Assets/Resources/ActionForge");

                var heroClip = BakeHero(source, sourceClip, hero.transform);
                SaveAsset(heroClip, HeroClipPath);
                var mountedClip = BakeMounted(source, sourceClip, rider, sourcePath);
                int trackCount = mountedClip.tracks.Length;
                SaveAsset(mountedClip, MountedClipPath);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/ThanhGiongKnight.controller");
                if (controller == null) throw new InvalidOperationException("Thiếu ThanhGiongKnight.controller.");
                var attack = controller.layers[0].stateMachine.states.FirstOrDefault(x => x.state.name == "Attack").state;
                if (attack == null) throw new InvalidOperationException("Thiếu state Attack.");
                Undo.RecordObject(attack, "Link ActionForge sword attack");
                attack.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipPath);
                foreach (var transition in controller.layers[0].stateMachine.anyStateTransitions)
                    if (transition.destinationState == attack) { transition.canTransitionToSelf = false; EditorUtility.SetDirty(transition); }
                EditorUtility.SetDirty(attack); EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                string report = "ActionForge: " + sourceClip.name + " (" + sourceClip.length.ToString("F2") + "s), KayKit Attack + " + trackCount + " mounted upper-body tracks. Root/horse/legs preserved.";
                Debug.Log(report);
                return report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(hero);
                UnityEngine.Object.DestroyImmediate(mounted);
            }
        }

        static AnimationClip BakeHero(GameObject source, AnimationClip clip, Transform target)
        {
            var result = new AnimationClip { name = "ActionForge_SwordAttack_KayKit", frameRate = 30f };
            // Both models use the Knight skeleton. Their mesh and armature containers differ.
            // Copy rotations by unique bone name; retain target proportions and root position.
            int count = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !binding.propertyName.StartsWith("m_LocalRotation.")) continue;
                string name = binding.path.Split('/').Last();
                if (name == "root" || string.IsNullOrEmpty(name)) continue;
                var bone = Find(target, name);
                if (bone == null) throw new InvalidOperationException("KayKit thiếu xương: " + name);
                var remapped = binding;
                remapped.path = AnimationUtility.CalculateTransformPath(bone, target);
                AnimationUtility.SetEditorCurve(result, remapped, AnimationUtility.GetEditorCurve(clip, binding));
                count++;
            }
            if (count < 32) throw new InvalidOperationException("Clip chưa đủ đường cong xương để tích hợp.");
            var settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            result.EnsureQuaternionContinuity();
            return result;
        }

        static ActionForgeMountedClip BakeMounted(GameObject source, AnimationClip clip, Transform rider, string sourcePath)
        {
            var result = ScriptableObject.CreateInstance<ActionForgeMountedClip>();
            result.sourceAsset = sourcePath; result.sourceDuration = clip.length;
            int frames = Mathf.CeilToInt(clip.length * 30f) + 1;
            result.tracks = UpperBody.Select(row => new ActionForgeMountedClip.Track { bone = row[2], rotations = new Quaternion[frames] }).ToArray();
            var sourceBones = UpperBody.Select(row => Find(source.transform, row[0])).ToArray();
            var targetBones = UpperBody.Select(row => Find(rider, row[2])).ToArray();
            var offsets = new Quaternion[UpperBody.Length];
            for (int i = 0; i < UpperBody.Length; i++)
            {
                if (sourceBones[i] == null || targetBones[i] == null) throw new InvalidOperationException("Thiếu xương " + UpperBody[i][0]);
                var sourceChild = Find(source.transform, UpperBody[i][1]);
                var targetChild = Find(rider, UpperBody[i][3]);
                // Align the child axis, so the seated rig need not share Knight's T-pose.
                Vector3 from = targetChild ? targetBones[i].InverseTransformDirection(targetChild.position - targetBones[i].position).normalized : Vector3.up;
                Vector3 to = sourceChild ? sourceBones[i].InverseTransformDirection(sourceChild.position - sourceBones[i].position).normalized : Vector3.up;
                offsets[i] = Quaternion.FromToRotation(from, to);
            }
            Quaternion[] originalRotations = targetBones.Select(x => x.localRotation).ToArray();
            float largestStrike = 0f;
            Quaternion previousArm = Quaternion.identity;
            for (int frame = 0; frame < frames; frame++)
            {
                clip.SampleAnimation(source, clip.length * frame / (frames - 1));
                for (int i = 0; i < UpperBody.Length; i++) targetBones[i].localRotation = originalRotations[i];
                for (int i = 0; i < UpperBody.Length; i++)
                {
                    var desired = rider.rotation * Quaternion.Inverse(source.transform.rotation) * sourceBones[i].rotation * offsets[i];
                    targetBones[i].rotation = desired;
                    result.tracks[i].rotations[frame] = targetBones[i].localRotation;
                }
                float t = (float)frame / (frames - 1);
                float change = Quaternion.Angle(previousArm, sourceBones[3].rotation);
                if (frame > 1 && t > .15f && t < .8f && change > largestStrike)
                { largestStrike = change; result.impactNormalizedTime = t; }
                previousArm = sourceBones[3].rotation;
            }
            return result;
        }

        static Transform Find(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            return all.FirstOrDefault(x => x.name == name) ?? all.FirstOrDefault(x => x.name.StartsWith(name, StringComparison.Ordinal) && x.name.Substring(name.Length).All(char.IsDigit));
        }

        static void SaveAsset(UnityEngine.Object generated, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null) AssetDatabase.CreateAsset(generated, path);
            else { EditorUtility.CopySerialized(generated, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(generated); }
        }
    }
}
