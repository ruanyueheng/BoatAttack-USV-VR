using UnityEngine;
using UnityEngine.InputSystem;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public FishingResidents Residents { get; private set; }
        public FishingRealmWorld Realms { get; private set; }
        public FishingRegionPresentation Regions { get; private set; }
        public FishingLetter Letter { get; private set; }
        public FishingWaterRoute WaterRoute { get; private set; }
        private int _storyConsumedFrame = -1;
        public bool DialogueOpen => Residents != null && Residents.Open;
        public bool StoryBlocksGameplay => DialogueOpen || (Letter != null && Letter.Open) || (Realms != null && Realms.Transitioning) || _storyConsumedFrame == Time.frameCount;
        internal void StoryModalChanged()
        {
            _storyConsumedFrame = Time.frameCount; CancelWorldAction(); LowerFieldTools();
            _pointerGate.Reset(); _gamepadGate.Reset(); View?.ResetInput(); Shore?.ResetMoveInput(); Hands?.ResetInteraction(); XRNotebook?.ResetInput();
            ShowDetails = false; HarborDeskOpen = false;
        }
        private bool TickStoryInput(Keyboard keyboard, bool focused, bool xr)
        {
            Regions.Tick(); Realms.Tick();
            if (Realms.Transitioning) return true;
            if (Letter.Open)
            {
                if (!focused || Paused) { Letter.Close(); return true; }
                Letter.Tick(Time.unscaledDeltaTime,xr,!Automated);
                if (!xr && !Automated && keyboard != null)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame) Letter.Close();
                    else if (keyboard.rKey.wasPressedThisFrame) ReturnToHarbor();
                    else if (keyboard.eKey.wasPressedThisFrame) { if(Letter.Ready)Letter.Close();else Letter.Reveal(); }
                }
                return true;
            }
            if (DialogueOpen)
            {
                if (!focused || Paused || !Residents.InRange(Residents.ActiveId)) { Residents.Close(); return true; }
                if (xr)
                {
                    XRNotebook.Tick(focused && !Automated);
                    if (!XRNotebook.IsOpen) Residents.Close();
                }
                else if (focused && !Automated && keyboard != null)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame) Residents.Close();
                    else if (keyboard.rKey.wasPressedThisFrame) ReturnToHarbor();
                    else if (keyboard.eKey.wasPressedThisFrame) Residents.Reveal();
                    else if (keyboard.digit1Key.wasPressedThisFrame) Residents.Choose(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) Residents.Choose(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) Residents.Choose(2);
                }
                return true;
            }
            return StoryBlocksGameplay;
        }
    }
}
