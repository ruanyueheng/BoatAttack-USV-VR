using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>V044 海上事件：调度确定性/冷却/资格、奖励、存档往返与旧档兼容、VR 无屏幕弹窗，并给每个事件截图（Logs/V044/sea）。</summary>
    public static class FishingV044SeaValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V044-SEA");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V044-SEA] CHECK: "+label);}
        private static SeaConditions Day=>new SeaConditions{AtSea=true};
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"Only the isolated automation profile is used");
            Rules();Saves();
            string evidence=Path.GetFullPath("Logs/V044/sea");Directory.CreateDirectory(evidence);
            var run=Play(c,evidence);while(run.MoveNext())yield return run.Current;
        }

        private static List<(float time,SeaEventKind kind)> Simulate(int seed,SeaConditions cond,float seconds)
        {
            var s=new FishingSeaSchedule(seed);var log=new List<(float,SeaEventKind)>();
            for(float t=.5f;t<=seconds;t+=.5f){var k=s.Tick(.5f,cond);if(k.HasValue)log.Add((t,k.Value));}
            return log;
        }
        private static void Rules()
        {
            var a=Simulate(9,Day,7200);var b=Simulate(9,Day,7200);
            Check(a.Count>3 && a.SequenceEqual(b),"Same seed and conditions give the same events ("+a.Count+" in two hours)");
            Check(!Simulate(10,Day,7200).SequenceEqual(a),"A different seed gives a different schedule");
            Check(a[0].time>=FishingSeaSchedule.FirstDelay,"No event in the first minute at sea");
            for(int i=1;i<a.Count;i++)Check(a[i].time-a[i-1].time>=FishingSeaSchedule.GlobalCooldown,"Global cooldown between events "+i);
            foreach(SeaEventKind k in Enum.GetValues(typeof(SeaEventKind)))
            {
                var times=a.Where(e=>e.kind==k).Select(e=>e.time).ToList();
                for(int i=1;i<times.Count;i++)Check(times[i]-times[i-1]>=FishingSeaSchedule.Cooldown(k),"Per-kind cooldown "+k);
            }
            Check(a.All(e=>e.kind==SeaEventKind.Shoal || e.kind==SeaEventKind.Whales || e.kind==SeaEventKind.Bottle),"Clear days only bring shoals, whales and bottles");
            Check(Simulate(9,new SeaConditions(),7200).Count==0,"Nothing happens ashore or inside realms");
            var mist=Simulate(9,new SeaConditions{AtSea=true,Mist=true},7200);
            Check(mist.Any(e=>e.kind==SeaEventKind.Fog) && mist.All(e=>e.kind==SeaEventKind.Fog || e.kind==SeaEventKind.Bottle),"Mist brings fog lights, never shoals or whales");
            Check(Simulate(9,new SeaConditions{AtSea=true,Mist=true,NearHarbor=true},7200).All(e=>e.kind!=SeaEventKind.Fog),"No fog rescue inside the harbour");
            Check(Simulate(9,new SeaConditions{AtSea=true,Night=true,GlowNight=true},7200).Any(e=>e.kind==SeaEventKind.Glow),"Glow nights glow");
            Check(Simulate(9,new SeaConditions{AtSea=true,Night=true},7200).All(e=>e.kind==SeaEventKind.Bottle),"Ordinary nights only drift bottles");
            Check(Simulate(9,new SeaConditions{AtSea=true,Rain=true},7200).Count==0,"Storms keep the sea quiet");
            int glow=Enumerable.Range(0,300).Count(FishingSeaSchedule.GlowNight);Check(glow>70 && glow<130,"About one night in three glows ("+glow+"/300)");
            var fog=new FishingSeaSchedule(1);fog.Start(SeaEventKind.Fog);fog.Tick(.5f,new SeaConditions{AtSea=true,Mist=true});
            Check(fog.Active==SeaEventKind.Fog,"Fog lasts while the mist does");fog.Tick(.5f,Day);
            Check(fog.Active==null && fog.CooldownLeft(SeaEventKind.Fog)>0 && fog.GlobalLeft>0,"Fog lifts with the mist and starts its cooldown");
            Check(FishingSeaLetters.Titles.Length==FishingSeaLetters.Count && FishingSeaLetters.Bodies.All(x=>x.Length>20),"Eight readable letters");
        }
        private static void Saves()
        {
            var career=new FishingCareer();var lore=career.Lore;
            Check(lore.SetSeaFlag(FishingSeaLetters.WhaleBit) && !lore.SetSeaFlag(FishingSeaLetters.WhaleBit) && !lore.SetSeaFlag(31),"Sea flags set once and stay in range");
            lore.SetSeaFlag(FishingSeaLetters.LetterBit+3);
            var json=JsonUtility.ToJson(career.Snapshot());
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(json),out var back) && back.Lore.SeaFlag(FishingSeaLetters.WhaleBit) && back.Lore.SeaFlag(FishingSeaLetters.LetterBit+3),"Sea log survives a JSON round trip");
            var bad=JsonUtility.FromJson<FishingSaveData>(json);bad.lore.seaLog=FishingSeaLetters.LogLimit;Check(!FishingCareer.TryRestore(bad,out _),"Out-of-range sea log is rejected");
            var forged=JsonUtility.FromJson<FishingSaveData>(json);forged.version=19;Check(!FishingCareer.TryRestore(forged,out _),"Pre-v20 saves cannot carry a sea log");
            var legacy=new FishingCareer().Snapshot();legacy.version=19;legacy.coins=77;
            var old=JsonUtility.ToJson(legacy).Replace(",\"seaLog\":0","");
            Check(!old.Contains("seaLog"),"Fixture really lacks the sea log");
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(old),out var upgraded) && upgraded.Coins==77 && !upgraded.Lore.SeaFlag(FishingSeaLetters.WhaleBit),"Old saves load with an empty sea log");
        }

        private static void Place(FishingPrototype c,Vector3 p,Vector3 forward)
        {
            p.y=c.Player.transform.position.y;var q=Quaternion.LookRotation(Vector3.ProjectOnPlane(forward,Vector3.up).normalized);
            var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=p;rb.rotation=q;c.Player.transform.SetPositionAndRotation(p,q);Physics.SyncTransforms();
        }
        private static IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        private static void Shot(FishingPrototype c,string dir,string name,Vector3 eye,Vector3 target)
        {
            var cam=c.viewCamera.transform;cam.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            c.Weather.RefreshForCapture();if(c.SeaEvents.Bottle.gameObject.activeInHierarchy)c.SeaEvents.SizeGlint(0);for(int i=0;i<5;i++)FishingV016Validation.Capture(c,dir,name); // water reflection/depth needs a few renders to settle
        }
        private static IEnumerator Sky(FishingPrototype c,BayWeather weather,float hour)
        {
            c.Career.Calendar.AdvanceToHour(hour);
            // Climate refuses a new preset until the previous 12s blend finishes.
            float until=Time.time+15;while(c.Weather.Climate.Current!=weather && !c.Weather.Climate.Select(weather) && Time.time<until)yield return null;
            c.Weather.Climate.Hold();Check(c.Weather.Climate.Current==weather,"Weather set to "+weather);
        }
        private static IEnumerator Market(FishingPrototype c,string dir)
        {
            var market=c.SeaMarket;var lore=c.Career.Lore;var sea=c.SeaEvents;
            Check(FishingSeaMarket.Open(5,10) && !FishingSeaMarket.Open(5,20) && !FishingSeaMarket.Open(4,10) && FishingSeaMarket.Open(13,10),"Market sails Saturdays and festival days, daytime only");
            Check(FishingSeaMarket.Ground(0)!=FishingSeaMarket.Ground(7),"Market moves to another ground each week");
            var sky=Sky(c,BayWeather.Clear,10);while(sky.MoveNext())yield return null;
            c.MoveToSpotForAutomation(0);market.ForceOpen=true;yield return null;yield return null;
            Check(market.Visible && FishingSeaEventWorld.OpenWater(market.Position,10),"Market ship moors in open water clear of rocks");
            var spot=c.fishingSpots[FishingSeaMarket.Ground(c.Career.Calendar.Day)];var side=(new Vector3(spot.x,0,spot.z)-market.Position).normalized;
            Shot(c,dir,"sea-market-side",market.Position+side*32+Vector3.up*1.2f,market.Position+Vector3.up*2);
            Shot(c,dir,"sea-market-far",market.Position+side*55+Vector3.up*5,market.Position+Vector3.up*5);
            Place(c,market.Position+side*10,-side);var w=Wait(.5f);while(w.MoveNext())yield return null;
            Check(market.Target!=null,"Gaze picks a stall once moored alongside");
            var marketLoop=c.WorldAudio.LoopSource("sea-market");
            Check(marketLoop!=null && marketLoop.isPlaying && marketLoop.clip.name=="SeaMarketLoop" && marketLoop.spatialBlend==1 && marketLoop.volume>0,"Nearby market maintains its dedicated spatial ambient recording");
            Place(c,market.Position+side*70,-side);w=Wait(.8f);while(w.MoveNext())yield return null;
            Check(c.WorldAudio.LoopSource("sea-market")==null,"Leaving the market's audible range fades and releases its loop");
            Place(c,market.Position+side*10,-side);w=Wait(.5f);while(w.MoveNext())yield return null;
            c.ToggleQuiet();yield return null;yield return null;
            Check(c.WorldAudio.LoopSource("sea-market")==null,"Quiet mode stops the market loop");
            c.ToggleQuiet();w=Wait(.5f);while(w.MoveNext())yield return null;
            float ambient=c.AmbientVolume;
            c.SetSetting("Ambient",0);yield return null;yield return null;
            Check(c.WorldAudio.LoopSource("sea-market")==null || c.WorldAudio.LoopSource("sea-market").volume==0,"Market ambience obeys Ambient mute");
            c.SetSetting("Ambient",ambient);yield return null;yield return null;
            Check(market.Merchant!=null,"Merchant A-Xun stands aboard");
            if(market.Merchant!=null)Shot(c,dir,"sea-market-owner",new Vector3(market.Merchant.transform.position.x,1.6f,market.Merchant.transform.position.z)+side*7,market.Merchant.transform.position+Vector3.up*1.5f); // seated eye height in the skiff
            Shot(c,dir,"sea-market-stalls",market.Position+side*7+Vector3.up*3.5f,market.Position+Vector3.up*2);
            c.Career.AwardSeaCoins(1000);int coins=c.Career.Coins;
            FishingSeaEventWorld.ForceVr=true;c.Notice("vr-market",60);
            int tradeSounds=c.WorldAudio.PlayCount("SeaMarketTrade");
            try{Check(market.Trade(MarketGoods.Bait) && market.BaitActive && c.Career.Coins==coins-FishingSeaMarket.BaitPrice && c.VisibleMessage=="vr-market","Seasonal bait sells without a VR popup");
                Check(c.WorldAudio.PlayCount("SeaMarketTrade")==tradeSounds+1,"Successful market trade plays the dedicated transaction cue");}
            finally{FishingSeaEventWorld.ForceVr=false;}
            int rares=0;for(int i=0;i<200;i++){var k=FishKind.Coral;bool r=false;sea.AdjustCatch(3,ref k,ref r,false);if(r)rares++;}
            Check(rares>20 && rares<120,"Market bait raises the rare rate ("+rares+"/200)");
            int seeds=c.Career.Backpack.Count(FieldItem.TurnipSeeds)+c.Career.Backpack.Count(FieldItem.FishFeed);
            Check(market.Trade(MarketGoods.Seeds) && c.Career.Backpack.Count(FieldItem.TurnipSeeds)+c.Career.Backpack.Count(FieldItem.FishFeed)==seeds+3,"Seasonal seeds go into the backpack");
            Check(market.Trade(MarketGoods.Souvenir) && FishingSeaMarket.Bit(lore.SeaPost,c.Career.Calendar.Season)==1 && !market.Trade(MarketGoods.Souvenir),"Season souvenir sells once");
            FishingV016Validation.Capture(c,dir,"sea-market-souvenir");c.Letter.Close();yield return null;
            tradeSounds=c.WorldAudio.PlayCount("SeaMarketTrade");
            Check(!market.Trade(MarketGoods.Buyback) && c.WorldAudio.PlayCount("SeaMarketTrade")==tradeSounds,"Empty buyback refuses the trade without a success sound");
            Check(!FishingSeaMarket.ReplyReady(3,lore) && FishingSeaMarket.ReplyReady(1,lore),"The finale-quoting reply waits for the grand treasure");
            Check(market.Trade(MarketGoods.Letter,1) && FishingSeaMarket.Pending(lore.SeaPost)==2 && lore.SeaPostDay==c.Career.Calendar.Day && !market.Trade(MarketGoods.Letter,2),"One drift letter at a time");
            sea.Force(SeaEventKind.Bottle);yield return null;Place(c,sea.Center,Vector3.forward);w=Wait(.4f);while(w.MoveNext())yield return null;c.Letter.Close();yield return null;
            Check(FishingSeaMarket.Pending(lore.SeaPost)==2,"No reply before two days pass");
            c.Career.Calendar.AdvanceToHour(9);c.Career.Calendar.AdvanceToHour(10);c.Career.Calendar.AdvanceToHour(9);c.Career.Calendar.AdvanceToHour(10);yield return null;
            sea.Force(SeaEventKind.Bottle);yield return null;Place(c,sea.Center,Vector3.forward);w=Wait(.4f);while(w.MoveNext())yield return null;
            Check(FishingSeaMarket.Pending(lore.SeaPost)==0 && FishingSeaMarket.Bit(lore.SeaPost,FishingSeaMarket.ReplyBit+1)==1 && c.Letter.Open,"Reply drifts back after two days");
            FishingV016Validation.Capture(c,dir,"sea-reply-letter");c.Letter.Close();yield return null;
            Check(FishingSeaMarket.Archive(lore).Count>=10 && FishingSeaMarket.Archive(lore).Exists(x=>x.title.StartsWith("回信")),"Journal archive lists read letters, replies and souvenir cards");
            var back=JsonUtility.FromJson<FishingSaveData>(JsonUtility.ToJson(c.Career.Snapshot()));
            Check(FishingCareer.TryRestore(back,out var restored) && restored.Lore.SeaPost==lore.SeaPost,"Market and post progress round-trip");
            market.ForceOpen=false;c.Career.Calendar.AdvanceToHour(22);
            Place(c,market.Position+side*10,-side);w=Wait(.8f);while(w.MoveNext())yield return null;
            Check(!market.Visible && c.WorldAudio.LoopSource("sea-market")==null,"Closing the market at night releases its ambience even with the player nearby");
        }
        private static IEnumerator Play(FishingPrototype c,string dir)
        {
            var sea=c.SeaEvents;var lore=c.Career.Lore;
            Check(sea!=null && !sea.AutoSchedule && sea.Schedule.Active==null,"Automated runs keep the scheduler quiet unless forced");
            Check(sea.Fluke!=null,"Whale has a Fluke node for the tail beat");
            Check(sea.WhaleTriangles>0 && sea.WhaleTriangles<=8000,"Whale stays within the 8k triangle budget ("+sea.WhaleTriangles+")");
            c.ReturnToHarbor();yield return null;c.MoveToSpotForAutomation(0);yield return null;
            Check(!c.Ashore && !c.StoryBlocksGameplay,"Boat is at sea and free");

            // VR：开始事件与奖励都不往屏幕写通知，只靠世界光点与空间声。
            FishingSeaEventWorld.ForceVr=true;
            try
            {
                var vsky=Sky(c,BayWeather.Clear,10);while(vsky.MoveNext())yield return null;c.Notice("vr-baseline",120);int cues=sea.CuePlays;
                sea.Force(SeaEventKind.Whales);var w=Wait(.5f);while(w.MoveNext())yield return null;
                Place(c,Vector3.Lerp(c.Player.transform.position,sea.Center,.55f),sea.Center-c.Player.transform.position);w=Wait(2.5f);while(w.MoveNext())yield return null;
                Check(sea.Beacon.gameObject.activeInHierarchy && sea.CuePlays>cues,"VR guidance is a world light point plus spatial sound");
                Check(lore.SeaFlag(FishingSeaLetters.WhaleBit) && c.Career.Seen(MarineKind.Dolphin),"Slow approach records whale and dolphins in the codex");
                Check(c.VisibleMessage=="vr-baseline","VR shows no screen popup for events or rewards");
            }
            finally{FishingSeaEventWorld.ForceVr=false;}
            Check(sea.ParticleCount<=FishingSeaEventWorld.ParticleBudget,"Whale spout within particle budget");
            Check(FishingSeaEventWorld.OpenWater(sea.Center,20),"Whale orbit stays in deep open water clear of rocks");
            Shot(c,dir,"sea-whales",sea.Center+new Vector3(-16,4.5f,-20),sea.Whale.position+Vector3.up);
            for(int f=0;f<4;f++){var fw=Wait(.35f);while(fw.MoveNext())yield return null;var tail=sea.Fluke.position;Shot(c,dir,"sea-fluke-"+f,tail+sea.Whale.right*7+Vector3.up*1.5f,tail);}
            // The fluke peaks at ~30 deg mid-dive (cycle 7.5 s of 16).
            {float until=Time.time+20;while(Mathf.Abs(sea.EventAge%16-7.5f)>.15f && Time.time<until)yield return null;
            var tail=sea.Fluke.position;var eye=tail+sea.Whale.right*7;eye.y=1.6f;Shot(c,dir,"sea-fluke-dive",eye,tail);}
            Shot(c,dir,"sea-dolphins",c.Player.transform.position-c.Player.transform.forward*6+Vector3.up*4,sea.Dolphins[1].position);

            // 鱼群沸腾
            c.MoveToSpotForAutomation(0);yield return null;
            sea.Force(SeaEventKind.Shoal);var s=Wait(3);while(s.MoveNext())yield return null;
            Check(sea.EventSpot==0 && sea.ParticleCount>0 && sea.ParticleCount<=FishingSeaEventWorld.ParticleBudget,"Shoal boils at the nearest ground within budget");
            var kind=FishKind.Coral;bool rare=false;sea.AdjustCatch(0,ref kind,ref rare,false);Check(rare && kind==FishKind.Coral,"Shoal ground guarantees a rare fish");
            rare=false;sea.AdjustCatch(1,ref kind,ref rare,false);Check(!rare,"Other grounds are unaffected");
            rare=false;sea.AdjustCatch(0,ref kind,ref rare,true);Check(!rare,"Legend strikes are left alone");
            Shot(c,dir,"sea-shoal",sea.Center+new Vector3(-22,6,-22),sea.Center+Vector3.up*2.5f);

            // 漂流瓶 → 信 → 藏宝 / 委托
            sea.Force(SeaEventKind.Bottle);yield return null;yield return null;
            Shot(c,dir,"sea-bottle",sea.Bottle.position+new Vector3(-1.6f,1.1f,-1.6f),sea.Bottle.position+Vector3.up*.3f);
            var toBottle=Vector3.ProjectOnPlane(sea.Bottle.position-c.Player.transform.position,Vector3.up).normalized;
            for(int k=0;k<2;k++)yield return null;
            foreach(int d in new[]{8,14})Shot(c,dir,"sea-bottle-"+d+"m",sea.Bottle.position-toBottle*d+Vector3.up*2.5f,sea.Bottle.position);
            sea.GlintMarker.gameObject.SetActive(false);
            foreach(bool add in new[]{false,true}){sea.AdditiveHalo=add;Shot(c,dir,"sea-bottle-20m-"+(add?"additive":"alpha-q3050"),sea.Bottle.position-toBottle*20+Vector3.up*3.5f,sea.Bottle.position);}
            sea.GlintMarker.gameObject.SetActive(true);sea.AdditiveHalo=true;
            // Overcast day and deep-water night backgrounds, each from 20 m.
            var wsky=Sky(c,BayWeather.Cloudy,14);while(wsky.MoveNext())yield return null;Shot(c,dir,"sea-bottle-20m-cloudy",sea.Bottle.position-toBottle*20+Vector3.up*3.5f,sea.Bottle.position);
            wsky=Sky(c,BayWeather.Clear,22);while(wsky.MoveNext())yield return null;Shot(c,dir,"sea-bottle-20m-night",sea.Bottle.position-toBottle*20+Vector3.up*3.5f,sea.Bottle.position);
            wsky=Sky(c,BayWeather.Clear,10);while(wsky.MoveNext())yield return null;
            Shot(c,dir,"sea-bottle-wide",sea.Bottle.position-toBottle*20+Vector3.up*3.5f,sea.Bottle.position);Debug.Log("[V044-SEA] bottle wide: bottle "+sea.Bottle.position+" boat "+c.Player.transform.position);
            Place(c,sea.Center,Vector3.forward);s=Wait(.4f);while(s.MoveNext())yield return null;
            Check(lore.SeaFlag(FishingSeaLetters.LetterBit) && c.Letter.Open && sea.Schedule.Active==null,"Bottle pickup opens the first letter");
            FishingV016Validation.Capture(c,dir,"sea-letter");c.Letter.Close();yield return null;
            lore.SetSeaFlag(FishingSeaLetters.LetterBit+1);lore.SetSeaFlag(FishingSeaLetters.LetterBit+2);
            sea.Force(SeaEventKind.Bottle);yield return null;Place(c,sea.Center,Vector3.forward);s=Wait(.4f);while(s.MoveNext())yield return null;c.Letter.Close();yield return null;
            Check(lore.SeaFlag(FishingSeaLetters.LetterBit+3) && sea.Crate.gameObject.activeSelf,"Treasure letter marks a drifting crate");
            Shot(c,dir,"sea-crate",sea.Crate.position+new Vector3(-2.5f,1.6f,-2.5f),sea.Crate.position);
            int coins=c.Career.Coins;Place(c,sea.Crate.position,Vector3.forward);s=Wait(.3f);while(s.MoveNext())yield return null;
            Check(c.Career.Coins==coins+FishingSeaLetters.TreasureReward && lore.SeaFlag(FishingSeaLetters.TreasureBit) && !sea.Crate.gameObject.activeSelf,"Crate pays once");
            lore.SetSeaFlag(FishingSeaLetters.LetterBit+4);lore.SetSeaFlag(FishingSeaLetters.LetterBit+5);
            coins=c.Career.Coins;sea.Force(SeaEventKind.Bottle);yield return null;Place(c,sea.Center,Vector3.forward);s=Wait(.4f);while(s.MoveNext())yield return null;c.Letter.Close();yield return null;
            Check(lore.SeaFlag(FishingSeaLetters.LetterBit+6) && lore.SeaFlag(FishingSeaLetters.CommissionBit) && c.Career.Coins==coins+FishingSeaLetters.CommissionReward,"Whale commission pays on reading since the whale is already recorded");

            // 雾中灯语
            c.MoveToSpotForAutomation(2);yield return null;var sky=Sky(c,BayWeather.Mist,17);while(sky.MoveNext())yield return null;s=Wait(1);while(s.MoveNext())yield return null;
            Check(c.HarborDistance>90,"Fog test starts away from the harbour");
            s=Wait(12.5f);while(s.MoveNext())yield return null; // let the mist finish blending in before the shots
            sea.Force(SeaEventKind.Fog);s=Wait(5);while(s.MoveNext())yield return null;
            Check(c.Weather.EventFog>=1 && RenderSettings.fogEndDistance<80,"Fog event thickens the sea fog ("+RenderSettings.fogEndDistance+"m)");
            Check(sea.Ghost.gameObject.activeSelf && sea.Beacon.gameObject.activeInHierarchy,"Fog shows a ship silhouette and the lighthouse guide light");
            Shot(c,dir,"sea-fog-ship",c.Player.transform.position+Vector3.up*3,sea.Ghost.position+Vector3.up*4);
            var toBoat=Vector3.ProjectOnPlane(c.Player.transform.position-sea.Beacon.position,Vector3.up);
            Shot(c,dir,"sea-fog-lighthouse",sea.Beacon.position+toBoat.normalized*Mathf.Min(70,toBoat.magnitude)+Vector3.down*(sea.Beacon.position.y-6),sea.Beacon.position);
            coins=c.Career.Coins;Place(c,sea.Ghost.position,Vector3.forward);s=Wait(.3f);while(s.MoveNext())yield return null;
            Check(c.Career.Coins==coins+FishingSeaLetters.GhostReward && !sea.Ghost.gameObject.activeSelf,"Catching the old ship pays its encounter");
            sky=Sky(c,BayWeather.Clear,17);while(sky.MoveNext())yield return null;yield return null;yield return null;
            Check(sea.Schedule.Active==null,"Fog event ends when the mist lifts");

            // 夜光海
            c.MoveToSpotForAutomation(1);yield return null;sky=Sky(c,BayWeather.Clear,22);while(sky.MoveNext())yield return null;
            sea.Force(SeaEventKind.Glow);s=Wait(4);while(s.MoveNext())yield return null;
            Check(sea.ParticleCount>20 && sea.ParticleCount<=FishingSeaEventWorld.ParticleBudget,"Glow sea motes within budget ("+sea.ParticleCount+")");
            kind=FishKind.Coral;rare=false;sea.AdjustCatch(1,ref kind,ref rare,false);
            Check(rare && (kind==FishKind.Lanternfish || kind==FishKind.Jellyfish),"Glow night yields luminous fish");
            FishingSeaEventWorld.ForceVr=true;try{s=Wait(4);while(s.MoveNext())yield return null;Check(sea.ParticleCount<=FishingSeaEventWorld.PicoParticleBudget,"PICO/VR glow stays within 100 particles ("+sea.ParticleCount+")");}finally{FishingSeaEventWorld.ForceVr=false;}
            Check(lore.SeaFlag((int)SeaEventKind.Glow) && lore.SeaFlag(FishingSeaLetters.LetterBit+7)==false,"Glow sighting recorded");
            Shot(c,dir,"sea-glow",c.Player.transform.position-c.Player.transform.forward*9+Vector3.up*5,c.Player.transform.position);
            sky=Sky(c,BayWeather.Clear,8);while(sky.MoveNext())yield return null;yield return null;yield return null;
            Check(sea.Schedule.Active==null,"Glow fades at dawn");

            var m=Market(c,dir);while(m.MoveNext())yield return m.Current;
            var data=c.Career.Snapshot();
            Check(FishingCareer.TryRestore(JsonUtility.FromJson<FishingSaveData>(JsonUtility.ToJson(data)),out var live) && live.Lore.SeaFlag(FishingSeaLetters.TreasureBit) && live.Lore.SeaFlag(FishingSeaLetters.CommissionBit),"Live sea progress round-trips");
        }
    }
}
