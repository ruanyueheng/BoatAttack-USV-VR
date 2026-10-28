using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV044RealmValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-REALMS");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-REALMS] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Save tests only write an isolated profile");
            Rules();Saves();
            string evidence=Path.GetFullPath("Logs/V044/realms");Directory.CreateDirectory(evidence);Directory.CreateDirectory(evidence+"/hazards");Directory.CreateDirectory("Logs/V038");
            // Full production playthroughs with hazards live first, so the targeted checks below also cover cleared-realm replays.
            for(int id=0;id<3;id++)
            {
                c.Realms.HazardsEnabled=true;
                var run=FishingV035Levels.Island(c,id,evidence+"/hazards");while(run.MoveNext())yield return run.Current;
                Check(c.Career.Lore.Cleared(id) && c.Career.Lore.RealmStage(id)==FishingLore.MaxStage(id),"Hazard-on playthrough clears realm "+id);
            }
            for(int id=0;id<4;id++){var run=Realm(c,id,evidence);while(run.MoveNext())yield return run.Current;}
        }
        private static void Rules()
        {
            for(int v=0;v<3;v++)
            {
                var t=new FishingShrineTrial(1,v);
                Check(Enumerable.Range(0,3).Select(t.MirrorTarget).Distinct().Count()==3 && Enumerable.Range(0,3).All(i=>t.MirrorTarget(i)>0),"Forest variant "+v+" never starts aligned");
                var s=new FishingShrineTrial(3,v);
                Check(Enumerable.Range(0,3).All(i=>Mathf.Abs(s.TargetYaw(i))<=75 && s.TargetPitch(i)>=10 && s.TargetPitch(i)<=70),"Star variant "+v+" stays inside the telescope limits");
            }
            Check(new FishingShrineTrial(1,1).MirrorTarget(0)!=new FishingShrineTrial(1,0).MirrorTarget(0) && new FishingShrineTrial(3,1).TargetYaw(0)!=FishingShrineTrial.StarTargetYaw(0),"Variants change the answers");
            Check(new FishingShrineTrial(0,0,1).WindRound==1 && new FishingShrineTrial(0,0,2).Ready,"Wind checkpoints restore round and completion");
            Check(new FishingShrineTrial(1,0,1).LightStage==1 && new FishingShrineTrial(2,0,1).Ready && new FishingShrineTrial(3,0,2).StarStage==2,"Forest, tide and star checkpoints restore");
            var tide=new FishingShrineTrial(2){LeakLimit=5};
            Check(tide.Use(1),"Open the drain");for(int i=0;i<45;i++)tide.Tick(.1f);
            Check(tide.WaterLevel<=.15f && !tide.Flooding,"Hold drains before the pump stalls");
            for(int i=0;i<42;i++)tide.PushCargo(.1f);float pushed=tide.CargoProgress;
            for(int i=0;i<60;i++)tide.Tick(.1f);
            Check(pushed>.5f && tide.Flooding && tide.CargoProgress==.5f && tide.State(1)==0 && tide.WaterLevel>.15f,"Stalled pump floods and the cargo keeps its half-way chock");
            Check(!tide.PushCargo(.1f),"Flooded hold blocks pushing");
            Check(tide.Use(1) && !tide.Flooding && tide.DrySeconds==0,"Re-opening the drain recovers without losing progress");
            var star=new FishingShrineTrial(3){FocusSeconds=1};
            star.Observe(star.TargetYaw(0),star.TargetPitch(0),.1f,true);for(int i=0;i<10;i++)star.Observe(star.TargetYaw(0),star.TargetPitch(0),.1f,true);
            Check(star.StarStage==1,"Night focus time is honoured");
        }
        private static void Saves()
        {
            var career=new FishingCareer();var lore=career.Lore;
            Check(lore.SaveRealmStage(1,1) && !lore.SaveRealmStage(1,1) && !lore.SaveRealmStage(2,2) && lore.RealmStage(1)==1,"Stages only move forward and within limits");
            Check(career.CollectRealmShell(0,0)==0 && career.CollectRealmShell(0,0)==-1 && career.CollectRealmShell(0,1)==0,"Shells are collected once");
            int coins=career.Coins;Check(career.CollectRealmShell(0,2)==30 && career.Coins==coins+30,"Completing a realm's shells pays once");
            Check(lore.RealmVariant(3,()=>2)==2 && lore.RealmVariant(3,()=>0)==2,"Variant is chosen once per save");
            var data=career.Snapshot();var json=JsonUtility.ToJson(data);
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(json),out var back) && back.Lore.RealmStage(1)==1 && back.Lore.ShellCount(0)==3 && back.Lore.RealmVariant(3,()=>0)==2,"Realm progress survives a JSON round trip");
            var bad=JsonUtility.FromJson<FishingSaveData>(json);bad.lore.realmStages=3<<4;
            Check(!FishingCareer.TryRestore(bad,out _),"Tide stage above its maximum is rejected");
            var forged=JsonUtility.FromJson<FishingSaveData>(json);forged.version=19;
            Check(!FishingCareer.TryRestore(forged,out _),"Pre-v20 saves cannot carry realm progress");
            var sea=JsonUtility.FromJson<FishingSaveData>(JsonUtility.ToJson(new FishingCareer().Snapshot()));sea.version=19;sea.lore.seaLog=1;
            Check(!FishingCareer.TryRestore(sea,out _),"Pre-v20 saves cannot carry sea-event log");
            sea.lore.tideLog=1;Check(!FishingCareer.TryRestore(sea,out _),"Pre-v20 saves cannot carry tide finds");sea.lore.keeperLog=1<<22;sea.version=20;Check(!FishingCareer.TryRestore(sea,out _),"Keeper log above its limit is rejected");sea.lore.keeperLog=(3<<12)|1;
            Check(FishingCareer.TryRestore(sea,out var seaBack) && seaBack.Lore.SeaFlag(0) && seaBack.Lore.TideFlag(0) && seaBack.Lore.KeeperFlag(0) && seaBack.Lore.KeeperNights==3,"v20 sea, tide and keeper logs restore");
            var legacy=new FishingCareer().Snapshot();legacy.version=19;legacy.coins=55;legacy.lore.shrines=1;
            var old=JsonUtility.ToJson(legacy).Replace(",\"realmStages\":0,\"realmShells\":0,\"realmVariants\":0","").Replace(",\"seaLog\":0","").Replace(",\"tideLog\":0,\"keeperLog\":0","").Replace(",\"photoLog\":0","").Replace(",\"seaPost\":0,\"seaPostDay\":0","");
            Check(!old.Contains("realmStages") && !old.Contains("seaPost") && !old.Contains("photoLog") && !old.Contains("keeperLog"),"Fixture really lacks the v20 fields");
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(old),out var upgraded) && upgraded.Coins==55 && upgraded.Lore.Cleared(0) && upgraded.Lore.RealmStage(0)==FishingLore.MaxStage(0) && upgraded.Lore.RealmStage(1)==0,"Schema19 save loads; cleared realms count as complete, others start fresh");
        }
        private static IEnumerator Enter(FishingPrototype c,int id)
        {
            c.SelectFieldSlot(8);c.ReturnToHarbor();yield return null;
            var p=FishingIslandWorld.Berth(id)+Vector3.up;var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=p;rb.rotation=Quaternion.identity;c.Player.transform.SetPositionAndRotation(p,Quaternion.identity);Physics.SyncTransforms();
            float until=Time.time+.5f;while(Time.time<until)yield return null;
            Check(c.Shore.TryDisembark(),"Landing "+id);yield return null;
            Check(c.Shore.Relocate(c.Realms.PortalApproach(id),Quaternion.identity) && c.Realms.Enter(id),"Production entry "+id);
            while(c.Realms.Transitioning)yield return null;yield return null;yield return null;
            c.Realms.HazardsEnabled=true;yield return null;yield return null;
        }
        private static IEnumerator Leave(FishingPrototype c){c.Realms.Abort();yield return null;}
        private static IEnumerator Realm(FishingPrototype c,int id,string evidence)
        {
            var lore=c.Career.Lore;
            if(id==3)lore.SaveRealmStage(3,2);
            var enter=Enter(c,id);while(enter.MoveNext())yield return enter.Current;
            var r=c.Realms;
            if(id==3)Check(r.Trial.StarStage==2 && !r.Trial.Ready,"Re-entering resumes the saved star stage");
            foreach(var prop in r.Room.GetComponentsInChildren<Transform>().Where(t=>t.name=="GustBanner" || t.name=="TideGauge" || t.name=="GrabCrank" || t.name.StartsWith("Echo shell",StringComparison.Ordinal)))
            {
                var rs=prop.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var x in rs)b.Encapsulate(x.bounds);
                var hits=Physics.OverlapBox(b.center+Vector3.up*.06f,Vector3.Max(b.extents-Vector3.one*.03f,Vector3.one*.01f),Quaternion.identity,~(1<<4),QueryTriggerInteraction.Ignore).Where(x=>!x.transform.IsChildOf(prop) && x!=c.Shore.Body).ToArray();
                Check(hits.Length==0,"V044 prop "+prop.name+" does not interpenetrate room collision "+id+(hits.Length>0?" (hits "+hits[0].name+")":""));
            }
            for(int i=0;i<3;i++)r.RequestHint();
            Check(c.VisibleMessage.Contains("方向提示") && !c.VisibleMessage.Contains("1、2、3") && !c.VisibleMessage.Contains("星 → 风"),"Third hint gives direction, not the full answer "+id);
            FishingV035Levels.CaptureViews(c,id,evidence);
            CheckClean(evidence,id);
            int pick=Enumerable.Range(0,3).Where(k=>!lore.ShellFound(id,k) && !(id==1 && k==2)).DefaultIfEmpty(-1).First();
            if(pick<0){Debug.Log("[V044-REALMS] All shells of "+id+" were already found during the playthrough");}
            var shellSave=pick;if(pick<0)pick=0;var shell=r.Shell(id,pick);Check(shell!=null && (shellSave<0 || shell.gameObject.activeSelf),"Echo shell placed "+id);
            Look(c,shell.position,new Vector3(1.6f,1.3f,-1.6f));FishingV016Validation.Capture(c,evidence,"v044-shell-"+id);
            if(id==0)
            {
                Look(c,r.Device(0).position+Vector3.up*1.2f,new Vector3(3,2,-3));FishingV016Validation.Capture(c,evidence,"v044-gust-banner");
                lore.SaveRealmStage(0,2);var leave=Leave(c);while(leave.MoveNext())yield return null;
                enter=Enter(c,0);while(enter.MoveNext())yield return enter.Current;
                Check(r.Trial.Ready || lore.Cleared(0),"Saved melody stage reopens the bridges");
                float until=Time.time+1.6f;while(Time.time<until)yield return null;
                var bridge=r.Room.Find("WindBridge1");var deck=bridge.GetComponentsInChildren<Collider>().Where(x=>x.enabled).Select(x=>x.bounds).OrderByDescending(b=>b.size.x*b.size.z).First();
                Check(Physics.Raycast(new Vector3(deck.center.x,deck.max.y+2,deck.center.z),Vector3.down,out var top,4,~(1<<4),QueryTriggerInteraction.Ignore) && top.transform.IsChildOf(bridge)
                    && c.Shore.Relocate(top.point,Quaternion.identity),"Stand on a raised wind bridge");
                c.Shore.SetCrouch(true,true);
                while(!r.GustActive)yield return null;var braced=c.Shore.Position;until=Time.time+1.5f;while(Time.time<until)yield return null;
                Check(FishingPrototype.FlatDistance(braced,c.Shore.Position)<.1f,"Crouching braces against the gust");
                c.Shore.SetCrouch(false,true);
                while(r.GustActive)yield return null;while(!r.GustActive)yield return null;var exposed=c.Shore.Position;until=Time.time+1.5f;while(Time.time<until)yield return null;
                Look(c,c.Shore.Position+Vector3.up,new Vector3(3,3,-4));FishingV016Validation.Capture(c,evidence,"v044-gust");
                Check(FishingPrototype.FlatDistance(exposed,c.Shore.Position)>.3f || c.Shore.Position.y<exposed.y-.5f,"An unbraced gust pushes the explorer");
                until=Time.time+4;while(Time.time<until)yield return null;
                Check(c.Shore.Relocate(new Vector3(deck.center.x,top.point.y,deck.center.z),Quaternion.identity),"Back on the bridge for the comfort branch");
                var bridgeFeet=c.Shore.Position;
                Check(r.ComfortGustReturn() && FishingInkTransition.Playing,"VR comfort gust starts the ink transition instead of a hard cut");
                Check(FishingPrototype.FlatDistance(c.Shore.Position,bridgeFeet)<.05f,"Nothing moves before the ink covers the view");
                until=Time.time+3;while((FishingInkTransition.Playing) && Time.time<until)yield return null;
                Check(r.WindBridgeUnderfoot()<0 && FishingPrototype.FlatDistance(c.Shore.Position,bridgeFeet)>.5f,"After the ink the explorer stands on the last pad, off the bridge");
                Check(r.Inside && (r.Trial.Ready || lore.Cleared(0)) && lore.RealmStage(0)==2,"Being blown off never loses the solved melody");
            }
            if(id==1)
            {
                Look(c,r.Device(0).position+Vector3.up*1.1f,new Vector3(1.4f,.6f,-1.4f));FishingV016Validation.Capture(c,evidence,"v044-mirror-crank");
                Check(!r.Shell(1,2).gameObject.activeSelf,"Shaded shell stays hidden without a lantern");
            }
            if(id==2)
            {
                Check(r.Trial.LeakLimit<1000,"Hazards arm the tide-stalled pump inside the game");
                Look(c,r.Device(1).position+Vector3.up*1.2f,new Vector3(2.4f,1,-2.4f));FishingV016Validation.Capture(c,evidence,"v044-tide-gauge");
            }
            if(shellSave>=0)
            {
            int coins=c.Career.Coins;
            Check(c.Shore.Relocate(shell.position,Quaternion.identity),"Walk onto echo shell "+id);
            for(int i=0;i<5;i++)yield return null;
            Check(lore.ShellFound(id,pick) && !shell.gameObject.activeSelf && (c.Career.Coins==coins || (lore.ShellCount(id)==3 && c.Career.Coins==coins+30)),"Shell is collected by walking over it "+id);
            }
            var leaveRealm=Leave(c);while(leaveRealm.MoveNext())yield return null;
        }
        /// <summary>Guards against the intermittent black-streak frame: overview sky/sea must not contain near-black pixels.</summary>
        private static void CheckClean(string evidence,int id)
        {
            var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(Path.Combine(evidence,"sanctuary-"+id+"-overview.png")));
            int dark=0;var px=image.GetPixels32();for(int i=0;i<px.Length;i+=7)if(px[i].r+px[i].g+px[i].b<12)dark++;
            UnityEngine.Object.DestroyImmediate(image);
            Check(dark*7<px.Length/300,"Overview "+id+" has no stray black geometry ("+dark*7+" dark px)");
        }
        private static void Look(FishingPrototype c,Vector3 target,Vector3 offset)
        {
            c.PresentCharacterFrame();var cam=c.viewCamera.transform;var eye=target+c.Realms.Room.rotation*offset;
            cam.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
        }
    }
}
