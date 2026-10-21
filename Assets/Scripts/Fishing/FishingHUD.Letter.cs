using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawLetter()
        {
            var letter=Controller.Letter;
            var r=new Rect(548,_height-88,504,48);
            StoryPaper(r,false,25);
            string label=letter.Ready?"E / Esc  收起来信":"E  跳过拆封  ·  Esc 收起";
            DisplayText(new Rect(r.x+22,r.y+6,r.width-44,36),label,17,InkText,false,TextAnchor.MiddleCenter);
            if(GUI.Button(new Rect(r.x+12,r.y+5,r.width-24,38),GUIContent.none,GUIStyle.none))
            {if(letter.Ready)letter.Close();else letter.Reveal();}
        }
    }
}
