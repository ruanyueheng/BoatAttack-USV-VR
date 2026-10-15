using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        private struct LocalPose
        {
            public Vector3 Position,Scale;
            public Quaternion Rotation;
            public LocalPose(Transform t){Position=t.localPosition;Rotation=t.localRotation;Scale=t.localScale;}
            public void Apply(Transform t){t.localPosition=Position;t.localRotation=Rotation;t.localScale=Scale;}
        }
        private sealed class OverlayBone
        {
            public Transform Bone;
            public LocalPose Rest,Sample;
            public OverlayBone(Transform bone){Bone=bone;Rest=Sample=new LocalPose(bone);}
        }
        private sealed class FingerJoint
        {
            public Transform Bone;
            public Vector3 Axis;
            public float Degrees;
            public bool Index;
            public int Digit;
        }
        private sealed class Arm
        {
            public Transform Shoulder,Upper,Lower,Wrist;
            public Vector3 PalmOffset;
            public Quaternion WristToPalm;
            public readonly List<OverlayBone> Overlay=new List<OverlayBone>();
            public readonly List<FingerJoint> Fingers=new List<FingerJoint>();
        }
        private Arm _leftArm,_rightArm;
        private int _armBaseFrame=-1;
        private bool _armsPaused;
        private Bounds _bodyPresentationBounds;
        public bool HandRigReady=>_leftArm!=null && _rightArm!=null;
        public SkinnedMeshRenderer BodySkin { get; private set; }
        public SkinnedMeshRenderer HeadSkin { get; private set; }
        // Diagnostics only: these are the real, source-skinned wrists, never tool parents.
        public Transform LeftWrist=>_leftArm?.Wrist;
        public Transform RightWrist=>_rightArm?.Wrist;
        public int LeftFingerJointCount=>_leftArm?.Fingers.Count ?? 0;
        public int RightFingerJointCount=>_rightArm?.Fingers.Count ?? 0;
        public Vector3 HandReferencePosition=>HandRigReady?(_leftArm.Upper.position+_rightArm.Upper.position)*.5f:transform.position+transform.up*1.3f;
        public Vector3 ShoulderPosition(bool left)=>(left?_leftArm:_rightArm).Upper.position;
        public Vector3 ElbowPosition(bool left)=>(left?_leftArm:_rightArm).Lower.position;
        public float ArmReach(bool left)
        {
            var arm=left?_leftArm:_rightArm;
            return Vector3.Distance(arm.Upper.position,arm.Lower.position)+Vector3.Distance(arm.Lower.position,arm.Wrist.position);
        }
        public float HandSide(bool left)=>Mathf.Sign(Vector3.Dot(ShoulderPosition(left)-HandReferencePosition,transform.right));
        public Vector3 PalmPosition(bool left)
        {
            var arm=left?_leftArm:_rightArm;return arm.Wrist.TransformPoint(arm.PalmOffset);
        }
        public Quaternion PalmRotation(bool left)
        {
            var arm=left?_leftArm:_rightArm;return arm.Wrist.rotation*arm.WristToPalm;
        }
        public Vector3 WristForPalm(bool left,Vector3 position,Quaternion rotation)
        {
            var arm=left?_leftArm:_rightArm;
            var offset=Quaternion.Inverse(arm.Wrist.rotation)*arm.Wrist.TransformVector(arm.PalmOffset);
            return position-rotation*Quaternion.Inverse(arm.WristToPalm)*offset;
        }
        private void BuildArms()
        {
            var bones=new Dictionary<string,Transform>();
            foreach(var bone in _model.GetComponentsInChildren<Transform>())bones[bone.name]=bone;
            foreach(var skin in _model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(skin.name=="Beach_Body")BodySkin=skin;
                if(skin.name=="Beach_Head")HeadSkin=skin;
            }
            if(BodySkin==null || HeadSkin==null)throw new InvalidOperationException("Beach Character needs the original separate body/head skins");
            _leftArm=BuildArm(bones,true);_rightArm=BuildArm(bones,false);
            // 导入动画边界不包含程序化抬臂，必须覆盖所有合法手臂姿态，否则第一人称会剔除整个身体。
            _bodyPresentationBounds=new Bounds(BodySkin.transform.InverseTransformPoint(transform.TransformPoint(Vector3.up*.9f)),Vector3.zero);
            for(int corner=0;corner<8;corner++)
            {
                var point=new Vector3((corner&1)==0?-1f:1f,(corner&2)==0?-.4f:2.2f,(corner&4)==0?-1f:1f);
                _bodyPresentationBounds.Encapsulate(BodySkin.transform.InverseTransformPoint(transform.TransformPoint(point)));
            }
            BodySkin.allowOcclusionWhenDynamic=false;
            CaptureArmBase(false);
        }
        private static Transform Required(Dictionary<string,Transform> bones,string name)
        {
            if(!bones.TryGetValue(name,out var bone))throw new InvalidOperationException("Beach Character missing source bone "+name);
            return bone;
        }
        private Arm BuildArm(Dictionary<string,Transform> bones,bool left)
        {
            string side=left?".L":".R";
            var arm=new Arm{Shoulder=Required(bones,"Shoulder"+side),Upper=Required(bones,"UpperArm"+side),
                Lower=Required(bones,"LowerArm"+side),Wrist=Required(bones,"Wrist"+side)};
            if(arm.Upper.parent!=arm.Shoulder || arm.Lower.parent!=arm.Upper || arm.Wrist.parent!=arm.Lower)
                throw new InvalidOperationException("Beach Character upper-limb hierarchy changed");
            foreach(var bone in arm.Shoulder.GetComponentsInChildren<Transform>())arm.Overlay.Add(new OverlayBone(bone));
            // *1 is a long metacarpal, NOT a proximal finger joint. The knuckles are *2.
            var index=Required(bones,"Index2"+side);var middle=Required(bones,"Middle2"+side);
            var ring=Required(bones,"Ring2"+side);var pinky=Required(bones,"Pinky2"+side);
            var knuckles=(index.position+middle.position+ring.position+pinky.position)*.25f;
            var forward=(knuckles-arm.Wrist.position).normalized;
            var thumbward=(index.position-pinky.position).normalized;
            var dorsal=Vector3.Cross(forward,thumbward).normalized*(left?1:-1);
            if(dorsal.sqrMagnitude<.9f)throw new InvalidOperationException("Beach Character degenerate palm calibration");
            var palm=SurfacePalmCentre(bones,side);
            arm.PalmOffset=arm.Wrist.InverseTransformPoint(palm);
            arm.WristToPalm=Quaternion.Inverse(arm.Wrist.rotation)*Quaternion.LookRotation(forward,dorsal);
            foreach(var digit in new[]{"Index","Middle","Ring","Pinky","Thumb"})
            {
                bool thumb=digit=="Thumb";int count=thumb?3:4;
                for(int joint=1;joint<=count;joint++)
                {
                    var bone=Required(bones,digit+joint+side);
                    var expected=joint==1?arm.Wrist:Required(bones,digit+(joint-1)+side);
                    if(bone.parent!=expected)throw new InvalidOperationException("Beach Character finger hierarchy changed: "+bone.name);
                    // Terminal display/end bones are exporter-dependent; the preceding segment
                    // gives the distal direction without adding or reparenting any skin bone.
                    var direction=joint<count?Required(bones,digit+(joint+1)+side).position-bone.position:bone.position-bone.parent.position;
                    var bend=-dorsal;
                    if(thumb)bend=(ring.position-bone.position).normalized*.65f-dorsal*.75f; // opposition, not copied finger-X curl
                    var axis=Vector3.Cross(direction.normalized,bend).normalized;
                    if(axis.sqrMagnitude<.9f)throw new InvalidOperationException("Beach Character degenerate finger axis: "+bone.name);
                    arm.Fingers.Add(new FingerJoint{Bone=bone,Axis=Quaternion.Inverse(bone.rotation)*axis,Index=digit=="Index",
                        Digit=digit=="Index"?0:digit=="Middle"?1:digit=="Ring"?2:digit=="Pinky"?3:4,
                        Degrees=thumb?(joint==1?22:joint==2?40:48):(joint==1?4:joint==2?75:joint==3?85:55)});
                }
            }
            return arm;
        }
        private Vector3 SurfacePalmCentre(Dictionary<string,Transform> bones,string side)
        {
            // 该 FBX 的掌骨不是表面中心；按四条掌骨的真实蒙皮分别取质心，避免拓扑密度偏向小指。
            var mesh=new Mesh();
            try
            {
                BodySkin.BakeMesh(mesh);var vertices=mesh.vertices;var weights=BodySkin.sharedMesh.boneWeights;var skinBones=BodySkin.bones;
                var sum=Vector3.zero;int groups=0;
                foreach(var digit in new[]{"Index","Middle","Ring","Pinky"})
                {
                    int index=Array.IndexOf(skinBones,Required(bones,digit+"1"+side));
                    var center=Vector3.zero;float total=0;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var w=weights[i];float amount=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)
                            +(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                        if(amount<=0)continue;
                        center+=BodySkin.transform.TransformPoint(vertices[i])*amount;total+=amount;
                    }
                    if(total<=.001f)continue;
                    sum+=center/total;groups++;
                }
                if(groups!=4)throw new InvalidOperationException("源角色掌骨蒙皮不完整，无法标定可见手掌 "+side);
                return sum/groups;
            }
            finally{Destroy(mesh);}
        }
        private void RestoreArmRest()
        {
            RestoreArm(_leftArm);RestoreArm(_rightArm);
        }
        private static void RestoreArm(Arm arm)
        {
            if(arm!=null)foreach(var bone in arm.Overlay)bone.Rest.Apply(bone.Bone);
        }
        private void CaptureArmBase(bool paused)
        {
            _armsPaused=paused;_armBaseFrame=Time.frameCount;
            CaptureArm(_leftArm);CaptureArm(_rightArm);
        }
        private static void CaptureArm(Arm arm)
        {
            if(arm!=null)foreach(var bone in arm.Overlay)bone.Sample=new LocalPose(bone.Bone);
        }
        /// <summary>
        /// Final-frame stage: root/base animation/legs -> camera -> independent hand/tool/
        /// catch goals -> this method -> rod-tip line. Never Sample after this call.
        /// Repeated calls restore the same sampled TRS, and expired goals restore animation.
        /// </summary>
        public void ApplyHandTargets(FishingHandTarget left,FishingHandTarget right,bool tracked=false)
        {
            if(!HandRigReady)return;
            ApplyArm(_leftArm,left,true,tracked);ApplyArm(_rightArm,right,false,tracked);
            BodySkin.localBounds=_bodyPresentationBounds;
            if(FirstPersonSkin!=null)FirstPersonSkin.localBounds=_bodyPresentationBounds;
        }
        private void ApplyArm(Arm arm,FishingHandTarget target,bool left,bool tracked)
        {
            foreach(var bone in arm.Overlay)bone.Sample.Apply(bone.Bone);
            if(_armsPaused || _armBaseFrame!=Time.frameCount || target==null || !target.Visible || target.IsLeft!=left)return;
            var desiredRotation=target.Palm.rotation*Quaternion.Inverse(arm.WristToPalm);
            // Preserve the sampled rig's actual segment scales; only rotations are solved.
            var wristTarget=WristForPalm(left,target.Palm.position,target.Palm.rotation);
            var outward=arm.Upper.position-HandReferencePosition;
            var pole=outward.normalized*.65f-transform.up*.8f-transform.forward*.15f;
            SolveArm(arm,wristTarget,pole);
            if(!tracked && target.ForearmRoll>0)
            {
                // Share pronation with the forearm instead of dumping the entire roll
                // into the wrist skin. Rotation about the lower-arm axis preserves length.
                var axis=(arm.Wrist.position-arm.Lower.position).normalized;
                var from=Vector3.ProjectOnPlane((arm.Wrist.rotation*arm.WristToPalm)*Vector3.up,axis);
                var to=Vector3.ProjectOnPlane(target.Palm.up,axis);
                if(from.sqrMagnitude>1e-6f && to.sqrMagnitude>1e-6f)
                    arm.Lower.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(from,to,axis)*target.ForearmRoll,axis)*arm.Lower.rotation;
            }
            arm.Wrist.rotation=desiredRotation;
            // Tracked hands use the hand-only source mesh: controller reach cannot stretch a visible forearm.
            if(tracked)arm.Wrist.position=wristTarget;
            foreach(var finger in arm.Fingers)
            {
                float curl=target.Pointing && finger.Index?0:target.Fingers.Curl(finger.Digit);
                finger.Bone.localRotation*=Quaternion.AngleAxis(finger.Degrees*curl,finger.Axis);
            }
        }
        private void SolveArm(Arm arm,Vector3 target,Vector3 pole)
        {
            float upper=Vector3.Distance(arm.Upper.position,arm.Lower.position),lower=Vector3.Distance(arm.Lower.position,arm.Wrist.position);
            if(upper<.001f || lower<.001f)return;
            var delta=target-arm.Upper.position;
            var axis=delta.sqrMagnitude>1e-8f?delta.normalized:transform.forward;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            var reachable=arm.Upper.position+axis*distance;
            var bend=Vector3.ProjectOnPlane(pole,axis);
            if(bend.sqrMagnitude<.0001f)bend=Vector3.ProjectOnPlane(transform.forward,axis);
            if(bend.sqrMagnitude<.0001f)bend=Vector3.ProjectOnPlane(transform.right,axis);
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            var elbow=arm.Upper.position+axis*along+bend.normalized*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            arm.Upper.rotation=Quaternion.FromToRotation(arm.Lower.position-arm.Upper.position,elbow-arm.Upper.position)*arm.Upper.rotation;
            arm.Lower.rotation=Quaternion.FromToRotation(arm.Wrist.position-arm.Lower.position,reachable-arm.Lower.position)*arm.Lower.rotation;
        }
    }
}
