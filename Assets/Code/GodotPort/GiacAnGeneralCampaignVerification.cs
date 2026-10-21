using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Explicitly attached Play Mode verification; never installed in shipping scenes.</summary>
public sealed class GiacAnGeneralCampaignVerification : MonoBehaviour
{
    public static bool Complete { get; private set; }
    readonly List<string> report = new List<string>();
    readonly HashSet<ThanhGiongEnemy.EnemyState> seen = new HashSet<ThanhGiongEnemy.EnemyState>();
    ThanhGiongEnemy boss;
    ThanhGiongCampaignController campaign;
    ThanhGiongEnemyAnimationDriver driver;
    ActionForgeEnemyEffects effects;
    string folder;
    bool movedAway;
    bool trailObserved;

    IEnumerator Start()
    {
        Complete = false;
        folder = "GiacAnGeneralVerification/" + gameObject.scene.name;
        Directory.CreateDirectory(folder);
        campaign = FindFirstObjectByType<ThanhGiongCampaignController>();
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour is ThanhGiongSceneTransition || behaviour.GetType().Name.Contains("SceneGate")) behaviour.enabled = false;
        boss = FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.isBoss);
        campaign.maxHealth = 100000f; // Only the temporary verification run survives the observed fight.
        campaign.RestartBattle();
        foreach (var e in FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!e.isBoss) e.gameObject.SetActive(false);
        boss.Initialize(campaign, campaign.transform);
        var movement = campaign.GetComponent<MountedHorseController>();
        if (movement != null) movement.enabled = false;
        driver = boss.GetComponent<ThanhGiongEnemyAnimationDriver>();
        effects = boss.GetComponent<ActionForgeEnemyEffects>();
        yield return new WaitForSeconds(.3f);
        var presentation = boss.GetComponent<LegendEnemyPresentation>();
        Check(driver.UsesActionForge && driver.actionForgeMotions.name == "GiacAnGeneral", "new exact-rig combat controller bound");
        Check(presentation.sourceAsset.EndsWith("GiacAnGeneral_Meshy.prefab"), "new Meshy model assigned");
        Check(presentation.model.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.sharedMaterial.shader.name == "Universal Render Pipeline/Lit"), "original PBR materials retained at runtime");
        Check(!driver.BoundAnimator.applyRootMotion, "root motion disabled");
        var tip = driver.BoundAnimator.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "ActionForge Weapon Tip");
        Check(tip != null && tip.parent.name == "hand_r", "sword trail marker follows actual right hand");
        MovePlayer(8f);
        var start = boss.transform.position;
        yield return new WaitForSeconds(1.1f);
        Check(Vector3.ProjectOnPlane(boss.transform.position-start, Vector3.up).magnitude > .05f, "boss physically chases player");
        Check(seen.Contains(ThanhGiongEnemy.EnemyState.Chasing), "movement state observed");
        MovePlayer(2.6f);
        float until = Time.time+12f;
        while (Time.time < until)
        {
            if (boss.CurrentState == ThanhGiongEnemy.EnemyState.TelegraphingAttack && !movedAway)
            {
                movedAway = true;
                MovePlayer(12f); // Dodge the real telegraph to exercise the existing vulnerability branch.
            }
            if (seen.Contains(ThanhGiongEnemy.EnemyState.Recovering)) break;
            yield return null;
        }
        foreach (var state in new[] { ThanhGiongEnemy.EnemyState.TelegraphingAttack, ThanhGiongEnemy.EnemyState.Slamming,
            ThanhGiongEnemy.EnemyState.StuckInGround, ThanhGiongEnemy.EnemyState.Recovering })
            Check(seen.Contains(state), "actual combat phase observed: " + state);
        Check(effects != null && effects.ContactBursts > 0, "weapon contact particles emitted from combat clock");
        Check(trailObserved, "animated sword trail emitted during attack");
        Check(presentation.RefreshGroundSupportNow() && Mathf.Abs(presentation.GroundGap-.03f)<.04f, "boss grounded on campaign terrain");
        MovePlayer(12f);
        boss.StopAllCoroutines();
        boss.Initialize(campaign, campaign.transform);
        float before = boss.HealthRatio;
        boss.TakeDamage(10f,.15f,campaign.transform.position,0f,false);
        yield return new WaitForSeconds(.08f);
        Check(boss.HealthRatio < before, "boss takes gameplay damage");
        Check(driver.BoundAnimator.GetInteger("State") == (int)ThanhGiongEnemy.EnemyState.Stunned, "hit animation driven by real damage");
        yield return new WaitForSeconds(.6f);
        boss.TakeDamage(boss.maxHealth*3f,0f,campaign.transform.position,0f,false);
        yield return new WaitForSeconds(.08f);
        Check(boss.CurrentState == ThanhGiongEnemy.EnemyState.Dead && driver.BoundAnimator.GetInteger("State")==7, "death drives new rig through original combat logic");
        yield return new WaitForSeconds(3.4f);
        Check(!boss.gameObject.activeSelf, "defeated boss finishes dissolve and deactivates");
        campaign.RestartBattle();
        yield return new WaitForSeconds(.2f);
        Check(boss.gameObject.activeInHierarchy && boss.HealthRatio>.99f && driver.UsesActionForge, "battle restart restores new boss rig and health");
        File.WriteAllLines(folder+"/runtime-results.txt", report);
        Complete = true;
        Debug.Log("GiacAnGeneral campaign verification complete: "+gameObject.scene.name+" | failures="+report.Count(s=>s.StartsWith("FAIL")));
    }

    void Update()
    {
        if (boss == null || !boss.gameObject.activeInHierarchy) return;
        seen.Add(boss.CurrentState);
        if (effects != null && effects.TrailEmitting) trailObserved = true;
    }

    void MovePlayer(float distance)
    {
        Vector3 p = boss.transform.position + Vector3.back*distance;
        p.y = campaign.transform.position.y;
        campaign.TeleportPlayer(p);
    }

    void Check(bool ok, string message)
    {
        report.Add((ok?"PASS ":"FAIL ")+message);
        File.WriteAllLines(folder+"/runtime-results.txt",report);
    }
}
