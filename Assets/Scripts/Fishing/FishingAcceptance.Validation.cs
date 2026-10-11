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
            string normalPath = Path.Combine(Application.persistentDataPath, "boatattack-fishing-v10.json");
            string normalStamp = SaveStamp(normalPath), backupStamp = SaveStamp(normalPath + ".bak");
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
                if (!capture && selected == FishingAcceptanceCase.Catch)
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
                if (capture && (_manifest == null || _manifest.captures.Count != FishingAcceptanceCases.Count))
                    throw new InvalidOperationException("The screenshot matrix is incomplete.");
                Debug.Log("[FISHING-ACCEPTANCE] Profiles: fixtures preserve free-play data, preferences and normal-player save/backup metadata.");
            })) yield break;
            Finish();
        }

        private void ValidateCase()
        {
            var c = Controller;
            string expected = Options.ProfilePath(Application.temporaryCachePath, CurrentCase != FishingAcceptanceCase.FreePlay);
            if (!c.HasIsolatedProfile || c.SavePath != expected || !File.Exists(expected) || c.GetComponent<FishingAutoplay>() != null)
                throw new InvalidOperationException("Acceptance profile/driver isolation failed.");
            if (CurrentCase == FishingAcceptanceCase.Bite && c.Session.Phase != CatchPhase.Bite) throw new InvalidOperationException("Bite fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.Fighting && (c.Session.Phase != CatchPhase.Fighting || c.Session.Tension < .78f)) throw new InvalidOperationException("Fight fixture failed.");
            if (CurrentCase == FishingAcceptanceCase.Landing && (!c.Session.CanLand || !c.NetDeployed)) throw new InvalidOperationException("Landing/net fixture failed.");
            if(CurrentCase==FishingAcceptanceCase.NightBoat && (!c.Career.Calendar.Night || c.FirstPerson || c.Weather.DeckLight==null || !c.Weather.DeckLight.enabled))
                throw new InvalidOperationException("Night boat fixture did not enable local deck illumination.");
            if (CurrentCase == FishingAcceptanceCase.Catch && (c.Session.Phase != CatchPhase.Caught || c.LastCatch.Fish == null || c.Career.Count != 1)) throw new InvalidOperationException("Catch/store fixture failed.");
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
                if(!c.Ashore || c.FirstPerson==thirdPerson || c.FieldWorld.HeldTool==null || !c.FieldWorld.HeldTool.gameObject.activeInHierarchy
                    || c.FieldWorld.PresentedItem!=FishingAcceptanceCases.ViewItem(CurrentCase))
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
