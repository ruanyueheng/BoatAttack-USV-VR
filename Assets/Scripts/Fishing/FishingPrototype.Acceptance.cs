using System;
using System.IO;
using System.Collections;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingAcceptanceOptions AcceptanceOptions { get; private set; }
        public FishingAcceptance Acceptance { get; private set; }
        public bool HasIsolatedProfile => Automated || AcceptanceOptions != null;
        public static bool XRSimulatorRequested => Application.isEditor && HasArgument("-fishing-xr-simulator") && HasArgument(FishingAcceptanceOptions.EnableFlag);

        private void ReadAcceptanceOptions()
        {
            try
            {
                AcceptanceOptions = FishingAcceptanceOptions.Parse(Environment.GetCommandLineArgs());
                if (AcceptanceOptions != null && Application.isBatchMode && !AcceptanceOptions.Smoke && !XRSimulatorRequested)
                    throw new ArgumentException("Headless acceptance requires -fishing-acceptance-smoke; screenshot capture needs a visible player.");
            }
            catch (ArgumentException)
            {
                if (!Application.isEditor) Application.Quit(1);
                throw;
            }
        }

        internal void ApplyAcceptanceCase(FishingAcceptanceCase selected)
        {
            if (AcceptanceOptions == null || Acceptance == null)
                throw new InvalidOperationException("Acceptance fixtures require an explicitly opted-in test profile.");
            // Validate before changing any live state. FreePlay is intentionally never synthesized.
            FishingCareer fixture = selected == FishingAcceptanceCase.FreePlay ? null : FishingAcceptanceCases.CreateCareer(selected);
            CancelV036AcceptanceFixture();Home?.CancelSleep();Home?.StandUp(true);
            HandGames?.Cancel();CloseHomeChart(true);CloseSaveMenu();
            if(StartMenuOpen)TitleVoyage.Close();StartMenuOpen=false;MenuControls=false;CancelGuideOpening();
            Letter?.Close();CancelWorldAction();_workshopReceipt=false;TackleShop?.Close();Residents?.Close();Realms?.Abort();CoastWorld?.CloseMenu();
            if (InventoryOpen) ToggleInventory();
            if (JournalOpen) ToggleJournal();
            else if (Paused) TogglePause();
            if(Ashore)Shore.BoardImmediately();
            // Switching explicit isolated fixtures must not settle or discard the prior profile's pending fish.
            Anchored=false;Session.Cancel();CatchRig.ResetFixture();HarborDeskOpen=false;ShowDetails=false;ChartExpanded=true;
            Teleport(spawnPosition, spawnRotation);
            UpdateNearestSpot();
            SavePath = AcceptanceOptions.ProfilePath(Application.temporaryCachePath, fixture != null);
            Career = fixture ?? FishingSaveStore.Load(SavePath, out _);
            Fleet.Refresh();Islands.Refresh();_hud.ShopCategory=0;
            Shore.RefreshMarkers();
            FieldWorld.RefreshPickups();
            WorkshopWorld.RefreshFromCareer();
            Farm?.Refresh();
            Expedition.RefreshFromCareer();
            SaveWarning = string.Empty;
            LastCatch = default;
            _previousPhase = CatchPhase.Idle;
            _netMotion = _netDeployment = 0f;
            Bait = selected == FishingAcceptanceCase.LegendLeap ? BaitKind.Pearl : BaitKind.Balanced;
            ForceLegendForAutomation = false;
            ChartRangeMeters = 160;
            NavigationTarget = 1;
            RefreshHold();
            if (_trophy != null) { _trophy.SetActive(false); Destroy(_trophy); _trophy = null; }
            if (LegendTrophy) BuildTrophy();
            if (!FirstPerson) ToggleView();
            View.ResetLook();
            ResetToolInput();
            _storyConsumedFrame=-1;
            _homeChartConsumedFrame=-1;
            var climate = Weather.Climate;
            climate.Hold();
            for (int i = 0; i < 64 && climate.Blend < 1f; i++) climate.Tick(.25f, false);
            if (!climate.Select(selected == FishingAcceptanceCase.LegendLeap ? BayWeather.Sunset : BayWeather.Clear))
                throw new InvalidOperationException("Acceptance climate could not finish its prior transition.");
            for (int i = 0; i < 64 && climate.Blend < 1f; i++) climate.Tick(.25f, false);
            if (selected == FishingAcceptanceCase.FreePlay) climate.ResumeCycle();
            Notice(string.Empty, 0f);

            CastMotion.Reset();
            if (FishingAcceptanceCases.IsCatchCase(selected) || FishingAcceptanceCases.IsFindCase(selected) || selected == FishingAcceptanceCase.FullHold || selected == FishingAcceptanceCase.Details || selected == FishingAcceptanceCase.CastFlight)
            {
                int spot = selected == FishingAcceptanceCase.LegendLeap ? FishingSpecies.LegendArea : 0;
                Teleport(fishingSpots[spot] + Vector3.up, spawnRotation);
                UpdateNearestSpot();
                Interact();
                if (!Anchored) throw new InvalidOperationException("Acceptance fixture did not anchor at its fishing area.");
                Notice(string.Empty, 0f);
            }
            if (FishingAcceptanceCases.IsCatchCase(selected))
            {
                _castGround=Spot; // The fixture was explicitly placed at this real ground.
                FishingAcceptanceCases.PrepareSession(Session, selected, Career);
                _fish.SetActive(false);
                Destroy(_fish);
                _fish = MakeFish(transform, Session.Kind, Session.Rare);
                AnimateEquipment();
                if ((selected == FishingAcceptanceCase.Catch || selected==FishingAcceptanceCase.CatchNight) && !CatchRig.CaptureAndStoreForAutomation())
                    throw new InvalidOperationException("Acceptance catch did not pass the real scoop/store path.");
                _previousPhase = Session.Phase;
            }
            switch (selected)
            {
                case FishingAcceptanceCase.HomeStorage:
                case FishingAcceptanceCase.SofaTelevision:
                case FishingAcceptanceCase.WateringCarry:
                case FishingAcceptanceCase.WellRefill:
                case FishingAcceptanceCase.WateringPour:
                case FishingAcceptanceCase.OrchardFruit:
                case FishingAcceptanceCase.PortalEntrance:
                case FishingAcceptanceCase.PortalExit:
                    BeginV036AcceptanceFixture(selected);break;
                case FishingAcceptanceCase.BootCatch:case FishingAcceptanceCase.CanCatch:case FishingAcceptanceCase.TreasureCatch:
                    PrepareAcceptanceFind(selected);break;
                case FishingAcceptanceCase.LetterEnvelope:case FishingAcceptanceCase.LetterPaper:
                    Letter.ShowIsland(0);if(selected==FishingAcceptanceCase.LetterPaper)Letter.Reveal();break;
                case FishingAcceptanceCase.ResidentDialogue:
                case FishingAcceptanceCase.WindSanctuary:
                case FishingAcceptanceCase.ForestSanctuary:
                case FishingAcceptanceCase.WreckSanctuary:
                case FishingAcceptanceCase.StarSanctuary:
                case FishingAcceptanceCase.StarObservation:
                case FishingAcceptanceCase.LoreJournal:
                case FishingAcceptanceCase.ShopRations:
                case FishingAcceptanceCase.RegionArrival:
                case FishingAcceptanceCase.SwimmingView:
                case FishingAcceptanceCase.ClimbingView:
                    PrepareStoryFixture(selected);break;
                case FishingAcceptanceCase.HarborOrders:
                case FishingAcceptanceCase.MaxUpgrades: ToggleHarborDesk(); break;
                case FishingAcceptanceCase.FullHold:
                    if (Cast()) throw new InvalidOperationException("Full-hold fixture unexpectedly allowed casting.");
                    break;
                case FishingAcceptanceCase.Details: ToggleDetails(); break;
                case FishingAcceptanceCase.Journal: ToggleJournal(); break;
                case FishingAcceptanceCase.HunyuanCatalog: _hud.JournalPage=0;_hud.CatalogPage=1;ToggleJournal();break;
                case FishingAcceptanceCase.HunyuanNightCatalog: _hud.JournalPage=0;_hud.CatalogPage=2;ToggleJournal();break;
                case FishingAcceptanceCase.CastFlight: Cast(1);CastMotion.Tick(.1f);CastMotion.Tick(.1f);CastMotion.Tick(.1f);CastMotion.Tick(.1f);break;
                case FishingAcceptanceCase.Calendar: _hud.JournalPage=4;ToggleJournal();break;
                case FishingAcceptanceCase.NightBoat: ToggleView();break;
                case FishingAcceptanceCase.LuxuryBoat: ToggleView();break;
                case FishingAcceptanceCase.IslandJournal: _hud.JournalPage=5;ToggleJournal();break;
                case FishingAcceptanceCase.IslandLanding:
                case FishingAcceptanceCase.ForestIslandLanding:
                case FishingAcceptanceCase.WreckIslandLanding:
                case FishingAcceptanceCase.StarIslandLanding:
                    int landingIsland=selected==FishingAcceptanceCase.ForestIslandLanding?1:selected==FishingAcceptanceCase.WreckIslandLanding?2:selected==FishingAcceptanceCase.StarIslandLanding?3:0;
                    Teleport(FishingIslandWorld.Berth(landingIsland),Quaternion.Euler(0,180,0));
                    if(!Shore.TryDisembark())throw new InvalidOperationException("Island fixture failed to disembark.");
                    ToggleView();View.ResetLook();NavigationTarget=IslandNavigationIndex(landingIsland);
                    Regions.Show(FishingAdventure.Name(landingIsland),FishingLore.Subtitle(landingIsland),landingIsland);break;
                case FishingAcceptanceCase.HomeInterior:
                case FishingAcceptanceCase.HomeWorkbench:
                    PrepareAcceptanceHome(selected);break;
                case FishingAcceptanceCase.HomeGarden:
                case FishingAcceptanceCase.HomeGardenNight:
                case FishingAcceptanceCase.FarmHarvest:
                    PrepareAcceptanceHome(FishingAcceptanceCase.HomeInterior);
                    var gardenFoot=selected!=FishingAcceptanceCase.FarmHarvest?Home.Origin+new Vector3(12.2f,0,-11):Farm.Target(0).position+new Vector3(-.25f,0,-1.1f);
                    if(!Shore.Relocate(gardenFoot,Quaternion.identity))throw new InvalidOperationException("Garden fixture footing is blocked.");
                    View.ResetLook();View.ApplyShoreLookDelta(new Vector2(0,-(selected!=FishingAcceptanceCase.FarmHarvest?12:35)/.12f));Farm.Refresh();break;
                case FishingAcceptanceCase.HomeBarbecue:
                case FishingAcceptanceCase.HomeGrilledFish:
                    PrepareAcceptanceHome(FishingAcceptanceCase.HomeInterior);
                    if(!Shore.Relocate(CoastWorld.Grill.position+Vector3.back*1.65f,Quaternion.identity))throw new InvalidOperationException("Barbecue fixture footing is blocked.");
                    View.ResetLook();View.ApplyShoreLookDelta(new Vector2(0,-20/.12f));
                    if(selected==FishingAcceptanceCase.HomeBarbecue)CoastWorld.OpenMenu();
                    else
                    {
                        if(!CoastWorld.StartRoasting(Career.Inventory[0].id))throw new InvalidOperationException("Barbecue fixture cannot reserve its fish.");
                        for(int step=0;step<72;step++)Career.TickBarbecue(.25f);
                    }
                    break;
                case FishingAcceptanceCase.CashierDialogue: PrepareAcceptanceShore(selected);break;
                case FishingAcceptanceCase.ShopSupplies: PrepareAcceptanceShore(selected);_hud.ShopCategory=2;break;
                case FishingAcceptanceCase.SaveSlots:
                case FishingAcceptanceCase.SaveOverwrite:
                    ActiveSaveSlot=0;
                    OpenStartMenu();
                    for(int slot=1;slot<FishingSlotStore.Count;slot++)
                        foreach(var kind in new[]{FishingSlotKind.Manual,FishingSlotKind.Auto})
                        {
                            string path=FishingSlotStore.PathFor(SavePath,slot,kind);
                            File.Delete(path);File.Delete(path+".bak");
                        }
                    FishingSlotStore.Write(SavePath,0,FishingSlotKind.Manual,Career,new FishingResumeData(),"acceptance");
                    FishingSlotStore.Write(SavePath,0,FishingSlotKind.Auto,Career,new FishingResumeData(),"acceptance");
                    OpenSaveMenu(selected==FishingAcceptanceCase.SaveSlots?"load":"save");
                    if(selected==FishingAcceptanceCase.SaveOverwrite)RequestSlotAction(0,"save");
                    break;
                case FishingAcceptanceCase.SandSieve:
                case FishingAcceptanceCase.RopeWinder:
                    PrepareAcceptanceHome(FishingAcceptanceCase.HomeInterior);
                    int handGame=selected==FishingAcceptanceCase.SandSieve?0:1;
                    Career.Backpack.TryAdd(FieldItem.Sand,4);Career.Backpack.TryAdd(FieldItem.Fiber,6);
                    if(!Shore.Relocate(HandGames.Approach(handGame),Quaternion.LookRotation(-HandGames.Station(handGame).forward)))throw new InvalidOperationException("Handcraft fixture has no safe approach.");
                    if(!FirstPerson)View.Toggle();View.ResetLook();View.ApplyView(true,true);
                    if(!HandGames.Begin(handGame))throw new InvalidOperationException("Handcraft fixture cannot begin a real round.");
                    break;
                case FishingAcceptanceCase.HomeChart:
                    PrepareAcceptanceHome(FishingAcceptanceCase.HomeInterior);
                    if(!Shore.Relocate(Home.WakePoint,Quaternion.identity))throw new InvalidOperationException("家中海图夹具无法到达安全落脚点。");
                    View.ResetLook();
                    if(!OpenPortableChart())throw new InvalidOperationException("随身海图夹具无法取出纸图。");
                    PortableChart.Tick(FishingPortableChart.OpenSeconds);PresentCharacterFrame();
                    break;
                case FishingAcceptanceCase.StartMenu:OpenStartMenu();break;
                case FishingAcceptanceCase.MenuControls:OpenStartMenu();MenuControls=true;_hud.SettingsTab=0;break;
                case FishingAcceptanceCase.MenuGraphics:OpenStartMenu();MenuControls=true;_hud.SettingsTab=1;break;
                case FishingAcceptanceCase.MenuAudio:OpenStartMenu();MenuControls=true;_hud.SettingsTab=2;break;
                case FishingAcceptanceCase.Departure:OpenStartMenu();StartFromMenu();break;
                case FishingAcceptanceCase.ArchIslet:
                case FishingAcceptanceCase.FireflyIslet:
                    int islet=selected==FishingAcceptanceCase.ArchIslet?0:1;
                    Teleport(FishingCoastalPresentation.Berth(islet)+Vector3.up,Quaternion.identity);
                    NavigationTarget=HomeNavigationIndex+1+islet;if(FirstPerson)ToggleView();break;
                case FishingAcceptanceCase.NatureJournal:_hud.JournalPage=2;ToggleJournal();break;
                case FishingAcceptanceCase.LegendJournal:_hud.JournalPage=1;ToggleJournal();break;
                case FishingAcceptanceCase.ShoreJournal:_hud.JournalPage=3;ToggleJournal();break;
                case FishingAcceptanceCase.GuideOpening:
                    PrepareAcceptanceShore(FishingAcceptanceCase.ShoreFirstPerson);
                    View.ResetLook();
                    if(!OpenFieldGuide())throw new InvalidOperationException("Guide fixture could not start opening");
                    GuideOpenAmount=.82f;_guideAge=.82f;break;
                case FishingAcceptanceCase.HomeMoonlight:
                    Teleport(Home.Berth+Vector3.back*55+Vector3.up,Quaternion.identity);
                    if(FirstPerson)ToggleView();SetDayNight(true);View.ResetLook();break;
                case FishingAcceptanceCase.Settings: TogglePause(); break;
                case FishingAcceptanceCase.ShoreFirstPerson:
                case FishingAcceptanceCase.ShoreThirdPerson:
                case FishingAcceptanceCase.ShopCounter:
                case FishingAcceptanceCase.StonePickaxe:
                case FishingAcceptanceCase.IronPickaxe:
                case FishingAcceptanceCase.BinocularCarry:
                case FishingAcceptanceCase.BinocularObserve:
                case FishingAcceptanceCase.LookDownStanding:
                case FishingAcceptanceCase.LookDownCrouched:
                case FishingAcceptanceCase.ShopAttendant:
                case FishingAcceptanceCase.NightCoast:
                case FishingAcceptanceCase.LanternCoast:
                case FishingAcceptanceCase.HuntingSpear:
                case FishingAcceptanceCase.BinocularThirdPerson:
                    PrepareAcceptanceShore(selected);break;
                case FishingAcceptanceCase.FreePlay:
                    Notice("独立验收进度 · F5/F6 查看预置场景，F9 返回实玩；普通玩家存档不受影响。", 8f);
                    break;
            }
            Save();
            if (SaveWarning.Length > 0) throw new IOException("Cannot persist the isolated acceptance profile.");
            View.ApplyView();
        }
        private void PrepareAcceptanceHome(FishingAcceptanceCase selected)
        {
            NavigationTarget=HomeNavigationIndex;
            Teleport(Home.Berth+Vector3.up*.2f,Quaternion.identity);
            if(!Shore.TryDisembark())throw new InvalidOperationException("Home fixture cannot land.");
            bool crafting=selected==FishingAcceptanceCase.HomeWorkbench;
            var feet=crafting?Home.StationPosition(CraftStation.Workbench)+Vector3.back*1.35f:Home.Origin+new Vector3(0,.22f,-3.5f);
            if(!Shore.Relocate(feet,Quaternion.identity))throw new InvalidOperationException("Home fixture footing is blocked.");
            for(int slot=0;slot<FishingBackpack.HotbarCount;slot++)
                if(Career.Backpack.At(slot).item==FieldItem.Empty){SelectFieldSlot(slot);break;}
            View.ResetLook();Regions.Show("回潮渔屋","休息整备 · 再赴潮汐",0);
            if(!crafting)return;
            foreach(var station in Career.Workshop.Stations)
                if(station.kind==CraftStation.Workbench && FlatDistance(new Vector3(station.x,station.y,station.z),Home.StationPosition(CraftStation.Workbench))<.5f)
                {
                    if(!WorkshopWorld.OpenStation(station.id))throw new InvalidOperationException("Home workbench fixture cannot open crafting.");
                    return;
                }
            throw new InvalidOperationException("Home workbench is missing.");
        }
        private void PrepareAcceptanceShore(FishingAcceptanceCase selected)
        {
            bool shop=FishingAcceptanceCases.IsShopCase(selected);
            if(shop && (TackleShop==null || !TackleShop.Available))throw new InvalidOperationException("Shop capture requires the installed prefab.");
            Teleport(shop?TackleShop.mooringPoint.position:Shore.Sites[0].Berth+Vector3.up*.6f,shop?TackleShop.transform.rotation:Quaternion.identity);
            if(!Shore.TryDisembark())throw new InvalidOperationException("Capture landing failed: "+Message);
            // 只为隔离画面夹具选择初始观察位置；完整步行通路已由 Shop/Shore 套件验证。
            if(shop)
            {
                Shore.Body.enabled=false;
                Shore.Actor.SetPositionAndRotation(TackleShop.purchasePoint.position+Vector3.up*.05f,TackleShop.transform.rotation);
                Shore.Body.enabled=true;Physics.SyncTransforms();Shore.Body.Move(Vector3.down*.1f);
            }
            View.ResetLook();
            if(selected==FishingAcceptanceCase.ShoreThirdPerson || selected==FishingAcceptanceCase.BinocularThirdPerson)
            {
                ToggleView();View.ApplyShoreLookDelta(new Vector2(1500,0));
            }
            else if(selected==FishingAcceptanceCase.ShoreFirstPerson)View.ApplyShoreLookDelta(new Vector2(0,-458));
            else if(selected==FishingAcceptanceCase.LookDownStanding || selected==FishingAcceptanceCase.LookDownCrouched)
                View.ApplyShoreLookDelta(new Vector2(0,-65/.12f));
            var item=FishingAcceptanceCases.ViewItem(selected);var bag=Career.Backpack;
            if(bag.Count(item)==0 && !bag.TryAdd(item,1))throw new InvalidOperationException("验收背包无法加入指定道具");
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(bag.At(i).item==item){if(i!=0)bag.Move(i,0);break;}
            SelectFieldSlot(0);
            if(selected==FishingAcceptanceCase.LookDownCrouched)Shore.SetCrouch(true);
            if(selected==FishingAcceptanceCase.LanternCoast)UseFieldItem();
            Weather.ApplyEnvironment();
            if(selected==FishingAcceptanceCase.BinocularObserve || selected==FishingAcceptanceCase.BinocularThirdPerson)UseFieldItem();
        }
        private IEnumerator PrepareObservationFixture()
        {
            while(Realms.Transitioning)yield return null;yield return null;yield return null;
            if(Acceptance.CurrentCase!=FishingAcceptanceCase.StarObservation)yield break;
            SelectFieldSlot(8);
            if(!Shore.Relocate(Realms.Device(0).position+Vector3.back*1.6f,Quaternion.identity) || !RequestWorldInteraction())throw new InvalidOperationException("Observation fixture cannot reach the actual telescope.");
            Realms.AimTelescope(new Vector2(Realms.Trial.TargetYaw(0)-Realms.Trial.StarYaw,Realms.Trial.TargetPitch(0)-Realms.Trial.StarPitch),true,.1f);
            for(int i=0;i<5;i++)Realms.AimTelescope(Vector2.zero,true,.1f);
        }
        private void PrepareStoryFixture(FishingAcceptanceCase selected)
        {
            if(selected==FishingAcceptanceCase.LoreJournal){_hud.JournalPage=6;ToggleJournal();return;}
            if(selected==FishingAcceptanceCase.ShopRations){PrepareAcceptanceShore(selected);_hud.ShopCategory=4;return;}
            int island=selected==FishingAcceptanceCase.ForestSanctuary?1:selected==FishingAcceptanceCase.WreckSanctuary?2:selected==FishingAcceptanceCase.StarSanctuary || selected==FishingAcceptanceCase.StarObservation?3:0;
            Teleport(FishingIslandWorld.Berth(island),Quaternion.identity);
            if(!Shore.TryDisembark())throw new InvalidOperationException("V19 fixture cannot land on island "+island);
            if(selected==FishingAcceptanceCase.ResidentDialogue)
            {
                var npc=Residents.Person(0);
                if(!Shore.Relocate(npc.position+Vector3.forward*1.8f,Quaternion.Euler(0,180,0)))throw new InvalidOperationException("NPC fixture footing rejected");
                View.ResetLook();viewCamera.transform.SetPositionAndRotation(Shore.Position+Vector3.up*1.5f,Quaternion.LookRotation(npc.position+Vector3.up*1.4f-Shore.Position-Vector3.up*1.5f));
                if(!Residents.Talk(0))throw new InvalidOperationException("NPC fixture dialogue rejected");Residents.Choose(0);Residents.Reveal();return;
            }
            if((selected>=FishingAcceptanceCase.WindSanctuary && selected<=FishingAcceptanceCase.StarSanctuary) || selected==FishingAcceptanceCase.StarObservation)
            {
                if(!Shore.Relocate(Realms.PortalApproach(island),Quaternion.identity) || !Realms.Enter(island))
                    throw new InvalidOperationException("Sanctuary fixture entry rejected "+island);
                if(selected==FishingAcceptanceCase.StarObservation)StartCoroutine(PrepareObservationFixture());
                return;
            }
            if(selected==FishingAcceptanceCase.RegionArrival)
            {
                ToggleView();View.ApplyShoreLookDelta(new Vector2(900,0));Regions.Show(FishingAdventure.Name(0),FishingLore.Subtitle(0),0);return;
            }
            if(selected==FishingAcceptanceCase.SwimmingView)
            {
                if(!Shore.Relocate(FishingIslandWorld.Center(0)+new Vector3(0,1.12f,33),Quaternion.identity))throw new InvalidOperationException("Swimming fixture jetty rejected");
                viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.right);
                for(int i=0;i<65;i++)Shore.Move(Vector2.up,true,false,false,.05f);
                if(!Shore.Swimming)throw new InvalidOperationException("Swimming fixture did not leave jetty");
                ToggleView();View.ResetLook();return;
            }
            FishingClimbRoute route=null;
            foreach(var candidate in FindObjectsOfType<FishingClimbRoute>())if(FlatDistance(candidate.Bottom,FishingIslandWorld.Center(0))<35)route=candidate;
            if(route==null)throw new InvalidOperationException("Climbing fixture has no authored route");
            StartCoroutine(PrepareClimbingFixture(route));
        }
        private System.Collections.IEnumerator PrepareClimbingFixture(FishingClimbRoute route)
        {
            yield return null;yield return null;
            if(Acceptance.CurrentCase!=FishingAcceptanceCase.ClimbingView)yield break;
            var outward=Vector3.ProjectOnPlane(route.ClimbEnd-route.Top,Vector3.up).normalized;
            if(outward.sqrMagnitude<.1f)outward=Vector3.forward;
            var approach=route.Bottom+outward*(Shore.Body.radius+.4f);
            if(!Shore.Ground(approach,out var ladderFoot))throw new InvalidOperationException("Climbing showcase has no ground at "+approach);
            if(!Shore.Relocate(ladderFoot,Quaternion.identity))throw new InvalidOperationException("Climbing showcase capsule is obstructed at "+ladderFoot);
            yield return null;
            if(Acceptance.CurrentCase!=FishingAcceptanceCase.ClimbingView)yield break;
            if(!Shore.TryClimb())throw new InvalidOperationException("Climbing showcase input is blocked: paused="+Paused+" story="+StoryBlocksGameplay+" shop="+ShopBlocksGameplay+" action="+WorldActionBusy);
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(route.Bottom,route.ClimbEnd)/1.8f*.45f/.05f));
            for(int i=0;i<steps;i++)Shore.Move(Vector2.up,false,false,false,.05f);
            ToggleView();View.ResetLook();
        }
    }
}
