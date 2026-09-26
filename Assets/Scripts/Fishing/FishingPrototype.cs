using System;
using System.Collections.Generic;
using System.IO;
using BoatAttack.VR;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace BoatAttack.Fishing
{
    [DefaultExecutionOrder(100)]
    public sealed class FishingPrototype : MonoBehaviour
    {
        public Boat boatPrefab;
        public Camera viewCamera;
        public Vector3 spawnPosition;
        public Quaternion spawnRotation = Quaternion.identity;
        public Vector3 harbor;
        public Vector3[] fishingSpots = new Vector3[3];
        public Material[] fishMaterials;
        public Material wood, cream, dark, orange, teal, lineMaterial;
        public Boat Player { get; private set; }
        public FishingCareer Career { get; private set; }
        public CatchSession Session { get; } = new CatchSession();
        public bool Anchored { get; private set; }
        public bool Paused { get; private set; }
        public bool Automated { get; private set; }
        public bool AutomationReel { get; set; }
        public int Spot { get; private set; }
        public string Message { get; private set; }
        public string SavePath { get; private set; }
        public string[] SpotNames = { "珊瑚浅湾", "飞梭水道", "鼓鼓礁" };
        public string[] FishNames = { "珊瑚鱼", "飞梭鱼", "鼓鼓鱼" };
        public float Speed => Player == null ? 0f : Vector3.ProjectOnPlane(Player.engine.RB.velocity,Vector3.up).magnitude;
        public float HarborDistance => Player == null ? 999f : FlatDistance(Player.engine.RB.position,harbor);
        public bool AtHarbor => HarborDistance < 22f && Speed < 3f && !Anchored;
        public float SpotDistance { get; private set; }
        private USVInputRouter _input;
        private Vector3 _anchor, _cameraVelocity, _catchStart;
        private Quaternion _anchorRotation;
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<GameObject> _storedFish = new List<GameObject>();
        private GameObject _fish, _bobber;
        private Transform _rodTip, _net, _box;
        private LineRenderer _line;
        private float _netMotion, _caughtAt, _messageUntil;
        private CatchPhase _previousPhase;
        private AudioSource _feedback;
        private AudioClip _biteTone, _successTone;
        private bool _recorded, _initialized;
        private int _previousTargetFrameRate = -1;

        private void Start()
        {
            Application.runInBackground=true;
            _previousTargetFrameRate=Application.targetFrameRate;Application.targetFrameRate=60;
            Automated = Application.isBatchMode || HasArgument("-fishing-capture") || HasArgument("-fishing-autoplay");
            SavePath = Path.Combine(Automated ? Application.temporaryCachePath : Application.persistentDataPath,
                Automated ? "fishing-test-" + Guid.NewGuid().ToString("N") + ".json" : "boatattack-fishing-v1.json");
            Career = FishingSaveStore.Load(SavePath, out var warning);
            if (viewCamera == null) throw new InvalidOperationException("Fishing scene camera is missing; regenerate the prototype scene.");
            foreach (var brain in FindObjectsOfType<CinemachineBrain>()) brain.enabled=false;
            foreach (var replay in FindObjectsOfType<ReplayCamera>()) replay.enabled=false;
            Player=Instantiate(boatPrefab,spawnPosition,spawnRotation);
            Player.name="Fishing player boat";Player.ParticipateInRaces=false;
            Player.Setup(1,true,new BoatLivery {primaryColor=new Color(0.05f,0.65f,0.65f),trimColor=new Color(1f,0.58f,0.14f)});
            // Exactly one driver applies forces: fishing mode uses the shared input router.
            Player.GetComponent<HumanController>().enabled=false;
            if(Player.cam!=null)Player.cam.gameObject.SetActive(false);
            _input=USVInputRouter.EnsureInstance();
            AppSettings.MainCamera=viewCamera;
            viewCamera.fieldOfView=58f;
            viewCamera.transform.SetPositionAndRotation(spawnPosition-spawnRotation*Vector3.forward*12f+Vector3.up*7f,
                Quaternion.LookRotation(spawnRotation*Vector3.forward*16f-Vector3.up*5f));
            BuildWorld(); BuildEquipment();
            _feedback=gameObject.AddComponent<AudioSource>();_feedback.spatialBlend=0f;_feedback.volume=0.45f;
            _biteTone=Tone(780f,0.13f);_successTone=Tone(1150f,0.2f);
            gameObject.AddComponent<FishingHUD>().Controller=this;
            Notice(string.IsNullOrEmpty(warning)?"欢迎船长！先驶向黄色浮标，在渔区内减速后按 E 停船。":warning,10f);
            _initialized=true;
            if(HasArgument("-fishing-autoplay") || HasArgument("-fishing-capture")) gameObject.AddComponent<FishingAutoplay>().Controller=this;
            Debug.Log("[FISHING] Prototype initialized. Original procedural fish; desktop preview only.");
        }

        private void Update()
        {
            if(!_initialized)return;
            var k=Keyboard.current;var m=Mouse.current;var g=Gamepad.current;
            bool focused=Automated || Application.isFocused;
            if(!Automated && _input.Current.pause) { Paused=!Paused;Time.timeScale=Paused?0f:1f; }
            if(!Paused && focused && !Automated)
            {
                if(_input.Current.reset)ReturnToHarbor();
                if(k!=null && k.qKey.wasPressedThisFrame)LeaveStation();
                if(k!=null && k.uKey.wasPressedThisFrame)BuyUpgrade();
                if(_input.Current.interact)Interact();
                bool press=(m!=null && m.leftButton.wasPressedThisFrame) || (g!=null && g.rightShoulder.wasPressedThisFrame);
                if(press && Anchored){if(Session.Phase==CatchPhase.Bite)Hook();else Cast();}
            }
            UpdateNearestSpot();
            if(Anchored && !Paused && focused)
            {
                bool reel=Automated?AutomationReel:(m!=null && m.leftButton.isPressed)||(g!=null && g.rightTrigger.ReadValue()>0.5f);
                Session.Tick(Time.deltaTime,reel);
                if(Session.Phase!=_previousPhase)
                {
                    if(Session.Phase==CatchPhase.Bite)_feedback.PlayOneShot(_biteTone);
                    if(Session.Phase==CatchPhase.Escaped)Notice(Session.Failure,8f);
                    _previousPhase=Session.Phase;
                }
                StoreLandedCatch();
            }
            AnimateEquipment();
        }

        private void FixedUpdate()
        {
            if(!_initialized)return;
            var rb=Player.engine.RB;
            if(Anchored)
            {
                var p=rb.position;
                rb.AddForce(new Vector3(_anchor.x-p.x,0,_anchor.z-p.z)*3f-new Vector3(rb.velocity.x,0,rb.velocity.z)*2f,ForceMode.Acceleration);
                // Hold yaw only; vertical water response remains simulated.
                var yaw=Quaternion.Euler(0,_anchorRotation.eulerAngles.y,0);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation,yaw,Time.fixedDeltaTime*2f));
                return;
            }
            var state=_input.Current;
            Player.engine.Accelerate(Mathf.Max(0f,state.throttle));Player.engine.Turn(state.steering);
            float braking=Mathf.Max(state.brake,Mathf.Max(0f,-state.throttle));
            if(braking>0f)
            {
                var v=rb.velocity;var h=Vector3.MoveTowards(new Vector3(v.x,0,v.z),Vector3.zero,7f*braking*Time.fixedDeltaTime);
                rb.velocity=new Vector3(h.x,v.y,h.z);
            }
        }

        private void LateUpdate()
        {
            if(!_initialized)return;
            var t=Player.transform;
            var yaw=Quaternion.Euler(0,t.eulerAngles.y,0);
            Vector3 position,look;
            if(Anchored)
            {
                position=t.position+yaw*new Vector3(-6.3f,7.0f,-5.0f);
                look=t.position+yaw*new Vector3(2.7f,0.8f,1.0f);
            }
            else if(HarborDistance<25f)
            {
                position=t.position+yaw*new Vector3(-5,9,-13);
                look=Vector3.Lerp(t.position,harbor,0.5f)+Vector3.up;
            }
            else
            {
                position=t.position+yaw*new Vector3(0,7.5f,-12.5f);
                look=t.position+yaw*new Vector3(0,0.7f,5f);
            }
            viewCamera.transform.position=Vector3.SmoothDamp(viewCamera.transform.position,position,ref _cameraVelocity,0.35f);
            viewCamera.transform.rotation=Quaternion.Slerp(viewCamera.transform.rotation,Quaternion.LookRotation(look-viewCamera.transform.position),Time.unscaledDeltaTime*5f);
        }

        public void Interact()
        {
            if(Paused)return;
            if(Anchored){if(Session.Phase==CatchPhase.Landing)Scoop();return;}
            if(AtHarbor){Sell();return;}
            if(SpotDistance>22f){Notice("先驶入任一黄色渔区，再按 E 停船。");return;}
            if(Speed>3f){Notice("速度过快，按住 Shift 刹车再停船。");return;}
            Anchored=true;_anchor=Player.engine.RB.position;_anchorRotation=Player.transform.rotation;
            var v=Player.engine.RB.velocity;Player.engine.RB.velocity=new Vector3(0,v.y,0);
            Session.Cancel();_previousPhase=CatchPhase.Idle;
            Notice("已停船。点击左键抛竿；Q 收起工具继续航行。",7f);
        }
        public bool Cast()
        {
            if(!Anchored || Paused)return false;
            if(Career.Count>=FishingCareer.Capacity){Notice("鱼箱已满！按 Q 起航，返港按 E 出售。");return false;}
            bool cast=Session.Cast((FishKind)Spot,UnityEngine.Random.value<0.12f,Career.RodLevel,Guid.NewGuid().ToString("N"));
            if(cast){_recorded=false;_previousPhase=CatchPhase.Waiting;_fish.GetComponentInChildren<MeshRenderer>().sharedMaterial=Session.Rare?orange:fishMaterials[Spot];}
            return cast;
        }
        public bool Hook()=>Anchored && !Paused && Session.Hook();
        public bool Scoop()
        {
            if(!Anchored || Paused)return false;
            _netMotion=1f;
            bool success=Session.Land();
            // Commit immediately, before another input in this frame can cast/cancel.
            if(success) StoreLandedCatch();
            else Notice("抄网落空！等指针进入绿色区再按 E。",2f);
            return success;
        }
        private void StoreLandedCatch()
        {
            if(Session.Phase!=CatchPhase.Caught || _recorded)return;
            var record=Session.Claim();
            if(record==null || !Career.Store(record))return;
            _recorded=true;_caughtAt=Time.time;_catchStart=_fish.transform.position;
            RefreshHold();Save();_feedback.PlayOneShot(_successTone);
            Notice((record.rare?"稀有！":"捕获成功！")+FishNames[(int)record.kind]+" 已入箱，价值 "+record.value+" 金币。",6f);
        }
        public void LeaveStation()
        {
            if(Paused)return;Anchored=false;Session.Cancel();AutomationReel=false;Notice("工具已收起，W 前进，A/D 转向，S/Shift 刹车；青色港口按 E 出售。");
        }
        public int Sell()
        {
            if(!AtHarbor || Paused)return 0;
            int sold=Career.Sell();if(sold>0){Save();RefreshHold();_feedback.PlayOneShot(_successTone);Notice("出售成功 +"+sold+" 金币！在港口按 U 升级鱼线（30 金币）。",7f);}
            else Notice("鱼箱还是空的。前往黄色渔区捕捞，带回这里出售。");return sold;
        }
        public bool BuyUpgrade()
        {
            if(!AtHarbor || Paused){Notice("请先回到青色港口，再按 U 升级。");return false;}
            bool ok=Career.Upgrade();if(ok){Save();Notice("轻量鱼线已升级！收线时张力增长降低 28%。",8f);}
            else Notice(Career.RodLevel==1?"本次试玩的鱼线升级已完成。":"需要 30 金币。先捕鱼、返港出售吧。");return ok;
        }
        public void ReturnToHarbor()
        {
            if(Paused)return;LeaveStation();Teleport(spawnPosition,spawnRotation);Notice("已救援回港；鱼箱和金币保留。",5f);
        }
        public void MoveToSpotForAutomation(int index)
        {
            if(!Automated)throw new InvalidOperationException("Automation-only helper.");
            Teleport(fishingSpots[index]+Vector3.up,spawnRotation);UpdateNearestSpot();
        }
        private void Teleport(Vector3 p,Quaternion q){var rb=Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;Player.transform.SetPositionAndRotation(p,q);rb.position=p;rb.rotation=q;Physics.SyncTransforms();}
        private void UpdateNearestSpot()
        {
            if(Anchored)return;SpotDistance=float.MaxValue;
            for(int i=0;i<fishingSpots.Length;i++){float d=FlatDistance(Player.engine.RB.position,fishingSpots[i]);if(d<SpotDistance){SpotDistance=d;Spot=i;}}
        }
        private void Save()
        {
            try{FishingSaveStore.Save(SavePath,Career);}
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException){Notice("存档失败：请检查磁盘权限。当前进度仍保留在内存。",10f);Debug.LogWarning("[FISHING] Save failed: "+ex.Message);}
        }
        public void Notice(string text,float seconds=5f){Message=text;_messageUntil=Time.unscaledTime+seconds;}
        public string VisibleMessage=>Time.unscaledTime<_messageUntil?Message:string.Empty;
        public static float FlatDistance(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        public static bool HasArgument(string arg)=>Array.IndexOf(Environment.GetCommandLineArgs(),arg)>=0;

        private void BuildWorld()
        {
            for(int i=0;i<fishingSpots.Length;i++)
            {
                FishingArt.Ring("Fishing area "+i,transform,new Vector3(fishingSpots[i].x,0.22f,fishingSpots[i].z),9f,orange);
                for(int j=0;j<4;j++)
                {
                    var pos=fishingSpots[i]+Quaternion.Euler(0,j*90,0)*new Vector3(9,0,0);pos.y=0.25f;
                    FishingArt.Shape("Marker buoy",PrimitiveType.Sphere,transform,pos,new Vector3(0.7f,0.5f,0.7f),orange);
                }
            }
            FishingArt.Ring("Harbor safe zone",transform,new Vector3(harbor.x,0.25f,harbor.z),11f,teal);
            var dock=new GameObject("Original prototype fish market").transform;dock.SetParent(transform);dock.position=harbor;
            FishingArt.Shape("Floating deck",PrimitiveType.Cube,dock,new Vector3(0,0.5f,0),new Vector3(8,0.5f,6),wood,true);
            foreach(float x in new[]{-3f,3f})foreach(float z in new[]{-2f,2f})
                FishingArt.Shape("Market post",PrimitiveType.Cylinder,dock,new Vector3(x,2.1f,z),new Vector3(0.18f,2.1f,0.18f),cream);
            FishingArt.Shape("Orange canopy",PrimitiveType.Cube,dock,new Vector3(0,4.2f,0),new Vector3(7,0.25f,5),orange);
            FishingArt.Shape("Fish counter",PrimitiveType.Cube,dock,new Vector3(0,1.3f,1.4f),new Vector3(5,1.2f,1.2f),teal);
            for(int i=0;i<3;i++){var f=FishingArt.Fish(dock,fishMaterials[i],cream,dark,_owned);f.transform.localPosition=new Vector3(i*1.4f-1.4f,2.2f,1.4f);f.transform.localRotation=Quaternion.Euler(0,90,0);}
        }

        private void BuildEquipment()
        {
            var boat=Player.transform;
            _box=new GameObject("Catch crate").transform;_box.SetParent(boat,false);_box.localPosition=new Vector3(0,0.85f,-1.3f);
            FishingArt.Shape("Crate floor",PrimitiveType.Cube,_box,Vector3.zero,new Vector3(1.65f,0.1f,1.15f),wood);
            foreach(float side in new[]{-1f,1f})
            {
                FishingArt.Shape("Crate side",PrimitiveType.Cube,_box,new Vector3(side*0.8f,0.3f,0),new Vector3(0.1f,0.65f,1.15f),teal);
                FishingArt.Shape("Crate end",PrimitiveType.Cube,_box,new Vector3(0,0.3f,side*0.53f),new Vector3(1.65f,0.65f,0.1f),teal);
            }
            FishingArt.Pole("Fishing rod",boat,new Vector3(0.65f,0.8f,0.4f),new Vector3(2.5f,3.1f,1.2f),0.055f,dark);
            _rodTip=new GameObject("Rod tip").transform;_rodTip.SetParent(boat,false);_rodTip.localPosition=new Vector3(2.5f,3.1f,1.2f);
            FishingArt.Shape("Reel",PrimitiveType.Cylinder,boat,new Vector3(0.9f,1.25f,0.52f),new Vector3(0.24f,0.08f,0.24f),orange);
            _net=new GameObject("Landing net").transform;_net.SetParent(boat,false);_net.localPosition=new Vector3(1.2f,1.0f,0.3f);
            FishingArt.Pole("Net handle",_net,Vector3.zero,new Vector3(1.3f,0,0),0.045f,cream);
            var hoop=FishingArt.Line("Net hoop",_net,teal,0.04f,false);hoop.positionCount=25;
            for(int i=0;i<25;i++){float a=i*Mathf.PI*2f/24;hoop.SetPosition(i,new Vector3(1.8f+Mathf.Cos(a)*0.6f,0,Mathf.Sin(a)*0.6f));}
            for(int i=0;i<5;i++){var strand=FishingArt.Line("Net strand",_net,cream,0.01f,false);strand.positionCount=3;float a=i*Mathf.PI*2/5;strand.SetPositions(new[]{new Vector3(1.8f+Mathf.Cos(a)*0.55f,0,Mathf.Sin(a)*0.55f),new Vector3(1.8f,-0.5f,0),new Vector3(1.8f-Mathf.Cos(a)*0.55f,0,-Mathf.Sin(a)*0.55f)});}
            _fish=FishingArt.Fish(transform,fishMaterials[0],cream,dark,_owned);
            _bobber=FishingArt.Shape("Float",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*0.18f,orange);
            _line=FishingArt.Line("Fishing line",transform,lineMaterial,0.016f);_line.positionCount=17;
            RefreshHold();
        }
        private void RefreshHold()
        {
            foreach(var f in _storedFish)Destroy(f);_storedFish.Clear();
            int i=0;foreach(var record in Career.Inventory)
            {
                var f=FishingArt.Fish(_box,record.rare?orange:fishMaterials[(int)record.kind],cream,dark,_owned);
                f.transform.localPosition=new Vector3((i%3-1)*0.45f,0.28f,(i/3-0.5f)*0.4f);f.transform.localRotation=Quaternion.Euler(0,90,80);f.transform.localScale=Vector3.one*0.55f;_storedFish.Add(f);i++;
            }
        }
        private void AnimateEquipment()
        {
            var phase=Session.Phase;
            bool active=Anchored && phase!=CatchPhase.Idle && phase!=CatchPhase.Escaped;
            _line.enabled=active && phase!=CatchPhase.Caught;_bobber.SetActive(active && (phase==CatchPhase.Waiting || phase==CatchPhase.Bite));
            _fish.SetActive(active && (phase==CatchPhase.Fighting || phase==CatchPhase.Landing || (phase==CatchPhase.Caught && Time.time-_caughtAt<0.8f)));
            var t=Player.transform;var yaw=Quaternion.Euler(0,t.eulerAngles.y,0);
            float distance=phase==CatchPhase.Fighting?3f+Session.Distance*0.35f:5.5f;
            var target=t.position+yaw*new Vector3(distance,0,1.5f);target.y=phase==CatchPhase.Fighting?0.7f+Mathf.Abs(Mathf.Sin(Time.time*3f))*0.45f:0.35f+Mathf.Sin(Time.time*4f)*0.1f;
            if(phase==CatchPhase.Landing)target=t.position+yaw*new Vector3(3.4f,1.2f+Mathf.Abs(Mathf.Sin(Time.time*5f))*0.7f,1.2f);
            _bobber.transform.position=target;
            _fish.transform.position=phase==CatchPhase.Caught?Vector3.Lerp(_catchStart,_box.position+Vector3.up*0.5f,Mathf.Clamp01((Time.time-_caughtAt)/0.7f))+Vector3.up*Mathf.Sin(Mathf.Clamp01((Time.time-_caughtAt)/0.7f)*Mathf.PI)*0.8f:target;
            _fish.transform.rotation=yaw*Quaternion.Euler(Mathf.Sin(Time.time*7)*18f,-90+Mathf.Sin(Time.time*9)*22f,Mathf.Sin(Time.time*6)*14f);
            _fish.transform.localScale=Vector3.one*(Spot==2?1.5f:1.25f);
            for(int i=0;i<17;i++){float u=i/16f;_line.SetPosition(i,Vector3.Lerp(_rodTip.position,target,u)-Vector3.up*Mathf.Sin(u*Mathf.PI)*(phase==CatchPhase.Fighting?0.15f:0.5f));}
            _netMotion=Mathf.MoveTowards(_netMotion,0,Time.deltaTime*2.8f);_net.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(_netMotion*Mathf.PI)*35f);
        }
        private AudioClip Tone(float frequency,float seconds)
        {
            int samples=(int)(22050*seconds);var data=new float[samples];for(int i=0;i<samples;i++)data[i]=Mathf.Sin(i*frequency*Mathf.PI*2/22050f)*Mathf.Sin(i/(float)samples*Mathf.PI)*0.25f;
            var clip=AudioClip.Create("Original prototype feedback tone",samples,1,22050,false);clip.SetData(data,0);_owned.Add(clip);return clip;
        }
        private void OnDestroy()
        {
            if(Paused)Time.timeScale=1f;
            Application.targetFrameRate=_previousTargetFrameRate;
            foreach(var o in _owned)if(o!=null)Destroy(o);
        }
    }
}
