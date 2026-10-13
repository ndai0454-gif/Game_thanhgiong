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
    private float fallbackPhase;
    private ThanhGiongCampaignController.Chapter lastChapter = (ThanhGiongCampaignController.Chapter)(-1);

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int BambooAttackHash = Animator.StringToHash("BambooAttack");
    private static readonly int HeavySlamHash = Animator.StringToHash("HeavySlam");
    private static readonly int AscensionHash = Animator.StringToHash("Ascension");

    private void Awake()
    {
        BindReferences();
        HideLegacyMeshRenderers();
    }

    private void Start()
    {
        BindReferences();
        HideLegacyMeshRenderers();
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
        if (leftUpLeg != null) leftUpLeg.localRotation = Quaternion.Euler(26f, 198f, 188f);
        if (rightUpLeg != null) rightUpLeg.localRotation = Quaternion.Euler(26f, 162f, 172f);
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(35f, 0f, 0f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(35f, 0f, 0f);
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
        float normalizedSpeed = Mathf.Clamp01(planarSpeed / 8.5f);

        if (heroAnimator != null && heroAnimator.runtimeAnimatorController != null)
        {
            heroAnimator.SetFloat(SpeedHash, normalizedSpeed);
        }

        // Ensure all Meshy renderers and weapon stay visible
        foreach (Renderer r in meshyRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) r.enabled = true;
            if (r.forceRenderingOff) r.forceRenderingOff = false;
        }

        if (meshySpear != null && !meshySpear.gameObject.activeSelf)
        {
            meshySpear.gameObject.SetActive(true);
        }

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

        bool isAttacking = movement != null && movement.AttackProgress >= 0f && movement.AttackProgress < 0.85f;

        if (spearTrail != null)
        {
            spearTrail.emitting = isAttacking || normalizedSpeed > 0.15f;
        }

        if (meshyHero != null)
        {
            if (!meshyHero.gameObject.activeSelf)
                meshyHero.gameObject.SetActive(true);

            float bob = Mathf.Sin(cycleRadians) * 0.045f * normalizedSpeed;
            // Seat hero cleanly on the skinned horse saddle
            meshyHero.localPosition = new Vector3(0f, 0.72f + bob, -0.08f);

            ApplySeatedLegPose();

            // Add dynamic upper-body gallop lean and combat swing reinforcement
            if (spineBone != null && normalizedSpeed > 0.05f && !isAttacking)
            {
                float leanForward = Mathf.Lerp(0f, 12f, normalizedSpeed) + Mathf.Cos(cycleRadians) * 3.5f * normalizedSpeed;
                spineBone.localRotation *= Quaternion.Euler(leanForward, 0f, 0f);
            }
        }

        // Trigger combat animations when attacking
        if (isAttacking && !wasAttacking && heroAnimator != null && heroAnimator.runtimeAnimatorController != null)
        {
            bool useBamboo = campaign != null && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo;
            if (useBamboo)
            {
                if (attackComboIndex % 2 == 0)
                    heroAnimator.SetTrigger(BambooAttackHash);
                else
                    heroAnimator.SetTrigger(HeavySlamHash);
            }
            else
            {
                heroAnimator.SetTrigger(AttackHash);
            }
            attackComboIndex++;
        }
        wasAttacking = isAttacking;

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
