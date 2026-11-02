using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV036OrchardValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V036-ORCHARD");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V036-ORCHARD] CHECK: "+label);}
        public static void Rules()
        {
            var gesture=new FishingTreeShake();gesture.Sample(Vector3.zero,false,true,.1f);gesture.Sample(Vector3.zero,true,true,.1f);
            for(int i=0;i<20;i++)gesture.Sample(Vector3.zero,true,true,.1f);
            Check(gesture.Strokes==0,"Stationary hands and moving head never shake a tree");
            for(int stroke=0;stroke<3;stroke++)
            {
                var p=Vector3.right*(stroke%2==0?.09f:0);gesture.Sample(p,true,true,.1f);gesture.Sample(p,true,true,.1f);gesture.Sample(p,true,true,.1f);
            }
            Check(gesture.Strokes==3,"Three real alternating nine centimetre strokes complete the shake");
            gesture.Sample(Vector3.zero,true,false,.1f);for(int i=0;i<8;i++)gesture.Sample(Vector3.right*(i%2==0?.1f:0),true,true,.1f);
            Check(gesture.Strokes==0,"Tracking loss requires a fresh release before rearming");
            gesture.Sample(Vector3.zero,false,true,.1f);gesture.Sample(Vector3.zero,true,true,.1f);gesture.Sample(Vector3.right*5,true,true,.1f);
            Check(gesture.Strokes==0,"Tracking teleports cannot drop fruit");
            var nativePalm=new Vector3(4,1,2);var calibratedCenter=new Vector3(4,0,2);var point=FishingHandGames.SieveMotionPoint(nativePalm,calibratedCenter,Quaternion.identity);
            Check(Quaternion.Euler(0,90,0)*nativePalm+Vector3.right!=nativePalm,"World rig fixture moves independently of the tracked controller");
            var sieve=new FishingHandTask(FishingHandTaskKind.Sieve);for(int i=0;i<40;i++)
            {
                var still=FishingHandGames.SieveMotionPoint(nativePalm,calibratedCenter,Quaternion.identity);sieve.Sample(still.x,still.y,.05f);
            }
            Check(sieve.Strokes==0 && point==Vector2.up,"Head motion, stick locomotion and snap turns cannot synthesize sieve strokes");
            int bonks=0;for(int day=0;day<20;day++)for(int tree=0;tree<5;tree++)if(FishingHome.OrchardHeadBonk(tree,day))bonks++;
            Check(bonks==20,"Playful head bonks occur on one fifth of tree-day pairs");
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Rules();c.SelectFieldSlot(8);c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;var berth=c.Home.Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=berth;c.Player.transform.position=berth;Physics.SyncTransforms();
            for(int i=0;i<15;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Orchard is reachable from the home jetty");yield return null;
            var treeRoot=c.Home.OrchardTreeRoot(0);Check(treeRoot!=null && c.Home.OrchardFruit(0)!=null,"Existing orchard tree and supplied apple model are registered");
            Check(c.Shore.Relocate(treeRoot.position+Vector3.back*1.55f,Quaternion.identity),"Tree has supported standing room");yield return null;yield return null;
            Check(c.Home.ReachInteraction(7) && c.Home.ShakeOrchardTree(0),"Production tree interaction begins leaf and fruit motion");
            Check(!c.Home.ShakeOrchardTree(0),"Repeated shaking cannot create duplicate ground fruit");
            float until=Time.time+2.3f;while(Time.time<until)yield return null;
            Check(c.Home.OrchardFruitLanded(0) && c.Home.OrchardFruit(0).gameObject.activeSelf,"Fruit lands and remains available for collection");
            int before=c.Career.Backpack.Count(FieldItem.IslandFruit);
            for(int frame=0;frame<8;frame++){c.Shore.Move(Vector2.zero,false,false,false,.05f);c.PresentCharacterFrame();yield return null;}
            Check(c.Shore.Grounded,"Orchard pickup stands on supported ground");
            Check(c.Home.InteractAt(7) && c.WorldActionBusy,"Desktop pickup uses the existing reachable hand-contact animation");
            until=Time.time+3;while(c.WorldActionBusy && Time.time<until)yield return null;
            Check(c.Career.Backpack.Count(FieldItem.IslandFruit)==before+1 && !c.Home.OrchardFruit(0).gameObject.activeSelf,"Only committed pickup adds the fruit and clears the world drop");
            Check(!c.Home.ShakeOrchardTree(0) && !c.Career.CanHarvestHomeFruit(0),"The saved daily limit prevents repeated rewards");
            var second=c.Home.OrchardTreeRoot(1);
            Check(c.Shore.Relocate(second.position+Vector3.back*1.55f,Quaternion.identity) && c.Home.ShakeOrchardTree(1),"Second tree supports grip pickup fixture");
            until=Time.time+2.3f;while(Time.time<until)yield return null;
            var fruit=c.Home.OrchardFruit(1);before=c.Career.Backpack.Count(FieldItem.IslandFruit);
            Check(!c.Home.TryGripOrchardFruit(fruit.position+Vector3.up) && !c.Home.TryGripOrchardFruit(new Vector3(float.NaN,0,0)),"Distant or invalid hand cannot take fruit");
            Check(c.Home.TryGripOrchardFruit(fruit.position+Vector3.up*.08f),"Near hand grip reaches the real fallen fruit transaction");
            Check(c.Career.Backpack.Count(FieldItem.IslandFruit)==before+1 && !fruit.gameObject.activeSelf
                && !c.Home.TryGripOrchardFruit(fruit.position),"Grip pickup adds exactly one fruit and cannot repeat");
        }
    }
}
