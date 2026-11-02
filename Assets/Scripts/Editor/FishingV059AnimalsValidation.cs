using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV059AnimalsValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException("[V059-ANIMALS] "+message);Debug.Log("[V059-ANIMALS] CHECK: "+message);}
        private static void Close(FishingPrototype c)
        {if(c.XRNotebook.IsOpen)c.XRNotebook.Close();if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();}
        private static void Gate(FishingPrototype c,LandAnimalKind kind)
        {
            var point=c.HomeEcology.HabitatPosition(kind)+Vector3.forward*3.2f;
            Check(c.Shore.Ground(point,out var floor) && c.Shore.Relocate(floor,Quaternion.Euler(0,180,0)),"Supported gate approach for "+kind);
            c.View.ResetLook();c.PresentCharacterFrame();Physics.SyncTransforms();
        }
        private static void CaptureResident(FishingPrototype c,Transform resident,LandAnimalKind kind)
        {
            // Evidence views deliberately separate scene/model visibility from the first-person controls.
            // Hide only the player and their held prop; keep signs, shelters and every resident intact.
            var camera=c.viewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;
            float fieldOfView=camera.fieldOfView;
            var hidden=new Dictionary<Renderer,bool>();
            void Hide(Transform root)
            {
                if(root==null)return;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {if(hidden.ContainsKey(renderer))continue;hidden.Add(renderer,renderer.forceRenderingOff);renderer.forceRenderingOff=true;}
            }
            Hide(c.Shore.Actor);Hide(c.FieldWorld.HeldTool);Hide(c.Hands.Rod);Hide(c.Hands.Net);
            try
            {
                var renderers=resident.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var center=resident.parent.position;
                void Capture(string label,Vector3 eye,Vector3 focus,float fov)
                {
                    camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));camera.fieldOfView=fov;
                    FishingV016Validation.Capture(c,"Logs/V059",label);
                }
                Capture("habitat-"+kind+"-overview",center+new Vector3(.65f,2.9f,4.3f),center+new Vector3(0,.55f,.9f),48);
                // A gull must be viewed below the aviary roof. Ground residents are viewed down into
                // the exercise run from inside the front fence, clear of the gate's management sign.
                var offset=kind==LandAnimalKind.Gull?new Vector3(.28f,.35f,1.25f):new Vector3(.45f,.85f,1.25f);
                Capture("habitat-"+kind,bounds.center+offset,bounds.center,40);
                Check(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),bounds),"The scene/detail capture frames its actual resident: "+kind);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fieldOfView;
                foreach(var pair in hidden)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;
            }
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Animal regression uses only the isolated production profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);Close(c);
            var original=c.Career.Snapshot();bool quiet=c.QuietMode;
            float master=c.MasterVolume,ambient=c.AmbientVolume;
            try
            {
                c.ReturnToHarbor();yield return null;
                var rb=c.Player.engine.RB;var berth=c.Home.Berth+Vector3.up*.7f;
                rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
                for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"The actual boat berth reaches the home habitat path");yield return null;
                c.RestoreShopTestProfile(FishingClassroomDemo.Create().Snapshot());Close(c);
                if(c.QuietMode)c.ToggleQuiet();c.SetSetting("Master",.65f);c.SetSetting("Ambient",.8f);
                var world=c.HomeEcology;world.ClearHabitatFilter();world.Refresh();yield return null;Physics.SyncTransforms();
                var play=typeof(FishingHomeEcologyWorld).GetMethod("PlayPetCall",Private);
                string[] cues={"RabbitCall","FoxCall","TortoiseCall","GullCall","SeaTurtleCall"};
                Directory.CreateDirectory("Logs/V059");
                for(int i=0;i<5;i++)
                {
                    var kind=(LandAnimalKind)i;
                    var animal=c.Career.HomeAnimals.First(a=>a.kind==kind);
                    int residents=c.Career.HousedAnimalCount(kind);
                    Check(c.Career.CarryHomeAnimal(animal.id),"Move a real resident into the live carrier for "+kind);
                    world.Refresh();yield return null;Physics.SyncTransforms();Gate(c,kind);yield return null;
                    var oldPlacement=world.Choices(3).First(choice=>choice.Label.StartsWith("#"+animal.id+" ",StringComparison.Ordinal));
                    Check(world.NearbyHint.Contains(FishingCareer.HabitatName(kind)) && world.NearbyHint.Contains("安置携带"),"Gate identifies the correct species and offers direct placement for "+kind);
                    Check(c.RequestWorldInteraction(),"The production A/E action accepts the gate for "+kind);
                    Check(!c.InventoryOpen && !c.XRNotebook.IsOpen && c.Career.HomeAnimal(animal.id).housed
                        && c.Career.HousedAnimalCount(kind)==residents && c.Career.CarriedAnimalCount==0,"One real interaction places exactly the carried animal and keeps gameplay open for "+kind);
                    oldPlacement.Run();Check(c.Career.HomeAnimal(animal.id).housed && c.Career.CarriedAnimalCount==0,"A repeated stale placement cannot pick the animal back up for "+kind);
                    Check(c.Career.CarryHomeAnimal(animal.id),"Prepare an actual repeated menu placement for "+kind);
                    var repeatedPlacement=world.Choices(3).First(choice=>choice.Label.StartsWith("#"+animal.id+" ",StringComparison.Ordinal));
                    repeatedPlacement.Run();repeatedPlacement.Run();
                    Check(c.Career.HomeAnimal(animal.id).housed && c.Career.CarriedAnimalCount==0 && c.Career.HousedAnimalCount(kind)==residents,"Calling the same real placement command twice keeps one housed animal for "+kind);
                    yield return null;Physics.SyncTransforms();
                    var pets=(IList)typeof(FishingHomeEcologyWorld).GetField("_pets",Private).GetValue(world);
                    object pet=null;
                    foreach(var candidate in pets)if((int)candidate.GetType().GetField("Id").GetValue(candidate)==animal.id){pet=candidate;break;}
                    Check(pet!=null,"Every placed "+kind+" owns a visible model");
                    var root=(Transform)pet.GetType().GetField("Root").GetValue(pet);
                    Check(root.GetComponentsInChildren<Renderer>().Any(r=>r.enabled && r.gameObject.activeInHierarchy),"The placed "+kind+" has active renderers");
                    if(kind==LandAnimalKind.Gull)Check(root.localPosition.y>=.59f && Mathf.Abs(root.localPosition.z+.2f)<.02f,"The housed bird stands on the actual aviary perch");
                    Check(c.WorldAudio.HasCue(cues[i]),"A decoded species call is present for "+kind);
                    foreach(var source in c.WorldAudio.Sources)source.Stop();
                    int before=c.WorldAudio.PlayCount(cues[i]);
                    // Ordinary Update can already have voiced the newly placed resident; respect the real cue cooldown.
                    float until=Time.realtimeSinceStartup+.22f;while(Time.realtimeSinceStartup<until)yield return null;
                    foreach(var source in c.WorldAudio.Sources)source.Stop();
                    before=c.WorldAudio.PlayCount(cues[i]);
                    Check((bool)play.Invoke(world,new[]{pet}) && c.WorldAudio.PlayCount(cues[i])==before+1,"The production pet call plays the correct species recording for "+kind);
                    var voice=c.WorldAudio.Sources.First(s=>s.isPlaying && s.clip==FishingWorldAudio.Clip(cues[i]));
                    Check(voice.spatialBlend==1 && Vector3.Distance(voice.transform.position,root.position+Vector3.up*.18f)<.001f,"The audible call originates from the placed animal for "+kind);
                    c.TogglePause();Check(!(bool)play.Invoke(world,new[]{pet}),"Pause prevents pet calls for "+kind);c.TogglePause();
                    c.ToggleQuiet();Check(!(bool)play.Invoke(world,new[]{pet}),"Quiet mode prevents pet calls for "+kind);c.ToggleQuiet();
                    c.SetSetting("Ambient",0);Check(!(bool)play.Invoke(world,new[]{pet}),"The ambient slider silences pet calls for "+kind);c.SetSetting("Ambient",.8f);
                    CaptureResident(c,root,kind);
                }
                FishingSaveStore.Save(c.SavePath,c.Career);var loaded=FishingSaveStore.Load(c.SavePath,out var warning);
                Check(warning.Length==0 && loaded.HomeAnimalCount==6 && loaded.CarriedAnimalCount==0,"Gate placements survive the real save store with unique ownership");
                for(int i=0;i<5;i++)Check(loaded.HousedAnimalCount((LandAnimalKind)i)==c.Career.HousedAnimalCount((LandAnimalKind)i),"The actual save restores housed "+(LandAnimalKind)i);

                // A captured animal without a matching kit must remain carried, with an actionable explanation.
                var missing=new FishingCareer();Check(missing.Backpack.TryAdd(FieldItem.CaptureNet,1) && missing.Backpack.Select(1),"Missing-habitat fixture owns a real capture net");
                int rabbit=missing.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit);Check(rabbit>0,"The missing-habitat fixture captures through the real transaction");
                c.RestoreShopTestProfile(missing.Snapshot());world.ClearHabitatFilter();world.Refresh();yield return null;Gate(c,LandAnimalKind.Rabbit);
                var blocked=world.Choices(3).First();Check(!blocked.Enabled && blocked.Label.Contains("先部署"),"A missing habitat disables placement and explains the required construction");
                blocked.Run();Check(!c.Career.HomeAnimal(rabbit).housed && c.Career.CarriedAnimalCount==1,"Invoking a stale or disabled placement cannot discard or duplicate the animal");
                Check(c.RequestWorldInteraction(),"An empty plot still opens the real construction page");
                Check(world.Choices(0).Count==1 && world.Choices(0)[0].Label.Contains(FishingCareer.HabitatName(LandAnimalKind.Rabbit)),"The empty plot selects its own habitat construction");Close(c);world.ClearHabitatFilter();

                // Fill the existing four places through real capture recovery, then reject a fifth carried rabbit.
                var full=FishingClassroomDemo.Create();full.Backpack.Select(5);
                int third=full.CaptureAnimalWithNet(6,LandAnimalKind.Rabbit);Check(third>0 && full.HouseAnimal(third),"Populate the third real rabbit place");
                for(int second=0;second<=FishingAnimalLoot.RespawnSeconds;second++)full.TickHomeEcology(1);
                int fourth=full.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit);Check(fourth>0 && full.HouseAnimal(fourth),"Populate the fourth real rabbit place");
                int fifth=full.CaptureAnimalWithNet(3,LandAnimalKind.Rabbit);Check(fifth>0,"Carry one additional real rabbit");
                c.RestoreShopTestProfile(full.Snapshot());world.ClearHabitatFilter();world.Refresh();yield return null;Gate(c,LandAnimalKind.Rabbit);
                Check(c.RequestWorldInteraction(),"A full habitat opens management instead of losing the carried animal");
                blocked=world.Choices(3).First();Check(!blocked.Enabled && blocked.Label.Contains("已满"),"The carried animal appears first with a capacity explanation");
                blocked.Run();Check(c.Career.HousedAnimalCount(LandAnimalKind.Rabbit)==4 && c.Career.CarriedAnimalCount==1
                    && !c.Career.HomeAnimal(fifth).housed,"A full habitat preserves the extra animal and its existing residents");
                Check(FishingCareer.TryRestore(c.Career.Snapshot(),out _),"Full-habitat rejection preserves the validated save schema");
            }
            finally
            {
                Close(c);c.RestoreShopTestProfile(original);c.HomeEcology.ClearHabitatFilter();c.HomeEcology.Refresh();
                c.SetSetting("Master",master);c.SetSetting("Ambient",ambient);if(c.QuietMode!=quiet)c.ToggleQuiet();
            }
            Debug.Log("[V059-ANIMALS] EVIDENCE: five production gate placements, visible/perched actors, spatial calls, mute guards, ownership persistence and capacity rejection passed; physical headset listening remains a device check.");
        }
    }
}
