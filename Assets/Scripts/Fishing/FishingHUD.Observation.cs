using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void DrawStarObservation()
        {
            var trial=Controller.Realms.Trial;int stage=Mathf.Min(2,trial.StarStage);
            Rounded(new Rect(45,35,310,240),new Color(.035f,.06f,.12f,.90f),9);
            WorldLabel(65,50,270,36,"观测星图 · "+FishingRealmWorld.StarPatternName(stage),23,CreamInk,true);
            var pattern=FishingRealmWorld.StarPattern(stage);var origin=new Vector2(200,172);
            for(int i=0;i<pattern.Count;i++)
            {
                var p=origin+new Vector2(pattern[i].x,-pattern[i].y)*23;
                if(i>0){var prev=origin+new Vector2(pattern[i-1].x,-pattern[i-1].y)*23;Segment(prev,p,new Color(.85f,.76f,.5f,.8f),1.5f);}
                Disc(p,3,AmberInk);
            }
            WorldLabel(65,242,270,25,trial.StarStage+" / 3 幅已记录",17,CreamInk);
            var centre=new Vector2(800,_height*.5f);
            Segment(centre+Vector2.left*12,centre+Vector2.right*12,CreamInk,1);Segment(centre+Vector2.up*12,centre+Vector2.down*12,CreamInk,1);
            Rounded(new Rect(660,_height-105,280,8),new Color(.12f,.16f,.21f,.9f),4);
            Rounded(new Rect(660,_height-105,280*trial.StarFocus,8),AmberInk,4);
            WorldLabel(405,_height-77,790,25,trial.Ready?"观测完成 · Esc 收起，阅读望远镜台上的回响":"移动鼠标 / 方向键调镜 · 按住左键对焦 · Esc 收起",18,CreamInk);
        }
    }
}
