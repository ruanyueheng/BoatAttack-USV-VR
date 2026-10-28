using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Walks every island's authored landing route, resident and portal approach with real movement input
    /// and records each stall (input held, speed ≈ 0 for over a second) with the nearest blocking collider.</summary>
    public static class FishingV044WalkAudit
    {
        public static void Run()=>FishingV011Validation.Begin("V044-WALK");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-WALK] CHECK: "+label);}
        private sealed class Stall{public int Island;public string Leg;public Vector3 Position;public float Seconds;public string Blocker,Collider="";public int Escapes;}

        public static IEnumerator Checks(FishingPrototype c)
        {
            string directory=Path.GetFullPath("Logs/V044/walk");Directory.CreateDirectory(directory);
            var stalls=new List<Stall>();int legs=0;
            if(!c.FirstPerson)c.ToggleView();
            for(int island=0;island<4;island++)
            {
                c.SelectFieldSlot(8);c.ReturnToHarbor();yield return null;
                var berth=FishingIslandWorld.Berth(island)+Vector3.up;var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=berth;rb.rotation=Quaternion.identity;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);
                for(float until=Time.time+.5f;Time.time<until;)yield return null;
                Check(c.Shore.TryDisembark(),"Physical landing "+island);yield return null;
                if(island==0){var crouch=CrouchShots(c);while(crouch.MoveNext())yield return null;}
                var targets=new List<(string name,Vector3 point)>();
                var art=c.Islands.WorldRoot.GetChild(island).Find(FishingV035Art.ArtRoot);
                if(art!=null && art.Find("Island approach")!=null)
                    targets.AddRange(art.Find("Island approach").Cast<Transform>().OrderBy(t=>int.Parse(t.name.Substring(6))).Select(t=>(t.name,t.position)));
                targets.Add(("resident",c.Residents.Person(island).position+Vector3.forward*1.8f));
                targets.Add(("portal approach",c.Realms.PortalApproach(island)));
                targets.Add(("back to landing",FishingIslandWorld.Landing(island)));
                foreach(var (name,point) in targets)
                {
                    legs++;var walk=Walk(c,island,name,point,stalls);while(walk.MoveNext())yield return null;
                }
            }
            var ranked=stalls.OrderByDescending(s=>s.Seconds).ToList();
            File.WriteAllLines(Path.Combine(directory,"stalls.csv"),new[]{"island,leg,x,y,z,seconds,blocker,free_directions,collider_fit"}.Concat(ranked.Select(s=>s.Island+","+s.Leg+","+s.Position.x.ToString("F2")+","+s.Position.y.ToString("F2")+","+s.Position.z.ToString("F2")+","+s.Seconds.ToString("F2")+","+s.Blocker+","+s.Escapes+","+s.Collider.Replace(',',' '))));
            Debug.Log("[V044-WALK] Walked "+legs+" legs, "+stalls.Count+" stalls → "+Path.Combine(directory,"stalls.csv"));
            foreach(var s in ranked.Take(10))Debug.Log("[V044-WALK] STALL island="+s.Island+" leg="+s.Leg+" at="+s.Position.ToString("F2")+" seconds="+s.Seconds.ToString("F2")+" blocker="+s.Blocker+" free="+s.Escapes+"/8 fit="+s.Collider);
            Check(legs>0,"Walk audit covered the island routes");
            Check(stalls.All(s=>s.Escapes>0),"Every blocked route lets the player escape in at least one direction");
        }

        /// <summary>Crouch idle, crouch walk and crouched reach, seen from four sides in third person and once first person.</summary>
        private static IEnumerator CrouchShots(FishingPrototype c)
        {
            string directory=Path.GetFullPath("Logs/V044/can/crouch");Directory.CreateDirectory(directory);
            var walkTo=c.Shore.Position+c.Shore.Actor.forward*3;
            foreach(var pose in new[]{"idle","walk","reach"})
            {
                for(int f=0;f<40;f++)
                {
                    var delta=Vector3.ProjectOnPlane(walkTo-c.Shore.Position,Vector3.up);
                    if(pose=="walk" && delta.sqrMagnitude>.04f)c.viewCamera.transform.rotation=Quaternion.LookRotation(delta.normalized);
                    c.Shore.Move(pose=="walk"?Vector2.up:Vector2.zero,false,false,true,.03f);
                    if(pose=="reach")c.Hands.Right.SetPose(c.Shore.Position+c.Shore.Actor.forward*.45f+c.Shore.Actor.right*.12f+Vector3.up*.05f,Quaternion.Euler(70,c.Shore.Actor.eulerAngles.y,0),.6f);
                    yield return null;
                }
                c.PresentCharacterFrame();
                Debug.Log("[V044-WALK] CROUCH "+pose+" crouched="+c.Shore.Crouched+" hips="+(c.Shore.Character.transform.position-c.Shore.Position).ToString("F3"));
                FishingV016Validation.Capture(c,directory,pose+"-firstperson");
                if(c.FirstPerson)c.ToggleView();c.PresentCharacterFrame();
                var eye=c.viewCamera.transform;var keep=eye.position;var keepRot=eye.rotation;var centre=c.Shore.Position+Vector3.up*.6f;
                for(int a=0;a<4;a++)
                {
                    eye.position=centre+Quaternion.Euler(0,c.Shore.Actor.eulerAngles.y+a*90,0)*Vector3.forward*2.1f+Vector3.up*.35f;eye.LookAt(centre);
                    FishingV016Validation.Capture(c,directory,pose+"-view"+a*90);
                }
                eye.SetPositionAndRotation(keep,keepRot);if(!c.FirstPerson)c.ToggleView();yield return null;
            }
            for(int f=0;f<20;f++){c.Shore.Move(Vector2.zero,false,false,false,.03f);yield return null;}
        }
        private static IEnumerator Walk(FishingPrototype c,int island,string leg,Vector3 target,List<Stall> stalls)
        {
            float still=0,clock=0;var window=c.Shore.Position;bool logged=false;
            for(int frame=0;frame<1500 && FishingPrototype.FlatDistance(c.Shore.Position,target)>.3f;frame++)
            {
                var delta=Vector3.ProjectOnPlane(target-c.Shore.Position,Vector3.up);c.viewCamera.transform.rotation=Quaternion.LookRotation(delta.normalized);
                c.Shore.Move(Vector2.up,false,false,false,Mathf.Min(.05f,delta.magnitude/2.6f));yield return null;
                clock+=Time.deltaTime;
                if(clock>=.25f)
                {
                    // Speed ≈ 0 means under 5 cm/s over the sampling window while forward input is held.
                    still=FishingPrototype.FlatDistance(window,c.Shore.Position)<.0125f?still+clock:0;window=c.Shore.Position;clock=0;
                    if(still>1 && !logged){stalls.Add(new Stall{Island=island,Leg=leg,Position=c.Shore.Position,Seconds=still,Blocker=Blocker(c)});logged=true;}
                    else if(logged && still>0)stalls[stalls.Count-1].Seconds=still;
                    if(still>3)break;
                    if(still==0)logged=false;
                }
            }
            if(logged || (stalls.Count>0 && stalls[stalls.Count-1].Leg==leg && stalls[stalls.Count-1].Island==island && FishingPrototype.FlatDistance(stalls[stalls.Count-1].Position,c.Shore.Position)<.3f))
            {var escape=Escape(c,stalls[stalls.Count-1]);while(escape.MoveNext())yield return null;}
            // A stuck leg must not hide the rest of the route: continue from the target when it is standable.
            if(FishingPrototype.FlatDistance(c.Shore.Position,target)>.6f)c.Shore.Relocate(target,c.Shore.Actor.rotation);
        }
        /// <summary>Blocked (like walking into a wall) is fine; stuck means no direction frees the capsule.</summary>
        private static IEnumerator Escape(FishingPrototype c,Stall stall)
        {
            var origin=c.Shore.Position;var face=c.Shore.Actor.rotation;
            if(c.FirstPerson){c.ToggleView();c.PresentCharacterFrame();yield return null;FishingV016Validation.Capture(c,Path.GetFullPath("Logs/V044/walk"),"stall-"+stall.Island+"-"+stall.Leg.Replace(' ','_'));c.ToggleView();}
            for(int d=0;d<8;d++)
            {
                c.Shore.Relocate(origin-Vector3.up*.08f,face);for(int f=0;f<3;f++)yield return null;
                var dir=Quaternion.Euler(0,d*45,0)*Vector3.forward;c.viewCamera.transform.rotation=Quaternion.LookRotation(dir);
                for(float t=0;t<1;t+=Time.deltaTime){c.viewCamera.transform.rotation=Quaternion.LookRotation(dir);c.Shore.Move(Vector2.up,false,false,false,.05f);yield return null;}
                if(FishingPrototype.FlatDistance(origin,c.Shore.Position)>.3f)stall.Escapes++;
            }
            stall.Collider=ColliderFit(c,origin);
            Debug.Log("[V044-WALK] ESCAPE island="+stall.Island+" leg="+stall.Leg+" freeDirections="+stall.Escapes+"/8 collider="+stall.Collider);
        }
        /// <summary>Flags invisible walls: a blocking collider whose bounds exceed its own visible renderers.</summary>
        private static string ColliderFit(FishingPrototype c,Vector3 feet)
        {
            foreach(var collider in Physics.OverlapCapsule(feet+Vector3.up*.35f,feet+Vector3.up*1.4f,.6f,~(1<<4),QueryTriggerInteraction.Ignore))
            {
                if(collider.transform.IsChildOf(c.Shore.Actor) || collider.name.StartsWith("Walkable"))continue;
                var renderers=collider.GetComponentsInChildren<Renderer>();if(renderers.Length==0)renderers=collider.GetComponentsInParent<Renderer>();
                if(renderers.Length==0)return collider.name+" has no visible model";
                var visible=renderers[0].bounds;foreach(var r in renderers)visible.Encapsulate(r.bounds);
                var excess=collider.bounds.extents-visible.extents;
                return collider.name+" excess="+excess.ToString("F2");
            }
            return "none";
        }
        private static string Blocker(FishingPrototype c)
        {
            var feet=c.Shore.Position;string best="none";float nearest=float.MaxValue;
            foreach(var collider in Physics.OverlapCapsule(feet+Vector3.up*.35f,feet+Vector3.up*1.4f,.6f,~(1<<4),QueryTriggerInteraction.Ignore))
            {
                if(collider.transform.IsChildOf(c.Shore.Actor) || collider.name.StartsWith("Walkable"))continue;
                float d=Vector3.Distance(collider.ClosestPoint(feet+Vector3.up*.8f),feet+Vector3.up*.8f);
                if(d<nearest){nearest=d;best=(collider.name+" / "+collider.transform.parent?.name).Replace(',',' ');}
            }
            return best;
        }
    }
}
