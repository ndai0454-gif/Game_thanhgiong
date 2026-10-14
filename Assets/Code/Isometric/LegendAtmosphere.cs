using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bounded native ambience. Generated visuals have no gameplay colliders.</summary>
[DisallowMultipleComponent]
public sealed class LegendAtmosphere : MonoBehaviour {
 [SerializeField,Range(0,6)] int chapterIndex;
 [SerializeField] Transform terrainRoot;
 [SerializeField] Transform player;
 public float MotionTime {get;private set;}
 public int RippleCount => ripples.Count;
 public int RayCount => sunRays.Count;
 public int ForegroundCandidateCount => foreground.Count;
 public int MoteBudget => 36;
 public int MoteCount {get {int count=0;foreach(var system in particles)if(system)count+=system.particleCount;return count;}}
 public int FadedRendererCount {get {int count=0;foreach(var fader in foreground)if(fader.renderer&&fader.visibility<.995f)count++;return count;}}
 readonly List<Material> materials=new();
 readonly List<Ripple> ripples=new();
 readonly List<Fader> foreground=new();
 readonly List<ParticleSystem> particles=new();
 readonly List<Renderer> sunRays=new();
 readonly RaycastHit[] hits=new RaycastHit[32];
 GameObject generated;
 Transform recognitionDisc,driftRoot;
 Renderer recognitionDiscRenderer;
 Mesh quad;
 MaterialPropertyBlock phaseBlock;
 float probeAt;
 bool built;
 struct Ripple {public Transform transform;public Renderer renderer;public float offset;}
 sealed class Fader {public Renderer renderer;public MaterialPropertyBlock original,working;public float visibility=1;}

 /// <summary>Call in editor installer before play; references are serialized.</summary>
 public void Configure(int index,Transform environment,Transform hero){
  chapterIndex=Mathf.Clamp(index,0,6);terrainRoot=environment;player=hero;
  if(Application.isPlaying&&built){Cleanup();Build();}
 }
 void Awake(){Resolve();}
 void Start(){Build();}
 void Resolve(){
  string sceneName=gameObject.scene.name;
  string[] chapters={"LangGiongTienTuyen","KinhThanhRenThep","PhaoDaiNgamQuanAn","ThungLungVuotSong","TranTuyenNuiSoc","DinhSocHoaThanh","AlbionForestMap"};
  for(int i=0;i<chapters.Length;i++)if(sceneName==chapters[i]){chapterIndex=i;break;}
  if(!player){var hero=FindAnyObjectByType<MountedHorseController>();if(hero)player=hero.transform;}
  if(!terrainRoot){var motion=FindAnyObjectByType<GodotEnvironmentMotion>();if(motion)terrainRoot=motion.transform;else terrainRoot=transform;}
 }
 void Build(){
  if(built)return;Resolve();Shader shader=Shader.Find("ThanhGiong/LegendAtmosphere");
  if(!shader)return;
  built=true;generated=new GameObject("Legend Native Atmosphere");generated.transform.SetParent(transform,false);
  phaseBlock=new MaterialPropertyBlock();
  quad=new Mesh{name="Legend atmosphere quad"};quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)};
  quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};quad.colors=new[]{Color.white,Color.white,Color.white,Color.white};quad.triangles=new[]{0,2,1,1,2,3};quad.RecalculateBounds();
  var discMaterial=Material(shader,"Warm hero recognition",new Color(1,.72f,.32f,.12f),2);
  recognitionDisc=Quad("Warm hero recognition",discMaterial);recognitionDisc.localScale=new Vector3(4.2f,4.2f,1);
  recognitionDiscRenderer=recognitionDisc.GetComponent<Renderer>();
  var drift=new GameObject("Drifting lights");drift.transform.SetParent(generated.transform,false);driftRoot=drift.transform;
  driftRoot.position=player?new Vector3(player.position.x,0,player.position.z):terrainRoot.position;
  CreateParticles(shader,false);CreateParticles(shader,true);
  CreateSunRays(shader);
  var rippleMaterial=Material(shader,"Soft water rings",new Color(.58f,.73f,.68f,.22f),1);
  int waterCount=0;
  // A map can have multiple imported environment roots. Do not let FindAny's
  // arbitrary first root omit the river or the foreground on scene reload.
  foreach(Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   if(renderer.gameObject.scene!=gameObject.scene||renderer.transform.IsChildOf(generated.transform)||
    renderer.GetComponentInParent<MountedHorseController>()||renderer.GetComponentInParent<ThanhGiongEnemy>())continue;
   string path=Path(renderer.transform).ToLowerInvariant();
   if(path.Contains("dongsong")||path.Contains("riverwater")||path.Contains("waterplane")){
    Bounds bounds=renderer.bounds;
    for(int i=0;i<6&&ripples.Count<12;i++){
     float x=Frac((i+1)*.6180339f),z=Frac((i+1)*.4142135f);
     var ring=Quad("Water ring "+waterCount+" "+i,rippleMaterial);
     ring.position=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x),bounds.max.y+.025f,Mathf.Lerp(bounds.min.z,bounds.max.z,z));
     ripples.Add(new Ripple{transform=ring,renderer=ring.GetComponent<Renderer>(),offset=i*.47f+waterCount});
    }waterCount++;
   }
   if(renderer is MeshRenderer&&!renderer.GetComponent<TextMesh>()&&renderer.bounds.size.y>2&&foreground.Count<128&&
    (path.Contains("tree")||path.Contains("luytre")||path.Contains("trenga")||path.Contains("nhatranh")||path.Contains("nha tranh")||path.Contains("stone_large"))){
    bool supported=false;foreach(var material in renderer.sharedMaterials)if(material&&material.HasProperty("_Visibility")){supported=true;break;}
    if(supported){var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);foreground.Add(new Fader{renderer=renderer,original=original,working=new MaterialPropertyBlock()});}
   }
  }
  RefreshDisc();
 }
 Material Material(Shader shader,string label,Color color,int shape){var material=new Material(shader){name=label};material.SetColor("_BaseColor",color);material.SetFloat("_Shape",shape);materials.Add(material);return material;}
 Transform Quad(string label,Material material){var go=new GameObject(label);go.transform.SetParent(generated.transform,false);go.transform.rotation=Quaternion.Euler(90,0,0);go.AddComponent<MeshFilter>().sharedMesh=quad;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;}
 void CreateSunRays(Shader shader){
  // Painted, world-anchored accents. They face the initial isometric camera;
  // later camera travel never drags their anchor points through the scenery.
  Camera camera=Camera.main;
  Quaternion facing=camera?camera.transform.rotation*Quaternion.Euler(0,0,-25):Quaternion.Euler(35,45,-25);
  Vector3 right=camera?Vector3.ProjectOnPlane(camera.transform.right,Vector3.up).normalized:Vector3.right;
  Vector3 forward=camera?Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized:Vector3.forward;
  Vector3 origin=player?player.position:terrainRoot.position;
  Material material=Material(shader,"Very faint painted sunshine",new Color(1,.80f,.45f,.04f),3);
  for(int i=0;i<4;i++){
   float height=6.5f+i*.7f;
   Transform beam=Quad("Painted golden sun ray "+i,material);
   beam.rotation=facing;
   beam.position=origin+right*(-7.5f+i*4.5f)+forward*(6+i%2*3.5f)+Vector3.up*(height*.43f);
   beam.localScale=new Vector3(.65f+i*.15f,height,1);
   sunRays.Add(beam.GetComponent<Renderer>());
  }
 }
 void CreateParticles(Shader shader,bool firefly){
  var go=new GameObject(firefly?"Warm fireflies":"Pollen motes");go.transform.SetParent(driftRoot,false);
  var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  system.useAutoRandomSeed=false;system.randomSeed=(uint)(4107+chapterIndex*73+(firefly?19:0));
  var main=system.main;main.maxParticles=firefly?12:24;main.startLifetime=firefly?8:10;main.startSpeed=firefly?.07f:.045f;main.startSize=firefly?.16f:.085f;main.startColor=Color.white;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=false;
  var emission=system.emission;emission.rateOverTime=firefly?1.5f:2.4f;
  var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(26,firefly?2.4f:5,26);shape.position=new Vector3(0,firefly?1.8f:3.2f,0);
  var velocity=system.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=new ParticleSystem.MinMaxCurve(-.10f,.12f);velocity.y=new ParticleSystem.MinMaxCurve(.025f,.07f);velocity.z=new ParticleSystem.MinMaxCurve(-.07f,.08f);
  var tint=system.colorOverLifetime;tint.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.75f,.22f),new GradientAlphaKey(.55f,.7f),new GradientAlphaKey(0,1)});tint.color=gradient;
  var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Material(shader,firefly?"Golden firefly glow":"Quiet pollen glow",firefly?new Color(1,.77f,.36f,.7f):new Color(.88f,.88f,.67f,.25f),0);renderer.shadowCastingMode=ShadowCastingMode.Off;
  particles.Add(system);system.Play();system.Simulate(firefly?6:8,true,false,true);system.Play();
 }
 void Update(){
  if(!built||Time.timeScale<=0)return;MotionTime+=Time.deltaTime;
  if(player&&driftRoot)driftRoot.position=new Vector3(player.position.x,0,player.position.z);
  if(phaseBlock==null)phaseBlock=new MaterialPropertyBlock();if(recognitionDisc&&recognitionDiscRenderer){if(player)recognitionDisc.position=new Vector3(player.position.x,recognitionDisc.position.y,player.position.z);phaseBlock.Clear();phaseBlock.SetFloat("_Phase",MotionTime);recognitionDiscRenderer.SetPropertyBlock(phaseBlock);}
  foreach(var ring in ripples){float phase=Frac((MotionTime+ring.offset)/3.4f);ring.transform.localScale=Vector3.one*Mathf.Lerp(1.1f,3.2f,phase);phaseBlock.Clear();phaseBlock.SetFloat("_Phase",phase);ring.renderer.SetPropertyBlock(phaseBlock);}
  for(int i=0;i<sunRays.Count;i++){phaseBlock.Clear();phaseBlock.SetFloat("_Phase",MotionTime*.38f+i*.9f);sunRays[i].SetPropertyBlock(phaseBlock);}
  if(MotionTime>=probeAt){probeAt=MotionTime+.12f;RefreshDisc();UpdateForeground();}
 }
 void RefreshDisc(){
  if(!player||!recognitionDisc)return;float y=0;
  int count=Physics.RaycastNonAlloc(player.position+Vector3.up*5,Vector3.down,hits,15,~0,QueryTriggerInteraction.Ignore);float distance=float.PositiveInfinity;
  for(int i=0;i<count;i++)if(hits[i].collider&&!hits[i].collider.transform.IsChildOf(player)&&hits[i].normal.y>.6f&&hits[i].distance<distance){distance=hits[i].distance;y=hits[i].point.y;}
  recognitionDisc.position=new Vector3(player.position.x,y+.035f,player.position.z);
 }
 void UpdateForeground(){
  Camera camera=Camera.main;if(!camera||!player)return;
  Vector3 focus=player.position+Vector3.up*1.4f;
  Ray ray=camera.ScreenPointToRay(camera.WorldToScreenPoint(focus));
  float heroDepth=Vector3.Dot(focus-ray.origin,ray.direction);
  foreach(var fader in foreground){if(!fader.renderer)continue;float target=1;Bounds bounds=fader.renderer.bounds;bounds.Expand(2f);
   if(bounds.IntersectRay(ray,out float hit)&&hit<heroDepth-1.5f)target=.26f;
   fader.visibility=Mathf.MoveTowards(fader.visibility,target,.40f);
   if(fader.visibility>.995f){fader.renderer.SetPropertyBlock(fader.original);continue;}
   fader.renderer.GetPropertyBlock(fader.working);fader.working.SetFloat("_Visibility",fader.visibility);fader.renderer.SetPropertyBlock(fader.working);
  }
 }
 void OnDisable(){foreach(var fader in foreground)if(fader.renderer)fader.renderer.SetPropertyBlock(fader.original);foreach(var system in particles)if(system)system.Pause();if(generated)generated.SetActive(false);}
 void OnEnable(){if(built){if(generated)generated.SetActive(true);foreach(var system in particles)if(system)system.Play();}}
 void OnDestroy(){Cleanup();}
 void Cleanup(){foreach(var fader in foreground)if(fader.renderer)fader.renderer.SetPropertyBlock(fader.original);foreground.Clear();ripples.Clear();sunRays.Clear();particles.Clear();if(generated)Destroy(generated);if(quad)Destroy(quad);foreach(var material in materials)if(material)Destroy(material);materials.Clear();recognitionDisc=null;recognitionDiscRenderer=null;driftRoot=null;built=false;MotionTime=0;}
 static float Frac(float value)=>value-Mathf.Floor(value);
 static string Path(Transform node){string result=node.name;for(var parent=node.parent;parent;parent=parent.parent)result=parent.name+"/"+result;return result;}
}
