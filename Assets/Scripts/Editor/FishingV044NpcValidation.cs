using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>[Claude v0.44] NPC behaviour, look-at limits, talk turn/return, voice-line integrity and per-activity pose captures (Logs/V044/npc).</summary>
    public static class FishingV044NpcValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-NPC");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-NPC] CHECK: "+label);}
        private static IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Only the isolated automation profile is used");
            // Fixed 09:00 and no clock-driven moves, so checks never straddle a schedule boundary.
            c.Career.Calendar.AdvanceToHour(9);c.Residents.FreezeSchedule=true;
            foreach(int id in new[]{0,1,2,3,5})c.Residents.SendTo(id,"home",true); // drop any move queued before the clock was set
            string evidence=Path.GetFullPath("Logs/V044/npc");Directory.CreateDirectory(evidence);
            var clipNames=Resources.LoadAll<AnimationClip>("FishingV044/Residents/ResidentClips").Select(x=>x.name).ToList();
            foreach(var name in new[]{"Idle","Talk","Fix","Water","Plant","Harvest","PickUp","Chest","Lantern","FoldArms","Drink","Nod","Interact","Walk"})Check(clipNames.Contains(name),"Resident clip imported "+name);
            // Voice lines: every spoken table entry has exactly one decodable mono clip, and no orphan clip exists.
            var spoken=FishingResidentLines.All.Where(p=>!p.Key.Contains("/opt_")).ToList();
            foreach(var line in spoken)
            {
                var clip=FishingResidentLines.Clip(line.Key);
                Check(clip!=null && clip.LoadAudioData() && clip.loadState==AudioDataLoadState.Loaded && clip.channels==1 && clip.length>.6f && clip.length<30,"Voice decodes for "+line.Key);
                Check(FishingResidentLines.VoiceKey(line.Value)==line.Key,"Subtitle text maps back to its own recording "+line.Key);
            }
            int clips=0;foreach(var id in new[]{0,1,2,3,4,5,6})clips+=Resources.LoadAll<AudioClip>("FishingV044/Voice/"+id).Length;
            Check(clips==spoken.Count,"One recording per line ("+clips+" clips / "+spoken.Count+" lines)");
            Check(FishingResidents.StationAt(0,9,false)=="home" && FishingResidents.StationAt(1,13,false)=="break" && FishingResidents.StationAt(5,20,false)=="dock"
                && FishingResidents.StationAt(3,17,true)=="festival","Daily schedule: work, lunch break, Lin Zhou lights the dock lamp, festival evening gathering");
            foreach(int id in new[]{0,1,2,3,5})Check(FishingResidentLines.Has(id,"fest") && FishingResidentLines.Has(id,"chat_1"),"Festival and chat lines exist "+id);
            foreach(var k in new[]{"hail_1","hail_2","deal_1","deal_2","bye","season_0","season_3"})Check(FishingResidentLines.Clip("6/"+k)!=null,"Merchant voice "+k);
            Check(FishingResidentLines.Stage(c.Career.Lore)==0,"A fresh or pre-v044 save starts at the first chapter without new save fields");
            foreach(int id in new[]{0,1,2,3,5})
            {
                var npc=c.Residents.Person(id);var brain=c.Residents.Brain(id);var animator=npc.GetComponentInChildren<Animator>();
                Check(brain!=null && animator!=null && animator.isHuman && animator.cullingMode==AnimatorCullingMode.AlwaysAnimate,"Resident is an animated humanoid "+id);
                var group=npc.GetComponentInChildren<LODGroup>();Check(group!=null && group.lodCount==2,"Resident keeps near/far LODs "+id);
                foreach(var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Check(skin.sharedMesh.triangles.Length/3<=15000,"PICO triangle budget "+id+" "+skin.name+" "+skin.sharedMesh.triangles.Length/3);
                    var tex=skin.sharedMaterial.GetTexture("_BaseMap");Check(tex!=null && tex.width<=1024 && skin.sharedMaterial.GetTexture("_BumpMap")!=null,"Resident atlas <=1024 "+id);
                }
            }
            foreach(int id in new[]{0,1,2,3,5})
            {
                var land=Land(c,id);while(land.MoveNext())yield return land.Current;
                var npc=c.Residents.Person(id);var brain=c.Residents.Brain(id);var home=brain.Home;var animator=npc.GetComponentInChildren<Animator>();
                // Both feet point forward (sum) in the resident's own idle: the visual front matches the facing used for turns.
                brain.Play("Idle",0,3);var idle=Wait(.3f);while(idle.MoveNext())yield return null;
                // Chest facing (shoulder line × up) matched the face-on captures; it must agree with the body's facing.
                var shoulders=animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position-animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                Check(brain.Oriented && Vector3.Dot(Vector3.Cross(shoulders,Vector3.up).normalized,npc.forward)>.7f,"Visual front matches the resident's facing "+id);

                // Look-at only: the player stands at 40° and 100° off the resident's front, then directly behind.
                foreach(var angle in new[]{40f,100f,175f})
                {
                    var stand=Stand(c,npc,angle,2.3f);while(stand.MoveNext())yield return stand.Current;
                    var w=Wait(1.6f);while(w.MoveNext())yield return null;
                    Check(Quaternion.Angle(npc.rotation,home)<1,"Body does not swivel toward a passer-by "+id+" at "+angle+"°");
                    Check(Mathf.Abs(brain.HeadYaw)<=FishingResidentBrain.MaxYaw+.5f,"Head yaw stays within the neck limit "+id+" yaw="+brain.HeadYaw);
                    if(angle<90)Check(brain.LookWeight>.5f && Mathf.Abs(brain.HeadYaw)>10,"Head turns to notice the player "+id);
                    if(angle>170){var r=Wait(1);while(r.MoveNext())yield return null;Check(brain.LookWeight<.05f,"No look-at behind the resident "+id);}
                    if(angle<90)Shot(c,npc,evidence,"lookat-"+id);
                }
                bool opened=false;
                foreach(var bearing in new[]{25f,0,-25,50,-50})
                {
                    var near=Stand(c,npc,bearing,1.8f);while(near.MoveNext())yield return near.Current;
                    if(opened=c.Residents.Talk(id))break;
                    Debug.Log("[V044-NPC] talk blocked at "+bearing+"° inRange="+c.Residents.InRange(id)+" story="+c.StoryBlocksGameplay);
                }
                Check(opened,"Talk opens "+id);
                Check(c.Residents.SpeechVoiceKey!=null,"Greeting is voiced "+id);
                var talk=Wait(2.2f);while(talk.MoveNext())yield return null;
                var toPlayer=Vector3.ProjectOnPlane(c.Shore.Position-npc.position,Vector3.up);
                Check(Vector3.Angle(npc.forward,toPlayer)<12,"Resident turns to face the speaker while talking "+id);
                Check(c.Residents.VoiceSource(id).isPlaying || c.Residents.VoiceSource(id).time>0 || brain.Current=="Idle","Voice source played the line "+id);
                Shot(c,npc,evidence,"talk-"+id);
                c.Residents.Choose(0);c.Residents.Reveal();Check(c.Residents.SpeechVoiceKey!=null,"Story beat is voiced "+id);
                c.Residents.Choose(0);Check(c.Residents.Asking && c.Residents.Choice(0)!=c.Residents.Choice(1),"Second story ask offers a reply choice "+id);
                c.Residents.Choose(0);Check(!c.Residents.Asking && c.Residents.SpeechVoiceKey==id+"/ask_r1","Reply gets a voiced reaction "+id);
                c.Residents.Close();
                var back=Wait(4.5f);while(back.MoveNext())yield return null;
                Check(Quaternion.Angle(npc.rotation,home)<2,"Resident returns to the original facing after the talk "+id);
                Check(brain.Current!="Talk","Resident resumes its own activity "+id+" ("+brain.Current+")");
                brain.ShowBark("bark_1");var bark=Wait(.6f);while(bark.MoveNext())yield return null;
                Check(brain.BubbleVisible,"Bark bubble shows "+id);Shot(c,npc,evidence,"bark-"+id);
                // Pose captures for every clip the resident can play (limb/clipping review).
                foreach(var clip in new[]{"Idle","Talk","Fix","Water","Plant","Harvest","PickUp","Chest","Lantern","FoldArms","Drink","Nod","Interact"})
                {
                    brain.Play(clip,0,5);var hold=Wait(clip=="PickUp" || clip=="Chest"?.5f:clip=="Fix" || clip=="Water" || clip=="Plant" || clip=="Harvest"?2.6f:.9f);while(hold.MoveNext())yield return null;
                    Check(brain.Current==clip && brain.ClipTime>.2f,"Clip plays on the resident "+id+" "+clip);
                    Shot(c,npc,evidence,"pose-"+id+"-"+clip);
                }
                brain.Play("Idle",.2f,3);
                // Schedule stations: visible moves walk a clear straight line, otherwise they wait for an off-screen jump.
                c.Residents.SendTo(id,"break",false);bool walked=false;float until=Time.time+15;
                while(Time.time<until && brain.Station=="break" && (brain.Walking || Vector3.Distance(npc.position,c.ExplorerPosition)<30)){walked|=brain.Walking;if(walked && !brain.Walking)break;yield return null;}
                Check(!walked || !brain.Walking,"A visible schedule walk reaches its spot "+id);
                foreach(var station in id==5?new[]{"break","dock","festival"}:new[]{"break","festival"})
                {
                    c.Residents.SendTo(id,station,true);yield return null;var settle=Wait(1.2f);while(settle.MoveNext())yield return null;
                    Check(OnlySelf(c,npc),"Station spot is clear of props "+id+" "+station);
                    if(station=="dock")
                        Check(FishingPrototype.FlatDistance(npc.position,c.TackleShop.LandingPosition)<3
                            && Vector3.Dot(brain.Home*Vector3.forward,Vector3.ProjectOnPlane(c.TackleShop.mooringPoint.position-npc.position,Vector3.up).normalized)>.9f,
                            "Lin Zhou watches the actual grounded landing and faces the mooring");
                    Shot(c,npc,evidence,"station-"+id+"-"+station);
                }
                c.Residents.SendTo(id,"home",true);yield return null;
            }
            for(int i=0;i<4;i++){c.Career.CompleteShrine(i);c.Career.ClaimShrineReward(i);}
            var harbor=Land(c,5);while(harbor.MoveNext())yield return harbor.Current;
            bool open5=false;
            foreach(var bearing in new[]{25f,0,-25,50,-50}){var stand5=Stand(c,c.Residents.Person(5),bearing,1.8f);while(stand5.MoveNext())yield return stand5.Current;if(open5=c.Residents.Talk(5))break;}
            Check(open5 && c.Residents.Speech.StartsWith(FishingResidentLines.Text(5,"greet_s3")),"Chapter-aware greeting after all four echoes");
            c.Residents.Close();
            // Festival evening in the scene: everyone gathers, one pair chats, wide and close shots.
            c.Residents.FreezeSchedule=true;
            var gathering=new[]{0,1,2,3,5};
            foreach(int id in gathering)c.Residents.SendTo(id,"festival",false);
            for(int i=0;i<gathering.Length;i++)for(int j=i+1;j<gathering.Length;j++)
                Check(FlatDistance(c.Residents.Brain(gathering[i]).ScheduledFeet,c.Residents.Brain(gathering[j]).ScheduledFeet)>=1.4f,
                    "Queued festival destinations are reserved before residents move "+gathering[i]+" / "+gathering[j]);
            foreach(int id in gathering)c.Residents.SendTo(id,"festival",true);
            var gather=Wait(1.5f);while(gather.MoveNext())yield return null;
            var ring=new[]{0,1,2,3,5}.Select(i=>c.Residents.Person(i)).ToArray();var centre=ring.Aggregate(Vector3.zero,(v,t)=>v+t.position)/ring.Length;
            for(int i=0;i<ring.Length;i++)for(int j=i+1;j<ring.Length;j++)Check(FlatDistance(ring[i].position,ring[j].position)>=1.2f,"Festival spots keep residents apart "+ring[i].name+" / "+ring[j].name);
            foreach(var t in ring)Check(OnlySelf(c,t),"Festival spot clear of props "+t.name);
            c.Residents.StartChat(0);var chat=Wait(.8f);while(chat.MoveNext())yield return null;
            Check(c.Residents.Brain(0).BubbleVisible && c.Residents.VoiceSource(0).isPlaying,"Festival chat: first speaker is voiced with a bubble");
            var wide=centre+new Vector3(5,2.6f,5);c.viewCamera.transform.SetPositionAndRotation(wide,Quaternion.LookRotation(centre+Vector3.up-wide));
            FishingV016Validation.Capture(c,evidence,"festival-wide");
            var reply=Wait(FishingResidentLines.Clip("0/chat_1").length+.6f);while(reply.MoveNext())yield return null;
            Check(c.Residents.Brain(1).BubbleVisible,"Festival chat: the partner replies");
            Shot(c,c.Residents.Person(1),evidence,"festival-reply",1.8f);
            foreach(int id in new[]{0,1,2,3,5})c.Residents.SendTo(id,"home",true);
            Debug.Log("[V044-NPC] evidence "+evidence);
        }
        private static IEnumerator Land(FishingPrototype c,int id)
        {
            if(c.Ashore)c.Shore.BoardImmediately();
            c.ReturnToHarbor();yield return null;
            if(id<4)
            {
                var p=FishingIslandWorld.Berth(id)+Vector3.up;var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=p;rb.rotation=Quaternion.identity;
                c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
            }
            var w=Wait(.5f);while(w.MoveNext())yield return null;
            Check(c.Shore.TryDisembark(),"Landing near resident "+id);yield return null;
        }
        private static IEnumerator Stand(FishingPrototype c,Transform npc,float angle,float distance)
        {
            // Props and slopes block some spots: search ±20° and closer rings around the requested bearing.
            foreach(var jitter in new[]{0f,10,-10,20,-20})
            for(float d=distance;d>1.2f;d-=.3f)
            {
                var dir=Quaternion.Euler(0,angle+jitter,0)*npc.GetComponent<FishingResidentBrain>().Home*Vector3.forward;
                var seed=npc.position+dir*d+Vector3.up*.3f;
                bool g=c.Shore.Ground(seed,out var feet);                if(g && Mathf.Abs(feet.y-npc.position.y)<.8f && c.Shore.Relocate(feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(npc.position-feet,Vector3.up))))
                {yield return null;yield break;}
            }
            Check(false,"Supported standing spot near "+npc.name+" at "+angle+"°");
        }
        private static bool OnlySelf(FishingPrototype c,Transform npc)
        {
            foreach(var col in Physics.OverlapCapsule(npc.position+Vector3.up*.45f,npc.position+Vector3.up*1.5f,.22f,~(1<<4|1<<5),QueryTriggerInteraction.Ignore))
                if(!col.transform.IsChildOf(npc) && !col.transform.IsChildOf(c.Shore.Actor) && col!=c.Shore.Body && !FishingShore.NonSolidSurface(col)){Debug.Log("[V044-NPC] overlap "+npc.name+" with "+col.name);return false;}
            return true;
        }
        private static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        private static bool Blocked(Transform npc,Vector3 from,Vector3 to)
        {
            foreach(var h in Physics.RaycastAll(from,(to-from).normalized,(to-from).magnitude,~(1<<4|1<<5),QueryTriggerInteraction.Ignore))
                if(!h.transform.IsChildOf(npc))return true;
            return false;
        }
        // Half-body framing: chest-to-head fills ~60% of the frame, camera nearly face-on (own collider ignored).
        private static void Shot(FishingPrototype c,Transform npc,string evidence,string label,float distance=1.25f)
        {
            var a=npc.GetComponentInChildren<Animator>();var head=a.GetBoneTransform(HumanBodyBones.Head).position;var hips=a.GetBoneTransform(HumanBodyBones.Hips).position;
            var target=Vector3.Lerp(hips,head,.6f);var forward=npc.forward;var eye=target+Quaternion.Euler(0,12,0)*forward*distance+Vector3.up*.12f;
            for(int k=0;k<6 && Blocked(npc,eye,target);k++)eye=target+Quaternion.Euler(0,12+(k%2==0?1:-1)*(k/2+1)*20,0)*forward*distance+Vector3.up*.12f;
            c.viewCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            var hidden=c.Shore.Actor.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToList();foreach(var r in hidden)r.enabled=false;
            FishingV016Validation.Capture(c,evidence,label);
            foreach(var r in hidden)r.enabled=true;
        }

    }
}
