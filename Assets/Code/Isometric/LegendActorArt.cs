using System.Collections.Generic;
using UnityEngine;

// Match the mounted skinned model to the painted scenery without changing its rig or textures.
[DefaultExecutionOrder(-150)]
public sealed class LegendActorArt : MonoBehaviour
{
    readonly List<Material> generated=new();
    void Awake()
    {
        Shader shader=Shader.Find("ThanhGiong/LegendSurface");if(!shader)return;
        var movement=GetComponent<MountedHorseController>();Transform visual=movement&&movement.visual?movement.visual:transform.Find("Visual");if(!visual)return;
        foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true)) {
            if(renderer is ParticleSystemRenderer||renderer is LineRenderer||renderer.GetComponent<TextMesh>())continue;
            if(IsMeshyRenderer(renderer))continue;
            var source=renderer.sharedMaterials;var replacement=new Material[source.Length];
            for(int i=0;i<source.Length;i++){
                var old=source[i];if(!old)continue;
                var material=new Material(shader){name="Painted Giong "+old.name};Color color=Color.white;
                foreach(string key in new[]{"baseColorFactor","_BaseColor","_Color"})if(old.HasProperty(key)){color=old.GetColor(key);break;}
                material.SetColor("_BaseColor",color);material.SetFloat("_Mode",0);
                foreach(string key in new[]{"baseColorTexture","_BaseMap","_MainTex"})if(old.HasProperty(key)&&old.GetTexture(key)){material.SetTexture("_BaseMap",old.GetTexture(key));material.SetTextureScale("_BaseMap",old.GetTextureScale(key));material.SetTextureOffset("_BaseMap",old.GetTextureOffset(key));break;}
                Mesh mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
                material.SetFloat("_UseVertexColors",mesh&&mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)?1:0);generated.Add(material);replacement[i]=material;
            }
            renderer.sharedMaterials=replacement;
        }
    }
    static bool IsMeshyRenderer(Renderer r)
    {
        if (r == null) return false;
        if (r.sharedMaterial != null && r.sharedMaterial.name.IndexOf("meshy", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        for (Transform t = r.transform; t != null; t = t.parent)
            if (t.name.IndexOf("meshy", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
    void OnDestroy(){foreach(var material in generated)if(material)Destroy(material);}
}
