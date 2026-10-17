using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingHUD
    {
        private void Dim()=>Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(.01f,.025f,.04f,.64f));
        private void DrawPause()
        {
            var c=Controller;Dim();
            if(c.MenuControls){DrawMenuControls(new Rect(380,(_height-690)*.5f,1100,690));return;}
            var r=new Rect(330,(_height-686)*.5f,940,686);Rounded(r,Ink,12);Outline(r,Hairline,12);
            ChapterImage(new Rect(r.x+1,r.y+1,r.width-2,172),7);
            Rounded(new Rect(r.x+25,r.y+42,360,93),new Color(.02f,.07f,.11f,.87f),6);
            DisplayText(new Rect(r.x+49,r.y+59,320,47),"在海风里歇一会",32,CreamInk,true);
            DisplayText(new Rect(r.x+51,r.y+110,300,26),"航程已暂停 · 进度自动保存",15,AmberInk);
            float x=r.x+30,y=r.y+204,w=432;
            if(Button(new Rect(x,y,w,47),"操作、画面与声音设置",true,true))c.MenuControls=true;
            if(Button(new Rect(x+w+16,y,w,47),"音效："+(c.QuietMode?"静音":"开启")))c.ToggleQuiet();
            y+=65;
            if(Button(new Rect(x,y,w,47),"舒适航行："+(c.ComfortMode?"开启":"标准")))c.ToggleComfort();
            if(Button(new Rect(x+w+16,y,w,47),"环境特效："+(c.Weather.ReducedEffects?"精简":"标准")))c.Weather.ToggleReducedEffects();
            y+=65;
            if(Button(new Rect(x,y,w,47),"天气："+BayClimate.Name(c.Weather.Climate.Current)))c.Weather.CyclePreview();
            if(Button(new Rect(x+w+16,y,w,47),"天气循环："+(c.Weather.Climate.Automatic?"自动":"固定")))
            {if(c.Weather.Climate.Automatic)c.Weather.Climate.Hold();else c.Weather.Climate.ResumeCycle();}
            y+=65;
            if(Button(new Rect(x,y,w,47),"世界仪表："+(c.WorldInstruments?"开启":"关闭")))c.ToggleInstruments();
            if(Button(new Rect(x+w+16,y,w,47),"显示模式："+(Screen.fullScreen?"全屏":"窗口")))c.SetFullscreen(!Screen.fullScreen);
            Label(x,r.y+491,870,50,"J 打开航海手札，I 整理行囊。回到海上之前，记得带一盏灯。",19,Muted,true);
            if(Button(new Rect(x,r.y+566,278,49),"继续航行",true,true))c.TogglePause();
            if(Button(new Rect(x+300,r.y+566,278,49),"返回开始菜单",!c.HasPendingCatch))c.OpenStartMenu();
            if(Button(new Rect(x+600,r.y+566,278,49),"退出游戏"))Application.Quit();
            Label(x,r.yMax-29,870,22,"图标：Delapouite / game-icons.net · CC BY 3.0",11,Muted);
        }
    }
}
