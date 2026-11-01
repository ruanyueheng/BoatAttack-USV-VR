using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHome
    {
        private void DressGarden()
        {
            var root=new GameObject("V035 Home flowers and orchard").transform;root.SetParent(transform,false);root.position=Origin;
            // Thin decorative pickets need continuous blockers; leave both authored west-side entrances open.
            foreach(var span in new[]{(new Vector3(13.8f,0,-9.4f),new Vector3(29,0,-9.4f)),
                (new Vector3(29,0,-9.4f),new Vector3(29,0,10)),(new Vector3(29,0,10),new Vector3(11.2f,0,10)),
                (new Vector3(11.2f,0,10),new Vector3(11.2f,0,7.6f)),(new Vector3(11.2f,0,-1.8f),new Vector3(11.2f,0,2))})
            {
                var fence=new GameObject("Garden fence blocker").transform;fence.SetParent(root,false);
                fence.localPosition=(span.Item1+span.Item2)*.5f+Vector3.up*.55f;
                fence.localRotation=Quaternion.LookRotation(span.Item2-span.Item1);
                fence.gameObject.AddComponent<BoxCollider>().size=new Vector3(.16f,1.1f,Vector3.Distance(span.Item1,span.Item2));
            }
            Physics.SyncTransforms();
            var land=transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();
            Vector3 OnLand(Vector3 local)
            {
                if(!land.Raycast(new Ray(root.TransformPoint(local)+Vector3.up*80,Vector3.down),out var hit,160))
                    throw new System.InvalidOperationException("Home planting has no terrain support: "+local);
                return root.InverseTransformPoint(hit.point);
            }
            int treeId=0;
            foreach(var p in new[]{new Vector3(-11,0,10),new Vector3(-7,0,17),new Vector3(7,0,19),new Vector3(32,0,12),new Vector3(30,0,19)})
                RegisterOrchard(treeId++,FishingFarmWorld.Model("OrchardTree",root,OnLand(p),true));
            for(int j=0;j<140;j++)
            {
                float a=j*2.399963f;float r=10+Mathf.Sqrt((j+.5f)/140)*17;var p=new Vector3(4+Mathf.Cos(a)*r,0,2+Mathf.Sin(a)*r*.75f);
                if((p.x>7 && p.x<33 && p.z>-13 && p.z<20) || (Mathf.Abs(p.x)<8 && p.z<8) || (p.x< -10 && p.x> -23 && p.z<12 && p.z>-10)
                    || (p.x> -9.4f && p.x<10.4f && p.z>6.5f && p.z<11.5f))continue;
                p=OnLand(p);if(root.TransformPoint(p).y<.8f)continue;
                var t=FishingFarmWorld.Model(j%4==0?"FlowerPatch":"MeadowPatch",root,p,false);t.localScale=Vector3.one*(j%4==0?1.5f:1.8f);t.localRotation=Quaternion.Euler(0,j*47,0);
            }
            foreach(float side in new[]{-1f,1f})
            {
                for(int j=0;j<8;j++)
                {
                    var flowers=FishingFarmWorld.Model("FlowerPatch",root,OnLand(new Vector3(side*(7.4f+j*.15f),0,-6+j*1.7f)),false);flowers.localScale=Vector3.one*1.2f;
                }
                var ivy=FishingFarmWorld.Model("IvyDrape",House,new Vector3(side*5.9f,3.1f,-5.3f),false);ivy.localScale=Vector3.one*.85f;
            }
        }
        private void HideProxySurfaces()
        {
            foreach(Transform child in transform)
                if(child.name.StartsWith("Home ") || child.name.StartsWith("Boardwalk "))
                    foreach(var r in child.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
            foreach(Transform child in House)
                if(child.name=="Home foundation" || child.name=="Porch foundation")
                    foreach(var r in child.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
        }
    }
}
