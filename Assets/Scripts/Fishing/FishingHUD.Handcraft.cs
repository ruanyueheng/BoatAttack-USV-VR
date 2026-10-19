using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawHandcraft()
        {
            var game=Controller.HandGames;
            WorldLabel(62,42,430,42,FishingHandGames.Name((int)game.Task.Kind),28,CreamInk,true);
            WorldLabel(64,92,560,28,game.Instruction,18,CreamInk);
            int total=game.Task.Kind==FishingHandTaskKind.Sieve?12:3;
            for(int i=0;i<total;i++)Disc(new Vector2(70+i*18,142),3.5f,i<game.Count?AmberInk:new Color(.75f,.75f,.70f,.45f));
            WorldLabel(64,165,430,24,game.Count+" / "+total+"    ·    完成前不扣材料",15,CreamInk);
            WorldLabel(64,_height-55,320,28,"Esc  收起手作",16,CreamInk);
        }
    }
}
