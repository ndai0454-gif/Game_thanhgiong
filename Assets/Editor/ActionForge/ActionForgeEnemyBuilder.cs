using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    /// <summary>Bakes fitted ActionForge actions onto each enemy's exact Generic hierarchy.</summary>
    public static class ActionForgeEnemyBuilder
    {
        public const string SourcePath = "Assets/Animations/ActionForge/Enemy_ActionForge.fbx";
        public const string GeneratedFolder = "Assets/Animations/ActionForge/Enemies";
        public const string ResourceFolder = "Assets/Resources/ActionForge/EnemyMotionSets";
        public const string ReportPath = "ActionForgeEnemyVerification/build-results.txt";
        static readonly string[] BoneNames = {
            "hips", "spine", "chest", "head",
            "upperarm.l", "lowerarm.l", "wrist.l", "hand.l", "handslot.l",
            "upperarm.r", "lowerarm.r", "wrist.r", "hand.r", "handslot.r",
            "upperleg.l", "lowerleg.l", "foot.l", "toes.l",
            "upperleg.r", "lowerleg.r", "foot.r", "toes.r"
        };
        static readonly string[] FamilyNames = { "Barbarian", "Knight" };
        static readonly string[] PrefabPaths = {
            "Assets/Prefabs/Enemies/KayKit_Barbarian_Soldier.prefab",
            "Assets/Prefabs/Enemies/KayKitKnightBoss/KayKit_Knight_Boss_Visual.prefab"
        };
        static readonly string[] PhaseNames = { "Windup", "Strike", "Stuck", "Recovery", "Hit", "Death" };
        public sealed class RoleDefinition
        {
            public readonly string Name, Field, Source, Idle;
            public readonly float WindupStart, SourceWindupEnd, SourceContact, SourceStrikeEnd, StrikeContact;
            public RoleDefinition(string name, string field, string source, string idle, float windupStart, float windupEnd, float contact, float strikeEnd, float strikeContact)
            {
                Name = name;
                Field = field;
                Source = source;
                Idle = idle;
                WindupStart = windupStart;
                SourceWindupEnd = windupEnd;
                SourceContact = contact;
                SourceStrikeEnd = strikeEnd;
                StrikeContact = strikeContact;
            }
        }
        // Measured directly from Enemy_ActionForge.fbx hand trajectories relative to hips.
        public static readonly RoleDefinition[] Roles = {
            new RoleDefinition("Spearman", "spearman", "sword_regular_c_fit", "sword_idle_fit", .08f, .30f, .36f, .68f, .25f),
            new RoleDefinition("ShieldBearer", "shieldBearer", "shield_oneshot_fit", "idle_shield_loop_fit", .00f, .22f, .45f, .78f, .25f),
            new RoleDefinition("Archer", "archer", "overhandthrow_fit", "idle_loop_fit", .00f, .20f, .32f, .60f, .30f),
            new RoleDefinition("Raider", "raider", "sword_regular_b_fit", "sword_idle_fit", .00f, .32f, .48f, .72f, .25f),
            new RoleDefinition("Boss", "boss", "sword_attack_fit", "sword_idle_fit", .00f, .20f, .35f, .65f, .35484f)
        };

        [MenuItem("Tools/Thánh Gióng/ActionForge/Build All Enemy Animations")]
        public static void BuildMenu() { Debug.Log(Build()); }

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding enemy animations.");
            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (sourceAsset == null) throw new InvalidOperationException("Missing fitted ActionForge source: " + SourcePath);
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal)).ToDictionary(c => c.name);
            string[] required = { "idle_loop_fit", "walk_loop_fit", "jog_fwd_loop_fit", "sprint_loop_fit", "hit_chest_fit", "death01_fit" };
            foreach (var name in required.Concat(Roles.SelectMany(r => new[] { r.Source, r.Idle })).Distinct())
                if (!sourceClips.ContainsKey(name)) throw new InvalidOperationException("Missing ActionForge clip: " + name);
            EnsureEffectMaterials();
            var report = new List<string> { "ActionForge enemy bake: " + DateTime.UtcNow.ToString("O"), "Source: " + SourcePath,
                "22 bone rotations; no object/root/hip translation or scale tracks. Generic exact-path controllers." };
            for (int family = 0; family < FamilyNames.Length; family++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[family]);
                if (prefab == null) throw new InvalidOperationException("Missing target prefab: " + PrefabPaths[family]);
                var targetAnimator = prefab.GetComponentsInChildren<Animator>(true).Single();
                string name = FamilyNames[family];
                EnsureFolder(GeneratedFolder + "/" + name);
                using (var baker = new Baker(sourceAsset, targetAnimator.gameObject))
                {
                    foreach (var move in new[] { "Walk", "Jog", "Sprint" })
                    {
                        string sourceName = move == "Walk" ? "walk_loop_fit" : move == "Jog" ? "jog_fwd_loop_fit" : "sprint_loop_fit";
                        SaveClip(baker.Bake(sourceClips[sourceName], move, p => p, true), ClipPath(name, null, move));
                    }
                    foreach (var role in Roles)
                    {
                        string folder = GeneratedFolder + "/" + name + "/" + role.Name;
                        EnsureFolder(folder);
                        var action = sourceClips[role.Source];
                        var idle = sourceClips[role.Idle];
                        SaveClip(baker.Bake(idle, role.Name + " Idle", p => p, true), ClipPath(name, role.Name, "Idle"));
                        SaveClip(baker.Bake(action, role.Name + " Windup", p => Mathf.Lerp(role.WindupStart, role.SourceWindupEnd, p), false), ClipPath(name, role.Name, "Windup"));
                        SaveClip(baker.Bake(action, role.Name + " Strike", p => p <= role.StrikeContact
                            ? Mathf.Lerp(role.SourceWindupEnd, role.SourceContact, p / role.StrikeContact)
                            : Mathf.Lerp(role.SourceContact, role.SourceStrikeEnd, (p - role.StrikeContact) / (1f - role.StrikeContact)), false,
                            null, role.StrikeContact), ClipPath(name, role.Name, "Strike"));
                        SaveClip(baker.Bake(action, role.Name + " Stuck", p => role.SourceContact, false), ClipPath(name, role.Name, "Stuck"));
                        SaveClip(baker.Bake(action, role.Name + " Recovery", p => Mathf.Lerp(role.SourceStrikeEnd, 1f, p), false,
                            idle), ClipPath(name, role.Name, "Recovery"));
                        SaveClip(baker.Bake(sourceClips["hit_chest_fit"], role.Name + " Hit", p => p, false), ClipPath(name, role.Name, "Hit"));
                        SaveClip(baker.Bake(sourceClips["death01_fit"], role.Name + " Death", p => p, false), ClipPath(name, role.Name, "Death"));
                        BuildController(name, role);
                        report.Add(name + "/" + role.Name + ": " + role.Source + "; source contact=" + role.SourceContact.ToString("F5")
                            + "; strike contact=" + role.StrikeContact.ToString("F5") + "; windup=" + role.WindupStart.ToString("F5") + "->" + role.SourceWindupEnd.ToString("F5")
                            + "; strike end=" + role.SourceStrikeEnd.ToString("F5"));
                    }
                    report.Add(name + ": maximum sampled rotation alignment error=" + baker.MaxAlignmentError.ToString("F5") + " degrees.");
                }
                SaveMotionSet(name);
            }
            AssignPrefabsAndSceneEnemies();
            AssetDatabase.SaveAssets();
            report.Add(Verify());
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, report);
            return string.Join("\n", report);
        }

        static void EnsureEffectMaterials()
        {
            const string folder = "Assets/Resources/ActionForge/EnemyEffects";
            EnsureFolder(folder);
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            EnsureMaterial(folder + "/Swipe.mat", shader, new Color(1f, 0.72f, 0.28f, 0.92f));
            EnsureMaterial(folder + "/SoftParticle.mat", shader, Color.white);
        }

        static void EnsureMaterial(string path, Shader shader, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
        }

        static void AssignPrefabsAndSceneEnemies()
        {
            var barbSpearman = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath("Barbarian", "Spearman"));
            var knightBoss = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath("Knight", "Boss"));

            var barbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[0]);
            if (barbPrefab != null && barbSpearman != null)
            {
                var anim = barbPrefab.GetComponentInChildren<Animator>(true);
                if (anim != null && anim.runtimeAnimatorController != barbSpearman)
                {
                    anim.runtimeAnimatorController = barbSpearman;
                    EditorUtility.SetDirty(barbPrefab);
                }
            }

            foreach (var bossPrefabPath in new[] { PrefabPaths[1], "Assets/Prefabs/Enemies/KayKit_Knight_Boss_An.prefab" })
            {
                var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bossPrefabPath);
                if (bossPrefab != null && knightBoss != null)
                {
                    var anim = bossPrefab.GetComponentInChildren<Animator>(true);
                    if (anim != null && anim.runtimeAnimatorController != knightBoss)
                    {
                        anim.runtimeAnimatorController = knightBoss;
                        EditorUtility.SetDirty(bossPrefab);
                    }
                }
            }

            foreach (var enemy in UnityEngine.Object.FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var driver = enemy.GetComponent<ThanhGiongEnemyAnimationDriver>() ?? enemy.gameObject.AddComponent<ThanhGiongEnemyAnimationDriver>();
                if (enemy.GetComponent<ActionForgeEnemyEffects>() == null)
                    enemy.gameObject.AddComponent<ActionForgeEnemyEffects>();
                driver.RebindAnimator();
                EditorUtility.SetDirty(enemy.gameObject);
            }
        }

        public static string ControllerPath(string family, string role) { return GeneratedFolder + "/" + family + "/" + role + ".controller"; }
        public static string ClipPath(string family, string role, string phase)
        { return GeneratedFolder + "/" + family + (string.IsNullOrEmpty(role) ? "" : "/" + role) + "/" + phase + ".anim"; }
        public static AnimationClip GetClip(string family, string role, string phase)
        { return AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(family, role, phase)); }

        sealed class Baker : IDisposable
        {
            readonly GameObject sourceModel, targetModel;
            readonly Dictionary<string, Transform> sourceBones, targetBones;
            readonly Dictionary<string, Quaternion> sourceBind, targetBind, sourceRestLocal, targetRestLocal;
            readonly string[] order;
            readonly Dictionary<string, string> paths;
            public float MaxAlignmentError { get; private set; }
            public Baker(GameObject source, GameObject target)
            {
                sourceModel = UnityEngine.Object.Instantiate(source);
                targetModel = UnityEngine.Object.Instantiate(target);
                sourceModel.hideFlags = targetModel.hideFlags = HideFlags.HideAndDontSave;
                foreach (var animator in sourceModel.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var animator in targetModel.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                sourceBones = MapBones(sourceModel.transform);
                targetBones = MapBones(targetModel.transform);
                sourceBind = BindRotations(sourceModel, sourceBones);
                targetBind = BindRotations(targetModel, targetBones);
                sourceRestLocal = sourceBones.ToDictionary(p => p.Key, p => p.Value.localRotation);
                targetRestLocal = targetBones.ToDictionary(p => p.Key, p => p.Value.localRotation);
                order = BoneNames.OrderBy(k => Depth(targetBones[k])).ToArray();
                paths = targetBones.ToDictionary(p => p.Key, p => AnimationUtility.CalculateTransformPath(p.Value, targetModel.transform));
            }
            public AnimationClip Bake(AnimationClip source, string name, Func<float, float> sourceTime, bool loop,
                AnimationClip recoveryIdle = null, float explicitKey = -1f)
            {
                ValidateChannels(source, sourceModel.transform, sourceBones);
                if (recoveryIdle != null) ValidateChannels(recoveryIdle, sourceModel.transform, sourceBones);
                // State-driven clips span one second, so Phase directly addresses normalized progress.
                float duration = loop || name.EndsWith(" Death", StringComparison.Ordinal) ? source.length : 1f;
                int count = Mathf.Max(2, Mathf.CeilToInt(duration * 30f) + 1);
                var phases = Enumerable.Range(0, count).Select(i => (float)i / (count - 1)).ToList();
                if (explicitKey > 0f && explicitKey < 1f) phases.Add(explicitKey);
                phases = phases.Distinct().OrderBy(p => p).ToList();
                var curves = BoneNames.ToDictionary(k => k, k => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
                var expected = new List<Dictionary<string, Quaternion>>();
                var previous = new Dictionary<string, Quaternion>();
                foreach (float phase in phases)
                {
                    var desired = Desired(source, sourceTime(phase));
                    if (recoveryIdle != null && phase > .72f)
                    {
                        var idle = Desired(recoveryIdle, 0f);
                        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, phase));
                        foreach (var key in BoneNames) desired[key] = Quaternion.Slerp(desired[key], idle[key], blend);
                    }
                    foreach (var key in BoneNames) targetBones[key].localRotation = targetRestLocal[key];
                    foreach (var key in order)
                    {
                        targetBones[key].rotation = desired[key];
                        var rotation = targetBones[key].localRotation.normalized;
                        if (!Finite(rotation)) throw new InvalidOperationException(name + " invalid rotation: " + key);
                        if (previous.TryGetValue(key, out var last) && Quaternion.Dot(last, rotation) < 0f)
                            rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                        previous[key] = rotation;
                        for (int channel = 0; channel < 4; channel++) curves[key][channel].AddKey(phase * duration, rotation[channel]);
                    }
                    expected.Add(desired);
                }
                var clip = new AnimationClip { name = name, frameRate = 30f, legacy = false, wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever };
                try
                {
                    foreach (var key in BoneNames)
                        for (int channel = 0; channel < 4; channel++)
                        {
                            var curve = curves[key][channel];
                            for (int i = 0; i < curve.length; i++)
                            {
                                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                            }
                            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(paths[key], typeof(Transform), "m_LocalRotation." + "xyzw"[channel]), curve);
                        }
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = loop;
                    settings.loopBlend = loop;
                    settings.startTime = 0f;
                    settings.stopTime = duration;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    clip.EnsureQuaternionContinuity();
                    foreach (int i in new[] { 0, phases.Count / 4, phases.Count / 2, phases.Count * 3 / 4, phases.Count - 1 }.Distinct())
                    {
                        foreach (var key in BoneNames) targetBones[key].localRotation = targetRestLocal[key];
                        clip.SampleAnimation(targetModel, phases[i] * duration);
                        foreach (var key in BoneNames)
                        {
                            float error = Quaternion.Angle(targetBones[key].rotation, expected[i][key]);
                            MaxAlignmentError = Mathf.Max(MaxAlignmentError, error);
                            if (error > .2f) throw new InvalidOperationException(name + " baked playback error: " + key + " " + error.ToString("F3") + " degrees.");
                        }
                    }
                    return clip;
                }
                catch { UnityEngine.Object.DestroyImmediate(clip); throw; }
            }
            Dictionary<string, Quaternion> Desired(AnimationClip clip, float phase)
            {
                foreach (var key in BoneNames) sourceBones[key].localRotation = sourceRestLocal[key];
                clip.SampleAnimation(sourceModel, Mathf.Clamp01(phase) * clip.length);
                var inverseRoot = Quaternion.Inverse(sourceModel.transform.rotation);
                var result = new Dictionary<string, Quaternion>();
                foreach (var key in BoneNames)
                    result[key] = targetModel.transform.rotation * (inverseRoot * sourceBones[key].rotation)
                        * Quaternion.Inverse(sourceBind[key]) * targetBind[key];
                return result;
            }
            public void Dispose()
            {
                if (sourceModel != null) UnityEngine.Object.DestroyImmediate(sourceModel);
                if (targetModel != null) UnityEngine.Object.DestroyImmediate(targetModel);
            }
        }

        static Dictionary<string, Transform> MapBones(Transform root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var key in BoneNames)
            {
                var matches = transforms.Where(t => t.name == key).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException(root.name + " needs one exact bone '" + key + "'; found " + matches.Length);
                bones[key] = matches[0];
            }
            return bones;
        }
        static Dictionary<string, Quaternion> BindRotations(GameObject model, Dictionary<string, Transform> bones)
        {
            var result = new Dictionary<string, Quaternion>();
            var inverseRoot = Quaternion.Inverse(model.transform.rotation);
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                var bindposes = renderer.sharedMesh.bindposes;
                for (int i = 0; i < Mathf.Min(bindposes.Length, renderer.bones.Length); i++)
                {
                    var bone = renderer.bones[i];
                    if (bone == null || !bones.TryGetValue(bone.name, out var expected) || bone != expected) continue;
                    var rotation = inverseRoot * (renderer.localToWorldMatrix * bindposes[i].inverse).rotation;
                    if (!Finite(rotation)) throw new InvalidOperationException("Invalid skin bind rotation: " + bone.name);
                    if (result.TryGetValue(bone.name, out var previous) && Quaternion.Angle(previous, rotation) > .2f)
                        throw new InvalidOperationException("Skin meshes disagree on bind pose: " + bone.name);
                    result[bone.name] = rotation;
                }
            }
            foreach (var key in BoneNames.OrderBy(k => Depth(bones[k])))
            {
                if (result.ContainsKey(key)) continue;
                // Nondeforming hand sockets/wrists may be omitted from skin joints. Their static
                // relative basis is anchored to the nearest real skin bind ancestor.
                var bone = bones[key];
                var parent = bone.parent;
                while (parent != null && !result.ContainsKey(parent.name)) parent = parent.parent;
                if (parent == null || (!key.StartsWith("handslot", StringComparison.Ordinal) && !key.StartsWith("wrist", StringComparison.Ordinal) && !key.StartsWith("toes", StringComparison.Ordinal)))
                    throw new InvalidOperationException(model.name + " has no skin bind pose for " + key + ". Export a fitted character with mesh.");
                result[key] = result[parent.name] * Quaternion.Inverse(parent.rotation) * bone.rotation;
            }
            return result;
        }
        static void ValidateChannels(AnimationClip clip, Transform root, Dictionary<string, Transform> bones)
        {
            var channels = new Dictionary<string, HashSet<string>>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)) continue;
                string key = binding.path.Split('/').Last();
                if (!bones.TryGetValue(key, out var bone)) continue;
                if (root.Find(binding.path) != bone) throw new InvalidOperationException("Invalid source binding: " + binding.path);
                if (!channels.TryGetValue(key, out var properties)) channels[key] = properties = new HashSet<string>();
                properties.Add(binding.propertyName);
            }
            foreach (var key in BoneNames)
            {
                // Sockets have no authored motion and inherit hand rotation normally.
                if (!channels.TryGetValue(key, out var properties) && key.StartsWith("handslot", StringComparison.Ordinal)) continue;
                if (properties == null || properties.Count != 4) throw new InvalidOperationException(clip.name + " is missing quaternion animation channels: " + key);
            }
        }
        static bool Finite(Quaternion q)
        { return Enumerable.Range(0, 4).All(i => !float.IsNaN(q[i]) && !float.IsInfinity(q[i])); }
        static int Depth(Transform t) { int depth = 0; while (t.parent != null) { depth++; t = t.parent; } return depth; }

        static void BuildController(string family, RoleDefinition role)
        {
            string path = ControllerPath(family, role.Name);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller.layers.Length != 1) throw new InvalidOperationException("Generated enemy controller must have one layer: " + path);
            controller.parameters = new[] {
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
                if (name != "Locomotion") state.motion = GetClip(family, role.Name, name);
                foreach (var transition in state.transitions) state.RemoveTransition(transition);
                EditorUtility.SetDirty(state);
            }
            foreach (var obsolete in machine.states.Where(s => !states.ContainsKey(s.state.name)).ToArray()) machine.RemoveState(obsolete.state);
            foreach (var child in machine.stateMachines) machine.RemoveStateMachine(child.stateMachine);
            var trees = AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().ToArray();
            var tree = trees.FirstOrDefault(t => t.name == "Enemy Locomotion");
            if (tree == null)
            {
                tree = new BlendTree { name = "Enemy Locomotion", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, controller);
            }
            foreach (var extra in trees.Where(t => t != tree)) UnityEngine.Object.DestroyImmediate(extra, true);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.children = new[] {
                new ChildMotion { motion = GetClip(family, role.Name, "Idle"), threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = GetClip(family, null, "Walk"), threshold = .4f, timeScale = 1f },
                new ChildMotion { motion = GetClip(family, null, "Jog"), threshold = .75f, timeScale = 1f },
                new ChildMotion { motion = GetClip(family, null, "Sprint"), threshold = 1f, timeScale = 1f }
            };
            states["Locomotion"].motion = tree;
            machine.defaultState = states["Locomotion"];
            // Death and hit get priority when they interrupt a phase transition.
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            var indices = new Dictionary<string, int> { { "Windup", 2 }, { "Strike", 3 }, { "Stuck", 4 }, { "Recovery", 5 }, { "Hit", 6 }, { "Death", 7 } };
            foreach (var name in new[] { "Death", "Hit", "Windup", "Strike", "Stuck", "Recovery", "Locomotion" })
            {
                var transition = machine.AddAnyStateTransition(states[name]);
                transition.conditions = new[] { new AnimatorCondition { parameter = "State", mode = name == "Locomotion" ? AnimatorConditionMode.Less : AnimatorConditionMode.Equals,
                    threshold = name == "Locomotion" ? 2f : indices[name] } };
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = name == "Hit" || name == "Strike" ? .04f : name == "Locomotion" || name == "Death" ? .08f : .06f;
                transition.canTransitionToSelf = false;
                transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                transition.orderedInterruption = false;
                EditorUtility.SetDirty(transition);
            }
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }
        static void SaveMotionSet(string family)
        {
            EnsureFolder(ResourceFolder);
            string path = ResourceFolder + "/" + family + ".asset";
            var set = AssetDatabase.LoadAssetAtPath<ActionForgeEnemyMotionSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ActionForgeEnemyMotionSet>();
                set.name = family;
                AssetDatabase.CreateAsset(set, path);
            }
            var serialized = new SerializedObject(set);
            foreach (var role in Roles) serialized.FindProperty(role.Field).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(family, role.Name));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
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
                UnityEngine.Object.DestroyImmediate(generated);
            }
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        public static string Verify()
        {
            int clips = 0;
            for (int f = 0; f < FamilyNames.Length; f++)
            {
                string family = FamilyNames[f];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[f]);
                var target = prefab.GetComponentsInChildren<Animator>(true).Single().transform;
                var bones = MapBones(target);
                var paths = new HashSet<string>(bones.Values.Select(b => AnimationUtility.CalculateTransformPath(b, target)));
                foreach (var role in Roles)
                {
                    foreach (var phase in new[] { "Idle" }.Concat(PhaseNames))
                    {
                        ValidateGenerated(GetClip(family, role.Name, phase), paths, phase == "Idle");
                        clips++;
                    }
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(family, role.Name));
                    if (controller == null || controller.layers.Length != 1) throw new InvalidOperationException("Invalid enemy controller " + family + "/" + role.Name);
                    var machine = controller.layers[0].stateMachine;
                    if (machine.states.Length != 7 || machine.anyStateTransitions.Length != 7 || machine.defaultState.name != "Locomotion")
                        throw new InvalidOperationException("Duplicate/missing enemy states: " + controller.name);
                    var types = new Dictionary<string, AnimatorControllerParameterType> { { "Speed", AnimatorControllerParameterType.Float }, { "MoveRate", AnimatorControllerParameterType.Float }, { "State", AnimatorControllerParameterType.Int }, { "Phase", AnimatorControllerParameterType.Float } };
                    if (controller.parameters.Length != 4 || controller.parameters.Any(p => !types.TryGetValue(p.name, out var type) || type != p.type))
                        throw new InvalidOperationException("Controller parameters differ from runtime contract: " + controller.name);
                    foreach (var child in machine.states)
                    {
                        var state = child.state;
                        if (state.transitions.Length != 0 || state.timeParameterActive != (state.name != "Locomotion" && state.name != "Death"))
                            throw new InvalidOperationException("Unexpected state time control: " + state.name);
                        var transitions = machine.anyStateTransitions.Where(t => t.destinationState == state).ToArray();
                        if (transitions.Length != 1 || transitions[0].canTransitionToSelf || transitions[0].hasExitTime || !transitions[0].hasFixedDuration)
                            throw new InvalidOperationException("Invalid state transition: " + state.name);
                        if (state.name == "Locomotion")
                        {
                            var tree = state.motion as BlendTree;
                            if (tree == null || tree.children.Length != 4 || tree.children.Any(c => c.motion == null) || tree.blendParameter != "Speed" || !state.speedParameterActive)
                                throw new InvalidOperationException("Invalid locomotion tree: " + controller.name);
                        }
                        else if (state.motion != GetClip(family, role.Name, state.name)) throw new InvalidOperationException("Incorrect state clip: " + state.name);
                    }
                    if (AssetDatabase.LoadAllAssetsAtPath(ControllerPath(family, role.Name)).OfType<BlendTree>().Count() != 1)
                        throw new InvalidOperationException("Duplicate hidden locomotion trees: " + controller.name);
                }
                foreach (var phase in new[] { "Walk", "Jog", "Sprint" }) { ValidateGenerated(GetClip(family, null, phase), paths, true); clips++; }
                var set = Resources.Load<ActionForgeEnemyMotionSet>("ActionForge/EnemyMotionSets/" + family);
                if (set == null) throw new InvalidOperationException("Missing Resources enemy motion set: " + family);
                var serialized = new SerializedObject(set);
                foreach (var role in Roles)
                    if (serialized.FindProperty(role.Field).objectReferenceValue != AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(family, role.Name)))
                        throw new InvalidOperationException("Wrong role resource reference: " + family + "/" + role.Name);
            }
            return "PASS: " + clips + " exact-target clips (88 rotation curves each), 10 controllers, 2 resource sets; 7 states/controller; no root translation or scale curves.";
        }
        static void ValidateGenerated(AnimationClip clip, HashSet<string> paths, bool loop)
        {
            if (clip == null || clip.length <= 0f) throw new InvalidOperationException("Missing or empty generated enemy clip.");
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length != BoneNames.Length * 4 || bindings.Any(b => b.type != typeof(Transform) || !paths.Contains(b.path) || !b.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)))
                throw new InvalidOperationException("Root, position, scale, or unmapped curve in " + clip.name);
            foreach (var binding in bindings)
                if (AnimationUtility.GetEditorCurve(clip, binding).keys.Any(k => float.IsNaN(k.value) || float.IsInfinity(k.value)))
                    throw new InvalidOperationException("Nonfinite generated curve: " + clip.name);
            if (AnimationUtility.GetAnimationClipSettings(clip).loopTime != loop) throw new InvalidOperationException("Wrong loop flag: " + clip.name);
        }
    }
}
