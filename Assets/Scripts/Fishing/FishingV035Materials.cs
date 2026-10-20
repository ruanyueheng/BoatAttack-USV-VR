using UnityEngine;

namespace BoatAttack.Fishing
{
    public static class FishingV035Materials
    {
        public static void Apply(Transform root,bool allFoliage=false)
        {
            var hard=Resources.Load<Material>("FishingV035/Coastal");var leaves=Resources.Load<Material>("FishingV035/Foliage");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var materials=new Material[mesh.subMeshCount];
                for(int i=0;i<materials.Length;i++)materials[i]=allFoliage || i==1?leaves:hard;
                renderer.sharedMaterials=materials;
            }
        }
    }
}
