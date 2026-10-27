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
            int total=game.Task.Kind==FishingHandTaskKind.Winder?3:game.Task.StrokeTarget;
            for(int i=0;i<total;i++)Disc(new Vector2(70+i*18,142),3.5f,i<game.Count?AmberInk:new Color(.75f,.75f,.70f,.45f));
            WorldLabel(64,165,430,24,game.Count+" / "+total+"    ·    完成前不扣材料",15,CreamInk);
            if(game.Task.Kind==FishingHandTaskKind.Winder)WorldLabel(64,200,560,28,"纤维×3 → 绳索×"+game.Task.OutputCount+(Controller.Career.Lore.BlueprintUnlocked(0)?" · 精纺工艺已解锁":" · 风境蓝图可提升产量"),16,CreamInk);
            if(game.Task.Kind==FishingHandTaskKind.Forge && Button(new Rect(64,205,430,38),game.ForgeGunParts?"切换：铁镐头（重新开始）":"切换：枪械铁件（重新开始）",true,false,15))game.SwitchForgeOutput();
            WorldLabel(64,_height-55,320,28,"Esc  收起手作",16,CreamInk);
        }
    }
}
