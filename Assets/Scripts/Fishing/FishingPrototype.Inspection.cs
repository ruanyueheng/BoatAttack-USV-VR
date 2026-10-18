using UnityEngine;
using UnityEngine.InputSystem;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        public bool InspectingItem {get;private set;}
        public FieldItem InspectedItem {get;private set;}
        public Vector2 InspectionAngles {get;private set;}
        public bool InspectFieldItem(int slot)
        {
            if(Career==null || ShopBlocksGameplay || StoryBlocksGameplay || HasPendingCatch || WorldActionBusy
                || (Paused && !InventoryOpen) || (!CanChangeBait && Anchored) || Shore.Swimming || Shore.Climbing)return false;
            var stack=Career.Backpack.At(slot);if(stack.count<=0)return false;
            if(InventoryOpen)ToggleInventory();
            XRNotebook.Close();CancelWorldAction();FieldWorld.ClearToolPresentation();CastMotion.CancelPreparation();LowerFieldTools();
            InspectingItem=true;InspectedItem=stack.item;InspectionAngles=Vector2.zero;
            Notice("检视 · "+FishingBackpack.Name(stack.item)+(UnityEngine.XR.XRSettings.isDeviceActive?" · 转动右手观看 · 右扳机收起":" · 按住右键转动 · Z / F / Esc 收起"),5);return true;
        }
        public void EndItemInspection(){InspectingItem=false;InspectedItem=FieldItem.Empty;InspectionAngles=Vector2.zero;}
        private bool TickItemInspection(Keyboard keyboard,Mouse mouse,bool focused)
        {
            if(UnityEngine.XR.XRSettings.isDeviceActive)return false;
            if(!Automated && focused && keyboard!=null && keyboard.zKey.wasPressedThisFrame)
            {if(InspectingItem)EndItemInspection();else InspectFieldItem(Career.Backpack.Selected);return true;}
            if(!InspectingItem)return false;
            if(Paused || !focused || ShopBlocksGameplay || StoryBlocksGameplay || Shore.Swimming || Shore.Climbing)
            {EndItemInspection();return true;}
            if(!Automated && keyboard!=null && (keyboard.fKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))EndItemInspection();
            else if(!Automated && mouse!=null && mouse.rightButton.isPressed)
            {
                var delta=mouse.delta.ReadValue();
                InspectionAngles=new Vector2(Mathf.Clamp(InspectionAngles.x-delta.y*.25f,-75,75),Mathf.Repeat(InspectionAngles.y+delta.x*.25f,360));
            }
            return true;
        }
    }
}
