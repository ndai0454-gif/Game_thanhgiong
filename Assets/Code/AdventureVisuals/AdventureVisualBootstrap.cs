using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Presentation only: runtime material copies, bounded particles and a gentle grading layer.
/// Never changes authored assets, enemy stats, animation, colliders or the golden death dissolve.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(250)]
public sealed class AdventureVisualBootstrap : MonoBehaviour
{
    public bool characterCelRim = true;
    public bool interactiveFoliage = true;
    public bool ambientParticles = true;
    public bool warmAtmosphere = true;
    [Range(0, 1)] public float gradingWeight = .65f;
    public Shader celShader;
    public Shader foliageShader;
    public Shader particleShader;
    public int StyledActorRenderers { get; private set; }
    public int InteractiveFoliageRenderers { get; private set; }
    public int ResourceGlowCount => glows.Count;
    public int FootstepBursts { get; private set; }
    public bool IsBuilt { get; private set; }
    public ParticleSystem AmbientLeaves => leaves;
    public Volume AtmosphereVolume => volume;
    public int ActiveFootstepParticles => dust ? dust.particleCount : 0;
    public Vector3 TramplePosition => Shader.GetGlobalVector("_AdventurePlayerPosition");

    readonly Dictionary<Renderer, Material[]> originals = new();
    readonly Dictionary<Material, Material> actorMaterials = new();
    readonly Dictionary<Material, Material> grassMaterials = new();
    readonly List<Material> ownedMaterials = new();
    readonly HashSet<Renderer> visited = new();
    readonly List<ResourceGlow> glows = new();
    readonly RaycastHit[] groundHits = new RaycastHit[32];
    readonly List<Transform> feet = new();
    readonly List<Texture2D> textures = new();
    MountedHorseController movement;
    GodotMountedMotion mountMotion;
    Transform player;
    GameObject generated;
    ParticleSystem dust, leaves;
    Volume volume;
    VolumeProfile profile;
    Material dustMaterial, leafMaterial, sparkMaterial;
    Vector3 lastPlayerPosition;
    float stepDistance, scanAt, leafFollowAt;
    int footIndex;
    int observedHoofContacts;
    bool fortress, changedFog, oldFog;
    FogMode oldFogMode;
    Color oldFogColor;
    float oldFogStart, oldFogEnd;
    sealed class ResourceGlow { public ThanhGiongCollectible item; public ParticleSystem particles; }

    public static AdventureVisualBootstrap EnsureInstalled(GameObject root = null)
    {
        var existing = FindAnyObjectByType<AdventureVisualBootstrap>();
        if (existing) return existing;
        return (root ? root : new GameObject("Adventure Visuals")).AddComponent<AdventureVisualBootstrap>();
    }

    IEnumerator BuildPresentation()
    {
        // Existing environment builders complete their Start before we capture their materials.
        yield return null;
        movement = FindAnyObjectByType<MountedHorseController>();
        if (!movement) yield break;
        player = movement.transform;
        mountMotion = movement.GetComponent<GodotMountedMotion>();
        observedHoofContacts = mountMotion ? mountMotion.HoofContactCount : 0;
        fortress = gameObject.scene.name.Contains("PhaoDai");
        celShader = celShader ? celShader : Shader.Find("ThanhGiong/Adventure/Cel Rim Lit");
        foliageShader = foliageShader ? foliageShader : Shader.Find("ThanhGiong/Adventure/Interactive Foliage");
        particleShader = particleShader ? particleShader : Shader.Find("Universal Render Pipeline/Particles/Unlit");
        generated = new GameObject("Adventure Runtime Presentation");
        generated.transform.SetParent(transform, false);
        generated.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        generated.transform.localScale = Vector3.one;
        ScanScene();
        if (particleShader)
        {
            dustMaterial = ParticleMaterial("Adventure Soft Dust", SoftTexture(false));
            leafMaterial = ParticleMaterial("Adventure Bamboo Leaf", SoftTexture(true));
            sparkMaterial = ParticleMaterial("Adventure Resource Light", SoftTexture(false));
            BuildFootsteps();
            if (ambientParticles) BuildLeaves();
            BuildResourceGlows();
        }
        if (warmAtmosphere) BuildAtmosphere();
        lastPlayerPosition = player.position;
        IsBuilt = true;
    }

    void Reset()
    {
        celShader = Shader.Find("ThanhGiong/Adventure/Cel Rim Lit");
        foliageShader = Shader.Find("ThanhGiong/Adventure/Interactive Foliage");
        particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
    }

    void ScanScene()
    {
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer.gameObject.scene != gameObject.scene || visited.Contains(renderer)) continue;
            if (renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>()) continue;
            bool actor = renderer.GetComponentInParent<MountedHorseController>() || renderer.GetComponentInParent<ThanhGiongEnemy>();
            Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh) continue;
            string label = renderer.name.ToLowerInvariant();
            bool blade = label.Contains("meadow / moss") || label.Contains("bank reeds");
            Material[] sources = renderer.sharedMaterials;
            bool foliage = blade;
            foreach (var source in sources)
                if (source && source.shader && source.shader.name == "ThanhGiong/StylizedFoliageWind") foliage = true;
            if ((!actor || !characterCelRim || !celShader) && (!foliage || !interactiveFoliage || !foliageShader)) continue;
            Material[] replacements = (Material[])sources.Clone();
            bool changed = false;
            for (int i = 0; i < sources.Length; i++)
            {
                Material source = sources[i];
                if (!source || !source.shader || source.HasProperty("_Dissolve") || source.renderQueue >= 3000) continue;
                var cache = actor ? actorMaterials : grassMaterials;
                if (!cache.TryGetValue(source, out Material target))
                {
                    target = new Material(actor ? celShader : foliageShader) { name = "Adventure " + source.name, enableInstancing = true };
                    CopyAuthoredSurface(source, target);
                    target.SetFloat("_UseVertexColors", mesh.HasVertexAttribute(VertexAttribute.Color) ? 1 : 0);
                    if (!actor)
                    {
                        target.SetFloat("_UseUVHeight", blade ? 1 : 0);
                        target.SetFloat("_RootHeight", mesh.bounds.min.y);
                        target.SetFloat("_BladeHeight", Mathf.Max(.1f, mesh.bounds.size.y));
                        target.SetFloat("_WindStrength", blade ? .12f : .10f);
                        target.SetFloat("_TrampleStrength", blade ? .58f : .16f);
                        target.SetFloat("_Cull", 0);
                    }
                    // Black/red Meshy armor remains dark; rim is a narrow warm outline.
                    else target.SetFloat("_RimIntensity", renderer.GetComponentInParent<ThanhGiongEnemy>()?.isBoss == true ? .16f : .24f);
                    cache[source] = target; ownedMaterials.Add(target);
                }
                replacements[i] = target; changed = true;
            }
            if (!changed) continue;
            originals[renderer] = sources;
            renderer.sharedMaterials = replacements;
            visited.Add(renderer);
            if (actor) StyledActorRenderers++; else InteractiveFoliageRenderers++;
        }
    }

    static void CopyAuthoredSurface(Material source, Material target)
    {
        foreach (string key in new[] { "_BaseColor", "_Color", "baseColorFactor", "_BaseColorFactor" })
            if (source.HasProperty(key)) { target.SetColor("_BaseColor", source.GetColor(key)); break; }
        CopyTexture(source, target, "_BaseMap", "_BaseMap", "_MainTex", "baseColorTexture", "_BaseColorTexture");
        CopyTexture(source, target, "_BumpMap", "_BumpMap", "normalTexture", "_NormalTexture");
        CopyTexture(source, target, "_MetallicGlossMap", "_MetallicGlossMap");
        CopyTexture(source, target, "_EmissionMap", "_EmissionMap", "emissiveTexture");
        foreach (string key in new[] { "_Metallic", "_Smoothness", "_BumpScale", "_Cutoff", "_Cull", "_AlphaClip", "_UseVertexColors", "_Visibility" })
            if (source.HasProperty(key) && target.HasProperty(key)) target.SetFloat(key, source.GetFloat(key));
        if (source.HasProperty("_EmissionColor") && target.HasProperty("_EmissionColor")) target.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
        if (source.IsKeywordEnabled("_ALPHATEST_ON")) target.EnableKeyword("_ALPHATEST_ON");
    }

    static void CopyTexture(Material source, Material target, string dest, params string[] aliases)
    {
        if (!target.HasProperty(dest)) return;
        foreach (string key in aliases)
        {
            if (!source.HasProperty(key) || !source.GetTexture(key)) continue;
            target.SetTexture(dest, source.GetTexture(key)); target.SetTextureScale(dest, source.GetTextureScale(key)); target.SetTextureOffset(dest, source.GetTextureOffset(key)); return;
        }
    }

    void BuildFootsteps()
    {
        dust = CreateParticles("Foot and Hoof Dust", generated.transform, dustMaterial, 96, .48f, .24f);
        var main = dust.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.38f, .72f); main.startSize = new ParticleSystem.MinMaxCurve(.14f, .34f);
        main.startColor = new Color(.73f, .63f, .43f, .32f); main.gravityModifier = -.015f;
        var size = dust.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .55f, 1, 1.8f));
        foreach (Transform bone in player.GetComponentsInChildren<Transform>(true))
        {
            string name = bone.name.ToLowerInvariant();
            if (name.Contains("hoof") || name == "foot_r" || name == "foot_l" || name.EndsWith("foot_r") || name.EndsWith("foot_l")) feet.Add(bone);
        }
    }

    void BuildLeaves()
    {
        leaves = CreateParticles("Bamboo Leaves on the Breeze", generated.transform, leafMaterial, 48, 5f, .11f);
        var main = leaves.main; main.startLifetime = new ParticleSystem.MinMaxCurve(4, 7); main.startSize = new ParticleSystem.MinMaxCurve(.06f, .13f);
        main.startColor = fortress ? new Color(.56f, .62f, .42f, .35f) : new Color(.58f, .73f, .36f, .48f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        var emission = leaves.emission; emission.rateOverTime = 3;
        var shape = leaves.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(22, 5, 22);
        var velocity = leaves.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = .6f; velocity.y = -.15f; velocity.z = .25f;
        var noise = leaves.noise; noise.enabled = true; noise.strength = .3f; noise.frequency = .25f; noise.scrollSpeed = .2f;
        var rotation = leaves.rotationOverLifetime; rotation.enabled = true; rotation.z = .65f;
        leaves.Play();
    }

    void BuildResourceGlows()
    {
        foreach (var item in FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (item.gameObject.scene != gameObject.scene || item.IsCollected || glows.Count >= 32) continue;
            var particles = CreateParticles("Resource Jade Sparkles", item.transform, sparkMaterial, 10, 1.25f, .065f);
            particles.transform.localPosition = Vector3.up * .16f;
            var main = particles.main; main.startColor = item.kind == ThanhGiongCollectible.Kind.Bamboo ? new Color(.3f, 1.7f, .85f, .6f) : item.GetAuraColor() * new Color(1.5f, 1.5f, 1.5f, .6f);
            main.startSpeed = .16f;
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = item.kind == ThanhGiongCollectible.Kind.Bamboo ? .7f : .3f; shape.rotation = new Vector3(-90, 0, 0);
            var emission = particles.emission; emission.rateOverTime = 2.5f;
            var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.y = .25f;
            particles.Play(); glows.Add(new ResourceGlow { item = item, particles = particles });
        }
    }

    void BuildAtmosphere()
    {
        var go = new GameObject("Adventure Warm Atmosphere"); go.transform.SetParent(generated.transform, false);
        volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 50; volume.weight = fortress ? gradingWeight * .4f : gradingWeight;
        profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Adventure Runtime Atmosphere"; volume.sharedProfile = profile;
        var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(.9f); bloom.intensity.Override(fortress ? .75f : 1.2f); bloom.scatter.Override(.7f);
        if (!fortress)
        {
            var color = profile.Add<ColorAdjustments>(true); color.contrast.Override(12); color.saturation.Override(16); color.colorFilter.Override(new Color(1f, .955f, .88f));
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            // Keep authored atmospheric settings where enabled, particularly the fortress.
            if (!RenderSettings.fog)
            {
                oldFog = RenderSettings.fog; oldFogMode = RenderSettings.fogMode; oldFogColor = RenderSettings.fogColor; oldFogStart = RenderSettings.fogStartDistance; oldFogEnd = RenderSettings.fogEndDistance;
                changedFog = true; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(.66f, .80f, .85f); RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 160;
            }
        }
        Camera camera = Camera.main;
        if (camera && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = true;
    }

    ParticleSystem CreateParticles(string name, Transform parent, Material material, int max, float lifetime, float size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max; main.startLifetime = lifetime; main.startSize = size; main.startSpeed = 0;
        var emission = ps.emission; emission.rateOverTime = 0;
        var shape = ps.shape; shape.enabled = false;
        var fade = ps.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(0, 1) }); fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return ps;
    }

    Material ParticleMaterial(string name, Texture2D texture)
    {
        var material = new Material(particleShader) { name = name, renderQueue = (int)RenderQueue.Transparent };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        ownedMaterials.Add(material); return material;
    }

    Texture2D SoftTexture(bool leaf)
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = leaf ? "Adventure Procedural Bamboo Leaf" : "Adventure Procedural Soft Dust", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[1024];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float u = (x + .5f) / 16 - 1, v = (y + .5f) / 16 - 1;
            float radius = leaf ? Mathf.Sqrt(u * u * 12 + v * v) : Mathf.Sqrt(u * u + v * v);
            float alpha = leaf ? Mathf.Clamp01((1 - radius) * 6) : Mathf.Pow(Mathf.Clamp01(1 - radius), 2);
            pixels[y * 32 + x] = new Color(1, 1, 1, alpha);
        }
        texture.SetPixels(pixels); texture.Apply(false, true); textures.Add(texture); return texture;
    }

    void LateUpdate()
    {
        if (!IsBuilt || !player || Time.timeScale <= 0) return;
        var art = player.GetComponent<MeshyHeroVisualDriver>();
        Vector3 footprint = art && art.meshyHorse ? art.meshyHorse.position : player.position;
        Shader.SetGlobalVector("_AdventurePlayerPosition", new Vector4(footprint.x, player.position.y, footprint.z, 1.4f));
        Vector3 rear = footprint - player.forward * .8f;
        Shader.SetGlobalVector("_AdventureHorsePosition", new Vector4(rear.x, rear.y, rear.z, 1.6f));
        foreach (var material in grassMaterials.Values) if (material) material.SetFloat("_MotionTime", Time.time);
        if (Time.time >= scanAt)
        {
            scanAt = Time.time + 2; ScanScene();
            foreach (var glow in glows)
            {
                if (!glow.particles) continue;
                bool show = glow.item && glow.item.gameObject.activeInHierarchy && !glow.item.IsCollected && (glow.item.transform.position - player.position).sqrMagnitude < 900;
                var emission = glow.particles.emission; emission.enabled = show;
            }
        }
        if (leaves && Time.time >= leafFollowAt)
        {
            leafFollowAt = Time.time + .15f;
            leaves.transform.position = player.position + Vector3.up * 3 + player.forward * 2;
        }
        Vector3 delta = Vector3.ProjectOnPlane(player.position - lastPlayerPosition, Vector3.up); lastPlayerPosition = player.position;
        if (delta.magnitude > 3) { stepDistance = 0; return; } // Teleports never create huge dust bursts.
        if (!movement.Grounded || movement.PlanarSpeed < .65f) { stepDistance = 0; return; }
        if (dust && mountMotion && mountMotion.IsRigReady)
        {
            if (mountMotion.HoofContactCount != observedHoofContacts)
            {
                observedHoofContacts = mountMotion.HoofContactCount;
                EmitDust(mountMotion.LastHoofContact);
            }
            return;
        }
        stepDistance += delta.magnitude;
        if (dust && stepDistance >= (movement.PlanarSpeed > 6 ? .85f : .62f))
        {
            stepDistance = 0;
            Vector3 point = feet.Count > 0 && feet[footIndex % feet.Count] ? feet[footIndex++ % feet.Count].position : player.position + player.right * ((footIndex++ % 2 == 0) ? -.35f : .35f);
            if (FindGround(point, out point)) EmitDust(point);
        }
    }

    void EmitDust(Vector3 point)
    {
        var emit = new ParticleSystem.EmitParams { position = point + Vector3.up * .035f, velocity = Vector3.up * .23f - player.forward * .15f };
        dust.Emit(emit, movement.PlanarSpeed > 6 ? 4 : 2); FootstepBursts++;
    }

    bool FindGround(Vector3 point, out Vector3 ground)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 1.5f, Vector3.down, groundHits, 5, ~0, QueryTriggerInteraction.Ignore);
        float height = float.NegativeInfinity; ground = point;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (!hit.collider || hit.normal.y < .5f || hit.collider.GetComponentInParent<MountedHorseController>() || hit.collider.GetComponentInParent<ThanhGiongEnemy>()) continue;
            if (hit.point.y > height) { height = hit.point.y; ground = hit.point; }
        }
        return !float.IsNegativeInfinity(height);
    }

    void OnEnable()
    {
        if(Application.isPlaying) StartCoroutine(BuildPresentation());
    }
    void OnDisable()
    {
        StopAllCoroutines();
        if (generated) generated.SetActive(false);
        foreach (var glow in glows) if (glow.particles) { var emission = glow.particles.emission; emission.enabled = false; }
        Shader.SetGlobalVector("_AdventurePlayerPosition", new Vector4(0, 0, 0, -1));
        Shader.SetGlobalVector("_AdventureHorsePosition", new Vector4(0, 0, 0, -1));
        ReleasePresentation();
    }
    void OnDestroy() { ReleasePresentation(); }
    void ReleasePresentation()
    {
        foreach (var pair in originals)
            if (pair.Key)
            {
                var current = pair.Key.sharedMaterials; bool ours = false;
                foreach (var material in current) if (ownedMaterials.Contains(material)) ours = true;
                if (ours) pair.Key.sharedMaterials = pair.Value;
            }
        foreach (var glow in glows) if (glow.particles) Destroy(glow.particles.gameObject);
        foreach (var material in ownedMaterials) if (material) Destroy(material);
        foreach (var texture in textures) if (texture) Destroy(texture);
        if (profile) Destroy(profile);
        if (generated) Destroy(generated);
        if (changedFog) { RenderSettings.fog = oldFog; RenderSettings.fogMode = oldFogMode; RenderSettings.fogColor = oldFogColor; RenderSettings.fogStartDistance = oldFogStart; RenderSettings.fogEndDistance = oldFogEnd; }
        originals.Clear();actorMaterials.Clear();grassMaterials.Clear();ownedMaterials.Clear();textures.Clear();visited.Clear();glows.Clear();feet.Clear();
        generated=null;dust=null;leaves=null;volume=null;profile=null;changedFog=false;IsBuilt=false;
        StyledActorRenderers=InteractiveFoliageRenderers=FootstepBursts=0;
        scanAt=leafFollowAt=stepDistance=0;
    }
}
