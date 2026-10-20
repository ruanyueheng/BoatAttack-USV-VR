using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawLetter()
        {
            var letter=Controller.Letter;
            var r=new Rect(570,_height-106,460,48);
            string label=letter.Ready?"E / Esc  收起来信":"E  跳过拆封  ·  Esc 收起";
            if(SeaButton(new Rect(r.x+14,r.y+8,r.width-28,32),label,false,true,16))
            {if(letter.Ready)letter.Close();else letter.Reveal();}
        }
    }
}
