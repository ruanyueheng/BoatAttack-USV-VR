using UnityEngine;
using System.Collections.Generic;
using Keyboard=UnityEngine.InputSystem.Keyboard;
using Mouse=UnityEngine.InputSystem.Mouse;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Small tabletop crafts. The real instruments stay in the world, away from the eyes.</summary>
    public sealed class FishingHandGames : MonoBehaviour
    {
        private FishingPrototype _c;
        private readonly Transform[] _stations=new Transform[4],_tools=new Transform[4];
        private readonly TextMesh[] _signs=new TextMesh[4];
        private readonly Light[] _lamps=new Light[4];
        private readonly Dictionary<Renderer,bool> _homeLabels=new Dictionary<Renderer,bool>();
        private Material _toolMaterial;
        private Material _material,_sandMaterial,_shellMaterial,_grainMaterial;
        private readonly Transform[] _workLeft=new Transform[2],_workRight=new Transform[2];
        private Quaternion _toolRest;
        private Quaternion _trackedToolRotation;
        private bool _trackedToolHeld,_hasTrackedContact;
        private readonly FishingPressEdge _worldGrip=new FishingPressEdge();
        private Vector3 _lastTrackedContact;
        private Transform _crank,_coil,_sand,_tray,_caughtSand,_feedCoil;
        private readonly Transform[] _shells=new Transform[6],_grains=new Transform[32];
        private readonly float[] _grainAge=new float[32];
        private readonly Vector3[] _grainStart=new Vector3[32];
        private Mesh _grainMesh;
        private LineRenderer _thread;
        private float _shakePhase,_visualSpeed,_emission;
        private int _visualFrame=-1,_nextGrain;
        private Transform _sieveGripA,_sieveGripB,_leftGrip,_rightGrip,_supportA,_supportB,_winderHandle;
        private float _crankStart,_crankSign,_crankHandleStart;
        private Canvas _canvas;
        private Text _title,_status;
        private FishingHandTask _task;
        private FishingCareer _career;
        private Vector2 _mouse,_origin,_motion;
        private Vector3 _toolHome,_cameraPosition;
        private Quaternion _frame,_cameraRotation,_nativeMotionFrame;
        private bool _recipeHeld=true;
        public bool ForgeGunParts {get;private set;}
        private bool _ready,_calibrated,_exitHeld,_desktopLabels;
        private int _index,_lastPercent=-1,_lastInteractionState=-1;
        private float _started,_cameraFov;
        private float _motionSpeed;
        private float _hapticAt,_audioAt;
        public static Vector2 SieveMotionPoint(Vector3 trackedPosition,Vector3 calibratedCenter,Quaternion calibratedFrame)
        {var relative=Quaternion.Inverse(calibratedFrame)*(trackedPosition-calibratedCenter);return new Vector2(relative.x,relative.y);}
        public bool Active=>_task!=null;
        public bool XRToolHeld=>_trackedToolHeld;
        public FishingHandTask Task=>_task;
        public Transform Station(int index)=>_stations[index];
        public Vector3 Approach(int index)=>_stations[index].TransformPoint(new Vector3(0,0,.60f));
        public Transform ActiveTool=>!Active?null:_index==1?_crank:_tools[_index];
        public Vector3 HandlePoint=>!Active?Vector3.zero:_rightGrip.position;
        public Vector3 WorkPoint=>!Active || _index<2?Vector3.zero:_stations[_index].TransformPoint(_index==2?new Vector3(-.12f,1.025f,0):new Vector3(-.10f,1.035f,-.12f));
        public Vector3 ToolContact=>!Active || _index<2?Vector3.zero:_tools[_index].TransformPoint(_index==2?new Vector3(.08f,.35f,0):new Vector3(.132f,.40f,0));
        public static string Name(int index)=>index==0?"潮汐摇筛":index==1?"手摇绕绳":index==2?"往复锯木":"锻锤塑形";
        public static string StartHint(bool desktop)=>desktop?"E 动手试试":"握柄旁按右握把 · A 开始";
        public string Instruction=>_index==1?"按住鼠标左键，沿摇柄画圈绕线":_index==2?"按住左键前后拖动，沿锯缝推拉":_index==3?"按住左键向上抬锤，再向下敲到铁坯":"按住鼠标左键，左右往复";
        public int Count=>_task==null?0:_index==1?Mathf.FloorToInt(_task.Progress*3):_task.Strokes;
        public void Build(FishingPrototype c)
        {
            _c=c;_material=Resources.Load<Material>("FishingV034/Handcraft");
            if(_material==null)throw new System.InvalidOperationException("Prepare V034 handcraft art before opening the scene.");
            _toolMaterial=new Material(c.PaletteShader){name="Handcraft tool vertex colours"};
            _sandMaterial=new Material(_material);_sandMaterial.SetColor("_BaseColor",new Color(.90f,.79f,.60f));_sandMaterial.SetFloat("_Smoothness",.25f);
            _shellMaterial=new Material(Resources.Load<Material>("FishingV035/Coastal"));_shellMaterial.SetColor("_BaseColor",new Color(1,.87f,.82f));
            _grainMaterial=new Material(c.PaletteShader){name="Warm sifted sand"};_grainMaterial.SetColor("_BaseColor",new Color(.67f,.45f,.23f));_grainMaterial.SetFloat("_Shadows",1);
            for(int i=0;i<4;i++)
            {
                var root=new GameObject(Name(i)).transform;root.SetParent(transform,false);
                var seed=c.Home.Origin+(i<2?new Vector3(i==0?-3.5f:3.5f,.2f,-6.2f):new Vector3(-10,.2f,i==2?-4:0));
                if(!c.Shore.Ground(seed,out var floor) || !c.Shore.Clearance(floor+Vector3.back*.6f))
                    throw new System.InvalidOperationException("Handcraft needs a supported, reachable porch footing.");
                root.SetPositionAndRotation(floor,Quaternion.Euler(0,180,0));_stations[i]=root;
                var table=Model("CraftTable",root);var obstacle=table.gameObject.AddComponent<BoxCollider>();
                obstacle.center=new Vector3(0,.47f,0);obstacle.size=new Vector3(1.04f,.94f,.56f);
                Model("LinenMat",root).localPosition=Vector3.up*.958f;
                Model("StarCharm",root).localPosition=new Vector3(-.34f,.966f,-.12f);
                var light=new GameObject("Warm porch bounce").AddComponent<Light>();light.transform.SetParent(root,false);light.transform.localPosition=new Vector3(0,1.45f,.14f);
                light.type=LightType.Point;light.range=1.7f;light.color=new Color(1,.83f,.64f);light.intensity=.18f;light.shadows=LightShadows.None;light.enabled=false;_lamps[i]=light;
                if(i==0)
                {
                    _tools[0]=Model("SieveFrame",root);_tools[0].localPosition=new Vector3(0,1.125f,0);
                    _sand=Model("SieveSand",_tools[0]);
                    foreach(var renderer in _sand.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=_sandMaterial;
                    _tray=Model("SieveCatchTray",root,"FishingV035/");_tray.localPosition=Vector3.up*.965f;
                    _caughtSand=Model("SieveSand",_tray);_caughtSand.localPosition=Vector3.up*.008f;_caughtSand.gameObject.SetActive(false);
                    foreach(var renderer in _caughtSand.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=_sandMaterial;
                    for(int j=0;j<_shells.Length;j++)
                    {
                        _shells[j]=Model("SieveShell",_tools[0],"FishingV035/");
                        foreach(var renderer in _shells[j].GetComponentsInChildren<Renderer>())renderer.sharedMaterial=_shellMaterial;
                    }
                    BuildGrains(root);
                    _sieveGripA=Marker(_tools[0],"SieveGripA");_sieveGripB=Marker(_tools[0],"SieveGripB");
                }
                else if(i==1)
                {
                    _tools[1]=Model("WinderBody",root);_tools[1].localPosition=new Vector3(0,.958f,0);
                    _tools[1].localScale=Vector3.one*1.6f;_tools[1].localRotation=Quaternion.identity;
                    _crank=Model("WinderCrank",_tools[1]);_crank.localPosition=new Vector3(0,.134f,.035f);_crank.localScale=Vector3.one*.8f;
                    var wheelMesh=_crank.GetChild(0);wheelMesh.localRotation=Quaternion.Euler(0,180,0)*wheelMesh.localRotation;
                    _coil=Model("WinderCoil",_crank);_crank.localRotation=Quaternion.Euler(0,0,180);
                    _supportA=Marker(_tools[1],"WinderSupportA");_supportB=Marker(_tools[1],"WinderSupportB");_winderHandle=Marker(_crank,"WinderHandle");
                    _feedCoil=Model("WinderCoil",root);_feedCoil.localPosition=new Vector3(-.25f,.99f,-.07f);_feedCoil.localScale=Vector3.one*.65f;
                    _thread=FishingArt.Line("Flax feeding into spindle",root,c.cream,.002f,false);_thread.positionCount=12;_thread.useWorldSpace=true;
                }
                if(i>=2)
                {
                    _tools[i]=Model(i==2?"Saw":"Hammer",root,"FishingFeedbackTools/");
                    // Model +Y runs along the blade/shaft; +X is the teeth/striking face.
                    _tools[i].localRotation=Quaternion.LookRotation(Vector3.right,Vector3.back);
                    _tools[i].localScale=Vector3.one*(i==3?.8f:1);
                    _tools[i].localPosition=i==2?new Vector3(-.12f,1.105f,.35f):new Vector3(-.10f,1.1406f,.20f);
                    _workRight[i-2]=new GameObject("Tool grip").transform;_workRight[i-2].SetParent(_tools[i],false);_workRight[i-2].localPosition=i==2?new Vector3(.055f,.035f,0):new Vector3(0,.13f,0);
                    _workLeft[i-2]=new GameObject("Work support").transform;_workLeft[i-2].SetParent(root,false);_workLeft[i-2].localPosition=i==2?new Vector3(.19f,1.045f,.09f):new Vector3(.29f,1.01f,.17f);
                    var piece=Model(i==2?"Plank":"IronIngot",root,"FishingV023/Items/");piece.localPosition=new Vector3(0,1.0f,0);
                    var surfaces=piece.GetComponentsInChildren<Renderer>();var bounds=surfaces[0].bounds;foreach(var r in surfaces)bounds.Encapsulate(r.bounds);
                    var size=i==2?new Vector3(.6f,.05f,.18f):new Vector3(.2f,.07f,.12f);
                    piece.localScale=Vector3.Scale(piece.localScale,new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z));
                    bounds=surfaces[0].bounds;foreach(var r in surfaces)bounds.Encapsulate(r.bounds);piece.position+=root.TransformPoint(i==2?new Vector3(0,1,0):new Vector3(-.10f,1,-.12f))-bounds.center;
                    if(i==2)foreach(var r in surfaces)r.sharedMaterial=_c.wood;
                }
                var sign=new GameObject("Small handcraft label",typeof(TextMesh));sign.transform.SetParent(root,false);sign.transform.localPosition=new Vector3(0,1.19f,-.10f);
                var label=sign.GetComponent<TextMesh>();label.font=FishingUIFont.Font;label.fontSize=32;label.characterSize=.0045f;
                label.anchor=TextAnchor.MiddleCenter;label.color=FishingUITheme.Light;label.text=Name(i)+"\n"+StartHint(c.DesktopDisplay);
                sign.GetComponent<Renderer>().sharedMaterial=FishingUIFont.Font.material;_signs[i]=label;
            }
            var panel=new GameObject("Small XR table-side instruction",typeof(RectTransform),typeof(Canvas));panel.layer=5;panel.transform.SetParent(transform,false);
            _canvas=panel.GetComponent<Canvas>();_canvas.renderMode=RenderMode.WorldSpace;_canvas.worldCamera=c.viewCamera;
            panel.GetComponent<RectTransform>().sizeDelta=new Vector2(430,148);panel.transform.localScale=Vector3.one*.00065f;
            var paper=panel.AddComponent<Image>();FishingUITheme.Bind(paper,0);paper.sprite=FishingUITheme.PaperSprite;paper.type=Image.Type.Sliced;paper.material=FishingUITheme.WorldMaterial;paper.raycastTarget=false;
            _title=Label(panel.transform,new Vector2(16,-10),new Vector2(398,38),28);
            _title.font=FishingUITheme.Heading;var outline=_title.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color32(221,171,67,255);outline.effectDistance=new Vector2(1.25f,-1.25f);
            _status=Label(panel.transform,new Vector2(16,-50),new Vector2(398,92),21);panel.SetActive(false);
        }
        private static Transform Marker(Transform model,string name)
        {
            foreach(var child in model.GetComponentsInChildren<Transform>())if(child.name==name)return child;
            throw new System.InvalidOperationException("Handcraft model is missing calibrated marker "+name);
        }
        private Transform Model(string name,Transform parent,string folder="FishingV034/")
        {
            var source=Resources.Load<GameObject>(folder+name);
            if(source==null)throw new System.InvalidOperationException("Missing V034 handcraft model "+name);
            var root=new GameObject(name).transform;root.SetParent(parent,false);var go=Instantiate(source,root,false);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=folder=="FishingV034/"?_material:(folder=="FishingFeedbackTools/" || folder=="FishingV023/Items/")?_toolMaterial:Resources.Load<Material>(folder+"Coastal");
            FishingPaintedArt.Apply(root,folder+name);
            return root;
        }
        private void BuildGrains(Transform parent)
        {
            var b=new FishingMeshBuilder();b.Ellipsoid(Vector3.zero,Vector3.one,Color.white,6,3);_grainMesh=b.Finish("Shared fine sieve grains");
            for(int i=0;i<_grains.Length;i++)
            {
                var go=new GameObject("Falling sand "+i,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
                go.GetComponent<MeshFilter>().sharedMesh=_grainMesh;go.GetComponent<MeshRenderer>().sharedMaterial=_grainMaterial;
                go.transform.localScale=new Vector3(.0022f,.0028f,.0017f);_grains[i]=go.transform;_grainAge[i]=-1;go.SetActive(false);
            }
            ResetShells();
        }
        private void ResetShells()
        {
            for(int i=0;i<_shells.Length;i++)
            {
                float a=i*2.399963f,r=.033f+(i%3)*.024f;
                _shells[i].localPosition=new Vector3(Mathf.Cos(a)*r,.007f,Mathf.Sin(a)*r*.65f);
                _shells[i].localRotation=Quaternion.Euler(0,i*57,0);
            }
        }
        private static Text Label(Transform parent,Vector2 position,Vector2 size,int font)
        {
            var go=new GameObject("Caption",typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=FishingUIFont.Font;text.fontSize=font;FishingUITheme.Bind(text,2);text.material=FishingUITheme.WorldMaterial;text.raycastTarget=false;
            var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;return text;
        }
        public bool TryTarget(out int index,out Transform root)
        {
            index=-1;root=null;if(_c==null || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing)return false;
            float best=1.8f;
            for(int i=0;i<4;i++)
            {
                float distance=Vector3.Distance(_c.Shore.Position,_stations[i].position);var target=_tools[i].position;
                if(distance<best && Vector3.Dot(_c.viewCamera.transform.forward,(target-_c.viewCamera.transform.position).normalized)>.2f
                    && _c.Shore.ClearSight(_c.Shore.Position+Vector3.up*_c.Shore.EyeHeight,target))
                {best=distance;index=i;root=_stations[i];}
            }
            return index>=0;
        }
        public bool Begin(int index)=>Begin(index,false);
        private bool Begin(int index,bool directGrab)
        {
            if(Active || index<0 || index>3 || _c.Paused || _c.StartMenuOpen || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.HasPendingCatch || _c.WorldActionBusy
                || _c.InventoryOpen || _c.JournalOpen || _c.XRNotebook.BlocksGameplay || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing
                || (!directGrab && (!TryTarget(out int nearby,out _) || nearby!=index)))return false;
            if(FishingPrototype.FlatDistance(_c.Shore.Position,_stations[index].position)>(_c.DesktopDisplay?.67f:1.10f))
            {_c.Notice("再靠近一点，站到桌沿旁就能动手了。",4);return false;}
            var task=new FishingHandTask((FishingHandTaskKind)index,index==3 && ForgeGunParts,_c.Career.Lore);
            if(!task.HasMaterials(_c.Career.Backpack)){_c.Notice(Name(index)+"需要"+(index==0?"砂×2":index==1?"植物纤维×3":index==2?"漂流木×1":ForgeGunParts?"铁锭×6":"铁锭×3")+"，完成前不会扣除材料。",5);return false;}
            if(_c.DesktopDisplay)
            {
                _c.Shore.SetCrouch(false);
                if(_c.Shore.Crouched){_c.Notice("站起身后再开始桌边手作。",4);return false;}
            }
            _index=index;_task=task;_career=_c.Career;_ready=_calibrated=false;_exitHeld=_recipeHeld=true;_mouse=_motion=new Vector2(.1f,0);
            _motionSpeed=_visualSpeed=_shakePhase=_emission=0;_trackedToolHeld=_hasTrackedContact=false;_lastInteractionState=-1;
            _frame=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0);_toolHome=_tools[index].localPosition;_toolRest=_tools[index].localRotation;
            if(index==0)
            {
                bool aIsRight=Vector3.Dot(_sieveGripA.position-_sieveGripB.position,_frame*Vector3.right)>0;
                _leftGrip=aIsRight?_sieveGripB:_sieveGripA;_rightGrip=aIsRight?_sieveGripA:_sieveGripB;
            }
            else if(index==1)
            {
                var facing=Vector3.ProjectOnPlane(_c.Shore.Character.EyePosition-_tools[1].position,Vector3.up);
                if(!directGrab && facing.sqrMagnitude>.01f)_tools[1].rotation=Quaternion.LookRotation(facing);
                _leftGrip=Vector3.Dot(_supportA.position-_supportB.position,_frame*Vector3.right)<0?_supportA:_supportB;_rightGrip=_winderHandle;
                float grabbedAngle=_crank.localEulerAngles.z;
                _crank.localRotation=Quaternion.identity;_crankStart=directGrab?grabbedAngle:Vector3.Dot(_winderHandle.position-_crank.position,_frame*Vector3.right)>0?0:180;
                _crank.localRotation=Quaternion.Euler(0,0,_crankStart);var before=_winderHandle.position;
                _crank.localRotation=Quaternion.Euler(0,0,_crankStart+5);_crankSign=Vector3.Dot(_winderHandle.position-before,Vector3.up)>0?1:-1;
                _crank.localRotation=Quaternion.Euler(0,0,_crankStart);
                var handleOffset=Quaternion.Inverse(_tools[1].rotation)*(_winderHandle.position-_crank.position);
                _crankHandleStart=Mathf.Atan2(handleOffset.y,handleOffset.x)*Mathf.Rad2Deg;
            }
            else {_leftGrip=_workLeft[index-2];_rightGrip=_workRight[index-2];}
            _cameraPosition=_c.DesktopDisplay?_c.Shore.Character.EyePosition:_c.viewCamera.transform.position;_cameraRotation=_c.viewCamera.transform.rotation;_cameraFov=_c.viewCamera.fieldOfView;_started=Time.unscaledTime;_lastPercent=-1;
            _c.StoryModalChanged();_c.FieldWorld.ClearToolPresentation();
            _c.Shore.Character.SetTabletopView(true);
            _homeLabels.Clear();foreach(var label in _c.GetComponentsInChildren<TextMesh>())
            {if((label.transform.position-_c.Home.Origin).sqrMagnitude>25*25)continue;var renderer=label.GetComponent<Renderer>();_homeLabels[renderer]=renderer.enabled;renderer.enabled=false;}
            if(!_c.DesktopDisplay)_canvas.transform.SetPositionAndRotation(_stations[index].TransformPoint(new Vector3(-.32f,1.25f,-.06f)),_cameraRotation);
            else
            {
                _c.Shore.Character.InteractionLean=_c.Shore.Actor.forward*.16f-Vector3.up*(index==0?.15f:.06f);
                _cameraPosition=_c.Shore.Character.EyePosition;
            }
            _canvas.gameObject.SetActive(!_c.DesktopDisplay);_title.text=Name(index);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        public void SwitchForgeOutput()
        {
            if(!Active || _index!=3)return;
            var next=new FishingHandTask(FishingHandTaskKind.Forge,!ForgeGunParts);
            if(!next.HasMaterials(_c.Career.Backpack)){_c.Notice("切换配方需备齐铁锭：镐头3个，枪械铁件6个；本次材料未扣除。",5);return;}
            ForgeGunParts=!ForgeGunParts;_task=next;_ready=_calibrated=_trackedToolHeld=_hasTrackedContact=false;_lastPercent=-1;
        }
        private bool TryBeginXRGrab(bool focused)
        {
            if(!focused || _c==null || _c.Hands==null || _c.XRSeat.HeadBlocked || _c.Hands.HoldingRod
                || !_c.Hands.TryControllerPalm(false,out var palm,out _))return false;
            int nearest=-1;float distance=.22f;
            var right=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0)*Vector3.right;
            for(int i=0;i<4;i++)
            {
                var grip=i==0?(Vector3.Dot(_sieveGripA.position-_sieveGripB.position,right)>0?_sieveGripA:_sieveGripB):i==1?_winderHandle:_workRight[i-2];
                float reach=Vector3.Distance(palm,grip.position);
                if(reach>=distance || !_c.Shore.ClearSight(palm,grip.position))continue;
                nearest=i;distance=reach;
            }
            if(nearest<0 || !Begin(nearest,true))return false;
            // This grip press is the entry action; the player need not open the hand and grab twice.
            _ready=true;return true;
        }
        private bool TrackedToolMotion(Vector3 palm,Quaternion palmRotation,out Vector2 point)
        {
            point=_motion;
            if(_index==1)
            {
                // Use the same world frame as the visible wheel. A head rotation/recenter must not move its centre.
                var relative=Quaternion.Inverse(_tools[1].rotation)*(palm-_crank.position);
                float angle=(Mathf.Atan2(relative.y,relative.x)*Mathf.Rad2Deg-_crankHandleStart)*_crankSign*Mathf.Deg2Rad;
                float radius=new Vector2(relative.x,relative.y).magnitude;
                point=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                float plane=(Quaternion.Inverse(_tools[1].rotation)*(_winderHandle.position-_crank.position)).z;
                return radius>=.04f && radius<=.22f && Mathf.Abs(relative.z-plane)<.18f;
            }
            var tool=_tools[_index];var rotation=palmRotation*_trackedToolRotation;
            tool.SetPositionAndRotation(palm+palmRotation*new Vector3(0,-.026f,0)-rotation*Vector3.Scale(_rightGrip.localPosition,tool.localScale),rotation);
            var local=_stations[_index].InverseTransformPoint(ToolContact)-_stations[_index].InverseTransformPoint(WorkPoint);
            if(_index==2)
            {
                var a=tool.TransformPoint(new Vector3(.08f,.20f,0));var b=tool.TransformPoint(new Vector3(.08f,.60f,0));
                var blade=b-a;var nearest=a+blade*Mathf.Clamp01(Vector3.Dot(WorkPoint-a,blade)/blade.sqrMagnitude);
                point=new Vector2(-_stations[2].InverseTransformPoint(palm).z,0);
                return Vector3.Distance(nearest,WorkPoint)<.08f && Mathf.Abs(Vector3.Dot(tool.up,_stations[2].forward))>.6f
                    && Vector3.Dot(tool.right,Vector3.down)>.5f;
            }
            var previous=_lastTrackedContact;bool observed=_hasTrackedContact;_lastTrackedContact=local;_hasTrackedContact=true;
            point=new Vector2(local.x,local.y);
            if(local.y>.025f)return Mathf.Abs(local.x)<.45f && Mathf.Abs(local.z)<.45f;
            // A fast downstroke can pass through the thin billet between two tracked frames.
            // Test that swept face at the billet height, rather than requiring a frame inside the metal.
            var impact=local;
            bool crossed=observed && previous.y>.025f && local.y<0;
            if(crossed)impact=Vector3.Lerp(previous,local,previous.y/(previous.y-local.y));
            return (local.y>=-.06f || crossed) && Mathf.Abs(impact.x)<.13f && Mathf.Abs(impact.z)<.10f
                && Vector3.Dot(tool.right,Vector3.down)>.55f;
        }
        private bool TrackedHandMotion(Vector3 palm,Quaternion palmRotation,bool held,out Vector2 point)
        {
            point=_motion;
            if(!held){_ready=true;_calibrated=_trackedToolHeld=_hasTrackedContact=false;return false;}
            if(!_ready)return false;
            if(!_trackedToolHeld)
            {
                if(Vector3.Distance(palm,HandlePoint)>=.22f)return false;
                _trackedToolRotation=Quaternion.Inverse(palmRotation)*_tools[_index].rotation;
                _trackedToolHeld=true;_hasTrackedContact=false;
            }
            // Workpiece contact controls progress only. The grip owns attachment until it is released.
            return _index==0 || TrackedToolMotion(palm,palmRotation,out point);
        }
        public bool Tick(Keyboard keyboard,Mouse mouse,bool focused,bool xr)
        {
            bool worldGrip=xr && Grip(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));
            bool grabAvailable=xr && focused && !Active && _c!=null && _c.Ashore && !_c.Paused && !_c.StartMenuOpen
                && !_c.StoryBlocksGameplay && !_c.ShopBlocksGameplay && !_c.InventoryOpen && !_c.JournalOpen
                && !_c.WorldActionBlocksGameplay && !_c.HasPendingCatch && !_c.XRNotebook.BlocksGameplay
                && !_c.XRSeat.HeadBlocked && !_c.Hands.HoldingRod && !_c.Shore.Swimming && !_c.Shore.Climbing
                && _c.Hands.TryControllerPalm(false,out _,out _);
            bool grabPressed=_worldGrip.Step(grabAvailable,worldGrip);
            if(!Active && (!xr || !grabPressed || !TryBeginXRGrab(focused)))return false;
            if(!focused || _c.Paused || _c.StartMenuOpen || _career!=_c.Career || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing
                || Vector3.Distance(_c.Shore.Position,_stations[_index].position)>(xr?1.5f:1.15f)){Cancel();return true;}
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame){Cancel();return true;}
            bool held=false,exit=false,tracked=true,contact=true;Vector2 point=_mouse;InputDevice device=default;
            if(xr)
            {
                device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);held=Grip(device);device.TryGetFeatureValue(CommonUsages.secondaryButton,out exit);
                device.TryGetFeatureValue(CommonUsages.primaryButton,out bool recipe);if(recipe && !_recipeHeld && _index==3)SwitchForgeOutput();_recipeHeld=recipe;
                // Entering a modal resets locomotion readiness; this modal owns its own release-to-grip guard.
                tracked=Application.isFocused && !_c.XRSeat.HeadBlocked && !_c.XRNotebook.IsOpen && _c.Hands.TryControllerPalm(false,out _,out _);
                if(tracked)
                {
                    _c.Hands.TryControllerPalm(false,out var palm,out var palmRotation);
                    contact=TrackedHandMotion(palm,palmRotation,held,out point);
                    if(_index==0 && _trackedToolHeld)
                    {
                        var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                        var nativeHeadRotation=Quaternion.identity;
                        tracked=device.TryGetFeatureValue(CommonUsages.devicePosition,out var nativePosition)
                            && head.TryGetFeatureValue(CommonUsages.deviceRotation,out nativeHeadRotation);
                        if(tracked)
                        {
                            if(!_calibrated)_nativeMotionFrame=Quaternion.Euler(0,nativeHeadRotation.eulerAngles.y,0);
                            point=SieveMotionPoint(nativePosition,Vector3.zero,_nativeMotionFrame);
                            if(!_calibrated){_origin=point-new Vector2(.1f,0);_calibrated=true;}point-=_origin;
                        }
                    }
                }
                if(exit && !_exitHeld){Cancel();return true;}_exitHeld=exit;
            }
            else if(mouse!=null)
            {
                held=mouse.leftButton.isPressed;
                if(held && _ready)
                {
                    _mouse=Vector2.ClampMagnitude(_mouse+mouse.delta.ReadValue()*.0007f,.22f);
                    point=_index==2?new Vector2(.1f+Mathf.Clamp(_mouse.y,-.09f,.09f),0):_index==3?new Vector2(0,Mathf.Clamp(_mouse.y,0,.18f)):_mouse;
                }
            }
            _motionSpeed=0;
            if(!tracked){_ready=_calibrated=_trackedToolHeld=_hasTrackedContact=false;_task.Release();}
            else if(!held){_ready=true;_task.Release();_calibrated=_trackedToolHeld=_hasTrackedContact=false;if(xr && _index>=2)_motion=new Vector2(.1f,0);}
            else if(_ready && (!xr || _trackedToolHeld) && !_c.Automated)
            {
                _motionSpeed=Mathf.Clamp01((point-_motion).magnitude/Mathf.Max(.001f,Time.unscaledDeltaTime)*2);
                _motion=point;bool step=_task.SampleContact(point.x,point.y,Time.unscaledDeltaTime,contact);
                if(step && Time.unscaledTime>=_audioAt)
                {_audioAt=Time.unscaledTime+.9f;_c.WorldAudio?.Play(_index==3?"ForgeStrike":"HandcraftRub",_rightGrip.position,.28f);}
                if(step && Time.unscaledTime>=_hapticAt && device.isValid && device.TryGetHapticCapabilities(out var cap) && cap.supportsImpulse)
                {FishingXRInput.Pulse(device,Mathf.Clamp(.12f+_motionSpeed*.12f,.12f,.24f),.035f);_hapticAt=Time.unscaledTime+.16f;}
            }
            int percent=Mathf.RoundToInt(_task.Progress*100);
            int interactionState=!tracked || !_ready?0:!held || xr && !_trackedToolHeld?1:!contact?2:3;
            if(percent!=_lastPercent || interactionState!=_lastInteractionState)
            {
                _lastPercent=percent;_lastInteractionState=interactionState;
                _status.text=FishingXRText.Adapt((interactionState==0?"松开握把，对准桌上握柄再握住。":interactionState==1?"伸手靠近握柄，按住右握把拿起。":interactionState==2?(_index==1?"已握住摇柄 · 围绕轮轴画圈":_index==2?"锯子仍在手中 · 将锯齿放回木料":"锤子仍在手中 · 对准铁坯再敲击"):_index==1?"右握把握住摇柄 · 转三圈绕线":_index==3?"右握把抬锤 · 锤面落到铁坯":_index==2?"右握把握锯 · 沿木料前后推拉":"右握把 · 左右往复")
                    +"\n"+(_index==3?FishingBackpack.Name(_task.Output)+" · A切换配方\n":_index==1?"纤维×3 → 绳索×"+_task.OutputCount+"\n":"")+(_index==1?Count+" / 3 圈":_task.Strokes+" / "+_task.StrokeTarget+" 次")+"    B 收起");
            }
            if(_task.Complete && held && _ready && tracked && (!xr || _trackedToolHeld))
            {
                if(_task.Claim(_c.Career.Backpack)){_c.WorldAudio?.Play("CraftFinish",_rightGrip.position,.45f);_c.Save();_c.Notice(Name(_index)+"完成 · "+FishingBackpack.Name(_task.Output)+"×"+_task.OutputCount+"已收入背包。",6);Cancel();}
                else {_c.WorldAudio?.Play("UIReject",_c.ExplorerPosition,.3f);_c.Notice("背包空间或材料不足，本次没有扣除材料。",5);Cancel();}
            }
            return true;
        }
        private static bool Grip(InputDevice device)=>FishingXRInput.Grip(device);
        public void Present()
        {
            float dt=_visualFrame==Time.frameCount?0:Time.unscaledDeltaTime;_visualFrame=Time.frameCount;
            if(!Active){PresentThread();return;}
            _visualSpeed=Mathf.MoveTowards(_visualSpeed,_motionSpeed,dt*6);_shakePhase+=dt*18*_visualSpeed;
            float wave=Mathf.Sin(_shakePhase)*_visualSpeed;
            if(_index==0)
            {
                _tools[0].localPosition=_toolHome+new Vector3(-Mathf.Clamp(_motion.x-.1f,-.065f,.065f),wave*.005f,0);
                _tools[0].localRotation=Quaternion.Euler(wave*1.5f,0,wave*3);_sand.localScale=Vector3.one*Mathf.Lerp(1,.30f,_task.Progress);_sand.localPosition=Vector3.down*.016f*_task.Progress;
                for(int i=0;i<_shells.Length;i++)
                {
                    float a=i*2.399963f,r=.033f+(i%3)*.024f,bounce=Mathf.Sin(_shakePhase+i*1.7f);
                    _shells[i].localPosition=new Vector3(Mathf.Cos(a)*r+Mathf.Sin(_shakePhase*.8f+i)*.017f*_visualSpeed,.007f+Mathf.Abs(bounce)*.012f*_visualSpeed,Mathf.Sin(a)*r*.65f+Mathf.Cos(_shakePhase+i)*.009f*_visualSpeed);
                    _shells[i].localRotation=Quaternion.Euler(bounce*15*_visualSpeed,i*57+Mathf.Sin(_shakePhase+i)*25*_visualSpeed,bounce*12*_visualSpeed);
                }
                PresentSand(dt);
            }
            else if(_index==1)
            {
                _crank.localRotation=Quaternion.AngleAxis(_crankStart+_crankSign*Mathf.Atan2(_motion.y,_motion.x)*Mathf.Rad2Deg,Vector3.forward);_coil.localScale=Vector3.one*Mathf.Lerp(.76f,1.08f,_task.Progress);
                _feedCoil.localScale=Vector3.one*Mathf.Lerp(.65f,.38f,_task.Progress);PresentThread();
            }
            else if(!XRSettings.isDeviceActive || !_trackedToolHeld)
            {
                if(_index==2){_tools[2].localRotation=_toolRest;_tools[2].localPosition=_toolHome+Vector3.back*Mathf.Clamp(_motion.x-.1f,-.09f,.09f);}
                else
                {
                    float lift=Mathf.Clamp(_motion.y,0,.12f);
                    var rotation=Quaternion.AngleAxis(lift/.18f*28,Vector3.right)*_toolRest;
                    var gripLocal=Vector3.Scale(_rightGrip.localPosition,_tools[3].localScale);
                    var grip=_toolHome+_toolRest*gripLocal+Vector3.up*lift;
                    _tools[3].localRotation=rotation;_tools[3].localPosition=grip-rotation*gripLocal;
                }
            }
            if(XRSettings.isDeviceActive)
            {
                if(_index>=2){if(_trackedToolHeld)GripSurface(_c.Hands.Right,_rightGrip,_tools[_index],_index==2?Vector3.right:Vector3.up,_index==2?.016f:.025f);return;}
                if(Grip(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand)) && _c.Hands.TryControllerPalm(true,out var supportPalm,out _)
                    && Vector3.Distance(supportPalm,_leftGrip.position)<.25f)
                    GripSurface(_c.Hands.Left,_leftGrip,_index==1?_tools[1]:_tools[_index],_index==0?Vector3.forward:Vector3.right);
                if(_trackedToolHeld)GripSurface(_c.Hands.Right,_rightGrip,_index==1?_crank:_tools[_index],Vector3.forward);
                return;
            }
            // 开始时可能仍处于坐起或蹲起过渡，镜头必须继续跟随同源眼位。
            _cameraPosition=_c.Shore.Character.EyePosition;
            var target=_stations[_index].TransformPoint(new Vector3(0,1.08f,0));
            var workBounds=new Bounds(WorkPoint,Vector3.zero);
            if(_index>=2)
            {
                foreach(var renderer in _tools[_index].GetComponentsInChildren<Renderer>())workBounds.Encapsulate(renderer.bounds);
                workBounds.Encapsulate(_leftGrip.position);target=workBounds.center;
            }
            float viewBlend=Mathf.SmoothStep(0,1,(Time.unscaledTime-_started)/.25f);
            _c.viewCamera.transform.SetPositionAndRotation(_cameraPosition,Quaternion.Slerp(_cameraRotation,Quaternion.LookRotation(target-_cameraPosition),viewBlend));
            if(_index>=2)
            {
                float tangent=0;
                for(int corner=0;corner<8;corner++)
                {
                    var p=_c.viewCamera.transform.InverseTransformPoint(workBounds.center+Vector3.Scale(workBounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                    tangent=Mathf.Max(tangent,Mathf.Max(Mathf.Abs(p.y),Mathf.Abs(p.x)/_c.viewCamera.aspect)/Mathf.Max(.1f,p.z));
                }
                _c.viewCamera.fieldOfView=Mathf.Lerp(_cameraFov,Mathf.Clamp(2*Mathf.Atan(tangent)*Mathf.Rad2Deg+6,60,80),viewBlend);
            }
            if(_index>=2)
            {
                var frame=_stations[_index].rotation;
                _c.Hands.Left.SetPose(_leftGrip.position,frame*Quaternion.LookRotation(Vector3.back,Vector3.up),.12f);
                var rotation=frame*Quaternion.LookRotation(_index==2?Vector3.back:Vector3.down,Vector3.left);
                if(_index==3)rotation=Quaternion.AngleAxis(Mathf.Clamp(_motion.y,0,.12f)/.18f*28,_stations[3].right)*rotation;
                _c.Hands.Right.SetPose(_rightGrip.position+rotation*new Vector3(0,.026f,-.01f),rotation,.82f);
                GripSurface(_c.Hands.Right,_rightGrip,_tools[_index],_index==2?Vector3.right:Vector3.up,_index==2?.016f:.025f);
                _c.Hands.Left.SetForearmRoll(.65f);_c.Hands.Right.SetForearmRoll(.65f);
                _c.Hands.Left.FollowAuthoredMotion();_c.Hands.Right.FollowAuthoredMotion();return;
            }
            PresentGrip(true,_leftGrip,_index==1?_tools[1]:_tools[_index],_index==0?Vector3.forward:Vector3.right);
            PresentGrip(false,_rightGrip,_index==1?_crank:_tools[_index],Vector3.forward);
            _c.Hands.Left.SetForearmRoll(.65f);_c.Hands.Right.SetForearmRoll(.65f);
            _c.Hands.Left.FollowAuthoredMotion();_c.Hands.Right.FollowAuthoredMotion();
        }
        private void PresentGrip(bool left,Transform grip,Transform tool,Vector3 localAxis)
        {
            var axis=tool.TransformDirection(localAxis).normalized;
            var inward=Vector3.Cross(axis,Vector3.up).normalized;
            if(Vector3.Dot(inward,tool.position-grip.position)<0)inward=-inward;
            if(_index==1)inward=_c.Shore.Actor.forward;
            else inward=(inward+_c.Shore.Actor.forward).normalized;
            var rotation=Quaternion.LookRotation(inward,Vector3.up);var target=left?_c.Hands.Left:_c.Hands.Right;
            target.SetPose(grip.position+rotation*(_index>=2?new Vector3(0,.012f,-.022f):new Vector3(0,.029f,-.055f)),rotation,.8f);
            target.SetFingers(new FishingFingerPose(.70f,.78f,.82f,.88f,.65f));GripSurface(target,grip,tool,localAxis);
        }
        private static void GripSurface(FishingHandTarget target,Transform grip,Transform tool,Vector3 localAxis,float radius=.009f)
        {
            if(!target.Visible)return;
            var axis=tool.TransformDirection(localAxis).normalized*.035f;
            target.SetGripSurface(grip.position-axis,grip.position+axis,radius);
        }
        private void PresentSand(float dt)
        {
            _caughtSand.gameObject.SetActive(_task.Progress>0);_caughtSand.localScale=new Vector3(1,Mathf.Lerp(.12f,1,_task.Progress),1)*Mathf.Sqrt(Mathf.Max(.001f,_task.Progress));
            _emission+=dt*45*_visualSpeed;
            while(_emission>=1)
            {
                _emission-=1;int i=_nextGrain++%_grains.Length;float a=i*2.399963f;
                _grainStart[i]=_stations[0].InverseTransformPoint(_tools[0].TransformPoint(new Vector3(Mathf.Cos(a)*.10f,-.02f,Mathf.Sin(a)*.055f)));
                _grainAge[i]=0;_grains[i].gameObject.SetActive(true);
            }
            for(int i=0;i<_grains.Length;i++)
            {
                if(_grainAge[i]<0)continue;float age=_grainAge[i]+=dt;
                var p=_grainStart[i]+new Vector3(Mathf.Sin(i)*age*.025f,-.10f*age-1.6f*age*age,0);
                if(p.y<=.981f){_grainAge[i]=-1;_grains[i].gameObject.SetActive(false);}else _grains[i].localPosition=p;
            }
        }
        private void PresentThread()
        {
            if(_thread==null)return;
            var start=_feedCoil.TransformPoint(new Vector3(0,.03f,0));var end=_coil.TransformPoint(new Vector3(-.028f,.03f,0));
            for(int i=0;i<12;i++)
            {
                float t=i/11f;var p=Vector3.Lerp(start,end,t);p.y-=Mathf.Sin(t*Mathf.PI)*(.016f+.005f*Mathf.Sin(_shakePhase+t*8));_thread.SetPosition(i,p);
            }
        }
        public void Cancel()
        {
            if(!Active)return;
            _tools[_index].localPosition=_toolHome;_tools[_index].localRotation=_toolRest;
            if(_c.DesktopDisplay && _c.viewCamera!=null)_c.viewCamera.fieldOfView=_cameraFov;
            _task=null;_career=null;_trackedToolHeld=_hasTrackedContact=false;_canvas.gameObject.SetActive(false);_sand.localScale=Vector3.one;_sand.localPosition=Vector3.zero;_coil.localScale=Vector3.one;_crank.localRotation=Quaternion.Euler(0,0,180);
            ResetShells();_caughtSand.gameObject.SetActive(false);_feedCoil.localScale=Vector3.one*.65f;_visualSpeed=_motionSpeed=0;
            for(int i=0;i<_grains.Length;i++){_grainAge[i]=-1;_grains[i].gameObject.SetActive(false);}
            _c.Shore.Character.InteractionLean=Vector3.zero;_c.StoryModalChanged();
            _c.Shore.Character.SetTabletopView(false);
            _c.Shore.Character.SetOpticalView(false);
            foreach(var entry in _homeLabels)if(entry.Key!=null)entry.Key.enabled=entry.Value;_homeLabels.Clear();
        }
        private void LateUpdate()
        {
            if(_c==null)return;
            bool desktop=_c.DesktopDisplay;
            if(_desktopLabels!=desktop)
            {
                _desktopLabels=desktop;
                for(int i=0;i<4;i++)_signs[i].text=Name(i)+"\n"+StartHint(desktop);
            }
            for(int i=0;i<4;i++)
            {
                bool visible=(_c.ExplorerPosition-_stations[i].position).sqrMagnitude<9*9;
                _signs[i].gameObject.SetActive(visible && !Active);
                _lamps[i].enabled=Active && _index==i;
                if(visible)_signs[i].transform.rotation=Quaternion.LookRotation(_signs[i].transform.position-_c.viewCamera.transform.position);
            }
        }
        private void OnDisable()=>Cancel();
        private void OnDestroy()
        {
            if(_grainMesh!=null)Destroy(_grainMesh);if(_toolMaterial!=null)Destroy(_toolMaterial);
            if(_sandMaterial!=null)Destroy(_sandMaterial);if(_shellMaterial!=null)Destroy(_shellMaterial);if(_grainMaterial!=null)Destroy(_grainMaterial);
        }
    }
}
