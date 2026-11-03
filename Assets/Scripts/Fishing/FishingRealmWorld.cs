using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingRealmWorld : MonoBehaviour
    {
        public const string RootName="V019 Tide sanctuaries";
        public const string GeometryRootName="V035 authored level";
        private FishingPrototype _c;
        private readonly Transform[] _portals=new Transform[4],_rooms=new Transform[4],_entries=new Transform[4],_exits=new Transform[4],_devices=new Transform[7];
        private readonly Transform[] _windBridges=new Transform[3],_mirrors=new Transform[3];
        private readonly LineRenderer[] _forestLight=new LineRenderer[4];
        private Vector3 _returnFeet,_cargoStart,_cargoEnd,_gangwayHome;
        private Quaternion _returnRotation;
        private float _nextUse,_demoAge;
        private int _demoNote,_padInside=-1,_windBridgeStage;
        private Material _lightMaterial;
        private Transform _rootBridge,_seedling,_cargoBridge,_waterSurface,_holdGate,_valveWheel;
        private FishingWaterVolume _holdWater;
        private BoxCollider _gangwayDeck;
        private readonly BoxCollider[] _gangwayRails=new BoxCollider[2];
        private Transform _sunWell,_seedReceiver;
        private readonly RaycastHit[] _targetHits=new RaycastHit[64];
        private readonly Collider[] _demoEntranceHits=new Collider[64];
        private bool _pushing;
        public int Island { get; private set; }=-1;
        public bool Inside=>Island>=0;
        public bool Transitioning { get; private set; }
        public float Blackout { get; private set; }
        public FishingShrineTrial Trial { get; private set; }
        public Transform Room=>Inside?_rooms[Island]:null;
        public bool PushingCargo=>_pushing;
        public void ResetInteraction(){_pushing=false;CancelObservation();}
        public Transform Portal(int island)=>island>=0 && island<4?_portals[island]:null;
        public Transform ExitPortal(int island)=>island>=0 && island<4?_exits[island]:null;
        public Vector3 PortalApproach(int island)=>_portals[island].Find("Approach").position;
        public Transform Device(int id)=>id>=0 && id<_devices.Length?_devices[id]:null;
        public bool TryWater(Vector3 position,out float surface,out float bottom)
        {
            surface=bottom=0;
            if(!Inside || Island!=2 || _holdWater==null || !_holdWater.Contains(position))return false;
            surface=_holdWater.SurfaceHeight;bottom=_holdWater.BottomHeight;return true;
        }
        public void DisturbWater(Vector3 position){if(Inside && Island==2)_holdWater.Disturb(position);}
        public Transform Landmark(string name)=>Inside?Marker(Room,name):null;
        public static Transform Marker(Transform room,string name)
        {
            foreach(var t in room.GetComponentsInChildren<Transform>(true))if(t.name=="V35_"+name || t.name.StartsWith("V35_"+name+".",StringComparison.Ordinal))return t;
            throw new InvalidOperationException("Missing authored level marker "+name+" in "+room.name);
        }
        public void Build(FishingPrototype c)
        {
            _c=c;Transform root=null;
            foreach(var go in gameObject.scene.GetRootGameObjects())if(go.name==RootName)root=go.transform;
            if(root==null)throw new InvalidOperationException("Install the V035 authored levels first.");
            _lightMaterial=new Material(c.PaletteShader){name="V035 coherent light and constellation paths"};_lightMaterial.SetFloat("_Unlit",1);
            for(int i=0;i<4;i++)
            {
                _rooms[i]=root.Find("Realm "+i);_entries[i]=Marker(_rooms[i],"Entry");_rooms[i].gameObject.SetActive(false);
                _portals[i]=c.Islands.WorldRoot.GetChild(i).Find("V019 island character/Portal");
                if(_portals[i]==null)throw new InvalidOperationException("Missing island entrance "+i);
                if(i==3)
                {
                    var art=c.Islands.WorldRoot.GetChild(i).Find("V035 crafted coastal scenery");
                    var observatory=art.Find("ObservatoryExterior");
                    _portals[i].position=observatory.TransformPoint(new Vector3(2.4f,3.6f,11.4f));
                    var approach=_portals[i].Find("Approach");approach.localPosition=Vector3.forward*1.8f;
                    var route=art.Find("Island approach");route.GetChild(route.childCount-1).position=approach.position;
                }
                var entrance=new GameObject(FishingPortalVisual.RootName).AddComponent<FishingPortalVisual>();entrance.transform.SetParent(_portals[i],false);entrance.Build(c,i,i==0 || i==2 || i==3);
                _exits[i]=_rooms[i].Find("Device 4");
                // The final platforms already provide supported floors; keep the optional reward separate from leaving.
                _exits[i].localPosition=new[]{new Vector3(-3,5,25.5f),new Vector3(-3.1f,0,23),new Vector3(2.5f,1.8f,19),new Vector3(-2.6f,3.6f,12.4f)}[i];
                _exits[i].localRotation=Quaternion.Euler(0,i==0?90:180,0);
                foreach(Transform child in _exits[i])child.gameObject.SetActive(false);
                var exit=new GameObject(FishingPortalVisual.RootName).AddComponent<FishingPortalVisual>();exit.transform.SetParent(_exits[i],false);exit.Build(c,i,i==2);
                BuildTrialScenery(i);
            }
        }
        private bool ClearTarget(Transform target)=>ClearTarget(_c.ExplorerPosition,target);
        private bool ClearTarget(Vector3 feet,Transform target)
        {
            var from=feet+Vector3.up*1.4f;float height=Inside && Island==2 && target==_devices[0]?.45f:1.2f;
            var to=target.position+Vector3.up*height;var delta=to-from;
            int count=Physics.RaycastNonAlloc(from,delta.normalized,_targetHits,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore);
            if(count==_targetHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                var hit=_targetHits[i];
                if(hit.collider!=_c.Shore.Body && !hit.transform.IsChildOf(target) && !FishingShore.NonSolidSurface(hit.collider))return false;
            }
            return true;
        }
        public bool TryTarget(out int id,out Transform target)
        {
            id=-1;target=null;
            if(_c==null || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing)return false;
            float best=2.75f;
            for(int i=0;i<(Inside?_devices.Length:4);i++)
            {
                var t=Inside?_devices[i]:_portals[i];if(t==null || !t.gameObject.activeInHierarchy)continue;
                float distance=Vector3.Distance(_c.ExplorerPosition,t.position);
                if(distance>=best || !ClearTarget(t))continue;
                best=distance;target=t;id=Inside?i:i+8;
            }
            return id>=0;
        }
        public string NearbyHint
        {
            get
            {
                if(!TryTarget(out int id,out _))
                {
                    if(!Inside || Island!=2 || _gangwayDeck.enabled || FishingPrototype.FlatDistance(_c.Shore.Position,_cargoBridge.position)>=5)return null;
                    return "缆绳仍绷在旧水线上 · 船腹传来空洞的回响";
                }
                if(id>=8)return "E 穿过星光门 · 进入 "+FishingLore.ShrineName(id-8);
                if(id==4)return "E 穿过归航门 · 返回岛屿";
                if(id==6)return "E 阅读旧刻痕 · 再读可逐级求助";
                if(id==5)return "E 转动侧岛铜闸 · "+(Trial.ShutterOpen?"光缝敞开":"光缝闭合");
                if(id==3)return "E 读取潮汐回响";
                if(Island==0)return "走入「"+(id==0?"风":id==1?"潮":"星")+"」共鸣台 · 循声登高";
                if(Island==1)return "E 转动反光镜 · 观察折光落点";
                if(Island==2)return id==0?(_c.DesktopDisplay?"按住 E 随货箱前进":"按住右手柄 A 随货箱前进")+" · "+Mathf.FloorToInt(Trial.CargoProgress*100)+"%":"E 水阀："+new[]{"关闭","排水","注水"}[Trial.State(1)];
                return id==0?"E 操作望远镜 · 观测 "+Trial.StarStage+" / 3":"E 阅读星图线索";
            }
        }
        public bool Interact()
        {
            if(_c.Paused || _c.InventoryOpen || _c.JournalOpen || _c.ShopBlocksGameplay || _c.StoryBlocksGameplay || _c.WorldActionBusy || !TryTarget(out int id,out _))return false;
            if(id>=8)return Enter(id-8);
            if(id==4)return Exit();
            if(id==6){RequestHint();return true;}
            if(id==3)
            {
                if(!Trial.Ready && !_c.Career.Lore.Cleared(Island)){RequestHint();return false;}
                bool firstEcho=!_c.Career.Lore.Cleared(Island);int pay=_c.Career.CompleteShrine(Island);
                if(pay==0 && !_c.Career.Lore.Cleared(Island)){_c.Notice("金币已达上限；回响保留，腾出余额后可再领取。",5);return false;}
                _c.Save();Refresh();_exits[Island].GetComponentInChildren<FishingPortalVisual>().Pulse();_c.Effects?.Celebrate(_c.ExplorerPosition+Vector3.up*.3f);_c.WorldAudio?.PlayRealm(Island,"Echo",_devices[3]!=null?_devices[3].position:_c.ExplorerPosition,.6f);_c.Regions.Show(FishingLore.ShrineName(Island)+" · 回响重现",pay>0?"+90 金币 · 航记已收录":"航记已收录",Island);_c.Notice(FishingLore.Echo(Island)+" 身旁的星光归航门可直接返回岛屿。",12);_c.Voyage?.ClaimRealmReward(Island);if(firstEcho && _c.Career.Lore.Cleared(Island))_c.Prologue.Queue(FishingPrologue.Relic(Island));return true;
            }
            if(Island==0){_c.Notice("让脚步踩入共鸣台，听风桥回应。",4);return false;}
            if(Island==3)
            {
                if(id==0)return BeginObservation();
                _c.Notice(id==1?"第一幅星图展开双翼，第二幅像垂落的船锚。":"最后一幅是归航星：三角帆下，一颗星指向回家的海。",9);return true;
            }
            return Operate(id);
        }
        /// <summary>Shared by E / A interaction and the VR grab-and-twist crank.</summary>
        private bool Operate(int id)
        {
            if(Time.time<_nextUse)return false;_nextUse=Time.time+.4f;
            bool ok=Trial.Use(id==5?3:id);
            if(ok && _devices[id]!=null)_c.WorldAudio?.PlayRealm(Island,"Step",_devices[id].position,.42f);
            if(Island==2 && id==0)
            {
                _pushing=ok;
                if(ok){_c.LowerFieldTools();_c.FieldWorld.ClearToolPresentation();}
                _c.Notice(ok?"保持按住并随货箱前进，直到铜台承重。":"先上侧舷打开水阀，将船腹排干。",5);return ok;
            }
            Refresh();
            _c.Notice(Trial.Ready?"通路已经形成，前往回响所在。":NearbyHint,5);return ok;
        }
        public bool Enter(int island)
        {
            if(island<0 || island>=4 || Inside || Transitioning || !_c.Ashore || _c.Paused || _c.HasPendingCatch || _c.WorldActionBusy || _c.InventoryOpen || _c.JournalOpen || _c.ShopBlocksGameplay || _c.DialogueOpen || _c.Shore.Swimming || _c.Shore.Climbing || !TryTarget(out int target,out _) || target!=island+8)return false;
            _returnFeet=_c.Shore.Position;_returnRotation=_c.Shore.Actor.rotation;StartCoroutine(Travel(island));return true;
        }
        public bool Exit()
        {
            if(!Inside || Transitioning || _c.Paused || _c.DialogueOpen || _c.WorldActionBusy || _c.InventoryOpen || _c.JournalOpen || !TryTarget(out int id,out _) || id!=4)return false;
            StartCoroutine(Travel(-1));return true;
        }
        private IEnumerator Travel(int destination)
        {
            CancelObservation();_pushing=false;Transitioning=true;_c.StoryModalChanged();_c.TackleShop.Close();_c.XRNotebook.Close();
            (destination>=0?_portals[destination]:_exits[Island]).GetComponentInChildren<FishingPortalVisual>().Pulse();_c.Regions.Chime(destination>=0?destination:Island);
            _c.WorldAudio?.Play("PortalEnter",_c.ExplorerPosition+Vector3.up,.5f);
            for(float t=0;t<.28f;t+=Time.unscaledDeltaTime){Blackout=Mathf.Clamp01(t/.28f);yield return null;}
            Blackout=1;int old=Island;bool moved;
            if(destination>=0)
            {
                _rooms[destination].gameObject.SetActive(true);Physics.SyncTransforms();
                moved=_c.Shore.Relocate(_entries[destination].position,Quaternion.identity);
                if(moved)
                {
                    var lore=_c.Career.Lore;bool replay=lore.Cleared(destination);
                    // Cleared realms replay from the start; unfinished ones resume at their saved stage.
                    Island=destination;Trial=new FishingShrineTrial(destination,lore.RealmVariant(destination,()=>_c.Automated?0:UnityEngine.Random.Range(0,3)),replay?0:lore.RealmStage(destination));_nextUse=0;_demoAge=0;_demoNote=0;_padInside=-1;_windBridgeStage=0;
                    for(int i=0;i<5;i++)_devices[i]=Room.Find("Device "+i);
                    _devices[5]=destination==1?_shutterControl:null;_devices[6]=_helpPlaques[destination];_hintLevel=0;_windRecovery=false;
                    ResetBridgePresentation();EnterV044();
                    Refresh();PresentTrial();
                }
                else _rooms[destination].gameObject.SetActive(false);
            }
            else
            {
                moved=RelocateReturnFeet();
                if(moved){Island=-1;Trial=null;_rooms[old].gameObject.SetActive(false);}
            }
            _c.View.ResetLook();_c.Shore.ResetMoveInput();_c.LowerFieldTools();
            for(float t=0;t<.42f;t+=Time.unscaledDeltaTime){Blackout=1-Mathf.Clamp01(t/.42f);yield return null;}
            Blackout=0;Transitioning=false;_c.StoryModalChanged();
            if(!moved){_c.Notice("入口落脚点被遮挡，已保留当前位置。",5);yield break;}
            _c.WorldAudio?.Play("PortalPass",_c.ExplorerPosition+Vector3.up,.5f);
            _c.Regions.Show(Inside?FishingLore.ShrineName(Island):FishingAdventure.Name(old),Inside?FishingLore.TrialObjective(Island)+" · 归航门位于探索终点":FishingLore.Subtitle(old),Inside?Island:old);
        }
        private bool RelocateReturnFeet()
        {
            if(_c.Shore.Relocate(_returnFeet,_returnRotation))return true;
            if(TryEntranceFooting(Island,out var entrance,out _) && _c.Shore.Relocate(entrance,_returnRotation))return true;
            return TryFootingNear(_returnFeet,Island,out var nearby) && _c.Shore.Relocate(nearby,_returnRotation);
        }
        public bool TryEntranceFooting(int island,out Vector3 feet,out Quaternion rotation)
        {
            feet=Vector3.zero;rotation=Quaternion.identity;
            if(_c==null || island<0 || island>=_portals.Length || _portals[island]==null
                || !TryFootingNear(PortalApproach(island),island,out feet))return false;
            var forward=Vector3.ProjectOnPlane(_portals[island].position-feet,Vector3.up);
            rotation=forward.sqrMagnitude>.001f?Quaternion.LookRotation(forward):_portals[island].rotation;
            return true;
        }
        public bool TryDemoEntranceFooting(int island,out Vector3 feet,out Quaternion rotation)
        {
            feet=Vector3.zero;rotation=Quaternion.identity;
            if(_c==null || island<0 || island>=_portals.Length || _portals[island]==null)return false;
            var approach=PortalApproach(island);
            var outward=Vector3.ProjectOnPlane(approach-_portals[island].position,Vector3.up).normalized;
            foreach(var anchor in new[]{approach+outward*.65f,approach})
            {
                if(TryDemoEntranceCandidate(anchor,approach,island,out feet,out rotation))return true;
                for(int ring=1;ring<=3;ring++)for(int step=0;step<16;step++)
                {
                    var candidate=anchor+Quaternion.Euler(0,step*22.5f,0)*Vector3.forward*(ring*.4f);
                    if(TryDemoEntranceCandidate(candidate,approach,island,out feet,out rotation))return true;
                }
            }
            return false;
        }
        private bool TryDemoEntranceCandidate(Vector3 candidate,Vector3 approach,int island,out Vector3 feet,out Quaternion rotation)
        {
            rotation=Quaternion.identity;
            if(!ClearFooting(candidate,island,out feet) || Mathf.Abs(feet.y-approach.y)>.3f)return false;
            var forward=Vector3.ProjectOnPlane(_portals[island].position-feet,Vector3.up);
            if(forward.sqrMagnitude<.001f)return false;
            rotation=Quaternion.LookRotation(forward);
            // The classroom menu needs room for the low controller ray and the open book, not only the standing capsule.
            int count=Physics.OverlapBoxNonAlloc(feet+Vector3.up*1.45f+rotation*Vector3.forward*.65f,
                new Vector3(.6f,.65f,.65f),_demoEntranceHits,rotation,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore);
            if(count==_demoEntranceHits.Length)return false;
            for(int i=0;i<count;i++)if(_demoEntranceHits[i]!=_c.Shore.Body && !FishingShore.NonSolidSurface(_demoEntranceHits[i]))return false;
            return true;
        }
        private bool TryFootingNear(Vector3 anchor,int island,out Vector3 feet)
        {
            if(ClearFooting(anchor,island,out feet))return true;
            // A player may enter beside a railing; return to supported ground nearby instead of trapping them inside.
            for(int ring=1;ring<=3;ring++)for(int step=0;step<8;step++)
                {
                    var candidate=anchor+Quaternion.Euler(0,step*45,0)*Vector3.forward*(ring*.4f);
                    if(ClearFooting(candidate,island,out feet) && Mathf.Abs(feet.y-anchor.y)<=.3f)return true;
                }
            return false;
        }
        private bool ClearFooting(Vector3 candidate,int island,out Vector3 feet)=>_c.Shore.Ground(candidate,out feet)
            && Mathf.Abs(feet.y-candidate.y)<=.3f && Vector3.Distance(feet,_portals[island].position)<2.7f
            && _c.Shore.Clearance(feet) && ClearTarget(feet,_portals[island]);
        public void Abort()
        {
            CancelObservation();_pushing=false;StopAllCoroutines();
            for(int i=0;i<4;i++)if(_rooms[i]!=null)_rooms[i].gameObject.SetActive(false);
            Island=-1;Trial=null;Blackout=0;Transitioning=false;
        }
        public void Tick()
        {
            if(!Inside || Transitioning || _c.Paused)return;
            bool ready=Trial.Ready;int progress=Trial.Progress,round=Trial.WindRound,lightStage=Trial.LightStage;Trial.Tick(Time.deltaTime);
            _demoAge+=Time.deltaTime;
            if(Island==0)
            {
                if(_demoNote<Trial.WindLength && _demoAge>=.7f+_demoNote*1.25f){_c.Regions.Chime(FishingShrineTrial.WindNote(Trial.WindRound,_demoNote));_demoNote++;}
                if(progress>0 && Trial.Progress==0)ResetWindCrossing();
                int pad=-1;
                for(int i=0;i<3;i++)if(FishingPrototype.FlatDistance(_c.Shore.Position,_devices[i].position)<.95f && Mathf.Abs(_c.Shore.Position.y-_devices[i].position.y)<.35f)pad=i;
                if(pad>=0 && pad!=_padInside && !Trial.Ready && !_c.Career.Lore.Cleared(0))
                {
                    bool ok=Trial.Use(pad);_c.Regions.Chime(pad);
                    if(ok)_c.WorldAudio?.PlayRealm(Island,"Step",_devices[pad].position,.5f);
                    if(!ok)ResetWindCrossing();
                    else if(round!=Trial.WindRound)ResetWindCrossing();
                    _windBridgeStage=Trial.WindRound>0 && !_windRecovery?3:Mathf.Min(3,Trial.Progress);
                    _c.Notice(!ok?"回声散去，风将你送回入口；本轮重新聆听。":round!=Trial.WindRound?"巨钟应和，另一段更长的回声从入口传来。":"共鸣回应 · "+Trial.Progress+" / "+Trial.WindLength,4);Refresh();
                }
                _padInside=pad;if(pad>=0)_safeFeet=_devices[pad].position;
            }
            if(Island==2)
            {
                bool held=_c.DesktopDisplay?(Keyboard.current!=null && Keyboard.current.eKey.isPressed):HeldAction();
                if(_pushing)
                {
                    bool focused=_c.Automated || Application.isFocused;
                    if(!held || !focused || FishingPrototype.FlatDistance(_c.Shore.Position,_devices[0].position)>2.1f || Trial.WaterLevel>.15f)_pushing=false;
                    else Trial.PushCargo(Time.deltaTime);
                }
            }
            if(ready!=Trial.Ready || progress!=Trial.Progress || round!=Trial.WindRound || lightStage!=Trial.LightStage)
            {
                Refresh();
            }
            TickV044();
            if(_c.Shore.Position.y<Room.position.y-4)
            {
                CancelObservation();_pushing=false;
                if(_c.Shore.Relocate(_safeFeet,Quaternion.identity) || _c.Shore.Relocate(_entries[Island].position,Quaternion.identity))_c.Notice("海风把你送回最近的落脚点，机关进度都还在。",5);
            }
            PresentTrial();
        }
        private static bool HeldAction()
        {var d=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);d.TryGetFeatureValue(CommonUsages.primaryButton,out bool held);return held || FishingXRInput.Grip(d);}
        private void Refresh()
        {
            bool open=Trial.Ready || _c.Career.Lore.Cleared(Island);
            for(int i=0;i<3;i++)
            {
                var t=_devices[i];if(t==null)continue;var signal=t.Find("Signal");
                if(signal!=null)signal.gameObject.SetActive(Island==3?Trial.StarStage>i:Island==1?Trial.State(i)==Trial.MirrorTarget(i):Trial.State(i)>0);
            }
            if(Island==0)for(int i=0;i<3;i++)_windBridgeWanted[i]=open || _windBridgeStage>i;
            if(Island==1)foreach(var collider in _rootBridge.GetComponentsInChildren<Collider>())collider.enabled=open;
            if(Island==2)
            {
                _gangwayDeck.enabled=open && Trial.WaterLevel>=.95f;
                foreach(var rail in _gangwayRails)rail.enabled=_gangwayDeck.enabled;
            }
        }
        private Transform Model(string name,Transform parent,Vector3 local,bool solid=false)
        {
            var source=Resources.Load<GameObject>("FishingV035/"+name);if(source==null)throw new InvalidOperationException("Missing authored V035 model "+name);
            var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=local;Instantiate(source,root,false);
            FishingV035Materials.Apply(root);
            if(solid)foreach(var f in root.GetComponentsInChildren<MeshFilter>())f.gameObject.AddComponent<MeshCollider>().sharedMesh=Resources.Load<Mesh>("FishingV035/"+name+"Collision")??f.sharedMesh;
            return root;
        }
        private void BuildTrialScenery(int island)
        {
            var room=_rooms[island];
            if(island==0)for(int i=0;i<3;i++){_windBridges[i]=Model("WindBridge"+i,room,Vector3.zero,true);_windBridges[i].gameObject.SetActive(false);}
            if(island==1)
            {
                _sunWell=Marker(room,"SunWell");_seedReceiver=Marker(room,"Seed");
                for(int i=0;i<3;i++)_mirrors[i]=Model("MirrorHead",room.Find("Device "+i),Vector3.up*1.5f);
                for(int i=0;i<4;i++){_forestLight[i]=FishingArt.Line("Connected sunlight "+i,room,_lightMaterial,.035f,false);_forestLight[i].useWorldSpace=true;_forestLight[i].positionCount=2;}
                _rootBridge=Model("RootBridge",room,room.InverseTransformPoint(Marker(room,"RootBridge").position),true);
                _seedling=Model("MangroveTree",room,room.InverseTransformPoint(Marker(room,"Seed").position));
                FishingArt.Shape("Root grotto stream",PrimitiveType.Plane,room,new Vector3(0,-.38f,1),new Vector3(2.6f,1,4.8f),Resources.Load<Material>("FishingV035/RealmWater"));
            }
            if(island==2)
            {
                var chart=room.Find("Device 3/CaptainChart");
                var top=chart.gameObject.AddComponent<BoxCollider>();top.center=new Vector3(0,.96f,0);top.size=new Vector3(1.7f,.10f,1);
                foreach(float x in new[]{-.75f,.75f})foreach(float z in new[]{-.4f,.4f})
                {
                    var leg=chart.gameObject.AddComponent<BoxCollider>();leg.center=new Vector3(x,.45f,z);leg.size=new Vector3(.08f,.90f,.08f);
                }
                _valveWheel=Model("ValveWheel",room.Find("Device 1"),new Vector3(0,1.15f,-.2f));
                _cargoStart=room.InverseTransformPoint(Marker(room,"CargoStart").position);_cargoEnd=room.InverseTransformPoint(Marker(room,"CargoEnd").position);
                _gangwayHome=room.InverseTransformPoint(Marker(room,"FloatGangway").position);_cargoBridge=Model("FloatGangway",room,_gangwayHome);
                _cargoBridge.localScale=new Vector3(1.25f,1,1.08f);
                _gangwayDeck=_cargoBridge.gameObject.AddComponent<BoxCollider>();_gangwayDeck.center=Vector3.down*.02f;_gangwayDeck.size=new Vector3(2.4f,.20f,5.8f);_gangwayDeck.enabled=false;
                for(int i=0;i<2;i++)
                {
                    var rail=new GameObject("Solid gangway side rope "+i).AddComponent<BoxCollider>();rail.transform.SetParent(_cargoBridge,false);
                    rail.center=new Vector3(i==0?-1.24f:1.24f,.38f,0);rail.size=new Vector3(.08f,.86f,5.2f);rail.enabled=false;_gangwayRails[i]=rail;
                }
                _holdGate=Model("HoldHatch",room,room.InverseTransformPoint(Marker(room,"HoldGate").position));
                var hatch=_holdGate.gameObject.AddComponent<BoxCollider>();hatch.center=Vector3.up*.75f;hatch.size=new Vector3(2.1f,1.5f,.20f);
                _holdGate.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(Marker(room,"HoldLanding").position-_holdGate.position,Vector3.up));
                _holdWater=new GameObject("Moving water in cargo hold").AddComponent<FishingWaterVolume>();_waterSurface=_holdWater.transform;
                _waterSurface.SetParent(room,false);_waterSurface.localPosition=Vector3.forward*.5f;
                _holdWater.Build(new Vector2(9.3f,28),-2.5f,Resources.Load<Material>("FishingV035/RealmWater"));
            }
            if(island==3)BuildObservation(room);
            BuildPuzzleExtensions(island,room);BuildV044(island,room);
        }
        private void PresentTrial()
        {
            PresentPuzzleExtensions();
            if(Island==1)
            {
                var source=_sunWell.position;var receiver=Trial.LightStage==0?_sideReceiver.position:_seedReceiver.position+Vector3.up*.6f;
                var first=_devices[0].position+Vector3.up*1.5f;_forestLight[0].enabled=true;_forestLight[0].SetPosition(0,source);_forestLight[0].SetPosition(1,first);
                bool connected=true;
                for(int order=0;order<3;order++)
                {
                    int i=LightMirror(order),prior=LightMirror(Mathf.Max(0,order-1)),following=LightMirror(Mathf.Min(2,order+1));
                    var start=_devices[i].position+Vector3.up*1.5f;var previous=order==0?source:_devices[prior].position+Vector3.up*1.5f;
                    var next=order==2?receiver:_devices[following].position+Vector3.up*1.5f;var correct=(next-start).normalized;
                    var outgoing=Quaternion.AngleAxis((Trial.State(i)-Trial.MirrorTarget(i))*90,Vector3.up)*correct;var normal=((start-previous).normalized-outgoing).normalized;
                    if(normal.sqrMagnitude>.01f)_mirrors[i].rotation=Quaternion.LookRotation(normal,Vector3.up);
                    var line=_forestLight[order+1];line.enabled=connected;line.SetPosition(0,start);
                    bool aligned=Trial.State(i)==Trial.MirrorTarget(i);var end=aligned?next:start+outgoing*8;
                    if(Trial.LightStage>0 && order==1 && aligned && !Trial.ShutterOpen)end=Vector3.Lerp(start,next,.5f);
                    line.SetPosition(1,end);line.startColor=line.endColor=new Color(1,.85f,.48f);connected&=aligned && !(Trial.LightStage>0 && order==1 && !Trial.ShutterOpen);
                }
                _rootBridge.gameObject.SetActive(Trial.Growth>.001f || _c.Career.Lore.Cleared(Island));float growth=_c.Career.Lore.Cleared(Island)?1:Trial.Growth;
                _rootBridge.localScale=new Vector3(1,1,Mathf.Max(.02f,growth));_seedling.localScale=Vector3.one*Mathf.Lerp(.15f,.55f,growth);
            }
            if(Island==2)
            {
                _devices[0].localPosition=Vector3.Lerp(_cargoStart,_cargoEnd,Trial.CargoProgress);
                bool released=Trial.CargoProgress>=1 || _c.Career.Lore.Cleared(2);
                // Locked moorings keep the boards submerged until the cargo plate releases the route.
                float floatLevel=released?Trial.WaterLevel:Mathf.Min(Trial.WaterLevel,.75f);
                var p=_gangwayHome;p.y=Mathf.Lerp(_cargoStart.y-.3f,_gangwayHome.y,floatLevel);_cargoBridge.localPosition=p;
                _valveWheel.localRotation=Quaternion.Slerp(_valveWheel.localRotation,Quaternion.Euler(0,0,Trial.State(1)==1?-90:Trial.State(1)==2?90:0),Time.deltaTime*5);
                _holdWater.SetLevel(Mathf.Lerp(_cargoStart.y-.5f,1.2f,Trial.WaterLevel));
                _holdGate.gameObject.SetActive(Trial.WaterLevel>.2f);
            }
        }
        private void OnDestroy(){if(_lightMaterial!=null)Destroy(_lightMaterial);if(_starSphereMesh!=null)Destroy(_starSphereMesh);}
        private void OnDisable(){if(_c!=null)Abort();}
    }
}
