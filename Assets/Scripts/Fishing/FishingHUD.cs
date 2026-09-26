using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed class FishingHUD : MonoBehaviour
    {
        public FishingPrototype Controller;
        private Font _font;
        private GUIStyle _label;
        private float _height,_scale;
        private static readonly Color Ink=new Color(0.035f,0.085f,0.13f,0.92f);
        private static readonly Color Muted=new Color(0.65f,0.79f,0.80f);
        private static readonly Color Mint=new Color(0.22f,0.92f,0.78f);
        private static readonly Color Gold=new Color(1f,0.70f,0.26f);
        private void Awake(){_font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);}
        private void OnGUI()
        {
            var c=Controller;if(c==null || c.Career==null || c.Player==null)return;
            _scale=Screen.width/1600f;_height=Screen.height/_scale;
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(_scale,_scale,1));
            if(_label==null)_label=new GUIStyle(GUI.skin.label){font=_font,wordWrap=true};
            Panel(24,22,1552,108);
            Label(48,30,400,54,"怪鱼航线",36,Color.white,true);
            Label(50,87,650,26,"FISHING FIELD TEST   /   原创简模 · 桌面灰盒 v0.1",15,Muted);
            Label(700,43,410,33,c.Career.RodLevel==1?"✓ 首次出航目标已完成":"本次目标：出售鱼获 30 金币，升级鱼线",19,Mint,true);
            Label(1120,39,230,42,c.Career.Coins+"  金币",28,Gold,true);
            Label(1360,39,200,42,"鱼箱 "+c.Career.Count+" / 6",26,Color.white,true);
            Label(1122,87,380,28,c.Career.RodLevel==1?"轻量鱼线  Lv.1  ·  张力增长 -28%":"基础鱼线  Lv.0  ·  港口按 U 升级",15,Muted);

            Panel(24,154,272,116);
            Label(44,167,235,25,c.Anchored?"● 已停船 / 捕捞站":"● 航行中",18,c.Anchored?Mint:Gold,true);
            Label(44,200,235,32,(c.Speed*3.6f).ToString("0.0")+" km/h",25,Color.white,true);
            Label(44,237,245,24,"W 前进 · A/D 转向 · S 刹车",15,Muted);
            if(c.VisibleMessage.Length>0)
            {
                Panel(365,154,870,65);
                Label(386,167,827,46,c.VisibleMessage,18,Color.white);
            }
            DrawWorldLabel(c.harbor+Vector3.up*5f,"渔港  ·  "+c.HarborDistance.ToString("0")+"m",Mint);
            for(int i=0;i<3;i++)DrawWorldLabel(c.fishingSpots[i]+Vector3.up*3f,(i+1)+"  "+c.SpotNames[i],i==c.Spot?Gold:Muted);

            Panel(1330,295,246,250);
            Label(1350,310,205,30,"本航次鱼获",20,Color.white,true);
            int row=0;foreach(var f in c.Career.Inventory)
            {
                Label(1350,352+row*27,205,26,(f.rare?"★ ":"· ")+c.FishNames[(int)f.kind]+"  "+f.value,16,f.rare?Gold:Muted);row++;
            }
            if(row==0)Label(1350,355,205,68,"还没有鱼\n驶向黄色渔区试试",17,Muted);
            Label(1350,515,205,26,"待售价值  "+c.Career.HoldValue,17,Gold,true);
            DrawMap(); DrawAction();
            Label(330,_height-34,1020,26,"E 交互 / 抄鱼    左键 抛竿 / 提竿 / 按住收线    Q 起航    R 救援回港    F / Esc 暂停",15,Color.white);
            if(c.Paused)
            {
                Fill(0,0,1600,_height,new Color(0,0,0,0.55f));Panel(560,_height/2-85,480,170);
                Label(620,_height/2-59,360,52,"航行已暂停",32,Color.white,true);
                Label(620,_height/2+6,360,40,"按 F 或 Esc 继续",20,Mint);
            }
            GUI.matrix=matrix;GUI.color=Color.white;
        }
        private void DrawAction()
        {
            var c=Controller;float x=370,y=_height-234;Panel(x,y,870,187);
            string title,help;
            if(!c.Anchored)
            {
                title=c.AtHarbor?"渔港补给站":c.SpotDistance<=22f?"已进入 "+c.SpotNames[c.Spot]:"下一站："+c.SpotNames[c.Spot]+"  "+c.SpotDistance.ToString("0")+"m";
                help=c.AtHarbor?"E 出售全部鱼获  ·  U 升级鱼线（30 金币）":c.SpotDistance<=22f?(c.Speed<3?"按 E 停船，进入捕捞站":"按住 Shift 减速，速度低于 10.8 km/h 后按 E 停船"):"驶向黄色圆环。先慢下来，再开始捕鱼。";
            }
            else
            {
                switch(c.Session.Phase)
                {
                    case CatchPhase.Waiting:title="等待咬钩…";help="留意浮标和提示音。咬钩时点击左键提竿。";break;
                    case CatchPhase.Bite:title="咬钩了！点击左键";help="提竿窗口还剩 "+c.Session.Remaining.ToString("0.0")+" 秒";break;
                    case CatchPhase.Fighting:title="收线与放线交替";help="按住左键收线；张力升高时松开。剩余 "+c.Session.Distance.ToString("0.0")+" m";break;
                    case CatchPhase.Landing:title="鱼到船边了！准备抄网";help="指针进入绿色区域时按 E。还有 "+c.Session.Remaining.ToString("0.0")+" 秒";break;
                    case CatchPhase.Caught:title="鱼已安全入箱！";help="点击左键再抛一竿；或 Q 起航，返回青色渔港出售。";break;
                    case CatchPhase.Escaped:title="这次没抓住，再试一次";help=c.Session.Failure+" 点击左键重试。";break;
                    default:title=c.Career.Count>=6?"鱼箱满了，满载而归！":"准备捕捞："+c.FishNames[c.Spot];help=c.Career.Count>=6?"Q 起航 → 青色港口 → E 出售":"点击左键抛竿。Q 收起工具并继续航行。";break;
                }
            }
            Label(x+25,y+15,815,48,title,29,c.Session.Phase==CatchPhase.Bite?Gold:Color.white,true);
            Label(x+25,y+70,815,42,help,19,Muted);
            if(c.Anchored && c.Session.Phase==CatchPhase.Fighting)
            {
                float t=c.Session.Tension;Label(x+25,y+125,120,28,"鱼线张力",16,Muted);
                Fill(x+145,y+128,675,19,new Color(0.15f,0.25f,0.29f));Fill(x+145,y+128,675*t,19,t>0.78f?new Color(1,0.27f,0.24f):Gold);
                Fill(x+145+675*0.78f,y+124,2,27,Color.white);
            }
            else if(c.Anchored && c.Session.Phase==CatchPhase.Landing)
            {
                Fill(x+25,y+132,790,18,new Color(0.15f,0.25f,0.29f));Fill(x+25+790*0.25f,y+132,790*0.5f,18,Mint);
                Fill(x+25+790*c.Session.LandPointer-3,y+122,6,38,Color.white);
            }
            else Label(x+25,y+129,805,28,"本版：3 个渔区 / 3 种鱼 / 1 次升级   ·   进度自动保存",16,Mint);
        }
        private void DrawMap()
        {
            var c=Controller;float y=_height-265;Panel(24,y,272,218);Label(42,y+12,240,28,"海图 / 黄色为渔区",17,Color.white,true);
            Vector3 min=c.harbor,max=c.harbor;foreach(var p in c.fishingSpots){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            min-=Vector3.one*25;max+=Vector3.one*25;
            MapDot(c.harbor,min,max,y,"港",Mint);
            for(int i=0;i<3;i++)MapDot(c.fishingSpots[i],min,max,y,(i+1).ToString(),Gold);
            MapDot(c.Player.transform.position,min,max,y,"▲",Color.white);
        }
        private void MapDot(Vector3 p,Vector3 min,Vector3 max,float y,string text,Color color)
        {
            float x=45+Mathf.Clamp01((p.x-min.x)/(max.x-min.x))*216;
            float yy=y+175-Mathf.Clamp01((p.z-min.z)/(max.z-min.z))*110;
            Label(x-10,yy-12,40,34,text,21,color,true);
        }
        private void DrawWorldLabel(Vector3 world,string text,Color color)
        {
            var screen=Controller.viewCamera.WorldToScreenPoint(world);if(screen.z<=0)return;
            float x=screen.x/_scale,y=(Screen.height-screen.y)/_scale;
            if(x<315 || x>1290 || y<235 || y>_height-250)return;
            Fill(x-105,y,210,31,new Color(0.025f,0.09f,0.12f,0.7f));Label(x-98,y+2,204,30,text,16,color,true);
        }
        private void Panel(float x,float y,float w,float h){Fill(x,y,w,h,Ink);Fill(x,y,4,h,Mint*new Color(1,1,1,0.7f));}
        private void Fill(float x,float y,float w,float h,Color color){GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;}
        private void Label(float x,float y,float w,float h,string text,int size,Color color,bool bold=false)
        {
            _label.fontSize=size;_label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;_label.normal.textColor=color;GUI.Label(new Rect(x,y,w,h),text,_label);
        }
        private void OnDestroy(){if(_font!=null)Destroy(_font);}
    }
}
