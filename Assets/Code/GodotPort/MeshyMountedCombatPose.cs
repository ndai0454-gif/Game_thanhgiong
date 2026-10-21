using UnityEngine;

/// <summary>World-space mounted grips layered after the fitted upper-body clips.</summary>
[DefaultExecutionOrder(120)]
public sealed class MeshyMountedCombatPose : MonoBehaviour
{
    MeshyMountedRig rig;
    MeshyHeroVisualDriver fbxDriver;
    MountedHorseController movement;
    ThanhGiongCampaignController campaign;
    GodotMountedMotion motion;
    Transform leftArm, leftElbow, leftHand, rightArm, rightElbow, rightHand;
    Transform spine02, spine01, spine, leftShoulder, rightShoulder;
    Transform head, mount, socket, spear, bamboo;
    LineRenderer[] reins;
    Material reinMaterial;
    Material ironMaterial, shaftMaterial;
    Mesh spearheadMesh, runtimeHeroMesh;
    TrailRenderer ironTrail, bambooTrail;
    Vector3 bambooBasePosition;
    Quaternion bambooBaseRotation;
    MaterialPropertyBlock bambooStyleBlock;
    float pullRemaining, breakRemaining;
    Vector3 pullTarget, leftGrip, rightGrip;
    bool gripsInitialized;
    Transform[] poseBones;
    Vector3[] frozenPositions;
    Quaternion[] frozenRotations;
    bool hasFrozenPose;
    MeshyHeroEquipment equipment;
    Transform leftUpLeg, leftLeg, leftFoot, leftToe, rightUpLeg, rightLeg, rightFoot, rightToe, heroHead;
    // Horse barrel half-width at knee and stirrup height, in Mount space.
    const float KneeClearance = .36f, StirrupClearance = .38f;
    public bool IsReady => leftHand != null && rightHand != null && head != null && mount != null;
    public float ReinGripError { get; private set; }
    public bool IsPulling => pullRemaining > 0f;
    [Header("Held Bamboo Sizing")]
    [Tooltip("Tỉ lệ kích thước cây tre trên tay: X, Z là độ to (thân tre), Y là chiều dài.")]
    public Vector3 bambooScale = new Vector3(1.3f, 2.0f, 1.3f);

    public bool IsBreaking => breakRemaining > 0f;
    public bool HeldBambooVisible => bamboo != null && bamboo.gameObject.activeInHierarchy;
    public float HeldBambooSwayDegrees { get; private set; }
    public int HeldBambooRendererCount { get; private set; }

    void Start()
    {
        rig = GetComponent<MeshyMountedRig>();
        fbxDriver = GetComponentInParent<MeshyHeroVisualDriver>();
        movement = GetComponentInParent<MountedHorseController>();
        campaign = GetComponentInParent<ThanhGiongCampaignController>();
        motion = GetComponentInParent<GodotMountedMotion>();
        leftArm = Bone("LeftArm"); leftElbow = Bone("LeftForeArm"); leftHand = Bone("LeftHand");
        rightArm = Bone("RightArm"); rightElbow = Bone("RightForeArm"); rightHand = Bone("RightHand");
        spine02 = Bone("Spine02"); spine01 = Bone("Spine01"); spine = Bone("Spine");
        leftShoulder = Bone("LeftShoulder"); rightShoulder = Bone("RightShoulder");
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "head") head = t;
            if (t.name == "Mount") mount = t;
            if (t.name == "Right Hand Weapon Socket") socket = t;
            if (t.name == "Weapon_IronSword") spear = t;
            if (t.name == "Weapon_GoldenBamboo") bamboo = t;
        }
        if (!IsReady) return;
        Transform hero = fbxDriver != null ? fbxDriver.meshyHero : rig != null ? rig.heroAnimator.transform : null;
        equipment = GetComponent<MeshyHeroEquipment>();
        if (hero != null)
        {
            leftUpLeg = HeroBone(hero, "LeftUpLeg"); leftLeg = HeroBone(hero, "LeftLeg"); leftFoot = HeroBone(hero, "LeftFoot"); leftToe = HeroBone(hero, "LeftToeBase");
            rightUpLeg = HeroBone(hero, "RightUpLeg"); rightLeg = HeroBone(hero, "RightLeg"); rightFoot = HeroBone(hero, "RightFoot"); rightToe = HeroBone(hero, "RightToeBase");
            heroHead = HeroBone(hero, "Head");
            EnsureCleanHeroSkin(hero);
            poseBones = hero.GetComponentsInChildren<Transform>(true);
            frozenPositions = new Vector3[poseBones.Length]; frozenRotations = new Quaternion[poseBones.Length];
        }
        if (socket == null)
        {
            socket = new GameObject("Right Hand Weapon Socket").transform;
            socket.SetParent(rightHand, false);
            socket.localPosition = new Vector3(-.005f, .105f, -.012f);
            socket.rotation = mount.rotation;
        }
        else if (socket.parent == rightHand)
        {
            socket.localPosition = new Vector3(-.005f, .105f, -.012f);
        }
        if (spear == null)
        {
            spear = new GameObject("Weapon_IronSword").transform;
            spear.SetParent(socket, false);
        }
        BuildIronSpear();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        reinMaterial = new Material(shader) { name = "Mounted Leather Reins" };
        ironTrail = BuildTrail(spear, new Color(.65f, .9f, 1f, .9f));
        bambooTrail = BuildTrail(bamboo, new Color(.35f, 1f, .15f, .85f));
        ConfigureHeldBamboo();
        reins = new LineRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "Left Leather Rein" : "Right Leather Rein");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = reinMaterial;
            line.useWorldSpace = true; line.positionCount = 20;
            line.widthMultiplier = .017f; line.numCapVertices = 3;
            line.startColor = new Color(.38f, .09f, .06f, 1f);
            line.endColor = new Color(.24f, .06f, .04f, 1f);
            reins[i] = line;
        }
    }

    void EnsureCleanHeroSkin(Transform heroRoot)
    {
        var smr = heroRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null || smr.sharedMesh == null || smr.bones == null || smr.bones.Length < 20) return;
        if (!HasCrossWeightedArmVertices(smr.sharedMesh)) return;
        runtimeHeroMesh = Instantiate(smr.sharedMesh);
        runtimeHeroMesh.name = smr.sharedMesh.name + "_RuntimeClean";
        CleanHeroMeshWeights(runtimeHeroMesh, smr.bones);
        smr.sharedMesh = runtimeHeroMesh;
    }

    static bool HasCrossWeightedArmVertices(Mesh mesh)
    {
        var bw = mesh.boneWeights;
        if (bw == null || bw.Length == 0) return false;
        for (int i = 0; i < bw.Length; i += 17)
        {
            var w = bw[i];
            bool arm = w.boneIndex0 == 10 || w.boneIndex0 == 11 || w.boneIndex0 == 14 || w.boneIndex0 == 15
                    || w.boneIndex1 == 10 || w.boneIndex1 == 11 || w.boneIndex1 == 14 || w.boneIndex1 == 15;
            bool leg = w.boneIndex0 == 0 || w.boneIndex0 >= 16 || w.boneIndex1 == 0 || w.boneIndex1 >= 16;
            if (arm && leg && w.weight1 > .02f) return true;
        }
        return false;
    }

    public static void CleanHeroMeshWeights(Mesh mesh, Transform[] bones)
    {
        if (mesh == null) return;
        var verts = mesh.vertices;
        var bw = mesh.boneWeights;
        if (bw == null || bw.Length != verts.Length) return;

        Vector3 lArm = new Vector3(-.188f, 1.460f, .038f);
        Vector3 lFore = new Vector3(-.231f, 1.224f, .019f);
        Vector3 lHand = new Vector3(-.291f, 1.021f, .088f);
        Vector3 lTip = new Vector3(-.320f, .890f, .120f);

        Vector3 rArm = new Vector3(.231f, 1.464f, .036f);
        Vector3 rFore = new Vector3(.291f, 1.236f, .019f);
        Vector3 rHand = new Vector3(.355f, 1.033f, .082f);
        Vector3 rTip = new Vector3(.385f, .900f, .115f);

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            float dl = Mathf.Min(SegmentDistance(v, lArm, lFore), Mathf.Min(SegmentDistance(v, lFore, lHand), SegmentDistance(v, lHand, lTip)));
            float dr = Mathf.Min(SegmentDistance(v, rArm, rFore), Mathf.Min(SegmentDistance(v, rFore, rHand), SegmentDistance(v, rHand, rTip)));
            var w = bw[i];
            int[] idx = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] val = { w.weight0, w.weight1, w.weight2, w.weight3 };
            bool changed = false;

            if (dl < .10f && v.y < 1.38f)
            {
                for (int k = 0; k < 4; k++) if (idx[k] < 8 || idx[k] > 11) { val[k] = 0f; changed = true; }
            }
            else if (dr < .10f && v.y < 1.38f)
            {
                for (int k = 0; k < 4; k++) if (idx[k] < 12 || idx[k] > 15) { val[k] = 0f; changed = true; }
            }
            else
            {
                for (int k = 0; k < 4; k++)
                {
                    int b = idx[k];
                    if (b == 10 || b == 11 || b == 14 || b == 15 || (v.y < 1.26f && (b == 8 || b == 9 || b == 12 || b == 13)))
                    { val[k] = 0f; changed = true; }
                }
            }

            if (changed)
            {
                for (int a = 0; a < 3; a++)
                    for (int b = a + 1; b < 4; b++)
                        if (val[b] > val[a]) { float tv = val[a]; val[a] = val[b]; val[b] = tv; int ti = idx[a]; idx[a] = idx[b]; idx[b] = ti; }
                float sum = val[0] + val[1] + val[2] + val[3];
                if (sum > .0001f)
                    bw[i] = new BoneWeight { boneIndex0 = idx[0], weight0 = val[0] / sum, boneIndex1 = idx[1], weight1 = val[1] / sum, boneIndex2 = idx[2], weight2 = val[2] / sum, boneIndex3 = idx[3], weight3 = val[3] / sum };
            }
        }
        mesh.boneWeights = bw;
    }

    static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(.0001f, ab.sqrMagnitude));
        return Vector3.Distance(p, a + ab * t);
    }

    void ConfigureHeldBamboo()
    {
        if (bamboo == null) return;
        bamboo.localScale = bambooScale;
        if (bamboo.parent == rightHand)
        {
            bamboo.localPosition = new Vector3(-.005f, .105f, -.012f);
            bamboo.localRotation = Quaternion.Euler(78f, 0f, 0f);
        }
        bambooBasePosition = bamboo.localPosition;
        bambooBaseRotation = bamboo.localRotation;
        bambooStyleBlock = new MaterialPropertyBlock();
        Renderer[] renderers = bamboo.GetComponentsInChildren<Renderer>(true);
        HeldBambooRendererCount = renderers.Length;
        foreach (Renderer renderer in renderers)
        {
            renderer.GetPropertyBlock(bambooStyleBlock);
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                bambooStyleBlock.SetColor("_BaseColor", new Color(.78f, .92f, .42f, 1f));
            renderer.SetPropertyBlock(bambooStyleBlock);
        }
    }

    void BuildIronSpear()
    {
        if (spear == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return;
        foreach (Renderer r in spear.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        ironMaterial = new Material(shader) { name = "Iron Spear Steel", color = new Color(.40f, .52f, .62f) };
        ironMaterial.SetFloat("_Metallic", .8f); ironMaterial.SetFloat("_Smoothness", .55f);
        shaftMaterial = new Material(shader) { name = "Iron Spear Shaft", color = new Color(.18f, .095f, .035f) };
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Iron Spear Shaft";
        shaft.transform.SetParent(spear, false);
        shaft.transform.localPosition = Vector3.up * .42f;
        shaft.transform.localScale = new Vector3(.038f, .82f, .038f);
        var col = shaft.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        shaft.GetComponent<Renderer>().sharedMaterial = shaftMaterial;
        spearheadMesh = new Mesh { name = "Leaf Shaped Iron Spearhead" };
        spearheadMesh.vertices = new[] { new Vector3(0,.26f,0), new Vector3(0,-.22f,0), new Vector3(-.095f,0,0), new Vector3(.095f,0,0), new Vector3(0,0,.035f), new Vector3(0,0,-.035f) };
        spearheadMesh.triangles = new[] { 0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,3,4, 1,5,3, 1,2,5 };
        spearheadMesh.RecalculateNormals(); spearheadMesh.RecalculateBounds();
        var tip = new GameObject("Iron Spearhead", typeof(MeshFilter), typeof(MeshRenderer));
        tip.transform.SetParent(spear, false); tip.transform.localPosition = Vector3.up * 1.38f;
        tip.GetComponent<MeshFilter>().sharedMesh = spearheadMesh;
        tip.GetComponent<Renderer>().sharedMaterial = ironMaterial;
        if (campaign != null)
            spear.gameObject.SetActive(campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.IronSword);
    }

    TrailRenderer BuildTrail(Transform weapon, Color color)
    {
        if (weapon == null) return null;
        var go = new GameObject("Weapon Tip Motion Trail");
        go.transform.SetParent(weapon, false); go.transform.localPosition = Vector3.up * 1.55f;
        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = reinMaterial; trail.time = .16f; trail.minVertexDistance = .025f;
        trail.startWidth = .10f; trail.endWidth = 0f;
        trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.emitting = false;
        return trail;
    }

    Transform Bone(string suffix)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name.Replace("mixamorig:", "").Replace("mixamorig_", "") == suffix) return t;
        return null;
    }

    static Transform HeroBone(Transform hero, string name)
    {
        foreach (Transform t in hero.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static float Smooth01(float v) { v = Mathf.Clamp01(v); return v * v * (3f - 2f * v); }

    void SeatLegs()
    {
        SeatLeg(leftUpLeg, leftLeg, leftFoot, leftToe, -1f);
        SeatLeg(rightUpLeg, rightLeg, rightFoot, rightToe, 1f);
    }

    // Thighs wrap the barrel, knees forward and out, heels down in the stirrups. The stirrup
    // stays fixed to the horse, so the knees absorb the gallop bounce and the two-point rise.
    void SeatLeg(Transform hip, Transform knee, Transform foot, Transform toe, float side)
    {
        if (hip == null || knee == null || foot == null) return;
        float scale = Mathf.Max(Mathf.Abs(mount.lossyScale.x), 1e-4f);
        float thigh = Vector3.Distance(hip.position, knee.position) / scale;
        float shin = Vector3.Distance(knee.position, foot.position) / scale;
        Vector3 hipLocal = mount.InverseTransformPoint(hip.position);
        Vector3 kneeLocal = hipLocal + new Vector3(side * .50f, -.48f, .72f).normalized * thigh;
        kneeLocal.x = side * Mathf.Max(Mathf.Abs(kneeLocal.x), KneeClearance);
        Vector3 footLocal = kneeLocal + new Vector3(0f, -.86f, -.50f).normalized * (shin * .96f);
        footLocal.x = side * Mathf.Max(Mathf.Abs(footLocal.x), StirrupClearance);
        Vector3 pole = mount.TransformDirection(new Vector3(side * .75f, .1f, 1f)).normalized;
        SolveTwoBone(hip, knee, foot, mount.TransformPoint(footLocal), pole);
        if (toe != null)
        {
            Vector3 desired = mount.TransformDirection(new Vector3(side * .28f, -.12f, 1f)).normalized;
            Vector3 current = toe.position - foot.position;
            if (current.sqrMagnitude > 1e-8f) foot.rotation = Quaternion.FromToRotation(current, desired) * foot.rotation;
        }
    }

    static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        Vector3 origin = upper.position;
        float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, end.position);
        if (a < 1e-4f || b < 1e-4f) return;
        Vector3 delta = target - origin;
        float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 1e-3f, (a + b) * .985f);
        Vector3 dir = delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector3.down;
        Vector3 side = Vector3.ProjectOnPlane(pole, dir).normalized;
        if (side.sqrMagnitude < 1e-3f) side = Vector3.Cross(dir, Vector3.right).normalized;
        float along = (a * a - b * b + d * d) / (2f * d);
        Vector3 joint = origin + dir * along + side * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, joint - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + dir * d - lower.position) * lower.rotation;
    }

    public void PlayBreak() { breakRemaining = .65f; pullRemaining = 0f; }
    public void PlayBambooPull(Vector3 worldTarget) { pullRemaining = .6f; pullTarget = worldTarget; }

    void LateUpdate()
    {
        if (!IsReady || movement == null || campaign == null) return;
        if (Time.deltaTime <= 0f && hasFrozenPose)
        {
            // Animator still writes its sampled transforms at speed zero. Restore
            // the final mounted pose after that sample so pausing cannot undo IK.
            for (int i = 0; i < poseBones.Length; i++)
                if (poseBones[i] != null) { poseBones[i].localPosition = frozenPositions[i]; poseBones[i].localRotation = frozenRotations[i]; }
            return;
        }
        float dt = Time.deltaTime > 0f ? Time.deltaTime : (1f / 60f);
        pullRemaining = Mathf.Max(0f, pullRemaining - dt);
        breakRemaining = Mathf.Max(0f, breakRemaining - dt);
        float speed = Mathf.Clamp01(movement.PlanarSpeed / Mathf.Max(.1f, movement.runSpeed));
        float bob = motion != null ? Mathf.Sin(motion.GaitPhase * Mathf.PI * 2f) * .018f * speed : 0f;
        bool iron = campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.IronSword;
        bool armed = iron || campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo;
        bool actionPlaying = rig != null ? rig.IsActionPlaying : movement.AttackProgress >= 0f;

        float windup = motion != null ? motion.AttackWindup : 0f;
        float strike = motion != null ? motion.AttackStrike : 0f;
        float gait = motion != null ? motion.GaitPhase * Mathf.PI * 2f : 0f;
        float pull = IsPulling ? 1f - pullRemaining / .6f : -1f;
        float pullReach = pull >= 0f ? Smooth01(pull / .25f) * (1f - Smooth01((pull - .85f) / .15f)) : 0f;
        float heave = pull >= 0f ? Smooth01((pull - .4f) / .45f) * (1f - Smooth01((pull - .85f) / .15f)) : 0f;
        float pullSide = IsPulling && mount.InverseTransformPoint(pullTarget).x < 0f ? -1f : 1f;

        // Square the rider's upper torso forward when seated so the left shoulder isn't twisted
        // backward by the standing side-stance idle clip. The lean deepens into a gallop crouch
        // that pumps with the stride; a spear thrust coils on the wind-up and drives the chest
        // forward on contact; a bamboo pull leans out to the stalk, then heaves back.
        if (iron || !actionPlaying || IsPulling || campaign.EquipmentBusy)
        {
            float lean = 5f + 15f * speed + Mathf.Sin(gait * 2f) * 2.2f * speed;
            float twist = 0f, roll = 0f;
            if (iron && actionPlaying) { lean += 12f * strike - 7f * windup; twist = -18f * windup + 24f * strike; }
            if (IsPulling) { lean += 10f * pullReach - 8f * heave; roll = 12f * pullSide * pullReach; twist += 12f * pullSide * pullReach; }
            if (campaign.EquipmentBusy) lean = 3f;
            if (spine02 != null) spine02.localRotation = Quaternion.Euler(lean * .30f, twist * .25f, roll * .30f);
            if (spine01 != null) spine01.localRotation = Quaternion.Euler(lean * .40f, twist * .35f, roll * .35f);
            if (spine != null) spine.localRotation = Quaternion.Euler(lean * .30f, twist * .40f, roll * .35f);
            // Keep the eyes on the road while the chest leans.
            if (heroHead != null) heroHead.localRotation *= Quaternion.Euler(-lean * .45f, -twist * .3f, -roll * .4f);
        }
        SeatLegs();

        // Keep the grip in saddle space with a comfortable bent elbow above the withers.
        Vector3 leftTarget = leftArm.position + mount.TransformVector(new Vector3(.08f, -.33f + bob, .22f));
        Vector3 rightTarget = rightArm.position + mount.TransformVector(new Vector3(-.04f, -.31f + bob, .20f));
        if (iron)
            rightTarget += mount.TransformVector(new Vector3(.10f * windup - .04f * strike, .20f * windup + .04f * strike, -.24f * windup + .46f * strike));
        else if (!armed) rightTarget = rightArm.position + mount.TransformVector(new Vector3(-.08f, -.33f + bob, .22f));
        if (IsBreaking)
            rightTarget += mount.TransformVector(new Vector3(.16f, .12f, -.18f)) * Mathf.Sin((1f - breakRemaining / .65f) * Mathf.PI);
        if (IsPulling)
        {
            // Both hands close on the stalk, then heave it up and back toward the saddle.
            float mountScale = Mathf.Abs(mount.lossyScale.x);
            float armLength = (Vector3.Distance(rightArm.position, rightElbow.position) + Vector3.Distance(rightElbow.position, rightHand.position)) * .95f;
            Vector3 shoulder = pullSide > 0f ? rightArm.position : leftArm.position;
            Vector3 grip = shoulder + Vector3.ClampMagnitude(pullTarget - shoulder, armLength);
            grip += mount.up * (.30f * heave * mountScale) + (shoulder - grip) * (.40f * heave);
            rightTarget = Vector3.Lerp(rightTarget, grip + mount.up * (.05f * mountScale), pullReach);
            leftTarget = Vector3.Lerp(leftTarget, grip - mount.up * (.09f * mountScale), pullReach);
        }
        if (!gripsInitialized) { leftGrip = leftTarget; rightGrip = rightTarget; gripsInitialized = true; }
        float blend = 1f - Mathf.Exp(-22f * dt);
        leftGrip = Vector3.Lerp(leftGrip, leftTarget, blend);
        rightGrip = Vector3.Lerp(rightGrip, rightTarget, blend);
        if (!campaign.EquipmentBusy)
        {
            Vector3 leftPole = (-mount.up * .82f - mount.right * .48f - mount.forward * .30f).normalized;
            Vector3 leftPalm = (mount.up * .65f + mount.right * .55f + mount.forward * .35f).normalized;
            Solve(leftArm, leftElbow, leftHand, leftGrip, leftPole, mount, leftPalm);
            // Bamboo uses the fitted attack clips; the left hand retains the reins.
            if (iron || !actionPlaying || IsPulling || IsBreaking)
            {
                Vector3 rightPole = (-mount.up * .82f + mount.right * .48f - mount.forward * .30f).normalized;
                Vector3 rightPalm = (mount.up * .65f - mount.right * .55f + mount.forward * .35f).normalized;
                Solve(rightArm, rightElbow, rightHand, rightGrip, rightPole, mount, rightPalm);
                if (armed && socket != null)
                {
                    Vector3 direction = (mount.up * (1f - .92f * strike) + mount.forward * (.16f + 1.5f * strike)).normalized;
                    socket.rotation = Quaternion.FromToRotation(mount.up, direction) * mount.rotation;
                }
            }
        }
        else if (equipment != null && motion != null &&
                 equipment.Animate(motion.CurrentEquipmentAction, motion.EquipmentProgress, mount, out Vector3 leftItem, out Vector3 rightItem, out float itemReach))
        {
            // Both hands carry the helmet or breastplate along its path.
            Vector3 leftPole = (-mount.up * .5f - mount.right * .8f).normalized;
            Vector3 rightPole = (-mount.up * .5f + mount.right * .8f).normalized;
            Solve(leftArm, leftElbow, leftHand, Vector3.Lerp(leftGrip, leftItem, itemReach), leftPole, mount, mount.right);
            Solve(rightArm, rightElbow, rightHand, Vector3.Lerp(rightGrip, rightItem, itemReach), rightPole, mount, -mount.right);
        }
        ReinGripError = Vector3.Distance(leftHand.position, leftGrip);
        DrawReins(speed);
        bool swinging = movement.AttackProgress >= .05f && movement.AttackProgress < .8f && !campaign.EquipmentBusy;
        if (ironTrail != null) ironTrail.emitting = swinging && iron;
        if (bambooTrail != null) bambooTrail.emitting = swinging && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo;
        if (poseBones != null)
        {
            for (int i = 0; i < poseBones.Length; i++)
                if (poseBones[i] != null) { frozenPositions[i] = poseBones[i].localPosition; frozenRotations[i] = poseBones[i].localRotation; }
            hasFrozenPose = true;
        }
        AnimateHeldBamboo(speed, strike);
    }

    void AnimateHeldBamboo(float speed, float strike)
    {
        if (bamboo == null) return;
        if (!HeldBambooVisible)
        {
            bamboo.localPosition = bambooBasePosition;
            bamboo.localRotation = bambooBaseRotation;
            HeldBambooSwayDegrees = 0f;
            return;
        }

        float gait = motion != null ? motion.GaitPhase * Mathf.PI * 2f : Time.time * 4f;
        float idleSway = Mathf.Sin(Time.time * 1.7f) * .55f + Mathf.Sin(gait) * Mathf.Lerp(.7f, 2.4f, speed);
        float strikeFlex = Mathf.Sin(strike * Mathf.PI) * 4.2f;
        HeldBambooSwayDegrees = idleSway + strikeFlex;
        bamboo.localPosition = bambooBasePosition + Vector3.up * (Mathf.Sin(gait * .5f) * .006f * speed);
        bamboo.localRotation = bambooBaseRotation * Quaternion.Euler(HeldBambooSwayDegrees, 0f, -idleSway * .35f);
        bamboo.localScale = bambooScale;
    }

    static void Solve(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole, Transform mount, Vector3 palmNormal)
    {
        if (upper == null || lower == null || hand == null) return;
        Vector3 origin = upper.position;
        float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, hand.position);
        if (a < .001f || b < .001f) return;
        Vector3 delta = target - origin;
        float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, (a + b) * .96f);
        Vector3 direction = delta.sqrMagnitude > .000001f ? delta.normalized : Vector3.down;
        Vector3 side = Vector3.ProjectOnPlane(pole, direction).normalized;
        if (side.sqrMagnitude < .001f) side = Vector3.Cross(direction, Vector3.forward).normalized;
        float along = (a * a - b * b + d * d) / (2f * d);
        Vector3 elbow = origin + direction * along + side * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
        Vector3 reachable = origin + direction * d;
        Vector3 foreDir = (reachable - lower.position).normalized;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, foreDir) * lower.rotation;
        if (mount != null)
        {
            Vector3 desiredHandUp = (foreDir * .92f + mount.forward * .25f - mount.up * .10f).normalized;
            hand.rotation = Quaternion.FromToRotation(hand.up, desiredHandUp) * hand.rotation;
            Vector3 currentPalm = Vector3.ProjectOnPlane(hand.forward, desiredHandUp).normalized;
            Vector3 targetPalm = Vector3.ProjectOnPlane(palmNormal, desiredHandUp).normalized;
            if (currentPalm.sqrMagnitude > .001f && targetPalm.sqrMagnitude > .001f)
                hand.rotation = Quaternion.FromToRotation(currentPalm, targetPalm) * hand.rotation;
        }
    }

    void DrawReins(float speed)
    {
        if (reins == null) return;
        float scale = Mathf.Abs(mount.lossyScale.x);
        Vector3 fistGrip = leftHand.TransformPoint(new Vector3(.012f, .095f, .007f));
        for (int side = 0; side < 2; side++)
        {
            float sgn = side == 0 ? -1f : 1f;
            Vector3 start = head.TransformPoint(new Vector3(sgn * .086f, -.055f, .375f));
            Vector3 gripEntry = fistGrip + mount.right * (sgn * .014f * scale) + mount.forward * (.018f * scale) + mount.up * (.010f * scale);
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                Vector3 p = Vector3.Lerp(start, gripEntry, t);
                float sag = Mathf.Sin(t * Mathf.PI) * (.058f - speed * .022f) * scale;
                float bowProfile = Mathf.Sin(Mathf.Pow(t, .78f) * Mathf.PI);
                float neckBow = bowProfile * (side == 0 ? .095f : .125f) * scale;
                reins[side].SetPosition(i, p - mount.up * sag + mount.right * (sgn * neckBow));
            }
            reins[side].SetPosition(16, fistGrip + mount.right * (sgn * .007f * scale));
            reins[side].SetPosition(17, fistGrip - mount.up * (.018f * scale) - mount.forward * (.010f * scale));
            reins[side].SetPosition(18, fistGrip - mount.up * (.055f * scale) - mount.forward * (.018f * scale) + mount.right * (sgn * .006f * scale));
            reins[side].SetPosition(19, fistGrip - mount.up * (.092f * scale) - mount.forward * (.022f * scale) + mount.right * (sgn * .010f * scale));
            reins[side].widthMultiplier = .017f * scale;
        }
    }

    void OnDestroy()
    {
        if (reinMaterial != null) Destroy(reinMaterial);
        if (ironMaterial != null) Destroy(ironMaterial);
        if (shaftMaterial != null) Destroy(shaftMaterial);
        if (spearheadMesh != null) Destroy(spearheadMesh);
        if (runtimeHeroMesh != null) Destroy(runtimeHeroMesh);
    }
}
