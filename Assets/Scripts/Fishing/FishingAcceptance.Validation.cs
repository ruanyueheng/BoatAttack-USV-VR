using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingAcceptance
    {
        private IEnumerator RunCases(bool capture)
        {
            yield return new WaitForSecondsRealtime(1f);
            string baseline = null;
            string normalPath = Path.Combine(Application.persistentDataPath, "boatattack-fishing-v13.json");
            string normalStamp = SaveStamp(normalPath), backupStamp = SaveStamp(normalPath + ".bak");
            string legacyPath=Path.Combine(Application.persistentDataPath,"boatattack-fishing-v12.json");
            string legacyStamp=SaveStamp(legacyPath),legacyBackupStamp=SaveStamp(legacyPath+".bak");
            int comfort = PlayerPrefs.GetInt("Fishing.V3.Comfort", int.MinValue), quiet = PlayerPrefs.GetInt("Fishing.V3.Quiet", int.MinValue);
            if (!Check(() =>
            {
                SelectCase(FishingAcceptanceCase.FreePlay);
                baseline = JsonUtility.ToJson(Controller.Career.Snapshot());
                ValidateToolbarGates();
            })) yield break;
            foreach (FishingAcceptanceCase selected in Enum.GetValues(typeof(FishingAcceptanceCase)))
            {
                if (!Check(() => SelectCase(selected))) yield break;
                while(Controller.Realms.Transitioning)yield return null;
                float prepareDeadline=Time.realtimeSinceStartup+20;
                while(Controller.AcceptanceFixturePreparing && Failure==null && Time.realtimeSinceStartup<prepareDeadline)yield return null;
                if(Failure!=null)yield break;
                if(!Check(()=>{if(Controller.AcceptanceFixturePreparing)throw new TimeoutException("Acceptance fixture setup exceeded twenty seconds: "+selected);}))yield break;
                float remaining = Controller.Session.Remaining, tension = Controller.Session.Tension;
                yield return new WaitForSecondsRealtime(capture ? 1f : .45f);
                if (!Check(() =>
                {
                    ValidateCase();
                    if (FishingAcceptanceCases.IsCatchCase(selected)
                        && (Controller.Session.Remaining != remaining || Controller.Session.Tension != tension))
                        throw new InvalidOperationException("Frozen fixture advanced its catch clock: " + selected);
                })) yield break;
                if (!capture && selected == FishingAcceptanceCase.Bite)
                {
                    FreezeScenario = false;
                    yield return new WaitForSecondsRealtime(.2f);
                    FreezeScenario = true;
                    if (!Check(() => { if (Controller.Session.Remaining >= remaining) throw new InvalidOperationException("Unfreezing did not resume the real catch clock."); })) yield break;
                }
                if (!capture && (selected == FishingAcceptanceCase.Catch || selected==FishingAcceptanceCase.CatchNight))
                {
                    yield return new WaitForSecondsRealtime(5f);
                    if (!Check(() =>
                    {
                        var hud = Controller.GetComponent<FishingHUD>();
                        if (hud.NoticeText.Length != 0 || Controller.VisibleMessage.Length == 0)
                            throw new InvalidOperationException("Frozen catch card did not retain its lifetime beyond five seconds.");
                        Controller.CycleBait();
                        if (hud.NoticeText != Controller.VisibleMessage || hud.NoticeText.Length == 0)
                            throw new InvalidOperationException("Frozen catch card swallowed subsequent real action feedback.");
                    })) yield break;
                }
                if (capture)
                {
                    yield return CaptureFrame();
                    if (Failure != null) yield break;
                }
                if(selected==FishingAcceptanceCase.PortalExit)
                {
                    if(!Check(()=>{if(!Controller.RequestWorldInteraction())throw new InvalidOperationException("Terminal portal capture cannot return through its real interaction.");}))yield break;
                    while(Controller.Realms.Transitioning)yield return null;
                    if(!Check(()=>{if(Controller.Realms.Inside)throw new InvalidOperationException("Terminal portal failed to return the player to the island.");}))yield break;
                }
            }
            if (!Check(() =>
            {
                SelectCase(FishingAcceptanceCase.FreePlay);
                if (JsonUtility.ToJson(Controller.Career.Snapshot()) != baseline)
                    throw new InvalidOperationException("Fixture switching changed the isolated free-play progress.");
                Controller.ToggleComfort(); Controller.ToggleComfort(); Controller.ToggleQuiet(); Controller.ToggleQuiet();
                if (PlayerPrefs.GetInt("Fishing.V3.Comfort", int.MinValue) != comfort || PlayerPrefs.GetInt("Fishing.V3.Quiet", int.MinValue) != quiet)
                    throw new InvalidOperationException("Acceptance wrote normal-player preferences.");
                if (SaveStamp(normalPath) != normalStamp || SaveStamp(normalPath + ".bak") != backupStamp)
                    throw new InvalidOperationException("Normal-player save metadata changed during acceptance.");
                if(SaveStamp(legacyPath)!=legacyStamp || SaveStamp(legacyPath+".bak")!=legacyBackupStamp)
                    throw new InvalidOperationException("Legacy-player save metadata changed during acceptance.");
                if (capture && (_manifest == null || _manifest.captures.Count != FishingAcceptanceCases.Count))
                    throw new InvalidOperationException("The screenshot matrix is incomplete.");
                Debug.Log("[FISHING-ACCEPTANCE] Profiles: fixtures preserve free-play data, preferences and normal-player save/backup metadata.");
            })) yield break;
            Finish();
        }

        private void ValidateCase()
        {
            var c = Controller;
            c.ValidateV036AcceptanceFixture(CurrentCase);
            if(CurrentCase==FishingAcceptanceCase.SandSieve || CurrentCase==FishingAcceptanceCase.RopeWinder)
            {
                if(!c.HandGames.Active || !c.StoryBlocksGameplay || !c.HandGames.Task.HasMaterials(c.Career.Backpack))
                    throw new InvalidOperationException("Handcraft must use a reachable production interaction with unspent real materials.");
            }
            if(CurrentCase==FishingAcceptanceCase.StartMenu || CurrentCase==FishingAcceptanceCase.MenuControls || CurrentCase==FishingAcceptanceCase.MenuGraphics || CurrentCase==FishingAcceptanceCase.MenuAudio || CurrentCase==FishingAcceptanceCase.Departure)
            {
                if(!c.StartMenuOpen || !c.Paused || c.TitleVoyage.Boat==null)throw new InvalidOperationException("Live title presentation fixture failed");
                if(CurrentCase==FishingAcceptanceCase.Departure && !c.TitleVoyage.Sailing)throw new InvalidOperationException("Title boat never began sailing");
                if(CurrentCase==FishingAcceptanceCase.MenuGraphics && c.GetComponent<FishingHUD>().SettingsTab!=1)throw new InvalidOperationException("Graphics settings fixture has the wrong tab");
                if(CurrentCase==FishingAcceptanceCase.MenuAudio && c.GetComponent<FishingHUD>().SettingsTab!=2)throw new InvalidOperationException("Audio settings fixture has the wrong tab");
            }
            if(CurrentCase==FishingAcceptanceCase.SaveSlots || CurrentCase==FishingAcceptanceCase.SaveOverwrite)
            {
                if(!c.StartMenuOpen || !c.Paused || !c.SaveMenuOpen || !c.CanContinue || c.TitleVoyage.Boat==null
                    || c.SaveMenuMode!=(CurrentCase==FishingAcceptanceCase.SaveSlots?"load":"save"))
                    throw new InvalidOperationException("存档夹具未打开真实启航菜单和三槽窗口。");
                for(int slot=0;slot<FishingSlotStore.Count;slot++)
                    foreach(var kind in new[]{FishingSlotKind.Manual,FishingSlotKind.Auto})
                    {
                        var info=c.SlotInfo(slot,kind);
                        if(slot==0?!info.Valid:info.Exists)
                            throw new InvalidOperationException("存档夹具必须包含一个有效手动/自动槽和两个空槽。");
                    }
                if(CurrentCase==FishingAcceptanceCase.SaveOverwrite && c.SaveConfirmation.Length==0)
                    throw new InvalidOperationException("覆盖夹具未经过真实保存操作或缺少覆盖确认。");
                if(CurrentCase==FishingAcceptanceCase.SaveSlots && c.SaveConfirmation.Length!=0)
                    throw new InvalidOperationException("读取窗口夹具保留了上一场景的确认弹窗。");
            }
            if(CurrentCase==FishingAcceptanceCase.HomeChart
                && (!c.HomeChartOpen || !c.Paused || !c.Ashore || c.Shore.Swimming
                    || FishingPrototype.FlatDistance(c.Shore.Position,c.Home.WakePoint)>.5f
                    || c.NavigationCount==0 || !c.HomeChartWaypoint(c.HomeNavigationIndex,out _,out _,out _)
                    || c.StartMenuOpen || c.SaveMenuOpen))
                throw new InvalidOperationException("家中海图夹具缺少安全落脚点、独立窗口或真实航点。");
            if(CurrentCase==FishingAcceptanceCase.ArchIslet || CurrentCase==FishingAcceptanceCase.FireflyIslet)
                if(c.NavigationTarget<=c.HomeNavigationIndex || Resources.Load<GameObject>("FishingV032/CoastalArch")==null)throw new InvalidOperationException("New islet/Tripo asset fixture failed");
            if(FishingAcceptanceCases.IsFindCase(CurrentCase))
            {
                var expectedFind=CurrentCase==FishingAcceptanceCase.BootCatch?FieldItem.OldBoot:CurrentCase==FishingAcceptanceCase.CanCatch?FieldItem.RustyCan:FieldItem.SeaTreasure;
                if(c.LastCatch.Find!=expectedFind || c.LastCatch.Fish!=null || c.HasPendingCatch || c.Career.Backpack.Count(expectedFind)!=1)
                    throw new InvalidOperationException("Sea-find feedback or inventory receipt is missing.");
                if(c.GetComponent<FishingHUD>().RegionTitleVisible)throw new InvalidOperationException("Region banner obscures the sea-find catch card.");
            }
            if((CurrentCase==FishingAcceptanceCase.LetterEnvelope || CurrentCase==FishingAcceptanceCase.LetterPaper)
                && (!c.Letter.Open || (CurrentCase==FishingAcceptanceCase.LetterPaper && !c.Letter.Ready)))
                throw new InvalidOperationException("Stationery capture is missing its actual envelope or readable paper.");
            if(CurrentCase==FishingAcceptanceCase.HomeBarbecue && (!c.CoastWorld.NearGrill || !c.CoastWorld.MenuOpen || c.Career.Count!=3))
                throw new InvalidOperationException("Barbecue capture lacks the reachable fish-selection menu.");
            if(CurrentCase==FishingAcceptanceCase.HomeGrilledFish)
            {
                var cooked=c.CoastWorld.Grill.Find("Whole grilled sea fish · scored skin and lemon");
                if(!c.CoastWorld.NearGrill || !c.Career.BarbecueReady || c.Career.Count!=2 || cooked==null || !cooked.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Barbecue capture lacks its reserved fish or visible cooked model.");
            }
            if(CurrentCase==FishingAcceptanceCase.CashierDialogue && (!c.DialogueOpen || c.Residents.ActiveId!=4 || c.Residents.ChoiceCount!=4))
                throw new InvalidOperationException("Cashier capture lacks the real four-branch dialogue.");
            if(CurrentCase==FishingAcceptanceCase.HomeInterior || CurrentCase==FishingAcceptanceCase.HomeWorkbench)
            {
                if(!c.Ashore || c.Shore.Swimming || FishingPrototype.FlatDistance(c.Shore.Position,c.Home.Origin)>6)
                    throw new InvalidOperationException("Home capture did not reach the interior.");
                if(c.Regions.Title!="回潮渔屋")throw new InvalidOperationException("Home capture retained a previous island title.");
                int stations=0;
                foreach(var station in c.Career.Workshop.Stations)
                    if(station.permanent && FishingPrototype.FlatDistance(new Vector3(station.x,station.y,station.z),c.Home.Origin)<7)stations++;
                if(stations!=3)throw new InvalidOperationException("Home capture requires all three usable stations.");
                if(CurrentCase==FishingAcceptanceCase.HomeWorkbench && (!c.InventoryOpen || c.WorkshopWorld.Context!=CraftStation.Workbench))
                    throw new InvalidOperationException("Home capture is missing the real workbench crafting page.");
            }
            if(CurrentCase==FishingAcceptanceCase.StarObservation && (!c.Realms.ObservingStars || c.Realms.Island!=3 || c.Realms.Trial.StarFocus<.2f))throw new InvalidOperationException("Observation fixture lacks the actual continuous focus state.");
            if((CurrentCase==FishingAcceptanceCase.HomeGarden || CurrentCase==FishingAcceptanceCase.FarmHarvest || CurrentCase==FishingAcceptanceCase.HomeGardenNight) && (!c.Ashore || !c.Farm.Nearby || !c.Career.Farm.Ready(0) || c.Career.Farm.Plot(8).growth!=1))throw new InvalidOperationException("Garden fixture lacks its saved crops at distinct actual growth stages.");
            if(CurrentCase>=FishingAcceptanceCase.WindSanctuary && CurrentCase<=FishingAcceptanceCase.StarSanctuary
                && (!c.Realms.Inside || c.Realms.Island!=(int)CurrentCase-(int)FishingAcceptanceCase.WindSanctuary))throw new InvalidOperationException("V19 sanctuary capture state failed");
            if(CurrentCase==FishingAcceptanceCase.ResidentDialogue && (!c.DialogueOpen || c.Residents.ActiveId!=0))throw new InvalidOperationException("V19 dialogue capture state failed");
            if(CurrentCase==FishingAcceptanceCase.SwimmingView && !c.Shore.Swimming)throw new InvalidOperationException("V19 swimming capture state failed");
            if(CurrentCase==FishingAcceptanceCase.ClimbingView && !c.Shore.Climbing)throw new InvalidOperationException("V19 climbing capture state failed");
            if(CurrentCase==FishingAcceptanceCase.LoreJournal && (!c.JournalOpen || c.GetComponent<FishingHUD>().JournalPage!=6))throw new InvalidOperationException("V19 lore journal failed");
            string expected = Options.ProfilePath(Application.temporaryCachePath, CurrentCase != FishingAcceptanceCase.FreePlay);
            if (!c.HasIsolatedProfile || c.SavePath != expected || !File.Exists(expected) || c.GetComponent<FishingAutoplay>() != null)
                throw new InvalidOperationException("Acceptance profile/driver isolation failed.");
            if (CurrentCase == FishingAcceptanceCase.Bite && c.Session.Phase != CatchPhase.Bite) throw new InvalidOperationException("Bite fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.Fighting && (c.Session.Phase != CatchPhase.Fighting || c.Session.Tension < .78f)) throw new InvalidOperationException("Fight fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.Landing && (!c.Session.CanLand || !c.NetDeployed)) throw new InvalidOperationException("Landing/net fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.NightBoat && (!c.Career.Calendar.Night || c.FirstPerson || c.Weather.DeckLight==null || !c.Weather.DeckLight.enabled))
                throw new InvalidOperationException("Night boat fixture did not enable local deck illumination.");
            if ((CurrentCase == FishingAcceptanceCase.Catch || CurrentCase==FishingAcceptanceCase.CatchNight) && (c.Session.Phase != CatchPhase.Caught || c.LastCatch.Fish == null || c.Career.Count != 1)) throw new InvalidOperationException("Catch/store fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.LegendLeap && (c.Session.Kind != FishKind.Sailfish || c.Session.Beat != FishBeat.Leap)) throw new InvalidOperationException("Legend fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.FullHold && (c.Career.Count != c.Career.Capacity || c.Cast())) throw new InvalidOperationException("Full-hold cast guard failed.");
            if ((CurrentCase == FishingAcceptanceCase.HarborOrders || CurrentCase == FishingAcceptanceCase.MaxUpgrades)
                && (!c.AtHarbor || !c.HarborDeskOpen || c.Career.CurrentOrder.LineCount != 3 || !c.Career.OrderReady))
                throw new InvalidOperationException("Harbor fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.MaxUpgrades && (c.Career.LineLevel != 2 || c.Career.ReelLevel != 2 || c.Career.HoldLevel != 2)) throw new InvalidOperationException("Upgrade fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.Details && (!c.ShowDetails || c.HarborDeskOpen)) throw new InvalidOperationException("Details fixture failed.");
            if ((CurrentCase == FishingAcceptanceCase.Journal || CurrentCase==FishingAcceptanceCase.HunyuanCatalog || CurrentCase==FishingAcceptanceCase.HunyuanNightCatalog)
                && (!c.JournalOpen || !c.Paused)) throw new InvalidOperationException("Journal fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.CastFlight && (!c.CastMotion.Casting || c.CastMotion.FlightProgress<=0 || c.CastMotion.FlightProgress>=1))
                throw new InvalidOperationException("Cast flight fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.Settings && (c.JournalOpen || !c.Paused)) throw new InvalidOperationException("Settings fixture failed.");
            if(FishingAcceptanceCases.IsShoreCase(CurrentCase) && !FishingAcceptanceCases.IsShopCase(CurrentCase))
            {
                bool thirdPerson=CurrentCase==FishingAcceptanceCase.ShoreThirdPerson || CurrentCase==FishingAcceptanceCase.BinocularThirdPerson;
                var item=FishingAcceptanceCases.ViewItem(CurrentCase);
                bool visible=item==FieldItem.Lantern
                    ?c.FieldWorld.LanternHeld && c.FieldWorld.HeldTool.gameObject.activeInHierarchy
                    :c.FieldWorld.HeldTool!=null && c.FieldWorld.HeldTool.gameObject.activeInHierarchy && c.FieldWorld.PresentedItem==item;
                if(!c.Ashore || c.FirstPerson==thirdPerson || !visible)
                    throw new InvalidOperationException("Shore avatar/tool capture fixture failed.");
                if(CurrentCase==FishingAcceptanceCase.BinocularObserve && (!c.View.OpticalView || c.FieldWorld.HeldToolRenderer.enabled
                    || Mathf.Abs(c.viewCamera.fieldOfView-FishingViewRig.BinocularFieldOfView)>.01f))
                    throw new InvalidOperationException("光学观察夹具没有完成举镜或仍有手物遮挡。");
                if(CurrentCase==FishingAcceptanceCase.BinocularThirdPerson && (!c.BinocularsRaised || !c.FieldWorld.HeldToolRenderer.enabled))
                    throw new InvalidOperationException("第三人称举镜实物未恢复。");
                if(CurrentCase==FishingAcceptanceCase.LookDownCrouched && (!c.Shore.Crouched || !c.Shore.Character.IsStanceSettled(true,true)))
                    throw new InvalidOperationException("低头蹲姿夹具未落位。");
            }
            if(CurrentCase==FishingAcceptanceCase.ShopCounter && (!c.Ashore || !c.ShopOpen || !c.TackleShop.NearCounter))
                throw new InvalidOperationException("Real shop counter capture fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.ShopSupplies && (!c.Ashore || !c.ShopOpen || c.GetComponent<FishingHUD>().ShopCategory!=2))throw new InvalidOperationException("Supply fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.ShopRations && (!c.Ashore || !c.ShopOpen || c.GetComponent<FishingHUD>().ShopCategory!=4))throw new InvalidOperationException("V19 ration shop fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.LuxuryBoat && (c.Career.CabinLevel!=2 || c.FirstPerson || c.Player.engine.horsePower!=FishingFleet.Power(2)))throw new InvalidOperationException("Fleet fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.IslandLanding && (!c.Ashore || c.Shore.LocationName!=FishingAdventure.Name(0)))throw new InvalidOperationException("Island fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.IslandJournal && (!c.JournalOpen || c.GetComponent<FishingHUD>().JournalPage!=5))throw new InvalidOperationException("Island journal fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.ShopAttendant)
            {
                if(!c.Ashore || c.ShopOpen || c.TackleShop.Cashier==null)throw new InvalidOperationException("店员夹具缺少实际角色，或被商店菜单替代。");
                var face=c.TackleShop.Cashier.FacePosition;var point=c.viewCamera.WorldToViewportPoint(face);
                if(point.z<=c.viewCamera.nearClipPlane || point.x<.05f || point.x>.95f || point.y<.05f || point.y>.95f
                    || !c.Shore.ClearSight(c.viewCamera.transform.position,face))throw new InvalidOperationException("实际相机看不到店员面部。");
            }
        }

        private static void ValidateToolbarGates()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1920, 810), new Vector2Int(1280, 1024) })
            {
                float scale = FishingHUD.LayoutScale(size.x, size.y), offset = (size.x - 1600 * scale) * .5f;
                var center = FishingHUD.AcceptanceBanner.center;
                var pointer = new Vector2(offset + center.x * scale, size.y - center.y * scale);
                if (!FishingHUD.BlocksToolPointer(pointer, size.x, size.y, acceptance: true)
                    || FishingHUD.BlocksToolPointer(pointer, size.x, size.y))
                    throw new InvalidOperationException("Acceptance toolbar leaked input or changed the normal HUD gate.");
            }
        }

        private static string SaveStamp(string path)
        {
            if (!File.Exists(path)) return "absent";
            var info = new FileInfo(path);
            return info.Length + ":" + info.LastWriteTimeUtc.Ticks;
        }

        private bool Check(Action action)
        {
            try { action(); return true; }
            catch (Exception ex) { Fail(ex); return false; }
        }
    }
}
