using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawHuntingAim()
        {
            var hunt=Controller.RifleHunting;
            if(hunt==null || !hunt.Equipped || !Controller.Ashore || Controller.ShopBlocksGameplay || Controller.StoryBlocksGameplay || Controller.WorldActionBlocksGameplay)return;
            float x=800,y=_height*.5f;var color=hunt.Obstructed?Danger:Mint;
            Fill(x-12,y-1,7,2,color);Fill(x+5,y-1,7,2,color);Fill(x-1,y-12,2,7,color);Fill(x-1,y+5,2,7,color);
            Label(new Rect(x-230,y+22,460,28),hunt.Obstructed?"空间不足 · 枪已收起":"左键 / F 单发 · 命中后靠近 E 收取猎物",14,color,false,TextAnchor.MiddleCenter);
        }
    }
}
