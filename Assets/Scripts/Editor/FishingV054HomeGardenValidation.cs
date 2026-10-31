using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV054HomeGardenValidation
    {
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("[V054-HOME-GARDEN] "+text);Debug.Log("[V054-HOME-GARDEN] CHECK: "+text);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/V054/Visual");Directory.CreateDirectory(evidence);
            var home=c.Home;var snapshot=c.Career.Snapshot();
            try
            {
                c.ReturnToHarbor();yield return null;
                var rb=c.Player.engine.RB;rb.position=home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
                for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Real home berth remains accessible");yield return null;
                var fences=home.GetComponentsInChildren<BoxCollider>().Where(x=>x.name=="Garden fence blocker").ToArray();
                Check(fences.Length==5 && fences.All(x=>x.enabled && !x.isTrigger),"All five fence runs have continuous solid collision");
                Check(c.Shore.Relocate(home.Origin+new Vector3(20,0,-10.5f),Quaternion.identity),"Exterior fence approach supports the real player");
                float start=c.Shore.Position.z;
                for(int i=0;i<40;i++)c.Shore.RoomStep(Vector3.forward*.1f);
                Check(c.Shore.Position.z>start+.1f && c.Shore.Position.z<home.Origin.z-9.5f,"Actual CharacterController.Move stops at the southern pickets");
                foreach(float z in new[]{-6f,4.8f})
                {
                    Check(c.Shore.Relocate(home.Origin+new Vector3(10.5f,0,z),Quaternion.identity),"West garden entrance has an unobstructed approach at "+z);
                    for(int i=0;i<16;i++)c.Shore.RoomStep(Vector3.right*.1f);
                    Check(c.Shore.Position.x>home.Origin.x+11.7f,"Actual CharacterController.Move passes through the authored garden gate at "+z);
                }
                var shell=home.House.Find("CottageShell").GetComponentInChildren<MeshRenderer>();
                Check(shell.sharedMaterials.Length==2 && shell.sharedMaterials[1]==Resources.Load<Material>("FishingV035/HomeLimewash")
                    && shell.sharedMaterials[1].GetTexture("_BaseMap")==null,"Cottage walls use matte limewash while structural wood keeps its atlas");
                foreach(var p in new[]{home.Entry,home.Origin+new Vector3(0,.2f,-3.5f),home.Origin+new Vector3(0,.2f,0)})
                    Check(c.Shore.Relocate(p,Quaternion.identity),"Existing cottage door and central aisle retain clearance "+p);
                var sprout=Resources.Load<GameObject>("FishingV035/Turnip0").GetComponentInChildren<MeshFilter>();
                float sproutHeight=sprout.sharedMesh.bounds.size.y;
                Check(sproutHeight>.05f && sproutHeight<.3f && sprout.sharedMesh.triangles.Length/3==784,"First crop stage is four compact cotyledon seedlings within the 784 triangle budget; height="+sproutHeight);
                Check(c.Farm.Root.Find("萝卜种子袋").GetComponent<MeshFilter>().sharedMesh.triangles.Length/3<400,"Seed station displays three labeled folded paper packets in one small mesh");
                var data=c.Career.Snapshot();for(int i=0;i<FishingFarm.PlotCount;i++){data.farm.plots[i].tilled=true;data.farm.plots[i].planted=true;data.farm.plots[i].growth=0;data.farm.plots[i].wateredDay=-1;}
                c.RestoreShopTestProfile(data);c.Farm.Refresh();
                Capture(c,evidence,"garden-seedling",home.Origin+new Vector3(12.5f,1.5f,-6.4f),home.Origin+new Vector3(14,.15f,-5));
                Capture(c,evidence,"seed-station",home.Origin+new Vector3(10.15f,1.6f,-5.3f),home.Origin+new Vector3(10.1f,1.03f,-3.8f));
                Capture(c,evidence,"cottage-exterior",home.Origin+new Vector3(9,3,-13),home.Origin+new Vector3(0,2.6f,-3));
                Capture(c,evidence,"cottage-interior",home.Origin+new Vector3(0,1.9f,-3.6f),home.Origin+new Vector3(0,2.1f,4));
                yield return null;
            }
            finally{c.ReturnToHarbor();c.RestoreShopTestProfile(snapshot);}
        }
        private static void Capture(FishingPrototype c,string directory,string label,Vector3 eye,Vector3 look)
        {
            var camera=c.viewCamera;var p=camera.transform.position;var q=camera.transform.rotation;float fov=camera.fieldOfView;
            try{camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye));camera.fieldOfView=65;FishingV016Validation.Capture(c,directory,label);}
            finally{camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=fov;}
        }
    }
}
