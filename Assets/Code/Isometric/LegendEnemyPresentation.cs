using System.Collections.Generic;
using UnityEngine;

/// <summary>Art-only palette adapter. Original enemy, physical ragdoll and attack state retain ownership.</summary>
[DefaultExecutionOrder(-120)]
[DisallowMultipleComponent]
public sealed class LegendEnemyPresentation : MonoBehaviour
{
    public Transform model;
    public string sourceAsset;
    public Renderer[] placeholders = new Renderer[0];
    public bool preserveAuthoredMaterials;
    // The Meshy prefab is grounded from sampled idle vertices, not conservative FBX bounds.
    public bool modelOriginIsFeet;
    public bool IsUsingAuthoredModel => model != null && model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
    public bool HasNativeArt => IsUsingAuthoredModel;
    public int VisibleSkinCount { get { int count = 0; if (model != null) foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy) count++; return count; } }
    readonly List<Material> generated = new List<Material>();
    readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
    readonly RaycastHit[] groundHits = new RaycastHit[48];
    ThanhGiongEnemy enemy;
    CapsuleCollider bodyCollider;
    Vector3 restModelPosition, restFeetInRoot;
    float groundY, targetWorldCorrection, currentWorldCorrection, nextGroundProbe, airborneUntil;
    bool feetCached, hasSupport;
    /// <summary>Rest-sole gap from supported terrain; excludes the retained gait/telegraph bob.</summary>
    public float GroundGap => hasSupport && feetCached ? transform.TransformPoint(restFeetInRoot).y + currentWorldCorrection - groundY : float.NaN;
    public bool HasGroundSupport => hasSupport;
    public bool RefreshGroundSupportNow()
    {
        bool found = ProbeGround();
        if (found)
        {
            currentWorldCorrection = targetWorldCorrection;
            ApplyGroundCorrection();
        }
        return found;
    }

    void Awake()
    {
        HidePlaceholders();
        if (model == null) return;
        enemy = GetComponent<ThanhGiongEnemy>();
        bodyCollider = GetComponent<CapsuleCollider>();
        CacheFeet();
        if (preserveAuthoredMaterials) return;
        Shader shader = Shader.Find("ThanhGiong/LegendSurface");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is LineRenderer || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>() != null) continue;
            Material[] source = renderer.sharedMaterials, replacement = new Material[source.Length];
            originals[renderer] = source;
            string name = renderer.name.ToLowerInvariant();
            Color tint = name.Contains("maroon cloth") ? new Color(.43f, .16f, .18f) : name.Contains("shield") ? new Color(.84f, .73f, .51f) : name.Contains("blade") || name.Contains("sword") || name.Contains("axe") ? new Color(.78f, .80f, .74f) : new Color(.93f, .87f, .81f);
            for (int i = 0; i < source.Length; i++)
            {
                Material old = source[i], material = new Material(shader) { name = "Painted Invader " + (old != null ? old.name : name) };
                Color color = Color.white;
                if (old != null)
                {
                    foreach (string key in new[] { "baseColorFactor", "_BaseColor", "_Color" }) if (old.HasProperty(key)) { color = old.GetColor(key); break; }
                    foreach (string key in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (old.HasProperty(key) && old.GetTexture(key) != null)
                    { material.SetTexture("_BaseMap", old.GetTexture(key)); material.SetTextureScale("_BaseMap", old.GetTextureScale(key)); material.SetTextureOffset("_BaseMap", old.GetTextureOffset(key)); break; }
                }
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color * tint);
                if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 0);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (material.HasProperty("_UseVertexColors")) material.SetFloat("_UseVertexColors", mesh != null && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color) ? 1 : 0);
                generated.Add(material); replacement[i] = material;
            }
            renderer.sharedMaterials = replacement;
        }
    }
    void OnEnable() { HidePlaceholders(); }
    void Start() { RefreshGroundSupportNow(); }
    void CacheFeet()
    {
        if (model == null) return;
        restModelPosition = model.localPosition;
        if (modelOriginIsFeet)
        {
            restFeetInRoot = transform.InverseTransformPoint(model.position);
            feetCached = true;
            return;
        }
        bool found = false; Bounds bodyBounds = new Bounds();
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!found) { bodyBounds = renderer.bounds; found = true; } else bodyBounds.Encapsulate(renderer.bounds);
        }
        if (!found) return;
        restFeetInRoot = transform.InverseTransformPoint(new Vector3(transform.position.x, bodyBounds.min.y, transform.position.z));
        feetCached = true;
    }
    void LateUpdate()
    {
        if (Time.deltaTime <= 0f || model == null || enemy == null || !feetCached || enemy.CurrentState == ThanhGiongEnemy.EnemyState.Dead || enemy.HealthRatio <= 0) return;
        Rigidbody body = enemy.Body;
        // Let the original impact/airborne ragdoll travel freely rather than pulling its visible model to the floor.
        if (body != null && (body.linearVelocity.y > .5f || body.angularVelocity.x * body.angularVelocity.x + body.angularVelocity.z * body.angularVelocity.z > 1f)) airborneUntil = Time.time + .5f;
        if (Time.time < airborneUntil || enemy.IsStunned) return;
        if (Time.time >= nextGroundProbe) { nextGroundProbe = Time.time + .125f; ProbeGround(); }
        if (!hasSupport) return;
        currentWorldCorrection = Mathf.Lerp(currentWorldCorrection, targetWorldCorrection, 1f - Mathf.Exp(-16f * Time.deltaTime));
        ApplyGroundCorrection();
    }
    bool ProbeGround()
    {
        if (!feetCached || enemy == null) return false;
        Vector3 origin = transform.position + Vector3.up * 4f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 16f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        float ceiling = bodyCollider != null ? bodyCollider.bounds.min.y + .25f : transform.position.y + .25f;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.normal.y < .65f || hit.point.y > ceiling || hit.distance >= nearest ||
                hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<ThanhGiongEnemy>() != null ||
                hit.collider.GetComponentInParent<MountedHorseController>() != null) continue;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
            nearest = hit.distance; groundY = hit.point.y; found = true;
        }
        hasSupport = found;
        if (found) targetWorldCorrection = groundY + .03f - transform.TransformPoint(restFeetInRoot).y;
        else if (bodyCollider != null)
        {
            groundY = bodyCollider.bounds.min.y - .03f;
            targetWorldCorrection = groundY + .03f - transform.TransformPoint(restFeetInRoot).y;
            hasSupport = true;
            return true;
        }
        return found;
    }
    void ApplyGroundCorrection()
    {
        if (!feetCached || model == null || model.parent == null || !hasSupport) return;
        // Change only the art child. Parent Visual retains Enemy's authored breath, step bob and telegraphs.
        float worldYPerLocalUnit = Vector3.Dot(model.parent.TransformVector(Vector3.up), Vector3.up);
        if (Mathf.Abs(worldYPerLocalUnit) < .05f) return;
        model.localPosition = restModelPosition + Vector3.up * (currentWorldCorrection / worldYPerLocalUnit);
    }
    void HidePlaceholders() { foreach (Renderer renderer in placeholders) if (renderer != null) renderer.enabled = false; }
    void OnDestroy()
    {
        foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        foreach (Material material in generated) if (material != null) Destroy(material);
    }
}
