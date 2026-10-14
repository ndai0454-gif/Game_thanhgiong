using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Moving brush strokes and a bounded particle budget, matching the Godot combat pass.
public sealed class GodotCombatFeedback : MonoBehaviour
{
    private int debrisCount;
    private Material effectMaterial;
    private readonly Dictionary<Renderer, Flash> flashes = new Dictionary<Renderer, Flash>();
    private readonly List<Renderer> expired = new List<Renderer>();
    private sealed class Flash { public MaterialPropertyBlock original; public float time; public Color color; }
    public int ImpactCount { get; private set; }
    public int DebrisCount => debrisCount;
    public int BloodBurstCount { get; private set; }
    public int ActiveBloodDropletCount { get; private set; }
    public int ActiveBloodBurstCount => bloodRoots.Count;
    public string LastBloodTargetName { get; private set; }
    public Vector3 LastBloodOrigin { get; private set; }
    private const int MaxBloodDroplets = 96;
    private const int DropletsPerBurst = 16;
    private readonly List<GameObject> bloodRoots = new List<GameObject>();
    private readonly Dictionary<Transform, float> bloodCooldowns = new Dictionary<Transform, float>();
    private readonly List<Transform> expiredBloodTargets = new List<Transform>();

    private void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null) effectMaterial = new Material(shader) { name = "Thánh Gióng transient brush" };
    }

    private void Update()
    {
        float delta = Time.deltaTime;
        expired.Clear();
        foreach (var entry in flashes) {
            Renderer renderer = entry.Key; Flash flash = entry.Value;
            if (renderer == null) { expired.Add(renderer); continue; }
            flash.time -= delta;
            if (flash.time <= 0) {
                renderer.SetPropertyBlock(flash.original); expired.Add(renderer); continue;
            }
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Color c = Color.Lerp(Color.white, flash.color, Mathf.Clamp01(flash.time / .14f));
            block.SetColor("_BaseColor", c); block.SetColor("_Color", c);
            renderer.SetPropertyBlock(block);
        }
        foreach (Renderer renderer in expired) flashes.Remove(renderer);
    }

    public void PlayAttack(bool bamboo, float radius)
    {
        if (effectMaterial == null) return;
        StartCoroutine(AttackStroke(bamboo, radius));
        if (bamboo) Debris(transform.position + Vector3.up * 1.2f, new Color(.43f, .64f, .12f), true, 5);
    }

    private IEnumerator AttackStroke(bool bamboo, float radius)
    {
        GameObject stroke = new GameObject("Godot Attack Trail");
        stroke.transform.SetParent(transform, false);
        LineRenderer line = stroke.AddComponent<LineRenderer>();
        line.sharedMaterial = effectMaterial; line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        line.positionCount = 18; line.widthMultiplier = bamboo ? .55f : .4f;
        line.widthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        Color color = bamboo ? new Color(.68f, 1f, .3f) : new Color(1f, .82f, .15f);
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 right = transform.right, forward = transform.forward;
        float age = 0;
        while (age < .36f) {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / .36f);
            float head = Mathf.Lerp(-65f, 95f, t);
            float tail = Mathf.Max(-65f, head - 65f);
            for (int i = 0; i < line.positionCount; i++) {
                float k = i / (float)(line.positionCount - 1);
                float a = Mathf.Lerp(tail, head, k) * Mathf.Deg2Rad;
                line.SetPosition(i, origin + (right * Mathf.Sin(a) + forward * Mathf.Cos(a)) * radius + Vector3.up * Mathf.Sin(k * Mathf.PI) * .4f);
            }
            line.startColor = new Color(color.r, color.g, color.b, 0);
            line.endColor = new Color(color.r, color.g, color.b, 1 - t);
            yield return null;
        }
        if (stroke != null) Destroy(stroke);
    }

    public void PlayImpact(Transform target, bool bamboo, bool hurt = false)
    {
        if (target == null || effectMaterial == null) return;
        ImpactCount++;
        Color color = hurt ? new Color(1, .24f, .13f) : (bamboo ? new Color(.63f, .76f, .23f) : new Color(1, .82f, .35f));
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>()) {
            if (!renderer.enabled || renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
            if (!flashes.TryGetValue(renderer, out Flash flash)) {
                flash = new Flash { original = new MaterialPropertyBlock() };
                renderer.GetPropertyBlock(flash.original); flashes.Add(renderer, flash);
            }
            flash.time = .14f; flash.color = color;
        }
        Debris(target.position + Vector3.up * 1.25f, color, bamboo, 7);
    }

    // Called only after an enemy actually takes mounted contact damage. Keeping this
    // separate from player hurt feedback prevents red spray on the rider or horse.
    public bool PlayMountedImpact(Transform target, Vector3 source, Vector3 travelDirection, float intensity = 1f)
    {
        if (!isActiveAndEnabled || target == null || target.root == transform.root || effectMaterial == null) return false;
        ThanhGiongEnemy enemy = target.GetComponentInParent<ThanhGiongEnemy>();
        if (enemy == null) return false;
        Transform enemyRoot = enemy.transform;
        if (bloodCooldowns.TryGetValue(enemyRoot, out float nextBurst) && Time.time < nextBurst) return false;
        if (ActiveBloodDropletCount + DropletsPerBurst > MaxBloodDroplets) return false;

        expiredBloodTargets.Clear();
        foreach (var entry in bloodCooldowns)
            if (entry.Key == null || Time.time >= entry.Value) expiredBloodTargets.Add(entry.Key);
        foreach (Transform expiredTarget in expiredBloodTargets) bloodCooldowns.Remove(expiredTarget);
        bloodCooldowns[enemyRoot] = Time.time + .18f;

        Vector3 direction = Vector3.ProjectOnPlane(travelDirection, Vector3.up).normalized;
        if (direction.sqrMagnitude < .01f) direction = Vector3.ProjectOnPlane(enemyRoot.position - source, Vector3.up).normalized;
        if (direction.sqrMagnitude < .01f) direction = transform.forward;
        Vector3 origin = enemyRoot.position + Vector3.up * .9f;
        Collider body = enemy.GetComponent<Collider>();
        if (body != null && body.enabled) origin = body.bounds.center;
        origin -= direction * .16f;
        BloodBurstCount++;
        ImpactCount++;
        LastBloodTargetName = enemyRoot.name;
        LastBloodOrigin = origin;
        StartCoroutine(BloodSpray(origin, direction, enemyRoot.position.y + .035f, Mathf.Clamp(intensity, .75f, 1.6f)));
        return true;
    }

    private IEnumerator BloodSpray(Vector3 origin, Vector3 direction, float groundY, float intensity)
    {
        // World-space ownership is deliberate: the spray stays at the collision
        // when the horse moves on, and never inherits the horse's scale or bob.
        GameObject root = new GameObject("Godot Mounted Blood Spray");
        bloodRoots.Add(root);
        Transform[] drops = new Transform[DropletsPerBurst];
        Renderer[] renderers = new Renderer[DropletsPerBurst];
        Vector3[] velocities = new Vector3[DropletsPerBurst];
        Vector3[] sizes = new Vector3[DropletsPerBurst];
        Color[] colors = new Color[DropletsPerBurst];
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < DropletsPerBurst; i++)
        {
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.name = "Blood Droplet";
            drop.transform.SetParent(root.transform, false);
            drop.transform.position = origin;
            Collider collider = drop.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            drops[i] = drop.transform;
            renderers[i] = drop.GetComponent<Renderer>();
            renderers[i].sharedMaterial = effectMaterial;
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
            velocities[i] = (direction * Random.Range(1.2f, 3.5f) + right * Random.Range(-2.2f, 2.2f) + Vector3.up * Random.Range(1.1f, 3.1f)) * intensity;
            float width = Random.Range(.065f, .13f);
            sizes[i] = new Vector3(width, width, width * Random.Range(1.5f, 2.8f));
            colors[i] = i % 3 == 0 ? new Color(.62f, .025f, .035f) : new Color(.95f, .055f, .065f);
            block.SetColor("_Color", colors[i]); block.SetColor("_BaseColor", colors[i]);
            renderers[i].SetPropertyBlock(block);
            drops[i].localScale = sizes[i];
        }
        ActiveBloodDropletCount += DropletsPerBurst;
        const float life = .95f;
        float age = 0;
        while (age < life)
        {
            age += Time.deltaTime;
            float opacity = 1f - Mathf.InverseLerp(.55f, life, age);
            for (int i = 0; i < DropletsPerBurst; i++)
            {
                if (drops[i] == null) continue;
                float fallTime = (velocities[i].y + Mathf.Sqrt(velocities[i].y * velocities[i].y + 19.6f * Mathf.Max(0f, origin.y - groundY))) / 9.8f;
                float flightAge = Mathf.Min(age, fallTime);
                Vector3 position = origin + velocities[i] * flightAge + Vector3.down * (4.9f * flightAge * flightAge);
                bool grounded = age >= fallTime;
                position.y = Mathf.Max(groundY, position.y);
                drops[i].position = position;
                Vector3 velocity = velocities[i] + Vector3.down * (9.8f * age);
                drops[i].rotation = grounded ? Quaternion.identity : Quaternion.LookRotation(velocity.normalized);
                drops[i].localScale = grounded ? new Vector3(sizes[i].x * 2.2f, .016f, sizes[i].x * 2.2f) : sizes[i];
                Color color = colors[i]; color.a = opacity;
                block.SetColor("_Color", color); block.SetColor("_BaseColor", color);
                renderers[i].SetPropertyBlock(block);
            }
            yield return null;
        }
        bloodRoots.Remove(root);
        if (root != null) Destroy(root);
        ActiveBloodDropletCount -= DropletsPerBurst;
    }

    private void Debris(Vector3 position, Color color, bool leaf, int count, float life = 0, float size = 1)
    {
        if (effectMaterial == null) return;
        count = Mathf.Min(count, Mathf.Max(0, 64 - debrisCount));
        for (int i = 0; i < count; i++) StartCoroutine(Piece(position, color, leaf, life, size));
    }

    private IEnumerator Piece(Vector3 position, Color color, bool leaf, float life, float size)
    {
        debrisCount++;
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = leaf ? "Godot Bamboo Leaf" : "Godot Contact Spark";
        piece.transform.SetParent(transform, true); piece.transform.position = position;
        Destroy(piece.GetComponent<Collider>());
        Renderer renderer = piece.GetComponent<Renderer>();
        renderer.sharedMaterial = effectMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
        MaterialPropertyBlock block = new MaterialPropertyBlock(); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
        Vector3 scale = size == 1 ? (leaf ? new Vector3(.14f, .018f, .055f) : new Vector3(.025f, .025f, .13f)) : Vector3.one * size;
        Vector3 velocity = new Vector3(Random.Range(-.8f, .8f), Random.Range(.2f, .9f), Random.Range(-.8f, .8f));
        life = life > 0 ? life : (leaf ? .6f : .24f);
        float age = 0;
        while (age < life) {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            piece.transform.position = position + velocity * (1 - (1 - t) * (1 - t));
            Vector3 parentScale = transform.lossyScale;
            piece.transform.localScale = new Vector3(scale.x / Mathf.Max(.01f, Mathf.Abs(parentScale.x)), scale.y / Mathf.Max(.01f, Mathf.Abs(parentScale.y)), scale.z / Mathf.Max(.01f, Mathf.Abs(parentScale.z))) * (1 - t);
            piece.transform.Rotate(new Vector3(1, 2, .5f) * Time.deltaTime * 90);
            yield return null;
        }
        if (piece != null) Destroy(piece);
        debrisCount--;
    }

    private void OnDisable()
    {
        foreach (var entry in flashes) if (entry.Key != null) entry.Key.SetPropertyBlock(entry.Value.original);
        flashes.Clear(); StopAllCoroutines();
        foreach (GameObject root in bloodRoots) if (root != null) Destroy(root);
        bloodRoots.Clear(); bloodCooldowns.Clear(); expiredBloodTargets.Clear();
        ActiveBloodDropletCount = 0;
        foreach (Transform child in transform) if (child.name.StartsWith("Godot ")) Destroy(child.gameObject);
        debrisCount = 0;
    }

    private void OnDestroy() { if (effectMaterial != null) Destroy(effectMaterial); }
}
