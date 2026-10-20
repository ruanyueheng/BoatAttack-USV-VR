using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    [InitializeOnLoad]
    public static class FishingWorkshopValidation
    {
        private const string Key="Fishing.WorkshopValidation.";
        private static IEnumerator _checks;
        private static double _next;
        static FishingWorkshopValidation()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,type)=>
            {if(SessionState.GetBool(Key+"active",false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))SessionState.SetString(Key+"error",message);};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"active",false))return;
                SessionState.SetBool(Key+"active",false);string error=SessionState.GetString(Key+"error","");
                if(!SessionState.GetBool(Key+"finished",false) && error=="")error="Incomplete checks";
                if(error=="")Debug.Log("[WORKSHOP] PASS: coastline, hull boarding, imported avatar, craft/smelt/place/save regression; simulated input only.");
                else Debug.LogError("[WORKSHOP] FAIL: "+error);
                EditorApplication.Exit(error==""?0:1);
            };
        }
        public static void Run()
        {
            SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"finished",false);SessionState.SetString(Key+"error","");
            SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+240);
            FishingScriptBinding.ValidateScene();EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))SessionState.SetString(Key+"error","Timeout");
            if(SessionState.GetString(Key+"error","")!=""){if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();return;}
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<_next)return;
            var c=UnityEngine.Object.FindObjectOfType<FishingPrototype>();if(c==null || c.WorkshopWorld==null)return;
            try
            {
                if(_checks==null)_checks=Checks(c);_next=EditorApplication.timeSinceStartup+.02;
                if(!_checks.MoveNext()){SessionState.SetBool(Key+"finished",true);EditorApplication.ExitPlaymode();}
            }
            catch(Exception e){SessionState.SetString(Key+"error",e.ToString());EditorApplication.ExitPlaymode();}
        }
        private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Debug.Log("[WORKSHOP] CHECK: "+name);}
        private static void Boat(FishingPrototype c,Vector3 position)
        {var rb=c.Player.engine.RB;rb.position=position;rb.rotation=Quaternion.identity;rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(position,Quaternion.identity);Physics.SyncTransforms();}
        private static void Feet(FishingPrototype c,Vector3 position)
        {c.Shore.Body.enabled=false;c.Shore.Actor.position=position;c.Shore.Body.enabled=true;Physics.SyncTransforms();}
        private static void Capture(FishingPrototype c,string name,Vector3 position,Vector3 target)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=c.viewCamera;var oldPos=camera.transform.position;var oldRot=camera.transform.rotation;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D image=null;
            try
            {
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                Directory.CreateDirectory("Logs/workshop-preview");File.WriteAllBytes("Logs/workshop-preview/"+name+".png",image.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;camera.transform.SetPositionAndRotation(oldPos,oldRot);RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
        }
        private static IEnumerator Checks(FishingPrototype c)
        {
            yield return null;
            var shore=c.Shore;var world=c.WorkshopWorld;var bag=c.Career.Backpack;
            Check(c.HasIsolatedProfile,"never touch normal player save");
            var character=shore.Actor.GetComponentInChildren<FishingCharacter>();Check(character!=null && character.Ready,"CC0 rig and all three locomotion clips imported");
            var skinned=character.GetComponentsInChildren<SkinnedMeshRenderer>();Check(skinned.Length>0 && skinned.All(r=>r.bones.Length>0),"real skinned character, not cuboid placeholder");
            Debug.Log("[WORKSHOP] MATERIALS: "+string.Join("; ",skinned.SelectMany(r=>r.sharedMaterials.Select(m=>r.name+"/"+(m==null?"null":m.name+"="+m.shader.name)))));
            Check(skinned.All(r=>r.sharedMaterials.All(m=>m!=null && m.shader.name=="Universal Render Pipeline/Lit")),"all character materials converted to URP");
            var tray=c.Player.transform.Find("Safe catch inspection tray");c.CatchRig.Apply(.02f);Check(tray!=null && !tray.gameObject.activeSelf,"idle inspection tray is stowed");
            Check(world.Nodes.Count>=40,"at least forty usable renewable resource nodes");
            Check(world.State.Stations.Count(s=>s.permanent)==6 && world.State.Stations.Count(s=>s.kind==CraftStation.Forge)==2,"shore and home workbench, furnace and forge spawn once at each location");
            // Real terrain, away from every named berth. Scan only to choose fixture coordinates,
            // then exercise production TryDisembark with the discovered geometry.
            bool natural=false;Vector3 coast=Vector3.zero;
            for(int x=-102;x<=130 && !natural;x+=8)for(int z=-106;z<=98 && !natural;z+=8)
            {
                var p=new Vector3(x,.6f,z);
                if(shore.Sites.Any(s=>FishingPrototype.FlatDistance(p,s.Berth)<16))continue;
                if(shore.Ground(p,out _))continue;
                Boat(c,p);shore.UpdateProximity();
                if(shore.FindLanding(out var landing,out var named) && !named){natural=true;coast=p;}
            }
            Check(natural,"natural non-marker coastline found");Boat(c,coast);
            Check(shore.TryDisembark() && shore.FreeLanding,"G lands on original terrain away from four authored sites at "+coast);
            Check(shore.Clearance(shore.Actor.position) && shore.SafeCoastalFooting(shore.Actor.position-Vector3.up*.08f),"coastal landing capsule has support and clearance");
            Capture(c,"01-free-coast",shore.Actor.position+new Vector3(5,3,-6),shore.Actor.position+Vector3.up);
            shore.BoardImmediately();
            // Explicit geometry tests separate policy from details of the art terrain.
            var platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.name="WORKSHOP TEST coast platform";platform.transform.position=new Vector3(250,.55f,250);platform.transform.localScale=new Vector3(8,1.1f,12);
            Boat(c,new Vector3(244,.5f,250));Physics.SyncTransforms();
            Check(shore.TryDisembark() && shore.FreeLanding,"unnamed flat shoreline is accepted");
            var landingPoint=shore.Actor.position;
            Feet(c,new Vector3(247,1.18f,253.8f));Check(shore.BoatEdgeDistance()<4 && shore.CanBoard(),"near bow boarding uses hull edge not old landing marker");
            Check(shore.TryBoard(),"near-bow G boards");
            Boat(c,new Vector3(244,.5f,250));Check(shore.TryDisembark(),"repeat coast landing");
            Feet(c,new Vector3(247,1.18f,246.2f));Check(shore.CanBoard(),"near stern boarding accepted");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="WORKSHOP TEST blocking wall";wall.transform.position=new Vector3(245.5f,2,250);wall.transform.localScale=new Vector3(.3f,5,18);Physics.SyncTransforms();
            Check(!shore.CanBoard() && !shore.TryBoard(),"nearby hull cannot be boarded through wall");
            UnityEngine.Object.DestroyImmediate(wall);Feet(c,new Vector3(247,8,250));Check(!shore.CanBoard(),"cliff above boat is not a boarding point");
            shore.BoardImmediately();UnityEngine.Object.DestroyImmediate(platform);Boat(c,new Vector3(250,.5f,250));Check(!shore.TryDisembark(),"open water does not become a landing");
            var ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);ledge.transform.position=new Vector3(254,4,250);ledge.transform.localScale=new Vector3(4,.3f,5);Physics.SyncTransforms();Check(!shore.TryDisembark(),"high isolated ledge rejected");UnityEngine.Object.DestroyImmediate(ledge);
            // Restore the normal map and test production world interactions at actual resources.
            c.ReturnToHarbor();Boat(c,shore.Sites[0].Berth+Vector3.up*.5f);Check(shore.TryDisembark(),"workshop route starts from original landing");
            Check(!shore.FreeLanding && shore.CanBoard(),"original long ramp still permits boarding");
            var route=shore.Actor.position-c.Player.transform.position;
            var gate=GameObject.CreatePrimitive(PrimitiveType.Cube);gate.name="WORKSHOP TEST named ramp wall";
            gate.transform.position=(shore.Actor.position+c.Player.transform.position)*.5f+Vector3.up*2;
            gate.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(route,Vector3.up));gate.transform.localScale=new Vector3(8,6,.3f);Physics.SyncTransforms();
            Check(!shore.CanBoard() && !shore.TryBoard(),"named-ramp convenience cannot bypass a blocking wall");
            Check(!shore.FindLanding(out _,out var throughGate) || !throughGate,"named landing checks actual transfer route");
            UnityEngine.Object.DestroyImmediate(gate);Check(shore.CanBoard(),"named-ramp boarding recovers after obstacle removal");
            if(c.FirstPerson)c.ToggleView();shore.SyncRider();
            for(int i=0;i<8;i++)yield return null;
            c.ToggleInventory(); // 暂停输入和动画时间，跨帧检查姿态而非同帧的旧蒙皮缓存。
            shore.SyncRider();yield return null;
            Debug.Log("[WORKSHOP] AVATAR firstPerson="+c.FirstPerson+" actor="+shore.Actor.position+" bounds="+skinned[0].bounds+" shadow="+skinned[0].shadowCastingMode+" scale="+skinned[0].transform.lossyScale);
            foreach(var t in character.GetComponentsInChildren<Transform>().Take(6))Debug.Log("[WORKSHOP] MODEL "+t.name+" local="+t.localPosition+" scale="+t.localScale);
            var actual=new Bounds();bool firstVertex=true;
            foreach(var renderer in skinned)
            {
                var baked=new Mesh();renderer.BakeMesh(baked);
                foreach(var vertex in baked.vertices){var v=renderer.transform.TransformPoint(vertex);if(firstVertex){actual=new Bounds(v,Vector3.zero);firstVertex=false;}else actual.Encapsulate(v);}
                UnityEngine.Object.DestroyImmediate(baked);
            }
            Debug.Log("[WORKSHOP] BAKED AVATAR "+actual);
            Check(actual.size.y>1.3f && actual.size.y<2.1f && Vector3.Distance(actual.center,shore.Actor.position+Vector3.up*.9f)<.5f,"animated skin vertices are person-sized and rooted at actor");
            Capture(c,"02-character-standing",shore.Actor.position+new Vector3(2.5f,1.6f,3.1f),shore.Actor.position+Vector3.up*.9f);
            var bones=character.GetComponentsInChildren<Transform>();var torso=bones.First(t=>t.name=="Body");
            var leftAnkle=bones.First(t=>t.name=="Ankle.L");var rightAnkle=bones.First(t=>t.name=="Ankle.R");
            var torsoStanding=torso.position;var leftStanding=leftAnkle.position;var rightStanding=rightAnkle.position;
            c.ToggleInventory();shore.SetCrouch(true);
            float stanceDeadline=Time.unscaledTime+1;
            while(!character.IsStanceSettled(true,true) && Time.unscaledTime<stanceDeadline)yield return null;
            c.ToggleInventory();shore.SyncRider();yield return null;
            Check(Mathf.Abs(torso.position.y-torsoStanding.y+FishingLocomotion.StandingHeight-FishingLocomotion.CrouchingHeight)<.015f,"crouch lowers common torso and thigh parent by the actual capsule height difference");
            Check(Vector3.Distance(leftStanding,leftAnkle.position)<.02f && Vector3.Distance(rightStanding,rightAnkle.position)<.02f,"crouch keeps both ankles attached to independent feet");
            var before=torso.position;
            for(int i=0;i<20;i++){shore.SyncRider();yield return null;}
            Check(Vector3.Distance(before,torso.position)<.015f,"repeated cross-frame pose updates do not accumulate crouch offsets");
            Capture(c,"03-character-crouch",shore.Actor.position+new Vector3(2.5f,1.4f,3.1f),shore.Actor.position+Vector3.up*.7f);
            c.ToggleInventory();shore.SetCrouch(false,true);stanceDeadline=Time.unscaledTime+1;
            while(!character.IsStanceSettled(true,false) && Time.unscaledTime<stanceDeadline)yield return null;
            c.ToggleInventory();shore.SyncRider();yield return null;
            Check(Vector3.Distance(torsoStanding,torso.position)<.015f && Vector3.Distance(leftStanding,leftAnkle.position)<.02f,"standing restores torso and legs after crouch");
            c.ToggleInventory();
            foreach(var item in new[]{FieldItem.Driftwood,FieldItem.Stone,FieldItem.Fiber,FieldItem.Clay,FieldItem.Sand})
            {
                var node=world.Nodes.First(n=>n.Item==item);Feet(c,node.Feet+Vector3.forward*.25f+Vector3.up*.06f);
                world.Sense();int previous=bag.Count(item);string action=world.ActionTitle;
                bool harvested=world.HarvestNearest();
                Debug.Log("[WORKSHOP] HARVEST "+item+" feet="+node.Feet+" actor="+shore.Actor.position+" action="+action+" result="+harvested+" delta="+(bag.Count(item)-previous));
                if(!harvested)
                {
                    var from=shore.Actor.position+Vector3.up*shore.EyeHeight;var delta=node.Feet+Vector3.up*.7f-from;
                    foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore))Debug.Log("[WORKSHOP] OCCLUDER "+hit.collider.name+" at "+hit.point);
                }
                Check(harvested,"world harvest succeeds "+item);Check(bag.Count(item)>previous,"world harvest correct item "+item);
            }
            var ore=world.Nodes.First(n=>n.Item==FieldItem.IronOre);Feet(c,ore.Feet+Vector3.forward*.65f+Vector3.up*.06f);Check(!world.HarvestNearest(),"bare hands cannot mine ore");
            bag.TryAdd(FieldItem.StonePickaxe,1);for(int i=0;i<36;i++)if(bag.At(i).item==FieldItem.StonePickaxe){bag.Move(i,0);break;}bag.Select(0);
            Check(world.HarvestNearest() && bag.Count(FieldItem.IronOre)>0,"equipped pick mines ore");
            var furnace=world.State.Stations.First(s=>s.kind==CraftStation.Furnace);var furnacePos=new Vector3(furnace.x,furnace.y,furnace.z);
            Feet(c,furnacePos+Vector3.back*1.1f);world.Sense();Debug.Log("[WORKSHOP] FURNACE action="+world.ActionTitle+" reach="+world.ReachStation(furnace.id));Check(world.Interact() && c.InventoryOpen && world.Context==CraftStation.Furnace,"E opens actual furnace crafting context");
            bag.TryAdd(FieldItem.Coal,2);Check(world.Smelt("iron",1,FieldItem.Coal),"UI command reserves fuel and ore");
            var progress=world.State.Station(furnace.id).progress;yield return null;yield return null;Check(world.State.Station(furnace.id).progress==progress,"open backpack pauses furnace");
            c.ToggleInventory();for(int i=0;i<8;i++)world.State.Tick(1);
            Check(world.State.Station(furnace.id).ready==1,"furnace produces an ingot after eight simulation seconds");
            world.Interact();Check(world.Collect() && bag.Count(FieldItem.IronIngot)==1,"production collect command moves furnace output into bag");c.ToggleInventory();
            Capture(c,"04-workshop-furnace",furnacePos+new Vector3(2,1.8f,-3),furnacePos+Vector3.up*.7f);
            // Workbench placement consumes the equipped item only after all geometry checks pass.
            bag.TryAdd(FieldItem.Workbench,1);for(int i=0;i<36;i++)if(bag.At(i).item==FieldItem.Workbench){bag.Move(i,0);break;}
            bool placed=false;
            for(int i=0;i<48 && !placed;i++)
            {
                if(!c.FieldWorld.TryLocation(shore.Sites[0].Landing,7+i/8,i*137.5f,out var p))continue;
                Feet(c,p+Vector3.up*.04f);shore.Actor.rotation=Quaternion.Euler(0,i*35,0);c.viewCamera.transform.rotation=shore.Actor.rotation;
                placed=world.PlaceEquipped();
                if(!placed && i%8==0)Debug.Log("[WORKSHOP] PLACE attempt="+i+" item="+bag.Equipped+" reason="+c.Message);
            }
            Check(placed && bag.Count(FieldItem.Workbench)==0,"F places owned workbench on clear ground");
            int own=world.State.Stations.First(s=>!s.permanent).id;world.SelectNearby();
            if(world.SelectedStation!=own){var s=world.State.Station(own);Feet(c,new Vector3(s.x,s.y,s.z)+Vector3.back*1.6f);world.SelectNearby();}
            c.OpenCrafting();bag.TryAdd(FieldItem.Stone,8);Check(world.Context==CraftStation.Workbench && world.Craft("furnace"),"placed bench unlocks furnace recipe");
            var saved=c.Career.Snapshot();Check(FishingCareer.TryRestore(saved,out var restored) && restored.Workshop.Station(own)!=null,"placed station survives career round trip");
            Check(world.Remove() && bag.Count(FieldItem.Workbench)==1,"empty player-owned bench can be recovered exactly once");c.ToggleInventory();
            c.ReturnToHarbor();c.CatchRig.Apply(.02f);stanceDeadline=Time.unscaledTime+1;
            while(!character.IsStanceSettled(false,false) && Time.unscaledTime<stanceDeadline)yield return null;
            c.TogglePause();shore.SyncRider();
            // 冻结救援传送后的浮力过渡，跨渲染帧检查坐姿而非拍摄瞬时船体翻滚。
            for(int i=0;i<8;i++)yield return null;
            Check(!c.Ashore && shore.Actor.InverseTransformPoint(torso.position).y<.7f,"boat rider uses stable seated pose after shore return");
            Capture(c,"05-boat-clear",c.Player.transform.position+new Vector3(5,3,5),c.Player.transform.position+Vector3.up);
            Check(!tray.gameObject.activeSelf,"tray still stowed after shore trip");c.TogglePause();
            var stationCount=world.State.Stations.Count();world.RefreshFromCareer();Check(world.State.Stations.Count()==stationCount,"fixture/profile refresh never duplicates public workstations");
        }
    }
}
