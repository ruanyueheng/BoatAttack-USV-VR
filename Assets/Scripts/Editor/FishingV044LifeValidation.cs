using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>V044 生活玩法：相机拍照与相册、图鉴解锁、掌心地图手势显隐、节日轮转与装饰、存档往返与旧档兼容（Logs/V044/life）。</summary>
    public static class FishingV044LifeValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-LIFE");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-LIFE] CHECK: "+label);}
        private static IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Only the isolated automation profile is used");
            Rules();Saves();
            string dir=Path.GetFullPath("Logs/V044/life");Directory.CreateDirectory(dir);
            foreach(var old in Directory.GetFiles(dir,"*.png"))File.Delete(old);
            var life=c.Life;Check(life!=null,"Life world is built");
            Check(c.AlbumDirectory.StartsWith(Path.GetDirectoryName(Path.GetFullPath(c.SavePath))),"Album lives beside the isolated saves, outside the save JSON");
            if(Directory.Exists(c.AlbumDirectory))Directory.Delete(c.AlbumDirectory,true);
            var run=CameraChecks(c,life,dir);while(run.MoveNext())yield return run.Current;
            run=PalmChecks(c,life,dir);while(run.MoveNext())yield return run.Current;
            run=FestivalChecks(c,life,dir);while(run.MoveNext())yield return run.Current;
        }

        private static void Rules()
        {
            Check(FishingFestival.On(12)==-1 && FishingFestival.On(13)==0 && FishingFestival.On(14)==-1,"Spring kite festival is day 14 only");
            Check(FishingFestival.On(28+12)==-1 && Enumerable.Range(28+13,3).All(d=>FishingFestival.On(d)==1) && FishingFestival.On(28+16)==-1,"Summer glow season lasts days 14–16");
            Check(FishingFestival.On(56+13)==2 && FishingFestival.On(84+13)==3,"Autumn harvest and winter lanterns on day 14");
            Check(FishingFestival.On(112+13)==0 && FishingFestival.On(5*112+84+13)==3,"Festivals rotate every year");
            Check(Enumerable.Range(0,112).Count(d=>FishingFestival.On(d)>=0)==6,"Six festival days per year");
            Check(FishingFestival.NextDay(0)==13 && FishingFestival.NextDay(14)==41 && FishingFestival.NextDay(97)==97 && FishingFestival.NextDay(98)==125,"Next festival day lookup");
            Check(Enumerable.Range(0,4).All(i=>FishingFestival.Name(i).Length>0 && FishingFestival.Blurb(i).Length>10) && FishingFestival.Name(-1)=="","Festival names and blurbs");
            Check(Enumerable.Range(0,FishingPhotoCodex.Count).All(e=>FishingPhotoCodex.Name(e).Length>0 && FishingPhotoCodex.Hint(e).Length>0),"Every codex entry has a name and a hint");
            Check(FishingPhotoCodex.TryParse(FishingPhotoCodex.FileName(7,30,-1),out int s,out int d,out int e) && s==7 && d==30 && e==-1,"Album file name round trip");
            Check(!FishingPhotoCodex.TryParse("p001_d2_e99.jpg",out _,out _,out _) && !FishingPhotoCodex.TryParse("other.jpg",out _,out _,out _),"Foreign album files are ignored");
            Check(FishingLifeWorld.PalmUp(Vector3.up*1.6f,new Vector3(0,1.25f,.3f),PalmUpRotation(Vector3.forward)),"Palm-up rule accepts a raised open left palm");
            Check(!FishingLifeWorld.PalmUp(Vector3.up*1.6f,new Vector3(0,1.25f,.3f),Quaternion.LookRotation(Vector3.forward,Vector3.up)),"Palm-up rule rejects a palm facing sideways");
            Check(!FishingLifeWorld.PalmUp(Vector3.up*1.6f,new Vector3(0,.8f,.2f),PalmUpRotation(Vector3.forward)),"Palm-up rule rejects a hand hanging at the hip");
            Check(FishingLifeWorld.RaisedToEye(Vector3.zero,Vector3.forward,new Vector3(0,0,.12f)) && !FishingLifeWorld.RaisedToEye(Vector3.zero,Vector3.forward,new Vector3(0,-.4f,.2f)),"Raise-to-eye rule");
        }
        private static Quaternion PalmUpRotation(Vector3 forward)
        {
            var q=Quaternion.LookRotation(forward,Vector3.Cross(forward,Vector3.up));
            if(Vector3.Angle(q*Vector3.right,Vector3.up)>1)throw new InvalidOperationException("palm fixture");return q;
        }
        private static void Saves()
        {
            var career=new FishingCareer();
            Check(career.RecordPhoto(FishingPhotoCodex.Lighthouse)==FishingPhotoCodex.FirstReward && career.RecordPhoto(FishingPhotoCodex.Lighthouse)==0 && career.RecordPhoto(99)==0,"Photo flag pays once and stays in range");
            var json=JsonUtility.ToJson(career.Snapshot());
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(json),out var back) && back.Lore.PhotoFlag(FishingPhotoCodex.Lighthouse) && back.Coins==career.Coins,"Photo codex survives a JSON round trip");
            var bad=JsonUtility.FromJson<FishingSaveData>(json);bad.lore.photoLog=1<<24;Check(!FishingCareer.TryRestore(bad,out _),"Out-of-range photo log is rejected");
            var forged=JsonUtility.FromJson<FishingSaveData>(json);forged.version=19;Check(!FishingCareer.TryRestore(forged,out _),"Pre-v20 saves cannot carry a photo log");
            var legacy=new FishingCareer().Snapshot();legacy.version=19;legacy.coins=55;
            var old=JsonUtility.ToJson(legacy);old=System.Text.RegularExpressions.Regex.Replace(old,",\"photoLog\":0","");
            Check(!old.Contains("photoLog"),"Fixture really lacks the photo log");
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(old),out var up) && up.Coins==55 && !up.Lore.PhotoFlag(FishingPhotoCodex.Lighthouse),"Old saves load with an empty photo codex");
        }

        private static IEnumerator Land(FishingPrototype c)
        {
            if(c.Ashore)c.Shore.BoardImmediately();
            c.ReturnToHarbor();yield return null;
            var w=Wait(.5f);while(w.MoveNext())yield return null;
            Check(c.Shore.TryDisembark(),"Landing at the harbour");yield return null;
        }
        private static IEnumerator StandFacing(FishingPrototype c,Vector3 target,float distance,float bearing=0)
        {
            foreach(var jitter in new[]{0f,15,-15,30,-30,60,-60,90,-90})
            for(float d=distance;d>1.5f;d-=.5f)
            {
                var dir=Quaternion.Euler(0,bearing+jitter,0)*Vector3.forward;var seed=target+dir*d;seed.y=.5f;
                if(c.Shore.Ground(seed,out var feet) && Mathf.Abs(feet.y-target.y)<1.5f && c.Shore.Relocate(feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(target-feet,Vector3.up))))
                {yield return null;yield break;}
            }
            Check(false,"Standing spot near "+target);
        }
        private static void Aim(FishingPrototype c,Vector3 eye,Vector3 target)=>c.viewCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
        private static IEnumerator Screen(string dir,string name){ScreenCapture.CaptureScreenshot(Path.Combine(dir,name+".png"));var f=Frames(6);while(f.MoveNext())yield return null;}

        private static IEnumerator CameraChecks(FishingPrototype c,FishingLifeWorld life,string dir)
        {
            var land=Land(c);while(land.MoveNext())yield return land.Current;
            var guide=c.Residents.Person(5);var head=guide.position+Vector3.up*1.35f;
            var stand=StandFacing(c,guide.position,4,180);while(stand.MoveNext())yield return stand.Current;
            bool firstBefore=c.FirstPerson;life.EnterViewfinder();yield return null;
            Check(life.Viewfinding && c.FirstPerson,"P enters the first-person viewfinder");
            var w=Wait(.6f);while(w.MoveNext())yield return null;
            var shot=Screen(dir,"life-viewfinder");while(shot.MoveNext())yield return null;
            int coins=c.Career.Coins;
            Aim(c,c.viewCamera.transform.position,head);
            // The porch window can sit between lens and guide; walk around until the guide is in clear view.
            foreach(var bearing in new[]{0f,45,-45,90,-90,135,-135})
            {
                if(life.Subject(c.viewCamera)==FishingPhotoCodex.Resident+5)break;
                life.ExitViewfinder();stand=StandFacing(c,guide.position,4,bearing);while(stand.MoveNext())yield return stand.Current;
                life.EnterViewfinder();yield return null;head=guide.position+Vector3.up*1.35f;Aim(c,c.viewCamera.transform.position,head);
            }
            Check(life.Subject(c.viewCamera)==FishingPhotoCodex.Resident+5,"Harbour guide is recognised in frame");
            string path=life.TakePhoto();
            Check(File.Exists(path) && new FileInfo(path).Length>2000 && new FileInfo(path).Length<=40*1024,"Photo saved as a small JPG ("+(File.Exists(path)?new FileInfo(path).Length:0)+" bytes)");
            Check(c.Career.Lore.PhotoFlag(FishingPhotoCodex.Resident+5) && c.Career.Coins==coins+FishingPhotoCodex.FirstReward,"First photo of a resident unlocks the codex illustration and pays once");
            File.Copy(path,Path.Combine(dir,"life-photo-resident.jpg"),true);
            // Occlusion: a wall between lens and subject blocks the unlock.
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);var eye=c.viewCamera.transform.position;
            wall.transform.position=Vector3.Lerp(eye,head,.5f);wall.transform.rotation=Quaternion.LookRotation(head-eye);wall.transform.localScale=new Vector3(2,2,.2f);Physics.SyncTransforms();
            Aim(c,eye,head);Check(life.Subject(c.viewCamera)!=FishingPhotoCodex.Resident+5,"A wall in front of the subject blocks recognition");
            UnityEngine.Object.Destroy(wall);yield return null;Physics.SyncTransforms();
            life.ExitViewfinder();yield return null;Check(!life.Viewfinding && c.FirstPerson==firstBefore,"Leaving the viewfinder restores the previous view");

            // Lighthouse from the harbour side.
            var lens=c.Home.LighthouseLens;var from=lens+new Vector3(30,-8,-30);
            Aim(c,from,lens);
            Check(life.Subject(c.viewCamera)==FishingPhotoCodex.Lighthouse,"Home lighthouse is recognised");
            life.TakePhoto();File.Copy(life.LastPhotoPath,Path.Combine(dir,"life-photo-lighthouse.jpg"),true);

            // Whale at sea.
            if(c.Ashore)c.Shore.BoardImmediately();
            c.ReturnToHarbor();yield return null;c.MoveToSpotForAutomation(0);yield return null;
            c.SeaEvents.Force(SeaEventKind.Whales);w=Wait(1);while(w.MoveNext())yield return null;
            var whale=c.SeaEvents.Whale.position;var boat=c.Player.transform.position+Vector3.up*2.5f;
            var toWhale=whale-boat;var camPos=whale-toWhale.normalized*Mathf.Min(40,toWhale.magnitude);camPos.y=Mathf.Max(camPos.y,2.5f);
            Aim(c,camPos,whale+Vector3.up);
            // Rocks may sit between; circle the whale at 30 m until the line of sight is open.
            for(int k=0;k<12 && life.Subject(c.viewCamera)!=FishingPhotoCodex.Whale;k++){camPos=whale+Quaternion.Euler(0,k*30,0)*Vector3.forward*30;camPos.y=4;Aim(c,camPos,whale+Vector3.up);}
            Check(life.Subject(c.viewCamera)==FishingPhotoCodex.Whale,"Whale is recognised");
            life.TakePhoto();Check(c.Career.Lore.PhotoFlag(FishingPhotoCodex.Whale),"Whale photo unlocks its codex illustration");
            Check(FishingPhotoCodex.Seen(c.Career,FishingPhotoCodex.Whale),"Codex shares the sea-events record (seen)");
            File.Copy(life.LastPhotoPath,Path.Combine(dir,"life-photo-whale.jpg"),true);
            c.SeaEvents.Schedule.Finish();yield return null;

            // Album rolls over at the limit.
            for(int i=0;i<FishingLifeWorld.AlbumLimit+3;i++)life.TakePhoto();
            var files=FishingLifeWorld.AlbumFiles(c.AlbumDirectory);
            long bytes=files.Sum(f=>new FileInfo(Path.Combine(c.AlbumDirectory,f.name)).Length);
            Check(files.Count==FishingLifeWorld.AlbumLimit && files[0].sequence>0,"Album keeps the newest "+FishingLifeWorld.AlbumLimit+" photos");
            Check(bytes<1024*1024,"Album stays under 1 MB ("+bytes/1024+" KB)");
            Check(life.Album.Count==FishingLifeWorld.AlbumLimit && life.Album.All(a=>a.tex!=null && a.tex.width==FishingLifeWorld.PhotoWidth),"Album textures load for the journal");

            // Journal page.
            var hud=c.GetComponent<FishingHUD>();hud.JournalPage=99;int albumPage=hud.JournalPage;
            if(!c.JournalOpen)c.ToggleJournal();hud.JournalPage=albumPage;var jf=Frames(40);while(jf.MoveNext())yield return null; // journal pauses game time
            shot=Screen(dir,"life-album");while(shot.MoveNext())yield return null;
            c.ToggleJournal();yield return null;
            Debug.Log("[V044-LIFE] album journal page index "+albumPage);
        }

        private static IEnumerator PalmChecks(FishingPrototype c,FishingLifeWorld life,string dir)
        {
            FishingLifeWorld.ForceVr=true;
            try
            {
                var eye=c.viewCamera.transform;var forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
                Vector3 hand()=>eye.position+forward*.32f+Vector3.down*.3f;
                life.DebugLeftHand=new Pose(hand(),Quaternion.LookRotation(forward,Vector3.up));
                var w=Wait(.5f);while(w.MoveNext())yield return null;
                Check(!life.PalmMapShown,"No chart while the palm faces sideways");
                // The view rig keeps nudging the camera, so the fake hand follows the eye every frame.
                float until=Time.time+.08f;while(Time.time<until){forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;life.DebugLeftHand=new Pose(hand(),PalmUpRotation(forward));yield return null;}
                Check(!life.PalmMapShown,"A brief flick does not pop the chart (hysteresis)");
                until=Time.time+.5f;while(Time.time<until){forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;life.DebugLeftHand=new Pose(hand(),PalmUpRotation(forward));yield return null;}
                Check(life.PalmMapShown && life.PalmMap.gameObject.activeSelf,"Turning the left palm up shows the chart");
                var p=life.PalmMap.position;var palm=life.DebugLeftHand.Value.position;Check(p.y>palm.y+.05f && (p-palm).magnitude<.12f,"Chart floats just above the palm");
                Check(life.PalmPlayerMarker.gameObject.activeSelf && Mathf.Abs(life.PalmPlayerMarker.localPosition.x)<=FishingLifeWorld.ChartWidth*.5f,"Player marker sits on the chart");
                // Close-up screenshot from the eye.
                var lookAt=life.PalmMap.position;
                eye.SetPositionAndRotation(lookAt+forward*-.28f+Vector3.up*.2f,Quaternion.LookRotation(lookAt-(lookAt+forward*-.28f+Vector3.up*.2f)));
                FishingV016Validation.Capture(c,dir,"life-palmmap");
                life.DebugLeftHand=new Pose(hand()+Vector3.down*.5f,Quaternion.LookRotation(forward,Vector3.up));
                w=Wait(.6f);while(w.MoveNext())yield return null;
                Check(!life.PalmMapShown && !life.PalmMap.gameObject.activeSelf,"Lowering the hand hides the chart");

                // Right hand raised to the eye: viewfinder frame and trigger photo.
                int taken=life.PhotosTaken;
                life.DebugRightHand=new Pose(eye.position+eye.forward*.1f,eye.rotation);yield return null;yield return null;
                Check(life.VrRaised,"Right hand at the eye raises the camera");
                life.DebugPressShutter();yield return null;yield return null;
                Check(life.PhotosTaken==taken+1,"VR trigger takes a photo");
                life.DebugRightHand=new Pose(eye.position+Vector3.down*.6f,eye.rotation);yield return null;yield return null;
                Check(!life.VrRaised,"Lowering the right hand puts the camera away");
            }
            finally{FishingLifeWorld.ForceVr=false;life.DebugLeftHand=null;life.DebugRightHand=null;}
        }

        private static IEnumerator FestivalChecks(FishingPrototype c,FishingLifeWorld life,string dir)
        {
            var cal=c.Career.Calendar;
            Check(life.PoleCount>=4,"At least four festival poles found clear ground ("+life.PoleCount+")");
            foreach(var feet in life.PoleFeet)Check(life.ClearSpot(feet,FishingLifeWorld.PoleCollider+.05f),"Festival pole does not intersect buildings or props at "+feet);
            Check(FishingFestival.On(cal.Day)<0 || life.FestivalShown>=0,"Decorations follow today's calendar");
            var land=Land(c);while(land.MoveNext())yield return land.Current;
            string[] names={"spring","summer","autumn","winter"};
            for(int k=0;k<4;k++)
            {
                int target=FishingFestival.NextDay(cal.Day);while(FishingFestival.On(target)!=k)target=FishingFestival.NextDay(target+3);
                while(cal.Day<target)cal.AdvanceToHour(cal.Hour<12?18:6);
                cal.AdvanceToHour(k==1 || k==3?20:11);yield return null;yield return null;
                Check(FishingFestival.On(cal.Day)==k && life.FestivalShown==k,"Festival "+FishingFestival.Name(k)+" decorates the plaza on day "+(cal.Day%28+1));
                var w=Wait(1);while(w.MoveNext())yield return null;
                c.Weather.RefreshForCapture();
                var eye=life.Plaza+life.PlazaOutward*13+Vector3.up*4.5f;
                // Pick a viewpoint with an open line of sight to the plaza (rocks and the shop surround it).
                for(int v=0;v<16;v++){var e=life.Plaza+Quaternion.Euler(0,v*22.5f,0)*Vector3.forward*12;e.y=.5f;if(!c.Shore.Ground(e,out var g) || g.y<life.Plaza.y-1.5f)continue;e=g+Vector3.up*2.2f;Aim(c,e,life.Plaza+Vector3.up*3);if(life.Subject(c.viewCamera)==FishingPhotoCodex.Festival){eye=e;break;}}
                Aim(c,eye,life.Plaza+Vector3.up*2.5f);FishingV016Validation.Capture(c,dir,"life-festival-"+names[k]);
                {var pf=life.PoleFeet[0];var e2=pf+new Vector3(2.5f,2.2f,2.5f);Aim(c,e2,pf+Vector3.up*2.4f);FishingV016Validation.Capture(c,dir,"life-festival-"+names[k]+"-pole");}
                Aim(c,eye,life.Plaza+Vector3.up*3);
                if(k==0){Check(life.Subject(c.viewCamera)==FishingPhotoCodex.Festival || c.Career.Lore.PhotoFlag(FishingPhotoCodex.Festival),"Festival lights are a codex subject");life.TakePhoto();}
            }
            cal.AdvanceToHour(cal.Hour<12?18:6);while(FishingFestival.On(cal.Day)>=0)cal.AdvanceToHour(cal.Hour<12?18:6);
            yield return null;
            Check(life.FestivalShown==-1,"Decorations come down after the festival");
        }
    }
}
