using UnityEngine;
namespace BoatAttack.Fishing
{
    /// <summary>Compact desktop overlay; a future XR build needs world-space instruments instead.</summary>
    public sealed class FishingHUD : MonoBehaviour
    {
        public FishingPrototype Controller;
        private Font _font;
        private GUIStyle _label,_button;
        private float _height,_scale,_offset;
        public static float LayoutScale(int width,int height) => Mathf.Max(0.01f,Mathf.Min(width/1600f,height/900f));
        private static readonly Color Ink=new Color(0.025f,0.075f,0.105f,0.88f);
        private static readonly Color Muted=new Color(0.70f,0.83f,0.83f);
        private static readonly Color Mint=new Color(0.3f,0.93f,0.78f);
        private static readonly Color Gold=new Color(1f,0.74f,0.36f);
        private void Awake(){_font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);}
        private void OnGUI()
        {
            var c=Controller;if(c==null || c.Career==null || c.Player==null)return;
            _scale=LayoutScale(Screen.width,Screen.height);_height=Screen.height/_scale;_offset=(Screen.width-1600f*_scale)*0.5f;
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3(_offset,0,0),Quaternion.identity,new Vector3(_scale,_scale,1));
            if(_label==null){_label=new GUIStyle(GUI.skin.label){font=_font,wordWrap=true};_button=new GUIStyle(GUI.skin.button){font=_font,fontSize=20};}
            Panel(24,22,320,91);
            Label(43,29,280,43,"怪鱼航线",30,Color.white,true);
            Label(45,80,280,24,"TIDE & FIN   /   DESKTOP 0.2",13,Muted);
            Panel(1140,22,436,91);
            Label(1160,34,170,39,c.Career.Coins+"  金币",27,Gold,true);
            Label(1375,34,180,39,"鱼箱 "+c.Career.Count+" / 6",24,Color.white,true);
            Label(1160,79,390,24,c.Career.RodLevel==1?"轻量鱼线 Lv.1   ·   张力增长 -28%":"基础鱼线   ·   港口升级需要 30 金币",14,Muted);
            Panel(362,22,760,91);
            string mode=c.FirstPerson?"第一人称":"第三人称";
            Label(380,33,725,28,mode+"  /  "+(c.Anchored?"捕捞站":"驾驶位")+"     C 切换   ·   右键环顾   ·   V 回正",16,Mint,true);
            Label(380,72,725,26,c.Career.RodLevel==1?"✓ 第一次出航目标已完成，去寻找稀有鱼吧":"今日目标：捕获鱼 → 返港出售 → 升级鱼线",16,Color.white);
            if(c.VisibleMessage.Length>0)
            {
                Panel(375,117,850,51);Label(393,127,816,40,c.VisibleMessage,16,Color.white);
            }
            Panel(24,126,265,44);
            Label(36,130,248,33,(c.Anchored?"已停船  ·  ":"航速  ")+(c.Speed*3.6f).ToString("0.0")+" km/h",19,Color.white,true);
            if(c.ShowDetails)
            {
                if(!c.Anchored)DrawMap();
                if(c.Career.Count>0)
                {
                    Panel(1360,146,216,68+26*c.Career.Count);Label(1376,156,186,25,"本航次 · "+c.Career.HoldValue+" 金币",16,Gold,true);
                    int row=0;foreach(var f in c.Career.Inventory){Label(1376,191+row*26,186,25,(f.rare?"★ ":"· ")+c.FishNames[(int)f.kind]+"   "+f.value,15,f.rare?Gold:Muted);row++;}
                }
                if(!c.Anchored)
                {
                    DrawWorldLabel(c.harbor+Vector3.up*3.5f,"渔港  "+c.HarborDistance.ToString("0")+"m",Mint);
                    for(int i=0;i<3;i++)DrawWorldLabel(c.fishingSpots[i]+Vector3.up*1.5f,(i+1)+"  "+c.SpotNames[i],Gold);
                }
            }
            if(c.FirstPerson && !c.Anchored)
            {
                Fill(798,_height*0.5f-1,4,2,new Color(1,1,1,0.75f));Fill(799,_height*0.5f-2,2,4,new Color(1,1,1,0.75f));
            }
            DrawAction();
            Fill(0,_height-39,1600,39,Ink);
            Label(35,_height-33,1530,25,"W 前进  A/D 转向  S/Shift 刹车     E 交互/抄鱼     左键 抛竿/提竿/收线     Q 起航     U 升级     R 救援     H 简洁界面     Esc 暂停",14,Color.white);
            if(c.Paused)
            {
                Fill(-_offset/_scale,0,Screen.width/_scale,_height,new Color(0,0,0,0.62f));Panel(545,_height/2-125,510,250);
                Label(590,_height/2-95,420,50,"慢一点，海还在这里。",28,Color.white,true);
                Label(590,_height/2-35,420,32,"F / Esc 继续；鼠标松开后恢复工具操作",16,Muted);
                if(GUI.Button(new Rect(590,_height/2+20,190,48),"继续航行",_button))c.TogglePause();
                if(GUI.Button(new Rect(815,_height/2+20,190,48),"退出试玩",_button))Application.Quit();
            }
            GUI.matrix=matrix;GUI.color=Color.white;
        }
        private void DrawAction()
        {
            var c=Controller;float x=390,y=_height-181;Panel(x,y,820,132);
            string title,help;
            if(!c.Anchored)
            {
                title=c.AtHarbor?"渔港 · TIDE & FIN":c.SpotDistance<=22f?"进入 "+c.SpotNames[c.Spot]:"驶向 "+c.SpotNames[c.Spot]+"  "+c.SpotDistance.ToString("0")+"m";
                help=c.AtHarbor?"E 出售全部鱼获  ·  U 升级鱼线（30 金币）":c.SpotDistance<=22f?(c.Speed<3?"按 E 停船，进入捕捞站":"按住 S 或 Shift 减速，然后 E 停船"):"跟随海图上的黄色编号。C 可切换第三人称观察路线。";
            }
            else
            {
                switch(c.Session.Phase)
                {
                    case CatchPhase.Waiting:title="留意浮标，等待咬钩…";help="看到咬钩提示 / 听到提示音后，点击左键提竿。";break;
                    case CatchPhase.Bite:title="咬钩！点击左键提竿";help="反应窗口  "+c.Session.Remaining.ToString("0.0")+" 秒";break;
                    case CatchPhase.Fighting:title="正在收捕 · "+c.FishNames[c.Spot];help="按住左键收线，张力升高时松开  /  剩余 "+c.Session.Distance.ToString("0.0")+" m";break;
                    case CatchPhase.Landing:title="准备抄鱼！指针进入绿色区时按 E";help="剩余 "+c.Session.Remaining.ToString("0.0")+" 秒  ·  看准时机，不用连续敲击";break;
                    case CatchPhase.Caught:title="已安全入箱 · "+c.FishNames[c.Spot];help="左键再抛一竿  /  Q 起航返港出售";break;
                    case CatchPhase.Escaped:title="这次没抓住，左键再试一次";help=c.Session.Failure;break;
                    default:title=c.Career.Count>=6?"鱼箱已满，满载而归！":"准备捕捞 · "+c.FishNames[c.Spot];help=c.Career.Count>=6?"Q 起航 → 青色港口 → E 出售":"左键抛竿  ·  右键环顾工具  ·  Q 继续航行";break;
                }
            }
            Label(x+20,y+10,780,40,title,24,c.Session.Phase==CatchPhase.Bite?Gold:Color.white,true);
            Label(x+20,y+52,780,32,help,17,Muted);
            if(c.Anchored && c.Session.Phase==CatchPhase.Fighting)
            {
                float t=c.Session.Tension;Label(x+20,y+92,90,23,"张力",14,Muted);
                Fill(x+85,y+96,690,12,new Color(0.15f,0.25f,0.29f));Fill(x+85,y+96,690*t,12,t>0.78f?new Color(1,0.33f,0.26f):Gold);Fill(x+85+690*0.78f,y+91,2,22,Color.white);
            }
            else if(c.Anchored && c.Session.Phase==CatchPhase.Landing)
            {
                Fill(x+20,y+96,770,12,new Color(0.15f,0.25f,0.29f));Fill(x+20+770*0.25f,y+96,770*0.5f,12,Mint);Fill(x+20+770*c.Session.LandPointer-3,y+89,6,27,Color.white);
            }
            else Label(x+20,y+96,780,24,"进度自动保存   /   第一人称桌面预览，非 VR 真机模式",13,Mint);
        }
        private void DrawMap()
        {
            var c=Controller;float y=_height-260;Panel(24,y,266,211);Label(41,y+10,230,25,"海图 / 港口与渔区",16,Color.white,true);
            Vector3 min=c.harbor,max=c.harbor;foreach(var p in c.fishingSpots){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}min-=Vector3.one*25;max+=Vector3.one*25;
            MapDot(c.harbor,min,max,y,"港",Mint);for(int i=0;i<3;i++)MapDot(c.fishingSpots[i],min,max,y,(i+1).ToString(),Gold);MapDot(c.Player.transform.position,min,max,y,"▲",Color.white);
        }
        private void MapDot(Vector3 p,Vector3 min,Vector3 max,float y,string text,Color color)
        {
            float x=45+Mathf.Clamp01((p.x-min.x)/(max.x-min.x))*208;float yy=y+177-Mathf.Clamp01((p.z-min.z)/(max.z-min.z))*111;Label(x-10,yy-12,40,34,text,21,color,true);
        }
        private void DrawWorldLabel(Vector3 world,string text,Color color)
        {
            var s=Controller.viewCamera.WorldToScreenPoint(world);if(s.z<=0)return;float x=(s.x-_offset)/_scale,y=(Screen.height-s.y)/_scale;
            if(x<305 || x>1330 || y<185 || y>_height-200)return;
            Fill(x-95,y,190,28,new Color(0.025f,0.09f,0.12f,0.6f));Label(x-88,y+2,180,26,text,15,color,true);
        }
        private void Panel(float x,float y,float w,float h){Fill(x,y,w,h,Ink);Fill(x,y,3,h,new Color(0.2f,0.8f,0.65f,0.7f));}
        private void Fill(float x,float y,float w,float h,Color c){GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;}
        private void Label(float x,float y,float w,float h,string text,int size,Color color,bool bold=false){_label.fontSize=size;_label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;_label.normal.textColor=color;GUI.Label(new Rect(x,y,w,h),text,_label);}
        private void OnDestroy(){if(_font!=null)Destroy(_font);}
    }
}
