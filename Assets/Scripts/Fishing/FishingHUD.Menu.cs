using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private static readonly Color SeaInk=new Color32(14,30,46,255),CreamInk=new Color32(247,235,208,255),AmberInk=new Color32(224,185,119,255);
        private Texture2D _chapterArt,_titleVeil;
        public int SettingsTab { get; set; }
        private Color TitleFill=>FishingUITheme.TitleFill(Controller.Realms!=null && Controller.Realms.Inside?Controller.Realms.Island+1:0);
        private void GildedTitle(Rect r,string text,int size,Color fill,TextAnchor align=TextAnchor.UpperLeft)
        {
            float stroke=Mathf.Clamp(size*.045f,.8f,2.5f);
            DisplayText(new Rect(r.x+1,r.y+2,r.width,r.height),text,size,new Color(.025f,.035f,.04f,.65f),true,align);
            // 描边只沿字形扩展；最后重画有色字芯，金边不会覆盖笔画内部。
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;var offset=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*stroke;
                var gold=offset.y<-.1f?new Color32(252,223,142,255):offset.y>.1f?new Color32(174,119,35,255):new Color32(221,171,67,255);
                DisplayText(new Rect(r.x+offset.x,r.y+offset.y,r.width,r.height),text,size,gold,true,align);
            }
            DisplayText(r,text,size,fill,true,align);
        }
        private void WorldLabel(Rect r,string text,int size,Color ink,bool heading=false,TextAnchor align=TextAnchor.UpperLeft,bool onPaper=false)
        {
            if(heading && size>=18){GildedTitle(r,text,size,ink==Danger?Danger:TitleFill,align);return;}
            if(onPaper)
            {
                ink=ink==Danger?Danger:ink==Mint?FishingUITheme.Blend(new Color(.16f,.34f,.24f),new Color(.65f,.86f,.72f))
                    :ink==Gold || ink==AmberInk?FishingUITheme.Blend(new Color(.39f,.24f,.08f),AmberInk):FishingUITheme.Ink;
            }
            else
            {
                ink=ink==Gold?AmberInk:ink==Mint?new Color(.70f,.94f,.83f):ink==Color.white || ink==Muted || ink==Faint?CreamInk:ink;
                DisplayText(new Rect(r.x+1,r.y+1,r.width,r.height),text,size,new Color(.015f,.035f,.045f,.95f),false,align);
            }
            DisplayText(r,text,size,ink,false,align);
        }
        private void WorldLabel(float x,float y,float width,float height,string text,int size,Color ink,bool heading=false,bool onPaper=false)
            =>WorldLabel(new Rect(x,y,width,height),text,size,ink,heading,TextAnchor.UpperLeft,onPaper);
        private void DisplayText(Rect r,string text,int size,Color ink,bool heading=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            _label.font=heading?FishingUITheme.Heading:_font;_label.fontSize=size;_label.fontStyle=FontStyle.Normal;
            _label.normal.textColor=Faded(ink);_label.alignment=align;GUI.Label(r,text,_label);
        }
        private bool SeaButton(Rect r,string text,bool primary=false,bool enabled=true,int size=20)
        {
            bool hover=enabled && r.Contains(Event.current.mousePosition);
            if(primary || hover)Fill(r.x,r.yMax-3,r.width,1,AmberInk);
            WorldLabel(new Rect(r.x+4,r.y,r.width-8,r.height),text,size,!enabled?new Color(.52f,.58f,.59f):primary || hover?AmberInk:CreamInk,enabled && size>=24,TextAnchor.MiddleLeft,onPaper:Controller.StartMenuOpen || Controller.Paused);
            return GUI.Button(r,GUIContent.none,GUIStyle.none) && enabled;
        }
        private void DrawStartMenu()
        {
            var c=Controller;var full=new Rect(-_offset/_scale,0,Screen.width/_scale,_height);
            if(_titleVeil==null)
            {
                _titleVeil=new Texture2D(128,1,TextureFormat.RGBA32,false){name="Smooth title reading veil",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color[128];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(1,1,1,.94f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,1,i/127f))));
                _titleVeil.SetPixels(pixels);_titleVeil.Apply(false,true);
            }
            var veilColor=GUI.color;GUI.color=Faded(FishingUITheme.Paper);GUI.DrawTexture(new Rect(full.x,0,680,_height),_titleVeil,ScaleMode.StretchToFill,true);GUI.color=veilColor;
            float top=(_height-740)*.5f;
            if(c.TitleVoyage.Sailing)
            {
                Fill(full.x,0,full.width,_height,new Color(.015f,.03f,.045f,c.TitleVoyage.Fade));
                WorldLabel(new Rect(100,_height-120,900,40),"风已起，向着下一座灯塔。",24,CreamInk,true,onPaper:true);return;
            }
            if(c.SaveMenuOpen){DrawSaveSlots();return;}
            if(c.MenuControls){DrawMenuControls(new Rect(380,top+30,1100,690));return;}
            WorldLabel(new Rect(116,top+84,540,30),"T I D E B O U N D   /   回 潮 海",17,AmberInk,onPaper:true);
            WorldLabel(new Rect(108,top+130,660,116),"潮汐回响",82,CreamInk,true,onPaper:true);
            Fill(120,top+258,62,2,AmberInk);
            WorldLabel(new Rect(120,top+284,530,60),"循着灯火，驶向海风寄来的远方。",24,CreamInk,true,onPaper:true);
            if(SeaButton(new Rect(120,top+376,310,52),"继续游戏    →",true,c.CanContinue,28))c.ContinueFromMenu();
            WorldLabel(new Rect(120,top+428,500,18),c.CanContinue?"恢复本次航程，不读档、不重新开局":"暂无本次航程 · 请新建或读取存档",13,CreamInk,onPaper:true);
            if(SeaButton(new Rect(120,top+446,278,48),"新建游戏",false,true,26))c.StartFromMenu();
            if(SeaButton(new Rect(120,top+508,278,48),"读取存档",false,true,26))c.OpenSaveMenu("load");
            if(SeaButton(new Rect(452,top+508,278,48),"手动保存",false,c.CanContinue,23))c.OpenSaveMenu("save");
            if(SeaButton(new Rect(120,top+570,278,48),"设置",false,true,26))c.MenuControls=true;
            if(SeaButton(new Rect(120,top+632,278,48),"退出游戏",false,true,26))Application.Quit();
            if(c.SaveMenuMessage.Length>0 || c.SaveWarning.Length>0)WorldLabel(new Rect(452,top+570,660,80),c.SaveWarning.Length>0?c.SaveWarning:c.SaveMenuMessage,17,AmberInk,onPaper:true);
            KeyCap(new Rect(120,_height-56,62,30),"Enter");WorldLabel(new Rect(196,_height-54,800,30),"继续 / 新建  ·  F11 显示模式  ·  自动与手动存档独立",15,CreamInk,onPaper:true);
            WorldLabel(new Rect(1190,_height-56,290,25),"第 35 版  /  v"+FishingPrototype.Version,14,AmberInk,false,TextAnchor.UpperRight,onPaper:true);
        }
        private void KeyCap(Rect r,string key)
        {
            Rounded(r,FishingUITheme.Selected,6);Outline(r,new Color(.96f,.91f,.80f,.85f),6,1.5f);
            if(key=="鼠标")
            {
                var m=new Rect(r.center.x-9,r.y+5,18,r.height-10);Outline(m,InkText,8,1.5f);
                Segment(new Vector2(m.center.x,m.y),new Vector2(m.center.x,m.center.y),InkText,1.5f);
                Segment(new Vector2(m.x,m.center.y),new Vector2(m.xMax,m.center.y),InkText,1.5f);
            }
            else WorldLabel(r,key,key.Length>4?16:20,CreamInk,false,TextAnchor.MiddleCenter,onPaper:true);
        }
        private void SettingSlider(float x,float y,string text,string key,float value,float min=0,float max=1)
        {
            WorldLabel(new Rect(x,y,220,35),text,23,CreamInk,onPaper:true);
            var track=new Rect(x+248,y+13,440,24);
            Fill(track.x,track.y+3,track.width,1,new Color(.9f,.83f,.68f,.45f));
            float next=InteractiveSettingValue(track,key,value,min,max);
            float amount=Mathf.InverseLerp(min,max,next);Fill(track.x,track.y+3,track.width*amount,2,AmberInk);
            Disc(new Vector2(track.x+track.width*amount,track.y+4),7,AmberInk);
            string number=key=="FOV"?next.ToString("0")+"°":key=="Sensitivity"?next.ToString("0.00")+"×":(next*100).ToString("0")+"%";
            WorldLabel(new Rect(x+720,y,110,34),number,18,AmberInk,onPaper:true);
            if(Mathf.Abs(next-value)>.0001f)Controller.SetSetting(key,next);
            if(Event.current.rawType==EventType.MouseUp)Controller.SaveSettings();
        }
        private void DrawMenuControls(Rect r)
        {
            var c=Controller;
            Fill(-_offset/_scale,0,Screen.width/_scale,_height,FishingUITheme.Paper*new Color(1,1,1,.96f));
            WorldLabel(new Rect(116,r.y+26,250,60),"潮汐回响",40,CreamInk,true,onPaper:true);
            WorldLabel(new Rect(120,r.y+140,220,40),"设 置",30,AmberInk,true,onPaper:true);
            Fill(r.x,r.y+36,r.width,1,new Color(.83f,.68f,.43f,.6f));
            string[] tabs={"操作","画面","声音"};
            for(int i=0;i<3;i++)if(SeaButton(new Rect(r.x+i*170,r.y+58,130,50),tabs[i],SettingsTab==i,true,26))SettingsTab=i;
            float x=r.x,y=r.y+152;
            if(SettingsTab==0)
            {
                string[] keys={"WASD","鼠标","E","F","G","Space","Shift","Ctrl","J / I","M / N","C","Esc"};
                string[] meanings={"移动 / 驾船（方向键也可以）","转动视角 · 船上按住右键","互动 / 交谈 / 采集","使用工具 / 开关提灯","上船 / 下船","跳跃 / 上低岸","步行快跑 / 航行刹车","蹲下 / 配合 W 航行加速","航海手札 / 背包","地图 / 切换航点","第一 / 第三人称","暂停 / 返回"};
                for(int i=0;i<keys.Length;i++)
                {
                    float cx=x+(i%2)*550,cy=y+(i/2)*56;
                    if(i==0)for(int k=0;k<4;k++)KeyCap(new Rect(cx+k*36,cy,31,32),keys[i][k].ToString());
                    else KeyCap(new Rect(cx,cy,keys[i].Length>4?102:76,32),keys[i]);
                    WorldLabel(new Rect(cx+158,cy+2,385,43),meanings[i],19,CreamInk,onPaper:true);
                }
                SettingSlider(x,y+360,"鼠标灵敏度","Sensitivity",c.MouseSensitivity,.25f,2.5f);
                if(SeaButton(new Rect(x,y+423,400,40),"垂直视角反转："+(c.InvertLook?"开启":"关闭")))c.SetSetting("Invert",c.InvertLook?0:1);
            }
            else if(SettingsTab==1)
            {
                WorldLabel(new Rect(x,y,180,40),"显示模式",24,CreamInk,onPaper:true);
                if(SeaButton(new Rect(x+248,y,180,40),"全屏",Screen.fullScreen))c.SetFullscreen(true);
                if(SeaButton(new Rect(x+468,y,180,40),"窗口",!Screen.fullScreen))c.SetFullscreen(false);
                WorldLabel(new Rect(x,y+80,180,40),"画质",24,CreamInk,onPaper:true);
                string[] qualities={"流畅","均衡","精致"};for(int i=0;i<3;i++)if(SeaButton(new Rect(x+248+i*180,y+80,140,40),qualities[i],c.GraphicsQuality==i))c.SetSetting("Quality",i);
                SettingSlider(x,y+169,"第一人称视野","FOV",c.FieldOfView,55,90);
                if(SeaButton(new Rect(x,y+259,660,42),"舒适航行："+(c.ComfortMode?"开启":"标准动力")))c.ToggleComfort();
                if(SeaButton(new Rect(x,y+335,660,42),"粒子特效："+(c.Weather.ReducedEffects?"精简":"完整")))c.Weather.ToggleReducedEffects();
                WorldLabel(new Rect(x,y+413,1000,70),"舒适航行会降低转向与加速度；视野设置只影响桌面第一人称。\n降低粒子数量可保留水花反馈，同时减轻画面负担。",17,AmberInk,onPaper:true);
            }
            else
            {
                SettingSlider(x,y,"主音量","Master",c.MasterVolume);
                SettingSlider(x,y+87,"音乐","Music",c.MusicVolume);
                SettingSlider(x,y+174,"海风与环境","Ambient",c.AmbientVolume);
                SettingSlider(x,y+261,"互动与捕鱼音效","Effects",c.EffectsVolume);
                if(SeaButton(new Rect(x,y+365,580,44),"静音："+(c.QuietMode?"开启":"关闭")))c.ToggleQuiet();
                WorldLabel(new Rect(x,y+439,1000,44),"设置自动保存，下次启航继续使用。",17,AmberInk,onPaper:true);
            }
            KeyCap(new Rect(120,r.yMax-44,62,34),"Esc");
            if(SeaButton(new Rect(200,r.yMax-48,180,42),"返回",false,true,23)){c.MenuControls=false;c.SaveSettings();}
        }
    }
}
