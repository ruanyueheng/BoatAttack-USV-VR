using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private static readonly Color SeaInk=new Color32(14,30,46,255),CreamInk=new Color32(247,235,208,255),AmberInk=new Color32(224,185,119,255);
        private Texture2D _titleArt,_chapterArt;
        private void DisplayText(Rect r,string text,int size,Color ink,bool heading=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            _label.font=heading?FishingUITheme.Heading:_font;_label.fontSize=size;_label.fontStyle=FontStyle.Normal;
            _label.normal.textColor=Faded(ink);_label.alignment=align;GUI.Label(r,text,_label);
        }
        private bool SeaButton(Rect r,string text,bool primary=false,bool enabled=true,int size=20)
        {
            bool hover=enabled && r.Contains(Event.current.mousePosition);
            Rounded(r,primary?new Color(.83f,.68f,.43f,.96f):new Color(.03f,.10f,.15f,hover?.92f:.7f),5);
            Outline(r,hover?CreamInk:new Color(.87f,.72f,.46f,.5f),5);
            DisplayText(r,text,size,!enabled?new Color(.52f,.58f,.59f):primary?SeaInk:CreamInk,false,TextAnchor.MiddleCenter);
            return GUI.Button(r,GUIContent.none,GUIStyle.none) && enabled;
        }
        private void DrawStartMenu()
        {
            var c=Controller;
            if(_titleArt==null)_titleArt=Resources.Load<Texture2D>("FishingV031/TitleSea");
            var full=new Rect(-_offset/_scale,0,Screen.width/_scale,_height);
            Fill(full.x,0,full.width,_height,SeaInk);
            if(_titleArt!=null)GUI.DrawTexture(full,_titleArt,ScaleMode.ScaleAndCrop);
            Fill(full.x,0,full.width,_height,new Color(.01f,.025f,.045f,.12f));
            float top=(_height-740)*.5f;
            if(c.MenuControls){DrawMenuControls(new Rect(230,top+26,1140,690));return;}
            DisplayText(new Rect(116,top+84,540,30),"T I D E B O U N D   /   回 潮 海",17,AmberInk);
            DisplayText(new Rect(108,top+130,660,116),"潮汐回响",82,CreamInk,true);
            Fill(120,top+258,62,2,AmberInk);
            DisplayText(new Rect(120,top+284,530,60),"循着灯火，驶向海风寄来的远方。",24,CreamInk,true);
            if(SeaButton(new Rect(120,top+406,318,62),"开始游戏    →",true))c.StartFromMenu();
            if(SeaButton(new Rect(120,top+484,318,54),"控制"))c.MenuControls=true;
            if(SeaButton(new Rect(120,top+554,318,54),"退出游戏"))Application.Quit();
            DisplayText(new Rect(120,_height-70,660,25),"Enter 开始  ·  F11 全屏 / 窗口  ·  进度自动保存",15,CreamInk);
            DisplayText(new Rect(1190,_height-70,290,25),"第 31 版  /  v"+FishingPrototype.Version,14,AmberInk,false,TextAnchor.UpperRight);
            // Only tiny accents move; the painted scene keeps a quiet reading area.
            for(int i=0;i<14;i++)
            {
                float a=.25f+.18f*Mathf.Sin(Time.unscaledTime*.6f+i*2.1f);
                Disc(new Vector2(860+(i*137%660),top+55+(i*79%365)),i%3==0?1.7f:1,new Color(1,.86f,.6f,a));
            }
        }
        private void DrawMenuControls(Rect r)
        {
            var c=Controller;
            Rounded(r,new Color(.025f,.075f,.115f,.96f),12);Outline(r,new Color(.8f,.65f,.4f,.55f),12);
            DisplayText(new Rect(r.x+38,r.y+28,600,56),"控制与航行",38,CreamInk,true);
            DisplayText(new Rect(r.x+40,r.y+88,900,28),"调好视野，慢慢认识这片海。",17,AmberInk);
            Fill(r.x+40,r.y+133,r.width-80,1,new Color(.8f,.7f,.5f,.4f));
            DisplayText(new Rect(r.x+42,r.y+157,440,32),"显示模式",26,CreamInk,true);
            if(SeaButton(new Rect(r.x+42,r.y+210,228,50),"全屏",Screen.fullScreen))c.SetFullscreen(true);
            if(SeaButton(new Rect(r.x+284,r.y+210,228,50),"窗口",!Screen.fullScreen))c.SetFullscreen(false);
            DisplayText(new Rect(r.x+42,r.y+279,474,62),"F11 或 Alt + Enter 随时切换\n窗口模式支持自由调整大小",17,CreamInk);
            if(SeaButton(new Rect(r.x+42,r.y+366,470,48),"舒适航行："+(c.ComfortMode?"开启":"标准动力")))c.ToggleComfort();
            if(SeaButton(new Rect(r.x+42,r.y+430,470,48),"环境与捕鱼音效："+(c.QuietMode?"关闭":"开启")))c.ToggleQuiet();
            string[] keys={"W A S D","鼠标 / Alt","E","F / T","J / I","C / Ctrl","G / Esc"};
            string[] meanings={"步行与航行","转头 / 释放鼠标","交谈、采集（低处自动蹲下）","使用手中工具 / 观察动物","航海手札 / 背包与制作","切换视角 / 手动蹲下","上船下船 / 暂停"};
            for(int i=0;i<keys.Length;i++)
            {
                float y=r.y+158+i*51;
                DisplayText(new Rect(r.x+566,y,146,32),keys[i],19,AmberInk);
                DisplayText(new Rect(r.x+720,y,380,42),meanings[i],18,CreamInk);
                Fill(r.x+566,y+40,530,1,new Color(.7f,.8f,.8f,.12f));
            }
            DisplayText(new Rect(r.x+566,r.y+531,530,48),"选择提灯后拿在手中，F 开关灯火。\n选择自然手册后 F 翻开，Esc 收起。",16,AmberInk);
            if(SeaButton(new Rect(r.x+42,r.yMax-80,470,48),"返回"))c.MenuControls=false;
        }
    }
}
