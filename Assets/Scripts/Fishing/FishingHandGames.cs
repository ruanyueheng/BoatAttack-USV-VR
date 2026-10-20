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
        private readonly Transform[] _stations=new Transform[2],_tools=new Transform[2];
        private readonly TextMesh[] _signs=new TextMesh[2];
        private readonly Light[] _lamps=new Light[2];
        private readonly Dictionary<Renderer,bool> _homeLabels=new Dictionary<Renderer,bool>();
        private Material _material,_sandMaterial,_shellMaterial,_grainMaterial;
        private Transform _crank,_coil,_sand,_tray,_caughtSand,_feedCoil;
        private readonly Transform[] _shells=new Transform[6],_grains=new Transform[32];
        private readonly float[] _grainAge=new float[32];
        private readonly Vector3[] _grainStart=new Vector3[32];
        private Mesh _grainMesh;
        private LineRenderer _thread;
        private float _shakePhase,_visualSpeed,_emission;
        private int _visualFrame=-1,_nextGrain;
        private Transform _sieveGripA,_sieveGripB,_leftGrip,_rightGrip,_supportA,_supportB,_winderHandle;
        private float _crankStart,_crankSign;
        private Canvas _canvas;
        private Text _title,_status;
        private FishingHandTask _task;
        private FishingCareer _career;
        private Vector2 _mouse,_origin,_motion;
        private Vector3 _toolHome,_cameraPosition;
        private Quaternion _frame,_cameraRotation;
        private bool _ready,_calibrated,_exitHeld,_desktopLabels;
        private int _index,_lastPercent=-1;
        private float _started;
        private float _motionSpeed;
        public bool Active=>_task!=null;
        public FishingHandTask Task=>_task;
        public Transform Station(int index)=>_stations[index];
        public Vector3 Approach(int index)=>_stations[index].TransformPoint(new Vector3(0,0,.60f));
        public Transform ActiveTool=>!Active?null:_index==0?_tools[0]:_crank;
        public Vector3 HandlePoint=>!Active?Vector3.zero:_rightGrip.position;
        public static string Name(int index)=>index==0?"潮汐摇筛":"手摇绕绳";
        public static string StartHint(bool desktop)=>desktop?"E 动手试试":"右手柄 A 动手试试";
        public string Instruction=>_index==0?"按住鼠标左键，轻轻左右摇筛":"按住鼠标左键，沿摇柄画圈绕线";
        public int Count=>_task==null?0:_index==0?_task.Strokes:Mathf.FloorToInt(_task.Progress*3);
        public void Build(FishingPrototype c)
        {
            _c=c;_material=Resources.Load<Material>("FishingV034/Handcraft");
            if(_material==null)throw new System.InvalidOperationException("Prepare V034 handcraft art before opening the scene.");
            _sandMaterial=new Material(_material);_sandMaterial.SetColor("_BaseColor",new Color(.90f,.79f,.60f));_sandMaterial.SetFloat("_Smoothness",.25f);
            _shellMaterial=new Material(Resources.Load<Material>("FishingV035/Coastal"));_shellMaterial.SetColor("_BaseColor",new Color(1,.87f,.82f));
            _grainMaterial=new Material(c.PaletteShader){name="Warm sifted sand"};_grainMaterial.SetColor("_BaseColor",new Color(.67f,.45f,.23f));_grainMaterial.SetFloat("_Shadows",1);
            for(int i=0;i<2;i++)
            {
                var root=new GameObject(Name(i)).transform;root.SetParent(transform,false);
                var seed=c.Home.Origin+new Vector3(i==0?-3.5f:3.5f,.2f,-6.2f);
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
                else
                {
                    _tools[1]=Model("WinderBody",root);_tools[1].localPosition=new Vector3(0,.958f,0);
                    _tools[1].localScale=Vector3.one*1.6f;_tools[1].localRotation=Quaternion.Euler(0,-55,0);
                    _crank=Model("WinderCrank",_tools[1]);_crank.localPosition=new Vector3(0,.134f,.035f);_crank.localScale=Vector3.one*.8f;
                    var wheelMesh=_crank.GetChild(0);wheelMesh.localRotation=Quaternion.Euler(0,180,0)*wheelMesh.localRotation;
                    _coil=Model("WinderCoil",_crank);_crank.localRotation=Quaternion.Euler(0,0,180);
                    _supportA=Marker(_tools[1],"WinderSupportA");_supportB=Marker(_tools[1],"WinderSupportB");_winderHandle=Marker(_crank,"WinderHandle");
                    _feedCoil=Model("WinderCoil",root);_feedCoil.localPosition=new Vector3(-.25f,.99f,-.07f);_feedCoil.localScale=Vector3.one*.65f;
                    _thread=FishingArt.Line("Flax feeding into spindle",root,c.cream,.002f,false);_thread.positionCount=12;_thread.useWorldSpace=true;
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
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=folder=="FishingV034/"?_material:Resources.Load<Material>(folder+"Coastal");
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
            for(int i=0;i<2;i++)
            {
                float distance=Vector3.Distance(_c.Shore.Position,_stations[i].position);var target=_tools[i].position;
                if(distance<best && Vector3.Dot(_c.viewCamera.transform.forward,(target-_c.viewCamera.transform.position).normalized)>.2f
                    && _c.Shore.ClearSight(_c.Shore.Position+Vector3.up*_c.Shore.EyeHeight,target))
                {best=distance;index=i;root=_stations[i];}
            }
            return index>=0;
        }
        public bool Begin(int index)
        {
            if(Active || index<0 || index>1 || _c.Paused || _c.StoryBlocksGameplay || _c.ShopBlocksGameplay || _c.HasPendingCatch || _c.WorldActionBusy
                || _c.InventoryOpen || _c.JournalOpen || _c.XRNotebook.BlocksGameplay || !TryTarget(out int nearby,out _) || nearby!=index)return false;
            if(FishingPrototype.FlatDistance(_c.Shore.Position,_stations[index].position)>.67f)
            {_c.Notice("再靠近一点，站到桌沿旁就能动手了。",4);return false;}
            var task=new FishingHandTask((FishingHandTaskKind)index);
            if(!task.HasMaterials(_c.Career.Backpack)){_c.Notice(Name(index)+"需要"+(index==0?"砂×2":"植物纤维×3")+"，完成前不会扣除材料。",5);return false;}
            if(_c.DesktopDisplay)
            {
                _c.Shore.SetCrouch(false);
                if(_c.Shore.Crouched){_c.Notice("站起身后再开始桌边手作。",4);return false;}
            }
            _index=index;_task=task;_career=_c.Career;_ready=_calibrated=false;_exitHeld=true;_mouse=_motion=new Vector2(.1f,0);
            _motionSpeed=_visualSpeed=_shakePhase=_emission=0;
            _frame=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0);_toolHome=_tools[index].localPosition;
            if(index==0)
            {
                bool aIsRight=Vector3.Dot(_sieveGripA.position-_sieveGripB.position,_frame*Vector3.right)>0;
                _leftGrip=aIsRight?_sieveGripB:_sieveGripA;_rightGrip=aIsRight?_sieveGripA:_sieveGripB;
            }
            else
            {
                _leftGrip=Vector3.Dot(_supportA.position-_supportB.position,_frame*Vector3.right)<0?_supportA:_supportB;_rightGrip=_winderHandle;
                _crank.localRotation=Quaternion.identity;_crankStart=Vector3.Dot(_winderHandle.position-_crank.position,_frame*Vector3.right)>0?0:180;
                _crank.localRotation=Quaternion.Euler(0,0,_crankStart);var before=_winderHandle.position;
                _crank.localRotation=Quaternion.Euler(0,0,_crankStart+5);_crankSign=Vector3.Dot(_winderHandle.position-before,Vector3.up)>0?1:-1;
                _crank.localRotation=Quaternion.Euler(0,0,_crankStart);
            }
            _cameraPosition=_c.DesktopDisplay?_c.Shore.Character.EyePosition:_c.viewCamera.transform.position;_cameraRotation=_c.viewCamera.transform.rotation;_started=Time.unscaledTime;_lastPercent=-1;
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
        public bool Tick(Keyboard keyboard,Mouse mouse,bool focused,bool xr)
        {
            if(!Active)return false;
            if(!focused || _c.Paused || _c.StartMenuOpen || _career!=_c.Career || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing
                || Vector3.Distance(_c.Shore.Position,_stations[_index].position)>1.15f){Cancel();return true;}
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame){Cancel();return true;}
            bool held=false,exit=false,tracked=true;Vector2 point=_mouse;InputDevice device=default;
            if(xr)
            {
                device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);held=Grip(device);device.TryGetFeatureValue(CommonUsages.secondaryButton,out exit);
                tracked=Application.isFocused && !_c.XRSeat.HeadBlocked && _c.Hands.TryControllerPalm(false,out _,out _);
                if(tracked)
                {
                    _c.Hands.TryControllerPalm(false,out var palm,out _);
                    tracked=Vector3.Distance(palm,HandlePoint)<.30f && Vector3.Distance(palm,_c.viewCamera.transform.position)>.32f;
                    var relative=Quaternion.Inverse(_frame)*(palm-_c.viewCamera.transform.position);point=new Vector2(relative.x,relative.y);
                    if(_index==1)
                    {
                        var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);bool leftHeld=Grip(left);
                        var lp=Vector3.zero;tracked=tracked && leftHeld && _c.Hands.TryControllerPalm(true,out lp,out _)
                            && Vector3.Distance(lp,_leftGrip.position)<.25f;
                        if(tracked)
                        {
                            relative=palm-_crank.position;
                            point=new Vector2(Vector3.Dot(relative,_tools[1].right),Vector3.Dot(relative,_tools[1].up));
                        }
                    }
                    if(_index==0)
                    {
                        if(!_calibrated){_origin=point-new Vector2(.1f,0);_calibrated=true;}point-=_origin;
                    }
                }
                if(exit && !_exitHeld){Cancel();return true;}_exitHeld=exit;
            }
            else if(mouse!=null)
            {
                held=mouse.leftButton.isPressed;
                if(held && _ready){_mouse=Vector2.ClampMagnitude(_mouse+mouse.delta.ReadValue()*.0007f,.22f);point=_mouse;}
            }
            _motionSpeed=0;
            if(!tracked){_ready=_calibrated=false;_task.Release();}
            else if(!held){_ready=true;_task.Release();_calibrated=false;}
            else if(_ready && !_c.Automated)
            {
                _motionSpeed=Mathf.Clamp01((point-_motion).magnitude/Mathf.Max(.001f,Time.unscaledDeltaTime)*2);
                _motion=point;bool step=_task.Sample(point.x,point.y,Time.unscaledDeltaTime);
                if(step && device.isValid && device.TryGetHapticCapabilities(out var cap) && cap.supportsImpulse)device.SendHapticImpulse(0,.22f,.035f);
            }
            int percent=Mathf.RoundToInt(_task.Progress*100);
            if(percent!=_lastPercent || !tracked)
            {
                _lastPercent=percent;_status.text=(!tracked?"松开握把，靠近桌上握柄后再握住。":_index==0?"右握把 · 小幅左右轻摇":"左握把扶稳 · 右握把画圈")
                    +"\n"+(_index==0?_task.Strokes+" / 12 次":Count+" / 3 圈")+"    B 收起";
            }
            if(_task.Complete && held && _ready && tracked)
            {
                if(_task.Claim(_c.Career.Backpack)){_c.Save();_c.Notice(Name(_index)+"完成 · "+(_index==0?"海贝":"绳索")+"×1已收入背包。",6);Cancel();}
                else {_c.Notice("背包空间或材料不足，本次没有扣除材料。",5);Cancel();}
            }
            return true;
        }
        private static bool Grip(InputDevice device)
        {
            if(device.TryGetFeatureValue(CommonUsages.grip,out float amount))return amount>=.55f;
            device.TryGetFeatureValue(CommonUsages.gripButton,out bool pressed);return pressed;
        }
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
            else
            {
                _crank.localRotation=Quaternion.AngleAxis(_crankStart+_crankSign*Mathf.Atan2(_motion.y,_motion.x)*Mathf.Rad2Deg,Vector3.forward);_coil.localScale=Vector3.one*Mathf.Lerp(.76f,1.08f,_task.Progress);
                _feedCoil.localScale=Vector3.one*Mathf.Lerp(.65f,.38f,_task.Progress);PresentThread();
            }
            if(XRSettings.isDeviceActive)return;
            // 开始时可能仍处于坐起或蹲起过渡，镜头必须继续跟随同源眼位。
            _cameraPosition=_c.Shore.Character.EyePosition;
            var target=_stations[_index].TransformPoint(new Vector3(0,1.08f,0));
            _c.viewCamera.transform.SetPositionAndRotation(_cameraPosition,Quaternion.Slerp(_cameraRotation,Quaternion.LookRotation(target-_cameraPosition),Mathf.SmoothStep(0,1,(Time.unscaledTime-_started)/.25f)));
            var facing=Quaternion.LookRotation(_c.Shore.Actor.forward,Vector3.up);
            _c.Hands.Left.SetPose(_leftGrip.position,facing,.5f);_c.Hands.Right.SetPose(HandlePoint,facing,.55f);
            _c.Hands.Left.SetForearmRoll(.65f);_c.Hands.Right.SetForearmRoll(.65f);
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
            _tools[_index].localPosition=_toolHome;_tools[_index].localRotation=_index==1?Quaternion.Euler(0,-55,0):Quaternion.identity;
            _task=null;_career=null;_canvas.gameObject.SetActive(false);_sand.localScale=Vector3.one;_sand.localPosition=Vector3.zero;_coil.localScale=Vector3.one;_crank.localRotation=Quaternion.Euler(0,0,180);
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
                for(int i=0;i<2;i++)_signs[i].text=Name(i)+"\n"+StartHint(desktop);
            }
            for(int i=0;i<2;i++)
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
            if(_grainMesh!=null)Destroy(_grainMesh);
            if(_sandMaterial!=null)Destroy(_sandMaterial);if(_shellMaterial!=null)Destroy(_shellMaterial);if(_grainMaterial!=null)Destroy(_grainMaterial);
        }
    }
}
