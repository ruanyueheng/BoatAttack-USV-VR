using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV019Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V019");
        private static void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);Debug.Log("[V019] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/v019-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            for(int frame=0;frame<30;frame++)yield return null;
            FishingV019WorldBuilder.CaptureThumbnails();
            Check(c.Realms!=null && c.Residents!=null && c.Regions!=null,"World systems initialized with production scene");
            Check(c.NavigationCount==15 && c.Career.Lore.ClearCount==0,"Existing island navigation, home and new empty story progress");
            Check(!c.Residents.Talk(0) && !c.Realms.Enter(0),"Remote dialogue/entry rejected from the boat");
            for(int i=0;i<6;i++)Check(c.Residents.Person(i)!=null && c.Residents.Person(i).GetComponentsInChildren<Renderer>().Length>0,"Real NPC geometry "+i);
            var wait=Wait(.5f);while(wait.MoveNext())yield return null;
            for(int island=0;island<4;island++)
            {
                c.ReturnToHarbor();yield return null;
                PlaceBoat(c,FishingIslandWorld.Berth(island));wait=Wait(.5f);while(wait.MoveNext())yield return null;
                Check(c.Shore.TryDisembark(),"Physical island landing "+island);yield return null;
                var npc=c.Residents.Person(island);var walk=Walk(c,npc.position+Vector3.forward*1.8f);while(walk.MoveNext())yield return null;
                Check(c.RequestWorldInteraction() && c.DialogueOpen && c.Residents.ActiveId==island,"Production interaction selects island resident "+island);
                c.Residents.Choose(1);c.Residents.Reveal();
                Check(c.Career.Lore.Met(island) && c.Residents.Speech.Contains(FishingLore.ShrineName(island)),"Dialogue provides concrete shrine clue "+island);
                var before=c.Shore.Position;c.Shore.Move(Vector2.up,true,true,false,.05f);
                Check(c.Shore.Position==before && !c.RequestWorldInteraction(),"Dialogue owns movement and duplicate E input "+island);
                c.viewCamera.transform.SetPositionAndRotation(npc.position+new Vector3(0,1.45f,2.5f),Quaternion.LookRotation(new Vector3(0,-.3f,-2.5f)));
                FishingV016Validation.Capture(c,evidence,"resident-"+island);
                c.Residents.Close();yield return null;yield return null;
                var portal=c.Realms.Portal(island);walk=Walk(c,portal.position+Vector3.forward*1.8f);while(walk.MoveNext())yield return null;
                Check(c.Realms.TryTarget(out int id,out _) && id==8+island,"Shrine door reachable from actual island motor "+island);
                Check(c.RequestWorldInteraction() && c.Realms.Transitioning,"Production E starts shrine fade "+island);
                before=c.Shore.Position;c.Shore.Move(Vector2.up,true,false,false,.05f);
                Check(c.Shore.Position==before && !c.RequestWorldInteraction(),"Transition blocks movement and double entry "+island);
                while(c.Realms.Transitioning)yield return null;yield return null;yield return null;
                Check(c.Realms.Inside && c.Shore.Ground(c.Shore.Position,out var floor) && c.Shore.Position.y-floor.y<.2f,"Safe supported interior transfer "+island);
                Check(!c.Shore.TryBoard(),"Indoor boarding rejected "+island);
                Check(!c.Realms.Trial.Ready && Physics.Raycast(c.Realms.Room.position+new Vector3(0,1.4f,4),Vector3.forward,out var blocked,5)
                    && blocked.collider.name=="Echo gate","Unsolved gate physically blocks altar "+island);
                var room=c.Realms.Room;
                c.viewCamera.transform.SetPositionAndRotation(room.position+new Vector3(6,3,-10),Quaternion.LookRotation(new Vector3(-6,-1.4f,12)));
                FishingV016Validation.Capture(c,evidence,"shrine-"+island);
                if(island==0)
                {
                    var device=Use(c,0,false);while(device.MoveNext())yield return null;
                    Check(c.Realms.Trial.Progress==0,"Wrong bell resets real room");
                    foreach(int key in new[]{2,0,1}){device=Use(c,key,true);while(device.MoveNext())yield return null;}
                }
                else if(island==1)
                {
                    foreach(int key in new[]{0,2}){var device=Use(c,key,true);while(device.MoveNext())yield return null;}
                    Check(room.Find("Light bridge").gameObject.activeSelf && !room.Find("Crossing barrier").gameObject.activeSelf,"Forest light creates traversable bridge");
                }
                else if(island==2)
                {
                    foreach(int key in new[]{0,0,1}){var device=Use(c,key,true);while(device.MoveNext())yield return null;}
                    Check(c.Realms.Trial.State(0)==2 && Vector3.Distance(c.Realms.Device(0).localPosition,new Vector3(-3,0,0))<.1f,"Cargo visibly moves onto load plate");
                }
                else foreach(int key in new[]{0,1,1,1,2,2}){var device=Use(c,key,true);while(device.MoveNext())yield return null;}
                Check(c.Realms.Trial.Ready && !room.Find("Echo gate").gameObject.activeSelf,"Distinct puzzle opens real gate "+island);
                walk=Walk(c,c.Realms.Device(3).position+Vector3.back*1.4f);while(walk.MoveNext())yield return null;
                int coins=c.Career.Coins;
                Check(c.RequestWorldInteraction() && c.Career.Lore.Cleared(island) && c.Career.Coins==coins+90,"Altar pays first reward through production E "+island);
                Check(c.RequestWorldInteraction() && c.Career.Coins==coins+90,"Repeated altar never duplicates coins "+island);
                walk=Walk(c,c.Realms.Device(4).position+Vector3.forward*1.5f);while(walk.MoveNext())yield return null;
                Check(c.RequestWorldInteraction() && c.Realms.Transitioning,"Return portal reachable "+island);
                while(c.Realms.Transitioning)yield return null;yield return null;yield return null;
                Check(!c.Realms.Inside && Vector3.Distance(c.Shore.Position,portal.position)<2.2f,"Return restores actual island feet "+island);
                c.viewCamera.transform.SetPositionAndRotation(FishingIslandWorld.Center(island)+new Vector3(35,25,42),Quaternion.LookRotation(new Vector3(-35,-23,-42)));
                FishingV016Validation.Capture(c,evidence,"island-"+island);
            }
            c.ReturnToHarbor();yield return null;yield return null;
            PlaceBoat(c,c.TackleShop.mooringPoint.position);wait=Wait(.5f);while(wait.MoveNext())yield return null;
            Check(c.Shore.TryDisembark(),"Return to actual shop porch");yield return null;
            var guide=c.Residents.Person(5);var approach=Walk(c,c.TackleShop.transform.TransformPoint(new Vector3(0,0,-6.6f)));while(approach.MoveNext())yield return null;
            Check(c.Residents.Talk(5),"Harbor guide dialogue reachable");int finale=c.Career.Coins;c.Residents.Choose(1);
            Check(c.Career.Lore.Finale && c.Career.Coins==finale+180,"Four echoes complete narrative with one payout");c.Residents.Choose(1);
            Check(c.Career.Coins==finale+180,"Narrative finale duplicate protection");c.Residents.Close();yield return null;yield return null;
            approach=Walk(c,c.TackleShop.transform.TransformPoint(new Vector3(0,0,-4)));while(approach.MoveNext())yield return null;
            approach=Walk(c,c.TackleShop.purchasePoint.position);while(approach.MoveNext())yield return null;
            Check(c.ToggleShop() && c.Residents.Talk(4) && c.DialogueOpen && !c.ShopOpen,"Shop switches to clerk dialogue without input leak");
            c.Residents.Choose(1);Check(c.Residents.Speech.Contains("金币不足"),"Clerk explains actual goods and purchase rules");c.Residents.Close();yield return null;yield return null;
            Check(c.ToggleShop() && c.TackleShop.BuySupply(12) && c.Career.Backpack.Count(FieldItem.CookedMeat)==3,"Remote voyage ration is a real bought item");c.TackleShop.Close();yield return null;yield return null;
            c.viewCamera.transform.SetPositionAndRotation(c.TackleShop.transform.TransformPoint(new Vector3(0,1.75f,-4.3f)),Quaternion.LookRotation(c.TackleShop.transform.forward));
            FishingV016Validation.Capture(c,evidence,"shop-interior");
            var display=c.TackleShop.transform.Find("V018 stocked chandlery");Check(display.childCount>70 && display.GetComponents<BoxCollider>().Length==2,"Store has distinct 3D goods and retained clear aisle colliders");
            var saved=FishingSaveStore.Load(c.SavePath,out string warning);
            Check(warning=="" && saved.Snapshot().version==12 && saved.Lore.Finale && saved.Lore.ClearCount==4,"Real Unity JSON story save round trip");
            c.ReturnToHarbor();yield return null;yield return null;
            var traversal=Traversal(c,evidence);while(traversal.MoveNext())yield return null;
            c.ReturnToHarbor();yield return null;yield return null;
            var steering=Steering(c);while(steering.MoveNext())yield return null;
            Debug.Log("[V019] EVIDENCE: "+evidence);
        }
        private static IEnumerator Use(FishingPrototype c,int device,bool expected)
        {
            var target=c.Realms.Device(device);var walk=Walk(c,target.position+Vector3.back*1.6f);while(walk.MoveNext())yield return null;
            Check(c.Realms.TryTarget(out int found,out _) && found==device,"Device proximity/line of sight "+device+" in "+c.Realms.Island);
            Check(c.RequestWorldInteraction()==expected,"Physical puzzle command "+device+" in "+c.Realms.Island);
            var wait=Wait(.8f);while(wait.MoveNext())yield return null;
        }
        private static IEnumerator Walk(FishingPrototype c,Vector3 target)
        {
            for(int i=0;i<700 && FishingPrototype.FlatDistance(c.Shore.Position,target)>.14f;i++)
            {
                var delta=Vector3.ProjectOnPlane(target-c.Shore.Position,Vector3.up);
                c.viewCamera.transform.rotation=Quaternion.LookRotation(delta.normalized);
                c.Shore.Move(Vector2.up,false,false,false,Mathf.Min(.05f,delta.magnitude/2.6f));yield return null;
            }
            Check(FishingPrototype.FlatDistance(c.Shore.Position,target)<.4f,"Continuous character path to "+target+" actual="+c.Shore.Position);
        }
        private static IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        private static void PlaceBoat(FishingPrototype c,Vector3 point)
        {var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=point;rb.rotation=Quaternion.identity;c.Player.transform.SetPositionAndRotation(point,Quaternion.identity);Physics.SyncTransforms();}
        private static IEnumerator Traversal(FishingPrototype c,string evidence)
        {
            PlaceBoat(c,FishingIslandWorld.Berth(0));var wait=Wait(.5f);while(wait.MoveNext())yield return null;
            Check(c.Shore.TryDisembark(),"Traversal island landing");yield return null;
            var route=Object.FindObjectsOfType<FishingClimbRoute>()[0];
            foreach(var candidate in Object.FindObjectsOfType<FishingClimbRoute>())if(FishingPrototype.FlatDistance(candidate.Bottom,FishingIslandWorld.Center(0))<35)route=candidate;
            var walk=Walk(c,route.Bottom+Vector3.forward*.6f);while(walk.MoveNext())yield return null;
            Check(c.Shore.TryClimb(),"Authored rope ladder accepts E");
            for(int i=0;i<260 && c.Shore.Climbing;i++){c.Shore.Move(Vector2.up,false,false,false,.05f);yield return null;}
            Check(!c.Shore.Climbing && Vector3.Distance(c.Shore.Position,route.Top)<.4f && c.Shore.Stamina<.95f,"Climb reaches supported lookout and spends stamina");
            var bag=c.Career.Backpack;for(int slot=0;slot<FishingBackpack.SlotCount;slot++)if(bag.At(slot).item==FieldItem.CookedMeat){if(slot!=1)bag.Move(slot,1);break;}
            c.SelectFieldSlot(1);float tired=c.Shore.Stamina;int rations=bag.Count(FieldItem.CookedMeat);
            Check(c.Shore.EatRation() && bag.Count(FieldItem.CookedMeat)==rations-1 && c.Shore.Stamina>tired,"Real purchased ration spends one item and restores stamina");
            Check(!c.Shore.EatRation() && bag.Count(FieldItem.CookedMeat)==rations-1,"Full stamina never wastes a ration");
            c.viewCamera.transform.SetPositionAndRotation(c.Shore.Position+new Vector3(3,2,4),Quaternion.LookRotation(new Vector3(-3,-1,-4)));
            FishingV016Validation.Capture(c,evidence,"climbing-lookout");
            c.ReturnToHarbor();yield return null;yield return null;
            PlaceBoat(c,FishingIslandWorld.Berth(0));wait=Wait(.5f);while(wait.MoveNext())yield return null;
            Check(c.Shore.TryDisembark(),"Swimming island landing");yield return null;
            walk=Walk(c,FishingIslandWorld.Center(0)+new Vector3(0,1.12f,33));while(walk.MoveNext())yield return null;
            c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.right);
            for(int i=0;i<100;i++){c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.right);c.Shore.Move(Vector2.up,true,false,false,.05f);yield return null;}
            Check(c.Shore.Swimming && c.Shore.Position.y<.4f && c.Shore.Position.y> -2 && c.Shore.Stamina<1,"Coast movement enters real wave-following swimming; position="+c.Shore.Position+" swimming="+c.Shore.Swimming);
            float stamina=c.Shore.Stamina;
            for(int i=0;i<60;i++){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}
            Check(c.Shore.Stamina>stamina && c.Shore.Position.y> -2,"Swimming rest restores stamina and keeps afloat");
            var shoreTarget=FishingIslandWorld.Center(0)+new Vector3(0,1.08f,26.4f);
            for(int i=0;i<250 && c.Shore.Swimming;i++)
            {
                c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(shoreTarget-c.Shore.Position,Vector3.up).normalized);
                c.Shore.Move(Vector2.up,false,true,false,.05f);yield return null;
            }
            Check(!c.Shore.Swimming && c.Shore.Position.y>.6f,"Swimming returns to a low real shore without rescue; position="+c.Shore.Position);
            c.ReturnToHarbor();yield return null;
            Check(!c.Shore.Swimming && !c.Shore.Climbing && !c.Realms.Transitioning,"Rescue clears traversal and scene state");
        }
        private static IEnumerator Steering(FishingPrototype c)
        {
            var driver=c.Player.gameObject.AddComponent<FishingV017Driver>();driver.Engine=c.Player.engine;c.enabled=false;
            try
            {
                PlaceBoat(c,new Vector3(-350,.35f,100));var wait=Wait(1);while(wait.MoveNext())yield return null;
                driver.Throttle=1;wait=Wait(5);while(wait.MoveNext())yield return null;driver.Steering=1;
                float yaw=0,tilt=0,until=Time.time+8;int wet=0,samples=0;
                while(Time.time<until){yaw=Mathf.Max(yaw,Mathf.Abs(c.Player.engine.RB.angularVelocity.y));tilt=Mathf.Max(tilt,Vector3.Angle(c.Player.transform.up,Vector3.up));if(c.Player.engine.InWater)wet++;samples++;yield return null;}
                Check(yaw>.65f && yaw<=1.8f && tilt<1 && wet>samples*.95f,"Faster real turning stays upright and powered; yaw="+yaw+" wet="+wet+"/"+samples);
            }
            finally{Object.Destroy(driver);c.enabled=true;c.ReturnToHarbor();}
        }
    }
}
