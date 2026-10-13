using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives ActionForge clips from physical movement and the combat coroutine clock.
/// Legacy controllers still use their existing triggers and bool parameters.
/// </summary>
[DefaultExecutionOrder(50)]
public class ThanhGiongEnemyAnimationDriver : MonoBehaviour
{
    public float speedBlendTime = .12f;
    public float stateBlendTime = .1f;
    public ActionForgeEnemyMotionSet actionForgeMotions;

    public Animator BoundAnimator => animator;
    public bool UsesActionForge => animator != null && animator.isActiveAndEnabled && hasState && hasPhase;

    private ThanhGiongEnemy enemy;
    private Animator animator;
    private Rigidbody body;
    private RuntimeAnimatorController boundController;
    private Avatar originalAvatar;
    private ActionForgeEnemyMotionSet selectedSet;
    private ThanhGiongEnemy.EnemyRole selectedRole;
    private bool selectedBoss;
    private bool hasSpeed, hasMoveRate, hasState, hasPhase;
    private bool hasAttack, hasWindup, hasStrike, hasRecovery, hasHit, hasDead, hasStuck;
    private bool stateInitialized;
    private bool parametersCached;
    private bool fallbackChecked;
    private bool hierarchyDirty = true;
    private float nextBindingCheck;
    private ThanhGiongEnemy.EnemyState previousState;
    private readonly List<Animator> candidates = new List<Animator>(4);
    private readonly List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>(2);
    private ActionForgeEnemyEffects effects;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveRateHash = Animator.StringToHash("MoveRate");
    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int PhaseHash = Animator.StringToHash("Phase");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int WindupHash = Animator.StringToHash("Windup");
    private static readonly int StrikeHash = Animator.StringToHash("Strike");
    private static readonly int RecoveryHash = Animator.StringToHash("Recovery");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");
    private static readonly int StuckHash = Animator.StringToHash("Stuck");

    private void Awake()
    {
        enemy = GetComponent<ThanhGiongEnemy>();
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        hierarchyDirty = true;
        stateInitialized = false;
        RebindAnimator();
    }

    private void OnTransformChildrenChanged() => hierarchyDirty = true;

    /// <summary>Also safe after an enemy visual is replaced or added after Awake.</summary>
    public void RebindAnimator()
    {
        if (enemy == null) enemy = GetComponent<ThanhGiongEnemy>();
        if (body == null) body = GetComponent<Rigidbody>();
        GetComponentsInChildren(false, candidates);
        Animator best = null;
        int bestScore = -1;
        foreach (Animator candidate in candidates)
        {
            if (!candidate.isActiveAndEnabled) continue;
            candidate.GetComponentsInChildren(false, skins);
            int score = skins.Count > 0 ? 20 : 0;
            if (candidate.runtimeAnimatorController != null) score += 5;
            if (candidate == animator) score++;
            if (score <= bestScore) continue;
            best = candidate;
            bestScore = score;
        }
        if (animator != best)
        {
            if (animator != null) animator.speed = 1f;
            animator = best;
            boundController = null;
            parametersCached = false;
            fallbackChecked = false;
            originalAvatar = animator != null ? animator.avatar : null;
            selectedSet = null;
            stateInitialized = false;
        }
        hierarchyDirty = false;
        nextBindingCheck = Time.unscaledTime + .75f;
        if (animator == null || enemy == null) return;
        ApplyMotionSet();
        CacheParameters();
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        // Combat phase uses the coroutine clock. Returning to view must never resume
        // a stale windup which already dealt its damage off screen.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (UsesActionForge && effects == null)
        {
            effects = GetComponent<ActionForgeEnemyEffects>();
            if (effects == null) effects = gameObject.AddComponent<ActionForgeEnemyEffects>();
        }
    }

    private void ApplyMotionSet()
    {
        if (actionForgeMotions == null && selectedSet == null && !fallbackChecked)
        {
            fallbackChecked = true;
            string skeletonName = animator.avatar != null ? animator.avatar.name : animator.name;
            string family = skeletonName.IndexOf("Knight", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                animator.name.IndexOf("Knight", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Knight" : "Barbarian";
            selectedSet = Resources.Load<ActionForgeEnemyMotionSet>("ActionForge/EnemyMotionSets/" + family);
        }
        ActionForgeEnemyMotionSet set = actionForgeMotions != null ? actionForgeMotions : selectedSet;
        ThanhGiongEnemy.EnemyRole role = enemy.ResolvedRole;
        if (set == null) return;
        RuntimeAnimatorController controller = set.For(role, enemy.isBoss);
        if (controller != null && (selectedSet != set || selectedRole != role || selectedBoss != enemy.isBoss ||
                                   animator.runtimeAnimatorController != controller))
        {
            if (animator.runtimeAnimatorController != controller) animator.runtimeAnimatorController = controller;
            selectedSet = set;
            selectedRole = role;
            selectedBoss = enemy.isBoss;
            stateInitialized = false;
        }
    }

    private void CacheParameters()
    {
        if (parametersCached && boundController == animator.runtimeAnimatorController) return;
        parametersCached = true;
        boundController = animator.runtimeAnimatorController;
        hasSpeed = hasMoveRate = hasState = hasPhase = false;
        hasAttack = hasWindup = hasStrike = hasRecovery = hasHit = hasDead = hasStuck = false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            else if (p.nameHash == MoveRateHash && p.type == AnimatorControllerParameterType.Float) hasMoveRate = true;
            else if (p.nameHash == StateHash && p.type == AnimatorControllerParameterType.Int) hasState = true;
            else if (p.nameHash == PhaseHash && p.type == AnimatorControllerParameterType.Float) hasPhase = true;
            else if (p.nameHash == AttackHash && p.type == AnimatorControllerParameterType.Trigger) hasAttack = true;
            else if (p.nameHash == HitHash && p.type == AnimatorControllerParameterType.Trigger) hasHit = true;
            else if (p.nameHash == DeadHash && p.type == AnimatorControllerParameterType.Trigger) hasDead = true;
            else if (p.nameHash == StrikeHash && p.type == AnimatorControllerParameterType.Trigger) hasStrike = true;
            else if (p.nameHash == StuckHash && p.type == AnimatorControllerParameterType.Bool) hasStuck = true;
            else if (p.nameHash == WindupHash && p.type == AnimatorControllerParameterType.Bool) hasWindup = true;
            else if (p.nameHash == RecoveryHash && p.type == AnimatorControllerParameterType.Bool) hasRecovery = true;
        }
        // Exact-path Generic curves already contain the correct rig-space pose;
        // Humanoid retargeting would otherwise override those rotations.
        if (hasState && hasPhase) animator.avatar = null;
        else if (originalAvatar != null && animator.avatar == null) animator.avatar = originalAvatar;
        stateInitialized = false;
    }

    private void Update()
    {
        if (enemy == null) return;
        if (hierarchyDirty || animator == null || !animator.isActiveAndEnabled || Time.unscaledTime >= nextBindingCheck)
            RebindAnimator();
        if (animator == null || !animator.isActiveAndEnabled) return;
        ApplyMotionSet();
        CacheParameters();
        animator.applyRootMotion = false;

        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        float normalized = Mathf.Clamp(velocity.magnitude / Mathf.Max(enemy.moveSpeed, .01f), 0f, 2f);
        ThanhGiongEnemy.EnemyState state = enemy.CurrentState;
        float move = state == ThanhGiongEnemy.EnemyState.Chasing ? normalized : 0f;
        if (hasSpeed) animator.SetFloat(SpeedHash, Mathf.Clamp01(move), Mathf.Max(.001f, speedBlendTime), Time.deltaTime);
        if (hasMoveRate) animator.SetFloat(MoveRateHash, Mathf.Clamp(move, .35f, 1.8f));

        if (UsesActionForge)
        {
            animator.speed = 1f;
            animator.SetInteger(StateHash, (int)state);
            animator.SetFloat(PhaseHash, enemy.AnimationStatePhase);
        }
        else
        {
            if (!stateInitialized || state != previousState)
            {
                if (state == ThanhGiongEnemy.EnemyState.TelegraphingAttack && hasAttack && !hasStrike) animator.SetTrigger(AttackHash);
                if (state == ThanhGiongEnemy.EnemyState.Slamming && hasStrike) animator.SetTrigger(StrikeHash);
                if (state == ThanhGiongEnemy.EnemyState.Stunned && hasHit) animator.SetTrigger(HitHash);
                if (state == ThanhGiongEnemy.EnemyState.Dead && hasDead) animator.SetTrigger(DeadHash);
            }
            if (hasStuck) animator.SetBool(StuckHash, state == ThanhGiongEnemy.EnemyState.StuckInGround);
            if (hasWindup) animator.SetBool(WindupHash, state == ThanhGiongEnemy.EnemyState.TelegraphingAttack);
            if (hasRecovery) animator.SetBool(RecoveryHash, state == ThanhGiongEnemy.EnemyState.Recovering);
            float targetPlayback = state == ThanhGiongEnemy.EnemyState.StuckInGround ? .38f : 1f;
            animator.speed = Mathf.MoveTowards(animator.speed, targetPlayback, Time.deltaTime / Mathf.Max(.001f, stateBlendTime));
        }
        previousState = state;
        stateInitialized = true;
    }

    private void OnDisable()
    {
        if (animator != null) animator.speed = 1f;
        stateInitialized = false;
    }
}
