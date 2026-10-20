using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>One embodied player, aboard or ashore. Desktop only; never takes a tracked XR camera.</summary>
    public sealed partial class FishingShore : MonoBehaviour
    {
        public sealed class Site
        {
            public Vector3 Berth, Landing;
            public readonly Vector3[] Tasks = new Vector3[2];
            public bool Available;
        }
        public FishingPrototype Controller { get; private set; }
        public Site[] Sites { get; } = new Site[FishingExploration.SiteCount];
        public bool Ashore { get; private set; }
        public int CurrentSite { get; private set; } = -1;
        public bool ShopLanding { get; private set; }
        public bool HasSurveySite => Ashore && !FreeLanding && !ShopLanding && CurrentSite >= 0 && CurrentSite < Sites.Length;
        public CharacterController Body { get; private set; }
        public Transform Actor => Body.transform;
        public Vector3 Position => Ashore ? Actor.position : Controller.Player.transform.position;
        public int NearestSite { get; private set; }
        public float BerthDistance { get; private set; }
        public int NearbyTask { get; private set; } = -1;
        public float WalkSpeed { get; private set; }
        private FishingCharacter _character;

        private readonly Renderer[] _taskMarkers = new Renderer[8];
        private Vector3 _mooring, _lastSafe;
        private Quaternion _mooringRotation;
        private float _nextWarning;
        private readonly FishingLocomotion _motion = new FishingLocomotion();
        private readonly RaycastHit[] _groundHits = new RaycastHit[32];
        private readonly Collider[] _stepOverlaps = new Collider[32];
        private static readonly Vector3[] SeamOffsets = { Vector3.right,Vector3.left,Vector3.forward,Vector3.back,
            new Vector3(.7071f,0,.7071f),new Vector3(-.7071f,0,.7071f),new Vector3(.7071f,0,-.7071f),new Vector3(-.7071f,0,-.7071f) };
        private Transform _visual;
        public bool Crouched { get; private set; }
        public float EyeHeight => (Crouched?FishingLocomotion.CrouchingHeight:FishingLocomotion.StandingHeight)-.2f;
        public bool Grounded => Body!=null && Body.isGrounded;
        public void ResetMoveInput() => _motion.ResetInput();

        private static readonly Vector3[] LandSeeds = { new Vector3(-66,0,-14), new Vector3(42,0,70), new Vector3(94,0,-66), new Vector3(66,0,-94) };
        private static readonly Vector3[] Berths = { new Vector3(-78,0,-14), new Vector3(42,0,84), new Vector3(108,0,-66), new Vector3(66,0,-78) };
        private static readonly Vector3[] TaskSeeds = { new Vector3(-62,0,-14), new Vector3(-58,0,-22), new Vector3(38,0,66), new Vector3(46,0,66), new Vector3(90,1.6f,-64), new Vector3(92.8f,1.6f,-73), new Vector3(62,0,-94), new Vector3(70,0,-98) };

        public void Build(FishingPrototype c)
        {
            Controller = c;
            var actor = new GameObject("Explorer • physical character");
            actor.transform.SetParent(transform, false);
            Body = actor.AddComponent<CharacterController>();
            Body.height = 1.75f; Body.radius = .3f; Body.center = new Vector3(0,.875f,0);
            Body.slopeLimit = 46; Body.stepOffset = .32f; Body.skinWidth = .025f; Body.minMoveDistance = 0;
            Body.enabled = false;
            _visual=new GameObject("Explorer appearance").transform;_visual.SetParent(Actor,false);
            _character = _visual.gameObject.AddComponent<FishingCharacter>();
            _character.Build();
            // A rider's motor is disabled while seated. The hull remains the vessel's collision body.
            foreach (var hull in c.Player.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(Body,hull);
            var marketBarrier=c.transform.Find("Tide and Fin floating fish market/Boat building barrier");
            if(marketBarrier!=null)Physics.IgnoreCollision(Body,marketBarrier.GetComponent<Collider>());
            Physics.SyncTransforms();
            for (int i=0;i<Sites.Length;i++)
            {
                var site = Sites[i] = new Site { Berth = Berths[i] };
                site.Available = Ground(LandSeeds[i],out site.Landing) && Clearance(site.Landing);
                for (int t=0;t<2;t++) site.Available &= Ground(TaskSeeds[i*2+t],out site.Tasks[t]) && Clearance(site.Tasks[t]);
                if (!site.Available) { Debug.LogWarning("[SHORE] Unavailable landing: " + FishingExploration.SiteName(i)); continue; }
                var root = new GameObject(FishingExploration.SiteName(i)).transform; root.SetParent(transform,false);
                // A real collidable sloping jetty joins the water end to the original terrain.
                Vector3 inland=Vector3.ProjectOnPlane(site.Landing-site.Berth,Vector3.up).normalized;
                Vector3 start = site.Berth + Vector3.up * 1.05f, end = site.Landing + inland*1.2f + Vector3.up * .08f;
                var deck = FishingArt.Shape("Walkable landing jetty",PrimitiveType.Cube,root,(start+end)*.5f,new Vector3(2.2f,.18f,Vector3.Distance(start,end)),c.wood,true);
                deck.transform.rotation = Quaternion.LookRotation(end-start,Vector3.up);
                FishingArt.Shape("Landing pole",PrimitiveType.Cylinder,root,site.Berth+new Vector3(1.5f,2,0),new Vector3(.16f,2,.16f),c.cream);
                FishingArt.Shape("Amber landing flag",PrimitiveType.Cube,root,site.Berth+new Vector3(2,3.65f,0),new Vector3(1,.55f,.1f),c.orange);
                for (int t=0;t<2;t++)
                {
                    int id=i*2+t; var p=site.Tasks[t];
                    if(id==5)
                    {
                        var beacon=new GameObject("Old port salvage beacon");
                        beacon.transform.SetParent(root,false);
                        beacon.AddComponent<FishingBeaconVisual>().Build(c,p);
                        // Keep the historical survey marker separate from the new repair facts.
                        p+=new Vector3(.8f,0,0);
                    }
                    FishingArt.Shape("Survey tripod",PrimitiveType.Cylinder,root,p+Vector3.up*.5f,new Vector3(.14f,.5f,.14f),c.dark);
                    var marker=FishingArt.Shape(FishingExploration.TaskName(id),PrimitiveType.Cube,root,p+Vector3.up*1.1f,new Vector3(.42f,.28f,.07f),c.orange);
                    _taskMarkers[id]=marker.GetComponent<Renderer>();
                }
            }
            RefreshMarkers(); SyncRider(); UpdateProximity();
        }
        private GameObject Part(string name,Vector3 pos,Vector3 size,Material material) => FishingArt.Shape(name,PrimitiveType.Cube,_visual,pos,size,material);

        // Surveyed heights keep docks below awnings from being mistaken for roofs.
        public bool Ground(Vector3 seed,out Vector3 point) => GroundBelow(seed,seed.y>.6f?seed.y+.6f:80,seed.y>.6f?1.2f:80,out point);
        private bool GroundBelow(Vector3 seed,float originY,float distance,out Vector3 point)
        {
            point=seed;
            int count=Physics.RaycastNonAlloc(new Vector3(seed.x,originY,seed.z),Vector3.down,_groundHits,distance,~(1<<4),QueryTriggerInteraction.Ignore);
            if(count==_groundHits.Length)return false;
            float top=float.MinValue;bool found=false;
            for(int i=0;i<count;i++)
            {
                var h=_groundHits[i];
                if(Ignored(h.collider) || h.point.y<.6f || h.normal.y<.69f || h.point.y<=top)continue;
                top=h.point.y;point=h.point;found=true;
            }
            return found;
        }
        private bool WalkingGroundBelow(Vector3 seed,float originY,float distance,out Vector3 point)
        {
            if(GroundBelow(seed,originY,distance,out point))return true;
            // 仅连续步行容忍胶囊足印内的窄缝；登陆和设施支撑仍使用精确中心射线。
            // 用实际表面射线取坡度，不能把球扫掠在陡边上的接触法线当作平地法线。
            bool found=false;float top=float.MinValue;
            foreach(var offset in SeamOffsets)
                if(GroundBelow(seed+offset*(Body.radius*.6f),originY,distance,out var nearby) && nearby.y>top)
                {top=nearby.y;point=new Vector3(seed.x,nearby.y,seed.z);found=true;}
            return found;
        }
        public static bool NonSolidSurface(Collider collider,bool walking=true)=>collider.GetComponentInParent<WaterSystem.Water>()!=null || collider.name=="NavMesh"
            || (walking && collider.name=="Boat building barrier");
        private bool Ignored(Collider collider) => collider==Body || collider.gameObject.layer==5 || collider.transform.IsChildOf(Controller.Player.transform) || NonSolidSurface(collider);
        public bool Clearance(Vector3 feet)
        {
            foreach(var col in Physics.OverlapCapsule(feet+Vector3.up*.36f,feet+Vector3.up*1.46f,.28f,~(1<<4),QueryTriggerInteraction.Ignore))
                if(!Ignored(col))return false;
            return true;
        }
        public bool CanSeeTask(Vector3 target) => Ashore && ClearSight(Actor.position+Vector3.up*EyeHeight,target+Vector3.up*1.1f);
        public bool ClearSight(Vector3 from,Vector3 to)
        {
            var delta=to-from;
            foreach(var h in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore)) if(!Ignored(h.collider))return false;
            return true;
        }
        public void UpdateProximity()
        {
            BerthDistance=float.MaxValue;NearestSite=-1;NearbyTask=-1;
            for(int i=0;i<Sites.Length;i++)
            {
                if(!Sites[i].Available)continue;
                float d=FishingPrototype.FlatDistance(Controller.Player.transform.position,Sites[i].Berth);
                if(d<BerthDistance){BerthDistance=d;NearestSite=i;}
                if(!Ashore)continue;
                for(int t=0;t<2;t++)
                {
                    int id=i*2+t;
                    if(!FishingExploration.Done(Controller.Career.ShoreTaskMask,id) && Vector3.Distance(Actor.position,Sites[i].Tasks[t])<2.6f && ClearSight(Actor.position+Vector3.up*EyeHeight,Sites[i].Tasks[t]+Vector3.up*1.1f))NearbyTask=id;
                }
            }
        }
        public bool TryDisembark()
        {
            if(Controller.Paused || Controller.StoryBlocksGameplay || Ashore || Controller.HasPendingCatch)return false;

            UpdateProximity();
            if(Controller.Anchored){Controller.Notice("先按 Q 收起钓具，再靠近岸边。",4);return false;}
            if(Controller.Speed>1.5f){Controller.Notice("按 Shift 刹车（S 为倒航），低于 5.4 km/h 再下船。",4);return false;}
            if(!FindLanding(out var feet,out var namedSite,out var surveySite,out var shopLanding))
            {Controller.Notice("附近没有安全落脚点：请靠近低矮岸面，避开陡壁、深水和头顶障碍。",4);return false;}
            FreeLanding=!namedSite;ShopLanding=shopLanding;
            LandingPoint=feet;
            CurrentSite=surveySite;
            _mooring=Controller.Player.engine.RB.position;_mooringRotation=Controller.Player.engine.RB.rotation;
            Controller.Player.engine.RB.velocity=Vector3.zero;Controller.Player.engine.RB.angularVelocity=Vector3.zero;
            Ashore=true;_lastSafe=feet+Vector3.up*.08f;_motion.Reset();
            Actor.SetPositionAndRotation(_lastSafe,Quaternion.LookRotation(Vector3.ProjectOnPlane(feet-Controller.Player.transform.position,Vector3.up).normalized));
            Body.enabled=true;SetCrouch(false);Controller.View.ResetLook();Controller.CloseBoatPanels();
            Controller.Notice(Controller.DesktopDisplay?"已登岸 · WASD 行走 · E 互动 · 靠近船边 G 上船":"已登岸 · 左摇杆行走 · 右手柄 A 互动 · 靠近船边登船",6);
            UpdateProximity();return true;
        }
        public bool TryBoard()
        {
            if(!Ashore || Controller.Paused || Controller.StoryBlocksGameplay || Controller.ShopBlocksGameplay)return false;
            if(Controller.Realms.Inside){Controller.Notice("从秘境入口按E返回岛屿，再靠近船边上船；R也可救援回港。",4);return false;}
            if(!CanBoard()) {Controller.Notice("靠近船体边缘（4m 内）再按 G；高差过大或中间有墙时无法登船。",4);return false;}
            BoardImmediately();Controller.Notice("已上船 · WASD 航行；回港按 E 提交海岸调查。",5);return true;
        }
        public void BoardImmediately()
        {
            Controller.Residents?.Close();Controller.Realms?.Abort();ResetTraversal();
            Controller.CancelWorldAction();
            Controller.TackleShop?.Close();ShopLanding=false;
            if(Controller.Expedition!=null)Controller.Expedition.Cancel();
            Controller.LowerFieldTools();SetCrouch(false,true);Body.enabled=false;Ashore=false;Controller.RestoreBoatChart();CurrentSite=-1;WalkSpeed=0;_motion.Reset();Controller.View.ResetLook();SyncRider();
        }
        public void HoldBoat()
        {
            var rb=Controller.Player.engine.RB;var p=rb.position;
            rb.velocity=new Vector3(0,rb.velocity.y,0);rb.angularVelocity=Vector3.zero;
            rb.MovePosition(new Vector3(_mooring.x,p.y,_mooring.z));rb.MoveRotation(_mooringRotation);
        }
        public void Tick(bool allowInput) => Tick(allowInput,Time.deltaTime);
        public void Tick(bool allowInput,float dt)
        {
            if(!Ashore){_motion.Exert(false,true,Controller.Paused?0:dt);UpdateProximity();return;}
            if(Controller.Paused || Controller.StoryBlocksGameplay || Controller.ShopBlocksGameplay || Controller.WorldActionBlocksGameplay || Controller.InventoryOpen || Controller.ShowDetails || !allowInput || (Controller.XRNotebook!=null && Controller.XRNotebook.BlocksGameplay))
            {WalkSpeed=0;ResetMoveInput();return;}
            var k=Keyboard.current;Vector2 input=Vector2.zero;bool sprint=false,jump=false,crouch=false;
            if(k!=null)
            {
                input.x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);
                input.y=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
                sprint=k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                jump=k.spaceKey.isPressed;crouch=k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
            }
            if(XRSettings.isDeviceActive)
            {
                var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if(!FishingXRTracking.TryPose(Controller.viewCamera,XRNode.LeftHand,out _,out _) || !FishingXRTracking.TryPose(Controller.viewCamera,XRNode.RightHand,out _,out _))
                {WalkSpeed=0;ResetMoveInput();return;}
                left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis,out input);
                if(input.magnitude<.18f)input=Vector2.zero;
                sprint=jump=false;crouch=Controller.XRSeat.PhysicallyCrouched;
            }
            Move(input,sprint,jump,crouch,dt);UpdateProximity();
        }
        public void Walk(Vector2 input,bool sprint,float deltaTime) => Move(input,sprint,false,false,deltaTime);
        public void SetCrouch(bool crouch,bool force=false)
        {
            if(!crouch && Crouched && !force && !Clearance(Actor.position))return;
            if(Crouched==crouch && !force)return;
            Crouched=crouch;Body.height=crouch?FishingLocomotion.CrouchingHeight:FishingLocomotion.StandingHeight;
            Body.center=Vector3.up*(Body.height*.5f);Body.stepOffset=crouch?.18f:.32f;
        }

        public void Move(Vector2 input,bool sprint,bool jump,bool crouch,float deltaTime)
        {
            if(!Ashore || Controller.Paused || Controller.StoryBlocksGameplay || Controller.ShopBlocksGameplay || Controller.WorldActionBlocksGameplay || Controller.InventoryOpen || Controller.ShowDetails || (Controller.Expedition!=null && Controller.Expedition.Aligning) || (Controller.XRNotebook!=null && Controller.XRNotebook.BlocksGameplay))
            {ResetMoveInput();return;}
            if(float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime<=0 || float.IsNaN(input.x) || float.IsNaN(input.y) || float.IsInfinity(input.x) || float.IsInfinity(input.y))return;
            float dt=Mathf.Min(deltaTime,.05f);
            if(Climbing){Climb(Vector2.ClampMagnitude(input,1),jump,dt);return;}
            SetCrouch(crouch);
            var heading=Quaternion.Euler(0,Controller.viewCamera.transform.eulerAngles.y,0);
            var axes=Vector2.ClampMagnitude(input,1);
            if(Swimming){Swim(axes,heading,sprint,jump,dt);return;}
            _motion.Exert(sprint && axes.sqrMagnitude>.01f && !Crouched,axes.sqrMagnitude<.01f,dt);
            sprint &= Stamina>.08f;
            var velocity=heading*new Vector3(axes.x,0,axes.y)*(XRSettings.isDeviceActive?(Crouched?.9f:1.6f):Crouched?1.25f:sprint?4.4f:2.6f);
            var next=Actor.position+velocity*dt;
            // Dry land below permits a gravity-driven descent; walls and slopes still use real collision geometry.
            bool support=WalkingGroundBelow(next,Actor.position.y+.45f,80f,out var floor);
            if(!support && Mathf.Abs(velocity.x)>.01f && Mathf.Abs(velocity.z)>.01f)
            {
                // Slide along a supported coast edge instead of cancelling both diagonal axes.
                var alongX=Actor.position+Vector3.right*velocity.x*dt;
                var alongZ=Actor.position+Vector3.forward*velocity.z*dt;
                bool Supported(Vector3 seed,out Vector3 point)=>WalkingGroundBelow(seed,Actor.position.y+.45f,80f,out point);
                if(Supported(alongX,out floor)){velocity.z=0;next=alongX;support=true;}
                else if(Supported(alongZ,out floor)){velocity.x=0;next=alongZ;support=true;}
            }
            if(!support)
            {
                if(!Controller.Realms.Inside && Actor.position.y<2.2f && _motion.VerticalSpeed<=0 && input.sqrMagnitude>.01f)
                {Swim(axes,heading,sprint,jump,dt);return;}
                velocity=Vector3.zero;
                if(input.sqrMagnitude>.01f && Time.unscaledTime>_nextWarning){Controller.Notice("前方是深水或陡坎，请沿岸步行；R 可救援回港。",3);_nextWarning=Time.unscaledTime+4;}
            }
            float vertical=_motion.Step(Body.isGrounded,jump,Crouched,dt);
            var previous=Actor.position;
            float rise=support?floor.y-previous.y:0;
            // Lift before crossing a safe low lip, so diagonal gravity cannot wedge
            // the capsule into a ramp/floor join. Headroom and wall checks stay active.
            if(Body.isGrounded && !jump && vertical<=0 && rise>.02f && rise<=Body.stepOffset && StepClear(new Vector3(next.x,floor.y,next.z)))
            {Body.Move(Vector3.up*(rise+.02f));vertical=-.02f/dt;}
            var collision=Body.Move((velocity+Vector3.up*vertical)*dt);
            if((collision&CollisionFlags.Above)!=0)_motion.HitCeiling();
            WalkSpeed=Vector3.ProjectOnPlane(Actor.position-previous,Vector3.up).magnitude/dt;
            if(velocity.sqrMagnitude>.01f)Actor.rotation=Quaternion.LookRotation(velocity);
            if(Body.isGrounded && Actor.position.y>.6f)_lastSafe=Actor.position;
            if(Actor.position.y<.2f){Body.enabled=false;Actor.position=_lastSafe;Body.enabled=true;_motion.Reset();}
        }
        public Vector3 RoomStep(Vector3 delta)
        {
            if(!Ashore || Controller.Paused || Controller.StoryBlocksGameplay || Controller.WorldActionBusy)return Vector3.zero;
            var before=Actor.position;var desired=before+Vector3.ProjectOnPlane(delta,Vector3.up);
            if(WalkingGroundBelow(desired,before.y+.35f,1f,out var ground) && Mathf.Abs(ground.y-before.y)<.32f)
                Body.Move(Vector3.ProjectOnPlane(delta,Vector3.up));
            return Vector3.ProjectOnPlane(Actor.position-before,Vector3.up);
        }
        private bool StepClear(Vector3 feet)
        {
            float radius=Body.radius;
            int count=Physics.OverlapCapsuleNonAlloc(feet+Vector3.up*(radius+.04f),feet+Vector3.up*(Body.height-radius),radius*.99f,_stepOverlaps,~(1<<4),QueryTriggerInteraction.Ignore);
            if(count==_stepOverlaps.Length)return false;
            for(int i=0;i<count;i++)if(!Ignored(_stepOverlaps[i]))return false;
            return true;
        }
        public bool Survey()
        {
            if(!Ashore || Controller.Paused || Controller.ShopBlocksGameplay)return false;
            UpdateProximity();
            if(NearbyTask<0){Controller.Notice("步行靠近橙色勘测台（2.6m 内），按 E 记录。J 查看海岸调查。",4);return false;}
            int id=NearbyTask;
            if(!Controller.RecordShoreTask(id))return false;
            RefreshMarkers();UpdateProximity();return true;
        }
        public Transform SurveyMarker(int task)=>task>=0 && task<_taskMarkers.Length && _taskMarkers[task]!=null?_taskMarkers[task].transform:null;
        public bool SurveyTask(int task)
        {
            if(!Ashore || Controller.Paused || task<0 || task>=Sites.Length*2)return false;
            var point=Sites[task/2].Tasks[task%2];
            if(!Sites[task/2].Available || Vector3.Distance(Actor.position,point)>=2.6f || !CanSeeTask(point))return false;
            if(!Controller.RecordShoreTask(task))return false;
            RefreshMarkers();UpdateProximity();return true;
        }
        public void RefreshMarkers()
        {
            for(int i=0;i<_taskMarkers.Length;i++)if(_taskMarkers[i]!=null)_taskMarkers[i].sharedMaterial=FishingExploration.Done(Controller.Career.ShoreTaskMask,i)?Controller.teal:Controller.orange;
        }
        public void SyncRider()
        {
            if(Body==null)return;
            if(!Ashore)
            {
                var boat=Controller.Player.transform;
                var yaw=Quaternion.Euler(0,boat.eulerAngles.y,0);
                // 桌面捕鱼时整个人物朝向作业侧，避免镜头看右舷而双臂仍朝船头；XR 不强制转动身体参考系。
                bool fishing=Controller.UsesFishingSeat;
                var facing=XRSettings.isDeviceActive?yaw:yaw*Quaternion.Euler(0,fishing?FishingPrototype.FishingSeatYaw:0,0);
                Actor.SetPositionAndRotation(boat.position+yaw*(fishing?new Vector3(.55f,.45f,.2f):new Vector3(0,.25f,-.25f)),facing);
            }
            _character.Pose(Ashore,WalkSpeed,Crouched,Grounded,Controller.FirstPerson || XRSettings.isDeviceActive,Controller.Paused,Swimming,Climbing);
        }
        public string LocationName => Controller.Home!=null && FishingPrototype.FlatDistance(Position,Controller.Home.Origin)<55 ? Controller.Home.Inside?"回潮渔屋":Controller.Farm!=null && Controller.Farm.Nearby?"回潮菜园":"回潮家岛" : Controller.Realms!=null && Controller.Realms.Inside ? FishingLore.ShrineName(Controller.Realms.Island) : ShopLanding ? FishingTackleShop.Name : Controller.Islands!=null && Controller.Islands.NearIsland(out int island) ? FishingAdventure.Name(island) : FreeLanding ? "自由海岸" : FishingExploration.SiteName(CurrentSite);
        public string ActionTitle => !Ashore ? "G 靠岸下船 · 自动寻找安全岸面 / 码头" : NearbyTask>=0 ? "E "+FishingExploration.TaskName(NearbyTask)
            : CanBoard() ? "G 返回船上" : "靠近船边 G 上船 · 船体距离 "+BoatEdgeDistance().ToString("0.0")+" m";
    }
}
