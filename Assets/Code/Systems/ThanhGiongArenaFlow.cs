using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ThanhGiongCampaignController))]
[RequireComponent(typeof(ThanhGiongArenaInput))]
[DefaultExecutionOrder(100)]
public sealed class ThanhGiongArenaFlow : MonoBehaviour
{
    public enum Stage { Intro, IronCombat, BreakingIronRod, SeekingBamboo, BambooCombat, VictoryCinematic, Victory, Defeat }
    public ThanhGiongArenaWaveManager waveManager;
    public ThanhGiongCollectible bamboo;
    [Min(1f)] public float interactionDistance = 3.5f;
    [Min(.1f)] public float breakDuration = 1.4f;
    [Min(1f)] public float cinematicDuration = 5f;
    public Stage CurrentStage { get; private set; }
    public int BambooPulls { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public bool Paused { get; private set; }
    public bool CanFight => !Paused && (CurrentStage == Stage.IronCombat || CurrentStage == Stage.BambooCombat);
    public bool InRound => CurrentStage != Stage.Intro && CurrentStage != Stage.Victory && CurrentStage != Stage.Defeat;
    public bool NearBamboo => bamboo != null && bamboo.gameObject.activeInHierarchy &&
        Vector3.ProjectOnPlane(bamboo.transform.position - transform.position, Vector3.up).sqrMagnitude <= interactionDistance * interactionDistance;
    public string Objective => CurrentStage switch
    {
        Stage.Intro => "Bảo vệ làng Phù Đổng. Xuất trận với roi sắt!",
        Stage.IronCombat => $"Roi sắt · Đánh tan đợt quân {waveManager.WaveIndex + 1}/3",
        Stage.BreakingIronRod => "Roi sắt đã gãy!",
        Stage.SeekingBamboo => NearBamboo ? $"Nhấn E để nhổ tre ({BambooPulls}/3)" : "Đến khóm tre phát sáng ở rìa chiến trường",
        Stage.BambooCombat => "Tre ngà · Đánh tan đợt quân cuối cùng",
        Stage.VictoryCinematic => "Giặc Ân tan tác · Non sông thái bình",
        Stage.Victory => "THẮNG TRẬN · LÀNG PHÙ ĐỔNG ĐÃ AN TOÀN",
        _ => "GIÓNG ĐÃ GỤC NGÃ"
    };

    ThanhGiongCampaignController campaign;
    Vector3 spawn, bambooPosition, bambooScale;
    Quaternion spawnRotation, bambooRotation;
    float stageTime;
    Renderer[] bambooRenderers;
    MaterialPropertyBlock bambooTint;

    void Awake()
    {
        campaign = GetComponent<ThanhGiongCampaignController>();
        spawn = transform.position; spawnRotation = transform.rotation;
        if (bamboo != null)
        {
            bambooPosition = bamboo.transform.localPosition; bambooRotation = bamboo.transform.localRotation;
            bambooScale = bamboo.transform.localScale;
            bambooRenderers = bamboo.GetComponentsInChildren<Renderer>(true);
            bambooTint = new MaterialPropertyBlock();
        }
        if (waveManager != null) { waveManager.Configure(campaign); waveManager.WaveCleared += OnWaveCleared; }
    }

    void Start() => RestartRound();
    void OnDestroy()
    {
        if (waveManager != null) waveManager.WaveCleared -= OnWaveCleared;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (ThanhGiongArenaInput.Down(KeyCode.Escape) && CurrentStage != Stage.VictoryCinematic) SetPaused(!Paused);
        if (ThanhGiongArenaInput.Down(KeyCode.R) && (Paused || CurrentStage == Stage.Defeat || CurrentStage == Stage.Victory)) { RestartRound(); return; }
        if (Paused) return;
        if (CurrentStage == Stage.Intro && ThanhGiongArenaInput.Down(KeyCode.Return)) BeginCombat();
        if (CurrentStage == Stage.SeekingBamboo && ThanhGiongArenaInput.Down(KeyCode.E)) TryPullBamboo();
        if (CurrentStage == Stage.IronCombat || CurrentStage == Stage.BambooCombat || CurrentStage == Stage.SeekingBamboo)
            ElapsedSeconds += Time.deltaTime;
        if (CanFight && campaign.IsDead) Defeat();
        if (CurrentStage == Stage.BreakingIronRod && Time.time - stageTime >= breakDuration) SetStage(Stage.SeekingBamboo);
        if (bambooRenderers != null && CurrentStage == Stage.SeekingBamboo)
        {
            bambooTint.SetColor("_EmissionColor", new Color(.1f, .9f, .18f) * (1f + Mathf.Sin(Time.time * 3f) * .35f));
            foreach (var r in bambooRenderers) if (r != null) r.SetPropertyBlock(bambooTint);
        }
    }

    public void BeginCombat()
    {
        if (CurrentStage != Stage.Intro || Paused || waveManager == null) return;
        SetStage(Stage.IronCombat);
        if (!waveManager.StartWave(0)) { Debug.LogError("Arena wave configuration is incomplete.", this); SetStage(Stage.Intro); }
    }

    void OnWaveCleared(int index)
    {
        if (campaign.IsDead) { Defeat(); return; }
        if (CurrentStage == Stage.IronCombat && index == 0) waveManager.StartWave(1);
        else if (CurrentStage == Stage.IronCombat && index == 1)
        {
            SetStage(Stage.BreakingIronRod);
            campaign.BreakArenaRod();
        }
        else if (CurrentStage == Stage.BambooCombat && index == 2 && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo)
        {
            SetStage(Stage.VictoryCinematic);
            StartCoroutine(VictorySequence());
        }
    }

    public bool TryPullBamboo()
    {
        if (Paused || CurrentStage != Stage.SeekingBamboo || !NearBamboo || BambooPulls >= 3) return false;
        BambooPulls++;
        GetComponent<ThanhGiongCampaignAudio>()?.PlayBambooPull();
        IsometricCameraFollow.Instance?.Shake(.16f, .25f);
        if (BambooPulls < 3) return true;
        bamboo.MarkCollected(); bamboo.gameObject.SetActive(false);
        campaign.EquipArenaBamboo();
        SetStage(Stage.BambooCombat);
        waveManager.StartWave(2);
        return true;
    }

    public bool NotifyEnemyDefeated(ThanhGiongEnemy enemy) => CanFight && waveManager != null && waveManager.ReportDeath(enemy);

    public void Defeat()
    {
        if (!InRound || CurrentStage == Stage.VictoryCinematic) return;
        StopAllCoroutines(); waveManager.StopSpawning(); SetStage(Stage.Defeat);
    }

    public void SetPaused(bool paused)
    {
        if (CurrentStage == Stage.VictoryCinematic) return;
        Paused = paused; Time.timeScale = paused ? 0f : 1f;
    }

    public void RestartRound()
    {
        StopAllCoroutines(); Paused = false; Time.timeScale = 1f;
        GetComponent<ThanhGiongCampaignAudio>()?.ResetArenaAudio();
        if (campaign.vfx != null) campaign.vfx.ClearTransientEffects();
        var feedback = GetComponent<GodotCombatFeedback>();
        if (feedback != null) { feedback.enabled = false; feedback.enabled = true; }
        waveManager?.ResetRound();
        BambooPulls = 0; ElapsedSeconds = 0f;
        if (bamboo != null)
        {
            bamboo.gameObject.SetActive(true); bamboo.transform.SetLocalPositionAndRotation(bambooPosition, bambooRotation);
            bamboo.transform.localScale = bambooScale; bamboo.ResetCollected();
            if (bambooRenderers != null) foreach (var r in bambooRenderers) if (r != null) r.SetPropertyBlock(null);
        }
        campaign.ResetArenaRound(spawn, spawnRotation);
        IsometricCameraFollow.Instance?.ClearOverview(); IsometricCameraFollow.Instance?.SnapToTarget();
        SetStage(Stage.Intro);
    }

    void SetStage(Stage stage)
    {
        CurrentStage = stage; stageTime = Time.time;
        bool move = stage == Stage.IronCombat || stage == Stage.SeekingBamboo || stage == Stage.BambooCombat;
        campaign.SetArenaMovement(move);
    }

    IEnumerator VictorySequence()
    {
        var audio = GetComponent<ThanhGiongCampaignAudio>();
        audio?.PlayAscensionMusic();
        campaign.vfx?.PlayAscensionDivineLight(transform.position);
        Vector3 start = transform.position;
        IsometricCameraFollow.Instance?.SetOverview(start + new Vector3(8, 6, -12), start + Vector3.up * 3);
        float time = 0;
        while (time < cinematicDuration)
        {
            time += Time.deltaTime;
            campaign.TeleportArenaCinematic(start + Vector3.up * (Mathf.SmoothStep(0, 1, time / cinematicDuration) * 8f));
            yield return null;
        }
        SetStage(Stage.Victory);
    }
}
