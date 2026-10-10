using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace BoatAttack.Fishing
{
    /// <summary>Quaternius Beach Character (CC0). Imported rig/locomotion, not primitive body blocks.</summary>
    public sealed partial class FishingCharacter : MonoBehaviour
    {
        private Animation _animation;
        private Transform _model,_body,_leftThigh,_rightThigh,_leftShin,_rightShin,_leftAnkle,_rightAnkle,_leftFoot,_rightFoot;
        private Renderer[] _renderers;
        private readonly Dictionary<string,string> _clips=new Dictionary<string,string>();
        private readonly Dictionary<Transform,Quaternion> _rest=new Dictionary<Transform,Quaternion>();
        private readonly Dictionary<Transform,Vector3> _restPositions=new Dictionary<Transform,Vector3>();
        public bool Ready=>_model!=null && _clips.Count>=3 && HandRigReady;
        private float _eyeHeight=1.58f,_eyeForward=.12f,_seatWeight,_crouchWeight;
        public const float CrouchDrop=FishingLocomotion.StandingHeight-FishingLocomotion.CrouchingHeight;
        public float RemainingCrouchDrop=>CrouchDrop*(1-_crouchWeight);
        public Vector3 RemainingCrouchOffset=>AshoreStanceOffset(true);
        public Vector3 AshoreStanceOffset(bool crouched)=>(Vector3.down*CrouchDrop-transform.forward*.12f)*((crouched?1:0)-_crouchWeight)
            +(Vector3.up*.38f+transform.forward*.08f)*_seatWeight;
        private int _stanceFrame=-1;
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
            var prefab=Resources.Load<GameObject>("FishingCharacter/BeachCharacter");
            if(prefab==null)throw new InvalidOperationException("Missing CC0 Beach Character FBX");
            _model=Instantiate(prefab,transform,false).transform;_model.name="Beach Character • Quaternius (CC0)";
            _animation=_model.GetComponent<Animation>();
            if(_animation==null)_animation=_model.gameObject.AddComponent<Animation>();
            _animation.playAutomatically=false;_animation.cullingType=AnimationCullingType.AlwaysAnimate;
            foreach(AnimationState state in _animation)
            {
                foreach(var key in new[]{"Idle","Walk","Run"})
                    if(state.name.EndsWith(key,StringComparison.OrdinalIgnoreCase)){_clips[key]=state.name;state.wrapMode=WrapMode.Loop;}
            }
            _renderers=_model.GetComponentsInChildren<Renderer>();
            if(_renderers.Length==0 || _clips.Count<3)throw new InvalidOperationException("Beach Character rig/locomotion import incomplete");
            // FBX animation bounds include exporter unit-conversion transforms and are not
            // a reliable measure of this rig. Normalize the *sampled skin*, in our local frame.
            _animation.Play(_clips["Idle"]);_animation.Sample();
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
            float scale=1.75f/bounds.size.y;_model.localScale*=scale;
            _model.localPosition=Vector3.up*(-bounds.min.y*scale);
            foreach(var bone in _model.GetComponentsInChildren<Transform>())
                switch(bone.name)
                {
                    case "Body":_body=bone;break;
                    case "UpperLeg.L":_leftThigh=bone;break;case "UpperLeg.R":_rightThigh=bone;break;
                    case "LowerLeg.L":_leftShin=bone;break;case "LowerLeg.R":_rightShin=bone;break;
                    case "Ankle.L":_leftAnkle=bone;break;case "Ankle.R":_rightAnkle=bone;break;
                    case "Foot.L":_leftFoot=bone;break;case "Foot.R":_rightFoot=bone;break;
                }
            // 源动画对骨段还有独立缩放，按采样后的大腿长度校准非蒙皮踝部锚点。
            CalibrateAnkle(_leftThigh,_leftShin,_leftAnkle);CalibrateAnkle(_rightThigh,_rightShin,_rightAnkle);
            foreach(var bone in new[]{_body,_leftThigh,_rightThigh,_leftShin,_rightShin,_leftAnkle,_rightAnkle,_leftFoot,_rightFoot})
            {
                if(bone==null)throw new InvalidOperationException("Beach Character stance rig incomplete");
                _rest[bone]=bone.localRotation;_restPositions[bone]=bone.localPosition;
            }
            BuildArms();CalibrateEye();BuildFirstPersonSkin();
            _animation.Play(_clips["Idle"]);
        }
        private static void CalibrateAnkle(Transform thigh,Transform shin,Transform ankle)
        {
            if(thigh==null || shin==null || ankle==null)return;
            var axis=ankle.localPosition.sqrMagnitude>1e-10f?ankle.localPosition.normalized:Vector3.up;
            ankle.localPosition=axis*(Vector3.Distance(thigh.position,shin.position)/shin.TransformVector(axis).magnitude);
        }
        public void Pose(bool ashore,float speed,bool crouched,bool grounded,bool hidden,bool paused)
        {
            if(_model==null)return;
            SetBodyPresentation(hidden);
            string clip=_clips[ashore && grounded && speed>.1f?(speed>3?"Run":"Walk"):"Idle"];
            if(!_animation.IsPlaying(clip))_animation.CrossFade(clip,.15f);
            foreach(AnimationState state in _animation)state.speed=paused?0:1;
            // 每次先恢复叠加层再采样，防止坐姿、蹲姿污染下一帧或站立姿态。
            foreach(var rest in _rest)rest.Key.localRotation=rest.Value;
            foreach(var rest in _restPositions)rest.Key.localPosition=rest.Value;
            RestoreArmRest(); // Also restores scale and all finger bones with no animation curves.
            _animation.Sample();
            _leftAnkle.localPosition=_restPositions[_leftAnkle];_rightAnkle.localPosition=_restPositions[_rightAnkle];
            // 每帧只推进一次视觉坐姿/蹲姿；碰撞与上下船安全判定仍立即生效。
            if(_stanceFrame!=Time.frameCount || paused)
            {
                float amount=paused || _stanceFrame<0?1:Mathf.Min(1,Time.deltaTime*8);
                _seatWeight=Mathf.MoveTowards(_seatWeight,ashore?0:1,amount);
                _crouchWeight=Mathf.MoveTowards(_crouchWeight,ashore && crouched?1:0,amount);
                _stanceFrame=Time.frameCount;
            }
            if(_seatWeight>0 || _crouchWeight>0)
            {
                var footOffset=transform.forward*(.35f*_seatWeight);
                var leftTarget=_leftAnkle.position+footOffset;var rightTarget=_rightAnkle.position+footOffset;
                _body.position+=Vector3.down*(.38f*_seatWeight+CrouchDrop*_crouchWeight)-transform.forward*(.08f*_seatWeight+.12f*_crouchWeight);
                _leftFoot.position+=footOffset;_rightFoot.position+=footOffset;
                SolveLeg(_leftThigh,_leftShin,_leftAnkle,leftTarget);
                SolveLeg(_rightThigh,_rightShin,_rightAnkle,rightTarget);
            }
            // 接触只允许有限的真实躯干俯身；脚目标保持不动，随后重新求解双腿。
            AppliedInteractionLean=paused?Vector3.zero:Vector3.ClampMagnitude(InteractionLean,.22f);
            if(AppliedInteractionLean.sqrMagnitude>0)
            {
                var leftTarget=_leftAnkle.position;var rightTarget=_rightAnkle.position;
                _body.position+=AppliedInteractionLean;
                SolveLeg(_leftThigh,_leftShin,_leftAnkle,leftTarget);SolveLeg(_rightThigh,_rightShin,_rightAnkle,rightTarget);
            }
            CaptureArmBase(paused);
        }
        private void SolveLeg(Transform thigh,Transform shin,Transform ankle,Vector3 target)
        {
            float upper=Vector3.Distance(thigh.position,shin.position),lower=Vector3.Distance(shin.position,ankle.position);
            var delta=target-thigh.position;float distance=Mathf.Clamp(delta.magnitude,.001f,upper+lower-.0001f);
            var axis=delta.normalized;var bend=Vector3.ProjectOnPlane(transform.forward,axis).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(transform.up,axis).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            var knee=thigh.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            thigh.rotation=Quaternion.FromToRotation(shin.position-thigh.position,knee-thigh.position)*thigh.rotation;
            shin.rotation=Quaternion.FromToRotation(ankle.position-shin.position,target-shin.position)*shin.rotation;
        }
    }
}
