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
    Transform head, mount, socket, spear, bamboo;
    LineRenderer[] reins;
    Material reinMaterial;
    Material ironMaterial, shaftMaterial;
    Mesh spearheadMesh;
    TrailRenderer ironTrail, bambooTrail;
    float pullRemaining, breakRemaining;
    Vector3 pullTarget, leftGrip, rightGrip;
    bool gripsInitialized;
    Transform[] poseBones;
    Vector3[] frozenPositions;
    Quaternion[] frozenRotations;
    bool hasFrozenPose;
    public bool IsReady => leftHand != null && rightHand != null && head != null && mount != null;
    public float ReinGripError { get; private set; }
    public bool IsPulling => pullRemaining > 0f;
    public bool IsBreaking => breakRemaining > 0f;

    void Start()
    {
        rig = GetComponent<MeshyMountedRig>();
        fbxDriver = GetComponentInParent<MeshyHeroVisualDriver>();
        movement = GetComponentInParent<MountedHorseController>();
        campaign = GetComponentInParent<ThanhGiongCampaignController>();
        motion = GetComponentInParent<GodotMountedMotion>();
        leftArm = Bone("LeftArm"); leftElbow = Bone("LeftForeArm"); leftHand = Bone("LeftHand");
        rightArm = Bone("RightArm"); rightElbow = Bone("RightForeArm"); rightHand = Bone("RightHand");
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
        if (hero != null)
        {
            poseBones = hero.GetComponentsInChildren<Transform>(true);
            frozenPositions = new Vector3[poseBones.Length]; frozenRotations = new Quaternion[poseBones.Length];
        }
        if (socket == null)
        {
            socket = new GameObject("Right Hand Weapon Socket").transform;
            socket.SetParent(rightHand, false);
            socket.rotation = mount.rotation;
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
        reins = new LineRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "Left Leather Rein" : "Right Leather Rein");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = reinMaterial;
            line.useWorldSpace = true; line.positionCount = 16;
            line.widthMultiplier = .018f; line.numCapVertices = 3;
            line.startColor = line.endColor = new Color(.22f, .10f, .035f, 1f);
            reins[i] = line;
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
        Destroy(shaft.GetComponent<Collider>());
        shaft.GetComponent<Renderer>().sharedMaterial = shaftMaterial;
        spearheadMesh = new Mesh { name = "Leaf Shaped Iron Spearhead" };
        spearheadMesh.vertices = new[] { new Vector3(0,.26f,0), new Vector3(0,-.22f,0), new Vector3(-.095f,0,0), new Vector3(.095f,0,0), new Vector3(0,0,.035f), new Vector3(0,0,-.035f) };
        spearheadMesh.triangles = new[] { 0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,3,4, 1,5,3, 1,2,5 };
        spearheadMesh.RecalculateNormals(); spearheadMesh.RecalculateBounds();
        var tip = new GameObject("Iron Spearhead", typeof(MeshFilter), typeof(MeshRenderer));
        tip.transform.SetParent(spear, false); tip.transform.localPosition = Vector3.up * 1.38f;
        tip.GetComponent<MeshFilter>().sharedMesh = spearheadMesh;
        tip.GetComponent<Renderer>().sharedMaterial = ironMaterial;
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

    public void PlayBreak() { breakRemaining = .65f; pullRemaining = 0f; }
    public void PlayBambooPull(Vector3 worldTarget) { pullRemaining = .6f; pullTarget = worldTarget; }

    void LateUpdate()
    {
        if (!IsReady || movement == null || campaign == null) return;
        if (Time.deltaTime <= 0f)
        {
            // Animator still writes its sampled transforms at speed zero. Restore
            // the final mounted pose after that sample so pausing cannot undo IK.
            if (hasFrozenPose)
                for (int i = 0; i < poseBones.Length; i++)
                    if (poseBones[i] != null) { poseBones[i].localPosition = frozenPositions[i]; poseBones[i].localRotation = frozenRotations[i]; }
            return;
        }
        float dt = Time.deltaTime;
        pullRemaining = Mathf.Max(0f, pullRemaining - dt);
        breakRemaining = Mathf.Max(0f, breakRemaining - dt);
        // Keep the grip in saddle space while the chest absorbs the horse's gait.
        float speed = Mathf.Clamp01(movement.PlanarSpeed / Mathf.Max(.1f, movement.runSpeed));
        float bob = motion != null ? Mathf.Sin(motion.GaitPhase * Mathf.PI * 2f) * .018f * speed : 0f;
        Vector3 leftTarget = leftArm.position + mount.TransformVector(new Vector3(.06f, -.27f + bob, .34f));
        Vector3 rightTarget = rightArm.position + mount.TransformVector(new Vector3(-.02f, -.27f + bob, .25f));
        bool iron = campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.IronSword;
        bool armed = iron || campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo;
        float windup = motion != null ? motion.AttackWindup : 0f;
        float strike = motion != null ? motion.AttackStrike : 0f;
        if (iron)
            rightTarget += mount.TransformVector(new Vector3(.08f * windup, .12f * windup, -.18f * windup + .25f * strike));
        else if (!armed) rightTarget = rightArm.position + mount.TransformVector(new Vector3(-.13f, -.28f, .33f));
        if (IsBreaking)
            rightTarget += mount.TransformVector(new Vector3(.16f, .12f, -.18f)) * Mathf.Sin((1f - breakRemaining / .65f) * Mathf.PI);
        if (IsPulling)
        {
            float p = 1f - pullRemaining / .6f;
            Vector3 reach = Vector3.ClampMagnitude(pullTarget - rightArm.position, .52f * Mathf.Abs(mount.lossyScale.x));
            Vector3 target = rightArm.position + reach;
            target += mount.up * (.32f * Mathf.SmoothStep(0f, 1f, p));
            rightTarget = Vector3.Lerp(rightTarget, target, Mathf.Sin(p * Mathf.PI));
        }
        if (!gripsInitialized) { leftGrip = leftTarget; rightGrip = rightTarget; gripsInitialized = true; }
        float blend = 1f - Mathf.Exp(-22f * dt);
        leftGrip = Vector3.Lerp(leftGrip, leftTarget, blend);
        rightGrip = Vector3.Lerp(rightGrip, rightTarget, blend);
        if (!campaign.EquipmentBusy)
        {
            Solve(leftArm, leftElbow, leftHand, leftGrip, -mount.right);
            // Bamboo uses the three fitted attack clips; the left hand retains the reins.
            bool actionPlaying = rig != null ? rig.IsActionPlaying : movement.AttackProgress >= 0f;
            if (iron || !actionPlaying || IsPulling || IsBreaking)
            {
                Solve(rightArm, rightElbow, rightHand, rightGrip, mount.right);
                if (armed && socket != null)
                {
                    Vector3 direction = (mount.up * (1f - .92f * strike) + mount.forward * (.16f + 1.5f * strike)).normalized;
                    // Align the held prop at its socket without twisting the FBX
                    // wrist skin, whose armor weights extend into the forearm.
                    socket.rotation = Quaternion.FromToRotation(mount.up, direction) * mount.rotation;
                }
            }
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
    }

    static void Solve(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole)
    {
        if (upper == null || lower == null || hand == null) return;
        Vector3 origin = upper.position;
        float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, hand.position);
        if (a < .001f || b < .001f) return;
        Vector3 delta = target - origin;
        float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
        Vector3 direction = delta.sqrMagnitude > .000001f ? delta.normalized : Vector3.down;
        Vector3 side = Vector3.ProjectOnPlane(pole, direction).normalized;
        if (side.sqrMagnitude < .001f) side = Vector3.Cross(direction, Vector3.forward).normalized;
        float along = (a * a - b * b + d * d) / (2f * d);
        Vector3 elbow = origin + direction * along + side * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
        Vector3 reachable = origin + direction * d;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, reachable - lower.position) * lower.rotation;
    }

    void DrawReins(float speed)
    {
        if (reins == null) return;
        float scale = Mathf.Abs(mount.lossyScale.x);
        for (int side = 0; side < 2; side++)
        {
            Vector3 start = head.position + mount.TransformVector(new Vector3(side == 0 ? -.105f : .105f, -.07f, .15f));
            Vector3 end = leftHand.position + mount.right * ((side == 0 ? -.015f : .015f) * scale);
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                float sag = Mathf.Sin(t * Mathf.PI) * (.08f - speed * .035f) * scale;
                reins[side].SetPosition(i, Vector3.Lerp(start, end, t) - mount.up * sag);
            }
            reins[side].widthMultiplier = .018f * scale;
        }
    }

    void OnDestroy()
    {
        if (reinMaterial != null) Destroy(reinMaterial);
        if (ironMaterial != null) Destroy(ironMaterial);
        if (shaftMaterial != null) Destroy(shaftMaterial);
        if (spearheadMesh != null) Destroy(spearheadMesh);
    }
}
