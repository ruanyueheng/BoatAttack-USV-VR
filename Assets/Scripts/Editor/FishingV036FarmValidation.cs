using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV036FarmValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V036-FARM");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V036-FARM] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var original=c.Career.Snapshot();string evidence=Path.GetFullPath("Logs/V036");Directory.CreateDirectory(evidence);
            try
            {
                c.CancelWorldAction();c.ReturnToHarbor();yield return null;
                var fixture=new FishingCareer();fixture.Farm.ClaimStarter(fixture.Backpack);
                for(int i=0;i<2;i++){fixture.Farm.Till(i);fixture.Farm.Plant(i,fixture.Backpack);}fixture.Farm.WaterPlot(1);
                c.RestoreShopTestProfile(fixture.Snapshot());c.SelectFieldSlot(8);
                var home=c.Home;var rb=c.Player.engine.RB;var berth=home.Berth+Vector3.up*.7f;
                rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
                for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Real home berth allows the player to reach the persistent watering can");yield return null;
                if(!c.FirstPerson)c.ToggleView();
                var can=c.Farm.WateringCan;var dock=c.Farm.Target(15);
                Check(can!=null && can.gameObject.activeInHierarchy && !c.Farm.CarryingCan && Vector3.Distance(can.position,dock.position)<.001f,"The same visible can starts on the well-side stand");
                var well=c.Farm.Target(12);var wellWater=c.Farm.WellWater;
                var homeGround=c.transform.Find("V035 Home HomeLand").GetComponentInChildren<MeshCollider>();
                foreach(var offset in new[]{Vector3.zero,Vector3.left*.60f,Vector3.right*.60f,Vector3.forward*.60f,Vector3.back*.60f})
                    Check(!homeGround.Raycast(new Ray(well.position+offset+Vector3.up*.15f,Vector3.down),out _,2),"The actual island mesh has a round opening beneath the well, offset "+offset);
                Check(wellWater!=null && Mathf.Abs(wellWater.position.y-well.position.y-FishingFarmWorld.WellWaterLevel)<.001f
                    && well.position.y+.88f-wellWater.position.y>1.2f,"Visible animated well water lies below the stone rim instead of covering grass at ground level");
                var waterSurface=wellWater.GetComponent<MeshCollider>();
                Check(waterSurface.Raycast(new Ray(wellWater.position+Vector3.right*.5f+Vector3.up,Vector3.down),out var waterHit,2)
                    && Mathf.Abs(waterHit.point.y-wellWater.position.y)<.001f,"The water surface spans the actual shaft opening");
                var wellFixture=c.Farm.Root.Find("GardenFixtures").GetComponentInChildren<MeshCollider>();bool backfaces=Physics.queriesHitBackfaces;
                try
                {
                    Physics.queriesHitBackfaces=true;
                    Check(wellFixture.Raycast(new Ray(well.position+Vector3.right*.45f+Vector3.up*.15f,Vector3.down),out var bottom,7)
                        && bottom.point.y<well.position.y+FishingFarmWorld.WellShaftBottom+.05f,"Masonry and the submerged well floor extend more than five metres below the island");
                }
                finally{Physics.queriesHitBackfaces=backfaces;}
                var wellView=Approach(c,12);while(wellView.MoveNext())yield return null;Aim(c,wellWater.position);yield return null;
                FishingV016Validation.Capture(c,evidence,"well-clear-water-depth");
                var move=Approach(c,0);while(move.MoveNext())yield return null;
                int water=c.Career.Farm.Water;
                Check(!c.RequestWorldInteraction() && !c.WorldActionBusy && c.Career.Farm.Water==water && c.Career.Farm.Plot(0).wateredDay<0,"A plot cannot create a can or water without picking it up");

                move=Approach(c,15);while(move.MoveNext())yield return null;
                Check(c.RequestWorldInteraction() && c.WorldActionBusy && !c.Farm.CarryingCan,"Pickup reaches for the can before ownership changes");
                c.CancelWorldAction();yield return null;
                Check(!c.Farm.CarryingCan && Vector3.Distance(can.position,dock.position)<.001f && c.Career.Farm.Water==water,"Cancelled pickup leaves the original can on its stand");
                float outerAccepted=0;
                foreach(float distance in new[]{.65f,.70f,.78f,.88f,.98f,1.08f})
                {
                    var stance=dock.position+Vector3.left*distance;stance.y=c.Home.Origin.y;
                    if(!c.Shore.Relocate(stance,Quaternion.identity))continue;c.View.ResetLook();AimCan(c);
                    for(int i=0;i<4;i++){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}c.PresentCharacterFrame();c.viewCamera.transform.LookAt(c.Farm.Aim(15));yield return null;
                    if(!c.RequestWorldInteraction()){Check(!c.Farm.Busy && !c.Farm.CarryingCan,"Unreachable handle rejects pickup before any ownership change at "+distance);continue;}
                    var grip=WaitForProgress(c,.27f);while(grip.MoveNext())yield return null;c.PresentCharacterFrame();
                    Check(c.Farm.CanGripError<.065f,"Actual palm reaches the handle at pickup contact, distance "+distance+", error "+c.Farm.CanGripError);
                    outerAccepted=distance;c.CancelWorldAction();yield return null;
                }
                Check(outerAccepted>0,"At least one actual capsule stance reaches the can handle");Debug.Log("[V036-FARM] Farthest accepted tested can distance: "+outerAccepted);
                move=Approach(c,15);while(move.MoveNext())yield return null;
                Check(c.RequestWorldInteraction(),"Pickup can be retried after cancellation");
                var finish=Finish(c);while(finish.MoveNext())yield return null;
                Check(c.Farm.CarryingCan && c.Farm.WateringCan==can && can.gameObject.activeInHierarchy,"Completed pickup carries the original can instead of spawning a prop");
                c.PresentCharacterFrame();
                Check(Vector3.Distance(c.Shore.Character.PalmPosition(false),can.TransformPoint(FishingFarmWorld.CanPalm))<.065f,"Solved source wrist follows the watering-can handle");
                FishingV016Validation.Capture(c,evidence,"watering-can-carried");

                move=Approach(c,12);while(move.MoveNext())yield return null;
                var bucketRest=c.Farm.WellBucket.position;
                Check(c.RequestWorldInteraction() && c.Career.Farm.Water==water,"Well refill starts without granting water immediately");
                var phase=WaitForProgress(c,.22f);while(phase.MoveNext())yield return null;c.PresentCharacterFrame();
                Check(c.Farm.WellBucket.position.y+.37f<wellWater.position.y-.02f && c.Career.Farm.Water==water,"The real bucket lowers fully into the shaft water before lifting or granting a refill");
                FishingV016Validation.Capture(c,evidence,"well-bucket-submerged");
                phase=WaitForProgress(c,.62f);while(phase.MoveNext())yield return null;
                c.PresentCharacterFrame();
                Check(Vector3.Distance(c.Farm.WellBucket.position,bucketRest)>.3f && c.Farm.WaterStreamVisible,"The authored bucket rises and visibly pours into the held can");
                Debug.Log("[V036-FARM] REFILL camera="+c.viewCamera.transform.position.ToString("F3")+" actor="+c.Shore.Position.ToString("F3")+" bucket="+c.Farm.WellBucket.position.ToString("F3")+" can="+can.position.ToString("F3"));
                foreach(var renderer in c.Farm.WellBucket.GetComponentsInChildren<Renderer>())
                {
                    Debug.Log("[V036-FARM] REFILL bucket bounds="+renderer.bounds);
                    Check(Vector3.Distance(renderer.bounds.ClosestPoint(c.viewCamera.transform.position),c.viewCamera.transform.position)>.06f,"The full-size pouring bucket and handle stay outside the first-person eye and near clip plane");
                }
                FishingV016Validation.Capture(c,evidence,"well-bucket-pouring");
                c.ToggleView();c.PresentCharacterFrame();FishingV016Validation.Capture(c,evidence,"well-bucket-pouring-thirdperson");c.ToggleView();c.PresentCharacterFrame();
                c.CancelWorldAction();yield return null;
                Check(c.Career.Farm.Water==water && c.Farm.CarryingCan && !c.Farm.WaterStreamVisible,"Interrupted bucket pouring grants no water and retains the carried can");
                Check(c.RequestWorldInteraction(),"The cancelled refill can restart");finish=Finish(c);while(finish.MoveNext())yield return null;
                Check(c.Career.Farm.Water==FishingFarm.WaterCapacity,"Only a completed refill fills the bounded, saved water reservoir");
                c.PresentCharacterFrame();Check(c.Farm.CanWater.gameObject.activeSelf,"Refilled ivory can visibly contains water inside its opening");
                FishingV038Validation.InspectLiquid(c);
                FishingV038Validation.InspectLiquidOrientations(c);

                move=Approach(c,0);while(move.MoveNext())yield return null;
                int attempts=c.ActionCommitAttempts;
                Check(c.RequestWorldInteraction() && !c.RequestWorldInteraction() && c.Career.Farm.Water==12,"Repeated input cannot overlap watering or consume water at its start");
                phase=WaitForProgress(c,.48f);while(phase.MoveNext())yield return null;c.PresentCharacterFrame();
                Check(c.Farm.WaterStreamVisible && c.Career.Farm.Plot(0).wateredDay<0,"Visible spout water and soil splashes play before the final crop transaction");
                FishingV016Validation.Capture(c,evidence,"watering-stream-and-splashes");
                c.TogglePause();yield return null;
                Check(!c.Farm.Busy && c.Career.Farm.Water==12 && c.Career.Farm.Plot(0).wateredDay<0 && c.ActionCommitAttempts==attempts,"Pausing cancels an unfinished pour without water loss or crop growth");
                c.TogglePause();yield return null;
                c.PresentCharacterFrame();c.viewCamera.transform.LookAt(c.Farm.Aim(0));yield return null;
                Check(c.RequestWorldInteraction(),"Watering restarts with the same carried can");finish=Finish(c);while(finish.MoveNext())yield return null;
                Check(c.Career.Farm.Water==11 && c.Career.Farm.Plot(0).wateredDay==c.Career.Farm.Day && c.ActionCommitAttempts==attempts+1,"Completion waters one crop and consumes exactly one saved charge");
                Check(!c.RequestWorldInteraction() && c.Career.Farm.Water==11,"A watered crop cannot charge the can again on repeated input");
                FishingSaveStore.Save(c.SavePath,c.Career);var loaded=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning=="" && loaded.Farm.Water==11 && loaded.Farm.Plot(0).wateredDay==loaded.Farm.Day,"Real JSON save preserves the existing water and crop schema");
                move=Approach(c,15);while(move.MoveNext())yield return null;
                Check(c.RequestWorldInteraction() && c.Farm.CarryingCan,"Returning the can animates before releasing the handle");
                phase=WaitForProgress(c,.88f);while(phase.MoveNext())yield return null;c.PresentCharacterFrame();Check(c.Farm.CanGripError<.065f,"Actual palm remains attached to the handle when the can contacts its stand");
                finish=Finish(c);while(finish.MoveNext())yield return null;
                Check(!c.Farm.CarryingCan && can==c.Farm.WateringCan && Vector3.Distance(can.position,dock.position)<.001f && c.Career.Farm.Water==11,"The same can rests on the original stand with its remaining water intact");
                FishingV016Validation.Capture(c,evidence,"watering-can-returned");
                c.RestoreShopTestProfile(loaded.Snapshot());yield return null;
                Check(!c.Farm.CarryingCan && c.Career.Farm.Water==11 && can==c.Farm.WateringCan,"Loading a profile safely docks the prop without duplicating or discarding saved water");
            }
            finally{if(c.Paused)c.TogglePause();c.CancelWorldAction();c.ReturnToHarbor();c.RestoreShopTestProfile(original);}
        }
        private static IEnumerator Approach(FishingPrototype c,int id)
        {
            var p=c.Farm.Target(id).position+(id==15?Vector3.left*.65f:Vector3.back*(id<12?1.1f:1.3f));p.y=c.Home.Origin.y;if(id<12)p.x-=.25f;
            bool relocated=c.Shore.Relocate(p,Quaternion.identity);
            if(!relocated)
            {
                bool supported=c.Shore.Ground(p,out var floor);Debug.Log("[V036-FARM] Stance rejection target="+id+" requested="+p+" supported="+supported+" floor="+floor);
                foreach(var collider in Physics.OverlapCapsule(p+Vector3.up*.36f,p+Vector3.up*1.46f,.28f,~(1<<4),QueryTriggerInteraction.Ignore))
                    Debug.Log("[V036-FARM] Stance overlap "+collider.name+" parent="+collider.transform.parent?.name+" bounds="+collider.bounds);
            }
            Check(relocated,"Actor capsule can stand at the actual farm target "+id);
            c.View.ResetLook();if(id==15)AimCan(c);
            for(int i=0;i<4;i++){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}
            Check(c.Shore.Grounded,"Relocated farm stance settles onto its physical floor "+id);
            Aim(c,c.Farm.Aim(id));yield return null;
            Check(c.Farm.TryTarget(out int found,out _) && found==id,"Production farm targeting selects "+id);
        }
        private static void AimCan(FishingPrototype c)
        {float scale=.12f*c.MouseSensitivity;c.View.ApplyShoreLookDelta(new Vector2(90/scale,-55/scale*(c.InvertLook?-1:1)));}
        internal static void Aim(FishingPrototype c,Vector3 target)
        {
            c.View.ResetLook();c.PresentCharacterFrame();var direction=(target-c.viewCamera.transform.position).normalized;
            float yaw=Mathf.DeltaAngle(c.Shore.Actor.eulerAngles.y,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg);
            float pitch=-Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg,scale=.12f*c.MouseSensitivity;
            c.View.ApplyShoreLookDelta(new Vector2(yaw/scale,-pitch/scale*(c.InvertLook?-1:1)));c.View.ApplyView();
        }
        private static IEnumerator Finish(FishingPrototype c)
        {int frames=0;while(c.WorldActionBusy && frames++<600)yield return null;Check(frames<600,"Farm animation finishes and releases movement");yield return null;yield return null;}
        private static IEnumerator WaitForProgress(FishingPrototype c,float p)
        {int frames=0;while(c.Farm.Busy && c.Farm.WorkProgress<p && frames++<600)yield return null;Check(c.Farm.Busy && frames<600,"Farm action reaches its visible animation phase before completion");}
    }
}
