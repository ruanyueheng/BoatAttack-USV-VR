using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    public static class FishingV035Materials
    {
        public static void Apply(Transform root,bool allFoliage=false,bool lod=true)
        {
            var hard=Resources.Load<Material>("FishingV035/Coastal");var leaves=Resources.Load<Material>("FishingV035/Foliage");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.name=="LOD1")continue;
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var materials=new Material[mesh.subMeshCount];var own=Override(mesh);
                for(int i=0;i<materials.Length;i++)materials[i]=own!=null?own:allFoliage || i==1?leaves:hard;
                renderer.sharedMaterials=materials;if(lod)AddLod(renderer);
            }
        }
        /// <summary>V044 AI-remodelled models carry their own painted texture instead of the shared V035 atlas.</summary>
        public static Material Override(Mesh mesh)=>mesh.name.StartsWith("GEO-")?Resources.Load<Material>("FishingV044/Scene/"+mesh.name.Substring(4)):null;
        /// <summary>PICO budget: swaps to Resources/FishingV044/Scene/&lt;Name&gt;_LOD1 (~20% triangles) when small on screen, then culls.
        /// Must run before static batching, i.e. at install time for scene objects and at spawn time for runtime props.</summary>
        public static void AddLod(MeshRenderer renderer)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            if(mesh==null || !mesh.name.StartsWith("GEO-"))return;
            var model=Resources.Load<GameObject>("FishingV044/Scene/"+mesh.name.Substring(4)+"_LOD1");if(model==null)return;
            var source=model.GetComponentInChildren<MeshFilter>();if(source==null || source.sharedMesh==null)return;
            var low=source.sharedMesh;var existing=renderer.transform.Find("LOD1");
            var go=existing!=null?existing.gameObject:new GameObject("LOD1");go.layer=renderer.gameObject.layer;go.isStatic=renderer.gameObject.isStatic;go.transform.SetParent(renderer.transform,false);
            // Loading a Mesh alone drops its FBX node's axis rotation, which laid all distant scenery on its side.
            go.transform.localPosition=source.transform.position;go.transform.localRotation=source.transform.rotation;go.transform.localScale=source.transform.lossyScale;
            if(existing!=null){renderer.GetComponentInParent<LODGroup>()?.RecalculateBounds();return;}
            go.AddComponent<MeshFilter>().sharedMesh=low;var lod=go.AddComponent<MeshRenderer>();lod.sharedMaterials=renderer.sharedMaterials;
            lod.shadowCastingMode=ShadowCastingMode.Off;lod.receiveShadows=renderer.receiveShadows;
            var group=renderer.GetComponentInParent<LODGroup>();if(group==null)group=renderer.gameObject.AddComponent<LODGroup>();
            // Ground cover swaps early and vanishes sooner than trees and rocks.
            bool small=mesh.bounds.size.y<1;
            group.SetLODs(new[]{new LOD(small?.12f:.2f,new Renderer[]{renderer}),new LOD(small?.02f:.008f,new Renderer[]{lod})});group.RecalculateBounds();
        }
    }
}
