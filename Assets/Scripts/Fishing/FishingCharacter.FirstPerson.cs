using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        private Mesh _firstPersonMesh;
        public SkinnedMeshRenderer FirstPersonSkin { get; private set; }
        public int HiddenNeckTriangles { get; private set; }
        public int HiddenTorsoTriangles { get; private set; }

        private void BuildFirstPersonSkin()
        {
            // 本地保留同源手臂、腰部与腿；完整世界身体继续投影，不通过缩头骨规避穿模。
            var source=BodySkin.sharedMesh;var weights=source.boneWeights;var bones=BodySkin.bones;
            var neckBones=new bool[bones.Length];var armBones=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                neckBones[i]=bones[i]!=null && (bones[i].name=="Neck" || bones[i].name=="Head");
                armBones[i]=bones[i]!=null && (bones[i].IsChildOf(_leftArm.Upper) || bones[i].IsChildOf(_rightArm.Upper));
            }
            var neck=new bool[source.vertexCount];var torso=new bool[source.vertexCount];
            var baked=new Mesh();
            try
            {
                BodySkin.BakeMesh(baked);var vertices=baked.vertices;
                float waist=transform.InverseTransformPoint(_body.position).y;
                for(int i=0;i<neck.Length;i++)
                {
                    var w=weights[i];
                    neck[i]=Weight(neckBones,w)>.08f;
                    var p=transform.InverseTransformPoint(BodySkin.transform.TransformPoint(vertices[i]));
                    torso[i]=p.y>waist && Weight(armBones,w)<.25f;
                }
            }
            finally{Destroy(baked);}
            _firstPersonMesh=Instantiate(source);_firstPersonMesh.name="Beach body • local arms and lower body";
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var triangles=source.GetTriangles(sub);var visible=new List<int>(triangles.Length);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    if(neck[triangles[i]] || neck[triangles[i+1]] || neck[triangles[i+2]]){HiddenNeckTriangles++;continue;}
                    if(torso[triangles[i]] || torso[triangles[i+1]] || torso[triangles[i+2]]){HiddenTorsoTriangles++;continue;}
                    visible.Add(triangles[i]);visible.Add(triangles[i+1]);visible.Add(triangles[i+2]);
                }
                _firstPersonMesh.SetTriangles(visible,sub,false);
            }
            if(HiddenNeckTriangles==0 || HiddenTorsoTriangles==0)throw new InvalidOperationException("源人物没有可识别的头颈/躯干区域，拒绝静默跳过第一人称遮罩");
            var go=new GameObject("Beach_Body_FirstPerson");go.layer=BodySkin.gameObject.layer;
            go.transform.SetParent(BodySkin.transform,false);
            FirstPersonSkin=go.AddComponent<SkinnedMeshRenderer>();
            FirstPersonSkin.sharedMesh=_firstPersonMesh;FirstPersonSkin.sharedMaterials=BodySkin.sharedMaterials;
            FirstPersonSkin.bones=bones;FirstPersonSkin.rootBone=BodySkin.rootBone;
            FirstPersonSkin.localBounds=_bodyPresentationBounds;FirstPersonSkin.updateWhenOffscreen=true;
            FirstPersonSkin.allowOcclusionWhenDynamic=false;FirstPersonSkin.receiveShadows=BodySkin.receiveShadows;
            FirstPersonSkin.shadowCastingMode=ShadowCastingMode.Off;FirstPersonSkin.enabled=false;
            BuildWaistCover();
        }
        private static float Weight(bool[] selected,BoneWeight w)
        {
            return One(w.boneIndex0,w.weight0)+One(w.boneIndex1,w.weight1)+One(w.boneIndex2,w.weight2)+One(w.boneIndex3,w.weight3);
            float One(int index,float amount)=>index>=0 && index<selected.Length && selected[index]?amount:0;
        }
        private void SetBodyPresentation(bool firstPerson)
        {
            // XR 不借用桌面身体遮罩，也不改变追踪相机投影或控制器标定。
            bool local=firstPerson && !XRSettings.isDeviceActive;
            foreach(var renderer in _renderers)renderer.shadowCastingMode=ShadowCastingMode.On;
            HeadSkin.shadowCastingMode=firstPerson?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
            BodySkin.shadowCastingMode=local?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
            FirstPersonSkin.enabled=local;FirstPersonSkin.forceRenderingOff=false;
            WaistCover.enabled=local;WaistCover.forceRenderingOff=false;
        }
        public void SetOpticalView(bool observing)
        {
            // 目镜观察仅展示光学视野，第三人称中的身体和镜筒仍正常存在。
            if(FirstPersonSkin==null)return;
            bool local=observing && FirstPersonSkin.enabled;
            FirstPersonSkin.forceRenderingOff=local;WaistCover.forceRenderingOff=local;
            foreach(var renderer in _renderers)
                if(renderer!=BodySkin && renderer!=HeadSkin)renderer.shadowCastingMode=local?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
        }
        private void OnDestroy()
        {
            if(_firstPersonMesh!=null)Destroy(_firstPersonMesh);
            if(_waistMesh!=null)Destroy(_waistMesh);
        }
    }
}
