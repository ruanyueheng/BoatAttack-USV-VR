using System;
using System.Collections;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingMarineCaptureValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        public static void Run()=>FishingV011Validation.Begin("MARINE-CAPTURE");
        private static void Check(bool value,string text)
        {if(!value)throw new InvalidOperationException("[MARINE-CAPTURE] "+text);Debug.Log("[MARINE-CAPTURE] CHECK: "+text);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Marine catch fixtures use an isolated temporary profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.Paused)c.TogglePause();c.ReturnToHarbor();yield return null;
            Check(c.Career.Backpack.TryAdd(FieldItem.CaptureNet,1),"The actual net fits in the isolated backpack");
            for(int slot=0;slot<FishingBackpack.HotbarCount;slot++)if(c.Career.Backpack.At(slot).item==FieldItem.CaptureNet)c.SelectFieldSlot(slot);
            var ecology=c.Ecology;bool enabled=ecology.enabled;ecology.enabled=false;
            try
            {
                var visits=(IEnumerable)typeof(FishingEcology).GetField("_shoreVisits",Private).GetValue(ecology);
                foreach(var kind in new[]{LandAnimalKind.Gull,LandAnimalKind.SeaTurtle})
                {
                    object selected=null,creature=null;Transform animal=null;
                    foreach(var visit in visits)
                    {
                        var candidate=visit.GetType().GetField("creature",Private).GetValue(visit);
                        var marine=(MarineKind)candidate.GetType().GetField("kind",Private).GetValue(candidate);
                        if(marine!=(kind==LandAnimalKind.Gull?MarineKind.Gull:MarineKind.Turtle))continue;
                        selected=visit;creature=candidate;animal=(Transform)candidate.GetType().GetField("transform",Private).GetValue(candidate);break;
                    }
                    Check(selected!=null && animal!=null,"A real validated coast visit exists for "+kind);
                    var tick=typeof(FishingEcology).GetMethod("TickMarineShoreVisit",Private);
                    bool window=false;Vector3 approach=default;float travelled=0;var previous=animal.position;int steps=0;
                    for(;steps<5000;steps++)
                    {
                        tick.Invoke(ecology,new[]{creature,(object).1f});
                        float delta=Vector3.Distance(previous,animal.position);travelled+=delta;previous=animal.position;
                        CheckStep(delta<=.321f,"Natural shore movement never teleports the animal");
                        if(ecology.MarineShoreWindow(animal,out approach)){window=true;break;}
                    }
                    Check(window && travelled>2,"The production "+kind+" flies/swims/crawls into a timed reachable shore window in "+(steps*.1f)+" simulated seconds");
                    Check(c.Shore.Ground(new Vector3(approach.x,0,approach.z),out var support) && c.Shore.Clearance(support),"The capture approach has real supported standing clearance");
                    Check(c.Shore.RestoreSavedFooting(support,Quaternion.LookRotation(Vector3.ProjectOnPlane(animal.position-support,Vector3.up)).eulerAngles.y),"The test character uses real safe shore footing");
                    typeof(FishingEcology).GetMethod("Sense",Private).Invoke(ecology,null);
                    Check(animal.gameObject.activeInHierarchy && c.Shore.ClearSight(c.Shore.Character.EyePosition,animal.position+Vector3.up*.2f),"The resting marine animal is visible from its standing approach");
                    for(int frame=0;frame<60;frame++)
                    {
                        c.Shore.Move(Vector2.zero,false,false,true,.02f);c.PresentCharacterFrame();
                        FishingV036FarmValidation.Aim(c,animal.position+Vector3.up*.2f);yield return null;
                    }
                    int source=-1;foreach(var candidate in c.RifleHunting.Targets)if(candidate.Root==animal)source=candidate.Id;
                    Check(source>=FishingAnimalLoot.GullStart && c.HomeEcology.UseNet(),"The actual net captures the resting "+kind+" through its physical range, view and net-mouth checks");
                    Check(!c.Career.WildAnimalAvailable(source,kind) && !animal.gameObject.activeSelf,"The original source ID receipt hides the captured animal");
                    var before=animal.position;typeof(FishingEcology).GetMethod("Update",Private).Invoke(ecology,null);
                    c.RifleHunting.RefreshAnimals();
                    Check(!animal.gameObject.activeSelf && animal.position==before,"Ecology and wildlife refresh cannot move or resurrect the captured marine source");
                    c.Shore.BoardImmediately();
                }
            }
            finally{ecology.enabled=enabled;}
        }
        private static void CheckStep(bool value,string text)
        {if(!value)throw new InvalidOperationException("[MARINE-CAPTURE] "+text);}
    }
}
