using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ThanhGiong.EditorTools
{
    /// <summary>Exact-rig combat clips. Does not modify any scene or prefab.</summary>
    public static class GiacAnGeneralCombatBuilder
    {
        public const string OutputFolder = GiacAnGeneralBuilder.OutputFolder + "/Combat";
        public const string ControllerPath = OutputFolder + "/GiacAnGeneral_Combat.controller";
        public const string MotionSetPath = "Assets/Resources/ActionForge/EnemyMotionSets/GiacAnGeneral.asset";
        public const string UpgradeFbxPath = "Assets/Animations/actionforge_8_clips_unity_fit_inplace_anim_only.fbx";
        public const float StrikeContact = .35484f;
        const float SourceContact = .35f;
        static readonly string[] PhaseNames = { "Windup", "Strike", "Stuck", "Recovery", "Hit", "Death" };

        [MenuItem("Tools/Thánh Gióng/ActionForge/Build Giac An General Combat")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building combat animations.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GiacAnGeneralBuilder.SourcePath);
            if (source == null) throw new InvalidOperationException("Missing fitted Giac An general FBX.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(GiacAnGeneralBuilder.SourcePath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal)).ToDictionary(c => c.name);
            // Newer 8-clip ActionForge export overrides matching clips (same skeleton paths); walk/sword_idle stay on the old source.
            foreach (var extra in AssetDatabase.LoadAllAssetsAtPath(UpgradeFbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal)))
                clips[extra.name == "idle_loop_fit" ? "sword_idle_fit" : extra.name] = extra;
            foreach (string name in new[] { "sword_idle", "sword_attack", "walk_loop", "jog_fwd_loop", "sprint_loop", "hit_chest", "death01" })
                if (!clips.ContainsKey(name + "_fit")) throw new InvalidOperationException("Missing source clip: " + name);
            EnsureFolder(OutputFolder);
            var idle = clips["sword_idle_fit"];
            var attack = clips["sword_attack_fit"];
            using (var baker = new ExactRigBaker(source, idle))
            {
                SaveClip(baker.Bake(idle, "Idle", p => p, true));
                foreach (var entry in new Dictionary<string, string> {
                    { "Walk", "walk_loop_fit" }, { "Jog", "jog_fwd_loop_fit" }, { "Sprint", "sprint_loop_fit" } })
                    SaveClip(baker.Bake(clips[entry.Value], entry.Key, p => p, true));
                SaveClip(baker.Bake(attack, "Windup", p => Mathf.Lerp(0f, .20f, p), false));
                SaveClip(baker.Bake(attack, "Strike", p => p <= StrikeContact
                    ? Mathf.Lerp(.20f, SourceContact, p / StrikeContact)
                    : Mathf.Lerp(SourceContact, .65f, (p - StrikeContact) / (1f - StrikeContact)), false, false, StrikeContact));
                // A dodged slam enters Stuck at its gameplay contact time, before the
                // full Strike finishes. Hold that contact pose and recover from it.
                SaveClip(baker.Bake(attack, "Stuck", p => SourceContact, false));
                SaveClip(baker.Bake(attack, "Recovery", p => Mathf.Lerp(SourceContact, 1f, p), false, true));
                SaveClip(baker.Bake(clips["hit_chest_fit"], "Hit", p => p, false));
                SaveClip(baker.Bake(clips["death01_fit"], "Death", p => p, false));
            }
            var controller = BuildController();
            EnsureFolder(Path.GetDirectoryName(MotionSetPath).Replace('\\', '/'));
            var set = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(MotionSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ActionForgeEnemyMotionSet>();
                set.name = "GiacAnGeneral";
                AssetDatabase.CreateAsset(set, MotionSetPath);
            }
            set.boss = controller;
            // Every role uses this rig's controller if the visual is ever reused on a nonboss.
            set.spearman = set.shieldBearer = set.archer = set.raider = controller;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return Verify();
        }

        sealed class ExactRigBaker : IDisposable
        {
            readonly GameObject model;
            readonly Transform[] transforms;
            readonly Vector3[] positions, scales, idlePositions, idleScales;
            readonly Quaternion[] rotations, idleRotations;
            readonly Dictionary<string, int> indices;
            readonly HashSet<string> horizontalAnchors;

            public ExactRigBaker(GameObject source, AnimationClip idle)
            {
                model = Object.Instantiate(source);
                model.hideFlags = HideFlags.HideAndDontSave;
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                transforms = model.GetComponentsInChildren<Transform>(true);
                indices = transforms.Select((t, i) => new { Path = AnimationUtility.CalculateTransformPath(t, model.transform), Index = i })
                    .ToDictionary(p => p.Path, p => p.Index);
                positions = transforms.Select(t => t.localPosition).ToArray();
                rotations = transforms.Select(t => t.localRotation).ToArray();
                scales = transforms.Select(t => t.localScale).ToArray();
                idle.SampleAnimation(model, 0f);
                idlePositions = transforms.Select(t => t.localPosition).ToArray();
                idleRotations = transforms.Select(t => t.localRotation).ToArray();
                idleScales = transforms.Select(t => t.localScale).ToArray();
                // Scripted navigation owns horizontal travel; keep vertical body motion and all
                // cape/armour transforms from the fitted source, while anchoring the skeleton base.
                horizontalAnchors = new HashSet<string>(transforms.Where(t => t != model.transform &&
                    (t.name.Equals("root", StringComparison.OrdinalIgnoreCase) ||
                     t.name.Equals("pelvis", StringComparison.OrdinalIgnoreCase) ||
                     t.name.Equals("hips", StringComparison.OrdinalIgnoreCase)))
                    .Select(t => AnimationUtility.CalculateTransformPath(t, model.transform)));
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (skin.rootBone != null)
                        horizontalAnchors.Add(AnimationUtility.CalculateTransformPath(skin.rootBone, model.transform));
            }

            public AnimationClip Bake(AnimationClip source, string name, Func<float, float> sourceTime,
                bool loop, bool blendToIdle = false, float explicitKey = -1f)
            {
                var bindings = AnimationUtility.GetCurveBindings(source).Where(b => b.type == typeof(Transform) &&
                    !string.IsNullOrEmpty(b.path)).ToArray();
                if (bindings.Length == 0 || bindings.Any(b => !indices.ContainsKey(b.path)))
                    throw new InvalidOperationException("Missing exact-rig paths in " + source.name);
                foreach (var b in bindings)
                    if (!b.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal) &&
                        !b.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal) &&
                        !b.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal))
                        throw new InvalidOperationException("Unsupported transform channel: " + b.propertyName);
                float duration = loop || name == "Death" ? source.length : 1f;
                int count = Mathf.Max(2, Mathf.CeilToInt(duration * 30f) + 1);
                var phases = Enumerable.Range(0, count).Select(i => (float)i / (count - 1)).ToList();
                if (explicitKey > 0f && explicitKey < 1f) phases.Add(explicitKey);
                phases = phases.Distinct().OrderBy(p => p).ToList();
                var curves = bindings.Select(b => new AnimationCurve()).ToArray();
                var previous = new Quaternion[transforms.Length];
                bool first = true;
                foreach (float phase in phases)
                {
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        transforms[i].localPosition = positions[i];
                        transforms[i].localRotation = rotations[i];
                        transforms[i].localScale = scales[i];
                    }
                    source.SampleAnimation(model, Mathf.Clamp01(sourceTime(phase)) * source.length);
                    float blend = blendToIdle ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, phase)) : 0f;
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        transforms[i].localPosition = Vector3.Lerp(transforms[i].localPosition, idlePositions[i], blend);
                        transforms[i].localRotation = Quaternion.Slerp(transforms[i].localRotation, idleRotations[i], blend);
                        transforms[i].localScale = Vector3.Lerp(transforms[i].localScale, idleScales[i], blend);
                        var q = transforms[i].localRotation.normalized;
                        if (!first && Quaternion.Dot(previous[i], q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        previous[i] = q;
                    }
                    for (int b = 0; b < bindings.Length; b++)
                    {
                        var binding = bindings[b];
                        int i = indices[binding.path];
                        char axis = binding.propertyName[binding.propertyName.Length - 1];
                        int channel = "xyzw".IndexOf(axis);
                        float value;
                        if (binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)) value = previous[i][channel];
                        else if (binding.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal)) value = transforms[i].localScale[channel];
                        else value = horizontalAnchors.Contains(binding.path) && axis != 'y'
                            ? idlePositions[i][channel] : transforms[i].localPosition[channel];
                        if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Nonfinite curve in " + name);
                        curves[b].AddKey(phase * duration, value);
                    }
                    first = false;
                }
                var clip = new AnimationClip { name = "GiacAnGeneral " + name, frameRate = 30f, legacy = false,
                    wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever };
                for (int b = 0; b < bindings.Length; b++)
                {
                    for (int k = 0; k < curves[b].length; k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curves[b], k, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curves[b], k, AnimationUtility.TangentMode.Linear);
                    }
                    AnimationUtility.SetEditorCurve(clip, bindings[b], curves[b]);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                settings.loopBlend = loop;
                settings.startTime = 0f;
                settings.stopTime = duration;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.EnsureQuaternionContinuity();
                return clip;
            }

            public void Dispose() => Object.DestroyImmediate(model);
        }

        static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputFolder + "/" + name + ".anim");

        static void SaveClip(AnimationClip generated)
        {
            string name = generated.name.Substring("GiacAnGeneral ".Length);
            string path = OutputFolder + "/" + name + ".anim";
            var existing = Clip(name);
            if (existing == null) AssetDatabase.CreateAsset(generated, path);
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(generated);
            }
        }

        static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            if (controller.layers.Length != 1) throw new InvalidOperationException("Expected a single combat layer.");
            controller.parameters = new[] {
                new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float },
                new AnimatorControllerParameter { name = "MoveRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f },
                new AnimatorControllerParameter { name = "State", type = AnimatorControllerParameterType.Int },
                new AnimatorControllerParameter { name = "Phase", type = AnimatorControllerParameterType.Float }
            };
            var machine = controller.layers[0].stateMachine;
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (var child in machine.states) machine.RemoveState(child.state);
            foreach (var child in machine.stateMachines) machine.RemoveStateMachine(child.stateMachine);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>()) Object.DestroyImmediate(tree, true);
            var locomotion = machine.AddState("Locomotion");
            var blendTree = new BlendTree { name = "General Locomotion", hideFlags = HideFlags.HideInHierarchy,
                blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.children = new[] {
                new ChildMotion { motion = Clip("Idle"), threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = Clip("Walk"), threshold = .4f, timeScale = 1f },
                new ChildMotion { motion = Clip("Jog"), threshold = .75f, timeScale = 1f },
                new ChildMotion { motion = Clip("Sprint"), threshold = 1f, timeScale = 1f }
            };
            locomotion.motion = blendTree;
            locomotion.writeDefaultValues = false;
            locomotion.speedParameter = "MoveRate";
            locomotion.speedParameterActive = true;
            machine.defaultState = locomotion;
            var map = new Dictionary<string, ThanhGiongEnemy.EnemyState> {
                { "Windup", ThanhGiongEnemy.EnemyState.TelegraphingAttack }, { "Strike", ThanhGiongEnemy.EnemyState.Slamming },
                { "Stuck", ThanhGiongEnemy.EnemyState.StuckInGround }, { "Recovery", ThanhGiongEnemy.EnemyState.Recovering },
                { "Hit", ThanhGiongEnemy.EnemyState.Stunned }, { "Death", ThanhGiongEnemy.EnemyState.Dead }
            };
            foreach (var name in new[] { "Death", "Hit", "Windup", "Strike", "Stuck", "Recovery", "Locomotion" })
            {
                var state = name == "Locomotion" ? locomotion : machine.AddState(name);
                if (name != "Locomotion")
                {
                    state.motion = Clip(name);
                    state.writeDefaultValues = false;
                    state.timeParameter = "Phase";
                    state.timeParameterActive = name != "Death";
                }
                var transition = machine.AddAnyStateTransition(state);
                transition.conditions = new[] { new AnimatorCondition { parameter = "State",
                    mode = name == "Locomotion" ? AnimatorConditionMode.Less : AnimatorConditionMode.Equals,
                    threshold = name == "Locomotion" ? (int)ThanhGiongEnemy.EnemyState.TelegraphingAttack : (int)map[name] } };
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = name == "Hit" || name == "Strike" ? .06f : name == "Locomotion" || name == "Death" ? .16f : .09f;
                transition.canTransitionToSelf = false;
                transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                transition.orderedInterruption = false;
                EditorUtility.SetDirty(state);
                EditorUtility.SetDirty(transition);
            }
            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        public static string Verify()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GiacAnGeneralBuilder.SourcePath);
            int channels = 0;
            foreach (var name in new[] { "Idle", "Walk", "Jog", "Sprint" }.Concat(PhaseNames))
            {
                var clip = Clip(name);
                if (clip == null || clip.length <= 0f) throw new InvalidOperationException("Missing generated " + name);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                if (bindings.Length == 0 || bindings.Any(b => b.type != typeof(Transform) || string.IsNullOrEmpty(b.path) || source.transform.Find(b.path) == null))
                    throw new InvalidOperationException("Unbound or model-root curve in " + name);
                foreach (var b in bindings)
                    if (AnimationUtility.GetEditorCurve(clip, b).keys.Any(k => float.IsNaN(k.value) || float.IsInfinity(k.value)))
                        throw new InvalidOperationException("Invalid generated channel in " + name);
                bool loop = name == "Idle" || name == "Walk" || name == "Jog" || name == "Sprint";
                if (AnimationUtility.GetAnimationClipSettings(clip).loopTime != loop)
                    throw new InvalidOperationException("Wrong loop setting in " + name);
                channels += bindings.Length;
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            if (machine.states.Length != 7 || machine.anyStateTransitions.Length != 7 || machine.defaultState.name != "Locomotion")
                throw new InvalidOperationException("Incomplete combat state machine.");
            var expected = new Dictionary<string, AnimatorControllerParameterType> { { "State", AnimatorControllerParameterType.Int },
                { "Phase", AnimatorControllerParameterType.Float }, { "Speed", AnimatorControllerParameterType.Float }, { "MoveRate", AnimatorControllerParameterType.Float } };
            if (controller.parameters.Length != 4 || controller.parameters.Any(p => !expected.ContainsKey(p.name) || expected[p.name] != p.type))
                throw new InvalidOperationException("Controller does not match the enemy animation driver.");
            foreach (var child in machine.states)
            {
                var state = child.state;
                if (state.timeParameterActive != (state.name != "Locomotion" && state.name != "Death") || state.transitions.Length != 0)
                    throw new InvalidOperationException("Wrong phase clock in " + state.name);
                if (state.name == "Locomotion")
                {
                    var tree = state.motion as BlendTree;
                    if (tree == null || tree.children.Length != 4 || tree.children.Any(c => c.motion == null))
                        throw new InvalidOperationException("Incomplete general locomotion tree.");
                }
                else if (state.motion != Clip(state.name)) throw new InvalidOperationException("Wrong clip for " + state.name);
            }
            var set = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(MotionSetPath);
            if (set == null || set.boss != controller) throw new InvalidOperationException("Boss motion-set assignment missing.");
            var sourceAttack = AssetDatabase.LoadAllAssetsAtPath(UpgradeFbxPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == "sword_attack_fit") ?? AssetDatabase.LoadAllAssetsAtPath(GiacAnGeneralBuilder.SourcePath).OfType<AnimationClip>()
                .Single(c => c.name == "sword_attack_fit");
            ValidateRotationPose(source, sourceAttack, SourceContact * sourceAttack.length, Clip("Strike"), StrikeContact);
            ValidateRotationPose(source, Clip("Windup"), 1f, Clip("Strike"), 0f);
            ValidateRotationPose(source, Clip("Strike"), StrikeContact, Clip("Stuck"), 0f);
            ValidateRotationPose(source, Clip("Stuck"), 1f, Clip("Recovery"), 0f);
            ValidateRotationPose(source, Clip("Recovery"), 1f, Clip("Idle"), 0f);
            return "PASS: GiacAnGeneral combat: 10 exact-rig clips, " + channels + " transform channels; 7 states, 4 driver parameters; " +
                "strike contact " + StrikeContact.ToString("F5") + " = source sword_attack " + SourceContact.ToString("F2") + "; root-object curves omitted, horizontal skeleton travel anchored. " +
                "Source contact pose and Windup/Strike/Stuck/Recovery/Idle boundaries match within .2 degrees. " +
                "Assign " + MotionSetPath + " to the boss driver; applyRootMotion=false; Generic exact-path driver clears avatar by design.";
        }

        static void ValidateRotationPose(GameObject source, AnimationClip a, float timeA, AnimationClip b, float timeB)
        {
            GameObject first = null, second = null;
            try
            {
                first = Object.Instantiate(source);
                second = Object.Instantiate(source);
                first.hideFlags = second.hideFlags = HideFlags.HideAndDontSave;
                foreach (var animator in first.GetComponentsInChildren<Animator>(true).Concat(second.GetComponentsInChildren<Animator>(true)))
                    animator.enabled = false;
                a.SampleAnimation(first, timeA);
                b.SampleAnimation(second, timeB);
                foreach (string path in AnimationUtility.GetCurveBindings(b).Where(binding =>
                    binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)).Select(binding => binding.path).Distinct())
                {
                    float error = Quaternion.Angle(first.transform.Find(path).localRotation, second.transform.Find(path).localRotation);
                    if (error > .2f) throw new InvalidOperationException("Pose boundary mismatch " + a.name + " -> " + b.name +
                        ": " + path + " differs by " + error.ToString("F4") + " degrees.");
                }
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
