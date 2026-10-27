using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingFeedbackP3Validation
    {
        public static void Run(){FishingInteractionAnimationBaker.Bake();FishingV011Validation.Begin("FEEDBACK-P3");}
        public static void RunMotion(){FishingInteractionAnimationBaker.Bake();FishingV011Validation.Begin("FEEDBACK-MOTION");}
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[FEEDBACK-P3] CHECK: "+text);}
        public static IEnumerator Checks(FishingPrototype c)=>ChecksCore(c,true);
        public static IEnumerator MotionChecks(FishingPrototype c)=>ChecksCore(c,false);
        private static IEnumerator ChecksCore(FishingPrototype c,bool includeRealms)
        {
            Check(c.Automated && c.HasIsolatedProfile,"P3 uses an isolated acceptance profile");
            var clips=Resources.LoadAll<AnimationClip>("FishingCharacter/CompatibleInteractions");
            Check(clips.Any(x=>x.name.EndsWith("Interact")) && clips.Any(x=>x.name.EndsWith("HitRecieve")),"Real CC0 interaction and reaction animation clips are imported");
            Check(c.Shore.Character.InteractionAnimationReady,"The supplied character loads the matching interaction clips");
            var model=c.Shore.Character.transform.GetChild(0);
            foreach(var clip in clips.Where(x=>!x.name.StartsWith("__")))
            {
                var bindings=AnimationUtility.GetCurveBindings(clip);
                var missing=bindings.Where(x=>x.path.Length>0 && model.Find(x.path)==null).Select(x=>x.path).Distinct().ToArray();
                Check(bindings.Length>0 && missing.Length==0,"Imported action binds the existing skeleton: "+clip.name+"; missing="+string.Join(",",missing));
            }
            var voice=Resources.Load<AudioClip>("FishingV036/OrchardOuch");
            Check(voice!=null && voice.length>.4f && voice.length<1.5f,"The licensed human reaction replaces the synthetic orchard clip");
            Check(c.Home.StorageLid!=null && c.Home.StorageLid.GetComponent<MeshFilter>()!=null,"Home chest preserves its authored separate hinged cover");
            var output=Path.GetFullPath("Logs/FeedbackP3");Directory.CreateDirectory(output);
            foreach(var name in new[]{"Axe","Hammer","Rake","Saw","Shovel"})
            {
                var source=Resources.Load<GameObject>("FishingFeedbackTools/"+name);Check(source!=null,"Tool resource is imported: "+name);
                var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;var colors=mesh.colors32;
                Check(colors.Length==mesh.vertexCount && colors.Distinct().Count()>1,"Unity receives actual palette vertex colours: "+name);
                Check(mesh.bounds.size.y>mesh.bounds.size.x && mesh.bounds.size.y>mesh.bounds.size.z && mesh.bounds.Contains(new Vector3(0,.1f,0)),"Shared tool mesh has metre Y axis and an in-bounds grip: "+name+" "+mesh.bounds);
            }
            c.ReturnToHarbor();
            typeof(FishingPrototype).GetMethod("PrepareAcceptanceHome",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{FishingAcceptanceCase.HomeInterior});yield return null;
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(3.1f,.2f,-1.35f),Quaternion.Euler(0,90,0)),"Reach the storage chest from supported floor");
            var lid=c.Home.StorageLid;var rest=lid.localRotation;var centre=lid.GetComponent<MeshFilter>().sharedMesh.bounds.center;float closedHeight=lid.TransformPoint(centre).y;
            Check(c.Home.InteractAt(6) && c.InventoryOpen,"Production storage interaction starts opening its original cover");
            float until=Time.unscaledTime+.35f;while(Time.unscaledTime<until)yield return null;
            Check(Quaternion.Angle(rest,lid.localRotation)>5 && Quaternion.Angle(rest,lid.localRotation)<104,"Chest cover passes through an intermediate pose while its inventory is open");
            Check(lid.TransformPoint(centre).y>closedHeight,"Home lid swings outward and upward from its actual hinge");
            CaptureChest(c,output,"home-chest-opening");
            until=Time.unscaledTime+.6f;while(Time.unscaledTime<until)yield return null;
            Check(Quaternion.Angle(rest,lid.localRotation)>100,"Storage cover reaches its open pose");
            c.ToggleInventory();until=Time.unscaledTime+.9f;while(Time.unscaledTime<until)yield return null;
            Check(Quaternion.Angle(rest,lid.localRotation)<1,"Closing storage restores the authored closed cover");
            if(c.FirstPerson)c.ToggleView();c.SelectFieldSlot(8);c.FieldWorld.ClearToolPresentation();
            Check(c.Shore.Relocate(c.Home.Origin+new Vector3(0,.2f,-2),Quaternion.Euler(0,180,0)),"Interaction clip has an unobstructed standing demonstration point");
            until=Time.unscaledTime+.25f;while(Time.unscaledTime<until){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}
            var arm=c.Shore.Character.GetComponentsInChildren<Transform>().First(x=>x.name=="UpperArm.R");
            var ankle=c.Shore.Character.GetComponentsInChildren<Transform>().First(x=>x.name=="Ankle.R");
            var idleArm=arm.rotation;var foot=ankle.position;c.Shore.Character.PlayInteraction();
            until=Time.unscaledTime+.4f;while(Time.unscaledTime<until){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}c.PresentCharacterFrame();
            Debug.Log("[FEEDBACK-P3] ACTION DIAGNOSTIC grounded="+c.Shore.Grounded+" speed="+c.Shore.WalkSpeed+" paused="+c.Paused+" first="+c.FirstPerson+" armAngle="+Quaternion.Angle(idleArm,arm.rotation));
            foreach(AnimationState state in c.Shore.Character.GetComponentInChildren<Animation>())if(state.weight>.001f)Debug.Log("[FEEDBACK-P3] ACTION CLIP "+state.name+" weight="+state.weight+" time="+state.time);
            Check(Quaternion.Angle(idleArm,arm.rotation)>8,"The actual 9S arm performs the imported interaction after final hand presentation");
            Check(Vector3.Distance(foot,ankle.position)<.025f,"Interaction blending keeps the standing foot planted");
            var head=c.Shore.Character.GetComponentsInChildren<Transform>().First(x=>x.name=="Head");
            Check(head.position.y-ankle.position.y>1.3f,"Imported interaction keeps the full character upright rather than rotating its FBX root");
            CaptureCharacter(c,output,"same-rig-interaction");
            c.Shore.Character.PlayInteraction(true);until=Time.unscaledTime+.2f;while(Time.unscaledTime<until)yield return null;c.PresentCharacterFrame();
            CaptureCharacter(c,output,"same-rig-reaction");
            foreach(string action in new[]{"Interact","HitRecieve","Punch_Right"})
            {
                if(action=="Punch_Right")c.Shore.Character.PlayToolInteraction();else c.Shore.Character.PlayInteraction(action=="HitRecieve");
                float started=Time.unscaledTime,length=clips.First(x=>x.name==action).length,minHead=float.MaxValue,maxFoot=0,minShoulder=float.MaxValue;int frames=0;bool captured=false;
                var planted=ankle.position;
                while(Time.unscaledTime<started+length+.2f)
                {
                    c.Shore.Move(Vector2.zero,false,false,false,.05f);c.PresentCharacterFrame();frames++;
                    minHead=Mathf.Min(minHead,head.position.y-ankle.position.y);maxFoot=Mathf.Max(maxFoot,Vector3.Distance(planted,ankle.position));
                    minShoulder=Mathf.Min(minShoulder,c.Shore.Character.ShoulderPosition(false).y-ankle.position.y);
                    if(!captured && Time.unscaledTime-started>=Mathf.Min(.36f,length*.5f)){captured=true;CaptureCharacter(c,output,"motion-whole-"+action);}
                    yield return null;
                }
                Check(frames>5 && minHead>1.3f && minShoulder>.9f && maxFoot<.025f,"Whole action including contact remains upright and planted: "+action+"; frames="+frames+" minHead="+minHead+" minShoulder="+minShoulder+" foot="+maxFoot);
            }
            if(!includeRealms)yield break;
            foreach(int island in new[]{0,1})
            {
                var run=FishingV035Levels.Island(c,island,output);
                while(run.MoveNext())yield return run.Current;
            }
        }
        private static void CaptureCharacter(FishingPrototype c,string output,string name)
        {
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;var actor=c.Shore.Actor;
            try
            {
                var eye=actor.position+new Vector3(-1.8f,1.4f,-2.4f);
                camera.SetPositionAndRotation(eye,Quaternion.LookRotation(actor.position+Vector3.up*.9f-eye));
                FishingV016Validation.Capture(c,output,name);
            }
            finally{camera.SetPositionAndRotation(position,rotation);}
        }
        private static void CaptureChest(FishingPrototype c,string output,string name)
        {
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;var chest=c.Home.StorageChest;
            try
            {
                var eye=chest.position+new Vector3(-1.8f,1.4f,-1.8f);
                camera.SetPositionAndRotation(eye,Quaternion.LookRotation(chest.position+Vector3.up*.6f-eye));
                FishingV016Validation.Capture(c,output,name);
            }
            finally{camera.SetPositionAndRotation(position,rotation);}
        }
    }
}
