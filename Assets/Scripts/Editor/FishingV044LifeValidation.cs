using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace BoatAttack.EditorTools
{
    /// <summary>V044 生活玩法：相机拍照与相册、图鉴解锁、掌心地图手势显隐、节日轮转与装饰、存档往返与旧档兼容（Logs/V044/life）。</summary>
    public static class FishingV044LifeValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-LIFE");
        public static void RunCamera()=>FishingV011Validation.Begin("LIFE-CAMERA");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-LIFE] CHECK: "+label);}
        private static IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        public static IEnumerator CameraReviewChecks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Camera checks use an isolated profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            string dir=Path.GetFullPath("Logs/VRReview/Camera");Directory.CreateDirectory(dir);
            if(Directory.Exists(c.AlbumDirectory))Directory.Delete(c.AlbumDirectory,true);
            c.Life.RefreshAlbum();
            foreach(var run in new[]{CameraChecks(c,c.Life,dir),PalmChecks(c,c.Life,dir)})
            {try{while(run.MoveNext())yield return run.Current;}finally{(run as IDisposable)?.Dispose();}}
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Only the isolated automation profile is used");
            Rules();Saves();HeadingChecks();
            string dir=Path.GetFullPath("Logs/V044/life");Directory.CreateDirectory(dir);
            foreach(var old in Directory.GetFiles(dir,"*.png"))File.Delete(old);
            var life=c.Life;Check(life!=null,"Life world is built");
            Check(c.AlbumDirectory.StartsWith(Path.GetDirectoryName(Path.GetFullPath(c.SavePath))),"Album lives beside the isolated saves, outside the save JSON");
            if(Directory.Exists(c.AlbumDirectory))Directory.Delete(c.AlbumDirectory,true);
            life.RefreshAlbum();NotebookChecks(c,dir,false);
            var run=CameraChecks(c,life,dir);while(run.MoveNext())yield return run.Current;
            NotebookChecks(c,dir,true);
            run=PalmChecks(c,life,dir);while(run.MoveNext())yield return run.Current;
            run=FestivalChecks(c,life,dir);while(run.MoveNext())yield return run.Current;
            run=BinocularChecks(c,life,dir);while(run.MoveNext())yield return run.Current;
        }

        private static void NotebookChecks(FishingPrototype c,string dir,bool withPhotos)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var book=c.XRNotebook;var type=typeof(FishingXRNotebook);
            FieldInfo F(string name)=>type.GetField(name,flags);
            var refresh=type.GetMethod("Refresh",flags);var gestures=type.GetMethod("TickPageGestures",flags);
            var canvas=(Canvas)F("_canvas").GetValue(book);var rect=(RectTransform)canvas.transform;
            var actions=(Action[])F("_actions").GetValue(book);var icons=(RawImage[])F("_icons").GetValue(book);
            var preview=(RawImage)F("_albumPreview").GetValue(book);var status=(Text)F("_status").GetValue(book);
            var position=rect.position;var rotation=rect.rotation;
            bool Gesture(bool left,bool right,Pose l,Pose r,float now)=>(bool)gestures.Invoke(book,new object[]{left,right,l,r,now});
            try
            {
                F("_page").SetValue(book,FishingXRNotebook.AlbumPage);F("_offset").SetValue(book,0);refresh.Invoke(book,null);
                Check(actions[7]!=null && status.text.Contains("相册"),"XR album has a photo-codex link and readable content");
                if(!withPhotos)
                {
                    Check(actions.Take(6).All(a=>a==null) && actions[6]==null && actions[8]==null && !preview.gameObject.activeSelf,"Empty XR album has no stale photo or invalid paging action");
                    Check(status.text.Contains("22 厘米") && status.text.Contains("放下手收起") && status.preferredHeight<=status.rectTransform.rect.height,"Empty album explains the camera gesture and fits the reading leaf");
                    FishingXRPresentationValidation.CaptureCanvas(canvas,Path.Combine(dir,"life-xr-album-empty.png"));return;
                }
                var album=c.Life.Album;
                Check(icons.Take(6).All(i=>i.gameObject.activeSelf && i.texture!=null) && icons[0].texture==album[album.Count-1].tex,"XR album displays the six newest real saved photos");
                actions[1]();Check(preview.texture==album[album.Count-2].tex && preview.gameObject.activeSelf,"Selecting an XR photograph updates the large preview");
                Check(Mathf.Abs(icons[0].rectTransform.rect.width/icons[0].rectTransform.rect.height-FishingLifeWorld.PhotoWidth/(float)FishingLifeWorld.PhotoHeight)<.001f,"XR photo thumbnails preserve their original aspect ratio");
                Check(status.preferredHeight<=status.rectTransform.rect.height,"XR album caption fits its journal leaf");
                FishingXRPresentationValidation.CaptureCanvas(canvas,Path.Combine(dir,"life-xr-album.png"));
                actions[8]();Check((int)F("_offset").GetValue(book)==6 && icons[0].texture==album[album.Count-7].tex,"XR next page reaches the next six saved photos");
                actions[6]();Check((int)F("_offset").GetValue(book)==0 && actions[6]==null,"XR previous page returns to the newest photos and stops at zero");
                F("_offset").SetValue(book,999);refresh.Invoke(book,null);Check((int)F("_offset").GetValue(book)==30 && actions[8]==null,"XR album clamps stale offsets to its final page");
                F("_offset").SetValue(book,0);refresh.Invoke(book,null);book.ResetInput();
                var left=new Pose(rect.TransformPoint(new Vector3(rect.rect.xMin+10,0,0)),rect.rotation);
                var right=new Pose(rect.TransformPoint(new Vector3(200,0,0)),rect.rotation);
                Gesture(false,false,left,right,0);Gesture(false,true,left,right,.1f);
                var sweep=new Pose(rect.TransformPoint(new Vector3(-20,0,0)),rect.rotation);
                Check(Gesture(false,true,left,sweep,.3f) && (int)F("_offset").GetValue(book)==6,"A gripped right-to-left sweep advances one XR album page");
                Gesture(false,true,left,new Pose(rect.TransformPoint(new Vector3(-250,0,0)),rect.rotation),.4f);
                Check((int)F("_offset").GetValue(book)==6,"One grip cannot flip several pages without release");
                Gesture(false,false,left,right,.5f);Gesture(false,true,left,sweep,.6f);Gesture(false,true,left,right,.8f);
                Check((int)F("_offset").GetValue(book)==0,"A released-and-regripped left-to-right sweep returns one page");
                Gesture(false,false,left,right,1);Gesture(false,true,left,right,1.1f);Gesture(false,true,left,sweep,2);
                Check((int)F("_offset").GetValue(book)==0,"A slow hand movement is not mistaken for a page sweep");
                Gesture(false,false,left,right,2.1f);Gesture(true,false,left,right,2.2f);var before=rect.position;
                var moved=new Pose(left.position+Vector3.up*.12f,left.rotation);Gesture(true,false,moved,right,2.3f);
                Check((bool)F("_notebookHeld").GetValue(book) && Vector3.Distance(rect.position,before+Vector3.up*.12f)<.001f,"Gripping the left book edge carries the notebook with the hand");
                Gesture(false,false,moved,right,2.4f);Check(!(bool)F("_notebookHeld").GetValue(book),"Releasing the book edge leaves it in world space");
                book.ResetInput();Gesture(true,true,moved,right,2.5f);
                Check(!(bool)F("_notebookHeld").GetValue(book) && !(bool)F("_swiping").GetValue(book),"Tracking/input reset requires fresh grip releases before book gestures");
                F("_page").SetValue(book,FishingXRNotebook.PhotoCodexPage);F("_offset").SetValue(book,0);refresh.Invoke(book,null);
                var content=(string[])F("_pageContent").GetValue(book);
                Check(content[0].StartsWith("★ "+FishingPhotoCodex.Name(0)) && status.text.Contains("与桌面相册共用"),"XR photo codex reads the shared discoveries and photographed flags");
                Check(status.text.Contains("22 厘米") && status.text.Contains("换下猎枪 / 望远镜") && status.preferredHeight<=status.rectTransform.rect.height,"Photo codex explains raising, shutter, stowing and competing tools without clipping");
                FishingXRPresentationValidation.CaptureCanvas(canvas,Path.Combine(dir,"life-xr-photo-codex.png"));
                F("_offset").SetValue(book,12);refresh.Invoke(book,null);
                Check(actions[8]==null && actions[7]!=null && content[3].Contains(FishingPhotoCodex.Seen(c.Career,15)?FishingPhotoCodex.Name(15):"未见条目"),"XR codex reaches the final entry and retains a home action");
            }
            finally{rect.SetPositionAndRotation(position,rotation);F("_page").SetValue(book,0);F("_offset").SetValue(book,0);refresh.Invoke(book,null);book.Close();}
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
        private static void HeadingChecks()
        {
            var root=new GameObject("Isolated repeated title styling check",typeof(RectTransform),typeof(Canvas));
            try
            {
                var go=new GameObject("Heading",typeof(RectTransform),typeof(Text));go.transform.SetParent(root.transform,false);
                var text=go.GetComponent<Text>();text.text="回潮海";FishingUITheme.StyleHeading(text);
                int dirty=0;text.RegisterDirtyVerticesCallback(()=>dirty++);
                for(int i=0;i<10;i++)FishingUITheme.StyleHeading(text);
                Check(dirty==0,"Repeated identical heading styling never dirties the text mesh");
                FishingUITheme.StyleHeading(text,1);
                Check(dirty>0 && text.color==FishingUITheme.TitleFill(1) && text.GetComponent<Outline>().effectColor==FishingUITheme.Gold,"Changing heading theme still updates the authored ink and gilt outline");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
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
            var forged=JsonUtility.FromJson<FishingSaveData>(json);forged.version=19;forged.homeStorage=forged.homeStorage.Take(FishingBackpack.SlotCount).ToList();Check(!FishingCareer.TryRestore(forged,out _),"Pre-v20 saves cannot carry a photo log");
            var legacy=new FishingCareer().Snapshot();legacy.version=19;legacy.coins=55;
            legacy.homeStorage=legacy.homeStorage.Take(FishingBackpack.SlotCount).ToList();
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
            int takenBefore=life.PhotosTaken;string lastPath=life.LastPhotoPath;int lastEntry=life.LastEntry;
            Directory.CreateDirectory(Path.GetDirectoryName(c.AlbumDirectory));
            File.WriteAllText(c.AlbumDirectory,"Isolated unavailable album directory");
            try
            {
                Check(life.TakePhoto()=="" && life.PhotosTaken==takenBefore && life.LastPhotoPath==lastPath && life.LastEntry==lastEntry
                    && !c.Career.Lore.PhotoFlag(FishingPhotoCodex.Resident+5) && c.Career.Coins==coins,
                    "Failed photo storage preserves photo count, last photo, codex and coins");
            }
            finally{File.Delete(c.AlbumDirectory);}
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
            string blocked=Path.Combine(c.AlbumDirectory,FishingPhotoCodex.FileName(files.Last().sequence+1,c.Career.Calendar.Day,life.Subject(c.viewCamera)));
            Directory.CreateDirectory(blocked);
            try
            {
                Check(life.TakePhoto()=="" && files.All(f=>File.Exists(Path.Combine(c.AlbumDirectory,f.name))),
                    "A full album retains every existing photo if writing the replacement fails");
            }
            finally{Directory.Delete(blocked);}
            Check(bytes<1024*1024,"Album stays under 1 MB ("+bytes/1024+" KB)");
            Check(life.Album.Count==FishingLifeWorld.AlbumLimit && life.Album.All(a=>a.tex!=null && a.tex.width==FishingLifeWorld.PhotoWidth),"Album textures load for the journal");

            // Journal page.
            var hud=c.GetComponent<FishingHUD>();hud.JournalPage=99;int albumPage=hud.JournalPage;
            if(!c.JournalOpen)c.ToggleJournal();hud.JournalPage=albumPage;var jf=Frames(40);while(jf.MoveNext())yield return null; // journal pauses game time
            shot=Screen(dir,"life-album");while(shot.MoveNext())yield return null;
            c.ToggleJournal();yield return null;
            Debug.Log("[V044-LIFE] album journal page index "+albumPage);
        }

        public static IEnumerator BinocularChecks(FishingPrototype c,FishingLifeWorld life,string dir)
        {
            var land=Land(c);while(land.MoveNext())yield return land.Current;
            var snapshot=c.Career.Snapshot();var eye=c.viewCamera.transform;var position=eye.position;var rotation=eye.rotation;
            float fov=c.viewCamera.fieldOfView,zoom=c.View.BinocularMagnification;
            GameObject target=null;Material material=null;Mesh mesh=null;
            Vector3 lp=Vector3.zero,rp=Vector3.zero;
            try
            {
                Check(c.Career.Backpack.TryAdd(FieldItem.Binoculars,1),"Isolated profile owns binoculars for the XR optics regression");
                var bag=c.Career.Backpack;int slot=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==FieldItem.Binoculars);
                if(slot>=FishingBackpack.HotbarCount){Check(bag.Move(slot,1),"Binoculars enter the selectable hotbar");slot=1;}
                c.SelectFieldSlot(slot);c.PresentCharacterFrame();
                FishingLifeWorld.ForceVr=true;life.DebugRightHand=new Pose(eye.position+eye.forward*.1f,eye.rotation);
                int taken=life.PhotosTaken;
                var cameraTick=typeof(FishingLifeWorld).GetMethod("UpdateVrCamera",BindingFlags.Instance|BindingFlags.NonPublic);
                cameraTick.Invoke(life,new object[]{true});life.DebugPressShutter();cameraTick.Invoke(life,new object[]{true});
                Check(!life.VrRaised && life.PhotosTaken==taken,"Binocular equipment prevents the near-eye film-camera gesture and shutter from taking over");
                eye.SetPositionAndRotation(new Vector3(0,300,0),Quaternion.identity);c.viewCamera.fieldOfView=69;
                var projection=c.viewCamera.projectionMatrix;
                c.View.AdjustMagnification(-100);
                var optics=life.Binoculars;Check(optics!=null,"The real life world builds the physical XR binocular optics component");
                var tool=c.FieldWorld.HeldTool;
                float scale=FishingFieldWorld.TrackedBinocularScale;
                var rightBase=Quaternion.Euler((Vector3)typeof(FishingHands).GetField("_rightControllerPalmEuler",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c.Hands));
                var leftBase=Quaternion.Euler((Vector3)typeof(FishingHands).GetField("_leftControllerPalmEuler",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c.Hands));
                void Hold(float distance,Quaternion turn,Vector3 offset=default)
                {
                    var q=eye.rotation*turn;
                    var root=eye.position+eye.forward*distance+offset-q*(Vector3.forward*FishingXRBinoculars.RearLensZ*scale);
                    lp=root+q*(FishingFieldWorld.TrackedBinocularLeftPalm*scale);rp=root+q*(FishingFieldWorld.TrackedBinocularRightPalm*scale);
                    Check(c.FieldWorld.ApplyTrackedBinocularPose(new Pose(lp,q*leftBase),new Pose(rp,q*rightBase),true,true),"Both real palm poses fit the binocular body");
                    optics.PresentForValidation(true,lp,rp);
                }
                Hold(.32f,Quaternion.identity);
                Check(!optics.Active,"Two hands holding binoculars at arm's length cannot open an optical overlay");
                Hold(.08f,Quaternion.identity,Vector3.down*.18f);
                Check(!optics.Active,"A lowered instrument cannot magnify the head view");
                Hold(.08f,Quaternion.identity);
                Check(optics.Active && optics.OpticalWindow.gameObject.activeSelf && optics.OpticalCamera.enabled
                    && optics.OpticalTexture.width==FishingXRBinoculars.Width && optics.OpticalTexture.height==FishingXRBinoculars.Height
                    && optics.OpticalCamera.stereoTargetEye==UnityEngine.StereoTargetEyeMask.None,
                    "Both grips raised to the eyes activate the physical optical camera without a use-button toggle");
                Check(optics.OpticalWindow.parent==tool && Vector3.Distance(optics.LeftEyepiece.position,tool.TransformPoint(FishingXRBinoculars.EyepieceLocal(true,optics.LensSeparation)))<.001f
                    && Vector3.Distance(optics.RightEyepiece.position,tool.TransformPoint(FishingXRBinoculars.EyepieceLocal(false,optics.LensSeparation)))<.001f
                    && Mathf.Abs(Vector3.Distance(optics.LeftEyepiece.position,optics.RightEyepiece.position)-FishingXRBinoculars.EyeSeparation(c.viewCamera))<.001f,
                    "Both rear eyecups follow the headset IPD and remain physically attached to the instrument");
                Check(Mathf.Abs(FishingXRBinoculars.CalibratedEyeSeparation(Vector3.left*.036f,Vector3.right*.036f)-.072f)<.0001f
                    && FishingXRBinoculars.CalibratedEyeSeparation(Vector3.zero,Vector3.zero)==FishingXRBinoculars.DefaultEyeSeparation,
                    "A 72mm wearer gets 72mm eyecups; missing eye tracking uses a safe 64mm fallback");
                var body=tool.GetComponent<MeshFilter>().sharedMesh.bounds.size*scale;
                Check(body.z>.16f && body.x>.17f && optics.LeftEyepiece.lossyScale.x>.05f,
                    "The XR binocular body is full size and both viewing apertures are over 50mm wide");
                Check(c.FieldWorld.HeldToolRenderer.enabled,"The real binocular body remains visible while looking through its rear lenses");
                Check((optics.OpticalCamera.cullingMask&(1<<5))==0 && !optics.OpticalCamera.GetUniversalAdditionalCameraData().allowXRRendering,
                    "The objective camera excludes its own eyepieces and cannot recursively render either HMD eye");
                optics.PresentForValidation(true,lp,rp,false,true);
                Check(!optics.Active && !optics.OpticalCamera.enabled,"Releasing either grip closes the lenses immediately");
                optics.PresentForValidation(true,lp,rp,true,false);
                Check(!optics.Active,"The right grip is required as well as the left");
                Hold(.08f,Quaternion.Euler(0,45,0));
                Check(!optics.Active,"Barrels pointed away from the eyes do not activate");
                Hold(.08f,Quaternion.Euler(0,0,90));
                Check(!optics.Active,"Vertically stacked barrels cannot pretend to align with both eyes");
                Hold(.11f,Quaternion.Euler(15,20,10),Vector3.right*.025f+Vector3.down*.03f);
                Check(c.FieldWorld.ApplyTrackedBinocularPose(new Pose(lp,eye.rotation*Quaternion.Euler(15,20,10)*leftBase),
                    new Pose(rp,eye.rotation*Quaternion.Euler(15,20,10)*rightBase),true,true,true),"A raised two-handed grip can request trigger alignment");
                optics.PresentForValidation(true,lp,rp);
                var rear=(optics.LeftEyepiece.position+optics.RightEyepiece.position)*.5f;
                Check(optics.Active && c.FieldWorld.BinocularEyeAlignment && Quaternion.Angle(tool.rotation,eye.rotation)<.01f
                    && Vector3.Distance(rear,FishingXRBinoculars.EyeCentre(c.viewCamera)+eye.forward*FishingXRBinoculars.ComfortEyeDistance)<.001f,
                    "Holding a trigger near the face seats both large eyecups at the headset's real eye positions");
                Check(!FishingXRBinoculars.CanAlignAtEyes(new Pose(eye.position,eye.rotation),lp+Vector3.down*.3f,rp+Vector3.down*.3f)
                    && !FishingXRBinoculars.CanAlignAtEyes(new Pose(eye.position,eye.rotation),lp+Vector3.forward*.5f,rp+Vector3.forward*.5f),
                    "Trigger assistance rejects lowered hands and an instrument held at arm's length");
                Check(c.viewCamera.projectionMatrix==projection,"Trigger alignment preserves the HMD stereo projection");
                Hold(.08f,Quaternion.identity);
                target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Binocular real-world scale target";
                target.transform.position=eye.position+eye.forward*12;target.transform.localScale=new Vector3(.5f,.5f,.05f);
                mesh=UnityEngine.Object.Instantiate(target.GetComponent<MeshFilter>().sharedMesh);mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();target.GetComponent<MeshFilter>().sharedMesh=mesh;
                material=new Material(c.PaletteShader);material.SetColor("_BaseColor",Color.magenta);material.SetFloat("_Unlit",1);target.GetComponent<Renderer>().sharedMaterial=material;
                optics.OpticalCamera.Render();int small=OpticalTargetPixels(optics.OpticalTexture);
                float lowFov=optics.OpticalCamera.fieldOfView;
                for(int i=0;i<30;i++)optics.ReadZoomAxis(Vector2.up,10+i*.31f);
                optics.OpticalCamera.Render();int large=OpticalTargetPixels(optics.OpticalTexture);
                Check(c.View.BinocularMagnification==8 && optics.OpticalCamera.fieldOfView<lowFov*.3f
                    && small>100 && large>small*8,"2x to 8x enlarges a real distant target through the physical eyepieces");
                Check(Mathf.Abs(c.viewCamera.fieldOfView-69)<.001f && c.viewCamera.projectionMatrix==projection,
                    "Physical binocular magnification leaves the main/HMD projection untouched");
                FishingV016Validation.Capture(c,dir,"life-xr-binocular-physical-body");
                eye.position-=eye.right*.033f;
                FishingV016Validation.Capture(c,dir,"life-xr-binocular-through-left-eye");
                eye.position+=eye.right*.033f;
                Hold(.08f,Quaternion.Euler(0,12,0));
                Check(optics.Active && Vector3.Dot(optics.OpticalCamera.transform.forward,tool.forward)>.999f
                    && Vector3.Dot(optics.OpticalCamera.transform.forward,eye.forward)<.99f,
                    "Distant scenery follows the instrument's objective direction rather than a head-locked window");
                // Render the actual shore and the source-skinned tracked hands, not only the high-altitude colour target.
                c.PresentCharacterFrame();c.viewCamera.fieldOfView=95;
                Hold(.08f,Quaternion.identity);
                var character=c.Shore.Character;
                character.ApplyHandTargets(c.Hands.Left,c.Hands.Right,true);
                Check(c.Hands.Left.HasGripSurface && c.Hands.Right.HasGripSurface
                    && Vector3.Distance(character.PalmPosition(true),lp)<.035f
                    && Vector3.Distance(character.PalmPosition(false),rp)<.035f,
                    "Both real source-skinned palms hold the instrument at their physical controller positions");
                var skin=character.FirstPersonSkin;var oldMesh=skin.sharedMesh;bool oldRecalc=skin.forceMatrixRecalculationPerRender,oldEnabled=skin.enabled,oldOff=skin.forceRenderingOff;
                skin.sharedMesh=character.TrackedHandsMesh;skin.forceMatrixRecalculationPerRender=true;skin.enabled=true;skin.forceRenderingOff=false;
                var baked=new Mesh();
                try
                {
                    skin.BakeMesh(baked,false);var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                    var visible=new System.Collections.Generic.HashSet<int>(skin.sharedMesh.triangles);
                    foreach(bool left in new[]{true,false})
                    {
                        var wrist=left?character.LeftWrist:character.RightWrist;var hand=left?c.Hands.Left:c.Hands.Right;
                        var a=hand.GripStart;var ab=hand.GripEnd-a;float nearest=float.MaxValue;int samples=0;
                        foreach(int index in visible)
                        {
                            var weight=weights[index];
                            if(!((weight.weight0>.4f && bones[weight.boneIndex0].IsChildOf(wrist))
                                ||(weight.weight1>.4f && bones[weight.boneIndex1].IsChildOf(wrist))))continue;
                            var point=skin.transform.TransformPoint(vertices[index]);samples++;
                            nearest=Mathf.Min(nearest,Vector3.Distance(point,a+ab*Mathf.Clamp01(Vector3.Dot(point-a,ab)/ab.sqrMagnitude)));
                        }
                        Check(samples>20 && nearest<hand.GripRadius+.02f,"Visible tracked "+(left?"left":"right")+" hand skin contacts its physical barrel");
                    }
                    optics.OpticalCamera.Render();
                    FishingV016Validation.Capture(c,dir,"life-xr-binocular-real-shore-grip");
                    var centre=eye.position;eye.position-=eye.right*.033f;
                    FishingV016Validation.Capture(c,dir,"life-xr-binocular-real-shore-left-eye");eye.position=centre;
                }
                finally{UnityEngine.Object.Destroy(baked);skin.sharedMesh=oldMesh;skin.forceMatrixRecalculationPerRender=oldRecalc;skin.enabled=oldEnabled;skin.forceRenderingOff=oldOff;}
                Hold(.08f,Quaternion.identity);
                optics.PresentForValidation(false,lp,rp);
                Check(!optics.Active && !optics.OpticalCamera.enabled && !optics.OpticalWindow.gameObject.activeSelf,
                    "Tracking loss immediately hides the lenses and stops the objective camera");
                optics.PresentForValidation(true,lp,rp);c.TogglePause();optics.PresentForValidation(true,lp,rp);
                Check(!optics.Active && !optics.OpticalCamera.enabled,"Pause cannot leave physical binocular rendering active");c.TogglePause();
                c.SelectFieldSlot(0);optics.PresentForValidation(true,lp,rp);
                Check(!optics.Active && !optics.OpticalWindow.gameObject.activeSelf,"Switching equipment clears the rear-lens images");
            }
            finally
            {
                FishingLifeWorld.ForceVr=false;life.DebugRightHand=null;life.Binoculars.PresentForValidation(false,lp,rp);
                if(c.Paused)c.TogglePause();c.RestoreShopTestProfile(snapshot);c.View.AdjustMagnification(-100);c.View.AdjustMagnification((zoom-2)*2);
                eye.SetPositionAndRotation(position,rotation);c.viewCamera.fieldOfView=fov;
                if(target!=null)UnityEngine.Object.Destroy(target);if(material!=null)UnityEngine.Object.Destroy(material);if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            }
        }
        private static int OpticalTargetPixels(RenderTexture rt)
        {
            var active=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                return image.GetPixels().Count(p=>p.r>.5f && p.b>.5f && p.g<.2f);
            }
            finally{RenderTexture.active=active;UnityEngine.Object.Destroy(image);}
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
                var chart=life.PalmMap.Find("Palm chart sheet").GetComponent<MeshRenderer>().sharedMaterial;
                foreach(float daylight in new[]{1f,0f})
                {
                    FishingUITheme.Tick(daylight,10);
                    typeof(FishingLifeWorld).GetMethod("UpdatePalmMap",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(life,null);
                    var water=chart.GetColor("_Water");var landColor=chart.GetColor("_Land");
                    Check(landColor.grayscale>water.grayscale+.35f && chart.GetColor("_Unknown").a>.99f,"Palm chart keeps land/water contrast and opaque unknown cells at "+(daylight>0?"day":"night"));
                }
                FishingUITheme.Tick(c.Career.Calendar.Daylight,10);
                // Close-up screenshot from the eye.
                var lookAt=life.PalmMap.position;
                eye.SetPositionAndRotation(lookAt+forward*-.28f+Vector3.up*.2f,Quaternion.LookRotation(lookAt-(lookAt+forward*-.28f+Vector3.up*.2f)));
                FishingV016Validation.Capture(c,dir,"life-palmmap");
                life.DebugLeftHand=new Pose(hand()+Vector3.down*.5f,Quaternion.LookRotation(forward,Vector3.up));
                w=Wait(.6f);while(w.MoveNext())yield return null;
                Check(!life.PalmMapShown && !life.PalmMap.gameObject.activeSelf,"Lowering the hand hides the chart");

                // Right hand raised to the eye: viewfinder frame and trigger photo.
                int taken=life.PhotosTaken;
                life.DebugRightHand=new Pose(eye.position+eye.forward*.1f,eye.rotation);
                Check(!life.VrRaised && life.OwnsVrTrigger,"A newly raised camera owns this frame's trigger before LateUpdate");
                typeof(FishingXRNotebook).GetMethod("UseTrackedTool",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.XRNotebook,null);
                Check(!c.JournalOpen && !c.XRNotebook.IsOpen,"Raising and pressing in the same frame cannot open the equipped guide");
                yield return null;yield return null;
                Check(life.VrRaised,"Right hand at the eye raises the camera");
                c.PresentCharacterFrame();
                Check(c.Shore.Character.FirstPersonSkin.forceRenderingOff && !c.FieldWorld.HeldTool.gameObject.activeSelf,
                    "Near-eye camera view hides local hands and the equipped tool without moving the tracked palms");
                Check(FishingXRHints.Current(c)==FishingXRHints.Context.Camera && FishingXRHints.Guidance(c).Contains("右扳机拍照"),"VR raised camera selects its own controller context and next-action guidance");
                typeof(FishingXRNotebook).GetMethod("UseTrackedTool",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.XRNotebook,null);
                Check(!c.JournalOpen && !c.XRNotebook.IsOpen,"A raised shore camera owns the trigger without opening the equipped field guide");
                var cameraTick=typeof(FishingLifeWorld).GetMethod("UpdateVrCamera",BindingFlags.Instance|BindingFlags.NonPublic);
                life.DebugPressShutter();cameraTick.Invoke(life,new object[]{false});
                Check(!life.VrRaised && life.PhotosTaken==taken,"Focus loss stows the camera and suppresses the shutter");
                life.DebugPressShutter();cameraTick.Invoke(life,new object[]{true});
                Check(life.VrRaised && life.PhotosTaken==taken,"A held trigger on focus recovery cannot take a photo");
                var pause=typeof(FishingLifeWorld).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic);
                pause.Invoke(life,new object[]{true});cameraTick.Invoke(life,new object[]{true});
                life.DebugPressShutter();cameraTick.Invoke(life,new object[]{true});
                Check(!life.VrRaised && !life.OwnsVrTrigger && life.PhotosTaken==taken,"Application pause blocks camera input even while focus remains true");
                typeof(FishingXRNotebook).GetMethod("UseTrackedTool",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.XRNotebook,null);
                Check(!c.JournalOpen && !c.XRNotebook.IsOpen,"A suspended camera cannot leak the shutter into the equipped field guide");
                pause.Invoke(life,new object[]{false});life.DebugPressShutter();cameraTick.Invoke(life,new object[]{true});
                Check(life.VrRaised && life.PhotosTaken==taken,"Resuming the application cannot fire a held shutter");
                cameraTick.Invoke(life,new object[]{true});
                life.DebugPressShutter();yield return null;yield return null;
                Check(life.PhotosTaken==taken+1,"VR trigger takes a photo");
                var landing=Land(c);while(landing.MoveNext())yield return landing.Current;
                var bag=c.Career.Backpack;int previous=bag.Selected;
                foreach(var item in new[]{FieldItem.HuntingRifle,FieldItem.Binoculars})
                {
                    if(bag.Count(item)==0)Check(bag.TryAdd(item,1),"Camera exclusion fixture owns "+item);
                    int slot=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==item);
                    if(slot>=FishingBackpack.HotbarCount){Check(bag.Move(slot,previous),"Camera exclusion fixture equips "+item);slot=previous;}
                    c.SelectFieldSlot(slot);life.DebugRightHand=new Pose(eye.position+eye.forward*.1f,eye.rotation);
                    life.DebugPressShutter();yield return null;yield return null;
                    Check(!life.VrRaised && life.PhotosTaken==taken+1,"Raising "+item+" cannot open or fire the camera");
                    if(item==FieldItem.Binoculars)
                    {
                        string guidance=FishingXRHints.Guidance(c);
                        Check(FishingXRHints.Current(c)==FishingXRHints.Context.Binocular && guidance.Contains("双手")
                            && guidance.Contains("握") && guidance.Contains("22 米") && guidance.Contains("A 记录"),
                            "Selected binoculars explain both physical grips, the real range and the record button");
                        if(UnityEngine.XR.XRSettings.isDeviceActive)
                        {
                            c.UseFieldItem();
                            Check(!c.BinocularsRaised,"An XR use-button press cannot bypass the physical binocular grip gate");
                        }
                        life.Binoculars.PresentForValidation(false,Vector3.zero,Vector3.zero);
                        Check(!life.Binoculars.Active,"Selected binoculars without tracked palms cannot activate physical optics");
                    }
                }
                c.SelectFieldSlot(previous);
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
