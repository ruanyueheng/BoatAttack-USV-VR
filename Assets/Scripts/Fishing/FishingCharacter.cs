using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace BoatAttack.Fishing
{
    /// <summary>Supplied 9S surface on the calibrated Quaternius rig and locomotion.</summary>
    public sealed partial class FishingCharacter : MonoBehaviour
    {
        public const string Resource="FishingCharacter/9SCharacter";
        private Animation _animation;
        private Transform _model,_body,_head,_leftThigh,_rightThigh,_leftShin,_rightShin,_leftAnkle,_rightAnkle,_leftFoot,_rightFoot;
        private LocalPose _modelRest;
        private Renderer[] _renderers;
        private readonly Dictionary<string,string> _clips=new Dictionary<string,string>();
        private readonly List<OverlayBone> _rigPose=new List<OverlayBone>();
        private readonly Dictionary<string,float> _fadeStart=new Dictionary<string,float>();
        private string _activeClip;
        private float _fadeAge=.15f;
        private float _interactionRemaining;
        private string _interactionKey="Interact";
        public bool InteractionAnimationReady=>_clips.ContainsKey("Interact") && _clips.ContainsKey("HitRecieve");
        public void PlayToolInteraction()
        {
            if(!_clips.TryGetValue("Punch_Right",out string clip))return;
            _interactionKey="Punch_Right";_interactionRemaining=Mathf.Max(.2f,_animation[clip].length);_animation[clip].time=0;
        }
        public void PlayInteraction(bool impact=false)
        {
            string key=impact?"HitRecieve":"Interact";
            if(!_clips.TryGetValue(key,out string clip))return;
            _interactionKey=key;_interactionRemaining=Mathf.Max(.2f,_animation[clip].length);_animation[clip].time=0;
        }
        private Vector3 _leftAnkleRest,_rightAnkleRest;
        public bool Ready=>_model!=null && _clips.Count>=3 && HandRigReady;
        private float _eyeHeight=1.58f,_eyeForward=.12f,_seatWeight,_crouchWeight;
        public const float CrouchDrop=FishingLocomotion.StandingHeight-FishingLocomotion.CrouchingHeight;
        public float RemainingCrouchDrop=>CrouchDrop*(1-_crouchWeight);
        public Vector3 RemainingCrouchOffset=>AshoreStanceOffset(true);
        public Vector3 AshoreStanceOffset(bool crouched)=>(Vector3.down*CrouchDrop-transform.forward*.12f)*((crouched?1:0)-_crouchWeight)
            +(Vector3.up*.38f+transform.forward*.08f)*_seatWeight;
        private int _stanceFrame=-1;
        private float _presentationDelta;
        public bool IsStanceSettled(bool ashore,bool crouched)=>Mathf.Approximately(_seatWeight,ashore?0:1)
            && Mathf.Approximately(_crouchWeight,ashore && crouched?1:0);
        public Vector3 InteractionLean { get; set; }
        public Vector3 AppliedInteractionLean { get; private set; }
        public Vector3 EyePosition=>transform.TransformPoint(new Vector3(0,_eyeHeight-.38f*_seatWeight-CrouchDrop*_crouchWeight,_eyeForward-.08f*_seatWeight-.12f*_crouchWeight))+Vector3.ClampMagnitude(InteractionLean,.22f);
        private void CalibrateEye()
        {
            // 从同源头部实际蒙皮采样估计眼位，不使用导入器膨胀的动画包围盒。
            var mesh=new Mesh();HeadSkin.BakeMesh(mesh);var bounds=new Bounds();bool first=true;
            var vertices=mesh.vertices;
            foreach(var v in vertices)
            {
                var p=transform.InverseTransformPoint(HeadSkin.transform.TransformPoint(v));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
            // 眼位取双眼子网格的中心，不能取整颗头最前端（鼻尖/头发），否则近处双手会落到镜头外。
            var eyes=new Bounds();bool hasEyes=false;var materials=HeadSkin.sharedMaterials;
            for(int submesh=0;submesh<mesh.subMeshCount && submesh<materials.Length;submesh++)
            {
                if(materials[submesh]==null || materials[submesh].name!="Eye")continue;
                foreach(int index in mesh.GetIndices(submesh))
                {
                    var point=transform.InverseTransformPoint(HeadSkin.transform.TransformPoint(vertices[index]));
                    if(!hasEyes){eyes=new Bounds(point,Vector3.zero);hasEyes=true;}else eyes.Encapsulate(point);
                }
            }
            Destroy(mesh);
            if(!first)
            {
                _eyeHeight=Mathf.Clamp(hasEyes?eyes.center.y:Mathf.Lerp(bounds.min.y,bounds.max.y,.60f),1.35f,1.70f);
                // 眼位位于实际双眼后方 2 cm，而不是头颈中心；仍处于角色胶囊内部。
                _eyeForward=Mathf.Clamp(hasEyes?eyes.center.z-.02f:bounds.center.z,-.05f,.22f);
            }
        }
        public void Build()
        {
            var prefab=Resources.Load<GameObject>(Resource);
            if(prefab==null)throw new InvalidOperationException("Missing CC0 Beach Character FBX");
            _model=Instantiate(prefab,transform,false).transform;_model.name="9S • supplied surface / calibrated game rig";
            _animation=_model.GetComponent<Animation>();
            if(_animation==null)_animation=_model.gameObject.AddComponent<Animation>();
            _animation.playAutomatically=false;_animation.cullingType=AnimationCullingType.AlwaysAnimate;
            // 仅由晚帧 Pose 驱动采样，禁止引擎在最终手部 IK 后再次写入骨骼。
            _animation.enabled=false;
            foreach(AnimationState state in _animation)
            {
                foreach(var key in new[]{"Idle","Walk","Run"})
                    if(state.name.EndsWith(key,StringComparison.OrdinalIgnoreCase)){_clips[key]=state.name;state.wrapMode=WrapMode.Loop;}
            }
            foreach(var clip in Resources.LoadAll<AnimationClip>("FishingCharacter/CompatibleInteractions"))
                foreach(var key in new[]{"Interact","Punch_Right","HitRecieve"})
                    if(clip.name.EndsWith(key,StringComparison.OrdinalIgnoreCase))
                    {
                        _animation.AddClip(clip,clip.name);_clips[key]=clip.name;
                        _animation[clip.name].wrapMode=WrapMode.ClampForever;
                    }
            _renderers=_model.GetComponentsInChildren<Renderer>();
            if(_renderers.Length==0 || _clips.Count<3)throw new InvalidOperationException("Beach Character rig/locomotion import incomplete");
            // FBX animation bounds include exporter unit-conversion transforms and are not
            // a reliable measure of this rig. Normalize the *sampled skin*, in our local frame.
            _activeClip=_clips["Idle"];
            foreach(AnimationState state in _animation)
            {state.enabled=state.name==_activeClip;state.weight=state.enabled?1:0;state.speed=0;state.time=0;}
            _animation.Sample();
            var bounds=new Bounds();bool first=true;
            foreach(var renderer in _model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked=new Mesh();renderer.BakeMesh(baked);
                foreach(var vertex in baked.vertices)
                {
                    var point=transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
                // One player: update skin even when the camera changes between FP and TP.
                renderer.updateWhenOffscreen=true;
                Destroy(baked);
            }
            if(first || bounds.size.y<.001f)throw new InvalidOperationException("Beach Character has no valid skin vertices");
            // 新人物约1.67m，保持已校准手掌与工具的物理尺寸。
            float scale=1.67f/bounds.size.y;_model.localScale*=scale;
            _model.localPosition=Vector3.up*(-bounds.min.y*scale);
            _modelRest=new LocalPose(_model);
            foreach(var bone in _model.GetComponentsInChildren<Transform>())
                switch(bone.name)
                {
                    case "Body":_body=bone;break;
                    case "Head":_head=bone;break;
                    case "UpperLeg.L":_leftThigh=bone;break;case "UpperLeg.R":_rightThigh=bone;break;
                    case "LowerLeg.L":_leftShin=bone;break;case "LowerLeg.R":_rightShin=bone;break;
                    case "Ankle.L":_leftAnkle=bone;break;case "Ankle.R":_rightAnkle=bone;break;
                    case "Foot.L":_leftFoot=bone;break;case "Foot.R":_rightFoot=bone;break;
                }
            // 源动画对骨段还有独立缩放，按采样后的大腿长度校准非蒙皮踝部锚点。
            CalibrateAnkle(_leftThigh,_leftShin,_leftAnkle);CalibrateAnkle(_rightThigh,_rightShin,_rightAnkle);
            foreach(var bone in new[]{_body,_head,_leftThigh,_rightThigh,_leftShin,_rightShin,_leftAnkle,_rightAnkle,_leftFoot,_rightFoot})
            {
                if(bone==null)throw new InvalidOperationException("Beach Character stance rig incomplete");
            }
            _leftAnkleRest=_leftAnkle.localPosition;_rightAnkleRest=_rightAnkle.localPosition;
            // 保存全部同源层级的 TRS；无曲线的躯干、脚、缩放也必须回基。
            foreach(var bone in _model.GetComponentsInChildren<Transform>())_rigPose.Add(new OverlayBone(bone));
            BuildArms();CalibrateEye();BuildFirstPersonSkin();
        }
        private static void CalibrateAnkle(Transform thigh,Transform shin,Transform ankle)
        {
            if(thigh==null || shin==null || ankle==null)return;
            var axis=ankle.localPosition.sqrMagnitude>1e-10f?ankle.localPosition.normalized:Vector3.up;
            ankle.localPosition=axis*(Vector3.Distance(thigh.position,shin.position)/shin.TransformVector(axis).magnitude);
        }
        public void Pose(bool ashore,float speed,bool crouched,bool grounded,bool hidden,bool paused,bool swimming=false,bool climbing=false)
            =>PoseCore(ashore,speed,crouched,grounded,hidden,paused,swimming,climbing,Time.deltaTime);
        public void SampleTitlePose(float deltaTime)
            =>PoseCore(false,0,false,true,false,false,false,false,deltaTime);
        private void PoseCore(bool ashore,float speed,bool crouched,bool grounded,bool hidden,bool paused,bool swimming,bool climbing,float deltaTime)
        {
            if(_model==null)return;
            bool initial=_stanceFrame<0;
            float dt=!paused && _stanceFrame!=Time.frameCount?Mathf.Clamp(deltaTime,0,.05f):0;
            if(_stanceFrame!=Time.frameCount)_presentationDelta=dt;
            _modelRest.Apply(_model);
            SetBodyPresentation(hidden);
            string clip=_clips[ashore && grounded && !swimming && !climbing && speed>.1f?(speed>3?"Run":"Walk"):"Idle"];
            if(dt>0)_interactionRemaining=Mathf.Max(0,_interactionRemaining-dt);
            bool interaction=ashore && grounded && !swimming && !climbing && speed<.1f
                && (_interactionRemaining>0 || InteractionLean.sqrMagnitude>.0001f) && InteractionAnimationReady;
            if(interaction)clip=_clips[_interactionRemaining>0?_interactionKey:"Interact"];
            if(!paused || initial)SelectClip(clip);
            // 每次先恢复完整局部 TRS，再采样；所有程序化层只能叠加在本帧基础上。
            foreach(var bone in _rigPose)bone.Rest.Apply(bone.Bone);
            var leftAnchor=_leftAnkle.position;var rightAnchor=_rightAnkle.position;
            SampleLocomotion(dt);
            _leftAnkle.localPosition=_leftAnkleRest;_rightAnkle.localPosition=_rightAnkleRest;
            if(interaction)
            {
                var leftOffset=_leftFoot.position-_leftAnkle.position;var rightOffset=_rightFoot.position-_rightAnkle.position;
                SolveLeg(_leftThigh,_leftShin,_leftAnkle,leftAnchor);SolveLeg(_rightThigh,_rightShin,_rightAnkle,rightAnchor);
                _leftFoot.position=_leftAnkle.position+leftOffset;_rightFoot.position=_rightAnkle.position+rightOffset;
            }
            // 初始化允许直接摆好坐姿；暂停中只重画，不推进动画或站姿过渡。
            float amount=initial?1:Mathf.Min(1,dt*8);
            _seatWeight=Mathf.MoveTowards(_seatWeight,ashore?0:1,amount);
            _crouchWeight=Mathf.MoveTowards(_crouchWeight,ashore && crouched?1:0,amount);
            _stanceFrame=Time.frameCount;
            if(_seatWeight>0 || _crouchWeight>0)
            {
                var footOffset=transform.forward*(.35f*_seatWeight);
                var leftTarget=_leftAnkle.position+footOffset;var rightTarget=_rightAnkle.position+footOffset;
                var leftOffset=_leftFoot.position-_leftAnkle.position;var rightOffset=_rightFoot.position-_rightAnkle.position;
                _body.position+=Vector3.down*(.38f*_seatWeight+CrouchDrop*_crouchWeight)-transform.forward*(.08f*_seatWeight+.12f*_crouchWeight);
                SolveLeg(_leftThigh,_leftShin,_leftAnkle,leftTarget);
                SolveLeg(_rightThigh,_rightShin,_rightAnkle,rightTarget);
                _leftFoot.position=_leftAnkle.position+leftOffset;_rightFoot.position=_rightAnkle.position+rightOffset;
            }
            // 接触只允许有限的真实躯干俯身；脚目标保持不动，随后重新求解双腿。
            if(!paused || initial)AppliedInteractionLean=Vector3.ClampMagnitude(InteractionLean,.22f);
            if(AppliedInteractionLean.sqrMagnitude>0)
            {
                var leftTarget=_leftAnkle.position;var rightTarget=_rightAnkle.position;
                var leftOffset=_leftFoot.position-leftTarget;var rightOffset=_rightFoot.position-rightTarget;
                _body.position+=AppliedInteractionLean;
                SolveLeg(_leftThigh,_leftShin,_leftAnkle,leftTarget);SolveLeg(_rightThigh,_rightShin,_rightAnkle,rightTarget);
                _leftFoot.position=_leftAnkle.position+leftOffset;_rightFoot.position=_rightAnkle.position+rightOffset;
            }
            CaptureRigBase();
            CaptureArmBase(paused);
        }
        private void SelectClip(string clip)
        {
            if(_activeClip==clip)return;
            _activeClip=clip;_fadeAge=0;_fadeStart.Clear();
            if(_animation[clip].wrapMode==WrapMode.ClampForever)_animation[clip].time=0;
            foreach(AnimationState state in _animation)_fadeStart[state.name]=state.weight;
        }
        private void SampleLocomotion(float dt)
        {
            _fadeAge=Mathf.Min(.15f,_fadeAge+dt);
            float fade=Mathf.SmoothStep(0,1,_fadeAge/.15f);
            foreach(AnimationState state in _animation)
            {
                float start=_fadeStart.TryGetValue(state.name,out var weight)?weight:(state.name==_activeClip?1:0);
                state.weight=Mathf.Lerp(start,state.name==_activeClip?1:0,fade);
                state.enabled=state.weight>0 || state.name==_activeClip;
                state.speed=0;
                if(state.enabled && state.length>0)state.time=state.wrapMode==WrapMode.ClampForever?Mathf.Min(state.time+dt,state.length):Mathf.Repeat(state.time+dt,state.length);
            }
            _animation.Sample();
        }
        private void CaptureRigBase()
        {
            foreach(var bone in _rigPose)bone.Sample=new LocalPose(bone.Bone);
        }
        private void RestoreRigBase()
        {
            foreach(var bone in _rigPose)bone.Sample.Apply(bone.Bone);
        }
        private void SolveLeg(Transform thigh,Transform shin,Transform ankle,Vector3 target)
        {
            float upper=Vector3.Distance(thigh.position,shin.position),lower=Vector3.Distance(shin.position,ankle.position);
            if(upper<.001f || lower<.001f)return;
            var delta=target-thigh.position;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            var axis=delta.sqrMagnitude>1e-8f?delta.normalized:-transform.up;
            var reachable=thigh.position+axis*distance;
            var bend=Vector3.ProjectOnPlane(transform.forward,axis).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(transform.up,axis).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            var knee=thigh.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            thigh.rotation=Quaternion.FromToRotation(shin.position-thigh.position,knee-thigh.position)*thigh.rotation;
            shin.rotation=Quaternion.FromToRotation(ankle.position-shin.position,reachable-shin.position)*shin.rotation;
        }
    }
}
