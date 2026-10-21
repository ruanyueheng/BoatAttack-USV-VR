using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void PaperImage(Rect rect,Sprite sprite,Color tint,bool flipX=false,bool flipY=false)
        {
            var previous=GUI.color;GUI.color=Faded(tint);
            GUI.DrawTextureWithTexCoords(rect,sprite.texture,new Rect(flipX?1:0,flipY?1:0,flipX?-1:1,flipY?-1:1));
            GUI.color=previous;
        }
        private void ThemedPaper(Rect r,FishingPaperTheme theme)
        {
            Rounded(new Rect(r.x-3,r.y+7,r.width+6,r.height),new Color(.015f,.035f,.045f,.3f),4);
            var sprite=FishingPaperDesign.Surface(theme);var texture=sprite.texture;
            float edge=Mathf.Min(110,Mathf.Min(r.width,r.height)*.22f);
            float uEdge=sprite.border.x/texture.width,vEdge=sprite.border.w/texture.height;
            var previous=GUI.color;GUI.color=Faded(FishingPaperDesign.PaperTint);
            // Stretch the quiet centre, keeping painted moons, compasses and pressed leaves in proportion.
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {
                float x=col==0?0:col==1?edge:r.width-edge,y=row==0?0:row==1?edge:r.height-edge;
                float w=col==1?r.width-2*edge:edge,h=row==1?r.height-2*edge:edge;
                float u=col==0?0:col==1?uEdge:1-uEdge,v=row==0?1-vEdge:row==1?vEdge:0;
                GUI.DrawTextureWithTexCoords(new Rect(r.x+x,r.y+y,w,h),texture,new Rect(u,v,col==1?1-2*uEdge:uEdge,row==1?1-2*vEdge:vEdge));
            }
            GUI.color=previous;
        }
        private void QuietRule(float x,float y,float width)
            =>Fill(x,y,width,1,FishingUITheme.Blend(new Color(.30f,.40f,.37f,.34f),new Color(.60f,.71f,.68f,.36f)));
        private void StarlightTrail(Vector2 start,Vector2 end,Color color)
        {
            var previous=start;
            for(int i=0;i<=8;i++)
            {
                float t=i/8f;var point=Vector2.Lerp(start,end,t)+Vector2.up*Mathf.Sin(t*Mathf.PI)*18;
                if(i>0)Segment(previous,point,color*.55f,.8f);
                GiltStar(point,i%3==0?5:2.5f,color);previous=point;
            }
        }
        private void DreamSettingsPaper(Rect r)
        {
            var previous=GUI.color;GUI.color=Faded(FishingPaperDesign.SettingsTint);
            var texture=FishingPaperDesign.Surface(FishingPaperTheme.DreamSettings).texture;
            float scale=Mathf.Min(r.width/texture.width,r.height/texture.height);
            float edgeX=texture.width*.20f*scale,edgeY=texture.height*.33f*scale;
            // Preserve the painted planet and roses on tall and wide screens; expand the quiet centre.
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {
                float x=col==0?0:col==1?edgeX:r.width-edgeX,y=row==0?0:row==1?edgeY:r.height-edgeY;
                float w=col==1?r.width-2*edgeX:edgeX,h=row==1?r.height-2*edgeY:edgeY;
                float u=col==0?0:col==1?.20f:.80f,v=row==0?.67f:row==1?.33f:0;
                GUI.DrawTextureWithTexCoords(new Rect(r.x+x,r.y+y,w,h),texture,new Rect(u,v,col==1?.60f:.20f,row==1?.34f:.33f));
            }
            GUI.color=previous;
        }
        private void StoryPaper(Rect r,bool bookmark=false,float corner=60)
        {
            Rounded(new Rect(r.x-3,r.y+7,r.width+6,r.height),new Color(.015f,.035f,.045f,.3f),4);
            PaperImage(r,FishingPaperDesign.Vellum,FishingPaperDesign.PaperTint);
            var gold=FishingUITheme.Blend(new Color(.61f,.49f,.26f,.60f),new Color(.83f,.74f,.48f,.64f));
            for(int rule=0;rule<2;rule++)
            {
                float inset=13+rule*5;
                Fill(r.x+inset+corner*.62f,r.y+inset,r.width-2*inset-corner*1.24f,1,gold);
                Fill(r.x+inset+corner*.62f,r.yMax-inset,r.width-2*inset-corner*1.24f,1,gold);
                float vertical=r.height-2*inset-corner*1.24f;
                if(vertical>0)
                {
                    Fill(r.x+inset,r.y+inset+corner*.62f,1,vertical,gold);
                    Fill(r.xMax-inset,r.y+inset+corner*.62f,1,vertical,gold);
                }
            }
            for(int i=0;i<4;i++)
            {
                bool right=i%2==1,bottom=i>1;
                PaperImage(new Rect(right?r.xMax-corner:r.x,bottom?r.yMax-corner:r.y,corner,corner),FishingPaperDesign.Corner,Color.white,right,bottom);
            }
            if(bookmark)PaperImage(new Rect(r.xMax-103,r.y-3,45,90),FishingPaperDesign.Bookmark,Color.white);
        }
        private void PaperRule(float x,float y,float width)
        {
            var gold=FishingUITheme.Blend(new Color(.63f,.51f,.29f,.5f),new Color(.84f,.74f,.50f,.55f));
            Fill(x,y,width*.5f-36,1,gold);Fill(x+width*.5f+36,y,width*.5f-36,1,gold);
            PaperImage(new Rect(x+width*.5f-24,y-19,48,38),FishingPaperDesign.Crest,Color.white);
        }
    }
}
