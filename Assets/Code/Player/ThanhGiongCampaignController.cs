using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MountedHorseController))]
[DefaultExecutionOrder(90)]
public class ThanhGiongCampaignController : MonoBehaviour
{
    public enum Chapter { Prologue, Preparation, Battle, Ascension, Complete }
    public enum Weapon { None, IronSword, Bamboo, Environment }
    public enum BambooVariant { Standard, SwiftYoung, HeavyRoot, FireBamboo }

    [Header("Scene references")]
    public GameObject battleRoot;
    public GameObject bambooRoot;
    public Transform ascensionTarget;
    public ThanhGiongCampaignVFX vfx;
    public ThanhGiongCampaignHUD hud;

    [Header("Balance")]
    public float foodPerGrowth = 100f;
    public float maxHeat = 100f;
    public int swordBreakKills = 6;
    public int victoryKills = 18;
    public float maxHealth = 90f;
    public float hurtInvulnerabilitySeconds = .42f;
    [Header("Hard Mode Pressure")]
    [Tooltip("Áp lực tổng thể tăng dần trong Màn 3 sau mỗi kẻ địch bị hạ.")]
    [Range(1f, 2f)] public float battlePressureScale = 1.35f;
    [Range(0f, 1f)] public float startingBattlePressure = .18f;
    [Tooltip("Màn bắt đầu khi scene được mở trực tiếp.")]
    public Chapter startingChapter = Chapter.Prologue;

    public Chapter CurrentChapter { get; private set; }
    public Weapon CurrentWeapon { get; private set; }
    public float Food { get; private set; }
    public float Heat { get; private set; }
    public int GrowthPhase { get; private set; }
    public int Kills { get; private set; }
    public float Health { get; private set; }
    public float Health01 => Mathf.Clamp01(Health / Mathf.Max(1f, maxHealth));
    public bool IsDead => Health <= 0f;
    public bool IsBattleActive => CurrentChapter == Chapter.Battle && !IsDead && !isTransitioning && (arena == null || arena.CanFight);
    public bool IsArena => arena != null;
    public float FoodProgress => CurrentChapter == Chapter.Prologue ? Mathf.Clamp01(Food / (foodPerGrowth * 3f)) : 1f;
    public float Heat01 => Heat / maxHeat;
    public float BattlePressure01 => CurrentChapter == Chapter.Battle ? Mathf.Clamp01(startingBattlePressure + (Kills / Mathf.Max(1f, victoryKills)) * (1f - startingBattlePressure)) : 0f;
    public string CenterMessage { get; private set; }
    public ThanhGiongImprovisedWeapon CarriedWeapon { get; private set; }
    public int EquipmentStep => qteIndex;
    public bool EquipmentBusy => mountedMotion != null && mountedMotion.IsBusy;
    public BambooVariant CurrentBambooVariant { get; private set; } = BambooVariant.Standard;
    public string BambooVariantName => CurrentBambooVariant switch { BambooVariant.SwiftYoung => "Tre Non Nhanh", BambooVariant.HeavyRoot => "Tre Gốc Đại Lực", BambooVariant.FireBamboo => "Tre Ngà Hỏa", _ => "Tre Ngà Chuẩn" };

    public string StageTitle => CurrentChapter switch
    {
        Chapter.Prologue => "MÀN 1 · TIẾNG RAO DƯỚI MÁI TRANH (LÀNG PHÙ ĐỔNG)",
        Chapter.Preparation => "MÀN 2 · RÈN THÉP & XUẤT QUÂN (LÒ RÈN TRIỀU ĐÌNH)",
        Chapter.Battle => "MÀN 3 · TRẬN TUYẾN NÚI SÓC (CÀN QUÉT GIẶC ÂN)",
        Chapter.Ascension => "MÀN 4 · HÓA THÁNH VỀ TRỜI (ĐỈNH NÚI SÓC)",
        _ => "THÁNH GIÓNG · TRUYỀN THUYẾT & DI SẢN BẤT TỬ"
    };

    public string Objective
    {
        get
        {
            if (CurrentChapter == Chapter.Prologue)
                return $"MỤC TIÊU MÀN 1: Gom đủ {foodPerGrowth * 3f} lương thực để Gióng lớn vọt thành tráng sĩ khổng lồ ({Mathf.RoundToInt(Food)}/{foodPerGrowth * 3f}). Hoàn thành sẽ tự động chuyển sang Màn 2!";
            if (CurrentChapter == Chapter.Preparation)
                return $"MỤC TIÊU MÀN 2: Hoàn tất 4 bước trang bị vua ban (Bước {Mathf.Min(qteIndex + 1, 4)}/4): Nhấn E (Mặc giáp) → Q (Đội nón) → E (Lên ngựa sắt) → F (Phun lửa kiểm tra vũ khí)!";
            if (CurrentChapter == Chapter.Battle && CurrentWeapon == Weapon.IronSword)
                return $"MỤC TIÊU MÀN 3: Càn quét tiền tuyến bằng Gươm Sắt (Hạ {Kills}/{victoryKills} giặc). Áp lực giặc tăng theo từng đợt; né đúng nhịp và nhặt đá/cây khi bị vây!";
            if (CurrentChapter == Chapter.Battle && CurrentWeapon == Weapon.None)
                return $"CẢNH BÁO: Gươm sắt đã gãy! Chạy ngay đến khóm tre ngà phát sáng và nhấn [E] {(3 - bambooQte)} lần nữa để NHỔ TRE TIẾP CHIẾN!";
            if (CurrentChapter == Chapter.Battle)
                return $"MỤC TIÊU MÀN 3: Dùng Khóm Tre Ngà quét sạch tàn quân & Tướng giặc (Hạ {Kills}/{victoryKills} giặc). Càng về cuối giặc càng áp sát nhanh; tích đủ 100% nhiệt rồi nhấn F phun Hỏa Tuyến!";
            if (CurrentChapter == Chapter.Ascension)
                return $"MỤC TIÊU MÀN 4: Nghi thức cởi giáp trên đỉnh Núi Sóc (Bước {Mathf.Min(qteIndex + 1, 3)}/3): Nhấn E → Q → E đặt từng mảnh giáp sắt xuống núi rồi bay về trời!";
            return "Hoàn thành đại nghiệp cứu quốc. Non sông thái bình, truyền thuyết Thánh Gióng sống mãi muôn đời.";
        }
    }

    public string StatusLine => CurrentChapter switch
    {
        Chapter.Prologue => $"Tiến độ: Lương thực {Mathf.RoundToInt(Food)}/{foodPerGrowth * 3f}  ·  Thể chất: Bậc {GrowthPhase + 1}/4" + (Food >= foodPerGrowth * 3f ? " [HOÀN THÀNH - ĐANG CHUYỂN MÀN 2...]" : ""),
        Chapter.Preparation => $"Tiến độ: Trang bị {qteIndex}/4 bước  ·  Trạng thái: " + (qteIndex switch { 0 => "Chờ mặc giáp [E]", 1 => "Đã mặc giáp vàng -> Chờ đội nón [Q]", 2 => "Đã đội nón sắt -> Chờ lên ngựa [E]", 3 => "Đã lên ngựa sắt -> Nhấn [F] phun lửa xuất quân!", _ => "Sẵn sàng xuất trận!" }),
        Chapter.Battle => $"Tiến độ: Hạ địch {Kills}/{victoryKills}  ·  Áp lực: {Mathf.RoundToInt(BattlePressure01 * 100)}%  ·  Vũ khí: {WeaponName}  ·  Nhiệt lượng: {Mathf.RoundToInt(Heat01 * 100)}%" + (CarriedWeapon != null ? "  [Đang vác vật thể]" : ""),
        Chapter.Ascension => $"Tiến độ: Cởi giáp {qteIndex}/3 bước  ·  Địa điểm: Đỉnh Núi Sóc",
        _ => "Hoàn thành đại nghiệp cứu quốc · Đất nước thái bình"
    };

    public float GetEnemySpeedScale(bool boss)
    {
        float pressure = BattlePressure01;
        float endScale = boss ? battlePressureScale + .06f : battlePressureScale;
        return Mathf.Lerp(1.05f, endScale, pressure);
    }

    public float GetEnemyDamageScale(bool boss)
    {
        float pressure = BattlePressure01;
        float endScale = boss ? 1.55f : 1.42f;
        return Mathf.Lerp(1.08f, endScale, pressure);
    }

    public float GetEnemyCooldownScale(bool boss)
    {
        float pressure = BattlePressure01;
        return Mathf.Lerp(.9f, boss ? .66f : .62f, pressure);
    }

    public float GetEnemyAttackRangeScale(bool boss)
    {
        float pressure = BattlePressure01;
        return Mathf.Lerp(1f, boss ? 1.18f : 1.12f, pressure);
    }

    private string WeaponName => CurrentWeapon switch
    {
        Weapon.IronSword => "Gươm Sắt (Chém Thư Pháp)",
        Weapon.Bamboo => BambooVariantName + " (Quét 360°)",
        Weapon.Environment => "Đá / Gốc cây",
        _ => "Tay không"
    };

    private readonly Collider[] hits = new Collider[128];
    private readonly Collider[] bambooProbeHits = new Collider[96];
    private readonly List<ThanhGiongCollectible> runtimeBambooGroves = new List<ThanhGiongCollectible>();
    private readonly HashSet<Transform> registeredBambooRoots = new HashSet<Transform>();
    private const float BambooPickupRadius = 7.5f;
    private const float BambooPickupColliderRadius = 2.35f;
    private const float MinDamageFalloff = 0.72f;
    private const float MaxKnockbackScale = 1.85f;

    private readonly struct MeleeProfile
    {
        public readonly float BaseDamage, Radius, ForwardReach, Stun, Cooldown, AttackDuration, HeatPerEnemy, BaseKnockback, ArcDot;
        public readonly bool Bamboo;
        public MeleeProfile(bool bamboo, float baseDamage, float radius, float forwardReach, float stun, float cooldown, float attackDuration, float heatPerEnemy, float baseKnockback, float arcDot)
        {
            Bamboo = bamboo; BaseDamage = baseDamage; Radius = radius; ForwardReach = forwardReach; Stun = stun; Cooldown = cooldown; AttackDuration = attackDuration; HeatPerEnemy = heatPerEnemy; BaseKnockback = baseKnockback; ArcDot = arcDot;
        }
    }

    private MountedHorseController movement;
    private ThanhGiongArenaFlow arena;
    private ThanhGiongCampaignAudio audioFx;
    private GodotMountedMotion mountedMotion;
    private GodotCombatFeedback combatFeedback;
    private readonly List<GameObject> visualSwords = new List<GameObject>();
    private readonly List<GameObject> visualBamboo = new List<GameObject>();
    private readonly HashSet<ThanhGiongEnemy> struckEnemies = new HashSet<ThanhGiongEnemy>();
    private readonly Dictionary<ThanhGiongEnemy, float> chargeHits = new Dictionary<ThanhGiongEnemy, float>();
    private Vector3 originalScale;
    private float nextAttackTime;
    private int comboBeat;
    private float comboUntil;
    private Coroutine pendingStrike;
    public int ComboBeat => comboBeat;
    public bool AttackPending => pendingStrike != null;
    private int qteIndex;
    private int bambooQte;
    private bool isTransitioning;
    private Coroutine clearMessageRoutine;
    private float hurtUntil;
    private Vector3 battleSpawn;
    private Quaternion battleSpawnRotation;
    private KeyCode[] equipQte = { KeyCode.E, KeyCode.Q, KeyCode.E };

    private void Awake()
    {
        arena = GetComponent<ThanhGiongArenaFlow>();
        movement = GetComponent<MountedHorseController>();
        audioFx = GetComponent<ThanhGiongCampaignAudio>();
        mountedMotion = GetComponent<GodotMountedMotion>();
        combatFeedback = GetComponent<GodotCombatFeedback>();
        if (combatFeedback == null) combatFeedback = gameObject.AddComponent<GodotCombatFeedback>();
        originalScale = transform.localScale;
        Health = maxHealth;
        battleSpawn = transform.position;
        battleSpawnRotation = transform.rotation;
        if (vfx == null) vfx = FindAnyObjectByType<ThanhGiongCampaignVFX>();
        if (hud == null) hud = FindAnyObjectByType<ThanhGiongCampaignHUD>();
        if (hud != null) hud.campaign = this;
        if (arena == null) RegisterSceneBambooGroves();
    }

    private void Start()
    {
        if (arena != null) return;
        hurtUntil = Time.time + 1.2f;
        WarpToChapter(startingChapter);
    }

    private void LateUpdate()
    {
        if (visualSwords.Count == 0 && visualBamboo.Count == 0)
            foreach (Transform child in GetComponentsInChildren<Transform>(true)) {
                if (child.name == "Weapon_IronSword") visualSwords.Add(child.gameObject);
                if (child.name == "Weapon_GoldenBamboo") visualBamboo.Add(child.gameObject);
            }
        bool available = !EquipmentBusy && !IsDead && CurrentChapter != Chapter.Complete;
        foreach (GameObject sword in visualSwords) if (sword != null) sword.SetActive(available && CurrentWeapon == Weapon.IronSword);
        foreach (GameObject bamboo in visualBamboo) if (bamboo != null) bamboo.SetActive(available && CurrentWeapon == Weapon.Bamboo);
    }

    private void Update()
    {
        if (arena != null)
        {
            if (arena.CanFight && ThanhGiongArenaInput.AttackDown()) TryMeleeAttack();
            return;
        }
        if (isTransitioning || Time.timeScale <= 0) return;
        if (IsDead) {
            if (Input.GetKeyDown(KeyCode.R)) {
                LegendCheckpoint checkpoint = GetComponent<LegendCheckpoint>();
                if (checkpoint == null || !checkpoint.RestartCheckpoint()) RestartBattle();
            }
            return;
        }

        if (CurrentChapter == Chapter.Prologue) UpdatePrologue();
        else if (CurrentChapter == Chapter.Preparation) UpdateEquipQte();
        else if (CurrentChapter == Chapter.Battle) UpdateBattle();
        else if (CurrentChapter == Chapter.Ascension) UpdateAscensionQte();
    }

    public void ResetArenaRound(Vector3 position, Quaternion rotation)
    {
        StopAllCoroutines(); clearMessageRoutine = null; pendingStrike = null;
        comboBeat = 0; comboUntil = 0; nextAttackTime = 0; isTransitioning = false;
        chargeHits.Clear(); struckEnemies.Clear();
        if (CarriedWeapon != null) { Destroy(CarriedWeapon.gameObject); CarriedWeapon = null; }
        CurrentChapter = Chapter.Battle; CurrentWeapon = Weapon.IronSword;
        CurrentBambooVariant = BambooVariant.Standard;
        GrowthPhase = 3; Health = maxHealth; Food = 0; Heat = 0; Kills = 0;
        qteIndex = 0; bambooQte = 0; CenterMessage = string.Empty; hurtUntil = Time.time + 1.2f;
        transform.localScale = originalScale;
        TeleportPlayer(position); transform.rotation = rotation;
        movement.ResetMovementState(); movement.enabled = false;
        mountedMotion?.SetFlying(false);
    }

    public void SetArenaMovement(bool allowed)
    {
        if (movement == null) movement = GetComponent<MountedHorseController>();
        if (!allowed)
        {
            if (pendingStrike != null) StopCoroutine(pendingStrike);
            pendingStrike = null; movement.ResetMovementState();
        }
        movement.enabled = allowed;
    }

    public void BreakArenaRod()
    {
        CurrentWeapon = Weapon.None;
        vfx?.PlaySwordBreak(transform); audioFx?.PlaySwordBreak();
        IsometricCameraFollow.Instance?.Shake(.55f, .6f);
    }

    public void EquipArenaBamboo()
    {
        CurrentWeapon = Weapon.Bamboo; CurrentBambooVariant = BambooVariant.Standard;
        vfx?.PlayBambooSweep(transform, 4f);
    }

    public void TeleportArenaCinematic(Vector3 position)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position;
    }

    public void TeleportPlayer(Vector3 destinationPosition)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = destinationPosition;
        if (cc != null) cc.enabled = true;
        IsometricCameraFollow.Instance?.SnapToTarget();
    }

    public void WarpToChapter(Chapter targetChapter, Vector3? destinationPosition = null)
    {
        if (arena != null) return;
        CurrentChapter = targetChapter;
        qteIndex = 0;
        bambooQte = 0;

        if (destinationPosition.HasValue)
        {
            TeleportPlayer(destinationPosition.Value);
        }

        switch (targetChapter)
        {
            case Chapter.Prologue:
                CurrentWeapon = Weapon.None;
                GrowthPhase = Mathf.Min(GrowthPhase, 1);
                ApplyGrowthScale();
                if (battleRoot != null) battleRoot.SetActive(false);
                if (bambooRoot != null) bambooRoot.SetActive(false);
                foreach (var f in FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include))
                {
                    if (f.kind <= ThanhGiongCollectible.Kind.Meat) f.gameObject.SetActive(true);
                }
                ShowMessage("ĐÃ ĐI QUA CỔNG: BƯỚC VÀO MÀN 1 · LÀNG PHÙ ĐỔNG!", 3.2f);
                break;

            case Chapter.Preparation:
                GrowthPhase = 3;
                ApplyGrowthScale();
                CurrentWeapon = Weapon.None;
                if (battleRoot != null) battleRoot.SetActive(false);
                if (bambooRoot != null) bambooRoot.SetActive(false);
                ShowMessage("ĐÃ ĐI QUA CỔNG: BƯỚC VÀO MÀN 2 · RÈN THÉP & XUẤT QUÂN! [NHẤN E: MẶC GIÁP]", 3.5f);
                break;

            case Chapter.Battle:
                GrowthPhase = 3;
                ApplyGrowthScale();
                battleSpawn = transform.position;
                battleSpawnRotation = transform.rotation;
                CurrentWeapon = Weapon.IronSword;
                Heat = maxHeat * 0.5f;
                Kills = 0;
                if (battleRoot != null)
                {
                    battleRoot.SetActive(true);
                    foreach (ThanhGiongEnemy enemy in battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true))
                        enemy.Initialize(this, transform);
                }
                if (bambooRoot != null) bambooRoot.SetActive(true);
                RegisterSceneBambooGroves();
                ShowMessage("ĐÃ ĐI QUA CỔNG: BƯỚC VÀO MÀN 3 · TRẬN TUYẾN NÚI SÓC!", 3.5f);
                break;

            case Chapter.Ascension:
                GrowthPhase = 3;
                ApplyGrowthScale();
                CurrentWeapon = Weapon.Bamboo;
                if (battleRoot != null) battleRoot.SetActive(false);
                movement.enabled = true;
                ShowMessage("ĐÃ ĐI QUA CỔNG: BƯỚC VÀO MÀN 4 · HÓA THÁNH VỀ TRỜI! [CỞI GIÁP ĐẶT ĐỈNH NÚI]", 3.8f);
                break;
        }

        mountedMotion?.SyncEquipment((int)CurrentChapter, qteIndex);
        mountedMotion?.SetFlying(false);
        audioFx?.PlayGrowth();
        IsometricCameraFollow.Instance?.Shake(0.4f, 0.5f);
    }

    private void UpdatePrologue()
    {
        // Interact with near collectibles
    }

    public bool TryCollect(ThanhGiongCollectible item)
    {
        if (arena != null) return false;
        if (IsDead || Time.timeScale <= 0) return false;
        if (CurrentChapter == Chapter.Prologue && item.kind <= ThanhGiongCollectible.Kind.Meat)
        {
            if (isTransitioning) return false;

            Food += item.foodValue;
            int phase = Mathf.Clamp(Mathf.FloorToInt(Food / foodPerGrowth), 0, 3);
            if (phase > GrowthPhase)
            {
                GrowthPhase = phase;
                ApplyGrowthScale();
                vfx?.PlayGrowth(transform);
                audioFx?.PlayGrowth();

                if (GrowthPhase == 3)
                {
                    // Collapse village roofs
                    CollapseVillageRoofs();
                    ShowMessage("GIÓNG VƯƠN VAI THÀNH TRÁNG SĨ KHỔNG LỒ — MÁI NHÀ TRANH SẬP ĐỔ!", 3.2f);
                }
                else
                {
                    ShowMessage(GrowthPhase == 1 ? "GIÓNG LỚN NHANH NHƯ THỔI!" : "GIÓNG VƯƠN VAI — DÂN LÀNG REO HÒ!", 2.2f);
                }
            }
            else
            {
                string foodName = item.kind switch
                {
                    ThanhGiongCollectible.Kind.Rice => "7 NONG CƠM GẠO",
                    ThanhGiongCollectible.Kind.Eggplant => "3 NONG CÀ PHÁO",
                    ThanhGiongCollectible.Kind.Firewood => "BÓ CỦI NHÓM LỬA",
                    ThanhGiongCollectible.Kind.Meat => "MÂM THỊT KHAO QUÂN",
                    _ => "LƯƠNG THỰC DÂN LÀNG"
                };
                ShowMessage($"ĐÃ GOM: {foodName} (+{Mathf.RoundToInt(item.foodValue)}) · TIẾN ĐỘ: {Mathf.RoundToInt(Food)}/300", 2.0f);
                audioFx?.PlayFoodPickup();
            }

            vfx?.PlayFoodBurst(item.transform.position, item.GetAuraColor());

            if (Food >= foodPerGrowth * 3f && !isTransitioning)
            {
                StartCoroutine(CompleteStage1AndTransition());
            }
            return true;
        }

        if (CurrentChapter == Chapter.Battle && CurrentWeapon == Weapon.None && item.kind == ThanhGiongCollectible.Kind.Bamboo)
        {
            bambooQte++;
            audioFx?.PlayBambooPull();
            vfx?.PlayFoodBurst(item.transform.position, item.GetAuraColor());
            IsometricCameraFollow.Instance?.Shake(0.25f + bambooQte * 0.08f, 0.45f + bambooQte * 0.08f);
            if (bambooQte >= 3)
            {
                CurrentWeapon = Weapon.Bamboo;
                CurrentBambooVariant = DetermineBambooVariant(item);
                CollapsePulledBamboo(item);
                vfx?.PlayBambooSweep(transform, 7.5f);
                ShowMessage($"NHỔ {BambooVariantName.ToUpperInvariant()} THÀNH CÔNG! MỖI BỤI TRE CHO NHỊP ĐÁNH KHÁC NHAU.", 3f);
            }
            else
            {
                item.gameObject.SetActive(true);
                ShowMessage($"ĐANG NHỔ {BambooName(item)}! NHẤN 'E' THÊM {3 - bambooQte} LẦN", 1.2f);
            }
            return bambooQte >= 3;
        }
        return false;
    }

    private void CollapseVillageRoofs()
    {
        audioFx?.PlayRoofCrash();
        IsometricCameraFollow.Instance?.Shake(0.85f, 0.8f);

        ThanhGiongRoofCollapse[] roofs = FindObjectsByType<ThanhGiongRoofCollapse>();
        foreach (var r in roofs)
        {
            r.Collapse();
        }
    }

    private void UpdateEquipQte()
    {
        if (isTransitioning) return;

        if (qteIndex < equipQte.Length && Input.GetKeyDown(equipQte[qteIndex]))
        {
            qteIndex++;
            if (qteIndex == 1) mountedMotion?.PlayEquipment("equip_armor");
            if (qteIndex == 2) mountedMotion?.PlayEquipment("equip_helmet");
            if (qteIndex <= 2) audioFx?.PlayEquipment();
            IsometricCameraFollow.Instance?.Shake(0.25f, 0.4f);

            if (qteIndex == 1) ShowMessage("MẶC GIÁP SẮT! (ÁO GIÁP VÀNG SÁNG RỰC) [BƯỚC TIẾP: NHẤN 'Q' ĐỘI NÓN SẮT]", 2.2f);
            else if (qteIndex == 2) ShowMessage("ĐỘI NÓN SẮT! (HÀO QUANG ĐÔNG SƠN CHIẾU RỌI) [BƯỚC TIẾP: NHẤN 'E' LÊN NGỰA SẮT]", 2.2f);
            else if (qteIndex == 3)
            {
                CurrentWeapon = Weapon.IronSword;
                Heat = maxHeat;
                audioFx?.PlayHorseRoar();
                IsometricCameraFollow.Instance?.Shake(0.8f, 0.9f);
                ShowMessage("CƯỠI NGỰA SẮT! NGỰA SẮT KÍCH HOẠT CORE LỬA! [NHẤN 'F' ĐỂ PHUN HỎA TUYẾN KIỂM TRA]", 3.2f);
            }
        }

        if (qteIndex == equipQte.Length && Input.GetKeyDown(KeyCode.F))
        {
            qteIndex = 4;
            StartCoroutine(CompleteStage2AndTransition());
        }
    }

    private void UpdateBattle()
    {
        UpdateCharge();
        // Check picking up improvised weapons
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (CurrentWeapon == Weapon.None && TryPullNearestBamboo()) return;
            if (CarriedWeapon == null) TryPickUpImprovisedWeapon();
        }

        // Throw carried weapon
        if (Input.GetMouseButtonDown(0))
        {
            if (CarriedWeapon != null)
            {
                CarriedWeapon.Throw(transform.forward);
                CarriedWeapon = null;
                audioFx?.PlayStoneSmash();
                ShowMessage("NÉM VŨ KHÍ MÔI TRƯỜNG!", 1.2f);
                return;
            }
            TryMeleeAttack();
        }

        if (Input.GetKeyDown(KeyCode.F) && Heat >= maxHeat) CastFireLine();
    }

    private void UpdateCharge()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null || !Input.GetKey(KeyCode.LeftShift) || movement.IsDodging || movement.PlanarSpeed < 5f) return;
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * .6f, 2.4f, hits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) {
            ThanhGiongEnemy enemy = hits[i].GetComponentInParent<ThanhGiongEnemy>();
            if (enemy == null || enemy.HealthRatio <= 0 || !enemy.gameObject.activeInHierarchy) continue;
            if (chargeHits.TryGetValue(enemy, out float nextHit) && Time.time < nextHit) continue;
            chargeHits[enemy] = Time.time + 1f;
            float chargeDamage = CalculateChargeDamage(enemy);
            float chargeImpact = Mathf.Clamp(chargeDamage / 18f, 0.75f, 1.55f);
            enemy.TakeDamage(chargeDamage, .65f, transform.position, chargeImpact, false);
            combatFeedback?.PlayImpact(enemy.transform, false);
            audioFx?.PlayImpact(false);
        }
    }

    private bool TryPullNearestBamboo()
    {
        ThanhGiongCollectible bamboo = FindNearestBambooPickup(BambooPickupRadius);
        if (bamboo == null)
        {
            RegisterSceneBambooGroves();
            bamboo = FindNearestBambooPickup(BambooPickupRadius);
        }
        if (bamboo == null)
        {
            ShowMessage("CHƯA THẤY BỤI TRE GẦN ĐÂY — LẠI SÁT TRE RỒI NHẤN E!", 1.1f);
            return false;
        }
        TryCollect(bamboo);
        return true;
    }

    private ThanhGiongCollectible FindNearestBambooPickup(float radius)
    {
        ThanhGiongCollectible best = null;
        float bestSqr = radius * radius;
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * .7f, radius, bambooProbeHits, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            ThanhGiongCollectible item = bambooProbeHits[i].GetComponentInParent<ThanhGiongCollectible>();
            if (item == null || item.kind != ThanhGiongCollectible.Kind.Bamboo || !item.gameObject.activeInHierarchy) continue;
            float sqr = Vector3.ProjectOnPlane(item.transform.position - transform.position, Vector3.up).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = item; }
        }
        foreach (ThanhGiongCollectible item in runtimeBambooGroves)
        {
            if (item == null || item.kind != ThanhGiongCollectible.Kind.Bamboo || !item.gameObject.activeInHierarchy) continue;
            float sqr = Vector3.ProjectOnPlane(item.transform.position - transform.position, Vector3.up).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = item; }
        }
        if (best != null && best.transform.root != transform.root) return best;
        return null;
    }

    private void RegisterSceneBambooGroves()
    {
        runtimeBambooGroves.RemoveAll(item => item == null);
        foreach (ThanhGiongCollectible item in FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include))
        {
            if (item.kind != ThanhGiongCollectible.Kind.Bamboo) continue;
            if (!runtimeBambooGroves.Contains(item)) runtimeBambooGroves.Add(item);
            registeredBambooRoots.Add(item.transform);
            EnsureBambooTrigger(item.gameObject);
        }

        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
            Transform candidate = FindBambooCandidateRoot(renderer.transform);
            if (candidate == null || candidate.root == transform.root || registeredBambooRoots.Contains(candidate)) continue;
            ThanhGiongCollectible item = candidate.GetComponent<ThanhGiongCollectible>();
            if (item == null) item = candidate.gameObject.AddComponent<ThanhGiongCollectible>();
            item.kind = ThanhGiongCollectible.Kind.Bamboo;
            item.displayName = "Bụi Tre Ngà";
            item.foodValue = 0f;
            item.spinSpeed = 0f;
            item.visualRoot = candidate;
            EnsureBambooTrigger(candidate.gameObject);
            runtimeBambooGroves.Add(item);
            registeredBambooRoots.Add(candidate);
        }
    }

    private static Transform FindBambooCandidateRoot(Transform start)
    {
        Transform best = null;
        for (Transform t = start; t != null; t = t.parent)
        {
            string path = BuildLowerPath(t);
            if (!LooksLikeBamboo(path)) continue;
            best = t;
            if (t.GetComponent<ThanhGiongCollectible>() != null) return t;
            if (t.parent == null || !LooksLikeBamboo(BuildLowerPath(t.parent))) return t;
        }
        return best;
    }

    private static string BuildLowerPath(Transform t)
    {
        string value = t.name.ToLowerInvariant();
        Transform parent = t.parent;
        int guard = 0;
        while (parent != null && guard++ < 4)
        {
            value = parent.name.ToLowerInvariant() + "/" + value;
            parent = parent.parent;
        }
        return value;
    }

    private static bool LooksLikeBamboo(string lowerPath)
    {
        return lowerPath.Contains("bamboo") || lowerPath.Contains("trenga") || lowerPath.Contains("luytre") || lowerPath.Contains("culm") || lowerPath.Contains("khóm tre") || lowerPath.Contains("khom tre") || lowerPath.Contains("tre ng") || lowerPath.Contains("/tre") || lowerPath.Contains("tre_") || lowerPath.EndsWith("tre");
    }

    private static void EnsureBambooTrigger(GameObject target)
    {
        SphereCollider trigger = null;
        foreach (SphereCollider sphere in target.GetComponents<SphereCollider>())
        {
            if (sphere.isTrigger) { trigger = sphere; break; }
        }
        if (trigger == null)
        {
            trigger = target.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
        }
        trigger.radius = Mathf.Max(trigger.radius, BambooPickupColliderRadius);
        trigger.center = Vector3.up * 1.2f;
    }

    private void CollapsePulledBamboo(ThanhGiongCollectible item)
    {
        if (item == null) return;
        item.MarkCollected();
        Transform visual = item.visualRoot != null ? item.visualRoot : item.transform;
        StartCoroutine(PullBambooVisualDown(visual));
    }

    private IEnumerator PullBambooVisualDown(Transform visual)
    {
        if (visual == null) yield break;
        Vector3 startPos = visual.localPosition;
        Quaternion startRot = visual.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(Random.Range(56f, 74f), Random.Range(-24f, 24f), Random.Range(-18f, 18f));
        float elapsed = 0f;
        while (elapsed < .35f && visual != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / .35f);
            float eased = t * t * (3f - 2f * t);
            visual.localRotation = Quaternion.Slerp(startRot, endRot, eased);
            visual.localPosition = startPos + Vector3.down * (.25f * eased);
            yield return null;
        }
        if (visual != null) visual.gameObject.SetActive(false);
    }

    private static string BambooName(ThanhGiongCollectible item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.displayName) ? item.displayName.ToUpperInvariant() : "BỤI TRE NGÀ";
    }

    private BambooVariant DetermineBambooVariant(ThanhGiongCollectible item)
    {
        if (item == null) return BambooVariant.Standard;
        string path = BuildLowerPath(item.transform);
        string display = (item.displayName ?? string.Empty).ToLowerInvariant();
        string key = path + "/" + display;
        if (key.Contains("fire") || key.Contains("lua") || key.Contains("lửa") || key.Contains("chay") || key.Contains("cháy")) return BambooVariant.FireBamboo;
        if (key.Contains("root") || key.Contains("goc") || key.Contains("gốc") || key.Contains("big") || key.Contains("large")) return BambooVariant.HeavyRoot;
        if (key.Contains("young") || key.Contains("non") || key.Contains("leaf") || key.Contains("la") || key.Contains("lá")) return BambooVariant.SwiftYoung;
        int roll = Mathf.Abs(item.GetEntityId().GetHashCode()) % 4;
        return roll switch { 1 => BambooVariant.SwiftYoung, 2 => BambooVariant.HeavyRoot, 3 => BambooVariant.FireBamboo, _ => BambooVariant.Standard };
    }

    private MeleeProfile GetMeleeProfile(bool bamboo)
    {
        if (!bamboo)
            return new MeleeProfile(false, 25f, 3.25f, 2.05f, .16f, .38f, .32f, 6f, .74f, -.08f);

        return CurrentBambooVariant switch
        {
            BambooVariant.SwiftYoung => new MeleeProfile(true, 34f, 5.1f, 0f, .82f, .54f, .46f, 8f, 1.05f, -1f),
            BambooVariant.HeavyRoot => new MeleeProfile(true, 56f, 6.25f, 0f, 1.32f, .96f, .70f, 9f, 1.55f, -1f),
            BambooVariant.FireBamboo => new MeleeProfile(true, 45f, 5.75f, 0f, 1.05f, .74f, .58f, 16f, 1.32f, -1f),
            _ => new MeleeProfile(true, 43f, 5.6f, 0f, 1.10f, .78f, .58f, 10f, 1.26f, -1f)
        };
    }

    private float CalculateMeleeDamage(MeleeProfile profile, ThanhGiongEnemy enemy, float distance, float radius, int beat)
    {
        float growthFactor = 1f + GrowthPhase * 0.22f;
        float comboFactor = beat == 3 ? 1.22f : beat == 2 ? 1.08f : 1f;
        float speedFactor = 1f + Mathf.Clamp01((movement != null ? movement.PlanarSpeed : 0f) / Mathf.Max(1f, movement != null ? movement.runSpeed : 7f)) * (profile.Bamboo ? .18f : .12f);
        float distanceFactor = Mathf.Lerp(1f, MinDamageFalloff, Mathf.Clamp01(distance / Mathf.Max(radius, .01f)));
        float bossFactor = enemy != null && enemy.isBoss ? .86f : 1f;
        return Mathf.Max(1f, profile.BaseDamage * growthFactor * comboFactor * speedFactor * distanceFactor * bossFactor);
    }

    private float CalculateImpactScale(MeleeProfile profile, float damage, ThanhGiongEnemy enemy, float distance, float radius, int beat)
    {
        float healthReference = enemy != null ? Mathf.Max(20f, enemy.maxHealth) : 75f;
        float damageWeight = Mathf.Sqrt(Mathf.Clamp01(damage / healthReference));
        float centerWeight = Mathf.Lerp(1.18f, .82f, Mathf.Clamp01(distance / Mathf.Max(radius, .01f)));
        float comboWeight = beat == 3 ? 1.2f : 1f;
        float bossWeight = enemy != null && enemy.isBoss ? .62f : 1f;
        return Mathf.Clamp(profile.BaseKnockback * (.72f + damageWeight) * centerWeight * comboWeight * bossWeight, .45f, MaxKnockbackScale);
    }

    private float CalculateChargeDamage(ThanhGiongEnemy enemy)
    {
        float speed = movement != null ? movement.PlanarSpeed : 0f;
        float speedFactor = Mathf.Clamp01((speed - 4.5f) / 5.5f);
        float growthFactor = 1f + GrowthPhase * .16f;
        float bossFactor = enemy != null && enemy.isBoss ? .62f : 1f;
        return Mathf.Lerp(10f, 24f, speedFactor) * growthFactor * bossFactor;
    }

    private void TryPickUpImprovisedWeapon()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, 4.5f);
        foreach (var c in cols)
        {
            ThanhGiongImprovisedWeapon iw = c.GetComponentInParent<ThanhGiongImprovisedWeapon>();
            if (iw != null && iw != CarriedWeapon)
            {
                CarriedWeapon = iw;
                iw.PickUp(transform);
                ShowMessage("ĐÃ NHẶT VŨ KHÍ MÔI TRƯỜNG! [CHUỘT TRÁI ĐỂ NÉM]", 1.8f);
                break;
            }
        }
    }

    public bool TryMeleeAttack()
    {
        if(!IsBattleActive||Time.timeScale<=0||CurrentWeapon==Weapon.None||EquipmentBusy||movement.IsDodging||Time.time<nextAttackTime)return false;
        Attack();return true;
    }

    private void Attack()
    {
        if (CurrentWeapon == Weapon.None || movement.IsDodging) return;
        bool bambooAttack = CurrentWeapon == Weapon.Bamboo;
        MeleeProfile profile = GetMeleeProfile(bambooAttack);
        comboBeat=Time.time<=comboUntil?comboBeat%3+1:1;comboUntil=Time.time+1.1f;
        nextAttackTime=Time.time+profile.Cooldown;
        movement.TriggerCampaignAttack(profile.AttackDuration);
        if(pendingStrike!=null)StopCoroutine(pendingStrike);
        pendingStrike=StartCoroutine(ResolveMeleeStrike(profile,comboBeat));
    }

    private IEnumerator ResolveMeleeStrike(MeleeProfile profile,int beat)
    {
        float windup=0;
        while(windup<.12f) {
            if(!IsBattleActive||movement.IsDodging||EquipmentBusy){pendingStrike=null;yield break;}
            windup+=Time.deltaTime;yield return null;
        }
        while(Time.timeScale<=0)yield return null;
        if(!IsBattleActive||movement.IsDodging){pendingStrike=null;yield break;}
        CharacterController cc=GetComponent<CharacterController>();
        if(cc!=null&&cc.enabled&&cc.isGrounded)cc.Move(transform.forward*(beat==3?.5f:.3f));
        float growthMultiplier = 1f + GrowthPhase * 0.15f;
        float radius = profile.Radius * growthMultiplier;

        Vector3 center = profile.Bamboo ? transform.position : transform.position + transform.forward * profile.ForwardReach;
        int count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Ignore);
        int struck = 0;
        struckEnemies.Clear();

        for (int i = 0; i < count; i++)
        {
            ThanhGiongEnemy enemy = hits[i].GetComponentInParent<ThanhGiongEnemy>();
            if (enemy == null || !enemy.gameObject.activeSelf || enemy.HealthRatio <= 0) continue;
            Vector3 toEnemy = enemy.transform.position - transform.position;
            Vector3 flatToEnemy = Vector3.ProjectOnPlane(toEnemy, Vector3.up);
            if (!profile.Bamboo && flatToEnemy.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, flatToEnemy.normalized) < profile.ArcDot) continue;
            if (!struckEnemies.Add(enemy)) continue;

            float damage = CalculateMeleeDamage(profile, enemy, flatToEnemy.magnitude, radius, beat);
            float impactScale = CalculateImpactScale(profile, damage, enemy, flatToEnemy.magnitude, radius, beat);
            enemy.TakeDamage(damage, profile.Stun, transform.position, impactScale, profile.Bamboo);
            if (profile.Bamboo && CurrentBambooVariant == BambooVariant.FireBamboo)
                enemy.TakeDamage(6f + BattlePressure01 * 6f, .12f, transform.position, .55f, false);
            combatFeedback?.PlayImpact(enemy.transform, profile.Bamboo);
            struck++;
        }

        if (struck > 0)
        {
            audioFx?.PlayImpact(profile.Bamboo);
            Heat = Mathf.Min(maxHeat, Heat + struck * profile.HeatPerEnemy);
            float shake = Mathf.Clamp01(0.16f + struck * 0.07f + (profile.Bamboo ? 0.18f : 0f));
            IsometricCameraFollow.Instance?.Shake(shake, profile.Bamboo ? 0.55f : 0.35f);
        }

        combatFeedback?.PlayAttack(profile.Bamboo, radius);
        if (profile.Bamboo)
        {
            vfx?.PlayBambooSweep(transform, radius);
            if (CurrentBambooVariant == BambooVariant.FireBamboo && struck > 0) vfx?.PlayFireLine(transform, Mathf.Min(16f, radius * 2.2f));
            if (CurrentBambooVariant == BambooVariant.HeavyRoot && struck > 0) IsometricCameraFollow.Instance?.Shake(.75f, .65f);
            audioFx?.PlayBamboo();
        }
        else
        {
            vfx?.PlayCalligraphySlash(transform, radius);
            audioFx?.PlaySlash();
        }
        pendingStrike=null;
    }

    private void CastFireLine()
    {
        Heat = 0f;
        GetComponentInChildren<ThanhGiong.Mounts.HorseController>(true)?.BreatheFire();
        const float length = 28f;
        vfx?.PlayFireLine(transform, length);
        audioFx?.PlayFire();
        IsometricCameraFollow.Instance?.Shake(0.85f, 0.9f);

        Vector3 center = transform.position + transform.forward * (length * .5f);
        int count = Physics.OverlapBoxNonAlloc(center, new Vector3(2.5f, 2.5f, length * .5f), hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            ThanhGiongEnemy enemy = hits[i].GetComponentInParent<ThanhGiongEnemy>();
            if (enemy != null) enemy.TakeDamage(1000f, 2.5f, transform.position);
        }
        ShowMessage("HỎA TUYẾN · THIÊU RỤI ĐỒN TRẠI GIẶC (LÀNG CHÁY)!", 2.2f);
    }

    public void NotifyEnemyDefeated(ThanhGiongEnemy enemy)
    {
        if (arena != null)
        {
            if (arena.NotifyEnemyDefeated(enemy)) Kills++;
            return;
        }
        if (IsDead) return;
        Kills++;
        Heat = Mathf.Min(maxHeat, Heat + (enemy.isBoss ? 35f : 10f));

        if (CurrentWeapon == Weapon.IronSword && Kills >= swordBreakKills)
        {
            CurrentWeapon = Weapon.None;
            vfx?.PlaySwordBreak(transform);
            audioFx?.PlaySwordBreak();
            IsometricCameraFollow.Instance?.Shake(0.9f, 1.2f);

            if (bambooRoot != null) bambooRoot.SetActive(true);
            RegisterSceneBambooGroves();
            ShowMessage("RẮC! GƯƠM SẮT GÃY — ĐỨNG CẠNH BẤT KỲ BỤI TRE NÀO VÀ NHẤN E ĐỂ NHỔ TRE NGÀ!", 4f);
        }

        if (Kills >= victoryKills && CurrentWeapon == Weapon.Bamboo && !isTransitioning)
        {
            StartCoroutine(CompleteStage3AndTransition());
        }
    }

    public bool DamagePlayer(float amount, Vector3 source = default)
    {
        if (!IsBattleActive || Time.timeScale <= 0 || amount <= 0 || Time.time < hurtUntil || movement.IsDodgeInvulnerable) return false;
        Health = Mathf.Max(0, Health - amount);
        hurtUntil = Time.time + Mathf.Max(0, hurtInvulnerabilitySeconds);
        audioFx?.PlayCue("hurt");
        combatFeedback?.PlayImpact(transform, false, true);
        IsometricCameraFollow.Instance?.Shake(.14f, .28f);
        if (IsDead) {
            movement.enabled = false;
            ShowMessage("GIÓNG ĐÃ GỤC NGÃ · NHẤN R ĐỂ THỬ LẠI", 99999f);
        } else {
            ShowMessage($"TRÚNG ĐÒN! MÁU {Mathf.RoundToInt(Health)} / {Mathf.RoundToInt(maxHealth)}", 1f);
            if (source != default) StartCoroutine(HurtPush(source));
        }
        return true;
    }

    public bool HealPlayer(float amount)
    {
        if (IsDead || isTransitioning || amount <= 0 || Time.timeScale <= 0) return false;
        float previous = Health;
        Health = Mathf.Min(maxHealth, Health + amount);
        return Health > previous;
    }

    [System.Serializable]
    public sealed class CheckpointState
    {
        public Chapter chapter; public Weapon weapon;
        public float food, heat, health;
        public int growth, kills, qte, bamboo;
    }

    public CheckpointState CaptureCheckpointState() => new CheckpointState {
        chapter = CurrentChapter, weapon = CurrentWeapon, food = Food, heat = Heat, health = Health,
        growth = GrowthPhase, kills = Kills, qte = qteIndex, bamboo = bambooQte
    };

    public bool RestoreCheckpointState(CheckpointState state, Vector3 position, Quaternion rotation)
    {
        if (state == null || EquipmentBusy || ThanhGiongSceneTransition.IsTransitioning) return false;
        StopAllCoroutines(); clearMessageRoutine = null; pendingStrike=null;comboBeat=0;comboUntil=0;
        isTransitioning = false; hurtUntil = 0; nextAttackTime = 0; chargeHits.Clear();
        CurrentChapter = state.chapter; CurrentWeapon = state.weapon;
        if (CurrentWeapon != Weapon.Bamboo) CurrentBambooVariant = BambooVariant.Standard;
        Food = state.food; Heat = state.heat; Health = Mathf.Clamp(state.health, 1, maxHealth);
        GrowthPhase = state.growth; Kills = state.kills; qteIndex = state.qte; bambooQte = state.bamboo;
        if (CarriedWeapon != null) { CarriedWeapon.Throw(transform.forward); CarriedWeapon = null; }
        ApplyGrowthScale(); TeleportPlayer(position); transform.rotation = rotation;
        movement.enabled = true; movement.ResetMovementState();
        mountedMotion?.SetFlying(false); mountedMotion?.SyncEquipment((int)CurrentChapter, qteIndex);
        if (battleRoot != null) battleRoot.SetActive(CurrentChapter == Chapter.Battle);
        if (bambooRoot != null) bambooRoot.SetActive(CurrentChapter == Chapter.Battle && CurrentWeapon != Weapon.Bamboo);
        ShowMessage("ĐÃ TRỞ LẠI ĐIỂM DỪNG CHÂN", 2f); hud?.RecallPopups();
        return true;
    }

    private IEnumerator HurtPush(Vector3 source)
    {
        CharacterController cc = GetComponent<CharacterController>();
        Vector3 away = transform.position - source; away.y = 0;
        Vector3 impulse = away.normalized * 6f;
        float elapsed = 0;
        while (elapsed < .2f && !IsDead && cc != null && cc.enabled) {
            elapsed += Time.deltaTime;
            cc.Move(impulse * (1 - Mathf.Clamp01(elapsed / .2f)) * Time.deltaTime);
            yield return null;
        }
    }

    public void RestartBattle()
    {
        if (arena != null) { arena.RestartRound(); return; }
        if (CurrentChapter != Chapter.Battle || isTransitioning) return;
        StopAllCoroutines(); clearMessageRoutine = null; pendingStrike=null;comboBeat=0;comboUntil=0;
        Health = maxHealth; hurtUntil = 0; nextAttackTime = 0;
        Kills = 0; qteIndex = 0; bambooQte = 0;
        chargeHits.Clear();
        CurrentWeapon = Weapon.IronSword; CurrentBambooVariant = BambooVariant.Standard; Heat = maxHeat * .5f;
        if (CarriedWeapon != null) { CarriedWeapon.Throw(transform.forward); CarriedWeapon = null; }
        TeleportPlayer(battleSpawn); transform.rotation = battleSpawnRotation;
        movement.enabled = true;
        movement.ResetMovementState();
        mountedMotion?.SetFlying(false);
        mountedMotion?.SyncEquipment((int)CurrentChapter, 0);
        if (bambooRoot != null) bambooRoot.SetActive(true);
        if (battleRoot != null) {
            battleRoot.SetActive(true);
            foreach (ThanhGiongEnemy enemy in battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true))
                enemy.ResetForBattle(this, transform);
        }
        ShowMessage("TRỞ LẠI CHIẾN TUYẾN · GIÓNG TIẾP CHIẾN!", 3f);
        hud?.RecallPopups();
    }

    private void UpdateAscensionQte()
    {
        if (isTransitioning) return;

        if (qteIndex < equipQte.Length && Input.GetKeyDown(equipQte[qteIndex]))
        {
            qteIndex++;
            if (qteIndex == 1) mountedMotion?.PlayEquipment("remove_armor");
            if (qteIndex == 2) mountedMotion?.PlayEquipment("remove_helmet");
            if (qteIndex <= 2) audioFx?.PlayEquipment();
            IsometricCameraFollow.Instance?.Shake(0.3f, 0.45f);

            if (qteIndex == 1) ShowMessage("ĐẶT MẢNH GIÁP THỨ NHẤT XUỐNG ĐỈNH NÚI SÓC... [BƯỚC TIẾP: NHẤN 'Q' ĐẶT NÓN SẮT]", 2.2f);
            else if (qteIndex == 2) ShowMessage("ĐẶT NÓN SẮT XUỐNG ĐỈNH NÚI — TRỞ THÀNH DI TÍCH LỊCH SỬ! [BƯỚC TIẾP: NHẤN 'E' CƯỠI NGỰA VỀ TRỜI]", 2.5f);
            else if (qteIndex == 3)
            {
                audioFx?.PlayHorseRoar();
                IsometricCameraFollow.Instance?.Shake(1.1f, 1.2f);
                ShowMessage("NGỰA SẮT HÍ VANG DỘI TRỜI CAO — CƯỠI NGỰA VỀ TRỜI!", 3.0f);
                StartCoroutine(FlyToSky());
            }
        }
    }

    private IEnumerator CompleteStage1AndTransition()
    {
        isTransitioning = true;
        ShowMessage("★ HOÀN THÀNH MÀN 1 ★\nGIÓNG ĐÃ LỚN VỌT THÀNH TRÁNG SĨ KHỔNG LỒ!\nĐANG CHUYỂN ĐẾN KHU VỰC SỨ GIẢ & LÒ RÈN TRIỀU ĐÌNH...", 3.2f);
        IsometricCameraFollow.Instance?.Shake(0.6f, 0.7f);
        yield return new WaitForSeconds(2.6f);

        if (ThanhGiongSceneTransition.TryTransitionTo("KinhThanhRenThep", "MÀN 2 · RÈN THÉP & XUẤT QUÂN"))
            yield break;

        TeleportPlayer(new Vector3(-12f, 0f, -6f));
        BeginPreparation();
        isTransitioning = false;
    }

    private IEnumerator CompleteStage2AndTransition()
    {
        isTransitioning = true;
        while (mountedMotion != null && mountedMotion.IsBusy) yield return null;
        CastFireLine();
        ShowMessage("★ HOÀN THÀNH MÀN 2 ★\nĐÃ TRANG BỊ ĐẦY ĐỦ VŨ KHÍ & NGỰA CHIẾN!\nXUẤT QUÂN RA TIỀN TUYẾN NÚI SÓC ĐỂ CÀN QUÉT GIẶC ÂN...", 3.2f);
        IsometricCameraFollow.Instance?.Shake(0.7f, 0.8f);
        yield return new WaitForSeconds(2.6f);

        if (ThanhGiongSceneTransition.TryTransitionTo("PhaoDaiNgamQuanAn", "MÀN 3 · PHÁO ĐÀI NGẦM QUÂN ÂN"))
            yield break;

        TeleportPlayer(new Vector3(-3f, 0f, 1f));
        BeginBattle();
        isTransitioning = false;
    }

    private IEnumerator CompleteStage3AndTransition()
    {
        isTransitioning = true;
        ShowMessage("★ HOÀN THÀNH MÀN 3 ★\nQUÂN ÂN ĐẠI BẠI, TƯỚNG GIẶC ĐÃ BỊ TIÊU DIỆT!\nTIẾN LÊN ĐỈNH NÚI SÓC ĐỂ HÓA THÁNH VỀ TRỜI...", 3.5f);
        IsometricCameraFollow.Instance?.Shake(0.8f, 1.0f);
        yield return new WaitForSeconds(2.8f);

        if (ThanhGiongSceneTransition.TryTransitionToNext("CHIẾN TUYẾN MỚI ĐANG MỞ RA..."))
            yield break;

        TeleportPlayer(new Vector3(36f, 48.5f, 48f));
        BeginAscension();
        isTransitioning = false;
    }

    private IEnumerator FlyToSky()
    {
        isTransitioning = true;
        while (mountedMotion != null && mountedMotion.IsBusy) yield return null;
        mountedMotion?.SetFlying(true);
        audioFx?.PlayAscensionMusic();
        movement.enabled = false;
        AscensionCloudTrail cloudTrail = GetComponent<AscensionCloudTrail>();
        cloudTrail?.BeginAscent();
        Vector3 start = transform.position;
        Vector3 end = ascensionTarget != null ? ascensionTarget.position : start + new Vector3(40f, 55f, 40f);

        float time = 0f;
        const float flyDuration = 8.5f;

        // Overview camera looking over the geographic map
        IsometricCameraFollow.Instance?.SetOverview(start + new Vector3(-25f, 42f, -25f), start + new Vector3(15f, 0f, 15f));

        bool ascensionDissolved = false;
        while (time < flyDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / flyDuration);
            // Smooth ascent curve
            transform.position = Vector3.Lerp(start, end, t * t * (3f - 2f * t));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation((end - start).normalized), Time.deltaTime * 2.5f);
            cloudTrail?.FollowHorse(transform.position, transform.rotation);

            // Periodically drop fire sparks and create lakes below
            if (time > 1.2f && time < 6.5f)
            {
                if (Mathf.FloorToInt(time * 3f) != Mathf.FloorToInt((time - Time.deltaTime) * 3f))
                {
                    Vector3 dropGround = new Vector3(transform.position.x + Random.Range(-6f, 6f), 0.5f, transform.position.z + Random.Range(-6f, 6f));
                    vfx?.SpawnLegendaryRemains(transform.position, dropGround);
                }
            }

            // Divine Golden Dissolve into clouds (Hóa Thánh Về Trời)
            if (t >= 0.7f && !ascensionDissolved)
            {
                ascensionDissolved = true;
                vfx?.PlayAscensionDivineLight(transform.position);
                ThanhGiongGoldenDissolve heroDissolve = GetComponent<ThanhGiongGoldenDissolve>();
                if (heroDissolve == null) heroDissolve = gameObject.AddComponent<ThanhGiongGoldenDissolve>();
                StartCoroutine(heroDissolve.Play(2.2f, new Color(1.0f, 0.92f, 0.35f, 1f), 90, true));
            }

            yield return null;
        }

        cloudTrail?.EndAscent();
        CurrentChapter = Chapter.Complete;
        ShowMessage("THÁNH GIÓNG · HÓA THÁNH VỀ TRỜI · TRUYỀN THUYẾT & DI SẢN BẤT TỬ", 999f);
    }

    private void BeginPreparation()
    {
        CurrentChapter = Chapter.Preparation;
        GrowthPhase = 3;
        ApplyGrowthScale();
        CurrentWeapon = Weapon.None;
        qteIndex = 0;
        mountedMotion?.SyncEquipment((int)CurrentChapter, qteIndex);
        if (battleRoot != null) battleRoot.SetActive(false);
        if (bambooRoot != null) bambooRoot.SetActive(false);
        ShowMessage("BƯỚC VÀO MÀN 2: RÈN THÉP & XUẤT QUÂN!\n[NHẤN 'E': MẶC ÁO GIÁP VÀNG VUA BAN]", 3.8f);
    }

    private void BeginBattle()
    {
        CurrentChapter = Chapter.Battle;
        battleSpawn = transform.position;
        battleSpawnRotation = transform.rotation;
        GrowthPhase = 3;
        ApplyGrowthScale();
        CurrentWeapon = Weapon.IronSword;
        mountedMotion?.SyncEquipment((int)CurrentChapter, 0);
        Heat = maxHeat * 0.5f;
        Kills = 0;
        if (battleRoot != null)
        {
            battleRoot.SetActive(true);
            foreach (ThanhGiongEnemy enemy in battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true))
                enemy.Initialize(this, transform);
        }
        if (bambooRoot != null) bambooRoot.SetActive(true);
        ShowMessage("BƯỚC VÀO MÀN 3: TRẬN TUYẾN NÚI SÓC!\nCÀN QUÉT QUÂN GIẶC BẰNG GƯƠM SẮT & NGỰA SẮT!", 3.5f);
    }

    private void BeginAscension()
    {
        CurrentChapter = Chapter.Ascension;
        GrowthPhase = 3;
        ApplyGrowthScale();
        CurrentWeapon = Weapon.Bamboo;
        if (battleRoot != null) battleRoot.SetActive(false);
        movement.enabled = true;
        qteIndex = 0;
        mountedMotion?.SyncEquipment((int)CurrentChapter, qteIndex);
        ShowMessage("BƯỚC VÀO MÀN 4: HÓA THÁNH VỀ TRỜI!\n[NHẤN 'E': CỞI MẢNH GIÁP ĐẦU TIÊN ĐẶT LÊN ĐỈNH NÚI SÓC]", 4f);
    }

    private void ApplyGrowthScale()
    {
        // Dynamic scale from boy (0.42) to huge titan (1.45)
        float[] scales = { 0.42f, 0.72f, 1.05f, 1.45f };
        float scale = scales[GrowthPhase];
        transform.localScale = originalScale * scale;

        // Dynamic speed based on scale
        movement.walkSpeed = Mathf.Lerp(3.8f, 6.2f, GrowthPhase / 3f);
        movement.runSpeed = Mathf.Lerp(6.2f, 11.5f, GrowthPhase / 3f);
    }

    public void ShowMessage(string message, float duration)
    {
        if (clearMessageRoutine != null) StopCoroutine(clearMessageRoutine);
        CenterMessage = message;
        clearMessageRoutine = StartCoroutine(ClearMessage(duration));
    }

    private IEnumerator ClearMessage(float duration)
    {
        yield return new WaitForSeconds(duration);
        CenterMessage = string.Empty;
        clearMessageRoutine = null;
    }
}

