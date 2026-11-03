using System;
using System.Collections;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV063RealmValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()=>FishingV011Validation.Begin("V063-REALMS");
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException(label);Debug.Log("[V063-REALMS] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Realm fixtures only use an isolated automated profile");
            DemoEntranceSpace(c);
            c.Career.Lore.SaveRealmStage(2,1);
            for(int obstructed=0;obstructed<2;obstructed++)
            {
                var enter=Enter(c,2);while(enter.MoveNext())yield return enter.Current;
                var realm=c.Realms;realm.HazardsEnabled=false;
                Check(realm.Trial.Ready,"Completed wreck checkpoint reconstructs the actual terminal route");
                var saved=(Vector3)typeof(FishingRealmWorld).GetField("_returnFeet",Private).GetValue(realm);
                var blocker=new GameObject("V063 isolated return obstruction",typeof(BoxCollider));
                blocker.transform.position=saved+Vector3.up;
                blocker.GetComponent<BoxCollider>().size=obstructed==0?new Vector3(.6f,2,.6f):new Vector3(8,4,8);
                try
                {
                    Physics.SyncTransforms();
                    Check(!c.Shore.Relocate(saved,Quaternion.identity),"Before-fix exact return destination really rejects the occupied capsule");
                    Check(realm.TryEntranceFooting(2,out _,out _)==(obstructed==0),"Entrance search finds nearby support and rejects a wholly blocked apron");
                    var exit=realm.ExitPortal(2);bool positioned=false;
                    foreach(var direction in new[]{exit.forward,-exit.forward,exit.right,-exit.right})
                        if(c.Shore.Relocate(exit.position+direction*1.4f,Quaternion.identity)
                            && realm.TryTarget(out int target,out _) && target==4){positioned=true;break;}
                    Check(positioned,"Return fixture reaches the actual final portal interaction");
                    yield return null;yield return null;
                    int coins=c.Career.Coins,stage=c.Career.Lore.RealmStage(2);
                    var before=c.Shore.Position;var trial=realm.Trial;
                    Check(realm.Exit(),"Actual return transition begins despite an occupied original destination");
                    while(realm.Transitioning)yield return null;
                    Check(realm.Inside==(obstructed==1),"Safe fallback returns; a fully blocked destination keeps the player in the realm");
                    if(obstructed==0)
                        Check(c.Shore.Clearance(c.Shore.Position) && FishingPrototype.FlatDistance(c.Shore.Position,saved)<=1.3f,"Fallback stands on clear ground near the same entrance");
                    else Check(realm.Trial==trial && Vector3.Distance(before,c.Shore.Position)<.2f,"Failed return preserves the active trial and current feet");
                    Check(c.Career.Coins==coins && c.Career.Lore.RealmStage(2)==stage,"Return fallback never rewards or resets realm progress");
                }
                finally{UnityEngine.Object.DestroyImmediate(blocker);realm.Abort();}
            }
            var stars=Enter(c,3);while(stars.MoveNext())yield return stars.Current;
            var r=c.Realms;
            Check(c.Shore.Relocate(r.Landmark("Route16").position,Quaternion.identity),"Star fixture uses the authored telescope approach");
            yield return null;yield return null;
            Check(r.TryTarget(out int telescope,out _) && telescope==0,"Fixed telescope is selected through real distance and line-of-sight checks");
            var handAim=typeof(FishingRealmWorld).GetField("_handAim",Private);handAim.SetValue(r,true);
            Check(c.RequestWorldInteraction() && r.ObservingStars,"Shared world action opens fixed telescope controls");
            Check(!(bool)handAim.GetValue(r),"Reopening telescope never reuses a previous held-hand aim sample");
            for(int stage=r.Trial.StarStage;stage<3;stage++)
            {
                r.AimTelescope(new Vector2(r.Trial.TargetYaw(stage)-r.Trial.StarYaw,r.Trial.TargetPitch(stage)-r.Trial.StarPitch),false,.1f);
                for(int frame=0;frame<25 && r.Trial.StarStage==stage;frame++)r.AimTelescope(Vector2.zero,true,.1f);
                Check(r.Trial.StarStage==stage+1,"Aligned continuous focus records constellation "+stage);
            }
            Check(r.Trial.Ready,"All three actual constellation stages become complete");
            handAim.SetValue(r,true);r.CancelObservation();
            Check(!r.ObservingStars && !(bool)handAim.GetValue(r) && r.Trial.StarFocus==0,"Closing telescope releases both focus and physical hand-aim history");
            r.Abort();c.ReturnToHarbor();
        }
        private static void DemoEntranceSpace(FishingPrototype c)
        {
            var before=c.Shore.Position;
            for(int island=0;island<4;island++)
            {
                Check(c.Realms.TryDemoEntranceFooting(island,out var feet,out var rotation),"Classroom entrance has supported hand and notebook space "+island);
                Check(c.Shore.Ground(feet,out var ground) && Mathf.Abs(ground.y-feet.y)<.01f && c.Shore.Clearance(feet),"Classroom entrance preserves real floor and standing clearance "+island);
                var center=feet+Vector3.up*1.45f+rotation*Vector3.forward*.65f;
                foreach(var collider in Physics.OverlapBox(center,new Vector3(.6f,.65f,.65f),rotation,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore))
                    Check(collider==c.Shore.Body || FishingShore.NonSolidSurface(collider),"Classroom book/controller volume excludes solid scenery "+island+" / "+collider.name);
                Check(c.Shore.ClearSight(feet+Vector3.up*1.4f,c.Realms.Portal(island).position+Vector3.up*1.2f,c.Realms.Portal(island)),"Classroom entrance retains the real portal interaction sight line "+island);
                Debug.Log("[V063-REALMS] Demo entrance "+island+" feet="+feet+" yaw="+rotation.eulerAngles.y);
            }
            var blocker=new GameObject("V063 classroom book-only overhead obstruction",typeof(BoxCollider));
            blocker.transform.position=c.Realms.PortalApproach(2)+Vector3.up*1.88f;
            var box=blocker.GetComponent<BoxCollider>();box.size=new Vector3(12,.16f,12);
            try
            {
                Physics.SyncTransforms();
                Check(c.Realms.TryEntranceFooting(2,out _,out _),"Ordinary return footing remains legal beneath a book-only obstruction");
                Check(!c.Realms.TryDemoEntranceFooting(2,out _,out _) && c.Shore.Position==before,"No notebook space rejects classroom travel without moving the player");
                box.isTrigger=true;Physics.SyncTransforms();
                Check(c.Realms.TryDemoEntranceFooting(2,out _,out _),"Non-solid triggers do not block classroom notebook space");
                box.isTrigger=false;blocker.layer=5;Physics.SyncTransforms();
                Check(c.Realms.TryDemoEntranceFooting(2,out _,out _),"UI geometry does not block classroom notebook space");
                blocker.layer=4;Physics.SyncTransforms();
                Check(c.Realms.TryDemoEntranceFooting(2,out _,out _),"Water-layer geometry does not block classroom notebook space");
            }
            finally{UnityEngine.Object.DestroyImmediate(blocker);Physics.SyncTransforms();}
        }
        private static IEnumerator Enter(FishingPrototype c,int island)
        {
            c.SelectFieldSlot(8);c.ReturnToHarbor();yield return null;
            var point=FishingIslandWorld.Berth(island)+Vector3.up;var rb=c.Player.engine.RB;
            rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=point;rb.rotation=Quaternion.identity;
            c.Player.transform.SetPositionAndRotation(point,Quaternion.identity);Physics.SyncTransforms();
            for(int frame=0;frame<30;frame++)yield return null;
            Check(c.Shore.TryDisembark(),"Production island landing "+island);yield return null;
            Check(c.Realms.TryEntranceFooting(island,out var feet,out var rotation)
                && c.Shore.Relocate(feet,rotation) && c.Realms.Enter(island),"Safe entrance footing enters the actual realm "+island);
            while(c.Realms.Transitioning)yield return null;
            yield return null;yield return null;
            Check(c.Realms.Island==island,"Realm transition finishes "+island);
        }
    }
}
