using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFarmWorld
    {
        private readonly FishingActionClock _canClock=new FishingActionClock();
        private readonly List<Mesh> _canMeshes=new List<Mesh>();
        private FishingCareer _canProfile;
        private Transform _bucket;
        private LineRenderer _wellRope,_waterStream;
        private ParticleSystem _waterDrops;
        private float _nextWet;
        private Material _streamMaterial,_dropMaterial;
        private FarmWork _canWork;
        private int _canTarget=-1,_canSlot;
        private FieldItem _canTool;
        private Vector3 _canStartActor,_startCanPosition,_startRightPalm,_canLean;
        private Quaternion _startCanRotation;
        private bool _canCommit,_canTracked,_canCrouched,_ownsCanLean,_wellSoundContact;
        private float _nextDrop;
        private bool _mapCanHidden;
        // Gameplay coordinates follow the imported FBX, whose depth axis is reversed from the Blender authoring script.
        public static readonly Vector3 CanHandle=new Vector3(-.025f,.120f,-.12f);
        public static readonly Quaternion CanPalmRotation=Quaternion.LookRotation(Vector3.back,Vector3.up);
        public static readonly Vector3 CanPalm=CanHandle+CanPalmRotation*new Vector3(0,.035f,-.045f);
        public static readonly Vector3 CanSpout=new Vector3(.33f,-.015f,-.25f);
        private static readonly Quaternion CanDockRotation=Quaternion.Euler(0,180,0);
        public bool CarryingCan {get;private set;}
        public bool Busy=>_canClock.Active || _canClock.ContactPending;
        public Transform WateringCan=>_can;
        public Transform WellBucket=>_bucket;
        public bool WaterStreamVisible=>_waterStream!=null && _waterStream.enabled;
        public float WorkProgress=>_canClock.Progress;
        public float CanGripError=>_c==null || _can==null?float.MaxValue:Vector3.Distance(_c.Shore.Character.PalmPosition(false),_can.TransformPoint(CanPalm));
        public string ActionHint=>( _canWork==FarmWork.Refill?"摇井绳 → 提起水桶 → 灌入水壶":_canWork==FarmWork.Water?"缓缓倾壶浇灌":_canWork==FarmWork.TakeCan?"拿起井边水壶":"将水壶放回木架")+" · "+Mathf.RoundToInt(WorkProgress*100)+"% · Esc 取消";

        private void BuildWateringCan(Transform fixtures)
        {
            // The can rests on the garden-path (-Z) edge of the desk-height stand, handle grip facing the path, so a standing arm reaches it.
            var dock=new GameObject("井边水壶木架 · 拿起 / 放回").transform;dock.SetParent(Root,false);dock.localPosition=new Vector3(11.79f,1.01f,-9.39f);_targets[15]=dock;
            FishingArt.Shape("Watering can stand",PrimitiveType.Cube,Root,new Vector3(11.7f,.74f,-9.1f),new Vector3(.68f,.08f,.62f),_c.wood,true);
            foreach(float x in new[]{-.25f,.25f})foreach(float z in new[]{-.22f,.22f})FishingArt.Shape("Can stand leg",PrimitiveType.Cube,Root,new Vector3(11.7f+x,.37f,-9.1f+z),new Vector3(.06f,.74f,.06f),_c.wood,true);
            _can=Instantiate(Resources.Load<GameObject>("FishingV036/StarlightWateringCanView"),Root,false).transform;_can.name="V036 persistent watering can";
            _canProfile=_c.Career;DockCan();
            _bucket=new GameObject("V036 working well bucket").transform;_bucket.SetParent(Root,false);_bucket.localPosition=new Vector3(10.2f,.29f,-8);
            ExtractWellBucket(fixtures);
            BuildWellWater();
            _wellRope=FishingArt.Line("Working well rope",Root,_c.wood,.022f);_wellRope.positionCount=2;BuildWindlass();
            _streamMaterial=new Material(Resources.Load<Shader>("FishingSpray")){name="Garden flowing water"};_streamMaterial.SetFloat("_Round",0);
            _dropMaterial=new Material(_streamMaterial){name="Garden water droplets"};_dropMaterial.SetFloat("_Round",1);
            _waterStream=FishingArt.Line("Can spout / well bucket water stream",Root,_streamMaterial,.025f);_waterStream.positionCount=12;_waterStream.enabled=false;
            _waterStream.startColor=new Color(.60f,.88f,1,.8f);_waterStream.endColor=new Color(.82f,.96f,1,.45f);
            var drops=new GameObject("Garden droplets and soil splashes");drops.transform.SetParent(Root,false);_waterDrops=drops.AddComponent<ParticleSystem>();_waterDrops.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=_waterDrops.main;main.maxParticles=72;main.startSpeed=0;main.startLifetime=.45f;main.startSize=.025f;main.gravityModifier=.8f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=_waterDrops.emission;emission.enabled=false;var shape=_waterDrops.shape;shape.enabled=false;
            var fade=_waterDrops.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.65f,.9f,1),0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=drops.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_dropMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            ResetBucket();
            FishingV019Art.Sign("井边水壶\n拿起 → 水井打水 → 提去浇灌 → 放回",Root,new Vector3(11.7f,1.51f,-9.1f),.012f,new Color(.94f,.86f,.61f));
        }

        private void ExtractWellBucket(Transform fixtures)
        {
            // The V035 well was baked into one mesh; detach its authored bucket instead of leaving a duplicate inside the well.
            foreach(var filter in fixtures.GetComponentsInChildren<MeshFilter>())
            {
                var source=filter.sharedMesh;var vertices=source.vertices;var normals=source.normals;var colors=source.colors;var uv=source.uv;
                var points=new Vector3[vertices.Length];var bucket=new bool[vertices.Length];var rope=new bool[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=points[i]=Root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                    bucket[i]=Mathf.Abs(p.x-10.2f)<.36f && Mathf.Abs(p.z+8)<.36f && p.y>.26f && p.y<1.01f;
                    rope[i]=Mathf.Abs(p.x-10.2f)<.04f && Mathf.Abs(p.z+8)<.04f && p.y>.35f && p.y<1.67f;
                    // The baked crank and full rope coil are replaced by the turning windlass parts.
                    rope[i]|=CrankVertex(p) || CoilVertex(p);
                }
                var body=Instantiate(source);body.name="Garden fixtures without fixed well bucket";_canMeshes.Add(body);
                var positions=new List<Vector3>();var ns=new List<Vector3>();var cs=new List<Color>();var us=new List<Vector2>();var remap=new Dictionary<int,int>();var subsets=new List<int>[source.subMeshCount];
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    var kept=new List<int>();subsets[sub]=new List<int>();var triangles=source.GetTriangles(sub);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                        bool moving=bucket[a] && bucket[b] && bucket[c];bool fixedRope=rope[a] && rope[b] && rope[c];
                        if(!moving && !fixedRope){kept.Add(a);kept.Add(b);kept.Add(c);continue;}
                        if(!moving)continue;
                        for(int j=0;j<3;j++)
                        {
                            int old=triangles[i+j];if(!remap.TryGetValue(old,out int next))
                            {
                                next=positions.Count;remap.Add(old,next);positions.Add(points[old]-_bucket.localPosition);
                                ns.Add(normals.Length==vertices.Length?Root.InverseTransformDirection(filter.transform.TransformDirection(normals[old])):Vector3.up);
                                cs.Add(colors.Length==vertices.Length?colors[old]:Color.white);us.Add(uv.Length==vertices.Length?uv[old]:Vector2.zero);
                            }
                            subsets[sub].Add(next);
                        }
                    }
                    body.SetTriangles(kept,sub,false);
                }
                filter.sharedMesh=body;
                if(positions.Count==0)continue;
                var mesh=new Mesh{name="Detached original well bucket"};mesh.SetVertices(positions);mesh.SetNormals(ns);mesh.SetColors(cs);mesh.SetUVs(0,us);mesh.subMeshCount=subsets.Length;
                for(int sub=0;sub<subsets.Length;sub++)mesh.SetTriangles(subsets[sub],sub,false);mesh.RecalculateBounds();_canMeshes.Add(mesh);
                var part=new GameObject("Authored cedar bucket",typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(_bucket,false);part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials;
            }
            if(_bucket.childCount==0)throw new InvalidOperationException("The authored garden well bucket could not be detached for animation.");
        }

        private bool CanUseWateringCan=>_c.Ashore && !_c.Shore.Swimming && !_c.Shore.Climbing && !_c.Paused && !_c.InventoryOpen && !_c.JournalOpen && !_c.ShopBlocksGameplay && !_c.StoryBlocksGameplay && !_c.HasPendingCatch
            && _c.Hands.CanPresentTools && !_c.XRSeat.HeadBlocked && (_c.XRNotebook==null || !_c.XRNotebook.BlocksGameplay);
        private bool BeginCanWork(int id,FarmWork work)
        {
            if(!CanUseWateringCan || Busy || Target(id)==null || FishingPrototype.FlatDistance(_c.Shore.Position,Target(id).position)>2.25f)return false;
            if(work!=FarmWork.TakeCan && !CarryingCan){_c.Notice("水壶就在水井右侧木架上 · 先靠近拿起。",4);return false;}
            if(work==FarmWork.Water && _c.Career.Farm.AvailableWater<=0){_c.Notice("水壶空了 · 提着水壶到水井打水。",4);return false;}
            bool tracked=XRSettings.isDeviceActive;bool handling=work==FarmWork.TakeCan || work==FarmWork.ReturnCan;
            if(tracked)
            {
                if(!_c.Hands.TryControllerPalm(false,out var palm,out _))return false;
                var handle=work==FarmWork.TakeCan?_can.TransformPoint(CanPalm):_targets[15].position+Root.rotation*CanDockRotation*CanPalm;
                if(handling && Vector3.Distance(palm,handle)>.3f){_c.Notice("将右手靠近井边水壶的提手，再按 A 拿起或放回。",4);return false;}
            }
            else if(handling && FishingPrototype.FlatDistance(_c.Shore.Position,Target(id).position)>1.6f){_c.Notice("再走近井边木架一些，伸手拿起或放回水壶。",4);return false;}
            _canLean=Vector3.zero;
            // Like reaching the handle, refilling and watering step up to a working distance instead of acting from 2 m away.
            var site=Target(id).position;
            if(!tracked && work==FarmWork.Refill && !WellStanceReaches(_c.Shore.Actor.position,WorkFacing))
            {
                if(_c.TryPlanReachApproach(Root.TransformPoint(CrankPivotLocal),Root.TransformPoint(CrankPivotLocal),WellStanceReaches,()=>BeginCanWork(id,work),1.8f))return true;
                if(_c.Automated)Debug.Log("[WELL] no stance: pivot="+Root.TransformPoint(CrankPivotLocal).ToString("F2")+" actor="+_c.Shore.Actor.position.ToString("F2"));
                _c.Notice("站到井边辘轳摇把旁，双手够得到摇把再打水。",4);return false;
            }
            if(!tracked && work==FarmWork.Water && FishingPrototype.FlatDistance(_c.Shore.Position,site)>1.3f
                && _c.TryPlanReachApproach(site,_c.Farm.Aim(id),(at,face)=>FishingPrototype.FlatDistance(at,site)<=1.3f,()=>BeginCanWork(id,work),1.8f))return true;
            if(!tracked && handling)
            {
                var dockRotation=Root.rotation*CanDockRotation;
                // The stand blocks the body, so step in beside it to a stance whose arm truly reaches the fixed handle.
                if(!_c.CanReachFarmHandle(DockPalm,dockRotation*CanPalmRotation,out _canLean))
                {
                    var grip=dockRotation*CanPalmRotation;
                    if(_c.TryPlanReachApproach(DockPalm,DockPalm,(at,face)=>_c.CanReachFarmHandle(DockPalm,grip,at,face,out _),()=>BeginCanWork(id,work)))return true;
                    _c.Notice("绕到木架另一侧，让手掌能稳稳握住水壶提手。",4);return false;
                }
            }
            float duration=work==FarmWork.Refill?6f:work==FarmWork.Water?2.2f:.9f;
            if(!_canClock.TryBegin(FishingActionKind.UseTool,"farm:"+id,duration,duration))return false;
            _canWork=work;_canTarget=id;_canProfile=_c.Career;_canStartActor=_c.Shore.Position;_canSlot=_c.Career.Backpack.Selected;_canTool=_c.Career.Backpack.Equipped;_canTracked=XRSettings.isDeviceActive;
            _wellSoundContact=false;
            _startCanPosition=_can.position;_startCanRotation=_can.rotation;_startRightPalm=_c.Shore.Character.PalmPosition(false);_nextDrop=0;
            _canCrouched=_c.Shore.Crouched;
            _ownsCanLean=!tracked && (handling || work==FarmWork.Refill);
            if(_ownsCanLean && work==FarmWork.Refill)_c.Shore.Character.SetTabletopView(true);
            if(work==FarmWork.Refill)RimSeat(_c.Shore.Position-Vector3.up*.08f,out _rimSeat,out _rimSeatRotation);
            _c.Shore.ResetMoveInput();return true;
        }
        private Vector3 DockPalm=>_targets[15].position+Root.rotation*CanDockRotation*CanPalm;
        private void TickCanWork()
        {
            if(_canProfile!=_c.Career || (CarryingCan && (!_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing || _c.Home.Sleeping || _c.Home.Lounging)))
            {CancelWork();CarryingCan=false;_canProfile=_c.Career;DockCan();}
            if(!Busy)return;
            if(!CanUseWateringCan || _canTracked!=XRSettings.isDeviceActive || _c.Career.Backpack.Selected!=_canSlot || _c.Career.Backpack.Equipped!=_canTool
                || Vector3.Distance(_canStartActor,_c.Shore.Position)>.4f || Work(_canTarget)!=_canWork || (_ownsCanLean && _c.Shore.Crouched!=_canCrouched)
                || (_canTracked && !_c.Hands.TryControllerPalm(false,out _,out _)))
            {CancelWork();return;}
            if(_ownsCanLean && !_c.Shore.Character.IsStanceSettled(true,_canCrouched))return;
            if(_c.Acceptance!=null && _c.Acceptance.FreezeFarmAnimation)return;
            _canClock.Tick(Time.deltaTime);
            if(_ownsCanLean && _canWork!=FarmWork.Refill)_c.Shore.Character.InteractionLean=_canLean*Ease(0,.20f,WorkProgress);
            if(!_canClock.ContactPending || !_canClock.TryConsumeContact(_canClock.ActionId))return;
            bool ok;_canCommit=true;
            try
            {
                if(_canWork==FarmWork.TakeCan){CarryingCan=true;ok=true;_c.Notice("已拿起水壶 · 在水井打水，再提到菜畦浇灌。",5);}
                else if(_canWork==FarmWork.ReturnCan){CarryingCan=false;ok=true;DockCan();_c.Notice("水壶已放回井边木架 · 剩余井水会保留。",4);}
                else ok=CommitAt(_canTarget,_canWork);
            }
            finally{_canCommit=false;}
            if(ok && (_canWork==FarmWork.TakeCan || _canWork==FarmWork.ReturnCan))_c.WorldAudio?.Play("CanHandle",_can.position,.5f);
            if(ok && (_canWork==FarmWork.Refill || _canWork==FarmWork.Water))_c.WorldAudio?.Play("WaterFinish",_can.position,.6f);
            _c.ReportFarmContact(_canClock.ActionId,ok);_canClock.Cancel();ResetBucket();_waterStream.enabled=false;RestoreCanLean();
        }
        public void CancelWork()
        {
            _canClock.Cancel();_canCommit=false;
            RestoreCanLean();
            if(_waterStream!=null)_waterStream.enabled=false;
            if(_waterDrops!=null)_waterDrops.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ResetBucket();if(!CarryingCan && _can!=null)DockCan();
        }
        private void RestoreCanLean()
        {
            if(_ownsCanLean)
            {
                _ownsCanLean=false;
                if(_c!=null && _c.Shore!=null && _c.Shore.Character!=null)
                {_c.Shore.Character.InteractionLean=Vector3.zero;if(_canWork==FarmWork.Refill)_c.Shore.Character.SetTabletopView(false);}
            }
        }
        private void DockCan()=>_can.SetPositionAndRotation(_targets[15].position,Root.rotation*CanDockRotation);
        private void ResetBucket()
        {
            if(_bucket==null || _wellRope==null)return;
            _wellRope.positionCount=2;
            _bucket.localPosition=new Vector3(10.2f,WellWaterLevel-.80f,-8);_bucket.localRotation=Quaternion.identity;PresentRope();
        }
        private void PresentRope(){PresentWindlass(Wound(_bucket.position));_wellRope.SetPosition(0,RopeTop);_wellRope.SetPosition(_wellRope.positionCount-1,_bucket.TransformPoint(Vector3.up*.65f));}
        private static float Ease(float from,float to,float p)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,p));
        public void ApplyPresentation()
        {
            if(_c==null || _can==null)return;
            bool hideCan=CarryingCan && _c.PortableChart!=null && _c.PortableChart.Active;
            if(hideCan!=_mapCanHidden){_can.gameObject.SetActive(!hideCan);_mapCanHidden=hideCan;}
            PresentSoilTool();
            if(!CarryingCan && !Busy){DockCan();UpdateCanWater();return;}
            if(!_c.Hands.CanPresentTools)return;
            bool tracked=XRSettings.isDeviceActive;float p=WorkProgress;
            _c.Hands.GetToolFrame(out var origin,out _);var frame=Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0);
            var rotation=frame*Quaternion.Inverse(CanPalmRotation);var handRotation=CanPalmRotation;var palm=origin+frame*new Vector3(_c.Shore.Character.HandSide(false)*.14f,-.14f,.42f);

            if(Busy && _canWork==FarmWork.Water)
            {
                float tilt=Ease(.08f,.28f,p)*(1-Ease(.76f,1,p));rotation*=Quaternion.Euler(0,0,-50*tilt);
                palm+=frame*new Vector3(Mathf.Sin(p*Mathf.PI*3)*.035f,0,.055f)*tilt;
            }
            if(tracked)
            {
                if(!_c.Hands.TryControllerPalm(false,out palm,out var palmRotation))return;
                rotation=_c.Hands.ControllerGripRotation(palmRotation)*Quaternion.Euler(0,-Mathf.Atan2(CanSpout.x,CanSpout.z)*Mathf.Rad2Deg,0);
                handRotation=Quaternion.Inverse(rotation)*palmRotation;
            }
            var position=palm-rotation*CanPalm;
            if(Busy && _canWork==FarmWork.TakeCan){float take=Ease(.24f,1,p);position=Vector3.Lerp(_startCanPosition,position,take);rotation=Quaternion.Slerp(_startCanRotation,rotation,take);}
            if(Busy && _canWork==FarmWork.ReturnCan){float put=Ease(.05f,.82f,p);position=Vector3.Lerp(position,_targets[15].position,put);rotation=Quaternion.Slerp(rotation,Root.rotation*CanDockRotation,put);}
            // Desktop refill sets the can down on the rim so both hands are free for the windlass and the bucket.
            float seated=!tracked && Busy && _canWork==FarmWork.Refill?Ease(.04f,.18f,p)*(1-Ease(.86f,.95f,p)):0;
            position=Vector3.Lerp(position,_rimSeat,seated);rotation=Quaternion.Slerp(rotation,_rimSeatRotation,seated);
            _can.SetPositionAndRotation(position,rotation);
            bool otherAction=_c.WorldActionBusy && !Busy;
            if(!tracked && !otherAction && seated<=0 && !(Busy && (_canWork==FarmWork.TakeCan || _canWork==FarmWork.ReturnCan)))_c.Hands.FitToolPose(_can,Vector3.zero,CanPalm,Quaternion.identity,CanPalmRotation,false,true);
            if(!otherAction)
            {
                if(!tracked && Busy && _canWork==FarmWork.TakeCan && p<.24f)
                    _c.Hands.Right.SetPose(Vector3.Lerp(_startRightPalm,_can.TransformPoint(CanPalm),Ease(0,.24f,p)),Quaternion.Slerp(frame,_can.rotation*CanPalmRotation,Ease(0,.24f,p)),Mathf.Lerp(.15f,.75f,Ease(0,.24f,p)));
                else
                {
                    _c.Hands.SetToolTarget(false,_can,CanPalm,handRotation,.88f,fingers:new FishingFingerPose(.80f,.85f,.88f,.92f,.76f));
                    _c.Hands.Right.SetGripSurface(_can.TransformPoint(CanHandle+Vector3.left*.065f),_can.TransformPoint(CanHandle+Vector3.right*.085f),.010f);
                }
                _c.Hands.Right.SetForearmRoll(.65f);
                _c.Hands.Right.FollowAuthoredMotion();
                if(!tracked)_c.Hands.RestHand(true);
            }
            UpdateCanWater();
            _waterStream.enabled=false;
            if(!Busy || _c.Paused)return;
            if(_canWork==FarmWork.Refill)PresentRefill(p,tracked,frame);
            if(_canWork==FarmWork.Water && p>.25f && p<.79f)
            {
                var landing=_targets[_canTarget].position+new Vector3(Mathf.Sin(p*11)*.30f,.13f,Mathf.Cos(p*11)*.22f);
                PresentStream(_can.TransformPoint(CanSpout),landing,true);
            }
        }
        private void PresentRefill(float p,bool tracked,Quaternion frame)
        {
            if((p>.25f && p<.52f) || (p>.82f && p<.98f))
                _c.WorldAudio?.MaintainLoop("well-rope","WellRopeLoop",_targets[12].position+Vector3.up,.045f,true,9);
            if(!_wellSoundContact && p>.17f && p<.27f)
            {_wellSoundContact=true;_c.WorldAudio?.Play("WellBucket",WellWater.position,.42f);}
            var low=_targets[12].position+Vector3.up*(WellWaterLevel-.80f);var raised=_targets[12].position+Vector3.up*1.05f;
            // Hangs filling in the water, winds up (.25-.52), is tipped into the can (.56-.80), then unwinds back down.
            var position=p<.25f?low:p<.52f?Vector3.Lerp(low,raised,Ease(.25f,.52f,p)):Vector3.Lerp(raised,low,Ease(.82f,1,p));
            float pour=Ease(.56f,.64f,p)*(1-Ease(.74f,.82f,p));var mouth=_can.TransformPoint(new Vector3(0,.005f,-.12f));
            var direction=Vector3.ProjectOnPlane(mouth-position,Vector3.up);var heading=direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):Quaternion.identity;
            var lip=new Vector3(0,.32f,.26f);var rotation=heading*Quaternion.Euler(42*pour,0,0);
            // Anchor the pour by its rim so the tilted bucket and tall handle stay away from the player's face.
            position=Vector3.Lerp(position,mouth+Vector3.up*.22f-heading*Vector3.forward*.35f-rotation*lip,pour);
            _bucket.SetPositionAndRotation(position,rotation);PresentRope();
            if(p>.17f && p<.27f && Time.time>=_nextDrop)
            {
                _nextDrop=Time.time+.1f;
                _waterDrops.Emit(new ParticleSystem.EmitParams{position=WellWater.position+Vector3.right*.24f,startLifetime=.35f,startSize=.022f,velocity=new Vector3(.1f,.55f,0),startColor=new Color(.65f,.9f,1,.8f)},3);
                if(p<.2f)_c.Effects?.Ripple(WellWater.position+Vector3.right*.24f+Vector3.up*.01f,.25f);
            }
            // The full bucket drips on the way up; drops landing back in the shaft ring the surface.
            if(p>.25f && p<.58f && Time.time>=_nextDrop)
            {
                _nextDrop=Time.time+.07f;var bottom=_bucket.TransformPoint(new Vector3(UnityEngine.Random.Range(-.2f,.2f),.02f,UnityEngine.Random.Range(-.2f,.2f)));
                _waterDrops.Emit(new ParticleSystem.EmitParams{position=bottom,startLifetime=.6f,startSize=.018f,velocity=Vector3.down*.4f,startColor=new Color(.65f,.9f,1,.8f)},1);
                if(p<.45f && UnityEngine.Random.value<.35f)_c.Effects?.Ripple(new Vector3(bottom.x,WellWater.position.y+.01f,bottom.z),.12f);
            }
            if(!tracked){PresentRefillHands(p);_c.Shore.Character.InteractionLean=RefillLean(p);}
            if(p>.58f && p<.78f)PresentStream(_bucket.TransformPoint(lip),mouth,false);
        }
        private void PresentStream(Vector3 from,Vector3 to,bool soil)
        {
            _waterStream.enabled=true;
            var audio=_c.WorldAudio;
            if(audio!=null)
            {
                // A separately supplied soil-sprinkling recording can replace the
                // existing recorded water pour. Until it exists, reuse that actual clip;
                // never claim a missing/new synthetic asset was generated.
                string cue=soil && audio.HasCue("WaterSprinkleLoop")?"WaterSprinkleLoop":"WaterPourLoop";
                audio.MaintainLoop("farm-water",cue,Vector3.Lerp(from,to,.6f),soil?.11f:.08f,true,10,FishingAudioCategory.Effects);
            }
            for(int i=0;i<12;i++){float u=i/11f;_waterStream.SetPosition(i,Vector3.Lerp(from,to,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.035f));}
            if(Time.time<_nextDrop)return;_nextDrop=Time.time+(_c.Weather.ReducedEffects?.10f:.045f);
            var velocity=(to-from)/.42f+Vector3.up*.5f;
            _waterDrops.Emit(new ParticleSystem.EmitParams{position=from,startLifetime=.42f,startSize=.025f,velocity=velocity,startColor=new Color(.65f,.9f,1,.9f)},_c.Weather.ReducedEffects?1:3);
            if(soil && Time.time>=_nextWet){_nextWet=Time.time+.6f;_c.Effects?.Wet(to);}
            if(soil)_waterDrops.Emit(new ParticleSystem.EmitParams{position=to,startLifetime=.22f,startSize=.035f,velocity=new Vector3(Mathf.Sin(Time.time*31)*.28f,.45f,Mathf.Cos(Time.time*23)*.28f),startColor=new Color(.76f,.92f,1,.8f)},2);
        }
        private void DestroyCanPresentation()
        {
            foreach(var mesh in _canMeshes)if(mesh!=null)Destroy(mesh);
            if(_streamMaterial!=null)Destroy(_streamMaterial);if(_dropMaterial!=null)Destroy(_dropMaterial);
            if(_wellWaterMaterial!=null)Destroy(_wellWaterMaterial);
            if(_canWaterMaterial!=null)Destroy(_canWaterMaterial);
        }
        private void OnDisable()=>CancelWork();
    }
}
