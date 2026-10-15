using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in live probe. Attach only in Play Mode; it restores the actor/economy and never saves a scene.</summary>
public sealed class ThanhGiongOpenWorldVerification : MonoBehaviour
{
    public static bool Complete { get; private set; }
    public static int Passed { get; private set; }
    public static int Failed { get; private set; }
    private readonly List<string> results = new List<string>();
    private readonly Dictionary<Behaviour, bool> behaviourStates = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<FieldInfo, object> coordinatorFlags = new Dictionary<FieldInfo, object>();
    private ThanhGiongOpenWorld world;
    private ThanhGiongCampaignController campaign;
    private ThanhGiongCampaignController.CheckpointState originalCampaign;
    private OpenWorldEconomyState originalEconomy;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Behaviour coordinator;
    private Component seeds;
    private object originalSeeds;
    private float originalTimeScale = 1f;
    private bool originalMapOpen, cleaned;
    private ulong sceneHandle;
    private int sceneCount;
    private ThanhGiongResourceNode touchedNode;
    private float originalNodeCooldown;

    private IEnumerator Start()
    {
        Complete = false; Passed = 0; Failed = 0;
        bool captured = false;
        try { Capture(); captured = true; }
        catch (Exception e) { Check(false, "capture: " + e.Message); }
        if (captured) {
            IEnumerator run = RunChecks();
            while (true) {
                object next = null;
                bool more;
                try { more = run.MoveNext(); if (more) next = run.Current; }
                catch (Exception e) { Check(false, "unexpected exception: " + e); break; }
                if (!more) break;
                yield return next;
            }
        }
        Cleanup();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../OpenWorldVerification"));
        Directory.CreateDirectory(folder);
        results.Add("SUMMARY passed=" + Passed + " failed=" + Failed);
        File.WriteAllLines(Path.Combine(folder, "runtime-results.txt"), results);
        Debug.Log("OPEN_WORLD_RUNTIME " + (Failed == 0 ? "PASS" : "FAIL") + " passed=" + Passed + " failed=" + Failed);
        Complete = true;
    }

    private void Capture()
    {
        originalTimeScale = Time.timeScale;
        world = FindAnyObjectByType<ThanhGiongOpenWorld>();
        if (world == null || world.campaign == null || world.player == null) throw new InvalidOperationException("World, campaign and player references are required.");
        campaign = world.campaign;
        originalCampaign = campaign.CaptureCheckpointState(); originalEconomy = world.CaptureEconomy();
        originalPosition = campaign.transform.position; originalRotation = campaign.transform.rotation;
        originalTimeScale = Time.timeScale; originalMapOpen = world.MapOpen;
        world.SetMapOpen(false); Time.timeScale = 1f;
        sceneHandle = SceneManager.GetActiveScene().handle.GetRawData(); sceneCount = SceneManager.sceneCount;
        coordinator = campaign.GetComponent("ThanhGiongSeamlessCampaign") as Behaviour;
        if (coordinator != null) {
            foreach (string name in new[] { "persistenceEnabled", "restoreSaveOnStart" }) {
                var field = coordinator.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(bool)) { coordinatorFlags[field] = field.GetValue(coordinator); field.SetValue(coordinator, false); }
            }
            Disable(coordinator);
        }
        Disable(campaign.GetComponent<LegendCheckpoint>());
        Disable(campaign.GetComponent<MountedHorseController>());
        foreach (var enemy in FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include)) Disable(enemy);
        seeds = campaign.GetComponent<LegendSeedSkills>();
        if (seeds != null) originalSeeds = seeds.GetType().GetMethod("CaptureState")?.Invoke(seeds, null);
    }

    private void Disable(Behaviour behaviour)
    {
        if (behaviour == null || behaviourStates.ContainsKey(behaviour)) return;
        behaviourStates[behaviour] = behaviour.enabled; behaviour.enabled = false;
    }

    private IEnumerator RunChecks()
    {
        Check(world.zones.Count == 13, "13 authored spatial zones");
        Check(sceneCount == 1, "only one loaded scene");
        var uniqueIds = new HashSet<string>();
        foreach (var zone in world.zones) Check(zone != null && !string.IsNullOrEmpty(zone.id) && uniqueIds.Add(zone.id), "unique zone " + (zone != null ? zone.id : "null"));
        OpenWorldResource[,] expected = {
            { OpenWorldResource.Wood, OpenWorldResource.Hide, OpenWorldResource.Stone },
            { OpenWorldResource.Ore, OpenWorldResource.Stone, OpenWorldResource.Fiber },
            { OpenWorldResource.Stone, OpenWorldResource.Ore, OpenWorldResource.Wood },
            { OpenWorldResource.Hide, OpenWorldResource.Fiber, OpenWorldResource.Ore },
            { OpenWorldResource.Fiber, OpenWorldResource.Wood, OpenWorldResource.Hide }
        };
        for (int b = 0; b < 5; b++) {
            int count = 0;
            for (int r = 0; r < 5; r++) if (ThanhGiongOpenWorld.BaseGatherYield((OpenWorldBiome)b, (OpenWorldResource)r) > 0) count++;
            Check(count == 3, "biome " + (OpenWorldBiome)b + " has exactly 3 resources");
            for (int rank = 0; rank < 3; rank++) Check(ThanhGiongOpenWorld.ResourceFor((OpenWorldBiome)b, rank) == expected[b, rank], "matrix " + (OpenWorldBiome)b + " rank " + rank);
            Check(ThanhGiongOpenWorld.BaseGatherYield((OpenWorldBiome)b, expected[b, 0]) == 6 &&
                ThanhGiongOpenWorld.BaseGatherYield((OpenWorldBiome)b, expected[b, 1]) == 3 &&
                ThanhGiongOpenWorld.BaseGatherYield((OpenWorldBiome)b, expected[b, 2]) == 1, "biome " + (OpenWorldBiome)b + " 6:3:1 yield");
        }
        var clone = world.CaptureEconomy(); clone.carried[0] = 987;
        Check(world.GetCarriedAmount(OpenWorldResource.Wood) != 987, "capture economy clones arrays");
        Check(!world.RestoreEconomy(new OpenWorldEconomyState { carried = new int[4] }), "invalid inventory array rejected");
        var invalid = new OpenWorldEconomyState(); invalid.stored[1] = -1;
        Check(!world.RestoreEconomy(invalid), "negative inventory rejected");
        Check(!world.RestoreEconomy(new OpenWorldEconomyState { durability = float.NaN }), "non-finite durability rejected");
        Check(world.FindZone(new Vector3(0f, 0f, 82f)) != null, "inter-zone road land has rules");

        var safe = world.zones.Find(z => z != null && z.risk == OpenWorldRisk.Safe);
        var blue = world.zones.Find(z => z != null && z.risk == OpenWorldRisk.Blue);
        var yellow = world.zones.Find(z => z != null && z.risk == OpenWorldRisk.Yellow);
        var red = world.zones.Find(z => z != null && z.risk == OpenWorldRisk.Red);
        var black = world.zones.Find(z => z != null && z.risk == OpenWorldRisk.Black && z.chapter == 2);
        Check(safe != null && blue != null && yellow != null && red != null && black != null, "all five risk categories present");
        if (safe == null || blue == null || yellow == null || red == null || black == null) yield break;

        Warp(safe.center + Vector3.up);
        float health = campaign.Health;
        Check(world.IsSafe && !world.CanTakeDamage && !campaign.DamagePlayer(campaign.maxHealth * 3f, default) && campaign.Health == health, "safe hub rejects incoming damage");
        Warp(blue.center + Vector3.up); health = campaign.Health;
        Check(world.IsSafe && !world.CanTakeDamage && !campaign.DamagePlayer(campaign.maxHealth * 3f, default) && campaign.Health == health, "blue zone rejects incoming damage");
        world.RestoreEconomy(new OpenWorldEconomyState());
        for (int r = 0; r < 5; r++) if (ThanhGiongOpenWorld.ResourceAbundance(blue.biome, (OpenWorldResource)r) < 0) {
            Check(!world.TryGather((OpenWorldResource)r, 1), "unsupported resource denied in " + blue.biome); break;
        }

        touchedNode = ThanhGiongResourceNode.ActiveNodes.Find(n => n != null && n.IsReady && world.FindZone(n.transform.position)?.risk == OpenWorldRisk.Blue);
        Check(touchedNode != null, "actual blue resource node exists");
        if (touchedNode != null) {
            originalNodeCooldown = touchedNode.RemainingSeconds;
            Warp(touchedNode.transform.position + Vector3.right * 2f);
            int before = world.GetCarriedAmount(touchedNode.resource);
            int expectedYield = touchedNode.yieldOverride > 0 ? touchedNode.yieldOverride : ThanhGiongOpenWorld.BaseGatherYield(world.CurrentZone.biome, touchedNode.resource);
            Check(touchedNode.TryHarvest(world) && world.GetCarriedAmount(touchedNode.resource) == before + expectedYield, "actual node G interaction harvests correct yield");
            Check(!touchedNode.IsReady && !touchedNode.TryHarvest(world), "depleted node rejects immediate repeat");
            touchedNode.RestoreCooldown(0f);
            Warp(touchedNode.transform.position + Vector3.right * 20f);
            Check(!touchedNode.TryHarvest(world), "resource node rejects distant harvesting");
        }

        var forge = ThanhGiongForgeStation.ActiveStations.Find(f => f != null);
        Check(forge != null, "actual forge station exists");
        if (forge != null) {
            Warp(forge.transform.position + Vector3.right * 2f);
            world.RestoreEconomy(new OpenWorldEconomyState());
            Check(!forge.TryInteract(world) && !world.HasForgedSupplies, "forge rejects insufficient resources");
            var supplies = new OpenWorldEconomyState(); supplies.carried[(int)OpenWorldResource.Ore] = 6; supplies.carried[(int)OpenWorldResource.Wood] = 3;
            world.RestoreEconomy(supplies);
            Check(forge.TryInteract(world) && world.HasForgedSupplies && world.GetCarriedAmount(OpenWorldResource.Ore) == 0 && world.GetCarriedAmount(OpenWorldResource.Wood) == 0, "real forge charges 6 iron + 3 wood");
            var repair = new OpenWorldEconomyState { forged = true, durability = 40f }; repair.carried[(int)OpenWorldResource.Ore] = 2; repair.carried[(int)OpenWorldResource.Stone] = 1;
            world.RestoreEconomy(repair);
            Check(forge.TryInteract(world) && world.Durability == 100f && world.GetCarriedAmount(OpenWorldResource.Ore) == 0 && world.GetCarriedAmount(OpenWorldResource.Stone) == 0, "real forge repair charges 2 iron + 1 stone");
            world.RestoreEconomy(StockedEconomy());
            Check(world.TryStoreCarriedResources() && world.GetCarriedAmount(OpenWorldResource.Wood) == 0 && world.GetStoredAmount(OpenWorldResource.Wood) == 30, "safe bank moves carried resources into protected store");
        }

        world.RestoreEconomy(StockedEconomy()); Warp(yellow.center + Vector3.up);
        Check(campaign.DamagePlayer(campaign.maxHealth * 3f, default) && world.IsRecovering && campaign.IsDead, "yellow lethal damage enters real knockdown");
        Check(world.GetCarriedAmount(OpenWorldResource.Ore) == 10 && world.Durability == 85f, "yellow keeps resources and removes 15 durability");
        yield return WaitRecovery();
        Check(!world.IsRecovering && campaign.Health == campaign.maxHealth && Vector3.Distance(campaign.transform.position, world.safeSpawn) < 1f, "yellow recovers at safe spawn after knockdown");
        Check(campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Battle && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo, "yellow recovery preserves story chapter and weapon");
        Check(world.CombatDamageMultiplier < 1f, "lost durability reduces combat damage");

        world.RestoreEconomy(StockedEconomy()); Warp(red.center + Vector3.up);
        Check(campaign.DamagePlayer(campaign.maxHealth * 3f, default) && world.IsRecovering, "red lethal damage enters recovery");
        Check(world.GetCarriedAmount(OpenWorldResource.Ore) == 0 && world.GetStoredAmount(OpenWorldResource.Ore) == 20 && world.HasForgedSupplies && world.Durability == 80f, "red loses carried stock, preserves bank and forged story supplies");
        yield return WaitRecovery();
        Check(!world.IsRecovering && campaign.Health > 0f && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo, "red respawns preserving story gear");

        world.RestoreEconomy(StockedEconomy()); Warp(black.center + Vector3.up);
        Check(campaign.DamagePlayer(campaign.maxHealth * 3f, default) && world.IsRecovering, "black lethal damage enters recovery");
        Check(world.GetCarriedAmount(OpenWorldResource.Fiber) == 0 && world.GetStoredAmount(OpenWorldResource.Fiber) == 20 && world.Durability == 75f, "black loses carried stock and 25 durability, preserves bank");
        yield return WaitRecovery();
        Check(!world.IsRecovering && campaign.Health > 0f, "black respawns successfully");
        world.SetMapOpen(true); Check(world.MapOpen && Time.timeScale == 0f, "M map pauses simulation");
        float frozenTime = Time.time;
        yield return new WaitForSecondsRealtime(.05f);
        Check(Mathf.Approximately(Time.time, frozenTime), "map freezes scaled game time while real time advances");
        world.SetMapOpen(false); Check(!world.MapOpen && Time.timeScale == 1f, "closing M map restores simulation");
        Check(SceneManager.GetActiveScene().handle.GetRawData() == sceneHandle && SceneManager.sceneCount == sceneCount, "gather, forge and all defeats retain same scene handle/count");
    }

    private IEnumerator WaitRecovery()
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (world.IsRecovering && Time.realtimeSinceStartup < deadline) yield return null;
        if (world.IsRecovering) { Check(false, "recovery timed out"); world.CancelPendingRecovery(); }
    }

    private static OpenWorldEconomyState StockedEconomy()
    {
        var state = new OpenWorldEconomyState { forged = true };
        for (int i = 0; i < 5; i++) { state.carried[i] = 10; state.stored[i] = 20; }
        return state;
    }

    private void Warp(Vector3 position)
    {
        world.CancelPendingRecovery();
        var state = campaign.CaptureCheckpointState();
        state.chapter = ThanhGiongCampaignController.Chapter.Battle; state.weapon = ThanhGiongCampaignController.Weapon.Bamboo; state.health = campaign.maxHealth;
        if (!campaign.RestoreCheckpointState(state, position, Quaternion.identity)) throw new InvalidOperationException("Cannot prepare actor while equipment animation is busy.");
        var movement = campaign.GetComponent<MountedHorseController>(); if (movement != null) movement.enabled = false;
        world.RefreshCurrentZone();
    }

    private void Cleanup()
    {
        if (cleaned) return; cleaned = true;
        if (world != null) { world.CancelPendingRecovery(); world.SetMapOpen(false); }
        Time.timeScale = 1f;
        if (campaign != null && originalCampaign != null) Check(campaign.RestoreCheckpointState(originalCampaign, originalPosition, originalRotation), "cleanup restores original campaign and position");
        if (world != null && originalEconomy != null) { Check(world.RestoreEconomy(originalEconomy), "cleanup restores original economy"); world.RefreshCurrentZone(); }
        if (touchedNode != null) touchedNode.RestoreCooldown(originalNodeCooldown);
        if (seeds != null && originalSeeds != null) seeds.GetType().GetMethod("RestoreState")?.Invoke(seeds, new[] { originalSeeds });
        foreach (var entry in coordinatorFlags) if (coordinator != null) entry.Key.SetValue(coordinator, entry.Value);
        foreach (var entry in behaviourStates) if (entry.Key != null) entry.Key.enabled = entry.Value;
        if (world != null && originalMapOpen) world.SetMapOpen(true); else Time.timeScale = originalTimeScale;
    }

    private void Check(bool success, string label)
    {
        if (success) Passed++; else Failed++;
        results.Add((success ? "PASS " : "FAIL ") + label);
    }
    private void OnDestroy() { Cleanup(); }
}
