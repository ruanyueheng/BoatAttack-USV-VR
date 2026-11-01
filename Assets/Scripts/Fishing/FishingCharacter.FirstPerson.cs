using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        private Mesh _firstPersonMesh,_xrFirstPersonMesh,_xrTabletopMesh,_tabletopMesh,_desktopMesh;
        public Mesh TrackedHandsMesh=>_xrFirstPersonMesh;
        public Mesh TabletopArmsMesh=>_tabletopMesh;
        public SkinnedMeshRenderer FirstPersonSkin { get; private set; }
        private bool _tabletopView;
        private readonly HashSet<Renderer> _headWear=new HashSet<Renderer>();
        public void SetTabletopView(bool active)=>_tabletopView=active;
        public int HiddenNeckTriangles { get; private set; }
        public int HiddenTorsoTriangles { get; private set; }

        private void BuildFirstPersonSkin()
        {
            // 桌面复用同源双臂，XR保留手与下肢；完整世界身体继续投影。
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
                // Hood, collar and hair skinned to the spine still sit above the shoulders, right under a lowered eye.
                float shoulders=transform.InverseTransformPoint((_leftArm.Upper.position+_rightArm.Upper.position)*.5f).y;
                foreach(var renderer in _renderers)
                    if(renderer!=BodySkin && renderer!=HeadSkin && transform.InverseTransformPoint(renderer.bounds.center).y>shoulders)_headWear.Add(renderer);
                for(int i=0;i<neck.Length;i++)
                {
                    var w=weights[i];
                    var p=transform.InverseTransformPoint(BodySkin.transform.TransformPoint(vertices[i]));
                    neck[i]=Weight(neckBones,w)>.08f || (p.y>shoulders && Weight(armBones,w)<.25f);
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
            // 桌面行走用全身感知：只去头颈，低头看到胸腹腿脚，而不是悬空双臂的袖口断面。
            _desktopMesh=Instantiate(source);_desktopMesh.name="Beach body • desktop body without head";
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var triangles=source.GetTriangles(sub);var visible=new List<int>(triangles.Length);
                for(int i=0;i<triangles.Length;i+=3)
                    if(!neck[triangles[i]] && !neck[triangles[i+1]] && !neck[triangles[i+2]]){visible.Add(triangles[i]);visible.Add(triangles[i+1]);visible.Add(triangles[i+2]);}
                _desktopMesh.SetTriangles(visible,sub,false);
            }
            _tabletopMesh=Instantiate(_firstPersonMesh);_tabletopMesh.name="Beach body • tabletop arms and hands";
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var triangles=_firstPersonMesh.GetTriangles(sub);var visible=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    if(Weight(armBones,weights[triangles[i]])<.1f && Weight(armBones,weights[triangles[i+1]])<.1f && Weight(armBones,weights[triangles[i+2]])<.1f)continue;
                    visible.Add(triangles[i]);visible.Add(triangles[i+1]);visible.Add(triangles[i+2]);
                }
                _tabletopMesh.SetTriangles(visible,sub,false);
            }
            _xrFirstPersonMesh=Instantiate(_firstPersonMesh);_xrFirstPersonMesh.name="Beach body • tracked hands and lower body";
            var handBones=new bool[bones.Length];var cutArm=new bool[source.vertexCount];
            for(int i=0;i<bones.Length;i++)handBones[i]=bones[i]!=null && (bones[i].IsChildOf(_leftArm.Wrist) || bones[i].IsChildOf(_rightArm.Wrist));
            for(int i=0;i<cutArm.Length;i++)cutArm[i]=Weight(armBones,weights[i])>.1f && Weight(handBones,weights[i])<.8f;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var triangles=_firstPersonMesh.GetTriangles(sub);var visible=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    if(cutArm[triangles[i]] || cutArm[triangles[i+1]] || cutArm[triangles[i+2]])continue;
                    visible.Add(triangles[i]);visible.Add(triangles[i+1]);visible.Add(triangles[i+2]);
                }
                _xrFirstPersonMesh.SetTriangles(visible,sub,false);
            }
            _xrTabletopMesh=Instantiate(_xrFirstPersonMesh);_xrTabletopMesh.name="Beach body • tracked tabletop hands";
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var triangles=_xrFirstPersonMesh.GetTriangles(sub);var visible=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                    if(Weight(handBones,weights[triangles[i]])>=.8f || Weight(handBones,weights[triangles[i+1]])>=.8f || Weight(handBones,weights[triangles[i+2]])>=.8f)
                    {visible.Add(triangles[i]);visible.Add(triangles[i+1]);visible.Add(triangles[i+2]);}
                _xrTabletopMesh.SetTriangles(visible,sub,false);
            }
            if(HiddenNeckTriangles==0 || HiddenTorsoTriangles==0)throw new InvalidOperationException("源人物没有可识别的头颈/躯干区域，拒绝静默跳过第一人称遮罩");
            var go=new GameObject("Beach_Body_FirstPerson");go.layer=BodySkin.gameObject.layer;
            go.transform.SetParent(BodySkin.transform,false);
            FirstPersonSkin=go.AddComponent<SkinnedMeshRenderer>();
            FirstPersonSkin.sharedMesh=_firstPersonMesh;FirstPersonSkin.sharedMaterials=BodySkin.sharedMaterials;
            FirstPersonSkin.bones=bones;FirstPersonSkin.rootBone=BodySkin.rootBone;
            FirstPersonSkin.quality=BodySkin.quality;
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
            // 同源近眼躯干遮罩也用于 XR；HMD 投影与实际手柄位置保持独立。
            bool local=firstPerson || _tabletopView;bool tracked=XRSettings.isDeviceActive;
            FirstPersonSkin.sharedMesh=tracked?(_tabletopView?_xrTabletopMesh:_xrFirstPersonMesh):_tabletopView?_tabletopMesh:_desktopMesh;BodySkin.forceRenderingOff=tracked;
            foreach(var renderer in _renderers)renderer.shadowCastingMode=local && (_tabletopView || _headWear.Contains(renderer)) && renderer!=BodySkin && renderer!=HeadSkin?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
            HeadSkin.shadowCastingMode=local?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
            BodySkin.shadowCastingMode=local?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
            FirstPersonSkin.enabled=local;FirstPersonSkin.forceRenderingOff=false;
            WaistCover.enabled=local && tracked && !_tabletopView;WaistCover.forceRenderingOff=false;
        }
        public void SetOpticalView(bool observing)
        {
            // 目镜观察仅展示光学视野，第三人称中的身体和镜筒仍正常存在。
            if(FirstPersonSkin==null)return;
            bool local=observing && FirstPersonSkin.enabled;
            FirstPersonSkin.forceRenderingOff=local;WaistCover.forceRenderingOff=local;
            foreach(var renderer in _renderers)
                if(renderer!=BodySkin && renderer!=HeadSkin)renderer.shadowCastingMode=local || (FirstPersonSkin.enabled && (_tabletopView || _headWear.Contains(renderer)))?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;
        }
        private void OnDestroy()
        {
            if(_firstPersonMesh!=null)Destroy(_firstPersonMesh);if(_xrFirstPersonMesh!=null)Destroy(_xrFirstPersonMesh);if(_xrTabletopMesh!=null)Destroy(_xrTabletopMesh);if(_tabletopMesh!=null)Destroy(_tabletopMesh);if(_desktopMesh!=null)Destroy(_desktopMesh);
            if(_waistMesh!=null)Destroy(_waistMesh);
        }
    }
}
