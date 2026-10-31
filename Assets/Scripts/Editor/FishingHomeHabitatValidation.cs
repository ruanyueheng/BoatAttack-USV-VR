using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingHomeHabitatValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("[HOME-HABITATS] "+message);Debug.Log("[HOME-HABITATS] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Habitat checks require the isolated production profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();c.Prologue.Suspend();c.CloseHomeChart(true);
            if(c.InventoryOpen)c.ToggleInventory();if(c.JournalOpen)c.ToggleJournal();if(c.Paused)c.TogglePause();
            c.ClassroomYear=1;c.ClassroomSeason=0;c.ClassroomDate=14;c.ClassroomHour=10;c.ApplyClassroomDate();
            yield return null;yield return null;
            Check(c.ClassroomDemoActive && c.Career.HomeAnimalCount==6,"Five populated habitat kinds use the real independent voyage fixture");
            var world=c.HomeEcology;world.Refresh();yield return null;Physics.SyncTransforms();
            var display=world.transform.Find("家园生态展示");var heights=new float[5];
            for(int i=0;i<5;i++)
            {
                var kind=(LandAnimalKind)i;var root=display.Find(FishingCareer.HabitatName(kind));heights[i]=root.position.y;
                var shell=root.Find("Shelter, exercise run and feeding station");
                Check(shell!=null && shell.GetComponentsInChildren<Renderer>().Length==1,"A single batched shelter mesh exists for "+kind);
                var mesh=shell.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.vertexCount>200 && mesh.triangles.Length/3<4000,"Habitat mesh budget remains bounded for "+kind);
                Check(root.GetComponents<BoxCollider>().Length==5,"The habitat has a supporting floor and four physical fence sides for "+kind);
                var approach=root.position+new Vector3(0,.2f,3.2f);
                Check(c.Shore.Ground(approach,out var ground) && c.Shore.Relocate(ground,Quaternion.Euler(0,180,0)),"The front management path is supported for "+kind);
                yield return null;
                Check(world.NearHome && world.NearbyHint!=null && world.NearbyHint.Contains(FishingCareer.HabitatName(kind)),"The habitat's front gate has a reachable desktop/XR action for "+kind);
                bool fence=false;
                foreach(var hit in Physics.RaycastAll(root.position+new Vector3(0,.5f,3.15f),Vector3.back,1.3f,~0,QueryTriggerInteraction.Ignore))
                    if(hit.collider.transform==root && !FishingShore.NonSolidSurface(hit.collider))fence=true;
                Check(fence,"A real ray across the front gate hits the solid fence for "+kind);
            }
            var pets=(IList)typeof(FishingHomeEcologyWorld).GetField("_pets",Private).GetValue(world);
            Check(pets.Count==c.Career.HomeAnimalCount,"Every housed animal has one visible actor");
            string captureDirectory=Path.GetFullPath("Logs/V054/Visual");Directory.CreateDirectory(captureDirectory);
            var camera=c.viewCamera;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;float fieldOfView=camera.fieldOfView;
            var playerRenderers=c.Shore.Actor.GetComponentsInChildren<Renderer>();var playerHidden=new bool[playerRenderers.Length];
            for(int i=0;i<playerRenderers.Length;i++){playerHidden[i]=playerRenderers[i].forceRenderingOff;playerRenderers[i].forceRenderingOff=true;}
            try
            {
                void Capture(string label,Vector3 position,Vector3 focus)
                {camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));camera.fieldOfView=55;FishingV016Validation.Capture(c,captureDirectory,label);}
                Capture("habitats-front",c.Home.Origin+new Vector3(.5f,5,20),c.Home.Origin+new Vector3(.5f,.6f,8.6f));
                foreach(var kind in new[]{LandAnimalKind.Rabbit,LandAnimalKind.Gull,LandAnimalKind.SeaTurtle})
                {
                    var root=display.Find(FishingCareer.HabitatName(kind));
                    Capture("habitat-"+kind,root.position+new Vector3(.6f,1.8f,4.8f),root.position+new Vector3(0,.55f,.65f));
                }
            }
            finally{camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);camera.fieldOfView=fieldOfView;for(int i=0;i<playerRenderers.Length;i++)if(playerRenderers[i]!=null)playerRenderers[i].forceRenderingOff=playerHidden[i];}
            var actors=new List<Transform>();foreach(var entry in pets)actors.Add((Transform)entry.GetType().GetField("Root").GetValue(entry));
            var start=actors.ConvertAll(a=>a.localPosition);
            var moved=new bool[actors.Count];float until=Time.time+9;
            while(Time.time<until){for(int i=0;i<actors.Count;i++)moved[i]|=Vector3.Distance(start[i],actors[i].localPosition)>.002f;yield return null;}
            for(int i=0;i<actors.Count;i++)Check(moved[i],"Production Update moves animal "+actors[i].name);
            c.TogglePause();start=actors.ConvertAll(a=>a.localPosition);for(int i=0;i<8;i++)yield return null;
            for(int i=0;i<actors.Count;i++)Check(actors[i].localPosition==start[i],"Pause freezes the activity clock for "+actors[i].name);
            c.TogglePause();
            CheckContainment(world,"initial six animals");
            // Reuse real capture recovery and housing transactions so the 4/8 resident fixtures remain restorable saves.
            foreach(var animal in new List<HomeAnimalData>(c.Career.HomeAnimals))Check(c.Career.ReleaseHomeAnimal(animal.id),"Release isolated animal "+animal.id+" for capacity fixture");
            c.Career.Backpack.Select(5);Check(c.Career.Backpack.Equipped==FieldItem.CaptureNet,"Capacity fixture uses the existing capture net");
            for(int i=0;i<5;i++)
            {
                var kind=(LandAnimalKind)i;int source=new[]{0,1,2,8,11}[i];
                for(int level=1;level<=2;level++)
                {
                    if(level==2)Check(c.Career.UpgradeAnimalHabitat(kind),"Existing expansion works for "+kind);
                    while(c.Career.HousedAnimalCount(kind)<level*4)
                    {
                        for(int second=0;second<=FishingAnimalLoot.RespawnSeconds;second++)c.Career.TickHomeEcology(1);
                        int id=c.Career.CaptureAnimalWithNet(source,kind);
                        Check(id>0 && c.Career.HouseAnimal(id),"Populate "+kind+" via recovered capture and housing "+c.Career.HousedAnimalCount(kind)+"/"+(level*4));
                    }
                    world.Refresh();yield return null;Physics.SyncTransforms();
                    Check(FishingCareer.TryRestore(c.Career.Snapshot(),out _),kind+" at capacity "+(level*4)+" is a valid existing-schema save");
                    CheckContainment(world,kind+" level "+level+" at capacity "+(level*4));
                }
                foreach(var animal in new List<HomeAnimalData>(c.Career.HomeAnimals))Check(c.Career.ReleaseHomeAnimal(animal.id),"Clear isolated "+kind+" capacity resident "+animal.id);
            }
            world.Refresh();yield return null;Physics.SyncTransforms();display=world.transform.Find("家园生态展示");
            for(int i=0;i<5;i++)Check(Mathf.Abs(display.Find(FishingCareer.HabitatName((LandAnimalKind)i)).position.y-heights[i])<.015f,"Refreshing/expanding does not stack the habitat above itself for "+(LandAnimalKind)i);
            Debug.Log("[HOME-HABITATS] EVIDENCE: motion, containment, pause, shared actions and gate collision passed; visual framing and native XR input need separate capture.");
            c.LeaveClassroomDemo();yield return null;
        }

        private static void CheckContainment(FishingHomeEcologyWorld world,string stage)
        {
            var pets=(IList)typeof(FishingHomeEcologyWorld).GetField("_pets",Private).GetValue(world);
            var actors=new List<Transform>();foreach(var entry in pets)actors.Add((Transform)entry.GetType().GetField("Root").GetValue(entry));
            var bodies=new List<(Transform actor,Renderer renderer)>();foreach(var actor in actors)foreach(var renderer in actor.GetComponentsInChildren<Renderer>())bodies.Add((actor,renderer));
            var animate=typeof(FishingHomeEcologyWorld).GetMethod("AnimatePets",Private);
            for(int step=0;step<1800;step++)
            {
                animate.Invoke(world,new object[]{.1f});
                var footprints=new List<(Transform actor,Rect rect)>();
                foreach(var (actor,renderer) in bodies)
                {
                    var bounds=renderer.bounds;var center=actor.parent.position;var min=bounds.min-center;var max=bounds.max-center;
                    bool inside=min.x>=-1.42f && max.x<=1.42f && min.z>=-.65f && max.z<=2.43f && min.y>=.07f && min.y<=.15f;
                    var footprint=Rect.MinMaxRect(min.x,min.z,max.x,max.z);
                    bool clear=!footprint.Overlaps(new Rect(.95f,1.18f,.36f,.36f)) && !footprint.Overlaps(new Rect(.95f,1.78f,.36f,.36f))
                        && !footprint.Overlaps(new Rect(-.9f,-.8f,1.8f,1.44f));
                    if(!inside || !clear)Check(false,stage+" step="+step+" actor="+actor.name+" renderer="+renderer.name+" min="+min.ToString("F4")+" max="+max.ToString("F4")+" inside="+inside+" clearFoodWaterShelter="+clear);
                    foreach(var previous in footprints)if(previous.actor!=actor && previous.actor.parent==actor.parent && previous.rect.Overlaps(footprint))
                        Check(false,stage+" step="+step+" bodies overlap: "+previous.actor.name+" and "+actor.name+" min="+min.ToString("F4")+" max="+max.ToString("F4"));
                    footprints.Add((actor,footprint));
                }
            }
            Check(true,stage+": three minutes keep every body inside the fence, apart from other animals and clear of food, water and shelter solids");
        }
    }
}
