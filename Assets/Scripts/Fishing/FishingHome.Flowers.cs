using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHome
    {
        private void DecoratePorchFlowers()
        {
            var root=new GameObject("V036 curated porch and path flowers").transform;root.SetParent(transform,false);root.position=Origin;
            foreach(var p in new[]{new Vector3(-4.5f,.2f,-7.4f),new Vector3(4.5f,.2f,-7.4f),new Vector3(-6.65f,.2f,-5.3f),new Vector3(6.65f,.2f,-5.3f)})
            {
                var pot=FishingV019Art.Spawn("pot",root,p,.48f,0,"FishingV019");pot.name="Porch violet planter";
                float rim=LeisureBounds(pot).max.y-Origin.y;
                FishingV019Art.Spawn("flower_purpleA",root,new Vector3(p.x,rim-.06f,p.z),.58f,17,"FishingV019");
            }
            foreach(float side in new[]{-1f,1f})for(int j=0;j<2;j++)
            {
                var point=Origin+new Vector3(side*3.2f,0,-9.4f-j*2.4f);
                if(!_c.Shore.Ground(point,out var ground) || ground.y<.5f)continue;
                var flowers=FishingFarmWorld.Model("FlowerPatch",root,ground-Origin,false);flowers.localScale=Vector3.one*.85f;flowers.localRotation=Quaternion.Euler(0,side*27+j*43,0);
            }
            StaticBatchingUtility.Combine(root.gameObject);
        }
    }
}
