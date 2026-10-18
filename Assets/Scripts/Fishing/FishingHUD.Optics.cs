using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private Texture2D _opticMask;
        private GUIStyle _opticLabel;
        private void DrawOpticalView()
        {
            float opacity=Controller.View!=null?Controller.View.OpticalOpacity:0;
            if(opacity<=0 || Event.current.type!=EventType.Repaint)return;
            if(_opticMask==null)
            {
                const int width=320,height=224;
                _opticMask=new Texture2D(width,height,TextureFormat.RGBA32,false){name="双筒目镜遮罩",wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color32[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    var p=new Vector2((x/(width-1f)-.5f)*1.4f,y/(height-1f)-.5f);
                    float distance=Mathf.Min((p-new Vector2(-.18f,0)).magnitude,(p-new Vector2(.18f,0)).magnitude);
                    byte alpha=(byte)Mathf.RoundToInt(255*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,.46f,distance)));
                    pixels[y*width+x]=new Color32(3,10,12,alpha);
                }
                _opticMask.SetPixels32(pixels);_opticMask.Apply(false,true);
            }
            var old=GUI.color;
            try
            {
                // 以屏幕高度定义光圈，宽屏和 5:4 均不会把圆形目镜拉成长椭圆。
                float width=Screen.height*1.4f,left=(Screen.width-width)*.5f;
                GUI.color=new Color(1,1,1,opacity);
                GUI.DrawTexture(new Rect(left,0,width,Screen.height),_opticMask);
                GUI.color=new Color(.012f,.039f,.047f,opacity);
                if(left>0)
                {
                    GUI.DrawTexture(new Rect(0,0,left+1,Screen.height),Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(Screen.width-left-1,0,left+1,Screen.height),Texture2D.whiteTexture);
                }
                GUI.color=new Color(.67f,.89f,.82f,.7f*opacity);
                float x=Screen.width*.5f,y=Screen.height*.5f;
                GUI.DrawTexture(new Rect(x-9,y,6,1),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x+3,y,6,1),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x,y-9,1,6),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x,y+3,1,6),Texture2D.whiteTexture);
                if(_opticLabel==null)_opticLabel=new GUIStyle(GUI.skin.label){font=_font,alignment=TextAnchor.MiddleCenter};
                _opticLabel.fontSize=Mathf.RoundToInt(Mathf.Clamp(Screen.height/48f,13,22));
                var labelRect=new Rect(x-250,Screen.height*.77f,500,30);var paper=FishingUITheme.Paper;
                GUI.color=new Color(paper.r,paper.g,paper.b,.96f*opacity);GUI.DrawTexture(labelRect,Texture2D.whiteTexture);
                GUI.color=new Color(1,1,1,opacity);_opticLabel.normal.textColor=FishingUITheme.Ink;
                GUI.Label(labelRect,"望远镜  "+Controller.View.BinocularMagnification.ToString("0.0")+"× · 滚轮调倍 · T 记录 / F 放下",_opticLabel);
            }
            finally{GUI.color=old;}
        }
        private void ReleaseOptics(){if(_opticMask!=null)Destroy(_opticMask);_opticMask=null;}
    }
}
