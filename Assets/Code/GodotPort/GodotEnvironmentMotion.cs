using System.Collections.Generic;
using UnityEngine;

public sealed class GodotEnvironmentMotion : MonoBehaviour {
 public float MotionTime {get;private set;}
 readonly List<Material> materials=new();
 readonly List<Transform> hammers=new();
 readonly List<Quaternion> hammerRest=new();
 readonly List<Light> fires=new();
 readonly List<Material> particleMaterials=new();
 readonly List<ParticleSystem> sparks=new();
 readonly Dictionary<Renderer,Material[]> originalMaterials=new();
 void Awake(){
  Shader shader=Shader.Find("ThanhGiong/LegendSurface");
  Shader foliageShader=Shader.Find("ThanhGiong/StylizedFoliageWind");
  foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true)){
   string path=Path(renderer.transform).ToLowerInvariant();
   if(path.Contains("meshy") || renderer is SkinnedMeshRenderer || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer.GetComponent<TextMesh>())continue;
   int kind=path.Contains("datlang")?1:path.Contains("duongdatuon")||path.Contains("sandat")||path.Contains("sanchien")||path.Contains("baichien")?2:path.Contains("triangularcloth")||path.Contains("goldclothedging")?4:path.Contains("dongsong")?5:path.Contains("culms")||path.Contains("leaves")||path.Contains("tree_default")||path.Contains("grass_large")||path.Contains("plant_bush")||path.Contains("crops_wheat")?3:0;
   bool culm=renderer.transform.name.StartsWith("Culms"),leaf=renderer.transform.name.StartsWith("Leaves");
   if(culm)kind=6;else if(leaf)kind=7;
   bool isFoliage=(kind==3||culm||leaf);
   Shader targetShader=(isFoliage&&foliageShader!=null)?foliageShader:shader;
   if(targetShader==null)continue;
   var originals=renderer.sharedMaterials;var converted=new Material[originals.Length];
   originalMaterials[renderer]=originals;
   for(int i=0;i<originals.Length;i++){
    Material old=originals[i],m=new(targetShader);m.name="Port "+renderer.name+" "+i;
    Color color=Color.white;Texture texture=null;string textureKey=null;
    if(old!=null){foreach(string key in new[]{"baseColorFactor","_BaseColorFactor","_BaseColor","_Color"})if(old.HasProperty(key)){color=old.GetColor(key);break;}
     foreach(string key in new[]{"baseColorTexture","_BaseColorTexture","_BaseMap","_MainTex"})if(old.HasProperty(key)){texture=old.GetTexture(key);if(texture!=null){textureKey=key;break;}}}
    if(kind==1)color=new Color(.34f,.43f,.27f);
    if(kind==2)color=new Color(.56f,.44f,.29f);
    if(kind==5)color=new Color(.29f,.47f,.48f);
    if(culm)color=path.Contains("trenga")?new Color(.78f,.70f,.39f):new Color(.35f,.50f,.25f);
    if(leaf)color=new Color(.38f,.55f,.28f);
    if(kind==3)color=new Color(.38f,.52f,.28f);
    m.SetColor("_BaseColor",color);if(texture!=null){m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",old.GetTextureScale(textureKey));m.SetTextureOffset("_BaseMap",old.GetTextureOffset(textureKey));}
    var filter=renderer.GetComponent<MeshFilter>();float height=filter&&filter.sharedMesh?filter.sharedMesh.bounds.size.y:1;
    if(isFoliage&&targetShader==foliageShader){
     m.SetColor("_CanopyColor",new Color(.92f,.98f,.70f,1f));
     m.SetColor("_UndergrowthColor",new Color(.24f,.38f,.18f,1f));
     m.SetFloat("_WrapLighting",.60f);
     m.SetColor("_TransmissionColor",new Color(.82f,.92f,.35f,1f));
     m.SetFloat("_TransmissionIntensity",.75f);
     m.SetFloat("_AmbientBoost",.38f);
     m.SetColor("_RimColor",new Color(.95f,.98f,.75f,1f));
     m.SetFloat("_RimPower",3.2f);
     m.SetFloat("_RimIntensity",.45f);
     m.SetFloat("_WindSpeed",culm?1.8f:1.6f);
     m.SetFloat("_WindTurbulence",2.4f);
     m.SetFloat("_TrunkSway",culm?.12f:Mathf.Clamp(height*.022f,.06f,.14f));
     m.SetFloat("_LeafFlutter",.042f);
     m.SetVector("_WindDirection",new Vector4(.85f,0f,.52f,0f));
     m.SetFloat("_HeightGradientScale",Mathf.Max(height,2.0f));
     m.SetFloat("_WindHeightThreshold",.15f);
     m.SetFloat("_Cull",0f);
    }else{
     m.SetFloat("_Mode",kind);
     m.SetFloat("_UseVertexColors",filter&&filter.sharedMesh&&filter.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)?1:0);
     m.SetFloat("_Height",Mathf.Max(height,.1f));m.SetFloat("_Amplitude",kind==4?.10f:height*.018f);m.SetFloat("_Clearing",path.Contains("san")||path.Contains("baichien")?1:0);
    }
    converted[i]=m;materials.Add(m);
   }renderer.sharedMaterials=converted;
  }
  foreach(Transform node in GetComponentsInChildren<Transform>(true)){
   if(node.name.StartsWith("WorkingHammer")){hammers.Add(node);hammerRest.Add(node.localRotation);}
   else if(node.name.StartsWith("FurnaceFlames"))Flames(node);
   else if(node.name.StartsWith("ChimneySmoke"))ForgeParticles(node,false);
   else if(node.name.StartsWith("HammerSparks"))ForgeParticles(node,true);
  }
  foreach(Light light in GetComponentsInChildren<Light>(true))if(light.type==LightType.Point)fires.Add(light);
 }
 static string Path(Transform t){string s=t.name;for(Transform p=t.parent;p!=null;p=p.parent)s=p.name+"/"+s;return s;}
 void Update(){
  if(Time.timeScale<=0)return;
  float previousTime=MotionTime;MotionTime+=Time.deltaTime;
  foreach(Material m in materials)m.SetFloat("_MotionTime",MotionTime);
  float phase=MotionTime%1.8f;float angle=phase<.9f?-.9f:phase<1.08f?Mathf.Lerp(-.9f,0,(phase-.9f)/.18f):phase<1.25f?Mathf.Lerp(0,-.9f,(phase-1.08f)/.17f):-.9f;
  for(int i=0;i<hammers.Count;i++)hammers[i].localRotation=hammerRest[i]*Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
  if(Mathf.FloorToInt((MotionTime-1.08f)/1.8f)>Mathf.FloorToInt((previousTime-1.08f)/1.8f))foreach(var spark in sparks)spark.Emit(12);
  foreach(Light fire in fires)fire.intensity=1.7f+Mathf.Sin(MotionTime*8.1f)*.22f+Mathf.Sin(MotionTime*13.7f)*.12f;
 }
 void Flames(Transform light){
  GameObject go=new("Native Forge Flames");go.transform.SetParent(light,false);
  go.transform.localRotation=Quaternion.Euler(-90,0,0);
  var ps=go.AddComponent<ParticleSystem>();ps.Stop();var main=ps.main;main.startLifetime=.8f;main.startSpeed=.5f;main.startSize=.45f;main.startColor=new Color(1,.4f,.06f,.6f);main.maxParticles=20;main.simulationSpace=ParticleSystemSimulationSpace.Local;
  var emission=ps.emission;emission.rateOverTime=12;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.15f;shape.angle=15;
  var renderer=go.GetComponent<ParticleSystemRenderer>();Shader shader=Shader.Find("Sprites/Default");if(shader){var material=new Material(shader);material.mainTexture=Resources.Load<Texture2D>("GodotPortParticles/fire_01");renderer.sharedMaterial=material;particleMaterials.Add(material);}ps.Play();
 }
 void ForgeParticles(Transform anchor,bool isSpark){
  GameObject go=new(isSpark?"Native Hammer Sparks":"Native Chimney Smoke");go.transform.SetParent(anchor,false);go.transform.localRotation=Quaternion.Euler(-90,0,0);
  var ps=go.AddComponent<ParticleSystem>();ps.Stop();var main=ps.main;main.startLifetime=isSpark?.45f:2.8f;main.startSpeed=isSpark?2.2f:.6f;main.startSize=isSpark?.055f:.65f;main.startColor=isSpark?new Color(1,.67f,.1f,1):new Color(.45f,.40f,.34f,.25f);main.maxParticles=40;main.simulationSpace=ParticleSystemSimulationSpace.World;
  var emission=ps.emission;emission.rateOverTime=isSpark?0:3;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.08f;shape.angle=isSpark?55:12;
  var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(main.startColor.color,0),new GradientColorKey(main.startColor.color,1)},new[]{new GradientAlphaKey(isSpark?1:.25f,0),new GradientAlphaKey(0,1)});color.color=gradient;
  Shader shader=Shader.Find("Sprites/Default");if(shader){var m=new Material(shader);m.mainTexture=Resources.Load<Texture2D>("GodotPortParticles/"+(isSpark?"spark_01":"smoke_01"));go.GetComponent<ParticleSystemRenderer>().sharedMaterial=m;particleMaterials.Add(m);}if(isSpark)sparks.Add(ps);ps.Play();
 }
 void OnDestroy(){foreach(var pair in originalMaterials)if(pair.Key)pair.Key.sharedMaterials=pair.Value;foreach(Material material in materials)if(material)Destroy(material);foreach(Material material in particleMaterials)if(material)Destroy(material);}
}
