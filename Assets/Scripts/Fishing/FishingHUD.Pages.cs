using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void Dim()=>Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.01f,.025f,.04f,.64f));
        private RenderTexture _pauseBackdrop;
        private bool _pauseCapturing;
        /// <summary>One frozen, downsampled frame (1/16 → 1/8 bilinear = soft blur) reused while paused: zero per-frame cost.</summary>
        private bool PauseBackdrop()
        {
            if(_pauseBackdrop==null){if(!_pauseCapturing){_pauseCapturing=true;StartCoroutine(CapturePauseBackdrop());}return false;}
            var previous=GUI.color;GUI.color=Faded(Color.white);
            GUI.DrawTexture(new Rect(-_offset/_scale,0,Screen.width/_scale,_height),_pauseBackdrop,ScaleMode.StretchToFill,false);GUI.color=previous;
            Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.02f,.04f,.08f,.45f));
            Tinted(new Rect(-_offset/_scale-200,-120,Screen.width/_scale*.75f,_height+240),FishingUITheme.TitleGlow,new Color(1,1,1,.9f));
            return true;
        }
        private System.Collections.IEnumerator CapturePauseBackdrop()
        {
            yield return new WaitForEndOfFrame();
            var frame=ScreenCapture.CaptureScreenshotAsTexture();
            int w=Mathf.Max(8,frame.width/16),h=Mathf.Max(8,frame.height/16);
            var small=RenderTexture.GetTemporary(w,h,0);var half=RenderTexture.GetTemporary(w*4,h*4,0);
            Graphics.Blit(frame,half);Graphics.Blit(half,small);
            _pauseBackdrop=new RenderTexture(w*2,h*2,0){name="Pause backdrop",filterMode=FilterMode.Bilinear};
            Graphics.Blit(small,_pauseBackdrop);
            RenderTexture.ReleaseTemporary(small);RenderTexture.ReleaseTemporary(half);Destroy(frame);
            _pauseCapturing=false;
        }
        private void ReleasePauseBackdrop(){if(_pauseBackdrop!=null){_pauseBackdrop.Release();Destroy(_pauseBackdrop);_pauseBackdrop=null;}}
        private bool PauseOption(ref float y,string text,bool enabled)
        {
            var r=new Rect(80,y,380,50);y+=56;
            bool hover=enabled && r.Contains(Event.current.mousePosition);
            if(hover){Fill(r.x+30,r.yMax-6,240,1,AmberInk*new Color(1,1,1,.6f));GiltStar(new Vector2(r.x+10,r.center.y),7,AmberInk);}
            float previous=_fade;if(!enabled)_fade*=.4f;
            DisplayText(new Rect(r.x+31,r.y+11,r.width,36),text,24,new Color(0,0,0,.6f),true,TextAnchor.UpperLeft);
            DisplayText(new Rect(r.x+30,r.y+10,r.width,36),text,24,hover?AmberInk:CreamInk,true,TextAnchor.UpperLeft);
            _fade=previous;
            bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none) && enabled;
            if(clicked)Controller.WorldAudio?.Play("UIConfirm",Controller.ExplorerPosition,.24f);
            return clicked;
        }
        private void DrawPause()
        {
            var c=Controller;
            if(!PauseBackdrop())return;
            if(c.ClassroomMenuOpen){DrawClassroomMenu();return;}
            if(c.MenuControls){DrawMenuControls(new Rect(380,(_height-690)*.5f,1100,690));return;}
            // V044：左侧竖排主选项（当前项金星），右侧羊皮纸放设置开关。
            float top=(_height-560)*.5f;
            DisplayText(new Rect(80,top-6,420,52),Spaced("在海风里歇一会"),34,CreamInk,true);
            GiltRule(80,top+54,300);
            DisplayText(new Rect(82,top+64,420,24),"航程已暂停 · "+(c.LastAutoSave.Length>0?"自动保存 "+c.LastAutoSave:"内存进度保留"),15,AmberInk);
            float oy=top+112;
            if(PauseOption(ref oy,"继续航行",true))c.TogglePause();
            if(PauseOption(ref oy,"操作与画面设置",true)){SettingsTab=0;c.MenuControls=true;}
            if(PauseOption(ref oy,"声音混音",true)){SettingsTab=2;c.MenuControls=true;}
            if(PauseOption(ref oy,"手动保存",c.CanContinue))c.OpenSaveMenu("save");
            if(PauseOption(ref oy,"读取存档",true))c.OpenSaveMenu("load");
            if(PauseOption(ref oy,"快速回家",true))c.ReturnHome();
            if(PauseOption(ref oy,"返回开始菜单",!c.HasPendingCatch))c.OpenStartMenu();
            if(PauseOption(ref oy,"退出游戏",true)){c.MenuControls=true;_exitConfirmation=true;}
            var r=new Rect(640,top,880,560);MenuSheet(r,false);
            ChapterImage(new Rect(r.x+24,r.y+22,r.width-48,150),7);
            float x=r.x+34,y=r.y+196,w=398;
            if(Button(new Rect(x,y,w,47),"舒适航行："+(c.ComfortMode?"开启":"标准")))c.ToggleComfort();
            if(Button(new Rect(x+w+16,y,w,47),"环境特效："+(c.Weather.ReducedEffects?"精简":"标准")))c.Weather.ToggleReducedEffects();
            y+=65;
            if(Button(new Rect(x,y,w,47),"天气："+BayClimate.Name(c.Weather.Climate.Current)))c.Weather.CyclePreview();
            if(Button(new Rect(x+w+16,y,w,47),"天气循环："+(c.Weather.Climate.Automatic?"自动":"固定")))
            {if(c.Weather.Climate.Automatic)c.Weather.Climate.Hold();else c.Weather.Climate.ResumeCycle();}
            y+=65;
            if(Button(new Rect(x,y,w,47),"世界仪表："+(c.WorldInstruments?"开启":"关闭")))c.ToggleInstruments();
            if(Button(new Rect(x+w+16,y,w,47),"显示模式："+(Screen.fullScreen?"全屏":"窗口")))c.SetFullscreen(!Screen.fullScreen);
            y+=65;
            if(Button(new Rect(x,y,w*2+16,47),"日期与物资"))c.OpenClassroomMenu();
            Label(x,r.yMax-36,812,22,"图标：Delapouite / game-icons.net · CC BY 3.0",11,Muted);
        }
    }
}
