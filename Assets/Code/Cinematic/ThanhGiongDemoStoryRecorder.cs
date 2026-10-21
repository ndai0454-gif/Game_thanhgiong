using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Records the story half of the demo reel (supplies, forge, equipment ritual, melee combat, bamboo, ascension) as numbered
/// PNG frames. It drives the real game APIs the way ThanhGiongSeamlessVerification does, but with Time.captureFramerate locked,
/// so nothing depends on real time. Never saves: persistence is switched off for the whole session.
/// </summary>
public sealed class ThanhGiongDemoStoryRecorder : MonoBehaviour
{
    public static bool Complete { get; private set; }
    public static int Frames { get; private set; }
    public static string Progress { get; private set; } = "idle";

    public string outputDirectory = "Builds/Videos/DemoFramesStory";
    public int frameRate = 30;

    ThanhGiongCampaignController actor;
    ThanhGiongSeamlessCampaign driver;
    ThanhGiongOpenWorld world;
    MountedHorseController movement;
    IsometricCameraFollow cam;
    string dir;

    IEnumerator Start()
    {
        Complete = false; Frames = 0; Progress = "starting";
        actor = FindAnyObjectByType<ThanhGiongCampaignController>();
        movement = actor != null ? actor.GetComponent<MountedHorseController>() : null;
        cam = IsometricCameraFollow.Instance;
        if (actor == null || actor.openWorldCampaign == null || movement == null || cam == null) { Progress = "missing references"; yield break; }
        driver = actor.openWorldCampaign; world = driver.world;

        dir = Path.Combine(Application.dataPath, "..", outputDirectory);
        if (Directory.Exists(dir)) foreach (var old in Directory.GetFiles(dir, "frame_*.png")) File.Delete(old);
        Directory.CreateDirectory(dir);

        cam.mouseLook = false;
        driver.persistenceEnabled = false; driver.restoreSaveOnStart = false;   // never touch the player's save
        movement.enabled = false;                                               // DriveMovement below replaces its Update
        Time.captureFramerate = frameRate;

        IEnumerator scenario = Run();
        while (true)
        {
            bool next; object current = null;
            try { next = scenario.MoveNext(); if (next) current = scenario.Current; }
            catch (Exception e) { Debug.LogError("DEMO_STORY exception: " + e); Progress = "exception: " + e.Message; break; }
            if (!next) break;
            yield return current;
        }

        Time.captureFramerate = 0;
        movement.enabled = true;
        cam.mouseLook = true;
        Complete = true;
        Debug.Log("DEMO_STORY complete frames=" + Frames + " chapter=" + actor.CurrentChapter + " progress=" + Progress);
    }

    IEnumerator Run()
    {
        yield return Silent(10);
        if (actor.CurrentChapter != ThanhGiongCampaignController.Chapter.Prologue) { Progress = "needs Prologue, found " + actor.CurrentChapter; yield break; }

        // Food: usually already complete from the save; otherwise collect it off-camera.
        if (!driver.GrowthComplete)
        {
            foreach (var item in FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item.kind > ThanhGiongCollectible.Kind.Meat || item.IsCollected) continue;
                if (actor.TryCollect(item)) { item.MarkCollected(); item.gameObject.SetActive(false); }
                if (driver.GrowthComplete) break;
            }
            yield return Silent(30);
        }

        // 1. Gather ore and wood.
        foreach (var resource in new[] { OpenWorldResource.Ore, OpenWorldResource.Wood })
        {
            Progress = "gather " + resource;
            var node = ThanhGiongResourceNode.ActiveNodes.Find(n => n.resource == resource &&
                world.FindZone(n.transform.position).risk == OpenWorldRisk.Blue &&
                ThanhGiongOpenWorld.ResourceAbundance(world.FindZone(n.transform.position).biome, resource) == 0);
            if (node == null) continue;
            Teleport(node.transform.position + Vector3.right * 2f + Vector3.up * .3f, Yaw(Vector3.right * -2f), 16f);
            yield return Silent(8);
            for (int f = 0; f < 60; f++)
            {
                if (f == 20) { node.RestoreCooldown(0); node.TryHarvest(world); }
                cam.SetOrbit(Yaw(Vector3.right * -2f) + Mathf.Sin(f / 59f * Mathf.PI) * 18f, 14f);
                yield return Frame();
            }
        }

        // 2. Forge: story advances, equipment ritual E Q E F.
        Progress = "forge";
        var forge = ThanhGiongForgeStation.ActiveStations[0];
        Vector3 forgePos = forge.transform.position + Vector3.right * 3f + Vector3.up * .3f;
        Teleport(forgePos, Yaw(-Vector3.right * 3f), 14f);
        yield return Silent(10);
        for (int f = 0; f < 45; f++) { cam.SetOrbit(Yaw(-Vector3.right * 3f) - 25f + f * .8f, 14f); yield return Frame(); }
        forge.TryInteract(world);
        yield return Frame();
        Progress = "equipment ritual";
        foreach (var key in new[] { KeyCode.E, KeyCode.Q, KeyCode.E, KeyCode.F })
        {
            yield return RecordWhileBusy(10, 420);
            actor.TryPerformRitualStep(key);
            yield return Frame();
        }
        yield return RecordWhileBusy(10, 420);
        if (actor.CurrentChapter != ThanhGiongCampaignController.Chapter.Battle) { Progress = "battle did not start: " + actor.CurrentChapter; yield break; }

        // 3. Combat with the iron spear until it breaks.
        Progress = "combat";
        var enemies = actor.battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true);
        if (enemies.Length == 0) { Progress = "no enemies"; yield break; }
        Vector3 front = enemies[0].transform.position;
        Teleport(front + new Vector3(0f, .4f, -9f), 0f, 16f);
        yield return Silent(20);
        yield return Combat(enemies, 14f, run: true, stopWhenWeapon: ThanhGiongCampaignController.Weapon.None);

        // Make sure the break happens even if the fight did not reach it.
        int safety = 0;
        foreach (var enemy in enemies)
        {
            if (actor.CurrentWeapon == ThanhGiongCampaignController.Weapon.None || safety++ > 80) break;
            if (enemy.HealthRatio > 0f) { enemy.TakeDamage(100000); yield return Silent(2); }
        }

        // 4. Pull a bamboo.
        Progress = "bamboo";
        var bamboo = Array.Find(actor.bambooRoot.GetComponentsInChildren<ThanhGiongCollectible>(true),
            i => i.kind == ThanhGiongCollectible.Kind.Bamboo && i.gameObject.activeInHierarchy && !i.IsCollected);
        if (bamboo != null)
        {
            Teleport(bamboo.transform.position + Vector3.right * 2f + Vector3.up * .3f, Yaw(Vector3.right * -2f), 14f);
            yield return Silent(10);
            for (int i = 0; i < 3; i++)
            {
                yield return RecordWhileBusy(8, 300);
                actor.TryPullNearestBamboo();
                yield return Frame();
            }
            yield return RecordWhileBusy(8, 300);
        }

        // 5. Bamboo combat on the remaining soldiers.
        Progress = "bamboo combat";
        var alive = Array.Find(enemies, e => e != null && e.HealthRatio > 0f && e.gameObject.activeInHierarchy);
        if (alive != null && !actor.IsDead)
        {
            Teleport(alive.transform.position + new Vector3(0f, .4f, -9f), 0f, 16f);
            yield return Silent(20);
            yield return Combat(enemies, 9f, run: true, stopWhenWeapon: null);
        }

        // 6. Finish the battle off-camera and walk into the summit ritual.
        Progress = "finish battle";
        foreach (var enemy in enemies) { if (enemy != null && enemy.HealthRatio > 0f) { enemy.TakeDamage(100000); yield return Silent(1); } }
        yield return Silent(20);
        Teleport(driver.peakAnchor + Vector3.right * 2f + Vector3.up * .3f, Yaw(-Vector3.right * 2f), 12f);
        yield return Silent(10);
        for (int f = 0; f < 40; f++) { cam.SetOrbit(Yaw(-Vector3.right * 2f) + f * .6f, 12f); yield return Frame(); }
        if (actor.CurrentChapter != ThanhGiongCampaignController.Chapter.Ascension) { Progress = "ascension did not start: " + actor.CurrentChapter; yield break; }

        // 7. Ascension ritual and the flight to the sky.
        Progress = "ascension";
        foreach (var key in new[] { KeyCode.E, KeyCode.Q, KeyCode.E })
        {
            yield return RecordWhileBusy(10, 420);
            actor.TryPerformRitualStep(key);
            yield return Frame();
        }
        int cap = 30 * 40;
        while (actor.CurrentChapter != ThanhGiongCampaignController.Chapter.Complete && cap-- > 0) yield return Frame();
        for (int f = 0; f < 45; f++) yield return Frame();   // hold on the ending
        Progress = "complete";
    }

    // Fight: face the nearest living enemy, close in, and swing with the game's own melee API.
    IEnumerator Combat(ThanhGiongEnemy[] enemies, float seconds, bool run, ThanhGiongCampaignController.Weapon? stopWhenWeapon)
    {
        int frames = Mathf.RoundToInt(seconds * frameRate);
        for (int f = 0; f < frames && !actor.IsDead; f++)
        {
            if (stopWhenWeapon.HasValue && actor.CurrentWeapon == stopWhenWeapon.Value) break;
            ThanhGiongEnemy target = null; float best = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e == null || e.HealthRatio <= 0f || !e.gameObject.activeInHierarchy) continue;
                float d = (e.transform.position - actor.transform.position).sqrMagnitude;
                if (d < best) { best = d; target = e; }
            }
            if (target == null) break;
            Vector3 toEnemy = target.transform.position - actor.transform.position;
            float dist = Mathf.Sqrt(best);
            cam.SetOrbit(Yaw(toEnemy) + Mathf.Sin(f * .04f) * 10f, 17f);
            Vector2 move = dist > 3.2f ? Vector2.up : Vector2.zero;
            if (dist < 6.5f) actor.TryMeleeAttack();
            // Safety net so a stalled fight cannot waste the whole shot.
            if (f > 0 && f % 150 == 0 && dist < 7f) target.TakeDamage(100000);
            yield return Frame(move, run && dist > 6f);
        }
    }

    IEnumerator RecordWhileBusy(int minFrames, int maxFrames)
    {
        int f = 0;
        while (f < maxFrames && (f < minFrames || actor.EquipmentBusy)) { f++; yield return Frame(); }
    }

    IEnumerator Frame(Vector2 move = default, bool run = false)
    {
        movement.DriveMovement(move, run);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "frame_" + Frames.ToString("D5") + ".png"));
        Frames++;
        yield return null;
    }

    IEnumerator Silent(int frames)
    {
        for (int i = 0; i < frames; i++) { movement.DriveMovement(Vector2.zero, false); yield return null; }
    }

    void Teleport(Vector3 position, float yaw, float pitch)
    {
        actor.TeleportPlayer(position);
        Physics.SyncTransforms();
        world.RefreshCurrentZone();
        cam.SnapToTarget();
        cam.SetOrbit(yaw, pitch);
    }

    static float Yaw(Vector3 direction)
    {
        Vector3 d = Vector3.ProjectOnPlane(direction, Vector3.up);
        return d.sqrMagnitude < 1e-4f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }
}
