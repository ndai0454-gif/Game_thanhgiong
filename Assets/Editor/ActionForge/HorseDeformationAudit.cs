using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    public static class HorseDeformationAudit
    {
        public static string Run(string label)
        {
            var driver=UnityEngine.Object.FindAnyObjectByType<MeshyHeroVisualDriver>();
            if(driver==null || EditorApplication.isPlaying) throw new InvalidOperationException("Open a campaign scene in Edit Mode.");
            var host=new GameObject("Horse skin audit");
            var clone=UnityEngine.Object.Instantiate(driver.meshyRoot.gameObject,host.transform);
            clone.transform.localPosition=Vector3.zero; clone.transform.localRotation=Quaternion.identity; clone.transform.localScale=Vector3.one;
            var movement=host.AddComponent<MountedHorseController>();
            var motion=host.AddComponent<GodotMountedMotion>();
            var baked=new Mesh();
            string folder="HorseRepairVerification/"+label; Directory.CreateDirectory(folder);
            try
            {
                motion.Configure(clone.transform);
                var skin=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name=="Meshy Horse Skin");
                var rest=skin.sharedMesh.vertices; var triangles=skin.sharedMesh.triangles;
                var ratios=new List<float>(); int edges=0, stretched=0; float maximum=1f;
                float[] speeds={4.8f,8.5f};
                foreach(float speed in speeds)
                {
                    for(int frame=0;frame<120;frame++)
                    {
                        motion.Drive(1f/60f,speed,speed/8.5f,1f,true,0,0,0,-1,false);
                        if(frame%10!=0)continue;
                        skin.BakeMesh(baked); var animated=baked.vertices;
                        for(int i=0;i<triangles.Length;i+=3)
                        for(int e=0;e<3;e++)
                        {
                            int a=triangles[i+e],b=triangles[i+(e+1)%3];
                            if(Mathf.Max(rest[a].y,rest[b].y)>.9f)continue;
                            float length=Vector3.Distance(rest[a],rest[b]); if(length<.008f)continue;
                            float ratio=Vector3.Distance(animated[a],animated[b])/length;
                            ratios.Add(ratio); edges++; if(ratio>1.5f)stretched++; maximum=Mathf.Max(maximum,ratio);
                        }
                        if(frame%30==0)Capture(clone,folder+"/"+(speed<5?"walk":"gallop")+"-"+frame+".png");
                    }
                }
                ratios.Sort();
                string report="sampledLegEdges="+edges+" p99="+ratios[(int)(ratios.Count*.99f)].ToString("F3")+" max="+maximum.ToString("F3")+" over1.5="+(stretched/(float)edges).ToString("P3");
                File.WriteAllText(folder+"/deformation.txt",report);Debug.Log("HORSE_DEFORMATION "+label+" "+report);return report;
            }
            finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(host);}
        }
        static void Capture(GameObject clone,string path)
        {
            var go=new GameObject("Horse audit camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(3.5f,1.35f,2.5f);camera.transform.LookAt(new Vector3(0,.95f,0));
            camera.orthographic=true;camera.orthographicSize=1.35f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.08f,.075f);camera.cullingMask=1<<31;
            var nodes=clone.GetComponentsInChildren<Transform>(true);var layers=nodes.Select(x=>x.gameObject.layer).ToArray();
            var target=RenderTexture.GetTemporary(900,900,24);var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                foreach(var node in nodes)node.gameObject.layer=31;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image=new Texture2D(900,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<nodes.Length;i++)if(nodes[i]!=null)nodes[i].gameObject.layer=layers[i];
                RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
