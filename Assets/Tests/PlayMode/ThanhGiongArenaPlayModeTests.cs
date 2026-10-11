using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Reflection keeps the existing test assembly independent of Assembly-CSharp.
public class ThanhGiongArenaPlayModeTests
{
    Component arena, campaign, waves;
    static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    static object P(object o, string name) => o.GetType().GetProperty(name).GetValue(o);
    static object F(object o, string name) => o.GetType().GetField(name).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name).Invoke(o, args);
    string Stage => P(arena, "CurrentStage").ToString();
    Component[] Living => ((IEnumerable)P(waves, "LivingEnemies")).Cast<Component>().ToArray();

    [UnitySetUp]
    public IEnumerator LoadArena()
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync("PhuDongArena");
        yield return null; yield return null;
        arena = UnityEngine.Object.FindFirstObjectByType(T("ThanhGiongArenaFlow")) as Component;
        Assert.NotNull(arena);
        campaign = arena.GetComponent(T("ThanhGiongCampaignController"));
        waves = (Component)F(arena, "waveManager");
        foreach (object wave in (Array)F(waves, "waves")) { wave.GetType().GetField("count").SetValue(wave, 1); wave.GetType().GetField("spawnInterval").SetValue(wave, .15f); }
        arena.GetType().GetField("breakDuration").SetValue(arena, .1f);
        arena.GetType().GetField("cinematicDuration").SetValue(arena, .5f);
    }
    [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; Call(arena, "RestartRound"); yield return null; }
    static void Kill(Component e) => Call(e, "TakeDamage", 10000f, 0f, Vector3.zero, 1f, false);
    IEnumerator WaitEnemy()
    {
        float deadline = Time.realtimeSinceStartup + 3;
        while (Living.Length == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.Greater(Living.Length, 0, "Wave did not spawn an enemy.");
    }
    IEnumerator ReachBamboo()
    {
        Call(arena, "BeginCombat"); yield return WaitEnemy(); Kill(Living[0]);
        yield return WaitEnemy(); Kill(Living[0]);
        Assert.AreEqual("BreakingIronRod", Stage);
        Assert.AreEqual("None", P(campaign, "CurrentWeapon").ToString());
        Assert.IsFalse((bool)Call(campaign, "TryMeleeAttack"));
        yield return new WaitForSeconds(.15f);
        Assert.AreEqual("SeekingBamboo", Stage);
    }

    [UnityTest] public IEnumerator IntroStartsAdultWithIronAndNoEnemies()
    {
        Assert.AreEqual("Intro", Stage); Assert.AreEqual(3, P(campaign, "GrowthPhase"));
        Assert.AreEqual(F(campaign, "maxHealth"), P(campaign, "Health"));
        Assert.AreEqual("IronSword", P(campaign, "CurrentWeapon").ToString());
        Assert.IsFalse((bool)P(campaign, "IsBattleActive")); Assert.AreEqual(0, P(waves, "AliveCount"));
        Assert.IsFalse((bool)Call(arena, "TryPullBamboo"));
        Call(arena, "BeginCombat"); yield return WaitEnemy(); Assert.AreEqual("IronCombat", Stage);
    }

    [UnityTest] public IEnumerator WaveWaitsForEverySpawnAndCountsDeathOnce()
    {
        object first = ((Array)F(waves, "waves")).GetValue(0);
        first.GetType().GetField("count").SetValue(first, 2);
        first.GetType().GetField("spawnInterval").SetValue(first, .4f);
        Call(arena, "BeginCombat"); yield return WaitEnemy();
        Component old = Living[0]; Kill(old); Call(campaign, "NotifyEnemyDefeated", old);
        Assert.AreEqual(0, P(waves, "WaveIndex")); Assert.AreEqual(1, P(waves, "Defeated")); Assert.AreEqual(1, P(campaign, "Kills"));
        yield return WaitEnemy(); Assert.AreEqual(0, P(waves, "WaveIndex"));
        Kill(Living[0]); Assert.AreEqual(1, P(waves, "WaveIndex"));
    }

    [UnityTest] public IEnumerator EnteringBambooDoesNotPullAndERequiresRange()
    {
        yield return ReachBamboo();
        Assert.IsFalse((bool)Call(arena, "TryPullBamboo"));
        Component bamboo = (Component)F(arena, "bamboo");
        Call(campaign, "TeleportPlayer", bamboo.transform.position);
        Physics.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
        Assert.AreEqual(0, P(arena, "BambooPulls")); Assert.AreEqual("None", P(campaign, "CurrentWeapon").ToString());
        Assert.IsTrue((bool)Call(arena, "TryPullBamboo")); Assert.IsTrue((bool)Call(arena, "TryPullBamboo"));
        Assert.AreEqual("SeekingBamboo", Stage); Assert.AreEqual(1, P(waves, "WaveIndex"));
        Assert.IsTrue((bool)Call(arena, "TryPullBamboo")); Assert.AreEqual("BambooCombat", Stage);
        Assert.AreEqual("Bamboo", P(campaign, "CurrentWeapon").ToString()); Assert.IsFalse(bamboo.gameObject.activeSelf);
        Assert.IsFalse((bool)Call(arena, "TryPullBamboo"));
        yield return WaitEnemy(); Assert.AreEqual(2, P(waves, "WaveIndex"));
    }

    [UnityTest] public IEnumerator PauseFreezesSpawnsTimerAndCombat()
    {
        object first = ((Array)F(waves, "waves")).GetValue(0);
        first.GetType().GetField("count").SetValue(first, 2);
        first.GetType().GetField("spawnInterval").SetValue(first, .3f);
        Call(arena, "BeginCombat"); yield return WaitEnemy(); Call(arena, "SetPaused", true);
        float elapsed = (float)P(arena, "ElapsedSeconds");
        yield return new WaitForSecondsRealtime(.5f);
        Assert.AreEqual(1, P(waves, "Spawned")); Assert.AreEqual(elapsed, P(arena, "ElapsedSeconds"));
        Assert.IsFalse((bool)Call(campaign, "TryMeleeAttack"));
        Call(arena, "SetPaused", false); yield return new WaitForSeconds(.4f); Assert.AreEqual(2, P(waves, "Spawned"));
    }

    [UnityTest] public IEnumerator ActualMeleeDamagesEnemyAndItsDeathAdvancesWave()
    {
        object first = ((Array)F(waves, "waves")).GetValue(0);
        first.GetType().GetField("health").SetValue(first, 20f);
        Call(arena, "BeginCombat"); yield return WaitEnemy(); Component enemy = Living[0];
        ((Behaviour)enemy).enabled = false;
        Call(campaign, "TeleportPlayer", enemy.transform.position - Vector3.forward * 1.5f);
        campaign.transform.rotation = Quaternion.identity; Physics.SyncTransforms();
        Assert.IsTrue((bool)Call(campaign, "TryMeleeAttack"));
        yield return new WaitForSeconds(.35f);
        Assert.AreEqual(1, P(campaign, "Kills")); Assert.AreEqual(1, P(waves, "WaveIndex"));
    }

    [UnityTest] public IEnumerator FinalWaveRunsCinematicThenVictoryAndRestartRestoresEverything()
    {
        yield return ReachBamboo(); Component bamboo = (Component)F(arena, "bamboo");
        Call(campaign, "TeleportPlayer", bamboo.transform.position);
        for (int i = 0; i < 3; i++) Call(arena, "TryPullBamboo");
        yield return WaitEnemy(); Assert.AreEqual("BambooCombat", Stage); Kill(Living[0]);
        Assert.AreEqual("VictoryCinematic", Stage); Assert.IsFalse((bool)P(campaign, "IsBattleActive"));
        yield return new WaitForSeconds(.6f); Assert.AreEqual("Victory", Stage);
        Call(arena, "RestartRound"); yield return null;
        Assert.AreEqual("Intro", Stage); Assert.IsTrue(bamboo.gameObject.activeSelf);
        Assert.AreEqual(0, P(campaign, "Kills")); Assert.AreEqual(0, P(arena, "BambooPulls"));
        Assert.AreEqual(0, P(waves, "AliveCount")); Assert.AreEqual(-1, P(waves, "WaveIndex"));
        Assert.AreEqual(1f, Time.timeScale); Assert.IsTrue(campaign.GetComponent<CharacterController>().enabled);
        Assert.AreEqual(F(campaign, "maxHealth"), P(campaign, "Health"));
    }

    [UnityTest] public IEnumerator RestartCancelsCinematicAndRejectsOldDeath()
    {
        yield return ReachBamboo(); Component bamboo = (Component)F(arena, "bamboo");
        Call(campaign, "TeleportPlayer", bamboo.transform.position);
        for (int i = 0; i < 3; i++) Call(arena, "TryPullBamboo");
        yield return WaitEnemy(); Component old = Living[0]; Kill(old);
        Assert.AreEqual("VictoryCinematic", Stage); Call(arena, "RestartRound");
        Call(arena, "BeginCombat"); Call(campaign, "NotifyEnemyDefeated", old);
        Assert.AreEqual(0, P(campaign, "Kills")); yield return new WaitForSeconds(.7f);
        Assert.AreEqual("IronCombat", Stage); Assert.IsTrue(campaign.GetComponent<CharacterController>().enabled);
        Assert.IsTrue(bamboo.gameObject.activeSelf);
    }

    [UnityTest] public IEnumerator DeathStopsWaveAndRestartFromPauseRecoversRound()
    {
        Call(arena, "BeginCombat"); yield return WaitEnemy();
        yield return new WaitForSeconds(1.3f);
        Assert.IsTrue((bool)Call(campaign, "DamagePlayer", 10000f, Vector3.zero));
        yield return null; Assert.AreEqual("Defeat", Stage); Assert.IsFalse((bool)P(waves, "Running"));
        Call(arena, "SetPaused", true); Call(arena, "RestartRound"); yield return null;
        Assert.AreEqual("Intro", Stage); Assert.AreEqual(1f, Time.timeScale); Assert.IsFalse((bool)P(arena, "Paused"));
        Assert.AreEqual(F(campaign, "maxHealth"), P(campaign, "Health"));
    }
}
