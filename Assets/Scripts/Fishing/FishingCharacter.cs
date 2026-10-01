using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace BoatAttack.Fishing
{
    /// <summary>Quaternius Beach Character (CC0). Imported rig/locomotion, not primitive body blocks.</summary>
    public sealed class FishingCharacter : MonoBehaviour
    {
        private Animation _animation;
        private Transform _model,_body,_leftThigh,_rightThigh,_leftShin,_rightShin,_leftAnkle,_rightAnkle,_leftFoot,_rightFoot;
        private Renderer[] _renderers;
        private readonly Dictionary<string,string> _clips=new Dictionary<string,string>();
        private bool _hidden;
        private readonly Dictionary<Transform,Quaternion> _rest=new Dictionary<Transform,Quaternion>();
        private readonly Dictionary<Transform,Vector3> _restPositions=new Dictionary<Transform,Vector3>();
        public bool Ready=>_model!=null && _clips.Count>=3;
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
            if(_hidden!=hidden){foreach(var renderer in _renderers)renderer.shadowCastingMode=hidden?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;_hidden=hidden;}
            string clip=_clips[ashore && grounded && speed>.1f?(speed>3?"Run":"Walk"):"Idle"];
            if(!_animation.IsPlaying(clip))_animation.CrossFade(clip,.15f);
            foreach(AnimationState state in _animation)state.speed=paused?0:1;
            // 每次先恢复叠加层再采样，防止坐姿、蹲姿污染下一帧或站立姿态。
            foreach(var rest in _rest)rest.Key.localRotation=rest.Value;
            foreach(var rest in _restPositions)rest.Key.localPosition=rest.Value;
            _animation.Sample();
            _leftAnkle.localPosition=_restPositions[_leftAnkle];_rightAnkle.localPosition=_restPositions[_rightAnkle];
            if(!ashore || crouched)
            {
                // 此模型的 Body 同时带动躯干和大腿；Foot 是 Root 的独立子骨骼。
                // 保留动画中的踝部目标，用双骨 IK 弯腿，而不是假定本地 X 轴一致。
                var footOffset=ashore?Vector3.zero:transform.forward*.35f;
                var leftTarget=_leftAnkle.position+footOffset;var rightTarget=_rightAnkle.position+footOffset;
                _body.position+=Vector3.down*(ashore?.42f:.38f)-transform.forward*(ashore?.12f:.08f);
                _leftFoot.position+=footOffset;_rightFoot.position+=footOffset;
                SolveLeg(_leftThigh,_leftShin,_leftAnkle,leftTarget);
                SolveLeg(_rightThigh,_rightShin,_rightAnkle,rightTarget);
            }
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
