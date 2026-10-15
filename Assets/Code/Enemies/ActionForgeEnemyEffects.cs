using UnityEngine;

/// <summary>Small, reused movement/contact effects driven by the real animated rig.</summary>
[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public sealed class ActionForgeEnemyEffects : MonoBehaviour
{
    static Material fallbackSwipeMat;
    static Material fallbackParticleMat;

    ThanhGiongEnemy enemy;
    ThanhGiongEnemyAnimationDriver driver;
    Animator bound;
    Transform leftFoot, rightFoot;
    TrailRenderer swipe;
    TrailRenderer offhandSwipe;
    ParticleSystem dust, contact;
    float leftY, rightY, leftAt, rightAt;
    bool windupBurstDone;
    bool contactDone;
    bool stateObserved;
    ThanhGiongEnemy.EnemyState previous;
    public int StepBursts { get; private set; }
    public int ContactBursts { get; private set; }
    public bool TrailEmitting => swipe != null && swipe.emitting;

    void Awake()
    {
        enemy = GetComponent<ThanhGiongEnemy>();
        driver = GetComponent<ThanhGiongEnemyAnimationDriver>();
    }

    static Material GetSwipeMaterial()
    {
        var mat = Resources.Load<Material>("ActionForge/EnemyEffects/Swipe");
        if (mat != null) return mat;
        if (fallbackSwipeMat == null)
        {
            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (s != null) fallbackSwipeMat = new Material(s) { name = "ActionForgeSwipeFallback" };
        }
        return fallbackSwipeMat;
    }

    static Material GetParticleMaterial()
    {
        var mat = Resources.Load<Material>("ActionForge/EnemyEffects/SoftParticle");
        if (mat != null) return mat;
        if (fallbackParticleMat == null)
        {
            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (s != null) fallbackParticleMat = new Material(s) { name = "ActionForgeParticleFallback" };
        }
        return fallbackParticleMat;
    }

    static Transform Bone(Animator animator, string name)
    {
        foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    Color ResolveTrailStartColor()
    {
        if (enemy == null) return new Color(1f, 0.65f, 0.24f, 0.92f);
        if (enemy.isBoss) return new Color(1f, 0.34f, 0.12f, 0.96f);
        switch (enemy.role)
        {
            case ThanhGiongEnemy.EnemyRole.Spearman: return new Color(1f, 0.56f, 0.16f, 0.94f);
            case ThanhGiongEnemy.EnemyRole.ShieldBearer: return new Color(1f, 0.78f, 0.24f, 0.94f);
            case ThanhGiongEnemy.EnemyRole.Archer: return new Color(1f, 0.86f, 0.32f, 0.92f);
            case ThanhGiongEnemy.EnemyRole.Raider: return new Color(1f, 0.28f, 0.16f, 0.95f);
            default: return new Color(1f, 0.65f, 0.24f, 0.92f);
        }
    }

    static Vector3 FindFarthestTip(Transform hand, Vector3 defaultTip)
    {
        Transform authoredTip = hand.Find("ActionForge Weapon Tip");
        if (authoredTip != null) return hand.InverseTransformPoint(authoredTip.position);
        Vector3 tip = defaultTip;
        float farthest = 0f;
        foreach (var filter in hand.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 p = hand.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
                        if (p.sqrMagnitude > farthest) { farthest = p.sqrMagnitude; tip = p; }
                    }
        }
        return tip;
    }

    TrailRenderer CreateHandTrail(Transform hand, string objectName, Vector3 tip, float startWidth, Color startColor, Color endColor)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(hand, false);
        go.transform.localPosition = tip;
        var tr = go.AddComponent<TrailRenderer>();
        tr.sharedMaterial = GetSwipeMaterial();
        tr.time = 0.20f;
        tr.minVertexDistance = 0.025f;
        tr.numCornerVertices = 4;
        tr.numCapVertices = 4;
        tr.startWidth = startWidth;
        tr.endWidth = 0.015f;
        tr.startColor = startColor;
        tr.endColor = endColor;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.emitting = false;
        return tr;
    }

    void Bind(Animator animator)
    {
        if (swipe != null) Destroy(swipe.gameObject);
        if (offhandSwipe != null) Destroy(offhandSwipe.gameObject);
        bound = animator;
        leftFoot = Bone(animator, "foot.l") ?? Bone(animator, "foot_l") ?? Bone(animator, "LeftFoot");
        rightFoot = Bone(animator, "foot.r") ?? Bone(animator, "foot_r") ?? Bone(animator, "RightFoot");
        Transform handR = Bone(animator, "handslot.r") ?? Bone(animator, "hand.r") ?? Bone(animator, "hand_r") ?? Bone(animator, "RightHand");
        Color startCol = ResolveTrailStartColor();
        Color endCol = new Color(1f, 0.22f, 0.06f, 0f);
        if (handR != null)
        {
            Vector3 tip = FindFarthestTip(handR, Vector3.up * 0.58f);
            swipe = CreateHandTrail(handR, "ActionForge Enemy Weapon Trail", tip, enemy != null && enemy.isBoss ? 0.42f : 0.22f, startCol, endCol);
        }

        if (enemy != null && (enemy.role == ThanhGiongEnemy.EnemyRole.ShieldBearer || enemy.isBoss))
        {
            Transform handL = Bone(animator, "handslot.l") ?? Bone(animator, "hand.l") ?? Bone(animator, "hand_l") ?? Bone(animator, "LeftHand");
            if (handL != null)
            {
                Vector3 leftTip = FindFarthestTip(handL, Vector3.forward * 0.35f);
                offhandSwipe = CreateHandTrail(
                    handL,
                    "ActionForge Enemy Offhand Trail",
                    leftTip,
                    enemy.isBoss ? 0.26f : 0.18f,
                    new Color(1f, 0.82f, 0.32f, 0.85f),
                    endCol);
            }
        }

        if (dust == null) dust = Particles("ActionForge Enemy Foot Dust", false);
        if (contact == null) contact = Particles("ActionForge Enemy Contact", true);
        leftY = leftFoot != null ? leftFoot.position.y : 0f;
        rightY = rightFoot != null ? rightFoot.position.y : 0f;
    }

    ParticleSystem Particles(string name, bool sparks)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var particles = go.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false; main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64; main.startLifetime = sparks ? 0.26f : 0.42f;
        main.startSpeed = sparks ? 2.2f : 0.35f;
        main.startSize = sparks ? 0.095f : 0.20f;
        main.gravityModifier = sparks ? 0.35f : -0.08f;
        main.startColor = sparks ? new Color(1f, 0.78f, 0.28f, 0.92f) : new Color(0.68f, 0.59f, 0.45f, 0.36f);
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = sparks ? 0.16f : 0.12f;
        var color = particles.colorOverLifetime; color.enabled = true;
        color.color = new Gradient { colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.42f, 0.12f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) } };
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.75f, 1f, sparks ? 0.12f : 1.55f));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetParticleMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    void Step(Transform foot, ref float previousY, ref float nextAt)
    {
        if (foot == null) return;
        float y = foot.position.y;
        float lowest = Mathf.Min(leftFoot != null ? leftFoot.position.y : y, rightFoot != null ? rightFoot.position.y : y);
        if (Time.time >= nextAt && y < previousY - 0.0002f && y <= lowest + 0.025f)
        {
            var emit = new ParticleSystem.EmitParams { position = foot.position + Vector3.up * 0.025f };
            dust.Emit(emit, enemy.isBoss ? 6 : 4);
            StepBursts++; nextAt = Time.time + 0.26f;
        }
        previousY = y;
    }

    void LateUpdate()
    {
        if (enemy == null || driver == null || !driver.UsesActionForge || Time.deltaTime <= 0f) return;
        var animator = driver.BoundAnimator;
        if (animator == null || !animator.gameObject.activeInHierarchy) return;
        if (bound != animator) Bind(animator);
        var state = enemy.CurrentState;
        if (state != previous)
        {
            // A missed boss slam enters Stuck immediately when its .22-second hit wait
            // completes. Coroutines resume before LateUpdate, so there need not be a
            // rendered Slamming frame whose phase has reached the contact threshold.
            // Complete that contact once when the attack proceeds normally; interrupted
            // attacks (Stunned/Dead) and battle resets (Idle) must never create a hit.
            if (stateObserved && previous == ThanhGiongEnemy.EnemyState.Slamming &&
                (state == ThanhGiongEnemy.EnemyState.StuckInGround || state == ThanhGiongEnemy.EnemyState.Recovering ||
                 state == ThanhGiongEnemy.EnemyState.Chasing))
                EmitContact();
            windupBurstDone = false;
            contactDone = false;
            previous = state;
        }
        stateObserved = true;
        float phase = enemy.AnimationStatePhase;
        float hit = enemy.AnimationContactPhase;
        bool telegraphing = state == ThanhGiongEnemy.EnemyState.TelegraphingAttack;
        bool striking = state == ThanhGiongEnemy.EnemyState.Slamming;
        bool emitTrail = (telegraphing && phase >= 0.68f) || (striking && phase <= Mathf.Min(0.95f, hit + 0.50f));
        if (swipe != null)
            swipe.emitting = emitTrail;
        if (offhandSwipe != null)
            offhandSwipe.emitting = emitTrail;

        if (telegraphing && phase >= 0.15f && !windupBurstDone && contact != null)
        {
            windupBurstDone = true;
            Vector3 glintPos = swipe != null ? swipe.transform.position : transform.position + Vector3.up * 1.35f + transform.forward * 0.45f;
            var emit = new ParticleSystem.EmitParams
            {
                position = glintPos,
                startColor = ResolveTrailStartColor(),
                startSize = enemy.isBoss ? 0.14f : 0.09f
            };
            contact.Emit(emit, enemy.isBoss ? 10 : 6);
        }

        if (striking && phase >= hit) EmitContact();
        if (state == ThanhGiongEnemy.EnemyState.Chasing && enemy.Body != null &&
            Vector3.ProjectOnPlane(enemy.Body.linearVelocity, Vector3.up).sqrMagnitude > 0.4f && Mathf.Abs(enemy.Body.linearVelocity.y) < 2f)
        {
            Step(leftFoot, ref leftY, ref leftAt); Step(rightFoot, ref rightY, ref rightAt);
        }
    }

    void EmitContact()
    {
        if (contactDone || contact == null) return;
        contactDone = true;
        var emit = new ParticleSystem.EmitParams
        {
            position = swipe != null ? swipe.transform.position : transform.position + transform.forward + Vector3.up * 0.9f,
            startColor = ResolveTrailStartColor(),
            startSize = enemy.isBoss ? 0.16f : 0.11f
        };
        contact.Emit(emit, enemy.isBoss ? 18 : 10);
        ContactBursts++;
    }

    void OnDisable()
    {
        stateObserved = false;
        contactDone = false;
        windupBurstDone = false;
        if (swipe != null) { swipe.emitting = false; swipe.Clear(); }
        if (offhandSwipe != null) { offhandSwipe.emitting = false; offhandSwipe.Clear(); }
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (contact != null) contact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
