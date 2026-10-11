using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Original WAV palette migrated from Godot. Scene-owned loops/voices require no global audio host.
public sealed class ThanhGiongCampaignAudio : MonoBehaviour
{
    [Range(0, 1)] public float musicGain = .11f;
    [Range(0, 1)] public float ambienceGain = .50f;
    [Range(0, 1)] public float effectsGain = .8f;
    public string Region { get; private set; }
    public string LastSurface { get; private set; } = "dirt";
    public int HoofCount { get; private set; }
    public float CombatIntensity { get; private set; }
    public float SpatialAmbienceGain { get; private set; }
    public int SpatialEmitterCount => spatial.Count;
    public string LastCue { get; private set; }
    public int SeedCueCount { get; private set; }
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private AudioSource music, combatMusic, ambience, ascensionMusic;
    private readonly List<SpatialEmitter> spatial = new List<SpatialEmitter>();
    private sealed class SpatialEmitter { public AudioSource source; public Bounds area; public float gain, radius; }
    private ThanhGiongEnemy[] nearbyEnemies = new ThanhGiongEnemy[0];
    private float enemyRefreshAt;
    private readonly List<AudioClip> generated = new List<AudioClip>();
    private AudioSource[] voices;
    private CharacterController body;
    private MountedHorseController movement;
    private ThanhGiongCampaignController campaign;
    private int voiceIndex;
    private float age, hoofClock, ascensionMix;
    private bool paused;
    private Renderer riverWater;
    private GodotEnvironmentMotion forgeMotion;
    private float previousForgePhase;

    public static string RegionForScene(string scene) => scene switch {
        "KinhThanhRenThep" => "forge", "PhaoDaiNgamQuanAn" => "camp",
        "ThungLungVuotSong" => "river", "TranTuyenNuiSoc" => "battle", "PhuDongArena" => "battle",
        "DinhSocHoaThanh" => "sacred", _ => "village"
    };

    private void Awake()
    {
        Region = RegionForScene(SceneManager.GetActiveScene().name);
        body = GetComponent<CharacterController>();
        movement = GetComponent<MountedHorseController>();
        campaign = GetComponent<ThanhGiongCampaignController>();
        music = NewVoice(true); ambience = NewVoice(true);
        music.clip = Clip(Region == "sacred" ? "music_sacred_full" : "music_" + (Region=="battle"?"camp":Region)); ambience.clip = Clip("ambient_" + Region);
        combatMusic = NewVoice(true); combatMusic.clip = Clip("music_battle"); combatMusic.volume = 0;
        if (Region == "sacred") { ascensionMusic = NewVoice(false); ascensionMusic.clip = Clip("music_ascension"); ascensionMusic.volume = 0; }
        music.volume = ambience.volume = 0;
        if (music.clip != null) music.Play();
        if (ambience.clip != null) ambience.Play();
        if (combatMusic.clip != null) combatMusic.Play();
        voices = new AudioSource[12];
        for (int i = 0; i < voices.Length; i++) voices[i] = NewVoice(false);
    }

    private void Start()
    {
        if (Region == "forge") forgeMotion = FindAnyObjectByType<GodotEnvironmentMotion>();
        if (Region != "river") return;
        Renderer[] scenery=FindObjectsByType<Renderer>();
        foreach(Renderer renderer in scenery)if(renderer.name.ToLowerInvariant().Contains("dongsong")){riverWater=renderer;break;}
        if(riverWater!=null)AddSpatial("ambient_water_lap",riverWater.bounds,.17f,14);
        foreach (Renderer renderer in scenery) {
            string label = renderer.name.ToLowerInvariant();
            if (label.Contains("caugo") || label.Contains("walkabledeck")) {
                if (spatial.Count < 3) AddSpatial("ambient_bridge_creak", renderer.bounds, .09f, 5);
            }
            else if (label.Contains("reed") || label.Contains("lausay")) {
                if (spatial.Count < 5) AddSpatial("ambient_reeds", renderer.bounds, .08f, 7);
            }
        }
        // Imported scenery can combine reeds into the bank mesh; retain one bounded bank layer.
        if (riverWater != null && spatial.Count < 2) AddSpatial("ambient_reeds", riverWater.bounds, .06f, 9); if (riverWater != null && spatial.Count < 5) AddSpatial("ambient_frogs", riverWater.bounds, .07f, 16);
    }

    private AudioSource NewVoice(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = loop; source.playOnAwake = false; source.spatialBlend = 0; source.dopplerLevel = 0;
        return source;
    }

    private AudioClip Clip(string cue)
    {
        if (!clips.TryGetValue(cue, out AudioClip clip)) {
            clip = Resources.Load<AudioClip>("ThanhGiongAudio/" + cue);
            if (clip == null && LegendProceduralAudio.CanCreate(cue)) {
                clip = LegendProceduralAudio.Create(cue); generated.Add(clip);
            }
            clips[cue] = clip;
            if (clip == null) Debug.LogWarning("Missing Thánh Gióng audio: " + cue, this);
        }
        return clip;
    }

    private void Update()
    {
        bool shouldPause = Time.timeScale <= 0;
        if (shouldPause != paused) {
            paused = shouldPause;
            SetPaused(music); SetPaused(ambience);
            SetPaused(combatMusic);
            if (ascensionMusic != null) SetPaused(ascensionMusic);
            foreach (SpatialEmitter emitter in spatial) SetPaused(emitter.source);
            foreach (AudioSource voice in voices) SetPaused(voice);
        }
        if (paused) return;
        age += Time.deltaTime;
        float fade = Mathf.Clamp01(age / 1.3f);
        ascensionMix = Mathf.MoveTowards(ascensionMix, ascensionMusic != null && ascensionMusic.isPlaying ? 1f : 0f, Time.deltaTime * .9f);
        float threat = FindThreat();
        CombatIntensity = Mathf.Lerp(CombatIntensity, threat, 1 - Mathf.Exp(-Time.deltaTime * (threat > CombatIntensity ? 1.6f : .65f)));
        music.volume = musicGain * fade * Mathf.Sqrt(1 - CombatIntensity) * (Region == "sacred" ? 1.6f : 1f) * Mathf.Lerp(1f, .38f, ascensionMix);
        combatMusic.volume = musicGain * fade * Mathf.Sqrt(CombatIntensity) * .85f;
        ambience.volume = ambienceGain * fade * Mathf.Lerp(1, .7f, CombatIntensity) * (Region == "sacred" ? .58f : 1f) * Mathf.Lerp(1f, .45f, ascensionMix);
        if (ascensionMusic != null) ascensionMusic.volume = musicGain * 2.2f * fade * ascensionMix;
        SpatialAmbienceGain = 0;
        foreach (SpatialEmitter emitter in spatial) {
            emitter.source.transform.position = emitter.area.center;
            Vector3 closest = emitter.area.ClosestPoint(transform.position);
            float proximity = Mathf.Clamp01(1 - Vector3.Distance(closest, transform.position) / emitter.radius);
            emitter.source.volume = emitter.gain * fade * proximity * proximity;
            SpatialAmbienceGain += emitter.source.volume;
        }
        if (forgeMotion != null) {
            float phase = forgeMotion.MotionTime % 1.8f;
            if (previousForgePhase < 1.08f && phase >= 1.08f) PlayCue("hit_metal", .4f);
            previousForgePhase = phase;
        }
        if (body == null || movement == null || !movement.enabled || !body.enabled ||
            (campaign != null && campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Complete)) {
            hoofClock = 0; return;
        }
        Vector3 velocity = movement.WorldVelocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        LastSurface = DetectSurface();
        UpdateHooves(Time.deltaTime, speed, body.isGrounded, LastSurface);
    }

    private float FindThreat()
    {
        if (campaign == null || !campaign.IsBattleActive || campaign.IsDead) return 0;
        if (Time.time >= enemyRefreshAt) { nearbyEnemies = FindObjectsByType<ThanhGiongEnemy>(); enemyRefreshAt = Time.time + .5f; }
        float threat = 0;
        foreach (ThanhGiongEnemy enemy in nearbyEnemies) {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.HealthRatio <= 0) continue;
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            threat = Mathf.Max(threat, Mathf.Clamp01((18 - distance) / 12) * (enemy.isBoss ? 1 : .85f));
        }
        return threat;
    }

    private void AddSpatial(string cue, Bounds area, float gain, float radius)
    {
        if(spatial.Count>=5)return;
        GameObject anchor = new GameObject(cue); anchor.transform.SetParent(transform, true); anchor.transform.position = area.center;
        AudioSource source = anchor.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = true; source.dopplerLevel = 0; source.spatialBlend = .35f;
        // The listener is on the elevated isometric camera; player-distance gain supplies bank locality.
        source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 25; source.maxDistance = 80;
        source.clip = Clip(cue); source.volume = 0;
        if (source.clip != null) source.Play();
        spatial.Add(new SpatialEmitter { source = source, area = area, gain = gain, radius = radius });
    }

    public void PlaySeed(int index, bool bloom = false)
    {
        if (index < 0 || index > 4 || Time.timeScale <= 0) return;
        SeedCueCount++; PlayCue("seed_" + index, bloom ? .32f : .60f);
    }

    private void SetPaused(AudioSource source)
    {
        if (source == null) return;
        if (paused) source.Pause(); else source.UnPause();
    }

    public void UpdateHooves(float delta, float speed, bool grounded, string surface)
    {
        if (paused || !grounded || speed < .45f) { hoofClock = 0; return; }
        float pace = Mathf.Clamp01((speed - 1f) / 7.5f);
        float interval = Mathf.Lerp(.42f, .19f, pace) * (HoofCount % 2 == 0 ? .92f : 1.08f);
        hoofClock += delta;
        if (hoofClock < interval) return;
        hoofClock %= interval; HoofCount++;
        PlayCue("hoof_" + (surface == "wood" || surface == "stone" || surface == "wet" ? surface : "dirt") + "_soft", Mathf.Lerp(.29f, .43f, pace));
    }

    public string DetectSurface()
    {
        Vector3 origin = transform.position + Vector3.up * .3f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 2.6f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue; Transform ground = null;
        for (int i = 0; i < count; i++) {
            RaycastHit hit = groundHits[i];
            if (hit.transform.IsChildOf(transform) || hit.normal.y < .6f || hit.distance >= nearest) continue;
            nearest = hit.distance; ground = hit.transform;
        }
        for (Transform t = ground; t != null; t = t.parent) {
            string label = t.name.ToLowerInvariant();
            if (label.Contains("caugo") || label.Contains("walkabledeck") || label.Contains("bridge")) return "wood";
            if (label.Contains("wet") || label.Contains("shallowwater") || label.Contains("songnong")) return "wet";
            if (label.Contains("stone") || label.Contains("rock") || label.Contains("da_")) return "stone";
        }
        if (Region == "river" && riverWater != null) {
            Bounds bounds = riverWater.bounds;
            Vector3 position = transform.position;
            if (position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.z >= bounds.min.z && position.z <= bounds.max.z) return "wet";
        }
        return Region == "forge" || Region == "sacred" ? "stone" : "dirt";
    }

    public void PlayCue(string cue, float gain = 1f)
    {
        if (Time.timeScale <= 0 || voices == null) return;
        AudioClip clip = Clip(cue);
        if (clip == null) return;
        LastCue = cue;
        AudioSource voice = null;
        // Avoid stealing a weapon/gear impact for routine hoof beats when the bounded pool fills.
        for (int i = 0; i < voices.Length; i++) { AudioSource candidate = voices[(voiceIndex + i) % voices.Length]; if (!candidate.isPlaying) { voice = candidate; break; } }
        bool hoof = cue.StartsWith("hoof_");
        if (voice == null && hoof) return;
        if (voice == null) voice = voices[voiceIndex % voices.Length]; voiceIndex++;
        voice.Stop(); voice.clip = clip; voice.pitch = hoof ? Random.Range(.96f, 1.04f) : Random.Range(.94f, 1.06f);
        voice.volume = effectsGain * gain * (hoof ? Random.Range(.92f, 1.04f) : 1f); voice.Play();
    }

    public void PlayAscensionMusic()
    {
        if (ascensionMusic == null && campaign != null && campaign.IsArena)
        {
            ascensionMusic = NewVoice(false); ascensionMusic.clip = Clip("music_ascension");
        }
        if (ascensionMusic == null || ascensionMusic.clip == null || Time.timeScale <= 0) return;
        ascensionMusic.Stop(); ascensionMusic.Play();
        LastCue = "music_ascension";
    }

    public void ResetArenaAudio()
    {
        if (ascensionMusic != null) ascensionMusic.Stop();
        ascensionMix = 0;
        if (voices != null) foreach (var voice in voices) if (voice != null) voice.Stop();
    }

    public void PlayGrowth() => PlayCue("arrival", .95f);
    public void PlayFoodPickup() => PlayCue("arrival", .55f);
    public void PlaySlash() => PlayCue("attack_sword", .72f);
    public void PlayBamboo() => PlayCue("attack_bamboo", .95f);
    public void PlayFire() => PlayCue("fire");
    public void PlayHorseRoar() => PlayCue("arrival", .85f);
    public void PlayRoofCrash() => PlayCue("sword_break", .88f);
    public void PlayStoneSmash() => PlayCue("hit_metal", .9f);
    public void PlayEquipment() => PlayCue("metal_equip", .35f);
    public void PlayImpact(bool bamboo) => PlayCue(bamboo ? "hit_bamboo" : "hit_metal");
    public void PlayBambooPull() => PlayCue("bamboo_pull");
    public void PlaySwordBreak() => PlayCue("sword_break");

    private void OnDisable()
    {
        if (music != null) music.Stop(); if (ambience != null) ambience.Stop();
        if (combatMusic != null) combatMusic.Stop();
        if (ascensionMusic != null) ascensionMusic.Stop();
        foreach (SpatialEmitter emitter in spatial) if (emitter.source != null) emitter.source.Stop();
        if (voices != null) foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
    }
    private void OnDestroy() { foreach (AudioClip clip in generated) if (clip != null) Destroy(clip); }
}
