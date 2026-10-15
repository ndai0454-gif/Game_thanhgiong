using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
public class ThanhGiongEnemy : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Chasing,
        TelegraphingAttack,
        Slamming,
        StuckInGround,    // Cửa sổ sơ hở (Vulnerability Window) - lấy cảm hứng từ mudLodge của The Heron trong Understory!
        Recovering,
        Stunned,
        Dead
    }

    public enum EnemyRole
    {
        Auto,
        Spearman,
        ShieldBearer,
        Archer,
        Raider
    }

    [Header("Enemy Attributes")]
    public float maxHealth = 65f;
    public float moveSpeed = 2.75f;
    public float engageDistance = 80f;
    public bool isBoss;
    public EnemyRole role = EnemyRole.Auto;
    public string enemyName = "Giáo Binh Ân";

    [Header("Heavy Slam & Vulnerability (Understory Mechanics)")]
    public float heavyAttackRange = 4.5f;
    public float heavyAttackCooldown = 4.6f;
    public float telegraphDuration = 0.72f;
    public float stuckDuration = 1.15f;           // Cửa sổ phản công ngắn hơn khi tăng độ khó
    public float vulnerabilityMultiplier = 1.75f; // Vẫn có thưởng né, nhưng boss ít bị phạt hơn.
    public float slamDamage = 42f;
    public float slamRadius = 3.15f;
    public float minionAttackInterval = 1.55f;
    public float locomotionSmoothTime = 0.16f;

    [Header("Iron Horse Pursuit & Formation")]
    public float surroundRadius = 2.25f;
    public float formationTolerance = 0.5f;
    public float separationRadius = 1.0f;
    public float separationStrength = 1.25f;

    private EnemyState currentState = EnemyState.Idle;
    public EnemyState CurrentState
    {
        get => currentState;
        private set
        {
            if (currentState == value) return;
            BeginAnimationState(value, DefaultAnimationDuration(value),
                value == EnemyState.Slamming ? (isBoss ? .22f : resolvedRole == EnemyRole.Archer ? .12f : .11f) : 0f);
        }
    }
    private float animationStateStarted;
    private float animationStateDuration = 1f;
    private float animationContactTime;
    /// <summary>Actual gameplay state clock; clips never decide damage or movement.</summary>
    public float AnimationStateDuration => animationStateDuration;
    public float AnimationStatePhase => Mathf.Clamp01((Time.time - animationStateStarted) / animationStateDuration);
    public float AnimationContactPhase => Mathf.Clamp01(animationContactTime / animationStateDuration);
    public bool IsStuckInGround => CurrentState == EnemyState.StuckInGround;
    public bool IsStunned => Time.time < stunnedUntil || CurrentState == EnemyState.Stunned;
    public bool IsFlinching => flinchTimer > 0f;
    public float FlinchPhase => 1f - Mathf.Clamp01(flinchTimer / (isBoss ? 0.18f : 0.22f));
    public bool IsRooted => Time.time < rootedUntil;
    public bool IsExposed => Time.time < exposedUntil;
    public bool IsSlowed => Time.time < slowedUntil;
    public float HealthRatio => Mathf.Clamp01(health / maxHealth);
    public Rigidbody Body => rb;
    public bool WorldDormant { get; private set; }
    private bool worldTierConfigured;
    public void ConfigureWorldTier(OpenWorldRisk risk)
    {
        if(worldTierConfigured||rb==null)return;
        worldTierConfigured=true;
        float factor=risk==OpenWorldRisk.Black?1.55f:risk==OpenWorldRisk.Red?1.2f:.85f;
        float ratio=HealthRatio;
        maxHealth*=factor;health=maxHealth*ratio;
        slamDamage*=factor;moveSpeed*=risk==OpenWorldRisk.Black?1.15f:risk==OpenWorldRisk.Yellow?.88f:1f;
    }
    public void SetWorldDormant(bool value)
    {
        if(WorldDormant==value&&(rb==null||rb.isKinematic==value))return;
        if(value&&!WorldDormant){
            StopAllCoroutines();DestroyWarningRing();DestroyStuckBladeMarker();
            if(CurrentState!=EnemyState.Dead)CurrentState=EnemyState.Idle;
        }
        if(!value&&WorldDormant&&CurrentState!=EnemyState.Dead){
            CurrentState=EnemyState.Chasing;
            nextMinionAttackTime=Mathf.Max(nextMinionAttackTime,Time.time+.7f);
            nextHeavyAttackTime=Mathf.Max(nextHeavyAttackTime,Time.time+.7f);
            nextRangedAttackTime=Mathf.Max(nextRangedAttackTime,Time.time+.7f);
        }
        WorldDormant=value;
        if(rb==null)return;
        if(value&&!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}
        rb.isKinematic=value;
    }

    private float health;
    private float stunnedUntil;
    private float rootedUntil;
    private float exposedUntil;
    private float exposureMultiplier = 1f;
    private float slowedUntil;
    private float slowFactor = 1f;
    private Transform target;
    private Transform focusTarget;
    private ThanhGiongCampaignController campaign;
    private Vector3 initialScale;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private Rigidbody rb;
    private CapsuleCollider col;
    private ThanhGiongRagdollPhysics ragdoll;
    private ThanhGiongEnemyAnimationDriver animationDriver;

    private float nextHeavyAttackTime;
    private float nextMinionAttackTime;
    private float nextRangedAttackTime;
    private int lastBossPhase = 1;
    private EnemyRole resolvedRole = EnemyRole.Spearman;
    private float stateTimer;
    private Vector3 slamTargetPos;
    private GameObject activeWarningRing;
    private GameObject stuckBladeMarker;
    private Transform visualModel;
    private Vector3 visualBaseLocalPos;
    private Quaternion visualBaseLocalRot;
    private Vector3 smoothedPlanarVelocity;
    private Vector3 velocityDamp;
    private Vector3 visualBaseScale;
    private float previousYaw;
    private float currentBankAngle;
    private float enemyGaitTime;
    private float flinchTimer;
    private float visualSpeedBlend;
    private float visualSpeedBlendVel;
    private float attackPoseBlend;
    private float stuckPoseBlend;
    private float stunnedPoseBlend;
    private Vector3 visualPoseVelocity;
    private Vector3 visualScaleVelocity;
    private Vector3 formationOffset;
    private int formationSlot = -1;
    private static int nextFormationSlot;
    private readonly Collider[] separationHits = new Collider[16];

    public Transform FocusTarget => focusTarget;
    public EnemyRole ResolvedRole => isBoss ? EnemyRole.ShieldBearer : resolvedRole;
    public int BossPhase => !isBoss ? 0 : HealthRatio > .66f ? 1 : HealthRatio > .33f ? 2 : 3;


    private void Awake()
    {
        initialScale = transform.localScale;
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        if (isBoss)
        {
            maxHealth = 540f;
            moveSpeed = 3.2f;
            enemyName = "Tướng Giặc Ân";
            transform.localScale = initialScale * 1.8f;
        }

        col = GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = isBoss ? 0.9f : 0.5f;
            col.height = isBoss ? 3.6f : 2.0f;
            col.center = Vector3.zero;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = isBoss ? 180f : 80f;
        rb.linearDamping = 0.8f;
        rb.angularDamping = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.isKinematic = false;

        ragdoll = GetComponent<ThanhGiongRagdollPhysics>();
        if (ragdoll == null) ragdoll = gameObject.AddComponent<ThanhGiongRagdollPhysics>();

        animationDriver = GetComponent<ThanhGiongEnemyAnimationDriver>();
        if (animationDriver == null) animationDriver = gameObject.AddComponent<ThanhGiongEnemyAnimationDriver>();

        // Cache or create visual pivot for procedural combat telegraphing & struggle shaking
        CacheVisualPivot();
        ResolveIronHorseTarget();
    }

    private void CacheVisualPivot()
    {
        if (transform.childCount > 0)
        {
            visualModel = transform.Find("Visual");
            if (visualModel == null) visualModel = transform.GetChild(0);
        }
        else
        {
            visualModel = transform;
        }

        if (visualModel != null && visualModel != transform)
        {
            visualBaseLocalPos = visualModel.localPosition;
            visualBaseLocalRot = visualModel.localRotation;
            visualBaseScale = visualModel.localScale;
            previousYaw = transform.eulerAngles.y;
        }
    }

    private void OnEnable()
    {
        if (formationSlot < 0)
        {
            formationSlot = nextFormationSlot++;
            ComputeFormationOffset();
        }

        ResolveIronHorseTarget();
        ResolveCombatRole();
        health = maxHealth;
        stunnedUntil = 0f;
        rootedUntil = 0f;
        exposedUntil = 0f; exposureMultiplier = 1f; slowedUntil = 0f; slowFactor = 1f;
        BeginAnimationState(EnemyState.Idle, 1f);
        nextHeavyAttackTime = Time.time + Random.Range(1.5f, 3.0f);
        nextMinionAttackTime = Time.time + Random.Range(0.5f, 1.5f);
        nextRangedAttackTime = Time.time + Random.Range(1.1f, 2.2f);
        lastBossPhase = isBoss ? 1 : 0;
        smoothedPlanarVelocity = Vector3.zero;
        velocityDamp = Vector3.zero;
        visualSpeedBlend = 0f;
        visualSpeedBlendVel = 0f;
        attackPoseBlend = 0f;
        stuckPoseBlend = 0f;
        stunnedPoseBlend = 0f;
        visualPoseVelocity = Vector3.zero;
        visualScaleVelocity = Vector3.zero;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ResetVisualPose();
    }

    private void OnDisable()
    {
        DestroyWarningRing();
        DestroyStuckBladeMarker();
    }

    public void Initialize(ThanhGiongCampaignController owner, Transform player)
    {
        campaign = owner;
        target = player;
        ResolveIronHorseTarget();
        ResolveCombatRole();
        gameObject.SetActive(true);
        CurrentState = EnemyState.Chasing;
    }

    public void ResetForBattle(ThanhGiongCampaignController owner, Transform player)
    {
        StopAllCoroutines();
        if (ragdoll != null) ragdoll.ResetForBattle();
        gameObject.SetActive(false); // Restores original dissolve materials and clears telegraphs.
        transform.SetPositionAndRotation(initialPosition, initialRotation);
        transform.localScale = isBoss ? initialScale * 1.8f : initialScale;
        if (col != null) col.enabled = true;
        stateTimer = 0; flinchTimer = 0; enemyGaitTime = 0;
        visualSpeedBlend = 0f; visualSpeedBlendVel = 0f; attackPoseBlend = 0f; stuckPoseBlend = 0f; stunnedPoseBlend = 0f;
        exposedUntil = 0f; exposureMultiplier = 1f; slowedUntil = 0f; slowFactor = 1f;
        visualPoseVelocity = Vector3.zero; visualScaleVelocity = Vector3.zero;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true)) {
            var main = particles.main; main.playOnAwake = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        campaign = owner; target = player;
        gameObject.SetActive(true);
        Initialize(owner, player);
    }

    private void Update()
    {
        if(WorldDormant)return;
        if (target == null || focusTarget == null) ResolveIronHorseTarget();

        AnimateEnemyProcedural();
    }

    private void AnimateEnemyProcedural()
    {
        if (visualModel == null || visualModel == transform || CurrentState == EnemyState.Dead) return;

        // Authored skeletal motion supplies the actual gait and weapon poses. Keep
        // only a small steering response so the old whole-body tilts do not double it.
        if (animationDriver != null && animationDriver.UsesActionForge)
        {
            float secondaryDt = Mathf.Max(Time.deltaTime, .0001f);
            float yawRate = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / secondaryDt;
            previousYaw = transform.eulerAngles.y;
            float bank = CurrentState == EnemyState.Chasing ? Mathf.Clamp(-yawRate * .012f, -1.5f, 1.5f) : 0f;
            currentBankAngle = Mathf.Lerp(currentBankAngle, bank, 1f - Mathf.Exp(-9f * secondaryDt));
            visualModel.localPosition = Vector3.SmoothDamp(visualModel.localPosition, visualBaseLocalPos,
                ref visualPoseVelocity, .06f, 12f, secondaryDt);
            visualModel.localRotation = Quaternion.Slerp(visualModel.localRotation,
                visualBaseLocalRot * Quaternion.Euler(0f, 0f, currentBankAngle), 1f - Mathf.Exp(-20f * secondaryDt));
            visualModel.localScale = Vector3.SmoothDamp(visualModel.localScale, visualBaseScale,
                ref visualScaleVelocity, .07f, 10f, secondaryDt);
            flinchTimer = Mathf.Max(0f, flinchTimer - secondaryDt);
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 planarVelocity = rb != null ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) : Vector3.zero;
        float speed = planarVelocity.magnitude;
        float desiredSpeedBlend = CurrentState == EnemyState.Chasing ? Mathf.Clamp01(speed / Mathf.Max(moveSpeed, 0.01f)) : 0f;
        visualSpeedBlend = Mathf.SmoothDamp(visualSpeedBlend, desiredSpeedBlend, ref visualSpeedBlendVel, CurrentState == EnemyState.Chasing ? 0.16f : 0.24f, 8f, dt);

        // Dynamic turn banking calculation (Understory predator steering feel)
        float yawDelta = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / dt;
        previousYaw = transform.eulerAngles.y;
        float targetBank = -Mathf.Clamp(yawDelta * 0.22f, -10f, 10f) * Mathf.Lerp(.35f, 1f, visualSpeedBlend);
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, 1f - Mathf.Exp(-9f * dt));
        attackPoseBlend = Mathf.MoveTowards(attackPoseBlend, CurrentState == EnemyState.TelegraphingAttack || CurrentState == EnemyState.Slamming ? 1f : 0f, dt * 5.5f);
        stuckPoseBlend = Mathf.MoveTowards(stuckPoseBlend, CurrentState == EnemyState.StuckInGround || CurrentState == EnemyState.Recovering ? 1f : 0f, dt * 4f);
        stunnedPoseBlend = Mathf.MoveTowards(stunnedPoseBlend, CurrentState == EnemyState.Stunned ? 1f : 0f, dt * 5f);

        Vector3 targetPos = visualBaseLocalPos;
        Quaternion targetRot = visualBaseLocalRot;
        Vector3 targetScale = visualBaseScale;

        // Flinch reaction when struck
        if (flinchTimer > 0f)
        {
            flinchTimer -= Time.deltaTime;
            float flinchT = flinchTimer / 0.16f;
            targetScale = new Vector3(visualBaseScale.x * (1f + flinchT * 0.09f), visualBaseScale.y * (1f - flinchT * 0.12f), visualBaseScale.z * (1f + flinchT * 0.09f));
            targetPos += Random.insideUnitSphere * (flinchT * 0.08f);
        }

        switch (CurrentState)
        {
            case EnemyState.Idle:
                float breatheRate = isBoss ? 1.8f : 2.6f;
                float breathe = Mathf.Sin(Time.time * breatheRate);
                targetPos.y += breathe * 0.024f;
                targetRot *= Quaternion.Euler(breathe * 1.4f, Mathf.Cos(Time.time * 0.9f) * 2.2f, 0f);
                targetScale = new Vector3(visualBaseScale.x * (1f - breathe * 0.015f), visualBaseScale.y * (1f + breathe * 0.022f), visualBaseScale.z * (1f - breathe * 0.015f));
                break;

            case EnemyState.Chasing:
                enemyGaitTime += dt * (isBoss ? 5.2f : 7.4f) * (0.35f + visualSpeedBlend * 0.65f);

                float wave = Mathf.Sin(enemyGaitTime);
                float doubleWave = Mathf.Sin(enemyGaitTime * 2f);
                float footPlant = Mathf.Pow(Mathf.Abs(wave), 0.65f);
                float stepLift = Mathf.Max(0f, Mathf.Sin(enemyGaitTime + Mathf.PI * .22f));

                // Heavy footstep vertical bob
                float stepBob = footPlant * (isBoss ? 0.085f : 0.052f) * visualSpeedBlend;
                targetPos.y += stepBob + stepLift * (isBoss ? 0.018f : 0.026f) * visualSpeedBlend;
                if (planarVelocity.sqrMagnitude > .01f && visualModel.parent != null)
                    targetPos += visualModel.parent.InverseTransformDirection(planarVelocity.normalized) * (Mathf.Sin(enemyGaitTime + Mathf.PI) * .035f * visualSpeedBlend);

                // Predatory forward lean while hunting
                float forwardPitch = (isBoss ? 9f : 12f) * visualSpeedBlend;

                // Heavy waddle sway (boss steps side to side with immense mass)
                float waddleRoll = wave * (isBoss ? 6.2f : 3.2f) * visualSpeedBlend;

                targetRot *= Quaternion.Euler(
                    forwardPitch + doubleWave * 2.2f,
                    Mathf.Sin(enemyGaitTime * .5f) * (isBoss ? 1.4f : 2.1f) * visualSpeedBlend,
                    waddleRoll + currentBankAngle
                );

                targetScale = new Vector3(
                    visualBaseScale.x * (1f + wave * 0.018f * visualSpeedBlend),
                    visualBaseScale.y * (1f - footPlant * 0.022f * visualSpeedBlend),
                    visualBaseScale.z * (1f + visualSpeedBlend * 0.032f)
                );
                break;

            case EnemyState.TelegraphingAttack:
                float tense = Mathf.Sin(Time.time * 42f) * 0.03f * attackPoseBlend;
                targetPos += Vector3.up * (0.18f + 0.24f * attackPoseBlend) + new Vector3(tense, 0f, tense);
                targetRot *= Quaternion.Euler(-26f * attackPoseBlend, 0f, tense * 20f);
                targetScale = new Vector3(visualBaseScale.x * (1f + .05f * attackPoseBlend), visualBaseScale.y * (1f + .08f * attackPoseBlend), visualBaseScale.z * (1f - .05f * attackPoseBlend));
                break;

            case EnemyState.Slamming:
                targetPos += Vector3.down * .08f;
                targetRot *= Quaternion.Euler(42f * attackPoseBlend, 0f, 0f);
                targetScale = new Vector3(visualBaseScale.x * 1.08f, visualBaseScale.y * 0.88f, visualBaseScale.z * 1.12f);
                break;

            case EnemyState.StuckInGround:
                // Struggling to yank the weapon out (Understory mudLodge)
                float struggle = Mathf.Sin(Time.time * 36f) * (isBoss ? 0.08f : 0.05f);
                targetPos += new Vector3(struggle, 0f, Mathf.Cos(Time.time * 28f) * struggle * 0.5f) * stuckPoseBlend;
                targetRot *= Quaternion.Euler((38f + Mathf.Sin(Time.time * 16f) * 4f) * stuckPoseBlend, 0f, struggle * 32f * stuckPoseBlend);
                break;

            case EnemyState.Recovering:
                targetPos += (Vector3.up * 0.15f - transform.forward * 0.2f) * stuckPoseBlend;
                targetRot *= Quaternion.Euler(-14f * stuckPoseBlend, 0f, 0f);
                break;

            case EnemyState.Stunned:
                float dizzy = Time.time * 4.5f;
                targetPos.y += Mathf.Sin(dizzy) * 0.035f * stunnedPoseBlend;
                targetRot *= Quaternion.Euler(Mathf.Sin(dizzy) * 14f * stunnedPoseBlend, Mathf.Cos(dizzy * 0.5f) * 18f * stunnedPoseBlend, Mathf.Sin(dizzy * 1.4f) * 10f * stunnedPoseBlend);
                break;
        }

        float damping = CurrentState == EnemyState.Chasing ? .085f : .055f;
        visualModel.localPosition = Vector3.SmoothDamp(visualModel.localPosition, targetPos, ref visualPoseVelocity, damping, 12f, dt);
        visualModel.localRotation = Quaternion.Slerp(visualModel.localRotation, targetRot, 1f - Mathf.Exp(-20f * dt));
        visualModel.localScale = Vector3.SmoothDamp(visualModel.localScale, targetScale, ref visualScaleVelocity, .07f, 10f, dt);
    }

    private void FixedUpdate()
    {
        if(WorldDormant)return;
        if (health <= 0f || CurrentState == EnemyState.Dead) return;
        if (IsRooted) { if (rb != null) SmoothVelocity(Vector3.zero); return; }

        if (Time.time < stunnedUntil)
        {
            CurrentState = EnemyState.Stunned;
            return;
        }

        if (target == null || (campaign != null && !campaign.IsBattleActive))
        {
            if (rb != null) SmoothVelocity(Vector3.zero);
            CurrentState = EnemyState.Idle;
            return;
        }

        // Handle active state machines
        switch (CurrentState)
        {
            case EnemyState.TelegraphingAttack:
                UpdateTelegraphing();
                return;

            case EnemyState.Slamming:
                return;

            case EnemyState.StuckInGround:
                UpdateStuckInGround();
                return;

            case EnemyState.Recovering:
                UpdateRecovering();
                return;

            default:
                CurrentState = EnemyState.Chasing;
                UpdateChasingMovement();
                break;
        }
    }

    private void ResolveCombatRole()
    {
        if (isBoss)
        {
            resolvedRole = EnemyRole.ShieldBearer;
            return;
        }

        if (role != EnemyRole.Auto)
        {
            resolvedRole = role;
        }
        else
        {
            int slot = formationSlot >= 0 ? formationSlot : Mathf.Abs(GetEntityId().GetHashCode());
            resolvedRole = (slot % 6) switch
            {
                0 => EnemyRole.ShieldBearer,
                2 => EnemyRole.Archer,
                4 => EnemyRole.Raider,
                _ => EnemyRole.Spearman
            };
        }

        if (string.IsNullOrWhiteSpace(enemyName) || enemyName == "Giáo Binh Ân" || enemyName.StartsWith("Giáo binh Ân"))
        {
            enemyName = resolvedRole switch
            {
                EnemyRole.ShieldBearer => "Khiên Binh Ân",
                EnemyRole.Archer => "Cung Binh Ân",
                EnemyRole.Raider => "Kỵ Tập Binh Ân",
                _ => "Giáo Binh Ân"
            };
        }
    }

    private float RoleSpeedMultiplier() => resolvedRole switch
    {
        EnemyRole.ShieldBearer => .82f,
        EnemyRole.Archer => .94f,
        EnemyRole.Raider => 1.18f,
        _ => 1f
    };

    private float RoleDamageMultiplier() => resolvedRole switch
    {
        EnemyRole.ShieldBearer => 1.16f,
        EnemyRole.Archer => .82f,
        EnemyRole.Raider => 1.05f,
        _ => 1f
    };

    private float RoleCooldownMultiplier() => resolvedRole switch
    {
        EnemyRole.ShieldBearer => 1.18f,
        EnemyRole.Archer => 1.35f,
        EnemyRole.Raider => .78f,
        _ => 1f
    };

    private float RoleRangeMultiplier() => resolvedRole switch
    {
        EnemyRole.ShieldBearer => .95f,
        EnemyRole.Archer => 1.25f,
        EnemyRole.Raider => 1.08f,
        _ => 1f
    };

    private float BossPhaseMultiplier()
    {
        if (!isBoss) return 1f;
        return BossPhase switch { 2 => 1.14f, 3 => 1.32f, _ => 1f };
    }

    private void AnnounceBossPhaseIfNeeded()
    {
        if (!isBoss || campaign == null || health <= 0f) return;
        int phase = BossPhase;
        if (phase <= lastBossPhase) return;
        lastBossPhase = phase;
        if (phase == 2)
            campaign.ShowMessage("TƯỚNG GIẶC ÂN NỔI GIẬN! ĐÒN DẬM ĐẤT NHANH VÀ RỘNG HƠN!", 2.4f);
        else if (phase == 3)
            campaign.ShowMessage("PHASE CUỐI! TƯỚNG GIẶC LIỀU MẠNG — NÉ VÒNG ĐỎ RỒI PHẢN CÔNG NGAY!", 3.0f);
        SpawnBladeSparks(transform.position + Vector3.up * 1.1f);
        IsometricCameraFollow.Instance?.Shake(0.55f + phase * .08f, .55f);
    }

    private void UpdateChasingMovement()
    {
        if (rb == null) return;

        Vector3 focusPosition = focusTarget != null ? focusTarget.position : target.position;
        Vector3 attackDelta = focusPosition - rb.position;
        attackDelta.y = 0f;
        float attackSqrDist = attackDelta.sqrMagnitude;

        Vector3 desiredSlot = GetReachableFormationPoint(focusPosition);
        if (!isBoss && resolvedRole == EnemyRole.Archer && formationOffset.sqrMagnitude > .01f)
            desiredSlot = focusPosition + formationOffset.normalized * Mathf.Max(7.5f, surroundRadius + 3.5f);
        Vector3 delta = desiredSlot - rb.position;
        delta.y = 0f;
        float sqrDist = delta.sqrMagnitude;

        // Boss Heavy Slam trigger check
        float attackRangeScale = (campaign != null ? campaign.GetEnemyAttackRangeScale(isBoss) : 1f) * RoleRangeMultiplier();
        float phaseMultiplier = BossPhaseMultiplier();
        float heavyRange = heavyAttackRange * attackRangeScale * phaseMultiplier;
        float minionRange = 2.8f * attackRangeScale;
        float archerRange = 12f * attackRangeScale;

        if (isBoss && Time.time >= nextHeavyAttackTime && attackSqrDist <= heavyRange * heavyRange)
        {
            StartCoroutine(PerformHeavySlamTelegraph());
            return;
        }

        if (!isBoss && resolvedRole == EnemyRole.Archer && Time.time >= nextRangedAttackTime && attackSqrDist <= archerRange * archerRange && attackSqrDist > 3.4f * 3.4f)
        {
            StartCoroutine(PerformRangedVolley(archerRange));
            return;
        }

        // Minion quick spear thrust check
        if (!isBoss && Time.time >= nextMinionAttackTime && attackSqrDist <= minionRange * minionRange)
        {
            StartCoroutine(PerformMinionThrust());
            return;
        }

        // Stopping distance in melee contact
        if (sqrDist < formationTolerance * formationTolerance)
        {
            SmoothVelocity(Vector3.zero);
            Vector3 faceDir = attackDelta.normalized;
            if (faceDir.sqrMagnitude > 0.001f)
            {
                Quaternion faceRot = Quaternion.LookRotation(faceDir, Vector3.up);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, faceRot, 8f * Time.fixedDeltaTime));
            }
            return;
        }

        Vector3 direction = delta.normalized;
        bool usingNavPath = TryGetNavDirection(desiredSlot, ref direction);
        direction = ApplyEnemySeparation(direction);

        // Physical obstacle & companion avoidance ray/spherecast (fallback when the NavMesh route is not in use)
        Vector3 rayOrigin = rb.position + Vector3.up * 0.7f;
        if (!usingNavPath && Physics.SphereCast(rayOrigin, 0.45f, direction, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != target && !hit.transform.IsChildOf(target))
            {
                Vector3 normal = hit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude > 0.01f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, normal.normalized).normalized;
                    if (Vector3.Dot(tangent, direction) < 0f) tangent = -tangent;
                    direction = (direction + tangent * 1.5f).normalized;
                }
            }
        }

        // Apply physical velocity
        float speedScale = campaign != null ? campaign.GetEnemySpeedScale(isBoss) : 1f;
        float catchUpMultiplier = attackSqrDist > engageDistance * engageDistance ? 1.32f : 1f;
        Vector3 targetVelocity = direction * moveSpeed * speedScale * RoleSpeedMultiplier() * catchUpMultiplier * (IsSlowed ? slowFactor : 1f);
        SmoothVelocity(targetVelocity);

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, 8f * Time.fixedDeltaTime));
        }
    }

    // ---- NavMesh path following (planner only; the Rigidbody still drives the motion) ----
    private UnityEngine.AI.NavMeshPath navPath;
    private float nextRepathTime;
    private int navCorner;
    private bool navPathValid;

    private bool TryGetNavDirection(Vector3 goal, ref Vector3 direction)
    {
        Vector3 pos = rb.position;
        if (!UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit from, 2.5f, UnityEngine.AI.NavMesh.AllAreas)) { navPathValid = false; return false; }
        if (!UnityEngine.AI.NavMesh.SamplePosition(goal, out UnityEngine.AI.NavMeshHit to, 3.5f, UnityEngine.AI.NavMesh.AllAreas)) { navPathValid = false; return false; }

        // Clear straight line on the mesh: walk directly (keeps formation feel, no zig-zag).
        if (!UnityEngine.AI.NavMesh.Raycast(from.position, to.position, out UnityEngine.AI.NavMeshHit blocked, UnityEngine.AI.NavMesh.AllAreas))
        {
            navPathValid = false;
            Vector3 straight = to.position - from.position; straight.y = 0f;
            if (straight.sqrMagnitude > .01f) direction = straight.normalized;
            return true;
        }

        if (navPath == null) navPath = new UnityEngine.AI.NavMeshPath();
        if (!navPathValid || Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Random.Range(.3f, .5f);
            navPathValid = UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, navPath) && navPath.corners.Length > 1;
            navCorner = 1;
        }
        if (!navPathValid) return false;

        Vector3[] corners = navPath.corners;
        while (navCorner < corners.Length - 1)
        {
            Vector3 flat = corners[navCorner] - pos; flat.y = 0f;
            if (flat.sqrMagnitude > .55f * .55f) break;
            navCorner++;
        }
        Vector3 step = corners[Mathf.Min(navCorner, corners.Length - 1)] - pos; step.y = 0f;
        if (step.sqrMagnitude < .0004f) return false;
        direction = step.normalized;
        return true;
    }

    private void ResolveIronHorseTarget()
    {
        MountedHorseController mounted = FindAnyObjectByType<MountedHorseController>();
        if (mounted != null)
        {
            target = mounted.transform;
            // Use a dedicated gameplay target on the mounted character root.
            // Never target environment meshes or a visual pivot that may be
            // animated into a wall during rearing/attacks.
            Transform combatFocus = mounted.transform.Find("CombatFocus_IronHorse");
            focusTarget = combatFocus != null ? combatFocus : mounted.transform;
            ComputeFormationOffset();
            return;
        }

        AdventurerController adventurer = FindAnyObjectByType<AdventurerController>();
        if (adventurer != null)
        {
            target = adventurer.transform;
            focusTarget = target;
            ComputeFormationOffset();
        }
    }

    private Vector3 GetReachableFormationPoint(Vector3 focusPosition)
    {
        Vector3 candidate = focusPosition + formationOffset;
        int count = Physics.OverlapSphereNonAlloc(candidate, 0.4f, separationHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = separationHits[i];
            if (hit == null || hit == col) continue;
            if (target != null && hit.transform.IsChildOf(target)) continue;
            if (hit.GetComponentInParent<ThanhGiongEnemy>() != null) continue;

            // Static colliders here are architecture/floor props. If a ring slot
            // falls inside one, chase the mounted character itself instead.
            if (hit.attachedRigidbody == null) return focusPosition;
        }
        return candidate;
    }

    private void ComputeFormationOffset()
    {
        // Six soldiers per ring leaves a readable combat lane around the horse.
        // Wider outer rings stop a whole wave from hiding the mounted hero at
        // scene start while still letting the army close in organically.
        int slot = formationSlot >= 0 ? formationSlot : Mathf.Abs(GetEntityId().GetHashCode());
        const int soldiersPerRing = 6;
        int ringIndex = (slot / soldiersPerRing) % 5;
        int positionInRing = slot % soldiersPerRing;
        float angleDegrees = positionInRing * (360f / soldiersPerRing) + ringIndex * 30f;
        float angle = angleDegrees * Mathf.Deg2Rad;
        float ring = surroundRadius + ringIndex * 1.35f;
        if (isBoss) ring += 1.2f;
        formationOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
    }

    private Vector3 ApplyEnemySeparation(Vector3 desiredDirection)
    {
        int count = Physics.OverlapSphereNonAlloc(rb.position, separationRadius, separationHits, ~0, QueryTriggerInteraction.Ignore);
        Vector3 separation = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider otherCollider = separationHits[i];
            if (otherCollider == null || otherCollider == col) continue;
            ThanhGiongEnemy other = otherCollider.GetComponentInParent<ThanhGiongEnemy>();
            if (other == null || other == this || other.CurrentState == EnemyState.Dead) continue;

            Vector3 away = rb.position - other.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance > 0.001f)
                separation += away / distance * (1f - Mathf.Clamp01(distance / separationRadius));
        }

        Vector3 combined = desiredDirection + separation * separationStrength;
        return combined.sqrMagnitude > 0.001f ? combined.normalized : desiredDirection;
    }

    private void SmoothVelocity(Vector3 targetVelocity)
    {
        float speedScale = (campaign != null ? campaign.GetEnemySpeedScale(isBoss) : 1f) * RoleSpeedMultiplier();
        smoothedPlanarVelocity = Vector3.SmoothDamp(smoothedPlanarVelocity, targetVelocity, ref velocityDamp, locomotionSmoothTime, moveSpeed * speedScale * 4f, Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(smoothedPlanarVelocity.x, rb.linearVelocity.y, smoothedPlanarVelocity.z);
    }

    #region Understory Vulnerability & Heavy Slam Coroutines

    private float DefaultAnimationDuration(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.TelegraphingAttack: return isBoss ? telegraphDuration : resolvedRole == EnemyRole.Archer ? .42f : .34f;
            case EnemyState.Slamming: return isBoss ? .62f : resolvedRole == EnemyRole.Archer ? .4f : .44f;
            case EnemyState.StuckInGround: return Mathf.Max(.001f, stateTimer > 0f ? stateTimer : stuckDuration);
            case EnemyState.Recovering: return isBoss ? .45f : .18f;
            case EnemyState.Stunned: return Mathf.Max(.05f, stunnedUntil - Time.time);
            default: return 1f;
        }
    }

    private void BeginAnimationState(EnemyState state, float duration, float contactTime = 0f)
    {
        currentState = state;
        animationStateStarted = Time.time;
        animationStateDuration = Mathf.Max(.001f, duration);
        animationContactTime = Mathf.Clamp(contactTime, 0f, animationStateDuration);
    }

    private bool UsesAuthoredAnimation => animationDriver != null && animationDriver.UsesActionForge;

    private IEnumerator PerformHeavySlamTelegraph()
    {
        CurrentState = EnemyState.TelegraphingAttack;
        rb.linearVelocity = Vector3.zero;

        // Predict slam position right in front of target or player's current location
        slamTargetPos = target.position;
        Vector3 faceDir = (slamTargetPos - transform.position);
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(faceDir.normalized, Vector3.up);

        // Spawn glowing red telegraph warning ring on ground
        float pressure = campaign != null ? campaign.BattlePressure01 : 0f;
        float cooldownScale = campaign != null ? campaign.GetEnemyCooldownScale(true) : 1f;
        float damageScale = (campaign != null ? campaign.GetEnemyDamageScale(true) : 1f) * BossPhaseMultiplier();
        float phase = BossPhase - 1f;
        float effectiveTelegraph = Mathf.Max(.34f, telegraphDuration * Mathf.Lerp(1f, .78f, pressure) * Mathf.Lerp(1f, .82f, Mathf.Clamp01(phase / 2f)));
        float effectiveSlamRadius = slamRadius * Mathf.Lerp(1f, 1.14f, pressure) * Mathf.Lerp(1f, 1.18f, Mathf.Clamp01(phase / 2f));
        float effectiveStuckDuration = Mathf.Max(.45f, stuckDuration * Mathf.Lerp(1f, .74f, pressure) * Mathf.Lerp(1f, .78f, Mathf.Clamp01(phase / 2f)));
        BeginAnimationState(EnemyState.TelegraphingAttack, effectiveTelegraph);

        SpawnWarningRing(slamTargetPos, effectiveSlamRadius);

        // Procedural wind-up: Lean body backward and raise upward
        float elapsed = 0f;
        while (elapsed < effectiveTelegraph)
        {
            if (health <= 0f || Time.time < stunnedUntil || campaign == null || !campaign.IsBattleActive)
            {
                DestroyWarningRing();
                ResetVisualPose();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / effectiveTelegraph;

            if (!UsesAuthoredAnimation && visualModel != null && visualModel != transform)
            {
                visualModel.localPosition = visualBaseLocalPos + Vector3.up * (Mathf.Sin(t * Mathf.PI * 0.5f) * 0.45f);
                visualModel.localRotation = visualBaseLocalRot * Quaternion.Euler(-25f * t, 0f, 0f);
            }

            yield return null;
        }

        // Phase: Slamming down
        BeginAnimationState(EnemyState.Slamming, .62f, .22f);
        DestroyWarningRing();

        // Smash down violently forward
        if (!UsesAuthoredAnimation && visualModel != null && visualModel != transform)
        {
            visualModel.localPosition = visualBaseLocalPos;
            visualModel.localRotation = visualBaseLocalRot * Quaternion.Euler(42f, 0f, 0f);
        }

        // Let the weapon animation reach its downswing before applying the hit.
        yield return new WaitForSeconds(.22f);
        if (health <= 0f || Time.time < stunnedUntil || campaign == null || !campaign.IsBattleActive) yield break;

        // Ground shockwave effects & camera shake
        SpawnGroundImpact(slamTargetPos, effectiveSlamRadius);
        SpawnMeleeSlashArc(effectiveSlamRadius * 0.85f, new Color(1f, 0.35f, 0.08f, 0.95f), 0.35f);
        IsometricCameraFollow.Instance?.Shake(0.7f, 0.45f);

        // Check if player is caught in slam
        Vector3 slamOffset = target.position - slamTargetPos;
        slamOffset.y = 0;
        float distanceToPlayer = slamOffset.magnitude;
        bool hitPlayer = distanceToPlayer <= effectiveSlamRadius;
        if (BossPhase >= 3)
        {
            SpawnGroundImpact(slamTargetPos, effectiveSlamRadius * 1.28f);
        }

        if (hitPlayer)
        {
            campaign?.DamagePlayer(slamDamage * damageScale, transform.position);
            // Player hit! Boss knocks player back and swiftly recovers
            Vector3 pushDir = (target.position - slamTargetPos).normalized;
            if (pushDir.sqrMagnitude < 0.01f) pushDir = transform.forward;

            Rigidbody targetBody = target.GetComponent<Rigidbody>();
            if (targetBody != null && !targetBody.isKinematic)
            {
                targetBody.AddForce(pushDir * 18f + Vector3.up * 4f, ForceMode.Impulse);
            }


            yield return new WaitForSeconds(0.4f);
            ResetVisualPose();
            nextHeavyAttackTime = Time.time + heavyAttackCooldown * cooldownScale / BossPhaseMultiplier();
            CurrentState = EnemyState.Chasing;
        }
        else
        {
            // PLAYER DODGED! WEAPON GETS STUCK IN THE GROUND (Understory mudLodge)!
            BeginAnimationState(EnemyState.StuckInGround, effectiveStuckDuration);
            stateTimer = effectiveStuckDuration;
            SpawnStuckBladeMarker(slamTargetPos);

            // Announce critical vulnerability window to player
            campaign?.ShowMessage("★ SƠ HỞ NGẮN! ĐẠI ĐAO GĂM XUỐNG ĐẤT — PHẢN CÔNG NGAY! ★", effectiveStuckDuration);

            // Spawn stuck sparks & dust puff
            SpawnBladeSparks(slamTargetPos + Vector3.up * 0.2f);

            // Wait out stuck duration while struggling
            yield return new WaitForSeconds(effectiveStuckDuration);

            if (CurrentState == EnemyState.StuckInGround && health > 0f)
            {
                // Recover from being stuck
                CurrentState = EnemyState.Recovering;
                DestroyStuckBladeMarker();
                campaign?.ShowMessage("TƯỚNG GIẶC ĐÃ NHỔ ĐƯỢC ĐẠI ĐAO LÊN!", 1.2f);
                SpawnBladeSparks(transform.position + Vector3.up * 0.5f);

                yield return new WaitForSeconds(0.45f);
                ResetVisualPose();
                nextHeavyAttackTime = Time.time + heavyAttackCooldown * cooldownScale / BossPhaseMultiplier();
                CurrentState = EnemyState.Chasing;
            }
        }
    }

    private void FaceFocusTarget(float turnSpeed)
    {
        Transform f = focusTarget != null ? focusTarget : target;
        if (f == null || rb == null) return;
        Vector3 d = f.position - rb.position; d.y = 0f;
        if (d.sqrMagnitude < .0004f) return;
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), turnSpeed * Time.fixedDeltaTime));
    }

    private IEnumerator PerformMinionThrust()
    {
        float cooldownScale = (campaign != null ? campaign.GetEnemyCooldownScale(false) : 1f) * RoleCooldownMultiplier();
        float damageScale = (campaign != null ? campaign.GetEnemyDamageScale(false) : 1f) * RoleDamageMultiplier();
        float rangeScale = (campaign != null ? campaign.GetEnemyAttackRangeScale(false) : 1f) * RoleRangeMultiplier();
        float pressure = campaign != null ? campaign.BattlePressure01 : 0f;
        nextMinionAttackTime = Time.time + minionAttackInterval * cooldownScale;
        CurrentState = EnemyState.TelegraphingAttack;
        if (rb != null) SmoothVelocity(Vector3.zero);
        Vector3 origPos = visualModel != null ? visualModel.localPosition : Vector3.zero;

        // Wind-up: keep facing the target while drawing the weapon back and showing a clear ground telegraph.
        float windup = Mathf.Lerp(0.34f, 0.24f, pressure);
        BeginAnimationState(EnemyState.TelegraphingAttack, windup);
        Color roleColor = resolvedRole == EnemyRole.ShieldBearer
            ? new Color(1f, 0.78f, 0.22f, 0.9f)
            : resolvedRole == EnemyRole.Raider
                ? new Color(1f, 0.22f, 0.16f, 0.9f)
                : new Color(1f, 0.48f, 0.14f, 0.9f);
        SpawnRangedTracer(transform.position + Vector3.up * 0.14f, transform.position + transform.forward * (2.4f * rangeScale) + Vector3.up * 0.14f, roleColor, windup);

        for (float t = 0f; t < windup; t += Time.fixedDeltaTime)
        {
            FaceFocusTarget(14f);
            if (!UsesAuthoredAnimation && visualModel != null && visualModel != transform)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / windup);
                visualModel.localPosition = Vector3.Lerp(origPos, origPos - Vector3.forward * 0.16f, k);
                visualModel.localRotation = visualBaseLocalRot * Quaternion.Euler(Mathf.Lerp(0f, -14f, k), 0f, 0f);
            }
            yield return new WaitForFixedUpdate();
            if (health <= 0f || Time.time < stunnedUntil) yield break;
        }
        BeginAnimationState(EnemyState.Slamming, .44f, .11f);
        // Strike: short committed lunge forward, body pitched into the thrust.
        if (!UsesAuthoredAnimation && visualModel != null && visualModel != transform)
        {
            visualModel.localPosition = origPos + Vector3.forward * 0.28f;
            visualModel.localRotation = visualBaseLocalRot * Quaternion.Euler(16f, 0f, 0f);
        }
        bool damageDone = false;
        for (float t = 0f; t < .16f; t += Time.fixedDeltaTime)
        {
            Vector3 toFocus = (focusTarget != null ? focusTarget.position : target.position) - rb.position; toFocus.y = 0f;
            if (toFocus.magnitude > 1.15f)
                rb.MovePosition(rb.position + transform.forward * (6.5f * Time.fixedDeltaTime));
            if (!damageDone && t >= .11f)
            {
                damageDone = true;
                SpawnMeleeSlashArc(1.65f * rangeScale, roleColor, 0.24f);
                SpawnBladeSparks(transform.position + transform.forward * 1.15f + Vector3.up * 1.0f);
                if (health > 0f && target != null && campaign != null && campaign.IsBattleActive &&
                    Vector3.Distance(target.position, transform.position) < 3f * rangeScale)
                    campaign.DamagePlayer(8f * damageScale, transform.position);
            }
            yield return new WaitForFixedUpdate();
            if (health <= 0f || Time.time < stunnedUntil) yield break;
        }
        yield return new WaitForSeconds(.28f);
        if (health <= 0f || Time.time < stunnedUntil) yield break;
        CurrentState = EnemyState.Recovering;
        yield return new WaitForSeconds(.18f);
        ResetVisualPose();
        if (health > 0f && Time.time >= stunnedUntil) CurrentState = EnemyState.Chasing;
    }

    private IEnumerator PerformRangedVolley(float archerRange)
    {
        float cooldownScale = (campaign != null ? campaign.GetEnemyCooldownScale(false) : 1f) * RoleCooldownMultiplier();
        float damageScale = (campaign != null ? campaign.GetEnemyDamageScale(false) : 1f) * RoleDamageMultiplier();
        float pressure = campaign != null ? campaign.BattlePressure01 : 0f;
        nextRangedAttackTime = Time.time + Mathf.Lerp(2.4f, 1.55f, pressure) * cooldownScale;
        CurrentState = EnemyState.TelegraphingAttack;
        if (rb != null) SmoothVelocity(Vector3.zero);

        Vector3 origin = transform.position + Vector3.up * (isBoss ? 1.8f : 1.25f);
        Vector3 aim = focusTarget != null ? focusTarget.position + Vector3.up * .9f : origin + transform.forward * archerRange;
        Vector3 faceDir = aim - transform.position;
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > .01f && rb != null) rb.MoveRotation(Quaternion.LookRotation(faceDir.normalized, Vector3.up));
        SpawnRangedTracer(origin, aim, new Color(1f, .74f, .18f, .95f), .38f);

        float draw = Mathf.Lerp(.42f, .28f, pressure);
        BeginAnimationState(EnemyState.TelegraphingAttack, draw);
        for (float t = 0f; t < draw; t += Time.fixedDeltaTime)
        {
            FaceFocusTarget(10f);
            yield return new WaitForFixedUpdate();
            if (health <= 0f || Time.time < stunnedUntil) yield break;
        }
        BeginAnimationState(EnemyState.Slamming, .4f, .12f);
        yield return new WaitForSeconds(.12f);
        if (health <= 0f || Time.time < stunnedUntil) yield break;
        Vector3 launchOrigin = transform.position + Vector3.up * 1.3f + transform.forward * 0.45f;
        Vector3 launchTarget = focusTarget != null ? focusTarget.position + Vector3.up * .9f : aim;
        StartCoroutine(AnimateFlyingProjectile(launchOrigin, launchTarget, new Color(1f, .82f, .22f, 1f), .18f));
        if (health > 0f && Time.time >= stunnedUntil && target != null && campaign != null && campaign.IsBattleActive)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= archerRange * archerRange)
                campaign.DamagePlayer(6.5f * damageScale, transform.position);
        }
        yield return new WaitForSeconds(.28f);
        if (health <= 0f || Time.time < stunnedUntil) yield break;
        CurrentState = EnemyState.Recovering;
        yield return new WaitForSeconds(.18f);
        ResetVisualPose();
        if (health > 0f && Time.time >= stunnedUntil) CurrentState = EnemyState.Chasing;
    }

    private void UpdateTelegraphing()
    {
        if (rb != null) SmoothVelocity(Vector3.zero);
    }

    private void UpdateStuckInGround()
    {
        if (rb != null) SmoothVelocity(Vector3.zero);
    }

    private void UpdateRecovering()
    {
        if (rb != null) SmoothVelocity(Vector3.zero);
    }

    private void ResetVisualPose()
    {
        if (visualModel != null && visualModel != transform)
        {
            visualModel.localPosition = visualBaseLocalPos;
            visualModel.localRotation = visualBaseLocalRot;
            visualModel.localScale = visualBaseScale;
            visualPoseVelocity = Vector3.zero;
            visualScaleVelocity = Vector3.zero;
        }
    }

    private void SpawnStuckBladeMarker(Vector3 point)
    {
        DestroyStuckBladeMarker();
        stuckBladeMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stuckBladeMarker.name = "Đại Đao Găm Đất — Vulnerability 1.5s";
        stuckBladeMarker.transform.position = point + Vector3.up * .72f;
        stuckBladeMarker.transform.rotation = transform.rotation * Quaternion.Euler(58f, 0f, 0f);
        stuckBladeMarker.transform.localScale = new Vector3(.16f, 1.35f, .08f);
        Collider markerCollider = stuckBladeMarker.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);
        Renderer markerRenderer = stuckBladeMarker.GetComponent<Renderer>();
        Material markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        markerMaterial.color = new Color(.24f, .27f, .3f);
        if (markerMaterial.HasProperty("_Metallic")) markerMaterial.SetFloat("_Metallic", .85f);
        markerRenderer.material = markerMaterial;
    }

    private void DestroyStuckBladeMarker()
    {
        if (stuckBladeMarker == null) return;
        Renderer renderer = stuckBladeMarker.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null) Destroy(renderer.material);
        Destroy(stuckBladeMarker);
        stuckBladeMarker = null;
    }

    #endregion

    #region Procedural Visuals & FX

    private void SpawnWarningRing(Vector3 center, float radius)
    {
        DestroyWarningRing();

        activeWarningRing = new GameObject("BossWarningRing");
        activeWarningRing.transform.position = center + Vector3.up * 0.08f;

        LineRenderer line = activeWarningRing.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 36;
        line.startWidth = 0.22f;
        line.endWidth = 0.22f;
        line.numCapVertices = 4;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1.0f, 0.15f, 0.05f, 0.95f);
        line.endColor = new Color(1.0f, 0.5f, 0.1f, 0.95f);

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private void DestroyWarningRing()
    {
        if (activeWarningRing != null)
        {
            Destroy(activeWarningRing);
            activeWarningRing = null;
        }
    }

    private void SpawnGroundImpact(Vector3 center, float radius)
    {
        GameObject impact = new GameObject("GroundImpactWave");
        impact.transform.position = center + Vector3.up * 0.06f;

        LineRenderer line = impact.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 32;
        line.startWidth = 0.35f;
        line.endWidth = 0.35f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1.0f, 0.7f, 0.2f, 0.9f);
        line.endColor = new Color(0.8f, 0.3f, 0.05f, 0.0f);

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        StartCoroutine(FadeOutImpact(impact, line, 0.45f));
    }

    private void SpawnRangedTracer(Vector3 start, Vector3 end, Color color, float life)
    {
        GameObject tracer = new GameObject("EnemyArrowTelegraph");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = .06f;
        line.endWidth = .015f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        Destroy(tracer, life);
    }

    private void SpawnBladeSparks(Vector3 point)
    {
        GameObject sparks = new GameObject("BladeSparks");
        sparks.transform.position = point;

        LineRenderer line = sparks.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 10;
        line.startWidth = 0.08f;
        line.endWidth = 0.01f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1.0f, 0.95f, 0.3f, 1f);
        line.endColor = new Color(1.0f, 0.3f, 0.05f, 0f);

        for (int i = 0; i < line.positionCount; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 0.9f;
            offset.y = Mathf.Abs(offset.y);
            line.SetPosition(i, point + offset);
        }

        Destroy(sparks, 0.35f);
    }

    private void SpawnCritSparks(Vector3 point)
    {
        GameObject crit = new GameObject("VulnerabilityCritSparks");
        crit.transform.position = point;

        LineRenderer line = crit.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 16;
        line.startWidth = 0.15f;
        line.endWidth = 0.02f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1.0f, 0.85f, 0.15f, 1.0f);
        line.endColor = new Color(1.0f, 0.25f, 0.05f, 0.0f);

        for (int i = 0; i < line.positionCount; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 1.6f;
            offset.y = Mathf.Abs(offset.y) + 0.3f;
            line.SetPosition(i, point + offset);
        }

        Destroy(crit, 0.4f);
    }

    private void SpawnMeleeSlashArc(float radius, Color color, float duration)
    {
        GameObject arcObj = new GameObject("EnemyMeleeSlashArc");
        arcObj.transform.position = transform.position + Vector3.up * (isBoss ? 1.35f : 0.95f);
        float tilt = isBoss ? -38f : resolvedRole == EnemyRole.Raider ? 8f : -28f;
        arcObj.transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, tilt);

        LineRenderer line = arcObj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 18;
        line.startWidth = isBoss ? 0.36f : 0.22f;
        line.endWidth = 0.03f;
        line.numCapVertices = 4;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = new Color(1f, 0.92f, 0.45f, 0.15f);

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float angle = Mathf.Lerp(-68f, 68f, t) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius));
        }

        StartCoroutine(FadeOutImpact(arcObj, line, duration));
    }

    private IEnumerator AnimateFlyingProjectile(Vector3 start, Vector3 end, Color color, float flightDuration)
    {
        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        proj.name = "EnemyFlyingJavelin";
        Collider c = proj.GetComponent<Collider>();
        if (c != null) Destroy(c);
        proj.transform.position = start;
        Vector3 dir = (end - start);
        if (dir.sqrMagnitude > 0.001f) proj.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        proj.transform.localScale = new Vector3(0.07f, 0.07f, 0.95f);

        Renderer r = proj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Sprites/Default")) { color = color };
        if (r != null) r.material = mat;

        TrailRenderer trail = proj.AddComponent<TrailRenderer>();
        trail.time = 0.14f;
        trail.startWidth = 0.14f;
        trail.endWidth = 0.01f;
        trail.material = mat;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);

        float elapsed = 0f;
        float dur = Mathf.Max(0.05f, flightDuration);
        while (elapsed < dur && proj != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            Vector3 p = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.65f);
            proj.transform.position = p;
            yield return null;
        }

        SpawnBladeSparks(end);
        if (mat != null) Destroy(mat);
        if (proj != null) Destroy(proj);
    }

    private IEnumerator FadeOutImpact(GameObject obj, LineRenderer line, float duration)
    {
        float elapsed = 0f;
        Color sCol = line.startColor;
        Color eCol = line.endColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            line.startColor = new Color(sCol.r, sCol.g, sCol.b, Mathf.Lerp(sCol.a, 0f, t));
            line.endColor = new Color(eCol.r, eCol.g, eCol.b, Mathf.Lerp(eCol.a, 0f, t));
            yield return null;
        }

        Destroy(obj);
    }

    #endregion

    public void ApplyRoot(float seconds)
    {
        if (seconds <= 0 || health <= 0 || !gameObject.activeInHierarchy || Time.timeScale <= 0) return;
        rootedUntil = Mathf.Max(rootedUntil, Time.time + seconds);
        smoothedPlanarVelocity = Vector3.zero; velocityDamp = Vector3.zero;
        visualSpeedBlend = 0f; visualSpeedBlendVel = 0f;
        if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    public void ApplyExposure(float seconds, float damageMultiplier)
    {
        if (seconds <= 0f || health <= 0f) return;
        if (!IsExposed) exposureMultiplier = 1f;
        exposedUntil = Mathf.Max(exposedUntil, Time.time + seconds);
        exposureMultiplier = Mathf.Max(exposureMultiplier, damageMultiplier);
    }

    public void ApplySlow(float seconds, float movementMultiplier)
    {
        if (seconds <= 0f || health <= 0f) return;
        if (!IsSlowed) slowFactor = 1f;
        slowedUntil = Mathf.Max(slowedUntil, Time.time + seconds);
        slowFactor = Mathf.Min(slowFactor, Mathf.Clamp(movementMultiplier, .3f, 1f));
    }

    public void ApplyWindGust(Vector3 source, float force, float stunSeconds)
    {
        if (health <= 0f || !gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        DestroyWarningRing();
        DestroyStuckBladeMarker();
        ResetVisualPose();
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + stunSeconds);
        CurrentState = EnemyState.Stunned;
        Vector3 away = transform.position - source;
        away.y = 0f;
        if (away.sqrMagnitude < .01f) away = transform.forward;
        if (rb != null && !rb.isKinematic)
            rb.AddForce(away.normalized * force + Vector3.up * (isBoss ? .15f : .7f), ForceMode.Impulse);
    }

    public void RestoreCheckpoint(ThanhGiongCampaignController owner, Transform player, Vector3 position, Quaternion rotation, float savedHealth, bool alive)
    {
        ResetForBattle(owner, player);
        transform.SetPositionAndRotation(position, rotation);
        if(rb!=null){rb.position=position;rb.rotation=rotation;}
        health = Mathf.Clamp(savedHealth, 0, maxHealth);
        stunnedUntil = 0f;
        rootedUntil = 0f;
        exposedUntil = 0f; exposureMultiplier = 1f; slowedUntil = 0f; slowFactor = 1f;
        flinchTimer = 0f;
        CurrentState = alive && health > 0 ? EnemyState.Chasing : EnemyState.Dead;
        gameObject.SetActive(alive && health > 0);
    }

    public void TakeDamage(float amount, float stunSeconds, Vector3 hitSource) => TakeDamage(amount, stunSeconds, hitSource, 1f, false);

    public void TakeDamage(float amount, float stunSeconds = 0f, Vector3 hitSource = default, float impactScale = 1f, bool heavyImpact = false)
    {
        if (!gameObject.activeSelf || health <= 0f) return;

        float actualDamage = Mathf.Max(0f, amount);
        if (!isBoss && resolvedRole == EnemyRole.ShieldBearer)
            actualDamage *= IsExposed ? .95f : heavyImpact ? .85f : .68f;
        if (IsExposed) actualDamage *= exposureMultiplier;

        // VULNERABILITY WINDOW: Multiply damage by 2.0x if struck while weapon is stuck in ground!
        if (CurrentState == EnemyState.StuckInGround)
        {
            actualDamage *= vulnerabilityMultiplier;
            SpawnCritSparks(transform.position + Vector3.up * 1.5f);
            IsometricCameraFollow.Instance?.Shake(0.55f, 0.4f);
            campaign?.ShowMessage($"★ CHÍ MẠNG SƠ HỞ! -{Mathf.RoundToInt(actualDamage)} MÁU (x{vulnerabilityMultiplier:0.0} SÁT THƯƠNG) ★", 1.2f);
        }

        health -= actualDamage;
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + stunSeconds);
        flinchTimer = Mathf.Max(flinchTimer, isBoss ? 0.18f : 0.22f);
        transform.localScale = (isBoss ? initialScale * 1.8f : initialScale) * 1.05f;
        SpawnBladeSparks(transform.position + Vector3.up * (isBoss ? 1.45f : 1.0f));

        Vector3 source = hitSource != default ? hitSource : (target != null ? target.position : transform.position - transform.forward);

        if (health > 0f)
        {
            AnnounceBossPhaseIfNeeded();
            if (ragdoll != null)
            {
                // Less knockback while stuck so boss doesn't slide away from his stuck weapon
                float vulnerabilityDrag = CurrentState == EnemyState.StuckInGround ? 0.45f : 1f;
                float damageRatio = Mathf.Sqrt(Mathf.Clamp01(actualDamage / Mathf.Max(1f, maxHealth)));
                float baseKnockPower = isBoss ? 5.2f : 9.5f;
                float knockPower = baseKnockPower * Mathf.Clamp(impactScale, 0.35f, 2.1f) * Mathf.Lerp(0.85f, 1.55f, damageRatio) * vulnerabilityDrag;
                float lift = heavyImpact ? 1.65f : 1.18f;
                ragdoll.ApplyKnockback(source, transform.position - source, knockPower, lift);
            }
            return;
        }

        // Defeated!
        CurrentState = EnemyState.Dead;
        DestroyWarningRing();
        DestroyStuckBladeMarker();
        ResetVisualPose();
        campaign?.NotifyEnemyDefeated(this);

        if (ragdoll != null)
        {
            ragdoll.LaunchOnDeath(source, isBoss ? 14f : 24f, isBoss ? 3.0f : 5.0f);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

