using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    // Calibrated against the normalized Meshy mesh, not a symmetric placeholder horse.
    public static class HorseSkinRepair
    {
        static readonly string[] Prefix = { "fl", "fr", "rl", "rr" };
        static readonly Vector3[] Hip = { new Vector3(-.16f,1.02f,.66f), new Vector3(.16f,1.02f,.68f), new Vector3(-.21f,1.03f,-.43f), new Vector3(.20f,1.03f,-.56f) };
        static readonly Vector3[] Knee = { new Vector3(-.16f,.49f,.79f), new Vector3(.16f,.49f,.76f), new Vector3(-.21f,.48f,-.39f), new Vector3(.20f,.48f,-.61f) };
        static readonly Vector3[] Foot = { new Vector3(-.170f,.222f,.631f), new Vector3(.157f,.03f,.798f), new Vector3(-.217f,.102f,-.230f), new Vector3(.187f,.077f,-.638f) };

        static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(low,high,value));
        static Vector3 Center(int leg, float y) => y > Knee[leg].y
            ? Vector3.Lerp(Knee[leg], Hip[leg], Mathf.InverseLerp(Knee[leg].y,Hip[leg].y,y))
            : Vector3.Lerp(Foot[leg], Knee[leg], Mathf.InverseLerp(Foot[leg].y,Knee[leg].y,y));

        public static void Repair(SkinnedMeshRenderer skin, bool rebuildWeights = true)
        {
            if (skin == null || skin.sharedMesh == null || skin.bones.Length != 17) return;
            var bones = skin.bones;
            Transform root = skin.transform.parent;
            // Only rotations drive gait. Joint translations/scales stay at the bind pose.
            for (int leg = 0; leg < 4; leg++)
            {
                string prefix = Prefix[leg];
                Transform upper = bones.First(x=>x.name==prefix+"_upper");
                Transform lower = bones.First(x=>x.name==prefix+"_lower");
                Transform hoof = bones.First(x=>x.name==prefix+"_hoof");
                upper.localRotation = lower.localRotation = hoof.localRotation = Quaternion.identity;
                upper.localScale = lower.localScale = hoof.localScale = Vector3.one;
                upper.position = root.TransformPoint(Hip[leg]); lower.position = root.TransformPoint(Knee[leg]); hoof.position = root.TransformPoint(Foot[leg]);
            }
            Mesh mesh = skin.sharedMesh;
            if(!rebuildWeights)
            {
                mesh.bindposes=bones.Select(x=>x.worldToLocalMatrix*skin.transform.localToWorldMatrix).ToArray();
                EditorUtility.SetDirty(skin);return;
            }
            var vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            int body = Array.FindIndex(bones,x=>x.name=="body");
            for (int i=0;i<vertices.Length;i++)
            {
                Vector3 v=vertices[i]; int best=0; float distance=float.MaxValue;
                for(int leg=0;leg<4;leg++)
                {
                    Vector3 center=Center(leg,v.y);
                    float d=new Vector2(v.x-center.x,v.z-center.z).magnitude;
                    if(d<distance){distance=d;best=leg;}
                }
                // Saddle cloth, chest armor, belly straps and tail stay out of leg influence.
                float legWeight=(1f-Smooth(.88f,1.08f,v.y))*(1f-Smooth(.14f,.23f,distance));
                if(legWeight>.001f && v.y<1.08f && Mathf.Abs(v.x)>.065f)
                {
                    int u=Array.FindIndex(bones,x=>x.name==Prefix[best]+"_upper");
                    float upper=Smooth(Knee[best].y-.045f,Knee[best].y+.12f,v.y);
                    float hoof=(1f-Smooth(Foot[best].y+.045f,Foot[best].y+.14f,v.y))*(1f-upper);
                    weights[i]=new BoneWeight{boneIndex0=body,weight0=1f-legWeight,boneIndex1=u,weight1=upper*legWeight,boneIndex2=u+1,weight2=(1f-upper-hoof)*legWeight,boneIndex3=u+2,weight3=hoof*legWeight};
                }
                else if(v.z>.42f && v.y>1.2f)
                {
                    float n=Smooth(.4f,.72f,v.z)*Smooth(1.18f,1.4f,v.y), h=Smooth(1.48f,1.73f,v.y);
                    weights[i]=new BoneWeight{boneIndex0=body,weight0=1f-n,boneIndex1=1,weight1=n*(1f-h),boneIndex2=2,weight2=n*h};
                }
                else if(v.z<-.9f && v.y>.3f)
                {
                    float t=Smooth(.88f,1.1f,-v.z);
                    weights[i]=new BoneWeight{boneIndex0=body,weight0=1f-t,boneIndex1=3,weight1=t*.65f,boneIndex2=4,weight2=t*.35f};
                }
                else weights[i]=new BoneWeight{boneIndex0=body,weight0=1f};
            }
            mesh.boneWeights=SmoothSurfaceWeights(vertices,mesh.triangles,weights);
            mesh.bindposes=bones.Select(x=>x.worldToLocalMatrix*skin.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); EditorUtility.SetDirty(skin);
        }

        static BoneWeight[] SmoothSurfaceWeights(Vector3[] vertices,int[] triangles,BoneWeight[] initial)
        {
            // FBX duplicates vertices at UV/normal seams. Weld their positions for
            // weight diffusion so an armor plate cannot tear along an import seam.
            var lookup=new System.Collections.Generic.Dictionary<Vector3Int,int>();
            var map=new int[vertices.Length]; var positions=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<vertices.Length;i++)
            {
                var key=Vector3Int.RoundToInt(vertices[i]*10000f); int node;
                if(!lookup.TryGetValue(key,out node)){node=positions.Count;lookup.Add(key,node);positions.Add(vertices[i]);}
                map[i]=node;
            }
            int count=positions.Count; var neighbors=new System.Collections.Generic.HashSet<int>[count];
            var current=new float[count,17]; var next=new float[count,17];var samples=new int[count];
            for(int i=0;i<count;i++)neighbors[i]=new System.Collections.Generic.HashSet<int>();
            for(int i=0;i<vertices.Length;i++)
            {
                int n=map[i];samples[n]++;var w=initial[i];
                current[n,w.boneIndex0]+=w.weight0;current[n,w.boneIndex1]+=w.weight1;current[n,w.boneIndex2]+=w.weight2;current[n,w.boneIndex3]+=w.weight3;
            }
            for(int n=0;n<count;n++)for(int b=0;b<17;b++)current[n,b]/=samples[n];
            for(int i=0;i<triangles.Length;i+=3)for(int e=0;e<3;e++)
            {int a=map[triangles[i+e]],b=map[triangles[i+(e+1)%3]];if(a!=b){neighbors[a].Add(b);neighbors[b].Add(a);}}
            for(int iteration=0;iteration<192;iteration++)
            {
                for(int n=0;n<count;n++)for(int b=0;b<17;b++)
                {
                    float sum=0f;foreach(int neighbor in neighbors[n])sum+=current[neighbor,b];
                    next[n,b]=positions[n].y<1.15f && neighbors[n].Count>0 ? Mathf.Lerp(current[n,b],sum/neighbors[n].Count,.65f) : current[n,b];
                }
                var swap=current;current=next;next=swap;
            }
            var output=new BoneWeight[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                int n=map[i];int[] indices={0,0,0,0};float[] values={0,0,0,0};
                for(int b=0;b<17;b++)for(int slot=0;slot<4;slot++)if(current[n,b]>values[slot])
                {for(int k=3;k>slot;k--){values[k]=values[k-1];indices[k]=indices[k-1];}values[slot]=current[n,b];indices[slot]=b;break;}
                float total=values.Sum();
                output[i]=new BoneWeight{boneIndex0=indices[0],weight0=values[0]/total,boneIndex1=indices[1],weight1=values[1]/total,boneIndex2=indices[2],weight2=values[2]/total,boneIndex3=indices[3],weight3=values[3]/total};
            }
            return output;
        }

        [MenuItem("Tools/Thánh Gióng/Meshy/Repair Horse Skin In Campaign Maps")]
        public static void RepairAll()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before repairing skin.");
            EditorSceneManager.SaveOpenScenes(); var setup=EditorSceneManager.GetSceneManagerSetup();
            string backup="HorseRepairVerification/SceneBackups/"+DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup);
            int count=0;
            var repairedMeshes=new System.Collections.Generic.HashSet<Mesh>();
            try
            {
                foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Meshy","Assets/Prefabs/Player"}).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var prefab=PrefabUtility.LoadPrefabContents(path); bool changed=false;
                    try
                    {
                        foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            if(skin.name=="Meshy Horse Skin" && skin.bones.Length==17){Repair(skin,repairedMeshes.Add(skin.sharedMesh));changed=true;}
                        if(changed){File.Copy(path,Path.Combine(backup,Path.GetFileName(path)),true);PrefabUtility.SaveAsPrefabAsset(prefab,path);}
                    }
                    finally{PrefabUtility.UnloadPrefabContents(prefab);}
                }
                foreach(string path in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes/ThanhGiongWorld"}).Select(AssetDatabase.GUIDToAssetPath))
                {
                    File.Copy(path,Path.Combine(backup,Path.GetFileName(path)),true);
                    var scene=EditorSceneManager.OpenScene(path);
                    foreach(var skin in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                        if(skin.name=="Meshy Horse Skin" && skin.bones.Length==17){Repair(skin,repairedMeshes.Add(skin.sharedMesh));count++;}
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets(); Debug.Log("HORSE_SKIN_REPAIR PASS sceneRigs="+count+" backup="+backup);
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
    }
}
