using UnityEngine;

/// <summary>ActionForge animation on the new rider, combined with the horse's native motion.</summary>
[DefaultExecutionOrder(100)]
public sealed class MeshyMountedRig : MonoBehaviour
{
    public Animator heroAnimator;
    public Transform[] armControls;
    public float[] attackClipLengths = { .433f, .533f, 1.533f };
    public float spearClipLength = 1.533f;
    Quaternion[] armRest;
    ThanhGiongCampaignController campaign;
    MountedHorseController movement;
    GodotMountedMotion motion;
    float actionRemaining, previousHealth, previousFood, previousHeat;
    bool equipmentWasBusy;
    public bool IsActionPlaying => actionRemaining > 0f;
    public bool IsReady => heroAnimator != null && heroAnimator.runtimeAnimatorController != null;

    void Awake()
    {
        campaign = GetComponentInParent<ThanhGiongCampaignController>();
        movement = GetComponentInParent<MountedHorseController>();
        motion = GetComponentInParent<GodotMountedMotion>();
        armRest = new Quaternion[armControls.Length];
        for (int i = 0; i < armControls.Length; i++) armRest[i] = armControls[i].localRotation;
        if (heroAnimator != null) { heroAnimator.applyRootMotion = false; heroAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        previousHealth = campaign != null ? campaign.Health : 0;
    }

    void Start()
    {
        if (campaign == null) return;
        previousHealth = campaign.Health; previousFood = campaign.Food; previousHeat = campaign.Heat;
        equipmentWasBusy = campaign.EquipmentBusy;
    }

    public void PlayAttack(float duration, bool spear, int comboBeat)
    {
        if (!IsReady) return;
        int variant = Mathf.Clamp((comboBeat - 1) % 3, 0, 2);
        float length = spear ? spearClipLength : attackClipLengths[variant];
        heroAnimator.SetFloat("ActionSpeed", length / Mathf.Max(duration, .1f));
        heroAnimator.SetInteger("AttackVariant", variant);
        heroAnimator.ResetTrigger(spear ? "Attack" : "BambooAttack");
        heroAnimator.SetTrigger(spear ? "BambooAttack" : "Attack");
        actionRemaining = duration;
    }

    void PlayGesture(string trigger, float duration, float sourceLength)
    {
        if (!IsReady || IsActionPlaying) return;
        heroAnimator.SetFloat("ActionSpeed", sourceLength / Mathf.Max(duration, .1f));
        heroAnimator.SetTrigger(trigger);
        actionRemaining = duration;
    }

    void LateUpdate()
    {
        if (!IsReady || campaign == null || movement == null) return;
        if (Time.deltaTime <= 0f) return;
        actionRemaining = Mathf.Max(0f, actionRemaining - Time.deltaTime);
        heroAnimator.SetBool("Ascending", motion != null && motion.flying);
        heroAnimator.SetFloat("Speed", 0f); // the horse owns locomotion; legs remain seated
        if (previousHealth > 0f && campaign.Health < previousHealth && !campaign.IsDead) PlayGesture("Hit", .28f, .333f);
        if (campaign.Food > previousFood) PlayGesture("Pickup", .55f, .833f);
        if (previousHeat - campaign.Heat > campaign.maxHeat * .4f) PlayGesture("Fire", .5f, .5f);
        if (campaign.EquipmentBusy && !equipmentWasBusy) PlayGesture("Interact", 1.2f, 2f);
        previousHealth = campaign.Health; previousFood = campaign.Food; previousHeat = campaign.Heat;
        equipmentWasBusy = campaign.EquipmentBusy;
        if (armRest != null)
        {
            // Authored idle/actions own the arms; avoid adding the old rig's reins
            // rotations on top of their different bone axes.
            for (int i = 0; i < armControls.Length; i++)
                if (armControls[i] != null) armControls[i].localRotation = armRest[i];
        }
    }
}
