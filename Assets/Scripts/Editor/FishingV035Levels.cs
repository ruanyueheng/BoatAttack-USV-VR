using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BoatAttack.EditorTools
{
    public static class FishingV035Levels
    {
        public static void Run()=>FishingV011Validation.Begin("V035-LEVELS");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V035-LEVELS] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V035");
            for(int i=0;i<4;i++){var run=Island(c,i,Path.GetFullPath("Logs/V035"));while(run.MoveNext())yield return run.Current;}
        }
        public static IEnumerator Island(FishingPrototype c,int id,string evidence)
        {
            c.SelectFieldSlot(8);c.ReturnToHarbor();yield return null;
            var p=FishingIslandWorld.Berth(id)+Vector3.up;var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=p;rb.rotation=Quaternion.identity;c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
            var wait=Wait(.5f);while(wait.MoveNext())yield return null;
            Check(c.Shore.TryDisembark(),"Physical island landing "+id);yield return null;
            var npc=c.Residents.Person(id);var walk=Walk(c,npc.position+Vector3.forward*1.8f);while(walk.MoveNext())yield return null;
            Check(c.Residents.Talk(id),"Resident is reachable on the redesigned island "+id);c.Residents.Choose(1);c.Residents.Reveal();
            Check(c.Career.Lore.Met(id) && c.Residents.Speech.Contains(FishingLore.ShrineName(id)),"Resident names the corresponding sanctuary "+id);
            var before=c.Shore.Position;c.Shore.Move(Vector2.up,true,false,false,.05f);Check(c.Shore.Position==before,"Dialogue still owns movement "+id);c.Residents.Close();yield return null;yield return null;
            if(!c.FirstPerson)c.ToggleView();
            var root=c.Islands.WorldRoot.GetChild(id).Find(FishingV035Art.ArtRoot);Check(root!=null,"New island geometry is installed "+id);
            var route=root.Find("Island approach").Cast<Transform>().OrderBy(t=>int.Parse(t.name.Substring(6))).ToArray();
            for(int i=0;i<route.Length;i++)
            {
                walk=Walk(c,route[i].position);while(walk.MoveNext())yield return null;
                if(i==1){c.PresentCharacterFrame();c.viewCamera.transform.rotation=Quaternion.LookRotation(c.Realms.Portal(id).position+Vector3.up*3-c.viewCamera.transform.position);FishingV016Validation.Capture(c,evidence,"island-player-"+id);}
            }
            Check(Vector3.Distance(c.Shore.Position,c.Realms.PortalApproach(id))<.5f,"Authored approach reaches the actual entrance "+id);
            Check(c.Realms.TryTarget(out int target,out _) && target==id+8,"Entrance line of sight "+id);
            Check(c.RequestWorldInteraction() && c.Realms.Transitioning,"Production entry starts the fade "+id);before=c.Shore.Position;c.Shore.Move(Vector2.up,false,false,false,.05f);Check(c.Shore.Position==before,"Fade blocks walking "+id);
            while(c.Realms.Transitioning)yield return null;yield return null;yield return null;
            var room=c.Realms.Room;Check(c.Realms.Island==id && room.Find(FishingRealmWorld.GeometryRootName)!=null && c.Shore.Ground(c.Shore.Position,out _),"Supported authored interior entry "+id);
            FishingV036PortalValidation.CheckEntry(c,id);
            Check(room.Find("Echo gate")==null && room.Find("Floor")==null,"Old shared rectangular room and gate removed "+id);
            c.Weather.ApplyEnvironment();c.PresentCharacterFrame();var aim=id==0?new Vector3(-2,3,5):id==1?new Vector3(-5,3,2):id==2?new Vector3(0,.5f,3):new Vector3(0,4,10);
            c.viewCamera.transform.rotation=Quaternion.LookRotation(room.TransformPoint(aim)-c.viewCamera.transform.position);FishingV016Validation.Capture(c,evidence,"sanctuary-player-"+id);CaptureViews(c,id,evidence);
            var solve=Solve(c,id,evidence);while(solve.MoveNext())yield return solve.Current;
            Check(c.Realms.Trial.Ready,"Different physical puzzle completed "+id);int coins=c.Career.Coins;
            Check(c.RequestWorldInteraction() && c.Career.Lore.Cleared(id) && c.Career.Coins==coins+90,"First echo reward uses production interaction "+id);yield return null;
            Check(c.RequestWorldInteraction() && c.Career.Coins==coins+90,"Echo reward is never duplicated "+id);
            var back=ReturnRoute(c,id);while(back.MoveNext())yield return back.Current;
            FishingV036PortalValidation.CheckTerminal(c,id);
            Check(c.Realms.Exit(),"Return interaction at the exploration endpoint "+id);while(c.Realms.Transitioning)yield return null;yield return null;yield return null;
            Check(!c.Realms.Inside && Vector3.Distance(c.Shore.Position,c.Realms.PortalApproach(id))<.5f,"Restores island feet after leaving "+id);
            c.Weather.ApplyEnvironment();Check(Mathf.Abs(RenderSettings.skybox.GetFloat("_Daylight")-c.Weather.Daylight)<.001f,"Exterior lighting restored "+id);
        }
        private static IEnumerator Solve(FishingPrototype c,int id,string evidence)
        {
            var r=c.Realms;IEnumerator walk;
            if(id==0)
            {
                Check(!r.Room.Find("WindBridge0").gameObject.activeSelf,"Wind bridge starts absent");
                for(int i=1;i<=10;i++){walk=Walk(c,r.Landmark("Route"+i).position);while(walk.MoveNext())yield return null;if(i==4)Check(r.Trial.Progress==2,"Walking onto two pads extends two bridges");}
                Check(r.Trial.Progress==3 && r.Room.Find("WindBridge2").gameObject.activeSelf,"Footstep sequence completes the ascending island chain");
                walk=Walk(c,r.Device(3).position+Vector3.back*1.4f);while(walk.MoveNext())yield return null;
            }
            if(id==1)
            {
                Check(!r.Room.Find("RootBridge").GetComponentsInChildren<Collider>().Any(x=>x.enabled),"Unsolved roots cannot be walked across");
                walk=Walk(c,r.Landmark("Route1").position);while(walk.MoveNext())yield return null;
                for(int i=0;i<3;i++)
                {
                    var stance=r.Device(i).position+(i==1?Vector3.back:Vector3.left)*1.4f;walk=Walk(c,stance);while(walk.MoveNext())yield return null;
                    for(int turn=0;turn<=i;turn++){Check(c.RequestWorldInteraction(),"Rotate connected mirror "+i+" turn "+turn);var w=Wait(.5f);while(w.MoveNext())yield return null;}
                    walk=Walk(c,r.Device(i).position);while(walk.MoveNext())yield return null;
                }
                var growth=Wait(5.4f);while(growth.MoveNext())yield return null;
                Check(r.Trial.Growth>=1 && r.Room.Find("RootBridge").GetComponentsInChildren<Collider>().All(x=>x.enabled),"Connected beam grows the actual root crossing");
                for(int i=5;i<=6;i++){walk=Walk(c,r.Landmark("Route"+i).position);while(walk.MoveNext())yield return null;}
                walk=Walk(c,r.Device(3).position+Vector3.back*1.4f);while(walk.MoveNext())yield return null;
            }
            if(id==2)
            {
                Check(r.Room.Find("HoldHatch").gameObject.activeSelf && !r.Room.Find("FloatGangway").GetComponent<BoxCollider>().enabled,"Water blocks the lower hold and the final floating route");
                foreach(int i in new[]{1,2}){walk=Walk(c,r.Landmark("Route"+i).position);while(walk.MoveNext())yield return null;}
                walk=Walk(c,r.Device(1).position+Vector3.back*1.4f);while(walk.MoveNext())yield return null;
                Check(c.RequestWorldInteraction(),"Drain from the upper catwalk");var drain=Wait(4.3f);while(drain.MoveNext())yield return null;
                Check(r.Trial.WaterLevel<=.2f && !r.Room.Find("HoldHatch").gameObject.activeSelf,"Drained water opens the actual stair hatch");
                for(int i=4;i<=7;i++){walk=Walk(c,r.Landmark("Route"+i).position);while(walk.MoveNext())yield return null;}
                foreach(var p in new[]{new Vector3(2,-1.8f,-4),new Vector3(2,-1.8f,-7.5f),new Vector3(.5f,-1.8f,-7.5f)}){walk=Walk(c,r.Room.TransformPoint(new Vector3(-p.x,p.y,p.z)));while(walk.MoveNext())yield return null;}
                var previousSettings=InputSystem.settings;var settings=UnityEngine.Object.Instantiate(previousSettings);
                settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings=settings;var previousKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);
                try
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;
                    bool near=r.TryTarget(out int selected,out var cargoTarget);
                    Debug.Log("[V035-LEVELS] Push preflight near="+near+" selected="+selected+" water="+r.Trial.WaterLevel+" story="+c.StoryBlocksGameplay+" action="+c.WorldActionBlocksGameplay+" pose="+c.Shore.Position+" cargo="+r.Device(0).position);
                    if(!near)
                    {
                        var start=c.Shore.Position+Vector3.up*1.4f;var d=r.Device(0).position+Vector3.up*.45f-start;
                        foreach(var hit in Physics.RaycastAll(start,d.normalized,d.magnitude))Debug.Log("[V035-LEVELS] Cargo sight blocker "+hit.collider.name+" point="+hit.point+" parent="+hit.transform.parent?.name);
                    }
                    bool began=c.RequestWorldInteraction();Debug.Log("[V035-LEVELS] Push command="+began+" owner="+r.PushingCargo);
                    Check(began && r.PushingCargo,"Held E starts sustained physical cargo pushing");
                    float until=Time.time+18;
                    while(r.Trial.CargoProgress<1 && Time.time<until)
                    {
                        keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));
                        var delta=Vector3.ProjectOnPlane(r.Device(0).position+Vector3.back*1.4f-c.Shore.Position,Vector3.up);
                        if(delta.magnitude>.04f){c.viewCamera.transform.rotation=Quaternion.LookRotation(delta);c.Shore.Move(Vector2.up,false,false,false,Mathf.Min(.05f,delta.magnitude/2.6f));}
                        yield return null;
                    }
                    Debug.Log("[V035-LEVELS] Push final progress="+r.Trial.CargoProgress+" owner="+r.PushingCargo+" delta="+Vector3.Distance(r.Device(0).position,r.Landmark("CargoEnd").position)+" player="+c.Shore.Position+" cargo="+r.Device(0).position);
                    Check(r.Trial.CargoProgress==1 && Vector3.Distance(r.Device(0).position,r.Landmark("CargoEnd").position)<.1f,"Cargo travels continuously for seven seconds onto its plate");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Check(!r.PushingCargo,"Releasing E clears the push input owner");
                }
                finally{InputSystem.RemoveDevice(keyboard);InputSystem.settings=previousSettings;UnityEngine.Object.DestroyImmediate(settings);if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();}
                foreach(var p in new[]{new Vector3(0,-1.8f,-4),new Vector3(-2.2f,-1.8f,-4),new Vector3(-4.2f,1.8f,-9),new Vector3(-4.2f,1.8f,-12),new Vector3(4.2f,1.8f,-12),new Vector3(4.2f,1.8f,3.6f)}){walk=Walk(c,r.Room.TransformPoint(new Vector3(-p.x,p.y,p.z)));while(walk.MoveNext())yield return null;}
                Check(c.RequestWorldInteraction(),"Refill after loading the pressure plate");var fill=Wait(4.3f);while(fill.MoveNext())yield return null;
                Check(r.Trial.Ready && r.Room.Find("FloatGangway").GetComponent<BoxCollider>().enabled,"Refilled water raises a collidable gangway");
                Check(c.RequestWorldInteraction(),"Close the filled valve without draining the hold");
                yield return null;
                Check(r.Trial.WaterLevel>=.95f && r.Trial.Ready && r.Room.Find("FloatGangway").GetComponent<BoxCollider>().enabled,"A closed valve preserves the raised bridge and its walking collision");
                foreach(var p in new[]{new Vector3(4.2f,1.8f,7),new Vector3(0,1.8f,7),new Vector3(0,1.8f,16.4f)}){walk=Walk(c,r.Room.TransformPoint(new Vector3(-p.x,p.y,p.z)));while(walk.MoveNext())yield return null;}
                var waterCheck=FishingV038Validation.InspectHold(c);while(waterCheck.MoveNext())yield return waterCheck.Current;
            }
            if(id==3)
            {
                for(int i=1;i<=16;i++){walk=Walk(c,r.Landmark("Route"+i).position);while(walk.MoveNext())yield return null;}
                Check(c.Shore.Position.y-r.Room.position.y>3.4f,"Spiral stair reaches the raised telescope platform");
                Check(c.RequestWorldInteraction() && r.ObservingStars,"Production E takes the continuous telescope controls");
                var before=c.Shore.Position;c.Shore.Move(Vector2.up,false,false,false,.05f);Check(c.Shore.Position==before,"Scope owns movement while observing");
                float fov=c.viewCamera.fieldOfView;
                r.TickObservationInput(Keyboard.current,Mouse.current,false,false);Check(!r.ObservingStars && c.viewCamera.fieldOfView==fov,"Focus loss restores the camera and releases controls");
                yield return null;yield return null;Check(c.RequestWorldInteraction(),"Scope can be reopened after cancellation");
                for(int star=0;star<3;star++)
                {
                    r.AimTelescope(new Vector2(FishingShrineTrial.StarTargetYaw(star)-r.Trial.StarYaw,FishingShrineTrial.StarTargetPitch(star)-r.Trial.StarPitch),true,.1f);
                    for(int j=0;j<11;j++)r.AimTelescope(Vector2.zero,true,.1f);
                    c.PresentCharacterFrame();
                    foreach(var dot in r.Room.Cast<Transform>().Where(t=>t.name.StartsWith("Star "+star+" ",StringComparison.Ordinal)))
                    {
                        var ray=dot.position-c.viewCamera.transform.position;
                        Check(!Physics.RaycastAll(c.viewCamera.transform.position,ray.normalized,ray.magnitude).Any(h=>h.collider!=c.Shore.Body && !h.transform.IsChildOf(r.Device(0)) && !FishingShore.NonSolidSurface(h.collider)),"Constellation has an unobstructed optical sight line: "+dot.name);
                    }
                    FishingV016Validation.Capture(c,evidence,"telescope-observation-"+star);
                    for(int j=0;j<5 && r.Trial.StarStage==star;j++)r.AimTelescope(Vector2.zero,true,.1f);
                    Check(r.Trial.StarStage==star+1,"Continuous aim and stable focus records constellation "+star);yield return null;
                }
                r.CancelObservation();yield return null;yield return null;
                foreach(var p in new[]{new Vector3(2,3.6f,8),new Vector3(2,3.6f,12),new Vector3(-1,3.6f,11.6f)}){walk=Walk(c,r.Room.TransformPoint(new Vector3(-p.x,p.y,p.z)));while(walk.MoveNext())yield return null;}
            }
            c.PresentCharacterFrame();c.viewCamera.transform.rotation=Quaternion.LookRotation(r.Device(3).position+Vector3.up*(id==0?2.8f:1)-c.viewCamera.transform.position);FishingV016Validation.Capture(c,evidence,"sanctuary-complete-"+id);
            Check(r.TryTarget(out int target,out _) && target==3,"The final focal object is reachable "+id);
            var terminal=ReturnRoute(c,id);while(terminal.MoveNext())yield return terminal.Current;
            FishingV036PortalValidation.CheckTerminal(c,id);
            c.PresentCharacterFrame();c.viewCamera.transform.rotation=Quaternion.LookRotation(r.Device(4).position+Vector3.up*1.3f-c.viewCamera.transform.position);FishingV016Validation.Capture(c,evidence,"sanctuary-terminal-portal-"+id);
            if(id==0)foreach(var p in new[]{new Vector3(-3.3f,5,25.1f),new Vector3(-3.3f,5,21.4f)}){walk=Walk(c,r.Room.TransformPoint(p));while(walk.MoveNext())yield return null;}
            if(id==1){walk=Walk(c,r.Room.TransformPoint(new Vector3(-3.1f,0,19.3f)));while(walk.MoveNext())yield return null;}
            walk=Walk(c,r.Device(3).position+Vector3.back*1.4f);while(walk.MoveNext())yield return null;
            Check(r.TryTarget(out target,out _) && target==3,"Optional echo stays accessible after inspecting the exit "+id);
        }
        private static IEnumerator ReturnRoute(FishingPrototype c,int id)
        {
            var r=c.Realms;IEnumerator walk;
            if(id==0)foreach(var p in new[]{new Vector3(-3.3f,5,21.4f),new Vector3(-3.3f,5,25.1f)}){walk=Walk(c,r.Room.TransformPoint(p));while(walk.MoveNext())yield return null;}
            if(id==1){walk=Walk(c,r.Room.TransformPoint(new Vector3(-3.1f,0,19.3f)));while(walk.MoveNext())yield return null;}
            walk=Walk(c,r.Device(4).position+r.Device(4).forward*1.4f);while(walk.MoveNext())yield return null;
        }
        private static void CaptureViews(FishingPrototype c,int id,string directory)
        {
            var camera=c.viewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var room=c.Realms.Room;c.Shore.Character.SetOpticalView(true);
            try
            {
                var eyes=new[]{new Vector3(25,23,-31),id==0?new Vector3(0,7,25):id==1?new Vector3(6,2.2f,22):id==2?new Vector3(0,4,19):new Vector3(2,5.3f,13),id==0?new Vector3(1.8f,6.6f,20):id==1?new Vector3(-7.4f,1.6f,-7):id==2?new Vector3(3.4f,3,3):new Vector3(-2.6f,5.4f,7.5f)};
                var targets=new[]{new Vector3(0,1,4),new Vector3(0,1,-4),id==0?new Vector3(0,8.5f,23.5f):id==1?new Vector3(-6,1.6f,-5):id==2?new Vector3(4.2f,3,5):new Vector3(0,5.7f,10)};
                for(int i=0;i<eyes.Length;i++)
                {
                    var eye=eyes[i];var target=targets[i];eye.x=-eye.x;target.x=-target.x;
                    camera.transform.SetPositionAndRotation(room.TransformPoint(eye),Quaternion.LookRotation(room.TransformPoint(target)-room.TransformPoint(eye)));camera.fieldOfView=i==2?48:65;
                    FishingV016Validation.Capture(c,directory,"sanctuary-"+id+"-"+new[]{"overview","reverse","detail"}[i]);
                }
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;c.Shore.Character.SetOpticalView(false);}
        }
        public static IEnumerator Walk(FishingPrototype c,Vector3 target)
        {
            float stuck=0;Vector3 previous=c.Shore.Position;
            for(int i=0;i<1500 && FishingPrototype.FlatDistance(c.Shore.Position,target)>.14f;i++)
            {
                var delta=Vector3.ProjectOnPlane(target-c.Shore.Position,Vector3.up);c.viewCamera.transform.rotation=Quaternion.LookRotation(delta.normalized);
                c.Shore.Move(Vector2.up,false,false,false,Mathf.Min(.05f,delta.magnitude/2.6f));yield return null;
                stuck=Vector3.Distance(previous,c.Shore.Position)<.003f?stuck+Time.deltaTime:0;previous=c.Shore.Position;
                if(stuck>2)break;
            }
            bool reached=FishingPrototype.FlatDistance(c.Shore.Position,target)<.4f && Mathf.Abs(c.Shore.Position.y-target.y)<.45f;
            if(!reached)foreach(var collider in Physics.OverlapCapsule(c.Shore.Position+Vector3.up*.35f,c.Shore.Position+Vector3.up*1.4f,1.1f))Debug.Log("[V035-LEVELS] Nearby collision: "+collider.name+" parent="+collider.transform.parent?.name+" bounds="+collider.bounds);
            Check(reached,"Continuous supported walking to "+target+" actual="+c.Shore.Position);
        }
        private static IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
    }
}
