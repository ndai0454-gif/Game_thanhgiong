using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OpenWorldRisk { Safe, Blue, Yellow, Red, Black }
public enum OpenWorldBiome { Forest, Mountain, Highland, Steppe, Swamp }
public enum OpenWorldResource { Wood, Stone, Ore, Hide, Fiber }
public enum OpenWorldForgeRecipe { Supplies, RepairEquipment }

[Serializable]
public sealed class OpenWorldEconomyState
{
    public int[] carried = new int[5];
    public int[] stored = new int[5];
    public float durability = 100f;
    public bool forged;
}

[Serializable]
public sealed class OpenWorldZone
{
    public string id;
    public string displayName;
    public Vector3 center;
    public Vector3 size = new Vector3(120f, 1000f, 120f);
    public OpenWorldRisk risk;
    public OpenWorldBiome biome;
    [Range(0, 3)] public int chapter;
    public bool Contains(Vector3 point) => Mathf.Abs(point.x - center.x) <= Mathf.Abs(size.x) * .5f &&
        Mathf.Abs(point.z - center.z) <= Mathf.Abs(size.z) * .5f;
}

[Serializable]
public sealed class OpenWorldConnection
{
    public string from;
    public string to;
    public OpenWorldConnection() { }
    public OpenWorldConnection(string fromId, string toId) { from = fromId; to = toId; }
}

/// <summary>Continuous spatial zones and a session-only economy. Never loads scenes or changes legacy saves.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)]
public sealed class ThanhGiongOpenWorld : MonoBehaviour
{
    public ThanhGiongCampaignController campaign;
    public Transform player;
    public Vector3 safeSpawn = new Vector3(0f, 1f, 0f);
    public List<OpenWorldZone> zones = new List<OpenWorldZone>();
    public List<OpenWorldConnection> connections = new List<OpenWorldConnection>();
    [SerializeField, Range(1f, 20f)] private float interactionRadius = 6f;
    [SerializeField, Range(0f, 100f)] private float durability = 100f;
    [SerializeField] private int[] carriedResources = new int[5];
    [SerializeField] private int[] storedResources = new int[5];
    [SerializeField] private bool hasForgedSupplies;

    public OpenWorldZone CurrentZone { get; private set; }
    public bool IsSafe => CurrentZone != null && (CurrentZone.risk == OpenWorldRisk.Safe || CurrentZone.risk == OpenWorldRisk.Blue);
    public bool CanTakeDamage => !IsSafe && !recovering;
    public bool HasForgedSupplies => hasForgedSupplies;
    public float Durability => durability;
    public float CombatDamageMultiplier => Mathf.Lerp(.55f, 1f, durability / 100f);
    public bool MapOpen { get; private set; }
    public bool IsRecovering => recovering;
    public string Notice { get; private set; }
    public string InteractionPrompt { get; private set; }
    public event Action<OpenWorldZone> ZoneChanged;
    public event Action EconomyChanged;

    private static readonly OpenWorldResource[,] BiomeResources = {
        { OpenWorldResource.Wood, OpenWorldResource.Hide, OpenWorldResource.Stone },
        { OpenWorldResource.Ore, OpenWorldResource.Stone, OpenWorldResource.Fiber },
        { OpenWorldResource.Stone, OpenWorldResource.Ore, OpenWorldResource.Wood },
        { OpenWorldResource.Hide, OpenWorldResource.Fiber, OpenWorldResource.Ore },
        { OpenWorldResource.Fiber, OpenWorldResource.Wood, OpenWorldResource.Hide }
    };
    private static readonly string[] ResourceLabels = { "Tre / gỗ", "Đá / than", "Quặng sắt", "Da thú", "Sợi vải" };
    private static readonly string[] BiomeLabels = { "Rừng tre", "Núi khoáng", "Cao nguyên", "Đồng lúa", "Đầm lầy" };
    private static readonly string[] RiskLabels = { "AN TOÀN", "XANH DƯƠNG · T1–T4", "VÀNG · T5", "ĐỎ · T6–T7", "ĐEN · T8" };
    private static readonly Color[] RiskColors = {
        new Color(.40f, .55f, .44f), new Color(.40f, .52f, .60f), new Color(.65f, .57f, .37f),
        new Color(.63f, .37f, .31f), new Color(.47f, .41f, .53f)
    };
    public OpenWorldZone Destination { get; private set; }
    public IReadOnlyList<OpenWorldZone> Route => route;
    private readonly List<OpenWorldZone> route = new List<OpenWorldZone>();
    private bool recovering;
    private Coroutine recoveryRoutine;
    private float mapPreviousTimeScale = 1f;
    private float noticeUntil;
    private OpenWorldZone selectedZone;
    private ThanhGiongResourceNode nearestNode;
    private ThanhGiongForgeStation nearestForge;
    private GUIStyle titleStyle, bodyStyle, smallStyle, centeredStyle, buttonStyle, mapRegionTitle;
    private Rect worldBounds;

    private void Awake()
    {
        if (campaign == null) campaign = FindAnyObjectByType<ThanhGiongCampaignController>();
        if (player == null && campaign != null) player = campaign.transform;
        EnsureInventory();
    }

    private void Start() { RecalculateBounds(); RefreshCurrentZone(); }

    private void Update()
    {
        RefreshCurrentZone();
        ClearDestinationIfReached();
        if (Input.GetKeyDown(KeyCode.M)) SetMapOpen(!MapOpen);
        if (Time.unscaledTime > noticeUntil) Notice = string.Empty;
        if (recovering || Time.timeScale <= 0f || (campaign != null && campaign.EquipmentBusy)) return;
        UpdateNearestInteraction();
        if (MapOpen) return;
        if (Input.GetKeyDown(KeyCode.G)) {
            if (nearestForge != null) nearestForge.TryInteract(this);
            else if (nearestNode != null && !nearestNode.TryHarvest(this)) ShowNotice("Chưa thu thập được. Đến sát điểm tài nguyên rồi nhấn G lại.", 2.5f);
            else if (nearestNode == null) ShowNotice("Không có tài nguyên hay lò rèn gần đây.", 2f);
        }
        if (Input.GetKeyDown(KeyCode.B) && nearestForge != null) TryStoreCarriedResources();
    }

    private void EnsureInventory()
    {
        if (carriedResources == null || carriedResources.Length != 5) carriedResources = new int[5];
        if (storedResources == null || storedResources.Length != 5) storedResources = new int[5];
    }

    public OpenWorldZone FindZone(Vector3 position)
    {
        OpenWorldZone found = null;
        float smallest = float.MaxValue;
        foreach (var zone in zones) {
            if (zone == null || !zone.Contains(position)) continue;
            float area = Mathf.Abs(zone.size.x * zone.size.z);
            if (area < smallest) { smallest = area; found = zone; }
        }
        if (found != null) return found;
        // Roads occupy the spaces between zone cores; the nearest boundary owns that land.
        float nearestDistance = float.MaxValue;
        foreach (var zone in zones) {
            if (zone == null) continue;
            float dx = Mathf.Max(0f, Mathf.Abs(position.x - zone.center.x) - Mathf.Abs(zone.size.x) * .5f);
            float dz = Mathf.Max(0f, Mathf.Abs(position.z - zone.center.z) - Mathf.Abs(zone.size.z) * .5f);
            float distance = dx * dx + dz * dz;
            if (distance < nearestDistance) { nearestDistance = distance; found = zone; }
        }
        return found;
    }

    public void RefreshCurrentZone()
    {
        if (player == null) return;
        var zone = FindZone(player.position);
        if (zone == CurrentZone) return;
        CurrentZone = zone;
        RebuildRoute();
        ZoneChanged?.Invoke(zone);
    }

    public int GetCarriedAmount(OpenWorldResource resource) { EnsureInventory(); return carriedResources[(int)resource]; }
    public int GetStoredAmount(OpenWorldResource resource) { EnsureInventory(); return storedResources[(int)resource]; }
    public OpenWorldEconomyState CaptureEconomy()
    {
        EnsureInventory();
        return new OpenWorldEconomyState {
            carried = (int[])carriedResources.Clone(), stored = (int[])storedResources.Clone(),
            durability = durability, forged = hasForgedSupplies
        };
    }

    public bool RestoreEconomy(OpenWorldEconomyState state)
    {
        if (state == null || state.carried == null || state.stored == null || state.carried.Length != 5 || state.stored.Length != 5 ||
            float.IsNaN(state.durability) || float.IsInfinity(state.durability) || state.durability < 0f || state.durability > 100f) return false;
        for (int i = 0; i < 5; i++) if (state.carried[i] < 0 || state.stored[i] < 0) return false;
        carriedResources = (int[])state.carried.Clone(); storedResources = (int[])state.stored.Clone();
        durability = state.durability; hasForgedSupplies = state.forged;
        EconomyChanged?.Invoke();
        return true;
    }
    public static string ResourceLabel(OpenWorldResource resource) => ResourceLabels[(int)resource];
    public static string BiomeLabel(OpenWorldBiome biome) => BiomeLabels[(int)biome];
    public static string RiskLabel(OpenWorldRisk risk) => RiskLabels[(int)risk];
    public static Color RiskColor(OpenWorldRisk risk) => RiskColors[(int)risk];
    public static OpenWorldResource ResourceFor(OpenWorldBiome biome, int abundanceIndex) => BiomeResources[(int)biome, Mathf.Clamp(abundanceIndex, 0, 2)];
    public static int ResourceAbundance(OpenWorldBiome biome, OpenWorldResource resource)
    {
        for (int i = 0; i < 3; i++) if (ResourceFor(biome, i) == resource) return i;
        return -1;
    }
    public static int BaseGatherYield(OpenWorldBiome biome, OpenWorldResource resource)
    {
        int rank = ResourceAbundance(biome, resource);
        return rank == 0 ? 6 : rank == 1 ? 3 : rank == 2 ? 1 : 0;
    }

    public bool TryGather(OpenWorldResource resource, int amount) => TryGather(resource, amount, CurrentZone);

    public bool TryGather(OpenWorldResource resource, int amount, OpenWorldZone zone)
    {
        if (zone == null || zone.risk == OpenWorldRisk.Safe || recovering || amount <= 0 ||
            ResourceAbundance(zone.biome, resource) < 0) return false;
        EnsureInventory();
        if (carriedResources[(int)resource] > 9999 - amount) return false;
        carriedResources[(int)resource] += amount;
        EconomyChanged?.Invoke();
        ShowNotice("+" + amount + " " + ResourceLabel(resource) + " · mang về lò rèn hoặc cất tại làng", 2.8f);
        return true;
    }

    public bool TryForge(OpenWorldForgeRecipe recipe)
    {
        if (CurrentZone == null || CurrentZone.risk != OpenWorldRisk.Safe || recovering) {
            ShowNotice("Rèn và sửa trang bị tại khu an toàn Làng Phù Đổng.", 3f); return false;
        }
        EnsureInventory();
        if (recipe == OpenWorldForgeRecipe.Supplies) {
            if (hasForgedSupplies) { ShowNotice("Đã đúc đủ vật tư cho trang bị vua ban.", 3f); return false; }
            if (!CanSpend(OpenWorldResource.Ore, 6) || !CanSpend(OpenWorldResource.Wood, 3)) {
                ShowNotice("Cần 6 Quặng sắt + 3 Tre / gỗ. Tìm mỏ núi và rừng tre vùng Xanh.", 4f); return false;
            }
            Spend(OpenWorldResource.Ore, 6); Spend(OpenWorldResource.Wood, 3);
            hasForgedSupplies = true; durability = 100f;
            ShowNotice("ĐÚC THÀNH CÔNG · Vật tư Ngựa Sắt và Roi Sắt sẵn sàng. Tiếp tục nghi thức trang bị E → Q → E → F.", 6f);
        } else {
            if (durability >= 100f) { ShowNotice("Trang bị đã ở độ bền tối đa.", 3f); return false; }
            if (!CanSpend(OpenWorldResource.Ore, 2) || !CanSpend(OpenWorldResource.Stone, 1)) {
                ShowNotice("Sửa trang bị cần 2 Quặng sắt + 1 Đá / than.", 3f); return false;
            }
            Spend(OpenWorldResource.Ore, 2); Spend(OpenWorldResource.Stone, 1); durability = 100f;
            ShowNotice("ĐÃ SỬA TRANG BỊ · Độ bền 100% · Sức đánh phục hồi.", 4f);
        }
        EconomyChanged?.Invoke();
        return true;
    }

    private bool CanSpend(OpenWorldResource resource, int amount) => (long)GetCarriedAmount(resource) + GetStoredAmount(resource) >= amount;
    private void Spend(OpenWorldResource resource, int amount)
    {
        int carried = Mathf.Min(carriedResources[(int)resource], amount);
        carriedResources[(int)resource] -= carried;
        storedResources[(int)resource] -= amount - carried;
    }

    public bool TryStoreCarriedResources()
    {
        if (CurrentZone == null || CurrentZone.risk != OpenWorldRisk.Safe || recovering) return false;
        EnsureInventory();
        long total = 0;
        for (int i = 0; i < 5; i++) {
            total += carriedResources[i];
            if ((long)storedResources[i] + carriedResources[i] > int.MaxValue) { ShowNotice("Kho làng đã đầy.", 3f); return false; }
        }
        if (total == 0) { ShowNotice("Túi mang theo đang trống.", 3f); return false; }
        for (int i = 0; i < 5; i++) { storedResources[i] += carriedResources[i]; carriedResources[i] = 0; }
        EconomyChanged?.Invoke();
        ShowNotice("ĐÃ CẤT " + total + " tài nguyên · Kho làng được bảo toàn khi gục ngã.", 4f);
        return true;
    }

    public void HandleDefeat()
    {
        if (recovering || campaign == null) return;
        RefreshCurrentZone();
        var risk = CurrentZone != null ? CurrentZone.risk : OpenWorldRisk.Red;
        var state = campaign.CaptureCheckpointState();
        state.health = campaign.maxHealth;
        if (risk == OpenWorldRisk.Yellow) {
            durability = Mathf.Max(0f, durability - 15f);
            ShowNotice("BỊ ĐÁNH GỤC · Giữ toàn bộ tài nguyên · Độ bền −15% · Trở về làng sau 2 giây.", 6f);
        } else if (risk == OpenWorldRisk.Red || risk == OpenWorldRisk.Black) {
            EnsureInventory();
            Array.Clear(carriedResources, 0, carriedResources.Length);
            durability = Mathf.Max(0f, durability - (risk == OpenWorldRisk.Black ? 25f : 20f));
            ShowNotice("GỤC NGÃ VÙNG " + (risk == OpenWorldRisk.Black ? "ĐEN" : "ĐỎ") + " · Mất tài nguyên mang theo · Kho làng và trang bị cốt truyện được giữ.", 7f);
        } else ShowNotice("ĐƯỢC DÂN LÀNG CỨU · Trở về khu an toàn.", 4f);
        EconomyChanged?.Invoke();
        recovering = true;
        var movement = campaign.GetComponent<MountedHorseController>();
        if (movement != null) movement.enabled = false;
        recoveryRoutine = StartCoroutine(RecoverAtHub(state, risk == OpenWorldRisk.Yellow ? 2f : 1.5f));
    }

    private IEnumerator RecoverAtHub(ThanhGiongCampaignController.CheckpointState state, float delay)
    {
        yield return new WaitForSeconds(delay);
        while (campaign != null && !campaign.RestoreCheckpointState(state, safeSpawn, Quaternion.identity))
            yield return null;
        recovering = false;
        recoveryRoutine = null;
        RefreshCurrentZone();
        EconomyChanged?.Invoke();
        ShowNotice("ĐÃ HỒI PHỤC TẠI LÀNG · Độ bền " + Mathf.RoundToInt(durability) + "% · G tại lò rèn để sửa / B cất tài nguyên.", 5f);
    }

    // ---- Route guidance: breadth-first search over the zone graph from the current zone to Destination.
    public void SetDestination(OpenWorldZone zone)
    {
        Destination = zone == Destination ? null : zone;
        RebuildRoute();
        ShowNotice(Destination == null ? "Đã hủy chỉ đường." : "Chỉ đường đến " + Destination.displayName + " · theo mũi tên và vạch vàng trên bản đồ.", 4f);
    }

    public void ClearDestinationIfReached()
    {
        if (Destination != null && CurrentZone == Destination && player != null && Flat(Destination.center - player.position).magnitude < 28f) {
            ShowNotice("Đã đến " + Destination.displayName + ".", 3f); Destination = null; RebuildRoute();
        }
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private void RebuildRoute()
    {
        route.Clear();
        if (Destination == null || CurrentZone == null) return;
        var previous = new Dictionary<string, OpenWorldZone>();
        var queue = new Queue<OpenWorldZone>();
        queue.Enqueue(CurrentZone); previous[CurrentZone.id] = null;
        while (queue.Count > 0) {
            var zone = queue.Dequeue();
            if (zone == Destination) break;
            foreach (var link in connections) {
                if (link == null) continue;
                string other = link.from == zone.id ? link.to : link.to == zone.id ? link.from : null;
                if (other == null || previous.ContainsKey(other)) continue;
                var next = zones.Find(z => z != null && z.id == other);
                if (next == null) continue;
                previous[other] = zone; queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(Destination.id)) return;
        for (var z = Destination; z != null; z = previous[z.id]) route.Insert(0, z);
    }

    /// <summary>World position the player should head to next (next zone on the route, or the destination itself).</summary>
    public bool TryGetGuidanceTarget(out Vector3 target)
    {
        target = default;
        if (Destination == null || player == null) return false;
        target = Destination.center;
        if (route.Count >= 2) {
            // Skip waypoints already close behind us; route[0] is the current zone.
            for (int i = 1; i < route.Count; i++) {
                target = route[i].center;
                if (Flat(target - player.position).sqrMagnitude > 22f * 22f) break;
            }
        }
        return true;
    }

    public void ShowNotice(string message, float duration = 3f) { Notice = message; noticeUntil = Time.unscaledTime + duration; }
    public void CancelPendingRecovery()
    {
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
        recoveryRoutine = null; recovering = false;
    }
    public void SetMapOpen(bool value)
    {
        if (value == MapOpen) return;
        MapOpen = value;
        if (value) {
            mapPreviousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            selectedZone = CurrentZone; RecalculateBounds(); Time.timeScale = 0f;
        } else Time.timeScale = campaign != null && campaign.hud != null && campaign.hud.IsPaused ? 0f : mapPreviousTimeScale;
    }

    private void OnDisable() { if (MapOpen) SetMapOpen(false); }

    private void UpdateNearestInteraction()
    {
        nearestForge = null; nearestNode = null; InteractionPrompt = string.Empty;
        if (player == null) return;
        float closest = interactionRadius * interactionRadius;
        foreach (var forge in ThanhGiongForgeStation.ActiveStations) {
            if (forge == null || !forge.isActiveAndEnabled) continue;
            float distance = (forge.transform.position - player.position).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance; nearestForge = forge;
        }
        if (nearestForge != null) {
            InteractionPrompt = !hasForgedSupplies ? "[G] Đúc vật tư: 6 quặng + 3 tre   [B] Cất tài nguyên" :
                durability < 100f ? "[G] Sửa: 2 quặng + 1 đá / than   [B] Cất tài nguyên" : "Lò rèn · Trang bị hoàn hảo   [B] Cất tài nguyên";
            return;
        }
        foreach (var node in ThanhGiongResourceNode.ActiveNodes) {
            if (node == null || !node.isActiveAndEnabled || !node.IsReady) continue;
            float distance = (node.transform.position - player.position).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance; nearestNode = node;
        }
        if (nearestNode != null) InteractionPrompt = "[G] Thu thập " + ResourceLabel(nearestNode.resource) + "   ·   [M] Bản đồ lục địa";
    }

    public bool IsWithinInteractionRange(Vector3 position) => player != null && (player.position - position).sqrMagnitude <= interactionRadius * interactionRadius;

    private void RecalculateBounds()
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var zone in zones) {
            if (zone == null) continue;
            minX = Mathf.Min(minX, zone.center.x - zone.size.x * .5f); maxX = Mathf.Max(maxX, zone.center.x + zone.size.x * .5f);
            minZ = Mathf.Min(minZ, zone.center.z - zone.size.z * .5f); maxZ = Mathf.Max(maxZ, zone.center.z + zone.size.z * .5f);
        }
        worldBounds = minX < maxX && minZ < maxZ ? new Rect(minX - 20f, minZ - 20f, maxX - minX + 40f, maxZ - minZ + 40f) : new Rect(-400f, -400f, 800f, 800f);
    }

    private void OnGUI()
    {
        if (campaign != null && campaign.hud != null && campaign.hud.IsPaused && !MapOpen) return;
        EnsureStyles();
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * scale) * .5f, (Screen.height - 720f * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);
        if (MapOpen) DrawWorldMap();
        else DrawTravelHud();
        GUI.color = oldColor; GUI.matrix = oldMatrix;
    }

    private void EnsureStyles()
    {
        if (bodyStyle != null) return;
        bodyStyle = new GUIStyle(GUI.skin.label) { font = ThanhGiongUITheme.Regular, fontSize = 17, wordWrap = true, normal = { textColor = ThanhGiongUITheme.Paper } };
        smallStyle = new GUIStyle(bodyStyle) { fontSize = 14 };
        titleStyle = new GUIStyle(bodyStyle) { fontSize = 30, font = ThanhGiongUITheme.Light, fontStyle = FontStyle.Normal };
        centeredStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        buttonStyle = ThanhGiongUITheme.Button(17);
        mapRegionTitle = new GUIStyle(titleStyle) { fontSize = 24 };
    }

    private void DrawTravelHud()
    {
        Rect panel = new Rect(20f, 628f, 270f, 69f);
        var risk = CurrentZone != null ? CurrentZone.risk : OpenWorldRisk.Red;
        ThanhGiongUITheme.ShadowLabel(new Rect(30f, 635f, 250f, 21f), CurrentZone != null ? CurrentZone.displayName : "Đường liên vùng", bodyStyle);
        GUI.color = RiskColor(risk); ThanhGiongUITheme.ShadowLabel(new Rect(30f, 657f, 250f, 19f), RiskLabel(risk) + " · Bền " + Mathf.RoundToInt(durability) + "%", smallStyle); GUI.color = Color.white;
        ThanhGiongUITheme.ShadowLabel(new Rect(30f, 677f, 250f, 19f), "[M] Bản đồ · [G] Thu thập / rèn", smallStyle);
        DrawGuidanceArrow();
        if (!string.IsNullOrEmpty(InteractionPrompt)) {
            bool ritual = campaign != null && (campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Preparation || campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Ascension);
            float promptY = ritual ? 510f : 621f;
            ThanhGiongUITheme.ShadowLabel(new Rect(325f, promptY + 6f, 630f, 25f), InteractionPrompt, new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter });
        }
        if (!string.IsNullOrEmpty(Notice)) {
            // Full text is always shown: the box grows with wrapped lines instead of clipping long notices.
            var noticeStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            float noticeHeight = Mathf.Max(30f, noticeStyle.CalcHeight(new GUIContent(Notice), 760f) + 6f);
            ThanhGiongUITheme.ShadowLabel(new Rect(260f, 118f, 760f, noticeHeight), Notice, noticeStyle);
        }
    }

    private void DrawGuidanceArrow()
    {
        if (!TryGetGuidanceTarget(out Vector3 target) || Camera.main == null) return;
        Vector3 toTarget = Flat(target - player.position);
        float distance = toTarget.magnitude;
        Vector3 forward = Flat(Camera.main.transform.forward);
        if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
        float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up); // + = clockwise on the screen
        Vector2 centre = new Vector2(640f, 92f);
        Color guide = new Color(1f, .86f, .30f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = old * Matrix4x4.TRS(new Vector3(centre.x, centre.y, 0f), Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Fill(new Rect(-3f, -6f, 6f, 30f), guide);                       // shaft (points to -y = up when angle is 0)
        DrawLine(new Vector2(0f, -22f), new Vector2(-13f, -4f), guide, 5f);
        DrawLine(new Vector2(0f, -22f), new Vector2(13f, -4f), guide, 5f);
        GUI.matrix = old;
        var style = new GUIStyle(centeredStyle) { fontSize = 15, wordWrap = false };
        ThanhGiongUITheme.ShadowLabel(new Rect(centre.x - 150f, centre.y + 28f, 300f, 22f), Destination.displayName + " · " + Mathf.RoundToInt(distance) + " m", style);
    }

    private void DrawWorldMap()
    {
        Fill(new Rect(0f, 0f, 1280f, 720f), new Color(.065f, .075f, .065f, .98f));
        GUI.Label(new Rect(32f, 20f, 970f, 38f), "VĂN LANG · MỘT LỤC ĐỊA, BỐN CHẶNG TRUYỀN THUYẾT", titleStyle);
        GUI.Label(new Rect(34f, 62f, 920f, 27f), "Nhấp vùng để xem luật · Đường nối: lối đi · Chấm sáng: Gióng · Ô nhỏ: tài nguyên · Bản đồ tạm dừng trò chơi", bodyStyle);
        if (GUI.Button(new Rect(1110f, 27f, 130f, 32f), "Đóng [M]", buttonStyle)) SetMapOpen(false);
        DrawMap(new Rect(32f, 109f, 845f, 526f), true);
        for (int i = 0; i < 5; i++) {
            Fill(new Rect(38f + i * 169f, 653f, 12f, 12f), RiskColors[i]);
            GUI.Label(new Rect(55f + i * 169f, 648f, 149f, 27f), RiskLabels[i], smallStyle);
        }
        Fill(new Rect(899f, 110f, 349f, 577f), new Color(.105f, .12f, .10f));
        var zone = selectedZone ?? CurrentZone;
        if (zone != null) {
            GUI.color = ThanhGiongUITheme.Paper; GUI.Label(new Rect(917f, 120f, 310f, 72f), zone.displayName, mapRegionTitle); GUI.color = Color.white;
            GUI.Label(new Rect(918f, 188f, 310f, 26f), RiskLabel(zone.risk) + " · " + BiomeLabel(zone.biome), bodyStyle);
            GUI.Label(new Rect(918f, 220f, 310f, 85f), RiskDescription(zone.risk), bodyStyle);
            GUI.Label(new Rect(918f, 312f, 310f, 26f), zone.risk == OpenWorldRisk.Safe ? "LÒ RÈN · KHO LÀNG · LƯƠNG THỰC" : "PHÂN BỔ TÀI NGUYÊN 6 : 3 : 1", bodyStyle);
            for (int i = 0; i < 3; i++) GUI.Label(new Rect(918f, 346f + i * 25f, 310f, 25f),
                (i == 0 ? "Chính  " : i == 1 ? "Phụ     " : "Hiếm   ") + ResourceLabel(ResourceFor(zone.biome, i)), bodyStyle);
            GUI.Label(new Rect(918f, 420f, 310f, 28f), "CHẶNG " + (zone.chapter + 1) + " · " + new[] { "Làng Phù Đổng", "Rèn thép", "Đánh giặc Ân", "Núi Sóc hóa thánh" }[Mathf.Clamp(zone.chapter, 0, 3)], bodyStyle);
            if (zone != CurrentZone && GUI.Button(new Rect(918f, 450f, 310f, 30f), Destination == zone ? "HỦY CHỈ ĐƯỜNG" : "CHỈ ĐƯỜNG ĐẾN ĐÂY", buttonStyle)) SetDestination(zone);
        }
        var rightStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleRight, wordWrap = false };
        var headStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight, wordWrap = false };
        GUI.Label(new Rect(918f, 486f, 120f, 24f), "TÚI ĐỒ", smallStyle);
        GUI.Label(new Rect(1040f, 486f, 90f, 24f), "MANG THEO", headStyle);
        GUI.Label(new Rect(1134f, 486f, 94f, 24f), "KHO LÀNG", headStyle);
        for (int i = 0; i < 5; i++) {
            float rowY = 513f + i * 24f;
            GUI.Label(new Rect(918f, rowY, 130f, 24f), ResourceLabels[i], new GUIStyle(bodyStyle) { wordWrap = false });
            GUI.Label(new Rect(1040f, rowY, 90f, 24f), GetCarriedAmount((OpenWorldResource)i).ToString(), rightStyle);
            GUI.Label(new Rect(1134f, rowY, 94f, 24f), GetStoredAmount((OpenWorldResource)i).ToString(), rightStyle);
        }
        GUI.Label(new Rect(918f, 646f, 310f, 30f), "Độ bền " + Mathf.RoundToInt(durability) + "% · [B] cất tại lò rèn", smallStyle);
    }

    public static string RiskDescription(OpenWorldRisk risk)
    {
        switch (risk) {
            case OpenWorldRisk.Safe: return "Dân cư và lò rèn được bảo hộ. Không nhận sát thương. Cất tài nguyên để giữ an toàn; kho làng cung cấp nguyên liệu rèn.";
            case OpenWorldRisk.Blue: return "Tập luyện và tài nguyên T1–T4. Không nhận sát thương nguy hiểm. Thu thập nguyên liệu trước khi xuất quân.";
            case OpenWorldRisk.Yellow: return "Tài nguyên T5. Gục ngã: choáng 2 giây, về làng, độ bền −15%. Giữ toàn bộ tài nguyên.";
            case OpenWorldRisk.Red: return "Tài nguyên T6–T7, cứ điểm giặc mạnh. Gục ngã: mất tài nguyên mang theo, độ bền −20%. Kho làng và đồ cốt truyện được giữ.";
            default: return "Tài nguyên T8 và trận chiến quyết định. Gục ngã: mất tài nguyên mang theo, độ bền −25%. Kho làng và đồ cốt truyện được giữ.";
        }
    }

    private void DrawMap(Rect area, bool interactive)
    {
        Fill(area, new Color(.09f, .16f, .13f));
        foreach (var link in connections) {
            if (link == null) continue;
            var from = zones.Find(z => z != null && z.id == link.from);
            var to = zones.Find(z => z != null && z.id == link.to);
            if (from != null && to != null) DrawLine(MapPoint(from.center, area), MapPoint(to.center, area), new Color(.69f, .61f, .40f), interactive ? 6f : 2f);
        }
        foreach (var zone in zones) {
            if (zone == null) continue;
            Vector2 topLeft = MapPoint(zone.center + new Vector3(-zone.size.x * .5f, 0f, zone.size.z * .5f), area);
            var rect = new Rect(topLeft.x, topLeft.y, Mathf.Abs(zone.size.x) / worldBounds.width * area.width, Mathf.Abs(zone.size.z) / worldBounds.height * area.height);
            var color = RiskColor(zone.risk); color.a = .68f;
            Fill(rect, color);
            if (zone == CurrentZone || (interactive && zone == selectedZone)) DrawBorder(rect, zone == CurrentZone ? Color.white : new Color(1f, .83f, .43f), interactive ? 3f : 1f);
            if (interactive) {
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) selectedZone = zone;
                string mapName=zone.id=="hub"?"PHÙ ĐỔNG · PHONG CHÂU":zone.displayName;
                GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), mapName + "\n" + BiomeLabel(zone.biome), centeredStyle);
            }
        }
        if (Destination != null) {
            Color guide = new Color(1f, .86f, .30f);
            Vector2 previousPoint = player != null ? MapPoint(player.position, area) : MapPoint(CurrentZone != null ? CurrentZone.center : Destination.center, area);
            for (int i = 1; i < route.Count; i++) {
                Vector2 next = MapPoint(route[i].center, area);
                DrawLine(previousPoint, next, guide, interactive ? 5f : 2f);
                previousPoint = next;
            }
            if (route.Count < 2 && player != null) DrawLine(previousPoint, MapPoint(Destination.center, area), guide, interactive ? 5f : 2f);
            Vector2 dest = MapPoint(Destination.center, area);
            DrawBorder(new Rect(dest.x - 11f, dest.y - 11f, 22f, 22f), guide, 3f);
            if (interactive) GUI.Label(new Rect(dest.x - 30f, dest.y - 34f, 60f, 20f), "ĐÍCH", centeredStyle);
        }
        foreach (var node in ThanhGiongResourceNode.ActiveNodes) {
            if (node == null || !node.gameObject.activeInHierarchy) continue;
            Vector2 p = MapPoint(node.transform.position, area);
            Color color = node.IsReady ? new Color(.95f, .87f, .57f) : new Color(.32f, .34f, .29f);
            float size = interactive ? 5f : 2f;
            Fill(new Rect(p.x - size * .5f, p.y - size * .5f, size, size), color);
        }
        foreach (var forge in ThanhGiongForgeStation.ActiveStations) {
            if (forge == null || !forge.gameObject.activeInHierarchy) continue;
            Vector2 p = MapPoint(forge.transform.position, area);
            Fill(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), new Color(1f, .65f, .22f));
            if (interactive) GUI.Label(new Rect(p.x - 28f, p.y + 4f, 56f, 20f), "LÒ RÈN", centeredStyle);
        }
        if (player != null) {
            Vector2 p = MapPoint(player.position, area);
            p.x = Mathf.Clamp(p.x, area.x + 4f, area.xMax - 4f); p.y = Mathf.Clamp(p.y, area.y + 4f, area.yMax - 4f);
            Fill(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Color.white);
            Fill(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), new Color(.3f, 1f, .85f));
        }
    }

    private Vector2 MapPoint(Vector3 p, Rect area) => new Vector2(area.x + (p.x - worldBounds.x) / worldBounds.width * area.width,
        area.yMax - (p.z - worldBounds.y) / worldBounds.height * area.height);
    private static void Fill(Rect rect, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    private static void DrawBorder(Rect r, Color c, float width) { Fill(new Rect(r.x, r.y, r.width, width), c); Fill(new Rect(r.x, r.yMax - width, r.width, width), c); Fill(new Rect(r.x, r.y, width, r.height), c); Fill(new Rect(r.xMax - width, r.y, width, r.height), c); }
    private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
    {
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = old * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg), Vector3.one);
        Fill(new Rect(0f, -width * .5f, Vector2.Distance(a, b), width), color);
        GUI.matrix = old;
    }
}
