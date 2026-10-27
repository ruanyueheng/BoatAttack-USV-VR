using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Explicit native visual fixtures reuse the existing full-HUD proof and isolated acceptance profile.</summary>
    public sealed class FishingFeedbackCapture : MonoBehaviour
    {
        public const string EnableFlag="-fishing-feedback-capture";
        private FishingPrototype _c;
        [Serializable] private sealed class Entry {public string stage,directory;}
        [Serializable] private sealed class Manifest {public string version=FishingPrototype.Version;public bool physicalInputTested=false;public List<Entry> captures=new List<Entry>();}
        private readonly Manifest _manifest=new Manifest();
        public void Begin(FishingPrototype c)
        {
            if(!c.Automated || !c.HasIsolatedProfile || c.AcceptanceOptions==null || c.AcceptanceOptions.Automatic || string.IsNullOrEmpty(c.AcceptanceOptions.OutputDirectory) || !FishingPrototype.HasArgument(EnableFlag))
                throw new InvalidOperationException("Feedback capture needs an explicit isolated manual acceptance session.");
            _c=c;Application.logMessageReceived+=Error;StartCoroutine(Run());
        }
        private void Error(string message,string stack,LogType type)
        {if(type==LogType.Exception || type==LogType.Error){Debug.LogWarning("[FEEDBACK-CAPTURE] Abort after runtime error: "+message);Application.Quit(1);}}
        private static void Require(bool ok,string text){if(!ok)throw new InvalidOperationException("[FEEDBACK-CAPTURE] "+text);}
        private IEnumerator Run()
        {
            yield return null;yield return null;
            _c.Acceptance.SelectCase(FishingAcceptanceCase.HomeInterior);yield return null;
            var c=_c.Career;var bag=c.Backpack;
            Require(bag.TryAdd(FieldItem.Aquarium,1) && bag.TryAdd(FieldItem.FishFeed,8) && c.BuildAquarium(),"Build actual aquarium");
            string fish=c.Inventory[0].id;Require(c.DisplayFish(fish) && c.FeedDisplayedFish(fish),"Display and feed real catch");
            Require(bag.TryAdd(FieldItem.RabbitHutch,1) && bag.TryAdd(FieldItem.CaptureNet,1) && bag.TryAdd(FieldItem.TurnipSeeds,8) && c.BuildAnimalHabitat(LandAnimalKind.Rabbit),"Build real habitat");
            Equip(FieldItem.CaptureNet);
            int first=c.CaptureAnimalWithNet(0,LandAnimalKind.Rabbit),second=c.CaptureAnimalWithNet(3,LandAnimalKind.Rabbit);
            Require(first>0 && second>0 && c.HouseAnimal(first) && c.HouseAnimal(second),"House a pair of captured rabbits");
            _c.HomeEcology.Refresh();Equip(FieldItem.Empty);
            Stand(_c.Home.Origin+new Vector3(2,.25f,-3.1f),_c.HomeEcology.AquariumPosition+Vector3.up*.95f);
            var shot=Shot("aquarium-world");while(shot.MoveNext())yield return shot.Current;
            _c.HomeEcology.Open();shot=Shot("ecology-menu");while(shot.MoveNext())yield return shot.Current;
            if(_c.InventoryOpen)_c.ToggleInventory();
            var habitat=_c.HomeEcology.HabitatPosition(LandAnimalKind.Rabbit);
            var familyApproach=habitat+Vector3.forward*3;familyApproach.y=0;Stand(familyApproach,habitat+Vector3.up*.5f);
            var breeding=_c.HomeEcology.Choices(3).Find(choice=>choice.Label.StartsWith("#"+first+" 与 #"+second+" 繁殖",StringComparison.Ordinal));
            Require(breeding!=null && breeding.Enabled,"The actual ecology menu offers this breeding pair");breeding.Run();
            Require(c.HomeAnimalCount==3,"The real breeding menu creates one offspring and its heart feedback");
            shot=Shot("rabbit-family");while(shot.MoveNext())yield return shot.Current;
            Require(_c.OpenPortableChart(),"Open the real portable chart");
            float chartDeadline=Time.realtimeSinceStartup+5;
            while(!_c.HomeChartReady && Time.realtimeSinceStartup<chartDeadline)yield return null;
            Require(_c.HomeChartReady,"The portable chart unfolds before capture");
            shot=Shot("portable-chart");while(shot.MoveNext())yield return shot.Current;
            _c.CloseHomeChart(true);for(int i=0;i<20;i++)yield return null;

            _c.Acceptance.SelectCase(FishingAcceptanceCase.ShoreFirstPerson);yield return null;
            Require(_c.Career.BuyHuntingRifle(),"Buy the real rifle");_c.Shore.BoardImmediately();
            var rb=_c.Player.engine.RB;var berth=FishingCoastalPresentation.Berth(1)+Vector3.up*.4f;
            _c.WaterRoute.CancelPlanning();rb.position=berth;rb.rotation=Quaternion.Euler(0,180,0);rb.velocity=rb.angularVelocity=Vector3.zero;
            _c.Player.transform.SetPositionAndRotation(berth,rb.rotation);Physics.SyncTransforms();
            for(int i=0;i<20;i++)yield return null;
            Require(_c.Shore.TryDisembark(),"Land on the optional encounter island");
            var board=_c.Combat.transform.Find("礁影试炼告示 · 自选挑战");
            var boardApproach=board.position+Vector3.forward*1.8f;boardApproach.y=0;Stand(boardApproach,board.position);
            yield return null;
            Require(_c.RequestWorldInteraction() && _c.Combat.Challenging,"Start the actual guardian challenge");
            Collider boss=null;foreach(var target in _c.Combat.GetComponentsInChildren<Collider>())if(_c.Combat.IsTarget(target)){boss=target;break;}
            Require(boss!=null,"Guardian target exists");
            Stand(_c.Combat.Arena+new Vector3(5,0,10),boss.bounds.center);Equip(FieldItem.HuntingRifle);
            for(int i=0;i<20;i++){_c.RifleHunting.ReadDesktopInput(false,true);_c.PresentCharacterFrame();yield return null;}
            shot=Shot("guardian-aim");while(shot.MoveNext())yield return shot.Current;
            _c.RifleHunting.ReadDesktopInput(false,false);

            for(int station=2;station<=3;station++)
            {
                _c.Acceptance.SelectCase(FishingAcceptanceCase.HomeInterior);yield return null;
                Require(_c.Career.Backpack.TryAdd(FieldItem.Driftwood,8) && _c.Career.Backpack.TryAdd(FieldItem.IronIngot,4),"Supply raw materials for actual handcraft");
                Stand(_c.HandGames.Approach(station),_c.HandGames.Station(station).position+Vector3.up);
                yield return null;_c.PresentCharacterFrame();yield return null;
                Require(_c.HandGames.Begin(station),"Begin the real handcraft station");
                for(int i=0;i<20;i++)yield return null;
                shot=Shot(station==2?"wood-saw":"forge-hammer");while(shot.MoveNext())yield return shot.Current;
                _c.HandGames.Cancel();yield return null;
            }
            _c.Acceptance.SelectCase(FishingAcceptanceCase.IslandLanding);yield return null;Equip(FieldItem.Empty);
            FishingFieldWorld.Pickup cache=null;foreach(var pickup in _c.FieldWorld.Pickups)if(pickup.Id==8){cache=pickup;break;}
            Require(cache!=null,"The appended wild cache exists");bool approached=false;
            for(int i=0;i<12;i++)
            {
                var seed=cache.Feet+Quaternion.Euler(0,i*30,0)*Vector3.forward*1.4f;
                if(!_c.Shore.Ground(seed,out var floor) || !_c.Shore.Clearance(floor))continue;
                Stand(floor,cache.Root.position+Vector3.up*.3f);approached=true;break;
            }
            Require(approached,"Wild cache has a supported viewing approach");
            yield return new WaitForSecondsRealtime(.6f);
            Require(_c.FieldWorld.PickupById(cache.Id,cache.Item,cache.Count),"Native fixture opens the real wild cache");
            _c.FieldWorld.ClearToolPresentation();
            var cacheShot=Shot("wild-cache-opening");while(cacheShot.MoveNext())yield return cacheShot.Current;
            string output=_c.AcceptanceOptions.OutputDirectory;
            File.WriteAllText(Path.Combine(output,"feedback-manifest.json"),JsonUtility.ToJson(_manifest,true));
            Debug.Log("[FEEDBACK-CAPTURE] PASS: eight native full-HUD captures; physical input was not tested. "+output);Application.Quit(0);
        }
        private void Equip(FieldItem item)
        {
            var bag=_c.Career.Backpack;
            for(int slot=0;slot<FishingBackpack.SlotCount;slot++)if(bag.At(slot).item==item)
            {if(slot>=FishingBackpack.HotbarCount){Require(bag.Move(slot,1),"Move fixture item into hotbar");slot=1;}_c.SelectFieldSlot(slot);return;}
            throw new InvalidOperationException("[FEEDBACK-CAPTURE] Missing fixture item: "+item);
        }
        private void Stand(Vector3 seed,Vector3 target)
        {
            Require(_c.Shore.Ground(seed,out var feet) && _c.Shore.Relocate(feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(target-seed,Vector3.up))),"Supported capture standing position");
            for(int i=0;i<8;i++)_c.Shore.Move(Vector2.zero,false,false,false,.02f);
            _c.View.ResetLook();_c.PresentCharacterFrame();var delta=target-_c.viewCamera.transform.position;
            float pitch=Mathf.Atan2(-delta.y,Vector3.ProjectOnPlane(delta,Vector3.up).magnitude)*Mathf.Rad2Deg;
            _c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));_c.PresentCharacterFrame();
        }
        private IEnumerator Shot(string stage)
        {
            yield return new WaitForSecondsRealtime(.6f);
            var capture=_c.Acceptance.CaptureFrame();while(capture.MoveNext())yield return capture.Current;
            Require(FishingAcceptance.Failure==null && _c.Acceptance.CaptureDirectory!=null,"Native backbuffer and opaque HUD proof are present");
            _manifest.captures.Add(new Entry{stage=stage,directory=_c.Acceptance.CaptureDirectory});
            Debug.Log("[FEEDBACK-CAPTURE] Captured "+stage);
        }
        private void OnDestroy(){Application.logMessageReceived-=Error;}
    }
}


