using UnityEngine;

/// <summary>Small, reused movement/contact effects driven by the real animated rig.</summary>
[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public sealed class ActionForgeEnemyEffects : MonoBehaviour
{
    ThanhGiongEnemy enemy;
    ThanhGiongEnemyAnimationDriver driver;
    Animator bound;
    Transform leftFoot, rightFoot;
    TrailRenderer swipe;
    ParticleSystem dust, contact;
    float leftY, rightY, leftAt, rightAt;
    bool contactDone;
    ThanhGiongEnemy.EnemyState previous;
    public int StepBursts { get; private set; }
    public int ContactBursts { get; private set; }
    public bool TrailEmitting => swipe != null && swipe.emitting;

    void Awake()
    {
        enemy = GetComponent<ThanhGiongEnemy>();
        driver = GetComponent<ThanhGiongEnemyAnimationDriver>();
    }

    static Transform Bone(Animator animator, string name)
    {
        foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    void Bind(Animator animator)
    {
        if (swipe != null) Destroy(swipe.gameObject);
        bound = animator;
        leftFoot = Bone(animator, "foot.l"); rightFoot = Bone(animator, "foot.r");
        Transform hand = Bone(animator, "handslot.r") ?? Bone(animator, "hand.r");
        if (hand != null)
        {
            Vector3 tip = Vector3.up * .45f;
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
            var go = new GameObject("ActionForge Enemy Weapon Trail");
            go.transform.SetParent(hand, false); go.transform.localPosition = tip;
            swipe = go.AddComponent<TrailRenderer>();
            swipe.sharedMaterial = Resources.Load<Material>("ActionForge/EnemyEffects/Swipe");
            swipe.time = .13f; swipe.minVertexDistance = .035f;
            swipe.startWidth = enemy.isBoss ? .24f : .11f; swipe.endWidth = .01f;
            swipe.startColor = new Color(1f, .65f, .24f, .72f);
            swipe.endColor = new Color(1f, .25f, .08f, 0f);
            swipe.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            swipe.receiveShadows = false; swipe.emitting = false;
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
        main.maxParticles = 32; main.startLifetime = sparks ? .2f : .38f;
        main.startSpeed = sparks ? .75f : .25f;
        main.startSize = sparks ? .045f : .16f;
        main.gravityModifier = sparks ? .25f : -.08f;
        main.startColor = sparks ? new Color(1f, .72f, .24f, .7f) : new Color(.64f, .57f, .43f, .28f);
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .1f;
        var color = particles.colorOverLifetime; color.enabled = true;
        color.color = new Gradient { colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) } };
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .5f, 1f, sparks ? .1f : 1.5f));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("ActionForge/EnemyEffects/SoftParticle");
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
        if (Time.time >= nextAt && y < previousY - .0002f && y <= lowest + .025f)
        {
            var emit = new ParticleSystem.EmitParams { position = foot.position + Vector3.up * .025f };
            dust.Emit(emit, enemy.isBoss ? 5 : 3);
            StepBursts++; nextAt = Time.time + .28f;
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
        if (state != previous) { contactDone = false; previous = state; }
        float phase = enemy.AnimationStatePhase;
        float hit = enemy.AnimationContactPhase;
        bool striking = state == ThanhGiongEnemy.EnemyState.Slamming;
        if (swipe != null)
            swipe.emitting = striking && phase >= Mathf.Max(0f, hit - .12f) && phase <= hit + .24f;
        if (striking && phase >= hit && !contactDone)
        {
            contactDone = true;
            var emit = new ParticleSystem.EmitParams { position = swipe != null ? swipe.transform.position : transform.position + transform.forward };
            contact.Emit(emit, enemy.isBoss ? 9 : 4); ContactBursts++;
        }
        if (state == ThanhGiongEnemy.EnemyState.Chasing && enemy.Body != null &&
            Vector3.ProjectOnPlane(enemy.Body.linearVelocity, Vector3.up).sqrMagnitude > .4f && Mathf.Abs(enemy.Body.linearVelocity.y) < 2f)
        {
            Step(leftFoot, ref leftY, ref leftAt); Step(rightFoot, ref rightY, ref rightAt);
        }
    }

    void OnDisable()
    {
        if (swipe != null) { swipe.emitting = false; swipe.Clear(); }
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (contact != null) contact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
