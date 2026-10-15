using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatBambooInstaller
{
    const string PrefabPath="Assets/Prefabs/Environment/Foliage/BambooGrove_Vietnamese_Legend.prefab";
    const string RootName="COMBAT - BAMBOO RESUPPLY";
    static readonly Dictionary<string,Vector2[]> Layout=new Dictionary<string,Vector2[]>
    {
        {"PhaoDaiNgamQuanAn",new[]{new Vector2(-14,4),new Vector2(-14,17),new Vector2(16,5),new Vector2(16,18),new Vector2(-5,-7),new Vector2(6,-7)}},
        {"ThungLungVuotSong",new[]{new Vector2(-28,-38),new Vector2(-20,-38),new Vector2(-29,-16),new Vector2(-19,-16),new Vector2(-28,-10),new Vector2(-20,-10)}},
        {"TranTuyenNuiSoc",new[]{new Vector2(-14,-26),new Vector2(14,-26),new Vector2(-14,-15),new Vector2(14,-15),new Vector2(-5,-35),new Vector2(5,-35)}}
    };

    [MenuItem("Tools/Thánh Gióng/Environment/Add Combat Bamboo Groves")]
    public static void Install()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null)throw new InvalidOperationException("Missing bamboo asset: "+PrefabPath);
        EditorSceneManager.SaveOpenScenes();var setup=EditorSceneManager.GetSceneManagerSetup();
        string folder="CombatBambooVerification";Directory.CreateDirectory(folder);
        string backup=folder+"/Backups/"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        var report=new List<string>();
        try
        {
            foreach(var entry in Layout)
            {
                string path="Assets/Scenes/ThanhGiongWorld/"+entry.Key+".unity";
                var scene=EditorSceneManager.OpenScene(path);
                var enemies=UnityEngine.Object.FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                if(enemies.Length==0)throw new InvalidOperationException("Map has no enemies: "+entry.Key);
                File.Copy(path,Path.Combine(backup,Path.GetFileName(path)),true);
                var root=scene.GetRootGameObjects().FirstOrDefault(x=>x.name==RootName);
                if(root==null)root=new GameObject(RootName);
                var placed=new List<Vector3>();
                Physics.SyncTransforms();
                for(int i=0;i<entry.Value.Length;i++)
                {
                    string name="Khóm Tre Tiếp Chiến "+(i+1);
                    var existing=root.transform.Find(name);
                    Vector3 position=existing!=null?existing.position:FindPlacement(entry.Key,entry.Value[i],placed,enemies);
                    var grove=existing!=null?existing.gameObject:new GameObject(name);
                    grove.transform.SetParent(root.transform,false);grove.transform.position=position;
                    Transform oldVisual=grove.transform.Find("MeshyVisual");if(oldVisual!=null)UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(prefab,grove.transform);visual.name="MeshyVisual";
                    visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.Euler(0,(i*67)%360,0);
                    visual.transform.localScale=Vector3.one*(i%2==0?.48f:.44f);
                    foreach(var collider in visual.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                    foreach(var nested in visual.GetComponentsInChildren<ThanhGiongCollectible>(true))UnityEngine.Object.DestroyImmediate(nested);
                    var bounds=new Bounds(position,Vector3.zero);
                    foreach(var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))bounds.Encapsulate(renderer.bounds);
                    visual.transform.position+=Vector3.up*(position.y-bounds.min.y);
                    var item=grove.GetComponent<ThanhGiongCollectible>();if(item==null)item=grove.AddComponent<ThanhGiongCollectible>();item.kind=ThanhGiongCollectible.Kind.Bamboo;
                    item.displayName="Bụi Tre Ngà Tiếp Chiến";item.foodValue=0f;item.spinSpeed=0f;item.visualRoot=visual.transform;
                    var trigger=grove.GetComponent<SphereCollider>();if(trigger==null)trigger=grove.AddComponent<SphereCollider>();trigger.isTrigger=true;trigger.radius=2.8f;trigger.center=Vector3.up*1.25f;
                    placed.Add(position);
                    report.Add((existing==null?"ADDED ":"UPDATED ")+entry.Key+" "+name+" position="+position.ToString("F2")+" enemyDistance="+enemies.Min(e=>Vector3.Distance(e.transform.position,position)).ToString("F1"));
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Capture(root.transform,folder+"/"+entry.Key+".png");
                report.Add("PASS "+entry.Key+" enemies="+enemies.Length+" addedGroves="+root.transform.childCount);
            }
            File.WriteAllLines(folder+"/installation.txt",report);AssetDatabase.SaveAssets();Debug.Log("COMBAT_BAMBOO PASS maps=3 groves=18");
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }

    static Vector3 FindPlacement(string map,Vector2 requested,List<Vector3> placed,ThanhGiongEnemy[] enemies)
    {
        for(int attempt=0;attempt<33;attempt++)
        {
            float radius=attempt==0?0f:2f+2f*((attempt-1)/8);float angle=(attempt-1)*Mathf.PI/4f;
            Vector3 p=new Vector3(requested.x+Mathf.Cos(angle)*radius,0,requested.y+Mathf.Sin(angle)*radius);
            if(map=="ThungLungVuotSong" && p.z>-36f && p.z<-24f)continue; // keep clear of river and bridge crossing
            if(placed.Any(x=>Vector3.Distance(x,p)<4.5f)||enemies.Any(e=>Vector3.ProjectOnPlane(e.transform.position-p,Vector3.up).magnitude<2.5f))continue;
            var ground=Physics.RaycastAll(p+Vector3.up*30f,Vector3.down,60f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.8f && (h.collider.name=="DatLang"||h.collider is TerrainCollider)).OrderBy(h=>h.distance).ToArray();
            if(ground.Length==0)continue;p.y=ground[0].point.y;
            bool blocked=Physics.OverlapSphere(p+Vector3.up*1.4f,1.6f,~0,QueryTriggerInteraction.Ignore)
                .Any(c=>c.name!="DatLang" && !(c is TerrainCollider) && c.bounds.size.y>1f);
            if(!blocked)return p;
        }
        throw new InvalidOperationException("No clear ground for bamboo at "+map+" "+requested);
    }

    static void Capture(Transform root,string path)
    {
        var center=Vector3.zero;foreach(Transform child in root)center+=child.position;center/=root.childCount;
        var go=new GameObject("Combat bamboo placement preview");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.transform.position=center+new Vector3(23,35,-28);camera.transform.LookAt(center+Vector3.up);
        camera.orthographic=true;camera.orthographicSize=23f;
        var target=RenderTexture.GetTemporary(1400,1000,24);var previous=RenderTexture.active;Texture2D image=null;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image=new Texture2D(1400,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);if(image!=null)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
