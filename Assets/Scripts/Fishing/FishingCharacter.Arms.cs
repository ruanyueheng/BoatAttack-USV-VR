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
            public bool Metacarpal;
            public int Digit;
            public LocalPose Neutral;
            public int SkinIndex;
            public readonly List<SkinPoint> Contact=new List<SkinPoint>();
        }
        private struct SkinPoint
        {
            public Vector3 Vertex;
            public BoneWeight Weight;
        }
        private sealed class Arm
        {
            public Transform Shoulder,Upper,Lower,Wrist;
            public Vector3 PalmOffset;
            public Quaternion WristToPalm;
            public Vector3 WristScale;
            public readonly List<OverlayBone> Overlay=new List<OverlayBone>();
            public readonly List<FingerJoint> Fingers=new List<FingerJoint>();
            public readonly List<SkinPoint>[] Contact=new[]{new List<SkinPoint>(),new List<SkinPoint>(),new List<SkinPoint>(),new List<SkinPoint>(),new List<SkinPoint>()};
            public LocalPose[] GripPose;
            public Vector3 GripStart,GripEnd,GripScale;
            public FishingFingerPose GripFingers;
            public float GripRadius;
            public bool GripCached,GripPointing;
            // [Claude v0.41] IK 过渡状态（角色本地空间）：权重渐变 + 目标跳变惯性偏移。
            public float Weight;
            public bool HasLast;
            public int BlendFrame=-1;
            public Vector3 LastWrist,Offset;
            public Quaternion LastRotation=Quaternion.identity,RotationOffset=Quaternion.identity;
        }
        private static readonly bool NoIkBlend=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-fishing-no-ik-blend")>=0;
        private Arm _leftArm,_rightArm;
        private int _armBaseFrame=-1;
        private bool _armsPaused;
        private Bounds _bodyPresentationBounds;
        private Transform[] _contactBones;
        private Matrix4x4[] _contactBindposes,_contactMatrices;
        public bool HandRigReady=>_leftArm!=null && _rightArm!=null;
        public SkinnedMeshRenderer BodySkin { get; private set; }
        public SkinnedMeshRenderer HeadSkin { get; private set; }
        // Diagnostics only: these are the real, source-skinned wrists, never tool parents.
        public Transform LeftWrist=>_leftArm?.Wrist;
        public Transform RightWrist=>_rightArm?.Wrist;
        public int LeftFingerJointCount=>_leftArm?.Fingers.Count ?? 0;
        public int RightFingerJointCount=>_rightArm?.Fingers.Count ?? 0;
        public int ContactSolveCount {get;private set;}
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
            // Match the project's two-influence skinning so rendered hands and contact prediction agree.
            BodySkin.quality=SkinQuality.Bone2;
            _contactBones=BodySkin.bones;_contactBindposes=BodySkin.sharedMesh.bindposes;_contactMatrices=new Matrix4x4[_contactBones.Length];
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
            arm.WristScale=arm.Wrist.localScale;
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
                        Metacarpal=!thumb && joint==1,
                        SkinIndex=Array.IndexOf(_contactBones,bone),
                        Neutral=new LocalPose(bone),
                        Digit=digit=="Index"?0:digit=="Middle"?1:digit=="Ring"?2:digit=="Pinky"?3:4,
                        Degrees=thumb?(joint==1?22:joint==2?40:48):(joint==1?4:joint==2?75:joint==3?85:55)});
                }
            }
            CacheFingerSurfaces(arm);
            arm.GripPose=new LocalPose[arm.Fingers.Count];
            return arm;
        }
        private void CacheFingerSurfaces(Arm arm)
        {
            var vertices=BodySkin.sharedMesh.vertices;var weights=BodySkin.sharedMesh.boneWeights;
            var unique=new[]{new HashSet<SkinPoint>(),new HashSet<SkinPoint>(),new HashSet<SkinPoint>(),new HashSet<SkinPoint>(),new HashSet<SkinPoint>()};
            var digits=new int[_contactBones.Length];for(int i=0;i<digits.Length;i++)digits[i]=-1;
            foreach(var joint in arm.Fingers)if(joint.SkinIndex>=0)digits[joint.SkinIndex]=joint.Digit;
            for(int i=0;i<vertices.Length;i++)
            {
                var original=weights[i];float total=original.weight0+original.weight1;
                if(total<=0)continue;
                var w=new BoneWeight{boneIndex0=original.boneIndex0,boneIndex1=original.boneIndex1,
                    weight0=original.weight0/total,weight1=original.weight1/total};
                for(int digit=0;digit<5;digit++)
                {
                    if((w.weight0>.2f && digits[w.boneIndex0]==digit) || (w.weight1>.2f && digits[w.boneIndex1]==digit))
                    {
                        var point=new SkinPoint{Vertex=vertices[i],Weight=w};
                        if(unique[digit].Add(point))arm.Contact[digit].Add(point);
                    }
                }
            }
            foreach(var joint in arm.Fingers)
                foreach(var point in arm.Contact[joint.Digit])
                {
                    var w=point.Weight;
                    if((w.weight0>0 && _contactBones[w.boneIndex0].IsChildOf(joint.Bone)) || (w.weight1>0 && _contactBones[w.boneIndex1].IsChildOf(joint.Bone)))
                        joint.Contact.Add(point);
                }
        }
        private Vector3 SurfacePalmCentre(Dictionary<string,Transform> bones,string side)
        {
            // 该 FBX 的掌骨不是表面中心；按四条掌骨的真实蒙皮分别取质心，避免拓扑密度偏向小指。
            var mesh=new Mesh();
            try
            {
                BodySkin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=BodySkin.sharedMesh.boneWeights;var skinBones=BodySkin.bones;
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
        public void ApplyHandTargets(FishingHandTarget left,FishingHandTarget right,bool tracked=false,bool allowPaused=false)
        {
            if(!HandRigReady)return;
            ApplyArm(_leftArm,left,true,tracked,allowPaused);ApplyArm(_rightArm,right,false,tracked,allowPaused);
            // Modal IK must refresh GPU skinning even while the animation clock is paused.
            BodySkin.forceMatrixRecalculationPerRender=allowPaused;
            if(FirstPersonSkin!=null)FirstPersonSkin.forceMatrixRecalculationPerRender=allowPaused;
            BodySkin.localBounds=_bodyPresentationBounds;
            if(FirstPersonSkin!=null)FirstPersonSkin.localBounds=_bodyPresentationBounds;
        }
        private void ApplyArm(Arm arm,FishingHandTarget target,bool left,bool tracked,bool allowPaused)
        {
            foreach(var bone in arm.Overlay)bone.Sample.Apply(bone.Bone);
            bool valid=!((_armsPaused && !allowPaused) || _armBaseFrame!=Time.frameCount || target==null || !target.Visible || target.IsLeft!=left);
            // [Claude v0.41] 原实现按帧开关 IK：目标出现瞬间吸附、消失瞬间弹回，是动作僵直的主因。
            // 现在桌面模式用权重渐入(≈0.16s)/渐出(≈0.24s)，并对目标跳变做衰减偏移；XR 追踪保持 1:1。
            float dt=arm.BlendFrame==Time.frameCount?0:Mathf.Clamp(Time.unscaledDeltaTime,0,.05f);arm.BlendFrame=Time.frameCount;
            if(!valid)
            {
                arm.GripCached=false;
                if(tracked || NoIkBlend || !arm.HasLast || _armBaseFrame!=Time.frameCount){arm.Weight=0;arm.HasLast=false;return;}
                arm.Weight=Mathf.MoveTowards(arm.Weight,0,dt/.24f);
                if(arm.Weight<=0){arm.HasLast=false;return;}
                BlendSolve(arm,transform.TransformPoint(arm.LastWrist),transform.rotation*arm.LastRotation,Mathf.SmoothStep(0,1,arm.Weight));
                return;
            }
            arm.Wrist.localScale=arm.WristScale;
            var desiredRotation=target.Palm.rotation*Quaternion.Inverse(arm.WristToPalm);
            // Preserve the sampled rig's actual segment scales; only rotations are solved.
            var wristTarget=WristForPalm(left,target.Palm.position,target.Palm.rotation);
            if(!tracked && !NoIkBlend)
            {
                var localWrist=transform.InverseTransformPoint(wristTarget);var localRotation=Quaternion.Inverse(transform.rotation)*desiredRotation;
                if(arm.HasLast && arm.Weight>.5f && (localWrist-arm.LastWrist).sqrMagnitude>.06f*.06f)
                {
                    // 目标一帧跳变超过 6cm：保留当前可见位置，偏移按约 0.1s 半衰期衰减。
                    arm.Offset+=arm.LastWrist-localWrist;arm.RotationOffset=arm.RotationOffset*(arm.LastRotation*Quaternion.Inverse(localRotation));
                }
                float decay=Mathf.Exp(-dt/.1f);
                arm.Offset=Vector3.ClampMagnitude(arm.Offset*decay,.5f);arm.RotationOffset=Quaternion.Slerp(Quaternion.identity,arm.RotationOffset,decay);
                arm.LastWrist=localWrist;arm.LastRotation=localRotation;
                arm.Weight=allowPaused && _armsPaused?1:Mathf.MoveTowards(arm.HasLast?arm.Weight:0,1,dt/.16f);arm.HasLast=true;
                if(arm.Weight<.999f || arm.Offset.sqrMagnitude>1e-6f || Quaternion.Angle(arm.RotationOffset,Quaternion.identity)>.5f)
                {
                    BlendSolve(arm,transform.TransformPoint(localWrist+arm.Offset),transform.rotation*(arm.RotationOffset*localRotation),Mathf.SmoothStep(0,1,arm.Weight));
                    arm.GripCached=false;ApplyFingers(arm,target);return;
                }
            }
            else{arm.Weight=1;arm.HasLast=false;arm.Offset=Vector3.zero;arm.RotationOffset=Quaternion.identity;}
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
            ApplyFingers(arm,target);
        }
        /// <summary>[Claude v0.41] 按权重在动画姿势与 IK 解之间插值上臂/前臂/腕部旋转。</summary>
        private void BlendSolve(Arm arm,Vector3 wrist,Quaternion rotation,float weight)
        {
            arm.Wrist.localScale=arm.WristScale;
            var upper=arm.Upper.localRotation;var lower=arm.Lower.localRotation;var hand=arm.Wrist.rotation;
            var outward=arm.Upper.position-HandReferencePosition;
            SolveArm(arm,wrist,outward.normalized*.65f-transform.up*.8f-transform.forward*.15f);
            arm.Upper.localRotation=Quaternion.Slerp(upper,arm.Upper.localRotation,weight);
            arm.Lower.localRotation=Quaternion.Slerp(lower,arm.Lower.localRotation,weight);
            arm.Wrist.rotation=Quaternion.Slerp(hand,rotation,weight);
        }
        private void ApplyFingers(Arm arm,FishingHandTarget target)
        {
            var gripStart=target.HasGripSurface?arm.Wrist.InverseTransformPoint(target.GripStart):Vector3.zero;
            var gripEnd=target.HasGripSurface?arm.Wrist.InverseTransformPoint(target.GripEnd):Vector3.zero;
            var gripScale=arm.Wrist.lossyScale;
            // A rigid tool moving with its palm does not change finger contact geometry.
            if(target.HasGripSurface && arm.GripCached && (gripStart-arm.GripStart).sqrMagnitude<4e-8f && (gripEnd-arm.GripEnd).sqrMagnitude<4e-8f
                && (gripScale-arm.GripScale).sqrMagnitude<1e-8f && target.GripRadius==arm.GripRadius && target.Pointing==arm.GripPointing
                && target.Fingers.Fingers==arm.GripFingers.Fingers && target.Fingers.Thumb==arm.GripFingers.Thumb)
            {
                for(int i=0;i<arm.Fingers.Count;i++)arm.GripPose[i].Apply(arm.Fingers[i].Bone);
                return;
            }
            arm.GripCached=false;
            if(target.HasGripSurface)
            {
                ContactSolveCount++;
                for(int digit=0;digit<5;digit++)SetDigit(arm,digit,0);
                for(int i=0;i<_contactBones.Length;i++)_contactMatrices[i]=_contactBones[i].localToWorldMatrix*_contactBindposes[i];
            }
            for(int digit=0;digit<5;digit++)
            {
                float curl=target.Pointing && digit==0?0:target.Fingers.Curl(digit);
                if(!target.HasGripSurface){SetDigit(arm,digit,curl);continue;}
                // Curl the fingertips before knuckle contact can stop the rest of the finger.
                for(int index=0;index<arm.Fingers.Count;index++)
                {
                    var joint=arm.Fingers[digit==4?index:arm.Fingers.Count-1-index];
                    if(joint.Digit!=digit || joint.Metacarpal)continue;
                    var axis=joint.Axis;float degrees=joint.Degrees;
                    if(digit==4)AimThumbGrip(arm,joint,target,curl,out axis,out degrees);
                    float previous=0;
                    for(int step=1;step<=12;step++)
                    {
                        float next=curl*step/12f;SetContactJoint(arm,joint,next,axis,degrees);
                        if(DigitTouches(joint,target))
                        {
                            for(int refine=0;refine<4;refine++)
                            {
                                float middle=(previous+next)*.5f;SetContactJoint(arm,joint,middle,axis,degrees);
                                if(DigitTouches(joint,target))next=middle;else previous=middle;
                            }
                            SetContactJoint(arm,joint,previous,axis,degrees);break;
                        }
                        previous=next;
                    }
                }
            }
            if(target.HasGripSurface)
            {
                for(int i=0;i<arm.Fingers.Count;i++)arm.GripPose[i]=new LocalPose(arm.Fingers[i].Bone);
                arm.GripStart=gripStart;arm.GripEnd=gripEnd;arm.GripScale=gripScale;arm.GripRadius=target.GripRadius;
                arm.GripFingers=target.Fingers;arm.GripPointing=target.Pointing;arm.GripCached=true;
            }
        }
        private static void SetDigit(Arm arm,int digit,float curl)
        {
            // Locomotion may animate fingers: a held object always uses the calibrated neutral hand.
            foreach(var joint in arm.Fingers)if(joint.Digit==digit)
            {
                joint.Neutral.Apply(joint.Bone);
                joint.Bone.localRotation*=Quaternion.AngleAxis(joint.Degrees*curl,joint.Axis);
            }
        }
        private void AimThumbGrip(Arm arm,FingerJoint joint,FishingHandTarget target,float curl,out Vector3 axis,out float degrees)
        {
            axis=joint.Axis;degrees=joint.Degrees;
            var shaft=target.GripEnd-target.GripStart;float length=shaft.sqrMagnitude,nearest=float.MaxValue;
            var from=Vector3.zero;var to=Vector3.zero;
            foreach(var point in arm.Contact[4])
            {
                var w=point.Weight;
                if(!(w.weight0>.2f && _contactBones[w.boneIndex0].IsChildOf(joint.Bone))
                    && !(w.weight1>.2f && _contactBones[w.boneIndex1].IsChildOf(joint.Bone)))continue;
                var p=_contactMatrices[w.boneIndex0].MultiplyPoint3x4(point.Vertex)*w.weight0+_contactMatrices[w.boneIndex1].MultiplyPoint3x4(point.Vertex)*w.weight1;
                var closest=target.GripStart+shaft*Mathf.Clamp01(Vector3.Dot(p-target.GripStart,shaft)/length);
                float distance=(p-closest).sqrMagnitude;if(distance>=nearest)continue;
                nearest=distance;from=p-joint.Bone.position;to=closest-joint.Bone.position;
            }
            // Thumb opposition must reach the actual handle rather than fold back toward the ring knuckle.
            var bend=Vector3.Cross(from,to);
            if(bend.sqrMagnitude<1e-10f || curl<=0)return;
            axis=Quaternion.Inverse(joint.Bone.rotation)*bend.normalized;
            degrees=Mathf.Min(75f,Vector3.Angle(from,to)/curl);
        }
        private void SetContactJoint(Arm arm,FingerJoint changed,float curl,Vector3 axis,float degrees)
        {
            changed.Neutral.Apply(changed.Bone);
            changed.Bone.localRotation*=Quaternion.AngleAxis(degrees*curl,axis);
            foreach(var joint in arm.Fingers)if(joint.Digit==changed.Digit && joint.SkinIndex>=0)
                _contactMatrices[joint.SkinIndex]=joint.Bone.localToWorldMatrix*_contactBindposes[joint.SkinIndex];
        }
        private bool DigitTouches(FingerJoint joint,FishingHandTarget target)
        {
            var axis=target.GripEnd-target.GripStart;float length=axis.sqrMagnitude,radius=target.GripRadius+.001f;
            // Contact on a fixed knuckle cannot stop a different, still straight distal joint.
            foreach(var point in joint.Contact)
            {
                var w=point.Weight;var v=point.Vertex;
                var p=_contactMatrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+_contactMatrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;
                var closest=target.GripStart+axis*Mathf.Clamp01(Vector3.Dot(p-target.GripStart,axis)/length);
                if((p-closest).sqrMagnitude<radius*radius)return true;
            }
            return false;
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
