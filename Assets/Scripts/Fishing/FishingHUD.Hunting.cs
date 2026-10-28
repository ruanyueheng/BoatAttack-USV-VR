using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawHuntingAim()
        {
            if(UnityEngine.XR.XRSettings.isDeviceActive)return;
            if(Controller.Combat!=null && Controller.Combat.Status.Length>0)
            {
                ReadingVeil(new Rect(530,104,540,36));
                WorldLabel(new Rect(540,108,520,28),Controller.Combat.Status,18,CreamInk,true,TextAnchor.MiddleCenter);
            }
            var hunt=Controller.RifleHunting;
            if(hunt==null || !hunt.Equipped || !Controller.Ashore || Controller.ShopBlocksGameplay || Controller.StoryBlocksGameplay || Controller.WorldActionBlocksGameplay)return;
            float x=800,y=_height*.5f,gap=5+hunt.SpreadDegrees*5;var color=hunt.Obstructed?Danger:Mint;
            Fill(x-gap-7,y-1,7,2,color);Fill(x+gap,y-1,7,2,color);Fill(x-1,y-gap-7,2,7,color);Fill(x-1,y+gap,2,7,color);
            Label(new Rect(x-270,y+28,540,28),hunt.Obstructed?"枪口受阻 · 调整握枪方向":hunt.Aiming?"瞄准 · 短点射稳定，持续射击会抬高枪口":"左键长按连射 · 右键按住瞄准 · F 单发",14,color,false,TextAnchor.MiddleCenter);
        }
    }
}

