using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    /// <summary>Fits ActionForge clips exported on the Meshy skeleton to the mounted hero hierarchy.</summary>
    public static class MeshyActionForgeAnimator
    {
        public const string SourcePath = "Assets/Animations/ActionForge/Meshy_ActionForge.fbx";
        public const string GeneratedFolder = "Assets/Animations/ActionForge/MeshyGenerated";
        public const string ControllerPath = GeneratedFolder + "/MeshyRider.controller";

        static readonly string[] UpperBones = {
            "spine", "spine1", "spine2", "neck", "head",
            "leftshoulder", "rightshoulder", "leftarm", "rightarm",
            "leftforearm", "rightforearm", "lefthand", "righthand"
        };

        // FBX/GLB can assign different local bone bases despite sharing a skeleton.
        // Bake world-space motion from bind poses; keep procedural controls untouched.
        static readonly KeyValuePair<string, string>[] Roles = {
            Pair("Idle", "idle_loop_fit"),
            Pair("AttackA", "sword_regular_a_fit"),
            Pair("AttackB", "sword_regular_b_fit"),
            Pair("AttackC", "sword_attack_fit"),
            Pair("BambooAttack", "sword_attack_fit"),
            Pair("Hit", "hit_chest_fit"),
            Pair("Pickup", "pickup_table_fit"),
            Pair("Fire", "spell_simple_shoot_fit"),
            Pair("Interact", "interact_fit"),
            Pair("Ascension", "spell_simple_idle_loop_fit")
        };

        static KeyValuePair<string, string> Pair(string role, string source)
        {
            return new KeyValuePair<string, string>(role, source);
        }

        public static AnimatorController Build(GameObject heroRoot)
        {
            if (heroRoot == null) throw new ArgumentNullException(nameof(heroRoot));
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before rebuilding Meshy rider animations.");
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal)).ToArray();
            if (sourceClips.Length == 0)
                throw new InvalidOperationException("Import " + SourcePath + " as Generic with Import Animation enabled; export ActionForge using the Meshy own skeleton.");

            MapBones(heroRoot.transform);
            var generated = new List<AnimationClip>();
            try
            {
                // Validate every source and target before changing any persistent assets.
                foreach (var role in Roles)
                {
                    var matches = sourceClips.Where(c => c.name == role.Value).ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException("Expected one ActionForge clip named '" + role.Value + "' in " + SourcePath + "; found " + matches.Length + ". Re-export the fitted animation set.");
                    generated.Add(Remap(matches[0], role.Key, heroRoot));
                }
                EnsureFolder(GeneratedFolder);
                for (int i = 0; i < Roles.Length; i++) SaveClip(generated[i], ClipPath(Roles[i].Key));
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(ControllerPath) != null)
                        throw new InvalidOperationException("The Meshy controller path contains a different asset: " + ControllerPath);
                    controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                }
                Configure(controller);
                AssetDatabase.SaveAssets();
                Verify(heroRoot);
                Debug.Log("Meshy ActionForge: validated 10 upper-body roles, 13 Mixamo bones, and MeshyRider.controller. Root and legs have no animation curves.");
                return controller;
            }
            finally
            {
                foreach (var clip in generated)
                    if (clip != null && !AssetDatabase.Contains(clip)) UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        public static AnimationClip GetClip(string role)
        {
            if (!Roles.Any(r => r.Key == role))
                throw new ArgumentException("Unknown Meshy animation role: " + role, nameof(role));
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(role));
        }

        static string ClipPath(string role) { return GeneratedFolder + "/" + role + ".anim"; }

        static string BoneKey(string name)
        {
            var key = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return key.StartsWith("mixamorig", StringComparison.Ordinal) ? key.Substring(9) : key;
        }

        static Dictionary<string, Transform> MapBones(Transform root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var key in UpperBones)
            {
                // Bare names such as spine/head belong to procedural control parents,
                // while only the prefixed Mixamo deform bones receive imported curves.
                var candidates = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith("mixamorig", StringComparison.OrdinalIgnoreCase) && BoneKey(t.name) == key).ToArray();
                if (candidates.Length != 1)
                    throw new InvalidOperationException("Meshy hero requires one unique deform bone '" + key + "' beneath " + root.name + "; found " + candidates.Length + ". Keep Mixamo bone names and disable Optimize Game Objects.");
                result.Add(key, candidates[0]);
            }
            return result;
        }

        static AnimationClip Remap(AnimationClip source, string role, GameObject heroRoot)
        {
            var clip = new AnimationClip { name = role, frameRate = 30f, legacy = false };
            GameObject sourceModel = null;
            GameObject targetModel = null;
            try
            {
                var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
                if (sourceAsset == null) throw new InvalidOperationException("Missing source FBX model: " + SourcePath);
                sourceModel = UnityEngine.Object.Instantiate(sourceAsset);
                targetModel = UnityEngine.Object.Instantiate(heroRoot);
                sourceModel.hideFlags = targetModel.hideFlags = HideFlags.HideAndDontSave;
                foreach (var animator in sourceModel.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var animator in targetModel.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var sourceBones = MapBones(sourceModel.transform);
                var targetBones = MapBones(targetModel.transform);
                ValidateSourceChannels(source, sourceModel.transform, sourceBones);
                var sourceBind = BindModelRotations(sourceModel, sourceBones);
                var targetRestLocal = targetBones.ToDictionary(p => p.Key, p => p.Value.localRotation);
                var targetRestModel = targetBones.ToDictionary(p => p.Key,
                    p => Quaternion.Inverse(targetModel.transform.rotation) * p.Value.rotation);
                var order = UpperBones.OrderBy(key => Depth(targetBones[key])).ToArray();
                var curves = UpperBones.ToDictionary(key => key, key => new[] {
                    new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve()
                });
                var previous = new Dictionary<string, Quaternion>();
                int frames = Mathf.Max(2, Mathf.CeilToInt(source.length * 30f) + 1);
                float maxAlignmentError = 0f;
                float maxIdleSpineDelta = 0f;
                for (int frame = 0; frame < frames; frame++)
                {
                    float time = source.length * frame / (frames - 1);
                    source.SampleAnimation(sourceModel, time);
                    foreach (string key in UpperBones) targetBones[key].localRotation = targetRestLocal[key];
                    foreach (string key in order)
                    {
                        Quaternion sourceAnimatedModel = Quaternion.Inverse(sourceModel.transform.rotation) * sourceBones[key].rotation;
                        Quaternion delta = sourceAnimatedModel * Quaternion.Inverse(sourceBind[key]);
                        Quaternion desired = targetModel.transform.rotation * delta * targetRestModel[key];
                        targetBones[key].rotation = desired;
                        maxAlignmentError = Mathf.Max(maxAlignmentError, Quaternion.Angle(targetBones[key].rotation, desired));
                        if (key == "spine" || key == "spine1" || key == "spine2")
                            maxIdleSpineDelta = Mathf.Max(maxIdleSpineDelta, Quaternion.Angle(Quaternion.identity, delta));
                        Quaternion local = targetBones[key].localRotation.normalized;
                        if (!Finite(local)) throw new InvalidOperationException(source.name + " produced a nonfinite rotation for " + key);
                        if (previous.TryGetValue(key, out var last) && Quaternion.Dot(last, local) < 0f)
                            local = new Quaternion(-local.x, -local.y, -local.z, -local.w);
                        previous[key] = local;
                        curves[key][0].AddKey(time, local.x);
                        curves[key][1].AddKey(time, local.y);
                        curves[key][2].AddKey(time, local.z);
                        curves[key][3].AddKey(time, local.w);
                    }
                }
                if (maxAlignmentError > .1f)
                    throw new InvalidOperationException(source.name + " retarget alignment exceeded 0.1 degree: " + maxAlignmentError);
                if (role == "Idle" && maxIdleSpineDelta > 65f)
                    throw new InvalidOperationException("Idle spine differs from the source skin bind pose by " + maxIdleSpineDelta.ToString("F1") + " degrees. Check ActionForge own skeleton export/bind poses before integrating.");
                foreach (string key in UpperBones)
                    for (int channel = 0; channel < 4; channel++)
                    {
                        var curve = curves[key][channel];
                        for (int i = 0; i < curve.length; i++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                        }
                        var binding = EditorCurveBinding.FloatCurve(
                            AnimationUtility.CalculateTransformPath(targetBones[key], targetModel.transform),
                            typeof(Transform), "m_LocalRotation." + "xyzw"[channel]);
                        AnimationUtility.SetEditorCurve(clip, binding, curve);
                    }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = role == "Idle" || role == "Ascension";
                settings.loopBlend = settings.loopTime;
                settings.startTime = 0f;
                settings.stopTime = source.length;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.EnsureQuaternionContinuity();
                foreach (int frame in new[] { 0, (frames - 1) / 4, (frames - 1) / 2, 3 * (frames - 1) / 4, frames - 1 }.Distinct())
                {
                    float time = source.length * frame / (frames - 1);
                    source.SampleAnimation(sourceModel, time);
                    foreach (string key in UpperBones) targetBones[key].localRotation = targetRestLocal[key];
                    clip.SampleAnimation(targetModel, time);
                    foreach (string key in UpperBones)
                    {
                        Quaternion animated = Quaternion.Inverse(sourceModel.transform.rotation) * sourceBones[key].rotation;
                        Quaternion desired = targetModel.transform.rotation * animated * Quaternion.Inverse(sourceBind[key]) * targetRestModel[key];
                        float error = Quaternion.Angle(targetBones[key].rotation, desired);
                        if (error > .15f) throw new InvalidOperationException(role + " baked playback differs from fitted source at " + time.ToString("F3") + "s for " + key + ": " + error.ToString("F3") + " degrees.");
                    }
                }
                Debug.Log("Meshy " + role + ": baked " + frames + " frames at 30 fps; max world alignment error "
                    + maxAlignmentError.ToString("F4") + " degrees" + (role == "Idle" ? "; max idle spine bind delta " + maxIdleSpineDelta.ToString("F1") + " degrees" : "") + ".");
                return clip;
            }
            catch { UnityEngine.Object.DestroyImmediate(clip); throw; }
            finally
            {
                if (sourceModel != null) UnityEngine.Object.DestroyImmediate(sourceModel);
                if (targetModel != null) UnityEngine.Object.DestroyImmediate(targetModel);
            }
        }

        static int Depth(Transform bone)
        {
            int depth = 0;
            while (bone.parent != null) { depth++; bone = bone.parent; }
            return depth;
        }

        static void ValidateSourceChannels(AnimationClip clip, Transform sourceRoot, Dictionary<string, Transform> bones)
        {
            var channels = new Dictionary<string, HashSet<string>>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)) continue;
                string key = BoneKey(binding.path.Split('/').Last());
                if (!bones.TryGetValue(key, out var bone)) continue;
                if (sourceRoot.Find(binding.path) != bone)
                    throw new InvalidOperationException(clip.name + " rotation path does not resolve to its source bone: " + binding.path);
                if (!channels.TryGetValue(key, out var properties)) channels[key] = properties = new HashSet<string>();
                if (!properties.Add(binding.propertyName)) throw new InvalidOperationException(clip.name + " has duplicate rotation channels for " + key);
            }
            foreach (string key in UpperBones)
                if (!channels.TryGetValue(key, out var properties) || properties.Count != 4)
                    throw new InvalidOperationException(clip.name + " has missing quaternion channels for " + key + ". Import the fitted own-skeleton Generic export.");
        }

        static bool Finite(Quaternion rotation)
        {
            return !float.IsNaN(rotation.x) && !float.IsInfinity(rotation.x)
                && !float.IsNaN(rotation.y) && !float.IsInfinity(rotation.y)
                && !float.IsNaN(rotation.z) && !float.IsInfinity(rotation.z)
                && !float.IsNaN(rotation.w) && !float.IsInfinity(rotation.w);
        }

        static Dictionary<string, Quaternion> BindModelRotations(GameObject sourceModel, Dictionary<string, Transform> sourceBones)
        {
            var result = new Dictionary<string, Quaternion>();
            Quaternion inverseRoot = Quaternion.Inverse(sourceModel.transform.rotation);
            foreach (var renderer in sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                var bindposes = renderer.sharedMesh.bindposes;
                var bones = renderer.bones;
                for (int i = 0; i < Mathf.Min(bindposes.Length, bones.Length); i++)
                {
                    if (bones[i] == null || !bones[i].name.StartsWith("mixamorig", StringComparison.OrdinalIgnoreCase)) continue;
                    string key = BoneKey(bones[i].name);
                    if (!sourceBones.TryGetValue(key, out var expected) || bones[i] != expected) continue;
                    Quaternion rotation = inverseRoot * (renderer.localToWorldMatrix * bindposes[i].inverse).rotation;
                    if (!Finite(rotation)) throw new InvalidOperationException("Invalid FBX skin bind rotation: " + key);
                    if (result.TryGetValue(key, out var previous) && Quaternion.Angle(previous, rotation) > .1f)
                        throw new InvalidOperationException("FBX meshes disagree on skin bind rotation: " + key);
                    result[key] = rotation;
                }
            }
            foreach (string key in UpperBones)
                if (!result.ContainsKey(key)) throw new InvalidOperationException("Source FBX has no skin bind pose for " + key + ". Export the own skeleton with its skinned mesh; the imported model first frame is not a valid bind pose.");
            return result;
        }

        static void SaveClip(AnimationClip generated, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Unexpected asset at " + path);
                AssetDatabase.CreateAsset(generated, path);
            }
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                EditorUtility.SetDirty(existing);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Configure(AnimatorController controller)
        {
            if (controller.layers.Length != 1)
                throw new InvalidOperationException("MeshyRider.controller must have exactly one owned base layer.");
            // Dedicated controller: a deterministic parameter list also removes stale parameter types.
            controller.parameters = new[] {
                new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float },
                new AnimatorControllerParameter { name = "ActionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f },
                new AnimatorControllerParameter { name = "AttackVariant", type = AnimatorControllerParameterType.Int },
                new AnimatorControllerParameter { name = "Ascending", type = AnimatorControllerParameterType.Bool },
                Trigger("Attack"), Trigger("BambooAttack"), Trigger("Hit"), Trigger("Pickup"), Trigger("Fire"), Trigger("Interact")
            };
            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            foreach (var role in Roles)
            {
                var matches = machine.states.Where(s => s.state.name == role.Key).ToArray();
                var state = matches.Length == 0 ? machine.AddState(role.Key) : matches[0].state;
                foreach (var duplicate in matches.Skip(1)) machine.RemoveState(duplicate.state);
                state.motion = GetClip(role.Key);
                state.speed = 1f;
                state.speedParameter = "ActionSpeed";
                state.speedParameterActive = role.Key != "Idle" && role.Key != "Ascension";
                state.writeDefaultValues = false;
                states.Add(role.Key, state);
                EditorUtility.SetDirty(state);
            }
            foreach (var obsolete in machine.states.Where(s => !states.ContainsKey(s.state.name)).ToArray()) machine.RemoveState(obsolete.state);
            machine.defaultState = states["Idle"];
            // Reuse transition objects where possible; remove duplicates without changing asset GUIDs.
            SetTransition(machine, null, states["Ascension"], false, Condition(AnimatorConditionMode.If, 0, "Ascending"));
            foreach (var role in Roles.Where(r => r.Key != "Idle" && r.Key != "Ascension"))
            {
                var conditions = new List<AnimatorCondition> { Condition(AnimatorConditionMode.IfNot, 0, "Ascending") };
                if (role.Key.StartsWith("Attack", StringComparison.Ordinal))
                {
                    conditions.Add(Condition(AnimatorConditionMode.If, 0, "Attack"));
                    conditions.Add(Condition(AnimatorConditionMode.Equals, role.Key[role.Key.Length - 1] - 'A', "AttackVariant"));
                }
                else conditions.Add(Condition(AnimatorConditionMode.If, 0, role.Key));
                SetTransition(machine, null, states[role.Key], false, conditions.ToArray());
                SetTransition(machine, states[role.Key], states["Idle"], true);
            }
            SetTransition(machine, states["Ascension"], states["Idle"], false, Condition(AnimatorConditionMode.IfNot, 0, "Ascending"));
            // A previous generated version must not retain extra transition destinations.
            foreach (var state in states.Values)
                foreach (var transition in state.transitions.Where(t => state.name == "Idle" || t.destinationState != states["Idle"]).ToArray()) state.RemoveTransition(transition);
            foreach (var transition in machine.anyStateTransitions.Where(t => t.destinationState == null || t.destinationState == states["Idle"]).ToArray()) machine.RemoveAnyStateTransition(transition);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }

        static AnimatorControllerParameter Trigger(string name)
        {
            return new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Trigger };
        }

        static AnimatorCondition Condition(AnimatorConditionMode mode, float threshold, string parameter)
        {
            return new AnimatorCondition { mode = mode, threshold = threshold, parameter = parameter };
        }

        static void SetTransition(AnimatorStateMachine machine, AnimatorState from, AnimatorState to, bool atEnd, params AnimatorCondition[] conditions)
        {
            var matches = (from == null ? machine.anyStateTransitions : from.transitions).Where(t => t.destinationState == to).ToArray();
            var transition = matches.Length == 0 ? (from == null ? machine.AddAnyStateTransition(to) : from.AddTransition(to)) : matches[0];
            foreach (var duplicate in matches.Skip(1))
                if (from == null) machine.RemoveAnyStateTransition(duplicate); else from.RemoveTransition(duplicate);
            transition.conditions = conditions;
            transition.hasExitTime = atEnd;
            transition.exitTime = .98f;
            transition.hasFixedDuration = true;
            transition.duration = to.name == "Hit" ? .04f : .06f;
            transition.offset = 0f;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.None;
            EditorUtility.SetDirty(transition);
        }

        /// <summary>Focused asset checks, callable by the integration installer after a rebuild.</summary>
        public static string Verify(GameObject heroRoot)
        {
            var bones = MapBones(heroRoot.transform);
            var paths = new HashSet<string>(bones.Values.Select(t => AnimationUtility.CalculateTransformPath(t, heroRoot.transform)));
            foreach (var role in Roles)
            {
                var clip = GetClip(role.Key);
                if (clip == null || clip.length <= 0f) throw new InvalidOperationException("Missing or empty generated clip: " + role.Key);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                if (bindings.Length != UpperBones.Length * 4 || bindings.Any(b => b.type != typeof(Transform) || !paths.Contains(b.path) || !b.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Unexpected root/leg/position/scale or unmapped curve in " + role.Key);
                bool loop = role.Key == "Idle" || role.Key == "Ascension";
                if (AnimationUtility.GetAnimationClipSettings(clip).loopTime != loop) throw new InvalidOperationException("Incorrect loop flag: " + role.Key);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Missing MeshyRider controller.");
            if (controller.layers.Length != 1) throw new InvalidOperationException("Unexpected MeshyRider layer count.");
            var expectedParameters = new Dictionary<string, AnimatorControllerParameterType> {
                { "Speed", AnimatorControllerParameterType.Float }, { "ActionSpeed", AnimatorControllerParameterType.Float },
                { "AttackVariant", AnimatorControllerParameterType.Int }, { "Ascending", AnimatorControllerParameterType.Bool },
                { "Attack", AnimatorControllerParameterType.Trigger }, { "BambooAttack", AnimatorControllerParameterType.Trigger },
                { "Hit", AnimatorControllerParameterType.Trigger }, { "Pickup", AnimatorControllerParameterType.Trigger },
                { "Fire", AnimatorControllerParameterType.Trigger }, { "Interact", AnimatorControllerParameterType.Trigger }
            };
            if (controller.parameters.Length != expectedParameters.Count || controller.parameters.Any(p => !expectedParameters.TryGetValue(p.name, out var type) || p.type != type)
                || !Mathf.Approximately(controller.parameters.Single(p => p.name == "ActionSpeed").defaultFloat, 1f))
                throw new InvalidOperationException("MeshyRider parameters do not match MeshyMountedRig.");
            var machine = controller.layers[0].stateMachine;
            if (machine.states.Length != Roles.Length || machine.anyStateTransitions.Length != Roles.Length - 1 || machine.defaultState.name != "Idle")
                throw new InvalidOperationException("MeshyRider has duplicate/missing states or transitions.");
            foreach (var child in machine.states)
            {
                var state = child.state;
                if (state.motion != GetClip(state.name) || state.transitions.Length != (state.name == "Idle" ? 0 : 1))
                    throw new InvalidOperationException("Incorrect MeshyRider motion/return transition: " + state.name);
                if (state.name == "Idle") continue;
                bool action = state.name != "Ascension";
                if (state.speedParameterActive != action || (action && state.speedParameter != "ActionSpeed"))
                    throw new InvalidOperationException("Incorrect ActionSpeed binding: " + state.name);
                var incoming = machine.anyStateTransitions.Where(t => t.destinationState == state).ToArray();
                if (incoming.Length != 1 || incoming[0].canTransitionToSelf || incoming[0].hasExitTime || !incoming[0].hasFixedDuration)
                    throw new InvalidOperationException("Incorrect AnyState action transition: " + state.name);
                var conditions = incoming[0].conditions;
                if (!conditions.Any(c => c.parameter == "Ascending" && c.mode == (action ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If)))
                    throw new InvalidOperationException("Missing Ascending condition: " + state.name);
                string trigger = state.name.StartsWith("Attack", StringComparison.Ordinal) ? "Attack" : state.name;
                if (action && !conditions.Any(c => c.parameter == trigger && c.mode == AnimatorConditionMode.If))
                    throw new InvalidOperationException("Missing action trigger: " + state.name);
                if (trigger == "Attack" && !conditions.Any(c => c.parameter == "AttackVariant" && c.mode == AnimatorConditionMode.Equals && Mathf.Approximately(c.threshold, state.name.Last() - 'A')))
                    throw new InvalidOperationException("Incorrect AttackVariant condition: " + state.name);
                var outgoing = state.transitions[0];
                if (outgoing.destinationState != machine.defaultState || outgoing.hasExitTime != action || !outgoing.hasFixedDuration
                    || (action && (!Mathf.Approximately(outgoing.exitTime, .98f) || outgoing.conditions.Length != 0))
                    || (!action && !outgoing.conditions.Any(c => c.parameter == "Ascending" && c.mode == AnimatorConditionMode.IfNot)))
                    throw new InvalidOperationException("Incorrect return to Idle: " + state.name);
            }
            return "PASS: 10 clips, 52 upper-body rotation curves each, 10 states, 9 AnyState transitions; root and legs excluded.";
        }
    }
}
