using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Destructive only to this temporary Play session. Never saved in the authored scene.
public sealed class ThanhGiongSeamlessVerification : MonoBehaviour
{
    public static bool Complete {get;private set;}
    public static int Passed {get;private set;}
    public static int Failed {get;private set;}
    public static string Progress {get;private set;}
    readonly List<string> results=new List<string>();
    ThanhGiongCampaignController actor;
    ThanhGiongSeamlessCampaign driver;
    ThanhGiongOpenWorld world;
    MountedHorseController movement;
    CharacterController capsule;
    ulong sceneHandle;
    int routeIndex;
    IEnumerator Start()
    {
        Complete=false;Passed=Failed=0;
        actor=FindAnyObjectByType<ThanhGiongCampaignController>();driver=actor.openWorldCampaign;world=driver.world;
        driver.persistenceEnabled=false;driver.restoreSaveOnStart=false;
        movement=actor.GetComponent<MountedHorseController>();capsule=actor.GetComponent<CharacterController>();
        sceneHandle=SceneManager.GetActiveScene().handle.GetRawData();
        movement.enabled=false;driver.enabled=false;
        IEnumerator scenario=Run();
        while(true){bool next;object current=null;try{next=scenario.MoveNext();if(next)current=scenario.Current;}catch(Exception e){Check(false,"exception: "+e);break;}if(!next)break;yield return current;}
        world.SetMapOpen(false);Time.timeScale=1;
        driver.persistenceEnabled=false;driver.restoreSaveOnStart=false;
        driver.enabled=true;movement.enabled=true;
        results.Add("SUMMARY passed="+Passed+" failed="+Failed);
        Directory.CreateDirectory("OpenWorldVerification");File.WriteAllLines("OpenWorldVerification/seamless-results.txt",results);
        Complete=true;Progress="Complete";
        Debug.Log("SEAMLESS_CAMPAIGN "+(Failed==0?"PASS":"FAIL")+" passed="+Passed+" failed="+Failed);
    }
    IEnumerator Run()
    {
        Check(SceneManager.sceneCount==1,"one loaded scene before traversal");
        foreach(var link in world.connections){
            Progress="Physical route "+(++routeIndex)+" / "+world.connections.Count+": "+link.from+" → "+link.to;
            Vector3 start=world.zones.Find(z=>z.id==link.from).center,end=world.zones.Find(z=>z.id==link.to).center;
            actor.TeleportPlayer(start+Vector3.up*.4f);Physics.SyncTransforms();
            float deadline=Time.realtimeSinceStartup+Vector3.Distance(start,end)/40f+12f;
            float stuckFor=0;bool passed=true;
            while(Vector2.Distance(new Vector2(actor.transform.position.x,actor.transform.position.z),new Vector2(end.x,end.z))>1.4f){
                if(Time.realtimeSinceStartup>deadline||actor.transform.position.y < -3){passed=false;break;}
                float dt=Mathf.Min(Time.deltaTime,.05f);Vector3 delta=Vector3.ProjectOnPlane(end-actor.transform.position,Vector3.up).normalized*(60f*dt);
                Vector3 before=actor.transform.position;capsule.Move(delta+Vector3.down*20f*dt);world.RefreshCurrentZone();
                if(Vector3.ProjectOnPlane(actor.transform.position-before,Vector3.up).magnitude<delta.magnitude*.15f)stuckFor+=Time.unscaledDeltaTime;else stuckFor=0;
                if(stuckFor>1.2f){passed=false;break;}
                yield return null;
            }
            Check(passed,"actual CharacterController traverses "+link.from+" → "+link.to+" end="+actor.transform.position);
        }
        Check(SceneManager.GetActiveScene().handle.GetRawData()==sceneHandle&&SceneManager.sceneCount==1,"18 road traversals never load another scene");
        Progress="Four-chapter progression";
        actor.TeleportPlayer(world.safeSpawn+Vector3.up*.3f);world.RefreshCurrentZone();
        // Trigger the existing collection API using authored food objects; no chapter or food field is assigned.
        foreach(var item in FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include)){
            if(item.kind>ThanhGiongCollectible.Kind.Meat||item.IsCollected)continue;
            if(actor.TryCollect(item)){item.MarkCollected();item.gameObject.SetActive(false);}if(driver.GrowthComplete)break;
        }
        yield return new WaitForSeconds(1);
        Check(driver.GrowthComplete&&actor.GrowthPhase==3&&actor.Food>=actor.foodPerGrowth*3,"real food collection completes growth");
        actor.TeleportPlayer(driver.forgeAnchor+Vector3.right*3+Vector3.up*.3f);world.RefreshCurrentZone();driver.enabled=true;
        yield return null;
        Check(actor.CurrentChapter==ThanhGiongCampaignController.Chapter.Prologue,"forge cannot advance story without supplies");
        foreach(var resource in new[]{OpenWorldResource.Ore,OpenWorldResource.Wood}){
            var node=ThanhGiongResourceNode.ActiveNodes.Find(n=>n.resource==resource&&world.FindZone(n.transform.position).risk==OpenWorldRisk.Blue&&ThanhGiongOpenWorld.ResourceAbundance(world.FindZone(n.transform.position).biome,resource)==0);
            Check(node!=null,"blue primary node exists for "+resource);if(node==null)continue;
            actor.TeleportPlayer(node.transform.position+Vector3.right*2+Vector3.up*.3f);world.RefreshCurrentZone();Physics.SyncTransforms();
            node.RestoreCooldown(0);Check(node.TryHarvest(world),"gather real node for narrative forge: "+resource);
        }
        var forge=ThanhGiongForgeStation.ActiveStations[0];actor.TeleportPlayer(forge.transform.position+Vector3.right*3+Vector3.up*.3f);world.RefreshCurrentZone();
        Check(forge.TryInteract(world),"narrative forge consumes actual gathered supplies");yield return null;
        Check(actor.CurrentChapter==ThanhGiongCampaignController.Chapter.Preparation,"growth + supplies + forge position enters chapter2 in place");
        Check(!actor.TryPerformRitualStep(KeyCode.F),"out-of-order equipment key rejected");
        foreach(var key in new[]{KeyCode.E,KeyCode.Q,KeyCode.E,KeyCode.F}){yield return WaitEquipment();Check(actor.TryPerformRitualStep(key),"equipment input "+key);yield return null;}
        yield return WaitEquipment();
        Check(driver.PreparationComplete&&actor.CurrentChapter==ThanhGiongCampaignController.Chapter.Battle,"E Q E F enters battle in same scene");
        foreach(var encounter in driver.encounters)encounter.Apply(false);
        var enemies=actor.battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true);
        int killed=0;
        foreach(var enemy in enemies){
            if(killed>=actor.swordBreakKills)break;
            enemy.TakeDamage(100000);killed++;yield return null;
        }
        Check(actor.CurrentWeapon==ThanhGiongCampaignController.Weapon.None,"real enemy defeats break iron spear at configured kill count");
        var bamboo=Array.Find(actor.bambooRoot.GetComponentsInChildren<ThanhGiongCollectible>(true),i=>i.kind==ThanhGiongCollectible.Kind.Bamboo&&i.gameObject.activeInHierarchy&&!i.IsCollected);
        Check(bamboo!=null,"merged world retains real bamboo pickup");
        if(bamboo!=null){actor.TeleportPlayer(bamboo.transform.position+Vector3.right*2+Vector3.up*.3f);world.RefreshCurrentZone();Physics.SyncTransforms();for(int i=0;i<3;i++){yield return WaitEquipment();Check(actor.TryPullNearestBamboo(),"bamboo E interaction "+(i+1));yield return new WaitForSeconds(.25f);}}
        Check(actor.CurrentWeapon==ThanhGiongCampaignController.Weapon.Bamboo,"three real bamboo interactions equip bamboo");
        // Persistence test uses a separate verification file; it never overwrites the player's world save.
        driver.checkpointFileName="ThanhGiongOpenWorld.verification.json";driver.persistenceEnabled=true;
        yield return WaitEquipment();
        Check(driver.SaveCheckpoint(),"world checkpoint writes isolated verification file");
        int savedKills=actor.Kills;float savedFood=actor.Food;Vector3 savedPosition=actor.transform.position;
        actor.TeleportPlayer(world.safeSpawn);world.RestoreEconomy(new OpenWorldEconomyState());
        Check(driver.LoadCheckpoint()&&actor.Kills==savedKills&&Mathf.Approximately(actor.Food,savedFood)&&(actor.transform.position-savedPosition).sqrMagnitude<1,"world checkpoint restores story, economy, position and defeated enemies");
        driver.persistenceEnabled=false;
        foreach(var enemy in enemies){if(enemy.HealthRatio<=0)continue;enemy.TakeDamage(100000);yield return null;}
        Check(actor.Kills>=actor.RequiredBattleKills&&driver.BattleComplete,"all 57 actual enemy defeat notifications unlock summit");
        Check(actor.CurrentChapter==ThanhGiongCampaignController.Chapter.Battle,"victory does not teleport or enter summit chapter from afar");
        actor.TeleportPlayer(driver.peakAnchor+Vector3.right*2+Vector3.up*.3f);world.RefreshCurrentZone();yield return null;
        Check(actor.CurrentChapter==ThanhGiongCampaignController.Chapter.Ascension,"walking into peak ritual area enters chapter4");
        foreach(var key in new[]{KeyCode.E,KeyCode.Q,KeyCode.E}){yield return WaitEquipment();Check(actor.TryPerformRitualStep(key),"ascension input "+key);yield return null;}
        driver.persistenceEnabled=true;
        Check(actor.OpenWorldFlightInProgress&&!driver.SaveCheckpoint()&&!driver.ReturnToHub(),"saving and return-to-hub denied during ending flight");
        driver.persistenceEnabled=false;
        float endingDeadline=Time.realtimeSinceStartup+15;
        while(actor.CurrentChapter!=ThanhGiongCampaignController.Chapter.Complete&&Time.realtimeSinceStartup<endingDeadline)yield return null;
        Check(actor.CurrentChapter==ThanhGiongCampaignController.Chapter.Complete,"full ascension animation reaches Complete");
        Check(SceneManager.GetActiveScene().handle.GetRawData()==sceneHandle&&SceneManager.sceneCount==1,"all four chapters retain same scene handle/count");
    }
    IEnumerator WaitEquipment(){float until=Time.realtimeSinceStartup+8;while(actor.EquipmentBusy&&Time.realtimeSinceStartup<until)yield return null;}
    void Check(bool ok,string message){if(ok)Passed++;else Failed++;results.Add((ok?"PASS ":"FAIL ")+message);}
}
