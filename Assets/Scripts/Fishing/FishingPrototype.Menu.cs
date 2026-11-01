using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public bool StartMenuOpen { get; private set; }
        public bool MenuControls { get; set; }
        public bool GuideOpening { get; private set; }
        public bool ReadingFieldGuide { get; private set; }
        public float GuideOpenAmount { get; private set; }
        public float GuideReachAmount=>GuideOpening?Mathf.SmoothStep(0,1,Mathf.Clamp01(_guideAge/.18f)):0;
        private float _guideAge;
        private int _windowWidth=1280,_windowHeight=720;
        private bool _titleStarted;
        public bool DesktopDisplay => !XRSettings.isDeviceActive && Application.platform!=RuntimePlatform.Android;

        public void OpenStartMenu()
        {
            if(!DesktopDisplay || Prologue.Open || Home.Sleeping || HasPendingCatch || StoryBlocksGameplay)return;
            CloseHomeChart(true);Save();SaveSettings();
            if(InventoryOpen)ToggleInventory();
            if(JournalOpen)ToggleJournal();
            if(!Paused)TogglePause();
            CoastWorld.ClearFindPresentation();StartMenuOpen=true;MenuControls=false;SaveMenuOpen=false;SaveMenuMessage=string.Empty;TitleVoyage.Open();ApplyAudioSettings();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void StartFromMenu()
        {
            if(!StartMenuOpen)return;
            if(TitleVoyage.Sailing)return;
            if(CanContinue && HasIsolatedProfile)TitleVoyage.Sail();
            else OpenSaveMenu("new");
        }
        public void FinishTitleVoyage()
        {
            if(!StartMenuOpen)return;
            bool resumeLook=TitleVoyage.ResumeLook;TitleVoyage.Close(resumeLook);
            StartMenuOpen=false;MenuControls=false;
            if(Paused)TogglePause();
            ApplyAudioSettings();
            if(!resumeLook && !Automated && !HasIsolatedProfile && !Career.PrologueSeen)Prologue.Begin();
            _titleStarted=true;
        }
        public void SetFullscreen(bool fullscreen)
        {
            if(!DesktopDisplay)return;
            if(fullscreen && !Screen.fullScreen){_windowWidth=Screen.width;_windowHeight=Screen.height;}
            var display=Screen.currentResolution;
            if(fullscreen)Screen.SetResolution(display.width,display.height,FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(Mathf.Clamp(_windowWidth,640,display.width),Mathf.Clamp(_windowHeight,480,display.height),FullScreenMode.Windowed);
            PersistDisplaySettings(fullscreen,_windowWidth,_windowHeight);
        }
        private void InitializeStartMenu()
        {
            if(Automated || HasIsolatedProfile || !DesktopDisplay)return;
            OpenStartMenu();
        }
        private bool _displayKeyHeld;
        public void HandleDisplayShortcut(Event input)
        {
            if(!DesktopDisplay || input.keyCode!=KeyCode.F11)return;
            if(input.type==EventType.KeyUp){_displayKeyHeld=false;input.Use();}
            else if(input.type==EventType.KeyDown)
            {if(!_displayKeyHeld){_displayKeyHeld=true;SetFullscreen(!Screen.fullScreen);}input.Use();}
        }
        private bool TickStartMenu(Keyboard keyboard)
        {
            if(!StartMenuOpen)return false;
            if(keyboard!=null && !Automated && Application.isFocused && !TitleVoyage.Sailing)
            {
                if(keyboard.escapeKey.wasPressedThisFrame)
                {
                    if(ClassroomMenuOpen)CloseClassroomMenu();else if(SaveConfirmation.Length>0)CancelSaveConfirmation();else if(SaveMenuOpen)CloseSaveMenu();else MenuControls=false;
                    SaveSettings();
                }
                else if(keyboard.enterKey.wasPressedThisFrame && !keyboard.leftAltKey.isPressed && !keyboard.rightAltKey.isPressed && !MenuControls && !SaveMenuOpen && !ClassroomMenuOpen)
                {if(CanContinue)ContinueFromMenu();else StartFromMenu();}
            }
            return true;
        }
        public bool OpenFieldGuide()
        {
            if(Paused || GuideOpening || !Ashore || Shore.Swimming || Shore.Climbing || StoryBlocksGameplay || ShopBlocksGameplay || WorldActionBlocksGameplay || Career.Backpack.Equipped!=FieldItem.FieldGuide)return false;
            if(XRSettings.isDeviceActive){XRNotebook.Open(15);return true;}
            EndItemInspection();LowerFieldTools();CoastWorld.ClearFindPresentation();FieldWorld.ClearToolPresentation();Hands.ResetInteraction();Shore.ResetMoveInput();View.ResetInput();
            GuideOpening=true;ReadingFieldGuide=true;GuideOpenAmount=0;_guideAge=0;
            return true;
        }
        private bool TickGuideOpening(Keyboard keyboard,bool focused)
        {
            if(!GuideOpening)return false;
            if(Acceptance!=null && Acceptance.FreezeScenario && Acceptance.CurrentCase==FishingAcceptanceCase.GuideOpening)return true;
            if(!focused || Paused || Career.Backpack.Equipped!=FieldItem.FieldGuide || !Ashore || Shore.Swimming || Shore.Climbing
                || (!Automated && keyboard!=null && keyboard.escapeKey.wasPressedThisFrame))
            {CancelGuideOpening();return true;}
            _guideAge+=Mathf.Min(Time.unscaledDeltaTime,.05f);GuideOpenAmount=Mathf.SmoothStep(0,1,Mathf.Clamp01((_guideAge-.18f)/.78f));
            if(_guideAge>=1.25f)
            {
                GuideOpening=false;ToggleJournal();
                if(JournalOpen)_hud.JournalPage=2;else ReadingFieldGuide=false;
            }
            return true;
        }
        private void CancelGuideOpening(){GuideOpening=false;ReadingFieldGuide=false;GuideOpenAmount=0;}
    }
}
