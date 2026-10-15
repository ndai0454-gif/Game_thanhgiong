using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Native Unity driver for the Godot-exported iron horse and seated rider skins.</summary>
[DefaultExecutionOrder(80)]
[RequireComponent(typeof(MountedHorseController))]
public sealed class GodotMountedMotion : MonoBehaviour
{
    public Transform visual;
    [Tooltip("glTFast mirrors local X; the imported prefab's 180 degree root supplies the world Z reflection.")]
    public bool mirrorX = true;
    public float walkStride = 1.7f, gallopStride = 3.6f, equipmentDuration = 1.7f;
    public bool flying;
    public bool IsBusy => action != null || pending.Count != 0;
    public bool IsRigReady => horse.ContainsKey("body");
    public int HorseBoneCount => horse.Count;
    public int RiderBoneCount => rider.Count;
    public float GaitPhase => cycle;
    public float MotionTime => clock;
    public float AttackWindup { get; private set; }
    public float AttackStrike { get; private set; }
    public float EquipmentProgress => action == null ? 0f : Mathf.Clamp01(elapsed / Mathf.Max(equipmentDuration, .1f));
    public int HoofContactCount { get; private set; }
    public Vector3 LastHoofContact { get; private set; }
    public int ActiveContactParticles => contacts != null ? contacts.particleCount : 0;
    public event Action EquipmentFinished;

    static readonly string[] HorseNames = { "body", "neck", "head", "tail", "tail_tip", "fl_upper", "fl_lower", "fl_hoof", "fr_upper", "fr_lower", "fr_hoof", "rl_upper", "rl_lower", "rl_hoof", "rr_upper", "rr_lower", "rr_hoof" };
    static readonly string[] RiderNames = { "pelvis", "spine", "chest", "head", "ribbon", "cape", "cape_low", "upperarm_r", "forearm_r", "hand_r", "upperarm_l", "forearm_l", "hand_l", "thigh_r", "shin_r", "thigh_l", "shin_l", "ribbon_l", "cape_hem", "cape_edge_r", "cape_edge_l" };
    static readonly string[] LegPrefixes = { "fl", "fr", "rl", "rr" };
    sealed class Bone
    {
        public Transform t; public Quaternion rest; public Vector3 position;
        public Bone(Transform transform) { t = transform; rest = t.localRotation; position = t.localPosition; }
    }
    readonly Dictionary<string, Bone> horse = new Dictionary<string, Bone>();
    readonly Dictionary<string, Bone> rider = new Dictionary<string, Bone>();
    readonly Dictionary<string, Vector2> springs = new Dictionary<string, Vector2>();
    readonly Queue<string> pending = new Queue<string>();
    readonly List<GameObject> weapons = new List<GameObject>();
    readonly List<bool> weaponStates = new List<bool>();
    MountedHorseController movement;
    ThanhGiongCampaignAudio audioSource;
    ThanhGiongCampaignController campaign;
    Transform mount, helmet, armor;
    Vector3 mountPosition;
    Quaternion mountRotation;
    float clock, cycle, move, gallop, air, attack = -1f, previousYaw, previousSpeed;
    bool wasGrounded = true, helmetWorn, armorWorn, configured;
    string action;
    float elapsed, cycleTravel;
    readonly float[] legPhases = new float[4];
    readonly bool[] contactsPending = new bool[4];
    readonly RaycastHit[] contactHits = new RaycastHit[16];
    ParticleSystem contacts;
    Material contactMaterial;
    Texture2D contactTexture;
    ActionForgeMountedClip swordAnimation;
    MeshyMountedRig meshyRig;
    public bool HasActionForgeSwordAnimation => swordAnimation != null;
    public float ActionForgeSwordWeight { get; private set; }

    void Awake()
    {
        movement = GetComponent<MountedHorseController>();
        if (movement != null) movement.DodgeStarted += OnDodgeStarted;
        audioSource = GetComponent<ThanhGiongCampaignAudio>();
        campaign = GetComponent<ThanhGiongCampaignController>();
        previousYaw = transform.eulerAngles.y;
        if (visual != null) Configure(visual);
    }

    public void Configure(Transform exportedVisual)
    {
        if (configured && visual == exportedVisual) return;
        visual = exportedVisual;
        meshyRig = visual != null ? visual.GetComponentInChildren<MeshyMountedRig>(true) : null;
        bool fbxRider = GetComponent<MeshyHeroVisualDriver>() != null;
        if (meshyRig != null || fbxRider) mirrorX = false;
        movement = GetComponent<MountedHorseController>();
        horse.Clear(); rider.Clear(); springs.Clear();
        if (visual == null) return;
        Transform horseModel = Find(visual, "IronHorse"), riderModel = Find(visual, "ThanhGiong");
        Bind(horseModel, HorseNames, horse); Bind(riderModel, RiderNames, rider);
        swordAnimation = Resources.Load<ActionForgeMountedClip>("ActionForge/SwordAttackMounted");
        mount = Find(visual, "Mount");
        if (mount != null) { mountPosition = mount.localPosition; mountRotation = mount.localRotation; }
        helmet = Find(visual, "IronHelmet"); armor = Find(visual, "IronArmor");
        if (helmet != null) helmet.gameObject.SetActive(false);
        if (armor != null) armor.gameObject.SetActive(false);
        // The exported skin already owns its cape and skeletal motion.
        foreach (GeneratedCharacterMotion legacy in visual.GetComponentsInChildren<GeneratedCharacterMotion>(true)) legacy.enabled = false;
        foreach (Animator animator in visual.GetComponentsInChildren<Animator>(true))
            animator.enabled = (meshyRig != null && animator == meshyRig.heroAnimator) || animator.name.IndexOf("Meshy", StringComparison.OrdinalIgnoreCase) >= 0;
        ThanhGiongRibbonCape cape = GetComponent<ThanhGiongRibbonCape>();
        if (cape != null) cape.enabled = meshyRig == null && !fbxRider;
        configured = true;
    }

    static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
        // Godot's GLB exporter globally uniquifies duplicated bone names (rider head2).
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.StartsWith(name, StringComparison.Ordinal) || child.name.Length == name.Length) continue;
            bool numeric = true;
            for (int i = name.Length; i < child.name.Length; i++) if (!char.IsDigit(child.name[i])) { numeric = false; break; }
            if (numeric) return child;
        }
        return null;
    }
    static void Bind(Transform root, string[] names, Dictionary<string, Bone> target)
    {
        foreach (string name in names) { Transform t = Find(root, name); if (t != null) target[name] = new Bone(t); }
    }

    void LateUpdate()
    {
        if (!configured) Configure(movement != null ? movement.visual : transform.Find("Visual"));
        if (!IsRigReady || Time.deltaTime <= 0f) return;
        float dt = Time.deltaTime;
        float speed = flying ? 9f * Growth : movement.PlanarSpeed;
        float turn = -Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) * Mathf.Deg2Rad / Mathf.Max(dt, .001f);
        float acceleration = (speed - previousSpeed) / Mathf.Max(dt, .001f);
        previousYaw = transform.eulerAngles.y; previousSpeed = speed;
        bool grounded = flying || movement.Grounded;
        Drive(dt, speed, flying ? .92f : Mathf.Clamp01(speed / Mathf.Max(movement.runSpeed, .01f)), Growth, grounded, flying ? 0f : movement.VerticalSpeed, turn, acceleration, flying ? -1f : movement.AttackProgress, grounded && !wasGrounded);
        wasGrounded = grounded;
        ContactFeedback(speed, grounded);
    }

    float Growth => visual != null ? Mathf.Max(Mathf.Abs(visual.lossyScale.x), .05f) : 1f;
    public void SetFlying(bool value) { flying = value; }
    static float SS(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
    static float Smooth(float a, float b, float v) { return SS((v - a) / (b - a)); }
    static float Noise(float t, float seed) { return (Mathf.Sin(t * .73f + seed) + .6f * Mathf.Sin(t * 1.37f + seed * 2.1f) + .35f * Mathf.Sin(t * 2.91f + seed * 3.7f)) / 1.95f; }
    Vector3 V(Vector3 value) { return mirrorX ? new Vector3(-value.x, value.y, value.z) : value; }
    Quaternion Q(Vector3 degrees)
    {
        // Godot's default Euler order is YXZ; Unity's Quaternion.Euler uses ZXY.
        Quaternion q = Quaternion.AngleAxis(degrees.y, Vector3.up) * Quaternion.AngleAxis(degrees.x, Vector3.right) * Quaternion.AngleAxis(degrees.z, Vector3.forward);
        return mirrorX ? new Quaternion(q.x, -q.y, -q.z, q.w) : q;
    }
    float Spring(string key, float target, float frequency, float damping, float dt)
    {
        Vector2 state; if (!springs.TryGetValue(key, out state)) state = new Vector2(target, 0);
        float omega = 2f * Mathf.PI * frequency;
        // Subdivide the spring solver, preserving the full scaled frame time.
        // Clamping the animation delta made 1.7-second gestures stall on slow render frames.
        int steps = Mathf.Max(1, Mathf.Max(Mathf.CeilToInt(dt * omega / .5f), Mathf.CeilToInt(dt / .05f)));
        float h = dt / steps;
        for (int i = 0; i < steps; i++) { state.y += (omega * omega * (target - state.x) - 2f * damping * omega * state.y) * h; state.x += state.y * h; }
        springs[key] = state; return state.x;
    }
    void Kick(string key, float velocity) { Vector2 state; springs.TryGetValue(key, out state); state.y += velocity; springs[key] = state; }
    Vector3 VS(string key, Vector3 target, float f, float d, float dt) { return new Vector3(Spring(key + "x", target.x, f, d, dt), Spring(key + "y", target.y, f, d, dt), Spring(key + "z", target.z, f, d, dt)); }
    void Pose(Dictionary<string, Bone> rig, string name, Vector3 degrees) { Bone b; if (rig.TryGetValue(name, out b)) b.t.localRotation = b.rest * Q(degrees); }
    float Rear => attack < 0f ? 0f : (AttackWindup * .85f + AttackStrike * .12f) * (movement != null && movement.attackDuration > .45f ? 1f : .22f);

    // Damage and its visual follow-through share the campaign's 0.12-second contact beat.
    // Durations differ for iron (.32s) and bamboo (.58s), so normalized constants drifted.
    public static void EvaluateAttack(float progress, float duration, out float windup, out float strike)
    {
        windup = strike = 0f;
        if (progress < 0f) return;
        float contact = Mathf.Clamp(.12f / Mathf.Max(duration, .1f), .12f, .75f);
        float prepared = Mathf.Max(.02f, contact - .045f / Mathf.Max(duration, .1f));
        float followThrough = Mathf.Min(.9f, contact + .055f / Mathf.Max(duration, .1f));
        windup = SS(progress / prepared) * (1f - Smooth(prepared, contact, progress));
        strike = Smooth(prepared, contact, progress) * (1f - Smooth(followThrough, 1f, progress));
    }

    /// <summary>Explicit drive entry also allows deterministic integration checks without keyboard input.</summary>
    public void Drive(float dt, float speed, float amount, float growth, bool grounded, float vy, float turn, float acceleration, float attackProgress, bool landed)
    {
        if (dt <= 0f || !IsRigReady) return;
        clock += dt; attack = attackProgress;
        EvaluateAttack(attack, movement != null ? movement.attackDuration : .58f, out float windup, out float strike);
        AttackWindup = windup; AttackStrike = strike;
        move = Spring("move", SS(amount / .14f), 1.6f, 1f, dt);
        gallop = Spring("gallop", SS((amount - .5f) / .3f), 1.1f, 1f, dt);
        air = Spring("air", grounded ? 0f : 1f, grounded ? 3f : 2.2f, .9f, dt);
        cycleTravel = Mathf.Max(0f, speed) / Mathf.Max(Mathf.Lerp(walkStride, gallopStride, gallop) * Mathf.Max(growth, .05f), .05f) * dt;
        cycle = Mathf.Repeat(cycle + cycleTravel, 1f);
        if (landed) Kick("land", -2.2f);
        float land = Spring("land", 0f, 2.6f, .45f, dt);
        turn = Mathf.Clamp(turn, -3f, 3f); acceleration = Mathf.Clamp(acceleration, -14f, 14f);
        DriveBody(dt, turn, acceleration, vy, land);
        DriveHorse(dt, turn);
        if (rider.ContainsKey("pelvis")) DriveRider(dt, turn, acceleration, land);
        ApplyActionForgeSwordAttack();
        DriveEquipment(dt);
    }

    void ApplyActionForgeSwordAttack()
    {
        ActionForgeSwordWeight = 0f;
        if (meshyRig != null || swordAnimation == null || swordAnimation.tracks == null || attack < 0f || flying || IsBusy ||
            movement != null && movement.IsDodging || campaign == null || campaign.CurrentWeapon != ThanhGiongCampaignController.Weapon.IronSword) return;
        // Blend over the existing mounted pose. Only exported upper-body rotations
        // are touched; the horse, saddle, rider position and legs keep their own motion.
        float duration = movement != null ? movement.attackDuration : .58f;
        float weight = Smooth(0f, .07f / Mathf.Max(duration, .1f), attack) * (1f - Smooth(.76f, 1f, attack));
        float sourceTime = swordAnimation.SourceTime(attack, duration);
        foreach (var track in swordAnimation.tracks)
        {
            Bone bone;
            if (rider.TryGetValue(track.bone, out bone))
                bone.t.localRotation = Quaternion.Slerp(bone.t.localRotation, track.Evaluate(sourceTime), weight);
        }
        ActionForgeSwordWeight = weight;
    }

    void DriveBody(float dt, float turn, float accel, float vy, float land)
    {
        if (mount == null) return;
        float walk = move * (1f - gallop), phase = cycle * Mathf.PI * 2f;
        float bob = walk * .035f * (.5f - .5f * Mathf.Cos(phase * 2f)) + gallop * move * .11f * (.5f - .5f * Mathf.Cos(phase - .6f));
        float pitch = walk * 1.2f * Mathf.Sin(phase * 2f + .8f) + gallop * move * 5.5f * Mathf.Sin(phase + .9f);
        pitch += Spring("accel_pitch", Mathf.Clamp(-accel * .9f, -9f, 9f), 1.4f, .65f, dt);
        pitch += Spring("air_pitch", Mathf.Clamp(-vy * 2.4f, -14f, 14f) * (air > .05f ? 1f : 0f), 2f, .8f, dt) - 24f * Rear;
        float roll = Spring("bank", Mathf.Clamp(-turn * 6.5f, -13f, 13f) * (.35f + .65f * move), 1.5f, .75f, dt) + walk * 1.6f * Mathf.Sin(phase);
        float evade = movement != null && movement.IsDodging ? Mathf.Sin(movement.DodgeProgress * Mathf.PI) : 0f;
        roll -= evade * (movement != null ? movement.DodgeLateral : 0f) * 18f;
        pitch -= evade * 10f;
        mount.localRotation = mountRotation * Q(new Vector3(pitch, 0, roll));
        mount.localPosition = mountPosition + V(new Vector3(0, bob + (1f - move) * .012f * Mathf.Sin(clock * 1.9f) + land * .08f + Rear * .30f - evade * .07f, Rear * .12f));
    }
    Vector2 LegWave(float offset, float duty)
    {
        float p = Mathf.Repeat(cycle + offset, 1f);
        if (p < duty) return new Vector2(-Mathf.Cos(Mathf.PI * p / duty), 0);
        float s = (p - duty) / (1f - duty); return new Vector2(Mathf.Cos(Mathf.PI * s), Mathf.Sin(Mathf.PI * s));
    }
    void DriveHorse(float dt, float turn)
    {
        float b = gallop, duty = Mathf.Lerp(.64f, .40f, b), phase = cycle * Mathf.PI * 2f;
        for (int i = 0; i < 4; i++)
        {
            float offset = i == 0 ? Mathf.Lerp(.25f, .46f, b) : i == 1 ? Mathf.Lerp(.75f, .58f, b) : i == 2 ? 0f : Mathf.Lerp(.5f, .12f, b);
            float legPhase = Mathf.Repeat(cycle + offset, 1f);
            contactsPending[i] = cycleTravel > .0001f && (legPhase < legPhases[i] && legPhases[i] > .65f || cycleTravel >= 1f);
            legPhases[i] = legPhase;
            bool front = i < 2; Vector2 wave = LegWave(offset, duty); float lift = wave.y * move;
            float upper = wave.x * (front ? Mathf.Lerp(13f, 22f, b) : Mathf.Lerp(12f, 20f, b)) * move;
            float lower = front ? lift * Mathf.Lerp(24f, 35f, b) : lift * Mathf.Lerp(16f, 25f, b) + 2f * move;
            // Counter-rotate the hoof against the knee/hock so the sole stays natural during lift and stance.
            float hoof = front ? -lift * Mathf.Lerp(14f, 20f, b) - (1f - wave.y) * 2f * move : -lift * Mathf.Lerp(10f, 16f, b) - 2f * move;
            if (!front) upper += 3f * move;
            upper = Mathf.Lerp(upper, front ? -42f : 28f, air); lower = Mathf.Lerp(lower, front ? 78f : 26f, air); hoof = Mathf.Lerp(hoof, front ? -28f : -18f, air);
            if (Rear > 0f)
            {
                float paw = Mathf.Sin(clock * 14f + i * 1.7f);
                upper = Mathf.Lerp(upper, front ? -56f + paw * 12f : 18f, Rear);
                lower = Mathf.Lerp(lower, front ? 68f - paw * 15f : 10f, Rear);
                if (front) hoof = Mathf.Lerp(hoof, -24f, Rear);
            }
            Pose(horse, LegPrefixes[i] + "_upper", new Vector3(Spring("leg" + i + "u", upper, 9f, 1f, dt), 0, 0));
            Pose(horse, LegPrefixes[i] + "_lower", new Vector3(Spring("leg" + i + "l", lower, 9f, 1f, dt), 0, 0));
            Pose(horse, LegPrefixes[i] + "_hoof", new Vector3(Spring("leg" + i + "h", hoof, 9f, 1f, dt), 0, 0));
        }
        float walk = move * (1f - b), idle = 1f - move;
        float look = Noise(clock * .55f, 1.3f) * 16f * idle + Mathf.Clamp(turn * 9f, -16f, 16f);
        float neck = walk * 4.5f * Mathf.Sin(phase * 2f + .4f) + b * move * 9f * Mathf.Sin(phase - .5f) - 6f * b * move + idle * (Noise(clock * .4f, 4f) * 5f + 2f) - 30f * Rear - 10f * air;
        float head = walk * 3f * Mathf.Sin(phase * 2f + 1.2f) + b * move * 6f * Mathf.Sin(phase + .4f) + idle * Noise(clock * .7f, 9f) * 4f + 18f * Rear;
        Pose(horse, "neck", new Vector3(Spring("neck_p", neck, 3f, .8f, dt), Spring("neck_y", look * .55f, 1.6f, .9f, dt), 0));
        Pose(horse, "head", new Vector3(Spring("head_p", head, 4f, .75f, dt), Spring("head_y", look * .45f, 2f, .85f, dt), 0));
        float tp = Spring("tail_p", Mathf.Lerp(0, 32f, move * (.4f + .6f * b)) + 8f * Mathf.Sin(phase * (2f - b)) * move + 20f * air, 1.4f, .55f, dt);
        float ty = Spring("tail_y", Mathf.Clamp(turn * 14f, -28f, 28f) + Noise(clock * 1.1f, 2f) * (5f + 6f * move), 1.2f, .35f, dt);
        Pose(horse, "tail", new Vector3(tp, ty, 0)); Pose(horse, "tail_tip", new Vector3(Spring("tail_tp", tp * .6f, 1.1f, .5f, dt), Spring("tail_ty", ty * .9f, 1f, .4f, dt), 0));
    }

    void DriveRider(float dt, float turn, float accel, float land)
    {
        float b = gallop, phase = cycle * Mathf.PI * 2f, walk = move * (1f - b), idle = 1f - move;
        float windup = meshyRig != null ? 0f : AttackWindup, strike = meshyRig != null ? 0f : AttackStrike;
        int beat = campaign != null ? campaign.ComboBeat : 1;
        bool backhand = beat == 2;
        float attackSide = backhand ? -1f : 1f;
        float evade = movement != null && movement.IsDodging ? Mathf.Sin(movement.DodgeProgress * Mathf.PI) : 0f;
        float lean = Spring("r_lean", 3f + 17f * b * move + 4f * walk - Mathf.Clamp(accel * 1.1f, -10f, 10f) - 8f * windup + 16f * strike + 10f * air + evade * 22f, 1.3f, .6f, dt);
        float roll = Spring("r_roll", Mathf.Clamp(-turn * 9f, -16f, 16f) * (.3f + .7f * move), 1.2f, .7f, dt);
        if (movement != null && movement.IsDodging) roll -= evade * movement.DodgeLateral * 18f;
        float twist = Spring("r_twist", attackSide * (-26f * windup + 30f * strike) + Mathf.Clamp(turn * 6f, -10f, 10f), attack < 0f ? 3.5f : 10f, .85f, dt);
        float rock = walk * 3f * Mathf.Sin(phase * 2f + .3f) + b * move * 5f * Mathf.Sin(phase + .2f);
        float absorb = Spring("r_absorb", rock, 3.5f, .55f, dt), rise = Spring("r_seat", b * move * (.025f + .018f * Mathf.Sin(phase - .3f)) + air * .04f + Mathf.Min(land, 0) * .025f, 4.5f, .85f, dt);
        Bone pelvis = rider["pelvis"]; pelvis.t.localPosition = pelvis.position + V(new Vector3(0, rise, -rise * .4f));
        float breathe = idle * Mathf.Sin(clock * 1.9f) * 1.6f;
        Pose(rider, "pelvis", new Vector3(absorb * .6f + lean * .15f, twist * .15f, roll * .3f));
        Pose(rider, "spine", new Vector3(lean * .40f - absorb * .5f + breathe * .4f, twist * .35f, roll * .35f));
        Pose(rider, "chest", new Vector3(lean * .45f - absorb * .3f - breathe, twist * .5f, roll * .35f));
        float look = Spring("r_look", Mathf.Clamp(turn * 14f, -26f, 26f) + idle * Noise(clock * .45f, 7f) * 22f - twist * .7f, 1.6f, .9f, dt);
        Pose(rider, "head", new Vector3(Spring("r_head_p", -lean - absorb * .1f + idle * Noise(clock * .6f, 3f) * 3f + 6f * strike, 3f, .8f, dt), look, -roll * .6f));
        float bounce = walk * 2.5f * Mathf.Sin(phase * 2f + 1f) + b * move * 5f * Mathf.Sin(phase + 1.2f);
        Vector3 preparation = backhand ? new Vector3(-100f, -55f, 18f) : new Vector3(-165f, 25f, 30f);
        Vector3 follow = backhand ? new Vector3(-35f, 42f, 35f) : beat == 3 ? new Vector3(-20f, -5f, 12f) : new Vector3(-40f, -20f, 18f);
        Vector3 up = Vector3.Lerp(Vector3.Lerp(new Vector3(-28f - 4f * b, 0, 14f), preparation, windup), follow, strike);
        Vector3 fore = Vector3.Lerp(Vector3.Lerp(new Vector3(-38f + bounce, 0, 6f), new Vector3(-45f, 0, 0), windup), new Vector3(-5f, 0, 0), strike);
        Vector3 hand = Vector3.Lerp(Vector3.Lerp(new Vector3(10f, 0, 0), new Vector3(-20f, 0, 0), windup), new Vector3(55f, 0, 0), strike);
        Pose(rider, "upperarm_r", VS("r_up", up, attack < 0 ? 4.5f : 18f, .85f, dt)); Pose(rider, "forearm_r", VS("r_fore", fore, attack < 0 ? 5f : 18f, .85f, dt)); Pose(rider, "hand_r", VS("r_hand", hand, attack < 0 ? 6f : 18f, .85f, dt));
        Pose(rider, "upperarm_l", VS("l_up", new Vector3(-30f - 6f * b - 12f * strike, 0, -38f + 10f * windup), 4f, .85f, dt));
        Pose(rider, "forearm_l", VS("l_fore", new Vector3(-42f + bounce, 0, -8f), 5f, .8f, dt)); Pose(rider, "hand_l", new Vector3(8f, 0, 0));
        float thigh = -12f - 10f * b * move - 8f * air, knee = 14f + 16f * b * move + 12f * air, swing = walk * 1.5f * Mathf.Sin(phase * 2f) + b * move * 2f * Mathf.Sin(phase + .7f), splay = 27f + 3f * b * move - 4f * air;
        Pose(rider, "thigh_r", VS("thigh_r", new Vector3(thigh + swing, 0, -splay), 4f, .8f, dt)); Pose(rider, "thigh_l", VS("thigh_l", new Vector3(thigh - swing, 0, splay), 4f, .8f, dt));
        Pose(rider, "shin_r", VS("shin_r", new Vector3(knee, 0, splay * .45f), 4f, .7f, dt)); Pose(rider, "shin_l", VS("shin_l", new Vector3(knee, 0, -splay * .45f), 4f, .7f, dt));
        DriveCape(dt, turn, accel, lean, windup, strike, land);
        float flutter = Mathf.Lerp(2f, 9f, move), rib = Mathf.Sin(clock * (flutter * 1.2f + 1f)) * Mathf.Lerp(6f, 16f, move), rib2 = Mathf.Sin(clock * (flutter * 1.1f + .7f) + 1.9f) * Mathf.Lerp(6f, 16f, move);
        Pose(rider, "ribbon", new Vector3(Spring("rib_p", 10f * move + rib * .5f, 2f, .4f, dt), Spring("rib_y", -48f + rib + turn * 12f, 1.8f, .35f, dt), 0));
        Pose(rider, "ribbon_l", new Vector3(Spring("rib2_p", 10f * move + rib2 * .5f, 2f, .4f, dt), Spring("rib2_y", 48f - rib2 + turn * 12f, 1.8f, .35f, dt), 0));
    }
    void DriveCape(float dt, float turn, float accel, float lean, float windup, float strike, float land)
    {
        float evade = movement != null && movement.IsDodging ? Mathf.Sin(movement.DodgeProgress * Mathf.PI) : 0f;
        float flow = Mathf.Clamp(move * (.55f + .45f * gallop) + air * .25f + evade * .5f, 0, 1.5f), frequency = Mathf.Lerp(2.2f, 9f, Mathf.Clamp01(flow)), flutter = 6f * (.18f + flow);
        float lift = Mathf.Clamp(18f + flow * 25f - lean * .35f - accel * .45f + Noise(clock * .8f, 5f) * flutter * .35f + evade * 30f, 8f, 75f);
        float yaw = Spring("cape_y", Mathf.Clamp(turn * 10f + (windup - strike) * 12f, -24f, 24f), 1.8f, .72f, dt);
        Pose(rider, "cape", new Vector3(Spring("cape_p", lift, 2.2f, .72f, dt), yaw, yaw * .12f));
        float lowerYaw = Spring("cape_ly", yaw * .45f, 1.5f, .72f, dt);
        Pose(rider, "cape_low", new Vector3(Spring("cape_lp", flow * 7f + Mathf.Sin(clock * frequency - .8f) * flutter + Mathf.Min(land, 0) * 8f, 2.6f, .72f, dt), lowerYaw, 0));
        Pose(rider, "cape_hem", new Vector3(Spring("cape_hp", Mathf.Sin(clock * frequency - 1.6f) * flutter * 1.25f - flow * 4f, 2.8f, .72f, dt), Spring("cape_hy", lowerYaw * .5f, 1.3f, .72f, dt), 0));
        for (int i = 0; i < 2; i++) { float wave = Mathf.Sin(clock * frequency - 2.1f + i * 1.5f) * flutter * .65f, side = i == 0 ? -1f : 1f; Pose(rider, i == 0 ? "cape_edge_r" : "cape_edge_l", VS("edge" + i, new Vector3(wave, side * flow * 3f, wave * side * .45f), 3f, .72f, dt)); }
    }

    public void SyncEquipment(int chapter, int step)
    {
        if (IsBusy) return;
        armorWorn = chapter == 2 || chapter == 1 && step >= 1 || chapter == 3 && step == 0;
        helmetWorn = chapter == 2 || chapter == 1 && step >= 2 || chapter == 3 && step < 2;
        PlaceWorn();
    }
    public void PlayEquipment(string nextAction)
    {
        if (nextAction != "equip_armor" && nextAction != "equip_helmet" && nextAction != "remove_armor" && nextAction != "remove_helmet") return;
        if (!configured) Configure(movement != null ? movement.visual : transform.Find("Visual"));
        if (!IsRigReady) return;
        if (!IsBusy)
        {
            foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
                if (t.name == "Weapon_IronSword" || t.name == "Weapon_GoldenBamboo") { weapons.Add(t.gameObject); weaponStates.Add(t.gameObject.activeSelf); t.gameObject.SetActive(false); }
        }
        pending.Enqueue(nextAction);
    }
    void Attach(Transform item, string boneName, Vector3 offset, bool visible)
    {
        Bone bone; if (item == null || !rider.TryGetValue(boneName, out bone)) return;
        item.SetPositionAndRotation(bone.t.TransformPoint(V(offset)), bone.t.rotation);
        item.gameObject.SetActive(visible);
    }
    void PlaceWorn() { Attach(helmet, "head", new Vector3(0, .21f, 0), helmetWorn); Attach(armor, "chest", new Vector3(0, -.08f, .025f), armorWorn); }
    void DriveEquipment(float dt)
    {
        PlaceWorn();
        if (action == null && pending.Count != 0) { action = pending.Dequeue(); elapsed = 0; }
        if (action == null) return;
        elapsed += dt; float t = Mathf.Clamp01(elapsed / Mathf.Max(equipmentDuration, .1f));
        bool removing = action.StartsWith("remove", StringComparison.Ordinal), isHelmet = action.EndsWith("helmet", StringComparison.Ordinal);
        Transform item = isHelmet ? helmet : armor;
        if (item != null)
        {
            // Work in skeleton/model space, matching the source's two-hand gesture.
            Transform space = rider["pelvis"].t.parent;
            Vector3 settled = space.InverseTransformPoint(item.position), position;
            float phase = removing ? 1f - t : t;
            if (isHelmet) { Vector3 held = settled + V(new Vector3(-.14f, -.38f, .42f)), raised = settled + V(new Vector3(0, .32f, .035f)); position = phase < .6f ? Vector3.Lerp(held, raised, Smooth(0, .6f, phase)) : Vector3.Lerp(raised, settled, Smooth(.6f, .88f, phase)); }
            else position = Vector3.Lerp(settled + V(new Vector3(0, -.05f, .4f)), settled, Smooth(.12f, .8f, phase));
            item.position = space.TransformPoint(position); item.gameObject.SetActive(true);
            float heldTilt = 1f - Smooth(.6f, .92f, phase);
            item.rotation *= Q(new Vector3(isHelmet ? -12f * heldTilt : 8f * heldTilt, 0, isHelmet ? 7f * heldTilt : 0));
            float reach = Smooth(0, .14f, t) * (1f - Smooth(.84f, 1f, t));
            HandTarget(true, space.TransformPoint(position + V(new Vector3(-.17f, .01f, .025f))), reach);
            HandTarget(false, space.TransformPoint(position + V(new Vector3(.17f, .01f, .025f))), reach);
        }
        if (t < 1f) return;
        if (isHelmet) helmetWorn = !removing; else armorWorn = !removing;
        action = null;
        if (pending.Count != 0) return;
        for (int i = 0; i < weapons.Count; i++) if (weapons[i] != null) weapons[i].SetActive(weaponStates[i]);
        weapons.Clear(); weaponStates.Clear(); PlaceWorn(); EquipmentFinished?.Invoke();
    }
    void HandTarget(bool right, Vector3 target, float blend)
    {
        Bone upper, lower, hand; string suffix = right ? "_r" : "_l";
        if (!rider.TryGetValue("upperarm" + suffix, out upper) || !rider.TryGetValue("forearm" + suffix, out lower) || !rider.TryGetValue("hand" + suffix, out hand)) return;
        Vector3 shoulder = upper.t.position, first = upper.t.TransformVector(lower.position), second = lower.t.TransformVector(hand.position), offset = target - shoulder;
        float a = first.magnitude, b = second.magnitude, distance = Mathf.Clamp(offset.magnitude, .001f, Mathf.Max(a + b - .001f, .002f));
        if (a < .001f || b < .001f) return;
        Vector3 direction = offset.normalized;
        Vector3 pole = rider["pelvis"].t.parent.TransformDirection(V(new Vector3(right ? -1f : 1f, 0, .3f)));
        Vector3 side = Vector3.ProjectOnPlane(pole, direction).normalized;
        if (side.sqrMagnitude < .001f) side = Vector3.Cross(direction, transform.up).normalized;
        float along = (a * a - b * b + distance * distance) / (2f * distance);
        Vector3 elbow = shoulder + direction * along + side * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        Quaternion desired = Quaternion.FromToRotation(lower.t.position - shoulder, elbow - shoulder) * upper.t.rotation;
        upper.t.rotation = Quaternion.Slerp(upper.t.rotation, desired, blend);
        desired = Quaternion.FromToRotation(hand.t.position - lower.t.position, target - lower.t.position) * lower.t.rotation;
        lower.t.rotation = Quaternion.Slerp(lower.t.rotation, desired, blend);
    }

    void ContactFeedback(float speed, bool grounded)
    {
        if (flying || !grounded || speed < .8f || IsBusy || Rear > .35f) return;
        bool contact = false;
        for (int i = 0; i < contactsPending.Length; i++) contact |= contactsPending[i];
        if (!contact) return;
        if (contacts == null)
        {
            GameObject go = new GameObject("Godot Hoof Contacts"); go.transform.SetParent(transform, false); contacts = go.AddComponent<ParticleSystem>(); contacts.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = contacts.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = .4f; main.startSpeed = .5f; main.startSize = .12f; main.gravityModifier = .3f; main.maxParticles = 80;
            var emission = contacts.emission; emission.enabled = false;
            var shape = contacts.shape; shape.enabled = false;
            var color = contacts.colorOverLifetime; color.enabled = true; Gradient gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(.65f, 0), new GradientAlphaKey(0, 1) }); color.color = gradient;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                contactMaterial = new Material(shader);
                contactTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                { float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude; pixels[y * 32 + x] = new Color(1, 1, 1, SS(1f - radius)); }
                contactTexture.SetPixels(pixels); contactTexture.Apply(); contactTexture.wrapMode = TextureWrapMode.Clamp;
                if (contactMaterial.HasProperty("_BaseMap")) contactMaterial.SetTexture("_BaseMap", contactTexture);
                if (contactMaterial.HasProperty("_MainTex")) contactMaterial.SetTexture("_MainTex", contactTexture);
                if (contactMaterial.HasProperty("_Surface")) contactMaterial.SetFloat("_Surface", 1);
                if (contactMaterial.HasProperty("_ZWrite")) contactMaterial.SetFloat("_ZWrite", 0);
                if (contactMaterial.HasProperty("_SrcBlend")) contactMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (contactMaterial.HasProperty("_DstBlend")) contactMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                contactMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); contactMaterial.renderQueue = 3000;
                contacts.GetComponent<ParticleSystemRenderer>().sharedMaterial = contactMaterial;
            }
            // Manual Emit adds particles, but the stopped system must run to age/move them.
            contacts.Play();
        }
        if (audioSource == null) audioSource = GetComponent<ThanhGiongCampaignAudio>();
        bool wet = audioSource != null && audioSource.DetectSurface() == "wet";
        for (int leg = 0; leg < 4; leg++)
        {
            if (!contactsPending[leg] || !horse.TryGetValue(LegPrefixes[leg] + "_hoof", out Bone hoof)) continue;
            Vector3 origin = hoof.t.position + Vector3.up * Mathf.Max(.6f, Growth * .7f);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, contactHits, Mathf.Max(2f, Growth * 1.6f), ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; Vector3 point = default;
            for (int hit = 0; hit < count; hit++)
            {
                Collider collider = contactHits[hit].collider;
                if (collider == null || collider.transform.IsChildOf(transform) || contactHits[hit].normal.y < .6f || collider.GetComponentInParent<ThanhGiongEnemy>() != null) continue;
                if (contactHits[hit].distance < nearest) { nearest = contactHits[hit].distance; point = contactHits[hit].point; }
            }
            if (float.IsPositiveInfinity(nearest)) continue;
            HoofContactCount++;
            LastHoofContact = point;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams { position = point + Vector3.up * .025f, startColor = wet ? new Color(.55f, .82f, .88f, .55f) : new Color(.58f, .44f, .24f, .5f), startSize = (wet ? .07f : .10f) * Growth, startLifetime = wet ? .25f : .45f };
            for (int i = 0; i < (wet ? 4 : 2); i++) { emit.velocity = -transform.forward * speed * .04f + new Vector3(UnityEngine.Random.Range(-.12f, .12f), .5f, UnityEngine.Random.Range(-.12f, .12f)); contacts.Emit(emit, 1); }
        }
    }
    void OnDodgeStarted(Vector3 dir)
    {
        if (audioSource == null) audioSource = GetComponent<ThanhGiongCampaignAudio>();
        audioSource?.PlayCue("dodge", 0.72f);
        ContactFeedback(14f, true);
        Kick("land", -2.4f);
        IsometricCameraFollow.Instance?.Shake(0.12f, 0.22f);
    }
    void OnDestroy()
    {
        if (movement != null) movement.DodgeStarted -= OnDodgeStarted;
        if (contactMaterial != null) Destroy(contactMaterial);
        if (contactTexture != null) Destroy(contactTexture);
    }
}
