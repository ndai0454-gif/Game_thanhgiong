using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class GodotPortImporter {
 public static readonly string[] Maps={"LangGiongTienTuyen","KinhThanhRenThep","PhaoDaiNgamQuanAn","ThungLungVuotSong","TranTuyenNuiSoc","DinhSocHoaThanh"};
 const string Folder="Assets/GodotPort/";
 [MenuItem("Tools/Thanh Giong/Godot Port/Reimport Models")]
 public static void Reimport(){foreach(string file in Directory.GetFiles(Folder+"Models","*.glb")){AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);Debug.Log("PORT_ASSET "+file+" "+AssetDatabase.LoadMainAssetAtPath(file));}}
 [Serializable]class Shape{public string name,kind;public float[] position,rotation,size;public float radius,height;}
 [Serializable]class MapData{public string map;public Shape[] colliders;}
 [MenuItem("Tools/Thanh Giong/Godot Port/Import All Maps")]
 public static void ImportAll(){
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit play mode before import");
  Asset("MountedGiong");Asset("TreNga");foreach(string map in Maps)Asset(map+"_Environment");
  EditorSceneManager.SaveOpenScenes();
  var setup=EditorSceneManager.GetSceneManagerSetup();
  foreach(string map in Maps)ImportMap(map);
  string prototype="Assets/Scenes/AlbionForestMap.unity";
  if(File.Exists(prototype))ImportActorOnly(prototype);
  EditorBuildSettings.scenes=Maps.Select(m=>new EditorBuildSettingsScene("Assets/Scenes/ThanhGiongWorld/"+m+".unity",true)).Concat(new[]{new EditorBuildSettingsScene(prototype,true)}).ToArray();
  AssetDatabase.SaveAssets();
  EditorSceneManager.OpenScene("Assets/Scenes/ThanhGiongWorld/LangGiongTienTuyen.unity");
  Debug.Log("GODOT_PORT_IMPORT PASS six campaign maps + prototype, native rig/audio/UI/environment");
 }
 static GameObject Asset(string name){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Models/"+name+".glb");if(prefab==null)throw new Exception("Missing imported model "+name);return prefab;}
 static void ImportMap(string map){
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/ThanhGiongWorld/"+map+".unity");
  var campaign=UnityEngine.Object.FindFirstObjectByType<ThanhGiongCampaignController>(FindObjectsInactive.Include);
  if(campaign==null)throw new Exception("No campaign in "+map);
  var existing=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Godot Legend Environment");if(existing)UnityEngine.Object.DestroyImmediate(existing);
  var oldCollisions=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Godot Environment Collisions");if(oldCollisions)UnityEngine.Object.DestroyImmediate(oldCollisions);
  foreach(var renderer in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Renderer>(true)).ToArray())if(!Keep(renderer.transform,campaign))UnityEngine.Object.DestroyImmediate(renderer);
  foreach(var collider in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Collider>(true)).ToArray())if(!Keep(collider.transform,campaign))UnityEngine.Object.DestroyImmediate(collider);
  // Old full-screen postprocessing is superseded by the new material/fog direction.
  foreach(var volume in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None))volume.enabled=false;
  var environment=(GameObject)PrefabUtility.InstantiatePrefab(Asset(map+"_Environment"),scene);
  environment.name="Godot Legend Environment";environment.transform.rotation=Quaternion.Euler(0,180,0);
  var data=JsonUtility.FromJson<MapData>(File.ReadAllText(Folder+"Data/"+map+".json"));
  var collisions=new GameObject("Godot Environment Collisions");
  foreach(var shape in data.colliders){var go=new GameObject(shape.name);go.transform.SetParent(collisions.transform);go.transform.position=new Vector3(shape.position[0],shape.position[1],shape.position[2]);go.transform.rotation=new Quaternion(shape.rotation[0],shape.rotation[1],shape.rotation[2],shape.rotation[3]);
   if(shape.kind=="capsule"){var c=go.AddComponent<CapsuleCollider>();c.radius=shape.radius;c.height=shape.height;}else{var c=go.AddComponent<BoxCollider>();c.size=new Vector3(shape.size[0],shape.size[1],shape.size[2]);}}
  environment.AddComponent<GodotEnvironmentMotion>();
  var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(x=>x.type==LightType.Directional);if(sun){sun.transform.rotation=Quaternion.Euler(48,32,0);sun.color=new Color(1,.92f,.75f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;}
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.58f,.64f,.55f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0035f;RenderSettings.fogColor=new Color(.64f,.70f,.61f);
  ReplaceActor(campaign);
  foreach(var item in UnityEngine.Object.FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(item.kind==ThanhGiongCollectible.Kind.Bamboo){
   foreach(var renderer in item.GetComponentsInChildren<Renderer>(true))if(!(renderer is MeshRenderer&&renderer.GetComponent<TextMesh>()))renderer.enabled=false;
   var grove=(GameObject)PrefabUtility.InstantiatePrefab(Asset("TreNga"));grove.transform.SetParent(item.transform,false);grove.transform.localRotation=Quaternion.Euler(0,180,0);item.visualRoot=grove.transform;item.spinSpeed=0;grove.AddComponent<GodotEnvironmentMotion>();}
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  Debug.Log("GODOT_PORT_MAP "+map+" colliders="+data.colliders.Length);
 }
 static bool Keep(Transform t,ThanhGiongCampaignController campaign){
  if(t==campaign.transform||t.IsChildOf(campaign.transform))return true;
  for(var p=t;p!=null;p=p.parent)if(p.GetComponent<ThanhGiongEnemy>()||p.GetComponent<ThanhGiongCollectible>()||p.GetComponent<ThanhGiongImprovisedWeapon>()||p.GetComponent<ThanhGiongRoofCollapse>()||p.GetComponent<ThanhGiongStoryBeatZone>())return true;
  return false;
 }
 static void ReplaceActor(ThanhGiongCampaignController campaign){
  var controller=campaign.GetComponent<MountedHorseController>();var old=controller.visual?controller.visual:campaign.transform.Find("Visual");
  Vector3 p=old?old.localPosition:Vector3.zero,s=old?old.localScale:Vector3.one;Quaternion r=old?old.localRotation:Quaternion.identity;
  if(old&&old!=campaign.transform)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var wrapper=new GameObject("Visual");wrapper.transform.SetParent(campaign.transform,false);wrapper.transform.localPosition=p;wrapper.transform.localRotation=r;wrapper.transform.localScale=s;
  var meshy=AssetDatabase.LoadAssetAtPath<GameObject>(ThanhGiong.EditorTools.MeshyMountedInstaller.PrefabPath);
  var model=(GameObject)PrefabUtility.InstantiatePrefab(meshy?meshy:Asset("MountedGiong"));model.transform.SetParent(wrapper.transform,false);model.transform.localRotation=meshy?Quaternion.identity:Quaternion.Euler(0,180,0);
  controller.visual=wrapper.transform;controller.frontLeftLeg=null;controller.frontRightLeg=null;controller.rearLeftLeg=null;controller.rearRightLeg=null;
  var motion=campaign.GetComponent<GodotMountedMotion>();if(!motion)motion=campaign.gameObject.AddComponent<GodotMountedMotion>();motion.visual=wrapper.transform;
  var feedback=campaign.GetComponent<GodotCombatFeedback>();if(!feedback)campaign.gameObject.AddComponent<GodotCombatFeedback>();
  foreach(var cape in campaign.GetComponents<ThanhGiongRibbonCape>())cape.enabled=false;
 }
 static void ImportActorOnly(string path){var scene=EditorSceneManager.OpenScene(path);var campaign=UnityEngine.Object.FindFirstObjectByType<ThanhGiongCampaignController>();if(campaign)ReplaceActor(campaign);EditorSceneManager.SaveScene(scene);}
}
