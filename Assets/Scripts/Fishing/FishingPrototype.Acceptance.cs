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
            if (JournalOpen) ToggleJournal();
            else if (Paused) TogglePause();
            LeaveStation();
            Teleport(spawnPosition, spawnRotation);
            UpdateNearestSpot();
            SavePath = AcceptanceOptions.ProfilePath(Application.temporaryCachePath, fixture != null);
            Career = fixture ?? FishingSaveStore.Load(SavePath, out _);
            SaveWarning = string.Empty;
            LastCatch = default;
            _recorded = false;
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

            if (FishingAcceptanceCases.IsCatchCase(selected) || selected == FishingAcceptanceCase.FullHold || selected == FishingAcceptanceCase.Details)
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
                FishingAcceptanceCases.PrepareSession(Session, selected, Career);
                _fish.SetActive(false);
                Destroy(_fish);
                _fish = MakeFish(transform, Session.Kind, Session.Rare);
                AnimateEquipment();
                if (selected == FishingAcceptanceCase.Catch && !Scoop())
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
                case FishingAcceptanceCase.Settings: TogglePause(); break;
                case FishingAcceptanceCase.FreePlay:
                    Notice("独立验收进度 · F5/F6 查看预置场景，F9 返回实玩；普通玩家存档不受影响。", 8f);
                    break;
            }
            Save();
            if (SaveWarning.Length > 0) throw new IOException("Cannot persist the isolated acceptance profile.");
            View.ApplyView();
        }
    }
}
