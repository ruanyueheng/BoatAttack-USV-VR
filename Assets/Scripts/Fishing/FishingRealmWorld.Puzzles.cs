using UnityEngine;
using UnityEngine.InputSystem;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingRealmWorld
    {
        private readonly Transform[] _helpPlaques=new Transform[4];
        private readonly bool[] _windBridgeWanted=new bool[3];
        private readonly float[] _windBridgeOpening=new float[3];
        private readonly Collider[][] _windBridgeColliders=new Collider[3][];
        private Transform _sideReceiver,_sideBridge,_shutterControl,_shutterPanel;
        private Vector3 _shutterHome;
        private bool _windRecovery;
        private int _hintLevel;
        public int HintLevel=>_hintLevel;
        public bool SideLightPathOpen=>Inside && Island==1 && (Trial.LightStage>0 || _c.Career.Lore.Cleared(1));

        private void BuildPuzzleExtensions(int island,Transform room)
        {
            var plaque=new GameObject("Old inscription and optional graduated help").transform;
            plaque.SetParent(room,false);plaque.position=_entries[island].position+room.right*.65f;
            FishingV019Art.Sign(FishingXRText.Adapt("旧刻痕 · E 阅读\n再次阅读可请求线索"),plaque,Vector3.up*1.35f,.008f,new Color(.90f,.82f,.62f));
            _helpPlaques[island]=plaque;
            if(island!=1)return;
            _sideReceiver=new GameObject("First sunlight receiver on the side island").transform;
            var sideIsland=room.InverseTransformPoint(Marker(room,"Ancient2").position);
            var start=room.Find("Device 1").localPosition;
            // The island's centre is an ancient solid tree; put the mechanism on its approach edge.
            var end=sideIsland+(start-sideIsland).normalized*2.6f;
            _sideReceiver.SetParent(room,false);_sideReceiver.localPosition=end+Vector3.up*1.5f;
            Model("MirrorHead",_sideReceiver,Vector3.zero);
            FishingV019Art.Sign("一 · 晨光留驻\n二 · 旧铜闸后的种子",_sideReceiver,Vector3.up*.8f,.009f,new Color(.92f,.85f,.64f));
            _sideBridge=Model("RootBridge",room,start,true);_sideBridge.name="First receiver side root bridge";
            _sideBridge.localRotation=Quaternion.LookRotation(end-start);_sideBridge.localScale=new Vector3(1,1,(end-start).magnitude/5.3f);_sideBridge.gameObject.SetActive(false);
            _shutterControl=new GameObject("Side island shutter control").transform;
            _shutterControl.SetParent(room,false);_shutterControl.localPosition=end;
            Model("ValveWheel",_shutterControl,Vector3.up*1.1f);
            FishingV019Art.Sign(FishingXRText.Adapt("旧铜闸 · E 转动"),_shutterControl,Vector3.up*2,.009f,new Color(.92f,.85f,.64f));
            _shutterControl.gameObject.SetActive(false);
            _shutterHome=new Vector3(0,0,8);_shutterPanel=Model("HoldHatch",room,_shutterHome);
            _shutterPanel.name="Linked optical shutter between the two far mirrors";_shutterPanel.localRotation=Quaternion.Euler(0,90,0);
        }
        private int LightMirror(int order)=>Trial.LightStage==0?order:(order==0?0:order==1?2:1);
        private void ResetBridgePresentation()
        {
            if(Island!=0)return;
            bool completed=_c.Career.Lore.Cleared(0);
            for(int i=0;i<3;i++)
            {
                _windBridgeWanted[i]=completed;_windBridgeOpening[i]=completed?1:0;
                _windBridges[i].localPosition=Vector3.down*(completed?0:1.2f);
                _windBridges[i].gameObject.SetActive(completed);
                if(_windBridgeColliders[i]==null)_windBridgeColliders[i]=_windBridges[i].GetComponentsInChildren<Collider>(true);
                foreach(var collider in _windBridgeColliders[i])collider.enabled=completed;
            }
        }
        private void ResetWindCrossing()
        {
            // Move the capsule to supported ground before any occupied bridge retracts.
            if(!_c.Shore.Relocate(_entries[0].position,Quaternion.identity))return;
            _c.Shore.ResetMoveInput();_windRecovery=true;_windBridgeStage=0;_demoAge=0;_demoNote=0;_padInside=-1;
            Refresh();
        }
        private void PresentPuzzleExtensions()
        {
            if(Keyboard.current!=null && Keyboard.current.f1Key.wasPressedThisFrame && !_c.InventoryOpen && !_c.JournalOpen && !_c.ShopBlocksGameplay && !_c.StoryBlocksGameplay)RequestHint();
            if(Island==0)
                for(int i=0;i<3;i++)
                {
                    float previous=_windBridgeOpening[i];float next=Mathf.MoveTowards(previous,_windBridgeWanted[i]?1:0,Time.deltaTime/1.1f);
                    _windBridgeOpening[i]=next;var bridge=_windBridges[i];bridge.gameObject.SetActive(next>0 || _windBridgeWanted[i]);
                    bridge.localPosition=Vector3.down*(1-Mathf.SmoothStep(0,1,next))*1.2f;
                    foreach(var collider in _windBridgeColliders[i])collider.enabled=next>=1 && _windBridgeWanted[i];
                    if(previous==0 && next>0)_c.WorldAudio?.PlayRealm(Island,"Step",bridge.position,.35f);
                }
            if(Island==1)
            {
                _sideBridge.gameObject.SetActive(SideLightPathOpen);_shutterControl.gameObject.SetActive(SideLightPathOpen);
                _shutterPanel.gameObject.SetActive(Trial.LightStage>0);
                _shutterPanel.localPosition=Vector3.Lerp(_shutterPanel.localPosition,_shutterHome+Vector3.up*(Trial.ShutterOpen?2.6f:0),Mathf.Min(1,Time.deltaTime*5));
            }
        }
        public void RequestHint()
        {
            if(!Inside || Transitioning || _c.Paused)return;
            int level=Mathf.Min(2,_hintLevel++);string hint;
            if(level==0)hint=FishingLore.TrialHint(Island);
            else if(Island==0)hint=level==1?"听钟声辨别石台音色；长回声中会折返，走错只重来当前一轮。桥上起风前旗会先猎猎作响。":Trial.WindRound==0?"第一声来自离入口最远、星纹的那块石台；之后沿着桥一路向前。":Trial.Ready?"看见旗帜狂舞就蹲下（C）或按住 E / 握键稳住，或在石台上等风过去。":"长回声的开头与短回声相同，但走到尽头前要回身一次。";
            else if(Island==1)hint=level==1?"第一处受光后会留下一条侧路；到侧岛解开遮光闸，再看光是否仍按原路前进。":"看光束落在镜子的哪一侧，就朝相反方向再拨一档；"+(Trial.LightStage==0?"光要依次经过三面镜，最后落在侧岛的受光镜上。":"闸开后，光会先跳到最远那面镜，再折回中间那面。");
            else if(Island==2)hint=level==1?"水轻时才能搬动货物；浮桥除了水，还受一块承重铜台约束。潮位计显示排水泵还能撑多久。":"排水泵在"+(_c.Career.Calendar.RisingTide?"涨潮时撑得更短":"落潮时更从容")+"；货箱推过半程会卡在止滑楔上，回灌也不会滑回。先看潮位计，再一口气推。";
            else
            {
                int stage=Mathf.Min(2,Trial.StarStage);float yaw=Trial.TargetYaw(stage),pitch=Trial.TargetPitch(stage);
                hint=level==1?"两页星图分别记录羽翼与锚，最后一幅藏着回家的帆。夜里星光更亮，对焦更快。":"「"+StarPatternName(stage)+"」在"+(yaw<-20?"左侧":yaw>20?"右侧":"正前方")+(pitch>45?"高空":"偏低的天幕")+"，找到后稳住对焦。";
            }
            if(level==2 && Inside)hint+=" · 本境回响贝 "+_c.Career.Lore.ShellCount(Island)+" / 3";
            _c.Notice((level==0?"旧刻痕：":level==1?"规则提示：":"方向提示：")+hint,12);
        }
    }
}
