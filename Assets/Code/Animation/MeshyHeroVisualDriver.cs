using UnityEngine;

/// <summary>
/// Drives the Meshy 3D models (ThanhGiong_Hero_Rigged, skinned 17-bone IronHorse_Meshy, BambooSpear_Meshy),
/// their 7 Generic animations, and horse/hero movement & combat VFX in sync with MountedHorseController,
/// ThanhGiongCampaignController, and GodotMountedMotion.
/// </summary>
[DefaultExecutionOrder(95)]
public sealed class MeshyHeroVisualDriver : MonoBehaviour
{
    public Transform meshyRoot;
    public Transform meshyMount;
    public Transform meshyHero;
    public Transform meshyHorse;
    public Transform meshySpear;
    public Animator heroAnimator;

    private Transform hipsBone;
    private Transform leftUpLeg;
    private Transform rightUpLeg;
    private Transform leftLeg;
    private Transform rightLeg;
    private Transform spineBone;
    private Transform rightArmBone;

    private ParticleSystem[] hoofVfx;
    private TrailRenderer spearTrail;

    private MountedHorseController movement;
    private ThanhGiongCampaignController campaign;
    private GodotMountedMotion godotMotion;
    private bool wasAttacking;
    private int attackComboIndex;
    private int attackStateHash;
    private float fallbackPhase;
    private float riderLean, riderBank, previousYaw, previousSpeed;
    private Vector3 seatOffset;

    [Header("Rider fit on the Iron Horse")]
    [Tooltip("Rider scale inside the mount. The prefab's 1.45 made Thanh Giong almost twice the horse's size.")]
    public float riderScale = 1.0f;
    [Tooltip("Rider root height above the mount so the pelvis rests in the saddle.")]
    public float seatHeight = 0.75f;
    public float seatForward = -0.08f;
    [Tooltip("Outward spread of the thighs so the legs wrap the horse's barrel.")]
    public float thighSpread = 22f;
    public float thighForward = 50f;
    public float kneeBend = 55f;
    private ThanhGiongCampaignController.Chapter lastChapter = (ThanhGiongCampaignController.Chapter)(-1);

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackStateHash = Animator.StringToHash("Attack");
    private static readonly int BambooSweepStateHash = Animator.StringToHash("BambooSweep360");
    private static readonly int BambooHeavyStateHash = Animator.StringToHash("BambooHeavySlam");
    private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");
    private static readonly int AscensionHash = Animator.StringToHash("Ascension");

    private void Awake()
    {
        previousYaw = transform.eulerAngles.y;
        BindReferences();
        EnsureMountedCombatPose();
        HideLegacyMeshRenderers();
    }

    private void Start()
    {
        BindReferences();
        EnsureMountedCombatPose();
        HideLegacyMeshRenderers();
        // Wait until every Awake has initialized the legacy ribbon before OnDisable hides it.
        foreach (ThanhGiongRibbonCape cape in GetComponentsInChildren<ThanhGiongRibbonCape>(true))
            cape.enabled = false;
    }

    private void EnsureMountedCombatPose()
    {
        if (meshyRoot != null && meshyRoot.GetComponent<MeshyMountedCombatPose>() == null)
            meshyRoot.gameObject.AddComponent<MeshyMountedCombatPose>();
    }

    public void BindReferences()
    {
        movement = GetComponent<MountedHorseController>();
        campaign = GetComponent<ThanhGiongCampaignController>();
        godotMotion = GetComponent<GodotMountedMotion>();

        Transform visual = movement != null && movement.visual != null
            ? movement.visual
            : transform.Find("Visual");

        if (visual == null) return;

        if (meshyRoot == null)
            meshyRoot = visual.Find("Meshy_Mounted_ThanhGiong");

        if (meshyRoot != null)
        {
            meshyMount = meshyRoot.Find("Mount");
            if (meshyHorse == null)
            {
                meshyHorse = meshyMount != null
                    ? meshyMount.Find("IronHorse")
                    : meshyRoot.Find("IronHorse_Meshy");
            }

            if (meshyHero == null)
            {
                meshyHero = meshyMount != null
                    ? meshyMount.Find("ThanhGiong_Hero_Meshy")
                    : meshyRoot.Find("ThanhGiong_Hero_Meshy");
            }

            if (meshyHorse != null)
            {
                hoofVfx = meshyHorse.GetComponentsInChildren<ParticleSystem>(true);
            }

            if (meshyHero != null)
            {
                if (heroAnimator == null)
                    heroAnimator = meshyHero.GetComponent<Animator>();

                if (heroAnimator != null)
                {
                    heroAnimator.enabled = true;
                    heroAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                hipsBone = meshyHero.Find("Armature/Hips");
                leftUpLeg = meshyHero.Find("Armature/Hips/LeftUpLeg");
                rightUpLeg = meshyHero.Find("Armature/Hips/RightUpLeg");
                leftLeg = meshyHero.Find("Armature/Hips/LeftUpLeg/LeftLeg");
                rightLeg = meshyHero.Find("Armature/Hips/RightUpLeg/RightLeg");
                spineBone = meshyHero.Find("Armature/Hips/Spine02/Spine01");
                rightArmBone = meshyHero.Find("Armature/Hips/Spine02/Spine01/Spine/RightShoulder/RightArm");

                ApplySeatedLegPose();

                if (meshySpear == null)
                {
                    foreach (Transform t in meshyHero.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Weapon_GoldenBamboo")
                        {
                            meshySpear = t;
                            spearTrail = meshySpear.GetComponentInChildren<TrailRenderer>(true);
                            break;
                        }
                    }
                }
            }
        }
    }

    private void ApplySeatedLegPose()
    {
        if (hipsBone != null)
        {
            hipsBone.localPosition = new Vector3(0.03f, -0.06f, 1.06f);
            hipsBone.localRotation = Quaternion.Euler(86.24f, 0f, 0f);
        }
        if (leftUpLeg != null) leftUpLeg.localRotation = Quaternion.Euler(thighForward, 198f, 180f + thighSpread);
        if (rightUpLeg != null) rightUpLeg.localRotation = Quaternion.Euler(thighForward, 162f, 180f - thighSpread);
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(kneeBend, 0f, 0f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(kneeBend, 0f, 0f);
    }

    private void HideLegacyMeshRenderers()
    {
        Transform visual = movement != null && movement.visual != null
            ? movement.visual
            : transform.Find("Visual");
        if (visual == null) return;

        Transform legacyMounted = visual.Find("MountedGiong");
        if (legacyMounted == null || meshyRoot == null) return;

        foreach (Renderer r in legacyMounted.GetComponentsInChildren<Renderer>(true))
        {
            r.forceRenderingOff = true;
        }
    }

    private void LateUpdate()
    {
        if (meshyRoot == null) BindReferences();
        if (meshyRoot == null) return;

        HideLegacyMeshRenderers();

        if (heroAnimator != null && !heroAnimator.enabled)
        {
            heroAnimator.enabled = true;
        }

        if (Time.timeScale <= 0f)
        {
            if (heroAnimator != null) heroAnimator.speed = 0f;
            return;
        }

        if (heroAnimator != null) heroAnimator.speed = 1f;

        float planarSpeed = movement != null ? movement.PlanarSpeed : 0f;
        float normalizedSpeed = Mathf.Clamp01(planarSpeed / Mathf.Max(movement != null ? movement.runSpeed : 8.5f, .1f));
        float dt = Time.deltaTime;
        float yawRate = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / Mathf.Max(dt, .001f);
        float acceleration = (planarSpeed - previousSpeed) / Mathf.Max(dt, .001f);
        previousYaw = transform.eulerAngles.y; previousSpeed = planarSpeed;
        float settle = 1f - Mathf.Exp(-9f * dt);
        riderLean = Mathf.Lerp(riderLean, normalizedSpeed * 9f + Mathf.Clamp(acceleration * .22f, -4f, 4f), settle);
        riderBank = Mathf.Lerp(riderBank, Mathf.Clamp(-yawRate * .035f, -7f, 7f) * normalizedSpeed, settle);

        if (heroAnimator != null && heroAnimator.runtimeAnimatorController != null)
        {
            // The rider stays seated; horse speed must not blend standing walk/run clips.
            heroAnimator.SetFloat(SpeedHash, 0f);
        }

        // Weapon visibility and replacement meshes belong to the campaign and mounted pose.

        fallbackPhase += Time.deltaTime * Mathf.Lerp(1.2f, 4.2f, normalizedSpeed);
        float cycleRadians = ((godotMotion != null && godotMotion.IsRigReady) ? godotMotion.GaitPhase : fallbackPhase) * Mathf.PI * 2f;

        if (meshyHorse != null && !meshyHorse.gameObject.activeSelf)
        {
            meshyHorse.gameObject.SetActive(true);
        }

        // Modulate hoof fire & dust VFX emission based on speed
        if (hoofVfx != null)
        {
            float rate = Mathf.Lerp(6f, 42f, normalizedSpeed);
            foreach (ParticleSystem ps in hoofVfx)
            {
                if (ps == null) continue;
                var em = ps.emission;
                em.rateOverTime = ps.name.Contains("Breath") ? Mathf.Lerp(10f, 28f, normalizedSpeed) : rate;
            }
        }

        float attackProgress = movement != null ? movement.AttackProgress : -1f;
        bool isAttacking = attackProgress >= 0f;

        if (heroAnimator != null && heroAnimator.runtimeAnimatorController != null)
        {
            if (isAttacking)
            {
                if (!wasAttacking)
                {
                    bool useBamboo = campaign != null && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo;
                    attackStateHash = useBamboo
                        ? (attackComboIndex % 2 == 0 ? BambooSweepStateHash : BambooHeavyStateHash)
                        : AttackStateHash;
                    attackComboIndex++;
                }
                // Sample the complete clip in gameplay attack time, before the saddle and hand constraints.
                heroAnimator.speed = 0f;
                heroAnimator.Play(attackStateHash, 0, Mathf.Clamp01(attackProgress));
                heroAnimator.Update(0f);
            }
            else
            {
                heroAnimator.speed = 1f;
                if (wasAttacking) heroAnimator.CrossFade(LocomotionStateHash, .08f, 0);
            }
        }
        wasAttacking = isAttacking;

        if (spearTrail != null)
        {
            spearTrail.emitting = isAttacking && campaign != null
                && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo
                && meshySpear != null && meshySpear.gameObject.activeInHierarchy;
        }

        if (meshyHero != null)
        {
            if (!meshyHero.gameObject.activeSelf)
                meshyHero.gameObject.SetActive(true);

            // The mount already bobs. Absorb a small part of it instead of bouncing twice.
            float bob = -Mathf.Sin(cycleRadians - .35f) * .018f * normalizedSpeed;
            seatOffset = Vector3.Lerp(seatOffset, new Vector3(0f, bob, 0f), settle);
            // Seat hero cleanly on the skinned horse saddle
            meshyHero.localScale = Vector3.one * riderScale;
            meshyHero.localPosition = new Vector3(0f, seatHeight, seatForward) + seatOffset;

            ApplySeatedLegPose();

            // Add dynamic upper-body gallop lean and combat swing reinforcement
            if (spineBone != null && heroAnimator != null && heroAnimator.runtimeAnimatorController != null
                && !isAttacking)
            {
                float leanForward = riderLean + Mathf.Cos(cycleRadians) * 1.5f * normalizedSpeed;
                spineBone.localRotation *= Quaternion.Euler(leanForward, 0f, riderBank);
            }
        }

        // Trigger Ascension animation in Chapter 4
        if (campaign != null && campaign.CurrentChapter != lastChapter)
        {
            lastChapter = campaign.CurrentChapter;
            if (lastChapter == ThanhGiongCampaignController.Chapter.Ascension && heroAnimator != null)
            {
                heroAnimator.SetTrigger(AscensionHash);
            }
        }
    }
}
