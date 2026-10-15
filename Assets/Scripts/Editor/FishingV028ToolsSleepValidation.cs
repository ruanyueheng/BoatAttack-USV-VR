using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV028ToolsSleepValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V028-TOOLS-SLEEP");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V028-TOOLS-SLEEP] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Tools and bed checks use the isolated profile");
            var original=c.Career.Snapshot();string output="Logs/V028ToolsSleep";Directory.CreateDirectory(output);
            try
            {
                c.ReturnToHarbor();yield return null;yield return null;
                var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
                Check(c.Shore.TryDisembark() && c.Shore.Relocate(c.Home.WakePoint,Quaternion.Euler(0,-90,0)),"Reach the actual new furnished bedroom");
                if(!c.FirstPerson)c.ToggleView();c.View.ResetLook();yield return null;yield return null;
                Check(c.Career.Backpack.TryAdd(FieldItem.Lantern,1),"Own an actual supplied lantern");
                int slot=-1;for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==FieldItem.Lantern){slot=i;break;}
                if(slot!=1)Check(c.Career.Backpack.Move(slot,1),"Put lantern into hotbar");
                c.SelectFieldSlot(1);c.UseFieldItem();c.PresentCharacterFrame();
                var lamp=c.FieldWorld.transform.Find("Field lantern light").GetComponent<Light>();
                Check(c.FieldWorld.LanternHeld && c.LanternOn && lamp.enabled,"Held source lantern lights its real chamber");
                c.SelectFieldSlot(0);c.PresentCharacterFrame();
                var holster=c.FieldWorld.HolsteredLantern;
                Check(holster!=null && holster.gameObject.activeInHierarchy && !c.FieldWorld.LanternHeld && c.LanternOn && lamp.enabled,"Switching tools clips the lit lantern to the waist");
                float nearest=float.MaxValue;var character=c.Shore.Character;
                foreach(var vertex in holster.GetComponent<MeshFilter>().sharedMesh.vertices)
                    nearest=Mathf.Min(nearest,Vector3.Dot(holster.TransformPoint(vertex)-character.HipPosition,c.Shore.Actor.right));
                Check(nearest>character.WaistHalfWidth+.025f,"Entire lantern mesh has positive clearance outside the source waist");
                Check(Vector3.Distance(lamp.transform.position,holster.TransformPoint(FishingFieldWorld.LanternLightPoint))<.002f,"The same warm light follows the visible waist lantern");
                FishingV016Validation.Capture(c,output,"waist-lantern");
                c.SelectFieldSlot(1);c.PresentCharacterFrame();Check(c.FieldWorld.LanternHeld && !holster.gameObject.activeSelf,"Equipping returns the same lantern to hand with no duplicate");
                c.SelectFieldSlot(0);c.PresentCharacterFrame();
                double before=c.Career.Calendar.TotalMinutes;var feet=c.Shore.Position;var eye=c.viewCamera.transform.position;
                Check(c.Home.Sleep(),"Bed starts the authored getting-on/lying/getting-off motion");
                c.Home.Tick(float.NaN);c.Home.Tick(float.PositiveInfinity);c.Home.Tick(-1);Check(c.Home.SleepProgress==0,"Malformed time cannot advance sleep or grant a morning");
                for(int i=0;i<6;i++)c.Home.Tick(.25f);c.PresentCharacterFrame();
                Check(c.Home.SleepPhase=="坐上床沿" && c.Home.SleepFade==0 && c.Career.Calendar.TotalMinutes==before,"Visible seated phase occurs before any fade or calendar commitment");
                var bedBounds=c.Home.Bed.GetComponentInChildren<Renderer>().bounds;var hip=character.HipPosition;
                Check(hip.x>bedBounds.min.x && hip.x<bedBounds.max.x && hip.z>bedBounds.min.z && hip.z<bedBounds.max.z,"Seated hips rest over the mattress instead of hanging outside the bedside");
                foreach(var frame in CaptureSleepPose(c,output,"sleep-sit"))yield return frame;
                CheckSeatedPose(c,"Sitting down");CheckCameraClearance(c,"Sitting down");
                c.Home.CancelSleep();c.PresentCharacterFrame();Check(c.Career.Calendar.TotalMinutes==before && c.Shore.Position==feet,"Cancelling before lying down preserves time and safe foot position");
                yield return null;yield return null;
                Check(c.Home.Sleep(),"A cancelled bed interaction can restart normally");
                for(int i=0;i<10;i++)c.Home.Tick(.25f);c.Home.Tick(.10f);c.PresentCharacterFrame();
                Debug.Log("[V028-TOOLS-SLEEP] RECLINE phase="+c.Home.SleepPhase+" baselineEye="+eye.ToString("F4")+" posedEye="+c.Home.SleepEyePosition.ToString("F4")+" camera="+c.viewCamera.transform.position.ToString("F4")+" hip="+character.HipPosition.ToString("F4")+" bed="+bedBounds+" fade="+c.Home.SleepFade);
                foreach(var frame in CaptureSleepPose(c,output,"sleep-recline"))yield return frame;
                Check(c.Home.SleepPhase=="躺下" && c.Home.SleepEyePosition.y<eye.y-.35f && c.Home.SleepFade<.05f,"The rig actually reclines to mattress height while the view is still visible");
                CheckCameraClearance(c,"Reclining");
                Check(c.Shore.Position==feet && !c.RequestWorldInteraction(),"Physical capsule stays safely beside the bed and duplicate interaction is blocked");
                for(int i=0;i<5;i++)c.Home.Tick(.25f);
                Check(c.Career.Calendar.TotalMinutes>before && c.Career.Calendar.Hour==6,"The morning commits once only after visible lying-down motion");
                double morning=c.Career.Calendar.TotalMinutes;
                for(int i=0;i<6;i++)c.Home.Tick(.25f);c.PresentCharacterFrame();
                Check(c.Home.Sleeping && c.Home.SleepPhase=="坐起" && c.Home.SleepFade==0,"Morning shows the sitting-up phase after the fade clears");
                foreach(var frame in CaptureSleepPose(c,output,"sleep-get-up"))yield return frame;
                CheckSeatedPose(c,"Sitting up");CheckCameraClearance(c,"Sitting up");
                for(int i=0;i<7;i++)c.Home.Tick(.25f);c.PresentCharacterFrame();
                Check(!c.Home.Sleeping && c.Shore.Position==feet && c.Career.Calendar.TotalMinutes==morning,"Getting off ends on the original supported floor and never advances morning twice");
                Check(c.NavigateHome() && c.NavigationTarget==c.HomeNavigationIndex,"Navigation retains the actual V027 home's waypoint");
            }
            finally
            {
                c.Home.CancelSleep();c.RestoreShopTestProfile(original);c.FieldWorld.RefreshPickups();
                if(c.Ashore)c.Shore.TryBoard();c.ReturnToHarbor();
            }
        }
        private static IEnumerable CaptureSleepPose(FishingPrototype c,string output,string name)
        {
            float timeScale=Time.timeScale,progress=c.Home.SleepProgress;bool firstPerson=c.FirstPerson,enabled=c.enabled;
            try
            {
                // Sleep uses unscaled time, so freeze its driver as well as the animation clock.
                // Two real frames let each source skin upload the current bone matrices before capture.
                Time.timeScale=0;c.enabled=false;
                if(!c.FirstPerson)c.ToggleView();c.PresentCharacterFrame();
                yield return null;yield return null;
                c.PresentCharacterFrame();FishingV016Validation.Capture(c,output,name);
                c.ToggleView();c.PresentCharacterFrame();
                yield return null;yield return null;
                c.PresentCharacterFrame();FishingV016Validation.Capture(c,output,name+"-body");
                Debug.Log("[V028-TOOLS-SLEEP] CAPTURE "+name+" frame="+Time.frameCount+" hip="+c.Shore.Character.HipPosition.ToString("F4")+" leftWrist="+c.Shore.Character.LeftWrist.position.ToString("F4")+" rightWrist="+c.Shore.Character.RightWrist.position.ToString("F4"));
                Check(Mathf.Approximately(progress,c.Home.SleepProgress),"Cross-frame sleep capture preserves the authored phase");
            }
            finally
            {
                c.enabled=enabled;Time.timeScale=timeScale;
                if(c.FirstPerson!=firstPerson)c.ToggleView();c.PresentCharacterFrame();
            }
        }
        private static void CheckSeatedPose(FishingPrototype c,string phase)
        {
            var character=c.Shore.Character;var outward=Vector3.ProjectOnPlane(c.Home.WakePoint-character.HipPosition,Vector3.up).normalized;
            foreach(string side in new[]{"L","R"})
            {
                var thigh=Bone(character,"UpperLeg."+side);var knee=Bone(character,"LowerLeg."+side);
                var ankle=Bone(character,"Ankle."+side);var foot=Bone(character,"Foot."+side);
                Debug.Log("[V028-TOOLS-SLEEP] "+phase+" "+side+" kneeForward="+Vector3.Dot(knee.position-thigh.position,outward).ToString("F4")+" calfDrop="+(knee.position.y-ankle.position.y).ToString("F4")+" ankleFoot="+Vector3.Distance(ankle.position,foot.position).ToString("F4"));
                Check(Vector3.Dot(knee.position-thigh.position,outward)>.10f && knee.position.y>ankle.position.y+.12f,phase+" knees bend out from the bed with calves hanging down");
                Check(Vector3.Distance(ankle.position,foot.position)<.16f,phase+" independent source feet remain attached to their ankles");
            }
            Check(character.LeftWrist.position.y<character.ShoulderPosition(true).y-.15f && character.RightWrist.position.y<character.ShoulderPosition(false).y-.15f,phase+" hands rest below the shoulders instead of holding an invisible tool");
        }
        private static Transform Bone(FishingCharacter character,string name)
        {
            foreach(var bone in character.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;
            throw new InvalidOperationException("Missing source sleep bone "+name);
        }
        private static void CheckCameraClearance(FishingPrototype c,string phase)
        {
            foreach(var collider in Physics.OverlapSphere(c.viewCamera.transform.position,.06f,~(1<<4),QueryTriggerInteraction.Ignore))
                Check(!collider.transform.IsChildOf(c.Home.House),phase+" eye clears the bed and furniture: "+collider.name);
            Debug.Log("[V028-TOOLS-SLEEP] CHECK: "+phase+" camera has furnished-room clearance");
        }
    }
}
