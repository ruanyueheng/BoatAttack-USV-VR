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
            // A gameplay regression reads shipped icons; rebuilding legacy art belongs to the art installer.
            foreach(string path in Directory.GetFiles("Assets/Resources/FishingV019/Thumbnails","*.png"))
            {
                var icon=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    Check(icon.LoadImage(File.ReadAllBytes(path)),"Readable shipped icon "+Path.GetFileName(path));
                    int visible=0;foreach(var pixel in icon.GetPixels32())if(pixel.a>32 && pixel.r+pixel.g+pixel.b>40)visible++;
                    Check(visible>=150,"Visible shipped icon "+Path.GetFileName(path));
                }
                finally{Object.DestroyImmediate(icon);}
            }
            Check(c.Realms!=null && c.Residents!=null && c.Regions!=null,"World systems initialized with production scene");
            Check(c.NavigationCount==17 && c.Career.Lore.ClearCount==0,"Existing island navigation, home, two islets and new empty story progress");
            Check(!c.Residents.Talk(0) && !c.Realms.Enter(0),"Remote dialogue/entry rejected from the boat");
            for(int i=0;i<6;i++)Check(c.Residents.Person(i)!=null && c.Residents.Person(i).GetComponentsInChildren<Renderer>().Length>0,"Real NPC geometry "+i);
            var wait=Wait(.5f);while(wait.MoveNext())yield return null;
            for(int island=0;island<4;island++)
            {
                var level=FishingV035Levels.Island(c,island,evidence);while(level.MoveNext())yield return level.Current;
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
            c.Residents.Choose(1);Check(c.Residents.Speech.Contains("只有确认购买才付款") && c.Residents.Speech.Contains("余额不足"),"Clerk explains actual goods and purchase rules");c.Residents.Close();yield return null;yield return null;
            Check(c.ToggleShop() && c.TackleShop.BuySupply(12) && c.Career.Backpack.Count(FieldItem.CookedMeat)==3,"Remote voyage ration is a real bought item");c.TackleShop.Close();yield return null;yield return null;
            c.viewCamera.transform.SetPositionAndRotation(c.TackleShop.transform.TransformPoint(new Vector3(0,1.75f,-4.3f)),Quaternion.LookRotation(c.TackleShop.transform.forward));
            FishingV016Validation.Capture(c,evidence,"shop-interior");
            var display=c.TackleShop.transform.Find("V018 stocked chandlery");Check(display.childCount>70 && display.GetComponents<BoxCollider>().Length==2,"Store has distinct 3D goods and retained clear aisle colliders");
            var saved=FishingSaveStore.Load(c.SavePath,out string warning);
            Check(warning=="" && saved.Snapshot().version==15 && saved.Lore.Finale && saved.Lore.ClearCount==4,"Real Unity JSON story save round trip");
            c.ReturnToHarbor();yield return null;yield return null;
            var traversal=Traversal(c,evidence);while(traversal.MoveNext())yield return null;
            c.ReturnToHarbor();yield return null;yield return null;
            var steering=Steering(c);while(steering.MoveNext())yield return null;
            Debug.Log("[V019] EVIDENCE: "+evidence);
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
            Check(!c.Shore.Climbing && Vector3.Distance(c.Shore.Position,route.Top)<.4f && c.Shore.Stamina<.95f,"Climb reaches supported lookout and spends stamina; position="+c.Shore.Position+" top="+route.Top+" climbing="+c.Shore.Climbing+" stamina="+c.Shore.Stamina);
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
