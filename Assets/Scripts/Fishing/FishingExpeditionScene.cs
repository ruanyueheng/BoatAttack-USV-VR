using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>Single resolver shared by HUD and E. Input is owned by the prototype, not a second Update listener.</summary>
    public sealed class FishingExpeditionScene : MonoBehaviour
    {
        private FishingPrototype _c;
        private readonly Vector3[] _points=new Vector3[5];
        private readonly bool[] _available=new bool[5];
        private static readonly int[] Regions={0,3,3,3,2};
        private static readonly string[] Labels={"阅读维修记录","检查鱼纹箱","检查双环维修箱","检查条纹箱","安装组件并校准信标"};
        public static readonly Vector3 ChannelMarker=new Vector3(108,0,-54);
        public FishingBeaconDial Dial { get; private set; }
        public bool Aligning => Dial!=null && Dial.Active;
        public Vector3 BeaconPosition => _points[4];
        public bool PrecisionGuide { get; private set; }
        public FishingExpedition Facts => _c.Career.Expedition;
        private GameObject _component;
        public string Clue => !Facts.Has(FishingExpedition.Accepted)?"港口交易台：接受重燃旧港信标委托。"
            : !Facts.Has(FishingExpedition.RecordRead)?"A 西湾：橙色水样台旁有维修记录。"
            : !Facts.Has(FishingExpedition.ComponentFound)?"D 南岸：寻找青色双环标记箱；鱼纹与条纹箱不是维修物资。"
            : !Facts.Has(FishingExpedition.BeaconRepaired)?"C 旧港：沿屋外栈道绕过墙角到塔身，将灯头转向海上双环航标。"
            : !Facts.Has(FishingExpedition.TargetCaught)?"渔区 4：北岛泊位以北，捕获一条珊瑚鱼入箱。"
            : !Facts.Has(FishingExpedition.Delivered)?"返港打开交易台，独立提交捕获记录；不扣除鱼。":"旧港信标永久点亮；航道守望者纪念已记入日志。";
        public string PortLabel => !Facts.Has(FishingExpedition.Accepted)?"接受委托：重燃旧港信标":Facts.Has(FishingExpedition.Delivered)?"信标委托已完成":Facts.Has(FishingExpedition.TargetCaught)?"提交捕获记录":"信标任务进行中 · J 查看线索";
        public void Build(FishingPrototype c)
        {
            _c=c;
            _points[0]=c.Shore.Sites[0].Tasks[0];
            _points[1]=c.Shore.Sites[3].Landing;
            _points[2]=c.Shore.Sites[3].Tasks[0];
            _points[3]=c.Shore.Sites[3].Tasks[1];
            _points[4]=c.Shore.Sites[2].Tasks[1];
            var direction=ChannelMarker-_points[4];
            Dial=new FishingBeaconDial(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg);
            for(int i=0;i<_points.Length;i++)_available[i]=c.Shore.Sites[Regions[i]].Available;
            if(_available[0])
            {
                var board=FishingArt.Shape("West bay maintenance record",PrimitiveType.Cube,transform,_points[0]+new Vector3(.75f,1.05f,0),new Vector3(.75f,.65f,.08f),c.cream);
                for(int row=0;row<3;row++)FishingArt.Shape("Record text stripe",PrimitiveType.Cube,board.transform,new Vector3(0,.25f-row*.22f,-.6f),new Vector3(.7f,.035f,.06f),c.dark);
            }
            for(int i=1;i<=3;i++)if(_available[i])BuildCrate(i);
            FishingArt.Shape("Double ring channel post",PrimitiveType.Cylinder,transform,ChannelMarker+Vector3.up*2,new Vector3(.18f,2,.18f),c.teal);
            var markerRight=Vector3.Cross(Vector3.up,(_points[4]-ChannelMarker).normalized).normalized;
            for(int i=0;i<2;i++)SymbolRing("Channel alignment ring",transform,ChannelMarker+Vector3.up*3.5f+markerRight*(-.65f+i*1.3f),.52f,c.cream,markerRight,.08f);
            Refresh();
        }
        private void BuildCrate(int id)
        {
            var root=new GameObject(Labels[id]).transform;root.SetParent(transform,false);root.position=_points[id]+new Vector3(.65f,0,0);
            for(int i=0;i<4;i++)FishingArt.Shape("Weathered crate plank",PrimitiveType.Cube,root,new Vector3(0,.13f+i*.16f,0),new Vector3(.8f,.14f,.7f),_c.wood);
            for(int i=-1;i<=1;i+=2)FishingArt.Shape("Iron binding",PrimitiveType.Cube,root,new Vector3(i*.27f,.39f,-.365f),new Vector3(.065f,.74f,.04f),_c.dark);
            var sign=FishingArt.Shape("Large cargo symbol",PrimitiveType.Cube,root,new Vector3(0,.48f,-.40f),new Vector3(.5f,.37f,.025f),id==2?_c.teal:_c.cream);
            // Bold, distinct silhouette symbols; also named explicitly in the action prompt.
            if(id==2)for(int i=0;i<2;i++)SymbolRing("Twin ring service symbol",root,new Vector3(-.115f+i*.23f,.48f,-.43f),.085f,_c.cream,Vector3.right,.022f);
            else FishingArt.Shape(id==1?"Fish cargo mark":"Stripe cargo mark",id==1?PrimitiveType.Sphere:PrimitiveType.Cube,sign.transform,new Vector3(0,0,-1),new Vector3(.65f,.18f,.1f),_c.orange);
            if(id==2)_component=FishingArt.Shape("Recovered brass component",PrimitiveType.Cylinder,root,new Vector3(0,.85f,0),new Vector3(.25f,.18f,.25f),_c.orange);
        }
        private static void SymbolRing(string label,Transform parent,Vector3 center,float radius,Material material,Vector3 right,float width)
        {
            var ring=FishingArt.Line(label,parent,material,width,false);ring.positionCount=33;
            for(int i=0;i<=32;i++){float a=i*Mathf.PI/16;ring.SetPosition(i,center+(right*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*radius);}
        }
        public int Resolve()
        {
            if(!_c.Ashore)return -1;
            int best=-1; float distance=2.6f;
            for(int i=0;i<_points.Length;i++)
            {
                if(!_available[i] || _c.Shore.CurrentSite!=Regions[i])continue;
                // Leave the historical survey reachable when repair prerequisites are missing.
                if(i==4 && !Facts.Has(FishingExpedition.Accepted|FishingExpedition.RecordRead|FishingExpedition.ComponentFound))continue;
                if((i==0 && Facts.Has(FishingExpedition.RecordRead)) || (i>=1 && i<=3 && Facts.Has(FishingExpedition.ComponentFound)) || (i==4 && Facts.Has(FishingExpedition.BeaconRepaired)))continue;
                float d=Vector3.Distance(_c.Shore.Position,_points[i]);
                if(d<distance && _c.Shore.CanSeeTask(_points[i])){best=i;distance=d;}
            }
            return best;
        }
        public string ActionTitle
        {
            get
            {
                if(Aligning)return "A/D 或 ←/→ 转动 · E 确认 · Esc 取消";
                int target=Resolve();return target<0?null:"E "+Labels[target];
            }
        }
        public string AlignmentHint => Dial.Aligned?"已对准双环航标 · E 点亮信标":(Dial.Error>0?"向右转":"向左转")+" · 偏差 "+Mathf.Abs(Dial.Error).ToString("0")+"°";
        public bool Interact()
        {
            if(_c.Paused || !_c.Ashore)return false;
            if(Aligning){Confirm();return true;}
            int target=Resolve();if(target<0)return false;
            if(target==0){Facts.ReadRecord();_c.CommitExpedition("维修记录：备用线圈运往 D 南岸，认准青色双环箱。鱼纹箱装渔具，条纹箱装布料。");}
            else if(target==2){Facts.FindComponent();Refresh();_c.CommitExpedition("取得维修线圈！不占鱼箱；接取委托并阅读记录后，到 C 旧港安装。");}
            else if(target==1 || target==3)_c.Notice("不是维修箱。"+(target==1?"鱼纹箱是渔具。":"条纹箱是布料。")+"找青色双环标记。",6);
            else if(!Facts.Has(FishingExpedition.Accepted|FishingExpedition.RecordRead|FishingExpedition.ComponentFound))_c.Notice(Clue,7);
            else {Dial.Begin();_c.Notice("灯头校准：A/D 转向海上双环航标，E 确认；Esc 取消，线圈不会消耗。",8);}
            return true;
        }
        public void Confirm()
        {
            if(!Aligning || _c.Paused || !_c.Ashore)return;
            if(Dial.Confirm(Facts)){Refresh();_c.CommitExpedition("旧港信标已点亮！北岛渔区 4 开放；在那里捕获一条珊瑚鱼，记录即可返港提交。");}
            else _c.Notice(AlignmentHint,4);
        }
        public void Cancel() { if(Dial!=null)Dial.Cancel(); }
        public void PortAction()
        {
            if(_c.Paused || !_c.AtHarbor)return;
            if(!Facts.Has(FishingExpedition.Accepted)){Facts.Accept();_c.CommitExpedition("已接受委托：重燃旧港信标。"+Clue);}
            else if(Facts.Deliver())_c.CommitExpedition("捕获记录已提交！旧港信标永久点亮，航道守望者纪念已收录；鱼货保留。");
            else _c.Notice(Clue,7);
        }
        public void ToggleGuide() { PrecisionGuide=!PrecisionGuide; }
        public Vector3 GuidePosition => !Facts.Has(FishingExpedition.Accepted) || Facts.Has(FishingExpedition.TargetCaught)?_c.harbor:!Facts.Has(FishingExpedition.RecordRead)?_points[0]:!Facts.Has(FishingExpedition.ComponentFound)?_points[2]:!Facts.Has(FishingExpedition.BeaconRepaired)?_points[4]:_c.fishingSpots[FishingGrounds.NorthIsland];
        public void RefreshFromCareer()
        {
            Cancel(); PrecisionGuide=false;
            Dial=new FishingBeaconDial(Dial.Target,Dial.Tolerance);
            Refresh();
        }
        private void Refresh() { if(_component!=null)_component.SetActive(!Facts.Has(FishingExpedition.ComponentFound)); }
    }
}
