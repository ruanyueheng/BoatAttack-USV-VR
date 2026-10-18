using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV029Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V029");
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V029] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var snapshot=c.Career.Snapshot();string evidence=Path.GetFullPath("Logs/v029-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            try
            {
                c.ReturnToHarbor();c.MoveToSpotForAutomation(0);c.Interact();yield return null;yield return null;
                Check(c.Anchored,"Actual interact anchors at a fishing ground");
                var previousSettings=InputSystem.settings;var settings=Object.Instantiate(previousSettings);
                settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings=settings;
                var mouse=InputSystem.AddDevice<Mouse>();InputSystem.EnableDevice(mouse);
                var probe=c.gameObject.AddComponent<FishingV029MouseProbe>();probe.Controller=c;probe.Mouse=mouse;
                try
                {
                InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
                for(int i=0;i<10 && !probe.Pressed;i++)yield return null;
                Check(probe.Pressed && c.CastMotion.Preparing,"Actual Input System mouse press begins preparation in the runtime Update loop");
                for(int i=0;i<12;i++){c.XRNotebook.Tick(false);yield return null;}
                Check(c.CastMotion.Preparing && c.CastMotion.Charge>0,"Desktop closed XR notebook cannot cancel held mouse cast preparation across frames");
                InputSystem.QueueStateEvent(mouse,new MouseState());for(int i=0;i<10 && !probe.Released;i++)yield return null;
                Check(probe.Released && !c.CastMotion.Preparing && c.CastMotion.Casting && c.Session.Phase==CatchPhase.Waiting,"Actual Input System mouse release launches the production cast and enters Waiting");
                }
                finally{Object.DestroyImmediate(probe);InputSystem.RemoveDevice(mouse);InputSystem.settings=previousSettings;Object.DestroyImmediate(settings);}
                while(c.CastMotion.Casting)yield return null;
                c.Session.Cancel();c.ReturnToHarbor();yield return null;
                c.View.AdjustMagnification(100);Check(c.View.BinocularMagnification==8,"Binocular upper clamp is 8x");
                c.View.AdjustMagnification(-100);c.View.AdjustMagnification(float.NaN);Check(c.View.BinocularMagnification==2,"Binocular lower clamp and invalid input stay finite");
                foreach(string name in new[]{"Rabbit","Turtle","Fox","Seagull","GrilledFish"})
                {
                    var mesh=FishingV029Art.Animal(name);
                    Check(mesh!=null && mesh.vertexCount>50 && mesh.colors.Length==mesh.vertexCount,"New supplied/modelled runtime mesh and colours: "+name);
                    Check(Resources.Load<Texture2D>("FishingV029/"+name+"Thumbnail")!=null,"Real model thumbnail: "+name);
                }
                var roast=FishingV029Art.Animal("GrilledFish").bounds.size;
                Check(roast.y>.16f && roast.y/roast.x>.22f,"Whole grilled fish has a thick 3D body and closed rounded sides");
                for(int zone=0;zone<FishingGrounds.Count;zone++)Check(FishingEcology.HabitatSpecies(zone).Length>=5,"Distinct habitat roster has at least five species at ground "+zone);
                foreach(var station in c.Career.Workshop.Stations)Debug.Log("[V029] STATION "+station.id+" "+station.kind+" "+station.permanent+" "+new Vector3(station.x,station.y,station.z));
                var landing=c.Shore.Sites[0].Landing;
                foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})
                {
                    var station=c.Career.Workshop.Stations.FirstOrDefault(s=>s.kind==kind && s.permanent && FishingPrototype.FlatDistance(new Vector3(s.x,s.y,s.z),landing)<45);
                    Check(station!=null && c.WorkshopWorld.StationRoot(station.id)!=null,"West bay has actual spawned public "+kind);
                }
                c.WorkshopWorld.RefreshFromCareer();yield return null;
                Check(c.Career.Workshop.Stations.Count(s=>s.permanent)==6,"Reload/repair does not duplicate the six fixed facilities");
                var legacy=c.Career.Snapshot();legacy.workshop.stations.RemoveAll(s=>s.permanent && s.kind!=CraftStation.Forge);legacy.workshop.initialized=true;
                c.RestoreShopTestProfile(legacy);yield return null;yield return null;
                Check(c.Career.Workshop.Stations.Count(s=>s.permanent)==6,"Legacy initialized forge-only save repairs all missing facilities without reset");
                var rb=c.Player.engine.RB;rb.position=c.Shore.Sites[0].Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
                Check(c.Shore.TryDisembark(),"West bay shoreline is reachable");yield return null;yield return null;
                foreach(var kind in new[]{CraftStation.Workbench,CraftStation.Furnace,CraftStation.Forge})
                {
                    var station=c.Career.Workshop.Stations.First(s=>s.kind==kind && s.permanent && FishingPrototype.FlatDistance(new Vector3(s.x,s.y,s.z),landing)<45);
                    Check(c.Shore.Relocate(new Vector3(station.x,station.y,station.z)+Vector3.back*1.25f,Quaternion.identity),"Dry front apron for west "+kind);
                    c.View.ResetLook();yield return null;yield return null;
                    bool reachable=c.WorkshopWorld.ReachStation(station.id);string hint=c.WorldInteractionTitle;bool opened=c.RequestWorldInteraction();
                    Debug.Log("[V029] WEST "+kind+" reachable="+reachable+" hint="+hint+" opened="+opened+" inventory="+c.InventoryOpen+" context="+c.WorkshopWorld.Context+" actor="+c.Shore.Position+" station="+new Vector3(station.x,station.y,station.z));
                    Check(reachable && opened && c.InventoryOpen && c.WorkshopWorld.Context==kind,"Actual E opens west "+kind);
                    c.ToggleInventory();yield return null;yield return null;Capture(c,evidence,"west-"+kind);
                }
                foreach(FieldItem item in Enum.GetValues(typeof(FieldItem)))
                {
                    if(item==FieldItem.Empty)continue;
                    var mesh=FishingWorkshopArt.Make(item);Check(mesh!=null && mesh.vertexCount>0,"Every inventory item has a real inspectable mesh: "+item);Object.Destroy(mesh);
                }
                var bag=c.Career.Backpack;
                foreach(FieldItem item in Enum.GetValues(typeof(FieldItem)))
                {
                    if(item==FieldItem.Empty)continue;
                    if(item!=FieldItem.FieldGuide)Check(bag.TryAdd(item,1),"Create isolated inspection fixture: "+item);
                    int slot=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==item);
                    int quantity=bag.Count(item);Check(c.InspectFieldItem(slot),"Begin held inspection: "+item);
                    yield return null;yield return null;c.FieldWorld.ApplyHeldTool();
                    Check(c.FieldWorld.PresentedItem==item && bag.Count(item)==quantity,"Real tool pose is visible and inventory unchanged: "+item);
                    if(item==FieldItem.GrilledFish || item==FieldItem.HuntingRifle)Capture(c,evidence,"inspect-"+item);
                    c.EndItemInspection();
                    if(item!=FieldItem.FieldGuide)Check(bag.Exchange(new[]{FishingRecipes.Stack(item,1)},FieldItem.Empty,0),"Clean isolated item: "+item);
                }
                Check(c.InspectFieldItem(0),"Inspect the selected backpack item");
                for(int i=0;i<8;i++)yield return null;
                c.FieldWorld.ApplyHeldTool();Check(c.FieldWorld.PresentedItem==FieldItem.FieldGuide,"Inspection shows the actual held item");
                Capture(c,evidence,"guide-inspection");c.EndItemInspection();
                c.FieldWorld.ApplyHeldTool();for(int i=0;i<3;i++)yield return null;Capture(c,evidence,"guide-first-person");
                Check(bag.TryAdd(FieldItem.Binoculars,1),"Isolated zoom fixture owns binoculars");
                int binocularSlot=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==FieldItem.Binoculars);
                c.SelectFieldSlot(binocularSlot);c.Shore.Relocate(landing,Quaternion.Euler(0,270,0));c.View.ResetLook();c.UseFieldItem();
                for(int i=0;i<100 && c.View.BinocularBlend<.999f;i++)yield return null;
                c.View.ApplyView(true,true);float fov=c.viewCamera.fieldOfView;Capture(c,evidence,"binocular-2x");
                c.View.AdjustMagnification(12);c.View.ApplyView(true,true);
                Check(c.View.BinocularMagnification==8 && c.viewCamera.fieldOfView<12 && c.viewCamera.fieldOfView<fov*.4f && bag.Selected==binocularSlot,"8x changes the real projection while retaining the equipped binocular slot");
                Capture(c,evidence,"binocular-8x");c.View.AdjustMagnification(-12);c.UseFieldItem();
                Check(bag.Exchange(new[]{FishingRecipes.Stack(FieldItem.Binoculars,1)},FieldItem.Empty,0),"Zoom fixture cleaned without consuming other items");
                bag.TryAdd(FieldItem.Lantern,1);int lantern=Enumerable.Range(0,FishingBackpack.SlotCount).First(i=>bag.At(i).item==FieldItem.Lantern);
                if(lantern>=9){bag.Move(lantern,1);lantern=1;}c.SelectFieldSlot(lantern);c.UseFieldItem();
                c.SetDayNight(true);Check(c.Career.Calendar.Night,"Set isolated lantern scene to night");
                for(int i=0;i<20;i++)yield return null;c.FieldWorld.ApplyHeldTool();
                var lights=c.FieldWorld.GetComponentsInChildren<Light>().Where(l=>l.enabled).ToArray();
                Check(lights.Any(l=>l.type==LightType.Point && l.range>=10 && l.intensity>.4f) && lights.Any(l=>l.type==LightType.Spot),"Night lantern has a warm local light and soft ground spill");
                Capture(c,evidence,"lantern-night");c.LowerFieldTools();yield return null;yield return null;Capture(c,evidence,"lantern-off");
                c.ReturnToHarbor();c.SetDayNight(false);c.MoveToSpotForAutomation(0);c.Interact();yield return null;
                var swimmer=c.Ecology.FindCreatureForValidation(MarineKind.Turtle);var first=swimmer.position;float radius=FishingPrototype.FlatDistance(first,c.fishingSpots[0]);float radialChange=0;
                float capture=Time.captureDeltaTime;int jumps=c.Ecology.FishJumpCount;
                try
                {
                    Time.captureDeltaTime=.05f;
                    for(int i=0;i<600;i++)
                    {
                        var heading=c.fishingSpots[0]-c.viewCamera.transform.position;heading.y=0;
                        if(heading.sqrMagnitude>.1f)c.viewCamera.transform.rotation=Quaternion.LookRotation(heading);
                        radialChange=Mathf.Max(radialChange,Mathf.Abs(FishingPrototype.FlatDistance(swimmer.position,c.fishingSpots[0])-radius));
                        yield return null;
                    }
                }
                finally{Time.captureDeltaTime=capture;}
                Check(Vector3.Distance(first,swimmer.position)>.3f && radialChange>.35f,"Water creatures change radius while roaming instead of circling a fixed centre");
                Check(c.Ecology.FishJumpCount>jumps,"An unforced nearby surface event occurs during 30 seconds of normal ecological updates");
                var jump=c.Ecology.PreviewSeaBeat(false,.5f);Check(jump!=null && jump.position.y>1.3f,"Surface leap is tall and visible");
                c.viewCamera.transform.SetPositionAndRotation(jump.position+new Vector3(3,1,-4),Quaternion.LookRotation(new Vector3(-3,-1,4)));
                Capture(c,evidence,"surface-leap");
                Debug.Log("[V029] EVIDENCE: "+evidence);
            }
            finally{c.EndItemInspection();c.ReturnToHarbor();c.RestoreShopTestProfile(snapshot);}
        }
        private static void Capture(FishingPrototype c,string directory,string name)
        {
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);var target=RenderTexture.GetTemporary(1600,900,24);
            var old=c.viewCamera.targetTexture;var active=RenderTexture.active;
            try{c.viewCamera.targetTexture=target;c.viewCamera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());}
            finally{c.viewCamera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);}
        }
    }
    [DefaultExecutionOrder(200)]
    public sealed class FishingV029MouseProbe:MonoBehaviour
    {
        public FishingPrototype Controller;public Mouse Mouse;public bool Pressed,Released;
        private static readonly MethodInfo Input=typeof(FishingPrototype).GetMethod("TickDesktopCast",BindingFlags.NonPublic|BindingFlags.Instance);
        private void Update()
        {
            if(Mouse==null)return;
            bool press=Mouse.leftButton.wasPressedThisFrame,release=Mouse.leftButton.wasReleasedThisFrame;
            if(!press && !release)return;
            Pressed|=press;Released|=release;
            Input.Invoke(Controller,new object[]{press,true,false,false,Mouse});
        }
    }
}
