using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-20)]
public class MountedHorseController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4.8f;
    public float runSpeed = 8.5f;
    public float turnSpeed = 10f;
    public float gravity = -24f;
    public float jumpHeight = 2.4f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float accelerationTime = 0.18f;
    public float decelerationTime = 0.24f;

    [Header("Mounted dodge (C)")]
    public float dodgeDuration = .38f;
    public float dodgeDistance = 4f;
    public float dodgeCooldown = 1f;
    public float dodgeInvulnerability = .24f;

    [Header("Procedural horse animation")]
    public Transform visual;
    public float gallopFrequency = 9f;
    public float gallopHeight = 0.09f;
    public float gallopPitch = 2.8f;
    public float gallopRoll = 1.8f;

    [Header("Four-leg procedural rig")]
    public int rigVersion = 3;
    public Transform frontLeftLeg;
    public Transform frontRightLeg;
    public Transform rearLeftLeg;
    public Transform rearRightLeg;
    public float walkStrideAngle = 24f;
    public float runStrideAngle = 38f;
    public float kneeBendAngle = 34f;

    [Header("Attack")]
    public float attackDuration = 0.58f;
    public float attackCooldown = 0.3f;
    public float attackRange = 2.4f;
    public float attackForce = 7f;

    private CharacterController controller;
    private Camera gameplayCamera;
    private Vector3 visualBasePosition;
    private Quaternion visualBaseRotation;
    private Vector3 visualBaseScale;
    private float verticalVelocity;
    private float lastGroundedTime = -10f;
    private float jumpPressedTime = -10f;
    private float gaitTime;
    private float attackTimer;
    private float nextAttackTime;
    private Transform[] legs;
    private Quaternion[] legBaseRotations;
    private Vector3 planarVelocity;
    private Vector3 planarVelocityDamp;
    private Animator mountedAnimator;
    private ThanhGiong.Mounts.HorseController modularHorse;
    private ThanhGiong.Characters.ThanhGiongCharacterController modularHero;
    private MeshyMountedRig meshyRig;
    private GeneratedCharacterMotion[] generatedMotions;
    private Transform bambooWeapon;
    private bool animatorHasSpeed;
    private bool animatorHasAttack;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    // Understory-style procedural dynamic motion
    private float previousYaw;
    private float currentBankAngle;
    private float prevForwardSpeed;
    private float currentPitchAccel;
    private GodotMountedMotion godotMotion;
    private MountedHorseMeshMotion originalHorseMotion;
    private ThanhGiongCampaignController campaignController;
    private Coroutine attackSlash;
    private GameObject activeSlash;
    private float dodgeTimer, nextDodgeTime, recentDirectionTime = -10f;
    private Vector3 dodgeDirection, recentDirection;
    private Vector3 frameVelocity;

    public bool IsDodging => dodgeTimer > 0f;
    public float DodgeRemaining => Mathf.Max(0, dodgeTimer);
    public float DodgeCooldownRemaining => Mathf.Max(0, nextDodgeTime - Time.time);
    public float DodgeProgress => IsDodging ? Mathf.Clamp01(1f - dodgeTimer / Mathf.Max(dodgeDuration, .01f)) : 1f;
    public bool IsDodgeInvulnerable => IsDodging && dodgeDuration - dodgeTimer < Mathf.Clamp(dodgeInvulnerability, 0, dodgeDuration);
    public float DodgeLateral => IsDodging ? Vector3.Dot(dodgeDirection, transform.right) : 0f;
    public event System.Action<Vector3> DodgeStarted;

    private bool IsAttacking => attackTimer > 0f;
    public float AttackProgress => IsAttacking ? Mathf.Clamp01(1f - attackTimer / Mathf.Max(attackDuration, .001f)) : -1f;
    public Vector3 WorldVelocity => frameVelocity;
    public float PlanarSpeed => new Vector3(frameVelocity.x, 0f, frameVelocity.z).magnitude;
    public float VerticalSpeed => verticalVelocity;
    public bool Grounded => controller != null && controller.isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        godotMotion = GetComponent<GodotMountedMotion>();
        originalHorseMotion = GetComponent<MountedHorseMeshMotion>();
        campaignController = GetComponent<ThanhGiongCampaignController>();
        gameplayCamera = Camera.main;
        if (visual == null)
        {
            Transform child = transform.Find("Visual");
            visual = child != null ? child : transform;
        }
        visualBasePosition = visual.localPosition;
        visualBaseRotation = visual.localRotation;
        visualBaseScale = visual.localScale;
        previousYaw = transform.eulerAngles.y;
        if (frontLeftLeg == null) frontLeftLeg = transform.Find("Visual/MountedGiong/Mount/IronHorse/model/body/fl_upper");
        if (frontRightLeg == null) frontRightLeg = transform.Find("Visual/MountedGiong/Mount/IronHorse/model/body/fr_upper");
        if (rearLeftLeg == null) rearLeftLeg = transform.Find("Visual/MountedGiong/Mount/IronHorse/model/body/rl_upper");
        if (rearRightLeg == null) rearRightLeg = transform.Find("Visual/MountedGiong/Mount/IronHorse/model/body/rr_upper");
        legs = new[] { frontLeftLeg, frontRightLeg, rearLeftLeg, rearRightLeg };
        legBaseRotations = new Quaternion[legs.Length];
        for (int i = 0; i < legs.Length; i++)
            legBaseRotations[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;

        modularHorse = GetComponentInChildren<ThanhGiong.Mounts.HorseController>(true);
        modularHero = GetComponentInChildren<ThanhGiong.Characters.ThanhGiongCharacterController>(true);
        meshyRig = GetComponentInChildren<MeshyMountedRig>(true);
        generatedMotions = GetComponentsInChildren<GeneratedCharacterMotion>(true);
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "Weapon_GoldenBamboo") { bambooWeapon = child; break; }
        mountedAnimator = modularHero != null
            ? modularHero.GetComponent<Animator>()
            : GetComponentInChildren<Animator>();
        if (mountedAnimator != null)
        {
            foreach (AnimatorControllerParameter parameter in mountedAnimator.parameters)
            {
                if (parameter.nameHash == SpeedHash) animatorHasSpeed = true;
                if (parameter.nameHash == AttackHash) animatorHasAttack = true;
            }
            mountedAnimator.applyRootMotion = false;
        }

        ThanhGiongSpeedRibbon ribbon = GetComponent<ThanhGiongSpeedRibbon>();
        if (ribbon == null) ribbon = gameObject.AddComponent<ThanhGiongSpeedRibbon>();
        ribbon.motionSource = transform;
        ribbon.ribbonAnchor = visual != null ? visual : transform;

        ThanhGiongRibbonCape cape = GetComponent<ThanhGiongRibbonCape>();
        if (cape == null) cape = gameObject.AddComponent<ThanhGiongRibbonCape>();
        cape.anchorTransform = visual != null ? visual : transform;
        foreach (GeneratedCharacterMotion motion in generatedMotions)
            if (motion != null && motion.kind == GeneratedCharacterMotion.CharacterKind.ThanhGiong)
            {
                // The generated hero already has a detailed cape. The legacy
                // wide line ribbons overlap the rider and saddle at idle.
                cape.enabled = false;
                break;
            }
    }

    private void Start()
    {
        if (bambooWeapon != null && generatedMotions != null)
        {
            foreach (GeneratedCharacterMotion motion in generatedMotions)
            {
                if (motion == null || motion.kind != GeneratedCharacterMotion.CharacterKind.ThanhGiong || motion.BambooGrip == null)
                    continue;
                // Preserve the enlarged bamboo's world scale while moving it from
                // the invisible KayKit socket onto the visible Hyper3D hand.
                if (bambooWeapon.parent != motion.BambooGrip)
                {
                    bambooWeapon.SetParent(motion.BambooGrip, false);
                    bambooWeapon.localPosition = Vector3.zero;
                    bambooWeapon.localRotation = Quaternion.identity;
                }
                break;
            }
        }
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        bool equipmentBusy = godotMotion != null && godotMotion.IsBusy;
        Vector2 input = equipmentBusy ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 move = CameraRelativeDirection(input);
        if (move.sqrMagnitude > .01f) { recentDirection = move; recentDirectionTime = Time.time; }
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.LeftControl)) TryDodge(move);
        if (!equipmentBusy && campaignController == null && Input.GetMouseButtonDown(0))
            TryAttack();

        if (!equipmentBusy && Input.GetKeyDown(KeyCode.Space))
            RequestJump();

        bool running = Input.GetKey(KeyCode.LeftShift);
        float speed = running ? runSpeed : walkSpeed;
        if (IsAttacking) speed *= 0.25f;
        Vector3 desiredPlanarVelocity = move * speed;
        float smoothing = desiredPlanarVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? accelerationTime : decelerationTime;
        bool dodgeFrame = IsDodging;
        if (dodgeFrame)
        {
            float step = Mathf.Min(Time.deltaTime, dodgeTimer);
            planarVelocity = dodgeDirection * (dodgeDistance / Mathf.Max(dodgeDuration, .01f)) * (step / Time.deltaTime);
            planarVelocityDamp = Vector3.zero;
        }
        else planarVelocity = Vector3.SmoothDamp(planarVelocity, desiredPlanarVelocity, ref planarVelocityDamp, smoothing, 35f, Time.deltaTime);

        if (!dodgeFrame && planarVelocity.sqrMagnitude > 0.025f)
        {
            Quaternion target = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }

        bool bufferedJump = Time.time - jumpPressedTime <= jumpBufferTime;
        bool canUseGroundJump = Time.time - lastGroundedTime <= coyoteTime;
        if (bufferedJump && canUseGroundJump && !IsAttacking && !equipmentBusy && !dodgeFrame)
        {
            ExecuteJump();
        }
        verticalVelocity += gravity * Time.deltaTime;
        Vector3 displacement = (planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
        Vector3 frameStart = transform.position;
        // CharacterController sweeps each segment; a low frame rate cannot jump across a thin wall.
        int segments = dodgeFrame ? Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / .35f)) : 1;
        for (int i = 0; i < segments; i++) controller.Move(displacement / segments);
        frameVelocity = (transform.position - frameStart) / Time.deltaTime;
        if (dodgeFrame)
        {
            dodgeTimer = Mathf.Max(0, dodgeTimer - Time.deltaTime);
            if (!IsDodging) { planarVelocity = Vector3.zero; planarVelocityDamp = Vector3.zero; }
        }
        float travelSpeed = PlanarSpeed;
        float speed01 = Mathf.Clamp01(travelSpeed / Mathf.Max(runSpeed, .01f));
        // Advance the hoof cycle by ground covered, so acceleration and
        // release of WASD cannot leave the legs cycling at a fixed rate.
        if (travelSpeed > .08f)
            gaitTime += travelSpeed * (Mathf.PI * 2f) /
                (originalHorseMotion != null && originalHorseMotion.IsReady
                    ? originalHorseMotion.CycleDistance(speed01 > .72f) : Mathf.Lerp(3.1f, 4f, speed01)) *
                Mathf.Clamp(gallopFrequency / 9f, .5f, 1.8f) * Time.deltaTime;
        if (generatedMotions != null)
            foreach (GeneratedCharacterMotion motion in generatedMotions)
                if (motion != null) motion.SetMotionState(travelSpeed, controller.isGrounded, verticalVelocity, gaitTime);

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;
        float actualSpeed01 = speed01;
        if (mountedAnimator != null && animatorHasSpeed) mountedAnimator.SetFloat(SpeedHash, actualSpeed01, .18f, Time.deltaTime);
        // The rider is seated on the animated saddle. Playing the standing Run
        // clip here fights the mounted leg pose and makes the two rigs jitter.
        // Keep the rider in mounted idle; attacks still use the dedicated clip.
        if (modularHero != null) modularHero.SetMoveSpeed(0f);
        if (modularHorse != null) modularHorse.SetMoveSpeed(planarVelocity.magnitude);
        AnimateVisual(actualSpeed01, actualSpeed01 > .72f);
    }

    private Vector3 CameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return Vector3.zero;
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (gameplayCamera == null) return new Vector3(input.x, 0f, input.y).normalized;

        Vector3 forward = gameplayCamera.transform.forward;
        Vector3 right = gameplayCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        return (forward.normalized * input.y + right.normalized * input.x).normalized;
    }

    private void AnimateVisual(float movementAmount, bool running)
    {
        if (visual == null) return;
        if (godotMotion != null && godotMotion.IsRigReady) return;

        // Modular Horse_Controller already supplies body bounce, four-leg gait
        // and rearing. A second procedural transform on the shared Visual root
        // doubles that motion and throws the rider off balance with the map.
        if (modularHorse != null)
        {
            float settle = 1f - Mathf.Exp(-14f * Time.deltaTime);
            float gaitBlend = Mathf.SmoothStep(0f, 1f, movementAmount);
            Vector3 targetPosition = visualBasePosition + Vector3.up *
                (Mathf.Sin(gaitTime * 2f) * .014f * gaitBlend + Mathf.Sin(Time.time * 1.7f) * .004f * (1f - gaitBlend));
            Quaternion targetRotation = visualBaseRotation * Quaternion.Euler(
                Mathf.Sin(gaitTime * 2f) * .8f * gaitBlend, 0f,
                Mathf.Sin(gaitTime) * .7f * gaitBlend);
            visual.localPosition = Vector3.Lerp(visual.localPosition, targetPosition, settle);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, settle);
            visual.localScale = Vector3.Lerp(visual.localScale, visualBaseScale, settle);
            return;
        }

        Vector3 position = visualBasePosition;
        Quaternion rotation = visualBaseRotation;
        Vector3 targetScale = visualBaseScale;

        // Dynamic turn banking calculation (Understory mount steering feel)
        float yawDelta = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / Mathf.Max(Time.deltaTime, 0.001f);
        previousYaw = transform.eulerAngles.y;
        float targetBank = -Mathf.Clamp(yawDelta * 0.38f, -14f, 14f);
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, 1f - Mathf.Exp(-9f * Time.deltaTime));

        // Dynamic forward acceleration/deceleration pitch
        float currentForwardSpeed = Vector3.Dot(controller.velocity, transform.forward);
        float accel = (currentForwardSpeed - prevForwardSpeed) / Mathf.Max(Time.deltaTime, 0.001f);
        prevForwardSpeed = currentForwardSpeed;
        currentPitchAccel = Mathf.Lerp(currentPitchAccel, Mathf.Clamp(accel * 0.65f, -8f, 11f), 1f - Mathf.Exp(-10f * Time.deltaTime));

        if (IsAttacking)
        {
            float progress = 1f - attackTimer / attackDuration;
            // Phase 1: Wind-up & Rear (0.0 to 0.35)
            float rearPhase = Mathf.Clamp01(progress / 0.35f);
            float rearSin = Mathf.Sin(rearPhase * Mathf.PI * 0.5f);

            // Phase 2: Explosive Forward Slash with Martial Body Twist (0.35 to 0.75)
            float strikePhase = Mathf.Clamp01((progress - 0.35f) / 0.4f);
            float strikeSin = Mathf.Sin(strikePhase * Mathf.PI);

            // Phase 3: Recoil damp (0.75 to 1.0)
            float recoilPhase = Mathf.Clamp01((progress - 0.75f) / 0.25f);
            float recoilSin = Mathf.Sin(recoilPhase * Mathf.PI);

            position += new Vector3(-0.12f * strikeSin, rearSin * 0.38f - strikeSin * 0.12f, strikeSin * 0.28f);
            rotation *= Quaternion.Euler(
                -rearSin * 18f + strikeSin * 24f - recoilSin * 4f,
                progress < 0.35f ? rearPhase * 16f : -strikeSin * 28f,
                strikeSin * -14f
            );

            // Dynamic attack squash & stretch
            targetScale = new Vector3(
                visualBaseScale.x * (1f + strikeSin * 0.04f),
                visualBaseScale.y * (1f + rearSin * 0.05f - strikeSin * 0.04f),
                visualBaseScale.z * (1f + strikeSin * 0.06f)
            );
        }
        else if (movementAmount > 0.05f)
        {
            float wave = Mathf.Sin(gaitTime);
            float doubleWave = Mathf.Sin(gaitTime * 2f);

            // True physical vertical trot & gallop bobbing
            bool plantedMesh = originalHorseMotion != null && originalHorseMotion.IsReady;
            float verticalBob = Mathf.Abs(wave) * (plantedMesh ? (running ? .025f : .012f) :
                (running ? gallopHeight * 1.55f : gallopHeight * 0.9f));
            position.y += verticalBob;
            position.z += doubleWave * 0.032f;

            // Pitch & roll + lean into turns + acceleration pitch
            rotation *= Quaternion.Euler(
                doubleWave * (plantedMesh ? .65f : gallopPitch) + currentPitchAccel * (plantedMesh ? .12f : 1f),
                0f,
                wave * (plantedMesh ? .4f : gallopRoll) + currentBankAngle * (plantedMesh ? .25f : 1f)
            );

            // Stride squash & stretch
            targetScale = plantedMesh ? visualBaseScale : new Vector3(
                visualBaseScale.x * (1f - wave * 0.018f),
                visualBaseScale.y * (1f + wave * 0.024f),
                visualBaseScale.z * (1f + (running ? 0.035f : 0f))
            );
        }
        else
        {
            // Organic idle breathing & alert sway
            float breathe = Mathf.Sin(Time.time * 2.2f);

            bool plantedMesh = originalHorseMotion != null && originalHorseMotion.IsReady;
            position += Vector3.up * (breathe * (plantedMesh ? .003f : .022f));
            rotation *= Quaternion.Euler(breathe * (plantedMesh ? .15f : 1.2f), Mathf.Cos(Time.time * 1.3f) * 1.6f, 0f);

            // Relax banking & pitch
            currentBankAngle = Mathf.Lerp(currentBankAngle, 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));
            currentPitchAccel = Mathf.Lerp(currentPitchAccel, 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));

            targetScale = plantedMesh ? visualBaseScale : new Vector3(
                visualBaseScale.x * (1f - breathe * 0.012f),
                visualBaseScale.y * (1f + breathe * 0.018f),
                visualBaseScale.z * (1f - breathe * 0.012f)
            );
        }

        float damping = 1f - Mathf.Exp(-16f * Time.deltaTime);
        visual.localPosition = Vector3.Lerp(visual.localPosition, position, damping);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, rotation, damping);
        visual.localScale = Vector3.Lerp(visual.localScale, targetScale, damping);

        // The modular horse owns its skeletal leg animation through Horse_Controller.
        // Legacy/procedural horses still use the fallback four-leg solver.
        if (modularHorse == null) AnimateLegs(movementAmount, running);
    }

    private void AnimateLegs(float movementAmount, bool running)
    {
        if (originalHorseMotion != null && originalHorseMotion.IsReady)
        {
            originalHorseMotion.Pose(gaitTime, movementAmount, running, AttackProgress, Grounded);
            return;
        }
        if (legs == null || legBaseRotations == null) return;

        float attackProgress = attackDuration > 0.001f ? 1f - attackTimer / attackDuration : 1f;
        // Walk uses four separate beats. Running uses readable diagonal pairs.
        float[] phases = running
            ? new[] { 0f, Mathf.PI, Mathf.PI, 0f }
            : new[] { 0f, Mathf.PI, Mathf.PI * .5f, Mathf.PI * 1.5f };

        for (int i = 0; i < legs.Length; i++)
        {
            Transform leg = legs[i];
            if (leg == null) continue;

            float upperAngle = 0f;
            float kneeAngle = 4f;
            if (IsAttacking)
            {
                float rear = Mathf.Sin(Mathf.Clamp01(attackProgress) * Mathf.PI);
                bool front = i < 2;
                upperAngle = front ? -62f * rear : 15f * rear;
                kneeAngle = front ? 68f * rear : 8f * rear;
            }
            else if (movementAmount > .05f)
            {
                float wave = Mathf.Sin(gaitTime + phases[i]);
                upperAngle = wave * (running ? runStrideAngle : walkStrideAngle);
                kneeAngle = Mathf.Max(0f, -wave) * kneeBendAngle;
            }

            Quaternion target = legBaseRotations[i] * Quaternion.Euler(upperAngle, 0f, 0f);
            leg.localRotation = Quaternion.Slerp(leg.localRotation, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
            Transform lower = leg.Find("Lower");
            if (lower != null)
            {
                Quaternion lowerTarget = Quaternion.Euler(kneeAngle, 0f, 0f);
                lower.localRotation = Quaternion.Slerp(lower.localRotation, lowerTarget, 1f - Mathf.Exp(-20f * Time.deltaTime));
            }
        }
    }

    private void BeginAttack()
    {
        attackTimer = attackDuration;
        nextAttackTime = Time.time + attackDuration + attackCooldown;
        if (generatedMotions != null)
            foreach (GeneratedCharacterMotion motion in generatedMotions)
                if (motion != null && motion.kind == GeneratedCharacterMotion.CharacterKind.ThanhGiong)
                    motion.TriggerAttack(attackDuration);
        if (campaignController == null) attackSlash = StartCoroutine(ShowAttackSlash());
        if (meshyRig != null) meshyRig.PlayAttack(attackDuration, campaignController != null && campaignController.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo, campaignController != null ? campaignController.ComboBeat : 1);
        else if (modularHero != null) modularHero.TriggerBambooAttack();
        else if (mountedAnimator != null && animatorHasAttack) mountedAnimator.SetTrigger(AttackHash);
        modularHorse?.TriggerRearing();
    }

    private void ApplyAttackHit()
    {
        Vector3 center = transform.position + Vector3.up * 1.35f + transform.forward * 1.15f;
        HashSet<ThanhGiongEnemy> damagedEnemies = new HashSet<ThanhGiongEnemy>();
        foreach (Collider hit in Physics.OverlapSphere(center, attackRange, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) continue;
            Rigidbody body = hit.attachedRigidbody;
            if (body != null && !body.isKinematic)
                body.AddForce((transform.forward + Vector3.up * 0.25f) * attackForce, ForceMode.Impulse);
            ThanhGiongEnemy enemy = hit.GetComponentInParent<ThanhGiongEnemy>();
            if (enemy != null && damagedEnemies.Add(enemy)) enemy.TakeDamage(25f, .18f);
        }
    }

    public bool TryAttack()
    {
        if (Time.timeScale <= 0f) return false;
        if (IsDodging) return false;
        if (campaignController != null && (!campaignController.IsBattleActive || campaignController.CurrentWeapon == ThanhGiongCampaignController.Weapon.None)) return false;
        if (godotMotion != null && godotMotion.IsBusy) return false;
        if (Time.time < nextAttackTime) return false;
        BeginAttack();
        return true;
    }

    // Campaign owns its weapon cooldown and damage; this starts the matching pose once accepted.
    public void TriggerCampaignAttack(float duration)
    {
        if (Time.timeScale <= 0f || IsDodging || godotMotion != null && godotMotion.IsBusy) return;
        attackDuration = Mathf.Max(duration, .1f);
        BeginAttack();
    }

    public void ResetMovementState()
    {
        planarVelocity = Vector3.zero;
        frameVelocity = Vector3.zero;
        planarVelocityDamp = Vector3.zero;
        verticalVelocity = 0f;
        attackTimer = nextAttackTime = 0f;
        dodgeTimer = nextDodgeTime = 0f;
        recentDirectionTime = -10f;
        jumpPressedTime = lastGroundedTime = -10f;
        if (attackSlash != null) { StopCoroutine(attackSlash); attackSlash = null; }
        ClearActiveSlash();
    }

    private void ClearActiveSlash()
    {
        if (activeSlash == null) return;
        foreach (LineRenderer line in activeSlash.GetComponentsInChildren<LineRenderer>())
            if (line.sharedMaterial != null) Destroy(line.sharedMaterial);
        Destroy(activeSlash);
        activeSlash = null;
    }

    public void RequestJump()
    {
        if (Time.timeScale <= 0f) return;
        if (IsDodging) return;
        if (godotMotion != null && godotMotion.IsBusy) return;
        jumpPressedTime = Time.time;
        if (controller != null && controller.isGrounded && !IsAttacking)
            ExecuteJump();
    }

    public bool TryDodge(Vector3 worldDirection)
    {
        if (!enabled || Time.timeScale <= 0f || controller == null || !controller.enabled || !controller.isGrounded ||
            IsDodging || IsAttacking || Time.time < nextDodgeTime || godotMotion != null && (godotMotion.IsBusy || godotMotion.flying) ||
            campaignController != null && (campaignController.IsDead || campaignController.CurrentChapter == ThanhGiongCampaignController.Chapter.Complete)) return false;
        worldDirection.y = 0;
        if (worldDirection.sqrMagnitude < .01f) worldDirection = Time.time - recentDirectionTime < .25f ? recentDirection : transform.forward;
        dodgeDirection = worldDirection.normalized;
        dodgeTimer = Mathf.Max(.05f, dodgeDuration);
        nextDodgeTime = Time.time + Mathf.Max(dodgeCooldown, dodgeDuration);
        jumpPressedTime = -10f;
        planarVelocityDamp = Vector3.zero;
        DodgeStarted?.Invoke(dodgeDirection);
        return true;
    }

    private void ExecuteJump()
    {
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        jumpPressedTime = -10f;
        lastGroundedTime = -10f;
        if (generatedMotions != null)
            foreach (GeneratedCharacterMotion motion in generatedMotions)
                if (motion != null) motion.TriggerJump();
    }

    private IEnumerator ShowAttackSlash()
    {
        // Reveal the sweep during the strike, after the bamboo wind-up.
        yield return new WaitForSeconds(attackDuration * .32f);
        GameObject slash = new GameObject("Golden Bamboo Sweep VFX");
        activeSlash = slash;
        slash.transform.SetParent(transform, false);
        Vector3 bambooTip = bambooWeapon != null && bambooWeapon.gameObject.activeInHierarchy
            ? transform.InverseTransformPoint(bambooWeapon.TransformPoint(new Vector3(0f, 1.02f, 0f)))
            : new Vector3(0f, 1.45f, 1.1f);
        Vector3 center = Vector3.Lerp(bambooTip, new Vector3(0f, 1.45f, 1.25f), .55f);

        // Core Golden Ribbon
        LineRenderer coreLine = slash.AddComponent<LineRenderer>();
        coreLine.useWorldSpace = false;
        coreLine.loop = false;
        coreLine.positionCount = 24;
        coreLine.widthMultiplier = 0.20f;
        coreLine.numCapVertices = 4;
        coreLine.material = new Material(Shader.Find("Sprites/Default"));
        coreLine.startColor = new Color(1f, 0.92f, 0.25f, 1f);
        coreLine.endColor = new Color(1f, 0.35f, 0.05f, 0.1f);

        // Outer emerald brush stroke makes the hit read as a bamboo sweep,
        // while the gold core preserves the Dong Son/Thanh Giong visual language.
        GameObject edgeGo = new GameObject("BambooLeafEdge");
        edgeGo.transform.SetParent(slash.transform, false);
        LineRenderer edgeLine = edgeGo.AddComponent<LineRenderer>();
        edgeLine.useWorldSpace = false;
        edgeLine.loop = false;
        edgeLine.positionCount = 24;
        edgeLine.widthMultiplier = 0.31f;
        edgeLine.numCapVertices = 4;
        edgeLine.material = new Material(Shader.Find("Sprites/Default"));
        edgeLine.startColor = new Color(0.20f, 0.95f, 0.28f, 0.9f);
        edgeLine.endColor = new Color(0.95f, 0.78f, 0.08f, 0.0f);

        float elapsed = 0f;
        float lifetime = attackDuration * .48f;
        bool hitApplied = false;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifetime);
            if (!hitApplied && progress >= .55f)
            {
                ApplyAttackHit();
                hitApplied = true;
            }
            float fade = 1f - Mathf.Clamp01((progress - .55f) / .45f);
            for (int i = 0; i < 24; i++)
            {
                float u = (i / 23f) * progress;
                float angle = Mathf.Lerp(-75f, 70f, u) * Mathf.Deg2Rad;
                Vector3 pt = center + new Vector3(Mathf.Sin(angle) * 1.55f,
                    Mathf.Cos(angle) * .70f, .15f + u * .55f);
                coreLine.SetPosition(i, pt);
                edgeLine.SetPosition(i, center + (pt - center) * 1.08f);
            }
            coreLine.widthMultiplier = .20f * fade;
            edgeLine.widthMultiplier = .31f * fade;
            yield return null;
        }
        if (!hitApplied) ApplyAttackHit();

        ClearActiveSlash();
        attackSlash = null;
    }

    private void OnDestroy() { ClearActiveSlash(); }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;
        if (pushDir.sqrMagnitude < 0.01f) pushDir = transform.forward;
        float pushPower = Input.GetKey(KeyCode.LeftShift) ? 14f : 5.5f;
        body.linearVelocity = pushDir * pushPower + Vector3.up * 1.2f;
    }
}
