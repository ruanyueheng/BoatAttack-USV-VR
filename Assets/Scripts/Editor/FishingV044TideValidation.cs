using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>V044 守灯值夜 + 潮汐日历：潮汐数学与时刻表、礁石露出/淹没与玩家安全、收集与存档往返、旧档兼容、值夜全流程，截图存 Logs/V044/tide 与 Logs/V044/lighthouse。</summary>
    public static class FishingV044TideValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-TIDE");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-TIDE] CHECK: "+label);}
        private const double Flood=137.5; // acos(1-2*0.30)/2π × 745

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Only the isolated automation profile is used");
            Math();Saves();
            string tide=Path.GetFullPath("Logs/V044/tide"),lighthouse=Path.GetFullPath("Logs/V044/lighthouse");
            Directory.CreateDirectory(tide);Directory.CreateDirectory(lighthouse);
            if(!c.FirstPerson)c.ToggleView();
            var run=GoHome(c);while(run.MoveNext())yield return run.Current;
            run=Flat(c,tide);while(run.MoveNext())yield return run.Current;
            run=Watch(c,lighthouse);while(run.MoveNext())yield return run.Current;
            var data=c.Career.Snapshot();
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(JsonUtility.ToJson(data)),out var live)
                && Enumerable.Range(0,12).All(i=>live.Lore.TideFlag(i)==c.Career.Lore.TideFlag(i)) && live.Lore.KeeperNights==c.Career.Lore.KeeperNights
                && Enumerable.Range(0,12).All(i=>live.Lore.KeeperFlag(i)==c.Career.Lore.KeeperFlag(i)),"Live tide and keeper progress round-trips");
        }

        // ---------- pure rules ----------
        private static void Math()
        {
            var cal=new FishingCalendar();Advance(cal,FishingCalendar.TidePeriod-cal.TidePhase);
            Check(cal.TideLevel<.001f && cal.RisingTide,"Low water starts the rising half");
            Advance(cal,FishingCalendar.TidePeriod/2);Check(cal.TideLevel>.999f && !cal.RisingTide,"Half a tide later is high water, falling");
            Advance(cal,FishingCalendar.TidePeriod/2);Check(cal.TideLevel<.001f,"A full 12h25m tide returns to low water");
            Check(FishingCalendar.TidePeriod*2-1440>45 && FishingCalendar.TidePeriod*2-1440<55,"Low water comes about 50 minutes later each day");
            var a=FishingTide.Table(0);var b=FishingTide.Table(1);
            Check(!a.Contains("00:00–") && !a.Contains("–23:59"),"Midnight-spanning reef window is merged into one span: "+a);
            Check(a!=b && a.Contains("低潮") && a.Contains("礁石小路") && a.Split('–').Length>=3,"Daily timetable lists low waters and reef windows: "+a+" | "+b);
            var w=new FishingCalendar();Advance(w,FishingCalendar.TidePeriod-w.TidePhase);Advance(w,FishingCalendar.TidePeriod-Flood+.5);
            Check(FishingTide.Exposed(w) && !w.RisingTide && double.IsPositiveInfinity(FishingTide.MinutesToFlood(w)),"Ebbing exposure has no flood countdown");
            Advance(w,Flood*2-1);Check(FishingTide.Exposed(w) && System.Math.Abs(FishingTide.MinutesToFlood(w)-.5)<.05,"Flood countdown reaches zero at the threshold");
            Advance(w,1);Check(!FishingTide.Exposed(w),"Path floods once the level passes 0.30");
            int nights=Enumerable.Range(0,300).Count(FishingTide.WatchNight);Check(nights>80 && nights<170,"Watch nights: mist plus about one in three ("+nights+"/300)");
            Check(Enumerable.Range(0,300).Where(d=>FishingCalendar.Forecast(d,3)==BayWeather.Mist).All(FishingTide.WatchNight),"Every misty night is a watch night");
            Check(FishingTide.Names.Length==FishingTide.ItemCount && FishingTide.Lines.Length==FishingTide.ItemCount && FishingTide.Letters.Length==FishingTide.BoatCount && FishingTide.Letters.All(l=>l.Length>40),"Twelve finds and six readable letters");
        }
        private static void Advance(FishingCalendar cal,double minutes)
        {
            while(minutes>1e-6){double step=System.Math.Min(minutes,1380);cal.AdvanceToHour((float)((cal.Minute+step)/60%24));minutes-=step;}
        }
        private static void Saves()
        {
            var career=new FishingCareer();var lore=career.Lore;
            Check(lore.SetTideFlag(3) && !lore.SetTideFlag(3) && !lore.SetTideFlag(12) && lore.SetKeeperFlag(FishingTide.ReadBit+5) && !lore.SetKeeperFlag(12),"Tide and keeper flags set once and stay in range");
            lore.AddKeeperNight();lore.AddKeeperNight();
            var json=JsonUtility.ToJson(career.Snapshot());
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(json),out var back) && back.Lore.TideFlag(3) && back.Lore.KeeperFlag(11) && back.Lore.KeeperNights==2,"Tide/keeper log survives a JSON round trip");
            var bad=JsonUtility.FromJson<FishingSaveData>(json);bad.lore.tideLog=1<<12;Check(!FishingCareer.TryRestore(bad,out _),"Out-of-range tide log is rejected");
            bad=JsonUtility.FromJson<FishingSaveData>(json);bad.lore.keeperLog=1<<22;Check(!FishingCareer.TryRestore(bad,out _),"Out-of-range keeper log is rejected");
            foreach(int version in new[]{19,FishingSaveData.CurrentVersion})
            {
                var legacy=new FishingCareer().Snapshot();legacy.version=version;legacy.coins=77;
                var old=JsonUtility.ToJson(legacy).Replace(",\"tideLog\":0","").Replace(",\"keeperLog\":0","");
                Check(!old.Contains("tideLog") && !old.Contains("keeperLog"),"Fixture v"+version+" really lacks the new fields");
                Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(old),out var up) && up.Coins==77 && !up.Lore.TideFlag(0) && up.Lore.KeeperNights==0,"Save v"+version+" without tide/keeper fields loads empty");
            }
        }

        // ---------- helpers ----------
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        private static IEnumerator GoHome(FishingPrototype c)
        {
            c.ReturnToHarbor();c.SelectNavigation(c.HomeNavigationIndex);
            var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();c.View.ResetLook();
            var f=Frames(25);while(f.MoveNext())yield return null;
            Check(c.Shore.TryDisembark(),"Landed on the home coast");yield return null;
        }
        private static void ToTide(FishingPrototype c,double phase)
        {
            var cal=c.Career.Calendar;double delta=(phase-cal.TidePhase+FishingCalendar.TidePeriod*4)%FishingCalendar.TidePeriod;
            Advance(cal,delta<.01?FishingCalendar.TidePeriod:delta);
        }
        private static IEnumerator Settle(FishingPrototype c){c.TidalFlat.Refresh(false);var f=Frames(4);while(f.MoveNext())yield return null;Physics.SyncTransforms();}
        /// <summary>Walks with real movement input; fails on a stall (input held, under 5 cm/s for over a second).</summary>
        private static IEnumerator Walk(FishingPrototype c,Vector3 target,string leg,bool crouch=false,float reach=.4f)
        {
            float still=0,clock=0;var window=c.Shore.Position;
            for(int frame=0;frame<1200 && FishingPrototype.FlatDistance(c.Shore.Position,target)>reach;frame++)
            {
                var delta=Vector3.ProjectOnPlane(target-c.Shore.Position,Vector3.up);c.viewCamera.transform.rotation=Quaternion.LookRotation(delta.normalized);
                c.Shore.Move(Vector2.up,false,false,crouch,Mathf.Min(.05f,delta.magnitude/2.6f));yield return null;
                clock+=.05f;
                if(clock>=.25f){still=FishingPrototype.FlatDistance(window,c.Shore.Position)<.0125f?still+clock:0;window=c.Shore.Position;clock=0;}
                Check(still<=1,"No stall on "+leg+" at "+c.Shore.Position);
            }
            Check(!c.Shore.Swimming && FishingPrototype.FlatDistance(c.Shore.Position,target)<=reach+.15f,"Walked "+leg+" / "+c.Shore.Position);
        }
        private static void Shot(FishingPrototype c,string dir,string name,Vector3 eye,Vector3 target)
        {
            var cam=c.viewCamera.transform;cam.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            c.Weather.RefreshForCapture();FishingV016Validation.Capture(c,dir,name);FishingV016Validation.Capture(c,dir,name); // second render: water RT is warm
        }
        private static IEnumerator Sky(FishingPrototype c,BayWeather weather)
        {
            float until=Time.time+15;while(c.Weather.Climate.Current!=weather && !c.Weather.Climate.Select(weather) && Time.time<until)yield return null;
            c.Weather.Climate.Hold();Check(c.Weather.Climate.Current==weather,"Weather set to "+weather);
            var f=Frames(10);while(f.MoveNext())yield return null;
        }
        private static IEnumerator Blend(){float until=Time.time+12.5f;while(Time.time<until)yield return null;}

        // ---------- low-tide reef ----------
        private static IEnumerator Flat(FishingPrototype c,string dir)
        {
            var flat=c.TidalFlat;var lore=c.Career.Lore;
            File.WriteAllBytes(Path.Combine(dir,"wet-sand-texture.png"),flat.SandTexture.EncodeToPNG());
            Check(flat.Triangles<=30000,"Reef area within the 30k triangle budget ("+flat.Triangles+")");
            var sky=Sky(c,BayWeather.Clear);while(sky.MoveNext())yield return null;
            // Daytime low water so the shots are readable.
            for(int i=0;i<4 && (c.Career.Calendar.Hour<9 || c.Career.Calendar.Hour>15);i++)ToTide(c,8);
            var s=Settle(c);while(s.MoveNext())yield return null;
            Check(FishingTide.Exposed(c.Career.Calendar) && flat.Solid && Mathf.Abs(flat.Root.position.y-FishingTidalFlat.Top)<.01f,"Low water: reef is up and solid (hour "+c.Career.Calendar.Hour.ToString("F1")+")");
            Check(FishingSeaEventWorld.OpenWater(flat.Islet+Vector3.forward*12,3),"Reef ends in open water, away from other islands");
            Shot(c,dir,"tide-low-overview",flat.ShoreStart+new Vector3(-14,9,-4),flat.PathPoint(18));
            Check(c.Shore.Relocate(flat.ShoreStart,Quaternion.identity),"Beach start of the reef path is standable");
            var w=Walk(c,flat.PathPoint(6)+(flat.Item(0).position-flat.PathPoint(6))*.2f,"beach → first shell");while(w.MoveNext())yield return null;
            Check(c.WorldInteractionTitle!=null && c.WorldInteractionTitle.Contains(FishingTide.Names[0]),"Hint offers the shell: "+c.WorldInteractionTitle);
            Check(c.RequestWorldInteraction() && lore.TideFlag(0) && !flat.Item(0).gameObject.activeSelf,"E picks up the shell and records it");
            foreach(float z in new[]{12f,18f,24f,30f})
            {
                w=Walk(c,flat.PathPoint(z),"reef path z="+z);while(w.MoveNext())yield return null;
                Check(flat.OnFlat && c.Shore.Position.y>FishingTidalFlat.Top-.2f,"Standing on the exposed reef at z="+z);
            }
            c.View.ResetLook();Shot(c,dir,"tide-path-walk",c.Shore.Position+Vector3.up*1.6f-Vector3.forward*.2f,flat.Islet+Vector3.up*.3f);
            w=Walk(c,flat.Islet,"path → islet");while(w.MoveNext())yield return null;
            // Crabs bolt from a standing walker; crouching lets you close in.
            int crab=FishingTide.ShellCount;
            for(int frame=0;frame<600 && !(c.WorldInteractionTitle??"").Contains(FishingTide.Names[crab]);frame++)
            {
                var d=Vector3.ProjectOnPlane(flat.Item(crab).position-c.Shore.Position,Vector3.up);if(d.magnitude>.6f){c.viewCamera.transform.rotation=Quaternion.LookRotation(d.normalized);c.Shore.Move(Vector2.up,false,false,true,.05f);}
                yield return null;
            }
            Check((c.WorldInteractionTitle??"").Contains(FishingTide.Names[crab]),"Crouching player closes in on the crab: "+c.WorldInteractionTitle);
            // External shots need the full third-person body; the first-person mesh is cut above the shoulders.
            c.ToggleView();c.PresentCharacterFrame();
            var mid=(flat.Item(crab).position+c.Shore.Position)*.5f;Debug.Log("[V044-TIDE] crab shot first person="+c.FirstPerson);
            Shot(c,dir,"tide-crab",mid+new Vector3(-2.2f,1.5f,-2.2f),mid+Vector3.up*.45f);
            c.ToggleView();
            Check(c.RequestWorldInteraction() && lore.TideFlag(crab),"E catches the crab for the codex");
            int page=FishingTide.ShellCount+FishingTide.CrabCount;
            Shot(c,dir,"tide-wreck",flat.Item(page).position+new Vector3(-2.6f,1.6f,-2.2f),flat.Item(page).position);
            Shot(c,dir,"tide-pools",flat.Islet+new Vector3(-4,2.4f,-3.5f),flat.Islet+new Vector3(0,0,.8f));
            var feet=flat.Item(page).position;feet.y=flat.Root.position.y;
            Check(c.Shore.Relocate(flat.Root.TransformPoint(new Vector3(-1.6f,0,35.8f)),Quaternion.identity),"Standable beside the wreck");
            w=Walk(c,feet,"islet → wreck page",false,.35f);while(w.MoveNext())yield return null;
            Check((c.WorldInteractionTitle??"").Contains(FishingTide.Names[page]) && c.RequestWorldInteraction() && lore.TideFlag(page),"Grandfather's tide table page found in the wreck");
            for(int i=0;i<FishingTide.ItemCount;i++)if(!lore.TideFlag(i))flat.Collect(i);
            Check(Enumerable.Range(0,12).All(lore.TideFlag),"All twelve finds collectable");
            yield return null;Check(Enumerable.Range(0,12).All(i=>!flat.Item(i).gameObject.activeSelf),"Collected finds do not come back");

            // Flood: warn while on the reef, then walk the player back to the beach before the rocks go soft.
            w=Walk(c,flat.PathPoint(26),"islet → path");while(w.MoveNext())yield return null;
            ToTide(c,Flood-30);s=Settle(c);while(s.MoveNext())yield return null;
            Check(flat.Warnings==1 && c.VisibleMessage.Contains("潮水在涨"),"Rising tide warns a player on the reef: "+c.VisibleMessage);
            Check(flat.OnFlat && flat.Solid && !c.Shore.Swimming,"Sinking reef still carries the walker");
            Shot(c,dir,"tide-rising",c.Shore.Position+new Vector3(-6,3.5f,-4),c.Shore.Position);
            int rescues=flat.Rescues;
            ToTide(c,Flood+2);s=Settle(c);while(s.MoveNext())yield return null;
            Check(flat.Rescues==rescues+1 && !flat.OnFlat && !flat.Solid && !c.Shore.Swimming && FishingPrototype.FlatDistance(c.Shore.Position,flat.ShoreStart)<1.5f,"Flood returns the player to the beach, dry: "+c.Shore.Position);
            var f=Frames(30);while(f.MoveNext())yield return null;
            Check(c.Ashore && !c.Shore.Swimming && Mathf.Abs(c.Shore.Position.y-flat.ShoreStart.y)<.6f,"Player stays put on the beach after the rescue");
            ToTide(c,FishingCalendar.TidePeriod/2);s=Settle(c);while(s.MoveNext())yield return null;
            Check(flat.Root.position.y<FishingTidalFlat.Top-FishingTidalFlat.Sink+.01f,"High water: the reef sinks fully under the sea");
            Shot(c,dir,"tide-high-overview",flat.ShoreStart+new Vector3(-14,9,-4),flat.PathPoint(18));
            Debug.Log("[V044-TIDE] Table today: "+FishingTide.Today(c.Career.Calendar));
        }

        // ---------- lamp watch ----------
        private static int NextDay(FishingPrototype c,Func<int,bool> pick){int d=c.Career.Calendar.Day+1;while(!pick(d))d++;return d;}
        private static IEnumerator ToNight(FishingPrototype c,int day,float hour)
        {
            var cal=c.Career.Calendar;while(cal.Day<day)Advance(cal,System.Math.Min(1380,(day-cal.Day)*1440-cal.Minute+1));
            cal.AdvanceToHour(hour);var f=Frames(6);while(f.MoveNext())yield return null;
        }
        private static IEnumerator Watch(FishingPrototype c,string dir)
        {
            var k=c.Keeper;var lore=c.Career.Lore;var cal=c.Career.Calendar;
            Check(k.Triangles<=30000,"Gallery, ladder and boats within the 30k triangle budget ("+k.Triangles+")");
            FishingKeeperWorld.ForceUnlock=false;cal.AdvanceToHour(20);var f=Frames(4);while(f.MoveNext())yield return null;
            Check(!k.WatchActive,"No watch before the player has taken over the lamp");
            FishingKeeperWorld.ForceUnlock=true;
            try
            {
                cal.AdvanceToHour(12);f=Frames(3);while(f.MoveNext())yield return null;
                Shot(c,dir,"lighthouse-day",k.Axis+Quaternion.Euler(0,k.LadderYaw+25,0)*Vector3.forward*17+Vector3.up*6,k.Axis+Vector3.up*(k.DeckY-2));
                int day=NextDay(c,d=>FishingTide.WatchNight(d) && FishingCalendar.Forecast(d,3)!=BayWeather.Mist);
                var n=ToNight(c,day,17.5f);while(n.MoveNext())yield return null;
                Check(c.Shore.Relocate(k.DeckPoint(90,2.6f),Quaternion.identity),"Gallery deck standable at dusk");f=Frames(3);while(f.MoveNext())yield return null;
                Check(k.Timelapse && Mathf.Approximately(c.Weather.TimeScale,FishingKeeperWorld.TimelapseScale),"Waiting on the gallery at dusk runs the sky forward");
                Shot(c,dir,"lighthouse-dusk",k.Axis+Quaternion.Euler(0,k.LadderYaw+40,0)*Vector3.forward*40+Vector3.up*14,k.Axis+Vector3.up*(k.DeckY-2));
                cal.AdvanceToHour(20);f=Frames(4);while(f.MoveNext())yield return null;
                Check(!k.Timelapse && Mathf.Approximately(c.Weather.TimeScale,1) && k.WatchActive,"Nightfall ends the timelapse and starts the watch");
                n=ToNight(c,day,20);while(n.MoveNext())yield return null;
                var sky=Sky(c,BayWeather.Clear);while(sky.MoveNext())yield return null;
                Check(k.WatchActive && k.Boats.Count>=1 && k.Boats.All(b=>FishingSeaEventWorld.OpenWater(b.Anchor,5) && FishingPrototype.FlatDistance(b.Anchor,k.Axis)<=125),"Watch night: "+k.Boats.Count+" late boats on open water within beam range");
                Check(!c.Nightscape.LighthouseSweep.gameObject.activeInHierarchy,"Lamp stays dark until the keeper lights it");

                // Climb the real ladder.
                var foot=k.Ladder.Bottom;
                Check(c.Shore.Relocate(foot,Quaternion.LookRotation(Vector3.ProjectOnPlane(k.Axis-foot,Vector3.up))),"Ladder foot is standable");
                Check(c.WorldInteractionTitle!=null && c.WorldInteractionTitle.Contains("攀登"),"Ladder offers the climb: "+c.WorldInteractionTitle);
                Check(c.RequestWorldInteraction() && c.Shore.Climbing,"E grips the lighthouse ladder");
                Shot(c,dir,"lighthouse-ladder",foot+Quaternion.Euler(0,k.LadderYaw,0)*new Vector3(2.5f,2.2f,4.5f),foot+Vector3.up*3);
                for(int i=0;i<600 && c.Shore.Climbing;i++){c.Shore.Move(Vector2.up,false,false,false,.05f);yield return null;}
                Check(!c.Shore.Climbing && Mathf.Abs(c.Shore.Position.y-k.DeckY)<.3f,"Climbed onto the gallery deck: "+c.Shore.Position);
                foreach(float a in new[]{60f,120f,180f,240f,300f,355f})
                {var w=Walk(c,k.DeckPoint(a,2.6f),"gallery ring "+a);while(w.MoveNext())yield return null;}
                Check(Mathf.Abs(c.Shore.Position.y-k.DeckY)<.3f,"Still on the gallery after a full lap");

                Check(!k.Work(FishingKeeperWorld.Chore.Wipe,5) && !k.SetManning(true),"Chores go in order: oil first");
                var go=Walk(c,k.DeckPoint(55,2.45f),"to oil can",false,.3f);while(go.MoveNext())yield return null;
                Check((c.WorldInteractionTitle??"").Contains("添灯油") && c.RequestWorldInteraction(),"Oil can offers hold-E: "+c.WorldInteractionTitle);
                Shot(c,dir,"lighthouse-oil",c.Shore.Position+Vector3.up*1.6f,k.Station(FishingKeeperWorld.Chore.Oil).position);
                Check(k.Work(FishingKeeperWorld.Chore.Oil,FishingKeeperWorld.OilSeconds) && k.Oil>=1,"Oil filled");
                go=Walk(c,k.DeckPoint(115,2.45f),"to cloth bucket",false,.3f);while(go.MoveNext())yield return null;
                Check((c.WorldInteractionTitle??"").Contains("擦拭"),"Bucket offers the wipe: "+c.WorldInteractionTitle);
                Shot(c,dir,"lighthouse-salt",c.Shore.Position+Vector3.up*1.6f,k.Axis+Vector3.up*(k.DeckY+.8f));
                Check(k.Work(FishingKeeperWorld.Chore.Wipe,FishingKeeperWorld.WipeMetres) && k.Lit && lore.KeeperNights==1,"Lens wiped, lamp lit, night counted");
                f=Frames(3);while(f.MoveNext())yield return null;
                Check(c.Nightscape.LighthouseSweep.gameObject.activeInHierarchy,"Beam is back once the lamp is lit");
                go=Walk(c,k.DeckPoint(180,2.4f),"to lens crank",false,.3f);while(go.MoveNext())yield return null;
                Check(c.RequestWorldInteraction() && k.Manning,"E takes the crank");

                // Aim with the view, exactly as the desktop player does.
                int coins=c.Career.Coins;
                foreach(var boat in k.Boats)
                {
                    float timeout=Time.time+30;
                    while(!boat.Guided && Time.time<timeout)
                    {
                        var to=Vector3.ProjectOnPlane(boat.Hull.position-k.Axis,Vector3.up);c.viewCamera.transform.rotation=Quaternion.LookRotation(to);
                        yield return null;
                    }
                    Check(boat.Guided,"Held the beam on "+FishingTide.Boats[boat.Id]+" until it turned for home");
                    if(boat==k.Boats[0])
                    {
                        var eye=k.Axis+Vector3.up*(k.DeckY+1.7f)+Vector3.ProjectOnPlane(boat.Hull.position-k.Axis,Vector3.up).normalized*2.7f;
                        Shot(c,dir,"lighthouse-beam-on-boat",eye,boat.Hull.position+Vector3.up*2);
                        Shot(c,dir,"lighthouse-night-wide",k.Axis+Quaternion.Euler(0,k.LadderYaw+40,0)*Vector3.forward*40+Vector3.up*14,k.Axis+Vector3.up*(k.DeckY-2));
                    }
                }
                float until=Time.time+60;while(k.Boats.Any(b=>!b.Arrived) && Time.time<until)yield return null;
                Check(k.Boats.All(b=>b.Arrived),"Every guided boat sails home");
                var first=k.Boats[0].Id;Check(lore.KeeperFlag(FishingTide.ArrivedBit+first) && k.Parcel(first).gameObject.activeSelf,"Parcel waits on the home jetty");
                Check(c.RequestWorldInteraction() && !k.Manning,"E releases the crank");

                // Down the ladder and over to the jetty.
                go=Walk(c,k.Ladder.Top,"to the ladder gate",false,.5f);while(go.MoveNext())yield return null;
                Check(c.RequestWorldInteraction() && c.Shore.Climbing,"E grips the ladder at the top");
                for(int i=0;i<600 && c.Shore.Climbing;i++){c.Shore.Move(Vector2.down,false,false,false,.05f);yield return null;}
                Check(!c.Shore.Climbing && FishingPrototype.FlatDistance(c.Shore.Position,k.Ladder.Bottom)<.5f,"Climbed down to the foot of the ladder");
                var parcel=k.Parcel(first);
                Check(c.Shore.Relocate(c.Home.BoardwalkStart,Quaternion.identity),"Jetty landing standable");
                go=Walk(c,parcel.position,"to the parcel",false,1.0f);while(go.MoveNext())yield return null;
                Shot(c,dir,"lighthouse-parcel-night",parcel.position+new Vector3(-1.3f,1.2f,-1.3f),parcel.position);
                Check((c.WorldInteractionTitle??"").Contains(FishingTide.Boats[first]) && c.RequestWorldInteraction() && c.Letter.Open && lore.KeeperFlag(FishingTide.ReadBit+first),"Parcel opens the skipper's letter");
                Check(c.Career.Coins==coins+FishingTide.ParcelReward+FishingTide.GuideReward,"Guided boat pays the guide bonus");
                FishingV016Validation.Capture(c,dir,"lighthouse-letter");c.Letter.Close();yield return null;
                Check(!parcel.gameObject.activeSelf,"Read parcel is gone");

                // Misty night left alone: boats come home late, letters still arrive, no bonus.
                int mistDay=NextDay(c,d=>FishingTide.WatchNight(d) && FishingCalendar.Forecast(d,3)==BayWeather.Mist);
                n=ToNight(c,mistDay,21);while(n.MoveNext())yield return null;
                sky=Sky(c,BayWeather.Mist);while(sky.MoveNext())yield return null;var bl=Blend();while(bl.MoveNext())yield return null;
                Check(k.WatchActive && k.Boats.Count==3,"Mist brings three late boats (active="+k.WatchActive+" boats="+k.Boats.Count+" spots="+k.SpawnSpots(mistDay,3).Count+")");
                var lamp=k.Boats[0].Hull.position;
                Shot(c,dir,"lighthouse-mist-lamps",k.Axis+Vector3.up*(k.DeckY+1.7f)+Vector3.ProjectOnPlane(lamp-k.Axis,Vector3.up).normalized*2.7f,lamp+Vector3.up*2);
                var ids=k.Boats.Select(b=>b.Id).ToList();
                cal.AdvanceToHour(5.5f);f=Frames(4);while(f.MoveNext())yield return null;
                Check(!k.WatchActive && ids.All(id=>lore.KeeperFlag(FishingTide.ArrivedBit+id)),"Unlit misty night: boats still make harbour by dawn");
                Check(c.VisibleMessage.Contains("天亮才摸回"),"Dawn note explains the late boats: "+c.VisibleMessage);
                cal.AdvanceToHour(10);f=Frames(4);while(f.MoveNext())yield return null;
                var late=k.Parcel(ids[0]);Check(late.gameObject.activeSelf,"Late boat's parcel waits on the jetty in daylight");
                Shot(c,dir,"lighthouse-parcel",late.position+new Vector3(-1.6f,1.4f,-1.8f),late.position+Vector3.up*.1f);
                Shot(c,dir,"lighthouse-parcel-wide",c.Home.BoardwalkStart+new Vector3(-4,3.2f,-4.5f),c.Home.BoardwalkStart);
                sky=Sky(c,BayWeather.Clear);while(sky.MoveNext())yield return null;
            }
            finally{FishingKeeperWorld.ForceUnlock=false;}
        }
    }
}
