using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GodotPortRuntimeVerifier : MonoBehaviour {
 public static readonly string[] Maps={"LangGiongTienTuyen","KinhThanhRenThep","PhaoDaiNgamQuanAn","ThungLungVuotSong","TranTuyenNuiSoc","DinhSocHoaThanh"};
 readonly List<string> results=new();int failures;
 string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../PortVerification"));
 void Check(bool value,string caption){string s=(value?"PASS ":"FAIL ")+caption;results.Add(s);Debug.Log("GODOT_PORT_CHECK "+s);if(!value)failures++;}
 void Start(){DontDestroyOnLoad(gameObject);Directory.CreateDirectory(Folder);StartCoroutine(Run());}
 IEnumerator Run(){
  for(int index=0;index<Maps.Length;index++){
   Time.timeScale=1;
   if(index==0)yield return SceneManager.LoadSceneAsync(Maps[index]);
   else {
    var menu=FindFirstObjectByType<ThanhGiongCampaignHUD>();menu.TogglePause();
    bool opened=menu.SelectChapter(index);Check(opened,"pause menu selects "+Maps[index]);
    if(opened){while(ThanhGiongSceneTransition.IsTransitioning)yield return null;}
    else yield return SceneManager.LoadSceneAsync(Maps[index]);
   }
   yield return new WaitForSeconds(.8f);
   var campaign=FindFirstObjectByType<ThanhGiongCampaignController>();var motion=FindFirstObjectByType<GodotMountedMotion>();var audio=FindFirstObjectByType<ThanhGiongCampaignAudio>();var hud=FindFirstObjectByType<ThanhGiongCampaignHUD>();var environment=FindFirstObjectByType<GodotEnvironmentMotion>();
   Check(campaign&&motion&&motion.IsRigReady&&motion.HorseBoneCount==17&&motion.RiderBoneCount==21,Maps[index]+" native two skins / complete bone rig");
   Check(audio&&audio.Region==ThanhGiongCampaignAudio.RegionForScene(Maps[index])&&audio.GetComponents<AudioSource>().Any(x=>x.loop&&x.isPlaying&&x.clip),Maps[index]+" regional audio loops play");
   Check(hud!=null,Maps[index]+" timed native HUD present");
   Check(index==6||environment!=null,Maps[index]+" environment motion present");
   Check(campaign&&campaign.GetComponent<CharacterController>().isGrounded,Maps[index]+" mounted actor grounded");
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,Maps[index]+".png"));yield return null;
   if(index==1&&motion){
    Check(environment.GetComponentsInChildren<ParticleSystem>().Length>=6,"both forges have native flame smoke and sparks");
    campaign.enabled=false;motion.SyncEquipment(1,0);
    foreach(string action in new[]{"equip_armor","equip_helmet","remove_helmet","remove_armor"}){
     motion.PlayEquipment(action);yield return new WaitForSeconds(.35f);Check(motion.IsBusy,action+" busy during gesture");
     Time.timeScale=0;float before=environment.MotionTime;Quaternion pose=motion.visual.GetComponentsInChildren<Transform>(true).First(x=>x.name=="cape").localRotation;
     yield return new WaitForSecondsRealtime(.15f);Check(Mathf.Approximately(environment.MotionTime,before)&&Quaternion.Angle(pose,motion.visual.GetComponentsInChildren<Transform>(true).First(x=>x.name=="cape").localRotation)<.01f,action+" pause freezes rig and environment");Time.timeScale=1;
     yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,action+".png"));
     yield return new WaitForSeconds(1.65f);Check(!motion.IsBusy,action+" completes and unlocks movement");
    }
    Check(!hud.MissionVisible,"mission popup expires after its display duration");
    hud.RecallPopups();Check(hud.MissionVisible&&hud.ControlsVisible,"Tab recall restores timed popups");
    hud.TogglePause();yield return null;
    var music=audio.GetComponents<AudioSource>().First(x=>x.loop&&x.clip);int sample=music.timeSamples;
    float pausedClock=environment.MotionTime;yield return new WaitForSecondsRealtime(.2f);
    Check(hud.IsPaused&&music.timeSamples==sample&&Mathf.Approximately(pausedClock,environment.MotionTime),"pause HUD freezes regional audio and environment");
    Check(hud.ChapterCount==6,"pause menu includes six chapters and forest prototype");
    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,"pause_menu.png"));
    hud.TogglePause();campaign.enabled=true;
   }
   if(index==2&&campaign){
    yield return GodotGameplayPortChecks.CheckHealth(campaign,(ok,message)=>Check(ok,message));
   }
   if(index==5){
    hud.TogglePause();bool restarted=hud.RestartCurrentMap();Check(restarted,"pause menu restarts current map");
    if(restarted){while(ThanhGiongSceneTransition.IsTransitioning)yield return null;yield return new WaitForSeconds(.3f);Check(SceneManager.GetActiveScene().name==Maps[index]&&FindFirstObjectByType<GodotMountedMotion>().IsRigReady&&Time.timeScale==1,"restarted map has active native rig and resumes time");}
   }
  }
  Time.timeScale=1;string status="GODOT_PORT_VERIFY "+(failures==0?"PASS":"FAIL")+" failures="+failures;results.Add(status);File.WriteAllLines(Path.Combine(Folder,"runtime_results.txt"),results);Debug.Log(status);Destroy(gameObject);
 }
}
