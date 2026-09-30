using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>One embodied player, aboard or ashore. Desktop only; never takes a tracked XR camera.</summary>
    public sealed class FishingShore : MonoBehaviour
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
        public CharacterController Body { get; private set; }
        public Transform Actor => Body.transform;
        public Vector3 Position => Ashore ? Actor.position : Controller.Player.transform.position;
        public int NearestSite { get; private set; }
        public float BerthDistance { get; private set; }
        public int NearbyTask { get; private set; } = -1;
        public float WalkSpeed { get; private set; }
        private Renderer[] _renderers;
        private Transform _leftLeg, _rightLeg;
        private readonly Renderer[] _taskMarkers = new Renderer[8];
        private Vector3 _mooring, _lastSafe;
        private Quaternion _mooringRotation;
        private float _step, _nextWarning;
        private readonly FishingLocomotion _motion = new FishingLocomotion();
        private Transform _visual;
        private readonly Dictionary<Transform,Vector3> _standingPositions=new Dictionary<Transform,Vector3>();
        public bool Crouched { get; private set; }
        public float EyeHeight => (Crouched?FishingLocomotion.CrouchingHeight:FishingLocomotion.StandingHeight)-.2f;
        public bool Grounded => Body!=null && Body.isGrounded;
        public void ResetMoveInput() => _motion.ResetInput();
        private bool _hidden;
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
            Part("Boot L", new Vector3(-.16f,.1f,.06f), new Vector3(.21f,.2f,.36f), c.dark);
            Part("Boot R", new Vector3(.16f,.1f,.06f), new Vector3(.21f,.2f,.36f), c.dark);
            _leftLeg = Part("Left trouser",new Vector3(-.16f,.46f,0),new Vector3(.23f,.58f,.27f),c.wood).transform;
            _rightLeg = Part("Right trouser",new Vector3(.16f,.46f,0),new Vector3(.23f,.58f,.27f),c.wood).transform;
            Part("Jacket",new Vector3(0,1.03f,0),new Vector3(.57f,.65f,.34f),c.teal);
            Part("Life vest",new Vector3(0,1.07f,.17f),new Vector3(.49f,.47f,.13f),c.orange);
            Part("Left sleeve",new Vector3(-.37f,1f,0),new Vector3(.18f,.58f,.22f),c.teal);
            Part("Right sleeve",new Vector3(.37f,1f,0),new Vector3(.18f,.58f,.22f),c.teal);
            FishingArt.Shape("Head",PrimitiveType.Sphere,_visual,new Vector3(0,1.51f,0),new Vector3(.35f,.4f,.35f),c.cream);
            Part("Cap",new Vector3(0,1.7f,.04f),new Vector3(.4f,.1f,.45f),c.dark);
            _renderers = Actor.GetComponentsInChildren<Renderer>();
            foreach(Transform part in _visual)_standingPositions.Add(part,part.localPosition);
            // A rider's motor is disabled while seated. The hull remains the vessel's collision body.
            foreach (var hull in c.Player.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(Body,hull);
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
            var hits=Physics.RaycastAll(new Vector3(seed.x,originY,seed.z),Vector3.down,distance,~(1<<4),QueryTriggerInteraction.Ignore);
            float top=float.MinValue; bool found=false;
            foreach(var h in hits)
            {
                if (Ignored(h.collider) || h.point.y<.6f || h.normal.y<.69f || h.point.y<=top) continue;
                top=h.point.y; point=h.point; found=true;
            }
            return found;
        }
        private bool Ignored(Collider collider) => collider==Body || collider.transform.IsChildOf(Controller.Player.transform) || collider.GetComponentInParent<WaterSystem.Water>()!=null || collider.name=="NavMesh";
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
            if(Controller.Paused || Ashore)return false;
            if(XRSettings.isDeviceActive){Controller.Notice("上岸步行为桌面预览，尚未接入头显移动。",4);return false;}
            UpdateProximity();
            if(Controller.Anchored){Controller.Notice("先按 Q 收起钓具，再靠近橙色登岸旗。",4);return false;}
            if(NearestSite<0 || BerthDistance>9f){Controller.Notice("驶近橙色登岸旗（9m 内），低速按 G 下船。",4);return false;}
            if(Controller.Speed>1.5f){Controller.Notice("按 Shift 刹车（S 为倒航），低于 5.4 km/h 再下船。",4);return false;}
            var site=Sites[NearestSite];
            // Validate current geometry again, including runtime obstacles. Never teleport into a rock.
            bool ground=Ground(site.Landing,out var feet);
            bool clear=Clearance(feet), sight=ClearSight(Controller.Player.transform.position+Vector3.up*2,site.Berth+Vector3.up*2);
            if(Controller.Automated)Debug.Log($"[SHORE] Landing {NearestSite}: ground={ground}, height={feet.y-site.Landing.y}, clearance={clear}, approach={sight}");
            if(!ground || Mathf.Abs(feet.y-site.Landing.y)>1f || !clear || !sight)
            {Controller.Notice("登岸通道被阻挡，请从旗帜的海侧靠近。",4);return false;}
            CurrentSite=NearestSite;
            _mooring=Controller.Player.engine.RB.position;_mooringRotation=Controller.Player.engine.RB.rotation;
            Controller.Player.engine.RB.velocity=Vector3.zero;Controller.Player.engine.RB.angularVelocity=Vector3.zero;
            Ashore=true;_lastSafe=feet+Vector3.up*.08f;_motion.Reset();
            Actor.SetPositionAndRotation(_lastSafe,Quaternion.LookRotation(Vector3.ProjectOnPlane(site.Landing-site.Berth,Vector3.up)));
            Body.enabled=true;SetCrouch(false);Controller.View.ResetLook();Controller.CloseBoatPanels();
            Controller.Notice("已登岸 · WASD 行走 / Shift 快走 / 空格跳跃 / Ctrl 蹲下 / 鼠标转头 / Alt 释放鼠标 / E 勘测 / 原登岸点 G 上船",8);
            UpdateProximity();return true;
        }
        public bool TryBoard()
        {
            if(!Ashore || Controller.Paused)return false;
            if(Vector3.Distance(Actor.position,Sites[CurrentSite].Landing)>4f){Controller.Notice("返回本次登岸点（4m 内），再按 G 上船。",4);return false;}
            if(FishingPrototype.FlatDistance(Controller.Player.transform.position,Sites[CurrentSite].Berth)>20f){Controller.Notice("船离开了泊位，请按 R 救援回港。",4);return false;}
            BoardImmediately();Controller.Notice("已上船 · WASD 航行；回港按 E 提交海岸调查。",5);return true;
        }
        public void BoardImmediately()
        {
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
            if(!Ashore){UpdateProximity();return;}
            if(Controller.Paused || Controller.InventoryOpen || Controller.ShowDetails || !allowInput || XRSettings.isDeviceActive)
            {WalkSpeed=0;ResetMoveInput();return;}
            var k=Keyboard.current;Vector2 input=Vector2.zero;bool sprint=false,jump=false,crouch=false;
            if(k!=null)
            {
                input.x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);
                input.y=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
                sprint=k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                jump=k.spaceKey.isPressed;crouch=k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
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
            // Preserve the head/torso proportions: lower the hips, bend the legs and lean forward rather than squashing the whole person.
            foreach(var pose in _standingPositions)
            {
                var position=pose.Value;var part=pose.Key;
                if(crouch)
                {
                    if(position.y>1.4f)position+=new Vector3(0,-.67f,.17f);
                    else if(position.y>.8f)position+=new Vector3(0,-.5f,.12f);
                    else if(part==_leftLeg || part==_rightLeg)position+=new Vector3(0,-.15f,-.06f);
                }
                part.localPosition=position;
                part.localRotation=Quaternion.Euler(crouch && position.y>.2f && pose.Value.y<1.4f?15:0,0,0);
            }
        }
        public void Move(Vector2 input,bool sprint,bool jump,bool crouch,float deltaTime)
        {
            if(!Ashore || Controller.Paused || Controller.InventoryOpen || Controller.ShowDetails || (Controller.Expedition!=null && Controller.Expedition.Aligning) || XRSettings.isDeviceActive)
            {ResetMoveInput();return;}
            if(float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime<=0 || float.IsNaN(input.x) || float.IsNaN(input.y) || float.IsInfinity(input.x) || float.IsInfinity(input.y))return;
            float dt=Mathf.Min(deltaTime,.05f);SetCrouch(crouch);
            var heading=Quaternion.Euler(0,Controller.viewCamera.transform.eulerAngles.y,0);
            var axes=Vector2.ClampMagnitude(input,1);
            var velocity=heading*new Vector3(axes.x,0,axes.y)*(Crouched?1.25f:sprint?4.4f:2.6f);
            var next=Actor.position+velocity*dt;
            // Search through the jump arc, but still reject water/large drops relative to the last grounded feet.
            bool support=GroundBelow(next,Actor.position.y+.45f,3f,out var floor) && floor.y<=Actor.position.y+.45f && floor.y>=_lastSafe.y-.65f;
            if(!support)
            {
                velocity=Vector3.zero;
                if(input.sqrMagnitude>.01f && Time.unscaledTime>_nextWarning){Controller.Notice("前方是深水或陡坎，请沿岸步行；R 可救援回港。",3);_nextWarning=Time.unscaledTime+4;}
            }
            float vertical=_motion.Step(Body.isGrounded,jump,Crouched,dt);
            var previous=Actor.position;var collision=Body.Move((velocity+Vector3.up*vertical)*dt);
            if((collision&CollisionFlags.Above)!=0)_motion.HitCeiling();
            WalkSpeed=Vector3.ProjectOnPlane(Actor.position-previous,Vector3.up).magnitude/dt;
            if(velocity.sqrMagnitude>.01f)Actor.rotation=Quaternion.LookRotation(velocity);
            if(Body.isGrounded && Actor.position.y>.6f)_lastSafe=Actor.position;
            if(Actor.position.y<.2f){Body.enabled=false;Actor.position=_lastSafe;Body.enabled=true;_motion.Reset();}
        }
        public bool Survey()
        {
            if(!Ashore || Controller.Paused)return false;
            UpdateProximity();
            if(NearbyTask<0){Controller.Notice("步行靠近橙色勘测台（2.6m 内），按 E 记录。J 查看海岸调查。",4);return false;}
            int id=NearbyTask;
            if(!Controller.RecordShoreTask(id))return false;
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
                Actor.SetPositionAndRotation(boat.position+yaw*(Controller.Anchored?new Vector3(.05f,.45f,-.55f):new Vector3(0,.25f,-.25f)),yaw);
            }
            bool hidden=Controller.FirstPerson || XRSettings.isDeviceActive;
            if(hidden!=_hidden){foreach(var r in _renderers)r.shadowCastingMode=hidden?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;_hidden=hidden;}
            if(!Controller.Paused)_step+=Time.deltaTime*WalkSpeed*3;
            float swing=Ashore?Mathf.Sin(_step)*Mathf.Min(WalkSpeed,1)*18:0;
            _leftLeg.localRotation=Quaternion.Euler((Crouched?55:0)+swing,0,0);_rightLeg.localRotation=Quaternion.Euler((Crouched?55:0)-swing,0,0);
        }
        public string ActionTitle => !Ashore ? (BerthDistance<18?"G 下船 · "+FishingExploration.SiteName(NearestSite):"N 选择橙色海岸航点 · 低速靠近后 G 下船")
            : NearbyTask>=0?"E "+FishingExploration.TaskName(NearbyTask):Vector3.Distance(Actor.position,Sites[CurrentSite].Landing)<4?"G 返回船上 · E 调查附近勘测台":"E 调查橙色台 · 返回登岸点 "+FishingPrototype.FlatDistance(Actor.position,Sites[CurrentSite].Landing).ToString("0")+" m";
    }
}
