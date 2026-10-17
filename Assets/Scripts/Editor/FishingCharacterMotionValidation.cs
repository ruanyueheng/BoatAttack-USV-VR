using System;
using System.Collections;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>独立同源角色的连续帧验证，不写模型、场景或存档。</summary>
    public static class FishingCharacterMotionValidation
    {
        private struct BonePose
        {
            public Transform Bone;
            public Vector3 Position,Scale;
            public Quaternion Rotation;
            public BonePose(Transform bone){Bone=bone;Position=bone.localPosition;Scale=bone.localScale;Rotation=bone.localRotation;}
            public bool Same()=>Vector3.Distance(Position,Bone.localPosition)<.00001f
                && Vector3.Distance(Scale,Bone.localScale)<.00001f && Quaternion.Angle(Rotation,Bone.localRotation)<.05f;
        }
        private static void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException("角色连续帧验证："+label);
        }
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value)=>Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static Transform Bone(FishingCharacter character,string name)
        {
            foreach(var bone in character.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;
            throw new InvalidOperationException("源骨骼缺失："+name);
        }
        private static List<BonePose> Snapshot(FishingCharacter character)
        {
            var result=new List<BonePose>();
            foreach(var bone in character.GetComponentsInChildren<Transform>())result.Add(new BonePose(bone));
            return result;
        }
        public static void CheckImport()
        {
            const string path="Assets/Resources/FishingCharacter/9SCharacter.fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            Check(importer!=null && importer.animationType==ModelImporterAnimationType.Legacy
                && importer.animationCompression==ModelImporterAnimationCompression.Off && importer.isReadable,"保留可读无压缩 Legacy 导入体系");
            var keys=new HashSet<string>();int curves=0,scaleCurves=0;
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if(!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__"))continue;
                foreach(var key in new[]{"Idle","Walk","Run"})if(clip.name.EndsWith(key,StringComparison.OrdinalIgnoreCase))keys.Add(key);
                Check(clip.legacy && clip.length>0,"有效 Legacy 动画 "+clip.name);
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    curves++;if(binding.propertyName.StartsWith("m_LocalScale"))scaleCurves++;
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);
                    foreach(var key in curve.keys)Check(Finite(key.time) && Finite(key.value),"动画曲线有限 "+clip.name+"/"+binding.path);
                }
            }
            Check(keys.Count==3 && curves>0,"同源 Idle/Walk/Run 曲线齐全");
            var source=Resources.Load<GameObject>(FishingCharacter.Resource);
            foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=skin.sharedMesh;var bindposes=mesh.bindposes;var bones=skin.bones;
                Check(bindposes.Length==bones.Length,"蒙皮骨骼与 bindpose 一一对应 "+skin.name);
                for(int i=0;i<bindposes.Length;i++)
                {
                    Check(bones[i]!=null,"蒙皮骨骼完整 "+skin.name);
                    for(int element=0;element<16;element++)Check(Finite(bindposes[i][element]),"bindpose 矩阵有限 "+skin.name);
                }
                foreach(var w in mesh.boneWeights)
                {
                    float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                    Check(Finite(sum) && Mathf.Abs(sum-1)<.005f,"蒙皮权重归一 "+skin.name);
                    Check(Valid(w.boneIndex0,w.weight0) && Valid(w.boneIndex1,w.weight1)
                        && Valid(w.boneIndex2,w.weight2) && Valid(w.boneIndex3,w.weight3),"蒙皮索引合法 "+skin.name);
                }
                bool Valid(int index,float weight)=>weight<=0 || index>=0 && index<bones.Length;
            }
            Debug.Log("[CHARACTER-MOTION] 导入检查通过，曲线="+curves+"，缩放曲线="+scaleCurves);
        }
        private static void CheckGeometry(FishingCharacter character,Mesh baked,float[] lengths)
        {
            int segment=0;
            foreach(string side in new[]{".L",".R"})
            {
                foreach(var chain in new[]{new[]{"UpperArm","LowerArm","Wrist"},new[]{"UpperLeg","LowerLeg","Ankle"}})
                    for(int i=0;i<2;i++)
                    {
                        float length=Vector3.Distance(Bone(character,chain[i]+side).position,Bone(character,chain[i+1]+side).position);
                        Check(Finite(length) && length>.01f && Mathf.Abs(length-lengths[segment++])<.025f,"骨段长度不漂移 "+chain[i]+side);
                    }
            }
            foreach(var bone in character.GetComponentsInChildren<Transform>())
                Check(Finite(bone.position) && Finite(bone.localScale) && Finite(bone.rotation.x)
                    && Finite(bone.rotation.y) && Finite(bone.rotation.z) && Finite(bone.rotation.w),"骨骼 TRS 有限 "+bone.name);
            foreach(var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(baked);
                foreach(var vertex in baked.vertices)
                {
                    var world=skin.transform.TransformPoint(vertex);
                    Check(Finite(world) && Vector3.Distance(world,character.transform.position)<5,"蒙皮顶点有限且不爆炸 "+skin.name);
                }
            }
        }
        public static IEnumerator Checks(FishingPrototype unused)
        {
            CheckImport();
            var root=new GameObject("隔离角色动作回归");var goals=new GameObject("隔离手掌目标");var baked=new Mesh();
            try
            {
                root.transform.position=new Vector3(0,200,0);
                var character=root.AddComponent<FishingCharacter>();character.Build();character.Pose(true,0,false,true,false,false);
                var left=goals.AddComponent<FishingHandTarget>();left.Build(true);
                var right=new GameObject("右手目标").AddComponent<FishingHandTarget>();right.transform.SetParent(goals.transform);right.Build(false);
                var lengths=new List<float>();
                foreach(string side in new[]{".L",".R"})foreach(var chain in new[]{new[]{"UpperArm","LowerArm","Wrist"},new[]{"UpperLeg","LowerLeg","Ankle"}})
                    for(int i=0;i<2;i++)lengths.Add(Vector3.Distance(Bone(character,chain[i]+side).position,Bone(character,chain[i+1]+side).position));
                var animation=character.GetComponentInChildren<Animation>();Check(!animation.enabled,"禁止最终 IK 后引擎自动采样");
                for(int frame=0;frame<360;frame++)
                {
                    int phase=frame/30;bool seated=phase==1 || phase==10;bool crouched=phase==2 || phase==3;
                    bool swim=phase==7;bool climb=phase==8;bool grounded=phase!=6;
                    float speed=phase==4?2.6f:phase==5?4.4f:swim?1.45f:climb?1.8f:0;
                    character.Pose(!seated,speed,crouched,grounded,frame%2==0,false,swim,climb);
                    character.ApplyTraversalPose(swim,climb,speed,false);
                    if(swim || climb)
                    {
                        var traversal=Snapshot(character);character.ApplyTraversalPose(swim,climb,speed,false);
                        foreach(var pose in traversal)Check(pose.Same(),"游泳攀登重复展示无旋转累积 "+pose.Bone.name);
                    }
                    float total=0;foreach(AnimationState state in animation){Check(state.weight>=0 && state.weight<=1,"动画权重界限");total+=state.weight;}
                    Check(Mathf.Abs(total-1)<.0001f,"混合权重始终归一");
                    if(phase==0 || phase==3 || phase==9)
                    {
                        foreach(bool isLeft in new[]{true,false})
                        {
                            var target=isLeft?left:right;
                            var palm=character.ShoulderPosition(isLeft)+character.transform.right*character.HandSide(isLeft)*.06f
                                +character.transform.forward*(.22f+.03f*Mathf.Sin(frame*.2f))-character.transform.up*.2f;
                            target.SetPose(palm,character.transform.rotation,phase==9?.6f:1);
                        }
                    }
                    else{left.Disable();right.Disable();}
                    character.ApplyHandTargets(left,right);
                    var snapshot=Snapshot(character);character.ApplyHandTargets(left,right);
                    foreach(var pose in snapshot)Check(pose.Same(),"同帧工具/抄鱼/握持 IK 幂等 "+pose.Bone.name);
                    CheckGeometry(character,baked,lengths.ToArray());
                    if(frame==68)
                    {
                        character.Pose(true,0,true,true,false,true);var frozen=Snapshot(character);
                        for(int pause=0;pause<8;pause++)
                        {
                            yield return null;character.Pose(true,0,true,true,false,true);character.ApplyHandTargets(null,null);
                            foreach(var pose in frozen)Check(pose.Same(),"暂停不推进站蹲权重或叠加骨骼 "+pose.Bone.name);
                        }
                    }
                    yield return null;
                    // 下一真实帧尚未调用 Pose；自动动画不得覆盖上一晚帧骨骼。
                    if(frame!=68)foreach(var pose in snapshot)Check(pose.Same(),"引擎不覆盖晚帧 IK "+pose.Bone.name);
                }
                // 快速 Walk→Run→Walk 反向必须以新目标继续淡入，而不是误认淡出的 Walk 正在播放。
                foreach(float speed in new[]{2.6f,4.4f,2.6f})
                {yield return null;character.Pose(true,speed,false,true,false,false);}
                for(int frame=0;frame<120;frame++)
                {yield return null;character.Pose(true,2.6f,false,true,false,false);}
                foreach(AnimationState state in animation)
                    Check(Mathf.Abs(state.weight-(state.name.EndsWith("Walk",StringComparison.OrdinalIgnoreCase)?1:0))<.001f,"快速动作反向收敛到最后 Walk 目标");
                for(int frame=0;frame<120;frame++)
                {
                    character.Pose(true,0,false,true,false,false);
                    character.ApplyTraversalPose(false,false,0,false);
                    float reclining=Mathf.Sin(frame/119f*Mathf.PI);float seated=1-reclining;
                    var rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(-90,0,0),reclining);
                    var feet=root.transform.position+new Vector3(0,.8f*reclining,0);
                    var hip=root.transform.position+new Vector3(0,.65f,.15f);
                    character.ApplySleepPose(feet,rotation,seated,hip,1);var snapshot=Snapshot(character);
                    character.ApplySleepPose(feet,rotation,seated,hip,1);
                    foreach(var pose in snapshot)Check(pose.Same(),"睡眠重复展示无旋转累积 "+pose.Bone.name);
                    CheckGeometry(character,baked,lengths.ToArray());yield return null;
                }
                character.Pose(true,0,false,true,false,true);var restored=Snapshot(character);
                character.ApplySleepPose(root.transform.position,Quaternion.Euler(-90,0,0),0,root.transform.position,1);
                character.Pose(true,0,false,true,false,true);
                foreach(var pose in restored)Check(pose.Same(),"离床恢复完整局部 TRS "+pose.Bone.name);
                Debug.Log("[CHARACTER-MOTION] 360帧站坐蹲、走跑跳、双手握持/抄鱼、游泳攀登、暂停恢复及120帧睡眠验证通过");
            }
            finally{UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(goals);UnityEngine.Object.Destroy(baked);}
        }
    }
}
