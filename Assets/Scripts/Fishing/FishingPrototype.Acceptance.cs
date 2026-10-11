using System;
using System.IO;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingAcceptanceOptions AcceptanceOptions { get; private set; }
        public FishingAcceptance Acceptance { get; private set; }
        public bool HasIsolatedProfile => Automated || AcceptanceOptions != null;

        private void ReadAcceptanceOptions()
        {
            try
            {
                AcceptanceOptions = FishingAcceptanceOptions.Parse(Environment.GetCommandLineArgs());
                if (AcceptanceOptions != null && Application.isBatchMode && !AcceptanceOptions.Smoke)
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
            CancelWorldAction();_workshopReceipt=false;TackleShop?.Close();
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
            Shore.RefreshMarkers();
            FieldWorld.RefreshPickups();
            WorkshopWorld.RefreshFromCareer();
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
            var climate = Weather.Climate;
            climate.Hold();
            for (int i = 0; i < 64 && climate.Blend < 1f; i++) climate.Tick(.25f, false);
            if (!climate.Select(selected == FishingAcceptanceCase.LegendLeap ? BayWeather.Sunset : BayWeather.Clear))
                throw new InvalidOperationException("Acceptance climate could not finish its prior transition.");
            for (int i = 0; i < 64 && climate.Blend < 1f; i++) climate.Tick(.25f, false);
            if (selected == FishingAcceptanceCase.FreePlay) climate.ResumeCycle();
            Notice(string.Empty, 0f);

            CastMotion.Reset();
            if (FishingAcceptanceCases.IsCatchCase(selected) || selected == FishingAcceptanceCase.FullHold || selected == FishingAcceptanceCase.Details || selected == FishingAcceptanceCase.CastFlight)
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
                if (selected == FishingAcceptanceCase.Catch && !CatchRig.CaptureAndStoreForAutomation())
                    throw new InvalidOperationException("Acceptance catch did not pass the real scoop/store path.");
                _previousPhase = Session.Phase;
            }
            switch (selected)
            {
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
    }
}
