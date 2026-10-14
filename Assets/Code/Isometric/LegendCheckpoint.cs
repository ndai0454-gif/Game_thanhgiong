using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(150)]
public sealed class LegendCheckpoint : MonoBehaviour
{
    public float activationRadius=2.2f;
    public bool HasCheckpoint => saved!=null;
    public string CheckpointLabel => saved!=null?saved.label:"ĐẦU CHƯƠNG";
    public int BeaconCount => beacons.Count;
    public Vector3 CheckpointPosition => saved!=null?saved.position:transform.position;
    const string Prefix="ThanhGiong.LegendCheckpoint.v1.";
    public static string StorageKeyForScene(string scene)=>Prefix+scene;
    ThanhGiongCampaignController campaign; LegendSeedSkills skills; Snapshot saved;
    readonly List<Beacon> beacons=new List<Beacon>();readonly List<Material> materials=new List<Material>();
    GameObject markerRoot;
    sealed class Beacon {public string label;public Vector3 position;public bool inside;}
    [Serializable] sealed class Snapshot {public int version=1;public string scene,label;public Vector3 position;public Quaternion rotation;public ThanhGiongCampaignController.CheckpointState campaign;public LegendSeedSkills.State seeds;public List<EnemyState> enemies=new List<EnemyState>();public List<CollectibleState> items=new List<CollectibleState>();}
    [Serializable] sealed class EnemyState {public string path;public Vector3 position;public Quaternion rotation;public float health;public bool alive;}
    [Serializable] sealed class CollectibleState {public string path,namePath;public bool active;}
    IEnumerator Start()
    {
        campaign=GetComponent<ThanhGiongCampaignController>();skills=GetComponent<LegendSeedSkills>();
        yield return null; // Campaign and skill components have initialized their scene state.
        string scene=SceneManager.GetActiveScene().name;
        string json=PlayerPrefs.GetString(Prefix+scene,string.Empty);
        if(!string.IsNullOrEmpty(json)) {
            try{saved=JsonUtility.FromJson<Snapshot>(json);}catch(Exception){saved=null;}
            if(saved==null||saved.version!=1||saved.scene!=scene||saved.campaign==null)saved=null;
        }
        if(saved!=null&&!RestartCheckpoint())saved=null;
        if(saved==null)ActivateCheckpoint("ĐẦU CHƯƠNG");
        AddBeacon(transform.position+transform.forward*7,"ĐIỂM DỪNG CHÂN");
    }
    void Update()
    {
        if(Time.timeScale<=0||campaign==null||campaign.IsDead||campaign.EquipmentBusy||ThanhGiongSceneTransition.IsTransitioning)return;
        foreach(Beacon beacon in beacons) {
            Vector3 delta=transform.position-beacon.position;delta.y=0;
            bool inside=delta.sqrMagnitude<=activationRadius*activationRadius;
            if(inside&&!beacon.inside)ActivateCheckpoint(beacon.label);
            beacon.inside=inside;
        }
    }
    public bool AddBeacon(Vector3 position,string label)
    {
        if(skills==null)skills=GetComponent<LegendSeedSkills>();
        if(skills==null||!skills.TryGround(position,out Vector3 supported))return false;
        foreach(Beacon b in beacons)if((b.position-supported).sqrMagnitude<1)return false;
        beacons.Add(new Beacon {position=supported,label=string.IsNullOrEmpty(label)?"ĐIỂM DỪNG CHÂN":label});
        if(markerRoot==null)markerRoot=new GameObject("WorldCheckpoint Beacons");
        GameObject marker=new GameObject("WorldCheckpoint "+label);marker.transform.SetParent(markerRoot.transform,false);marker.transform.position=supported;
        Shader shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)shader=Shader.Find("Sprites/Default");if(shader==null)shader=Shader.Find("Unlit/Color");
        Material material=shader!=null?new Material(shader):null;Color gold=new Color(.77f,.61f,.29f);if(material!=null){material.SetColor("_BaseColor",gold);material.SetColor("_Color",gold);materials.Add(material);}
        var ring=marker.AddComponent<LineRenderer>();ring.sharedMaterial=material;ring.useWorldSpace=false;ring.loop=true;ring.positionCount=48;ring.widthMultiplier=.025f;ring.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*.64f,.015f,Mathf.Sin(a)*.64f));}
        for(int i=0;i<8;i++){var spoke=new GameObject("Bronze Sun Ray");spoke.transform.SetParent(marker.transform,false);var line=spoke.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.positionCount=2;line.widthMultiplier=.022f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;float a=i*Mathf.PI/4;Vector3 ray=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));line.SetPosition(0,ray*.23f+Vector3.up*.015f);line.SetPosition(1,ray*.43f+Vector3.up*.015f);}
        GameObject glow=new GameObject("Checkpoint Warm Light");glow.transform.SetParent(marker.transform,false);glow.transform.localPosition=Vector3.up*.3f;Light light=glow.AddComponent<Light>();light.color=gold;light.range=3;light.intensity=.65f;light.shadows=LightShadows.None;
        return true;
    }
    public bool ActivateCheckpoint(string label)
    {
        if(campaign==null)campaign=GetComponent<ThanhGiongCampaignController>();if(skills==null)skills=GetComponent<LegendSeedSkills>();
        if(campaign==null||campaign.IsDead||campaign.EquipmentBusy||ThanhGiongSceneTransition.IsTransitioning)return false;
        saved=new Snapshot {scene=SceneManager.GetActiveScene().name,label=label,position=transform.position,rotation=transform.rotation,campaign=campaign.CaptureCheckpointState(),seeds=skills!=null?skills.CaptureState():null};
        foreach(ThanhGiongEnemy enemy in FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include))saved.enemies.Add(new EnemyState {path=Path(enemy.transform),position=enemy.transform.position,rotation=enemy.transform.rotation,health=enemy.HealthRatio*enemy.maxHealth,alive=enemy.gameObject.activeInHierarchy&&enemy.HealthRatio>0});
        foreach(ThanhGiongCollectible item in FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include))saved.items.Add(new CollectibleState {path=Path(item.transform),namePath=NamedPath(item.transform),active=item.gameObject.activeSelf});
        PlayerPrefs.SetString(Prefix+saved.scene,JsonUtility.ToJson(saved));PlayerPrefs.Save();
        campaign.ShowMessage("ĐIỂM DỪNG CHÂN · "+label,2f);GetComponent<ThanhGiongCampaignAudio>()?.PlayCue("arrival",.4f);return true;
    }
    public bool RestartCheckpoint()
    {
        if(saved==null||campaign==null||campaign.EquipmentBusy||ThanhGiongSceneTransition.IsTransitioning)return false;
        if(!campaign.RestoreCheckpointState(saved.campaign,saved.position,saved.rotation))return false;
        if(skills!=null)skills.RestoreState(saved.seeds);
        HashSet<ThanhGiongEnemy> restoredEnemies=new HashSet<ThanhGiongEnemy>();ThanhGiongEnemy[] sceneEnemies=FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(EnemyState state in saved.enemies) {
            Transform node=Resolve(state.path);ThanhGiongEnemy enemy=node!=null?node.GetComponent<ThanhGiongEnemy>():null;
            if(enemy==null||restoredEnemies.Contains(enemy))enemy=FindNearestEnemy(sceneEnemies,restoredEnemies,state.position);
            if(enemy!=null){enemy.RestoreCheckpoint(campaign,transform,state.position,state.rotation,state.health,state.alive);restoredEnemies.Add(enemy);}
        }
        foreach(CollectibleState state in saved.items)
        {
            Transform node=string.IsNullOrEmpty(state.namePath)?Resolve(state.path):ResolveNamed(state.namePath);
            if(node==null||node.GetComponent<ThanhGiongCollectible>()==null)continue;
            // Old saves used sibling indices. Newly inserted scene objects can occupy
            // an old index; do not apply that unrelated old state to new supply groves.
            if(string.IsNullOrEmpty(state.namePath)&&node.root.name=="COMBAT - BAMBOO RESUPPLY")continue;
            node.gameObject.SetActive(state.active);
        }
        foreach(Beacon beacon in beacons)beacon.inside=(transform.position-beacon.position).sqrMagnitude<activationRadius*activationRadius;
        return true;
    }
    public static void ForgetScene(string scene){PlayerPrefs.DeleteKey(Prefix+scene);PlayerPrefs.Save();}
    static string NamedPath(Transform node)
    {
        string path=node.name;while(node.parent!=null){node=node.parent;path=node.name+"/"+path;}return path;
    }
    static Transform ResolveNamed(string path)
    {
        string[] parts=path.Split('/');Transform node=null;
        foreach(GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())if(root.name==parts[0]){node=root.transform;break;}
        for(int i=1;i<parts.Length&&node!=null;i++)node=node.Find(parts[i]);return node;
    }
    static string Path(Transform node)
    {
        string path=node.GetSiblingIndex().ToString();while(node.parent!=null){node=node.parent;path=node.GetSiblingIndex()+"/"+path;}return path;
    }
    static Transform Resolve(string path)
    {
        string[] parts=path.Split('/');GameObject[] roots=SceneManager.GetActiveScene().GetRootGameObjects();
        if(parts.Length==0||!int.TryParse(parts[0],out int root)||root<0||root>=roots.Length)return null;
        Transform node=roots[root].transform;
        for(int i=1;i<parts.Length;i++){if(!int.TryParse(parts[i],out int child)||child<0||child>=node.childCount)return null;node=node.GetChild(child);}return node;
    }
    static ThanhGiongEnemy FindNearestEnemy(ThanhGiongEnemy[] enemies,HashSet<ThanhGiongEnemy> used,Vector3 position)
    {
        ThanhGiongEnemy best=null;float bestSqr=float.PositiveInfinity;
        foreach(ThanhGiongEnemy enemy in enemies){if(enemy==null||used.Contains(enemy))continue;float sqr=(enemy.transform.position-position).sqrMagnitude;if(sqr<bestSqr){best=enemy;bestSqr=sqr;}}
        return best;
    }
    void OnDestroy(){foreach(Material material in materials)if(material)Destroy(material);if(markerRoot)Destroy(markerRoot);}
}
