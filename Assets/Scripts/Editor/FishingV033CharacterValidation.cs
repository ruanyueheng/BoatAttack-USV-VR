using System;
using System.Collections;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV033CharacterValidation
    {
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V033-CHARACTER] CHECK: "+label);}
        private static Transform Bone(FishingCharacter actor,string name){foreach(var t in actor.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new InvalidOperationException("Missing bone "+name);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            var go=new GameObject("V033 isolated consecutive-frame character");var actor=go.AddComponent<FishingCharacter>();actor.Build();
            try
            {
                Check(actor.Ready,"Supplied mesh imports its complete game skeleton and legacy clips");
                var animation=actor.GetComponentInChildren<Animation>();Check(!animation.enabled,"Legacy sampling has one owner after tool/IK presentation");
                var body=Bone(actor,"Body");var shin=Bone(actor,"LowerLeg.L");var thigh=Bone(actor,"UpperLeg.L");var ankle=Bone(actor,"Ankle.L");
                actor.Pose(true,0,false,true,false,false);yield return null;actor.Pose(true,0,false,true,false,false);
                float leg=Vector3.Distance(thigh.position,shin.position);var restScale=shin.localScale;
                foreach(float speed in new[]{2.6f,4.4f,0f,2.6f})
                {
                    for(int i=0;i<12;i++){yield return null;actor.Pose(true,speed,false,true,false,false);}
                    float weights=0;foreach(AnimationState state in animation)weights+=state.weight;
                    Check(Mathf.Abs(weights-1)<.002f,"Walk/run/idle crossfade has normalized weights at speed "+speed);
                    Check(Mathf.Abs(Vector3.Distance(thigh.position,shin.position)-leg)<.04f,"Locomotion preserves upper-leg length at speed "+speed);
                }
                actor.Pose(true,0,true,true,false,false);yield return null;actor.Pose(true,0,true,true,false,false);
                var paused=body.localPosition;for(int i=0;i<5;i++){yield return null;actor.Pose(true,0,true,true,false,true);}
                Check(Vector3.Distance(paused,body.localPosition)<.0001f,"Pause freezes crouch transition rather than snapping stance");
                for(int i=0;i<20;i++){yield return null;actor.Pose(true,0,false,true,false,false);}
                Check(actor.IsStanceSettled(true,false),"Crouch resumes and returns smoothly to standing");
                foreach(bool swim in new[]{true,false,true,false})
                {
                    yield return null;actor.Pose(true,2,false,true,false,false,swim,!swim);actor.ApplyTraversalPose(swim,!swim,2,false);
                    var before=body.localRotation;actor.ApplyTraversalPose(swim,!swim,2,false);
                    Check(Quaternion.Angle(before,body.localRotation)<.001f,"Repeated traversal presentation does not accumulate twisting");
                }
                for(int i=0;i<20;i++){yield return null;actor.Pose(true,0,false,true,false,false);actor.ApplyTraversalPose(false,false,0,false);}
                Check(shin.localScale==restScale && Vector3.Distance(thigh.position,shin.position)<leg+.05f,"Leaving traversal restores scale and leg proportions");
                actor.ApplySleepPose(go.transform.position,Quaternion.identity,.5f,go.transform.position+Vector3.up*.7f,1);
                yield return null;actor.Pose(true,0,false,true,false,false);actor.ApplyTraversalPose(false,false,0,false);
                Check(Vector3.Distance(thigh.position,shin.position)<leg+.05f,"Sleep overlay does not contaminate the next standing pose");
                int vertices=0;var mesh=new Mesh();
                try
                {
                    foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        Check(skin.sharedMesh!=null && skin.bones.Length==skin.sharedMesh.bindposes.Length,"Skin bone indices match bind-pose count: "+skin.name);
                        skin.BakeMesh(mesh);var points=new List<Vector3>();mesh.GetVertices(points);
                        foreach(var p in points){CheckFinite(p);vertices++;}
                    }
                }
                finally{UnityEngine.Object.Destroy(mesh);}
                Check(vertices>100,"All sampled skin vertices remain finite after high-risk action transitions");
                Check(Vector3.Distance(shin.position,ankle.position)>.1f,"Knee-to-ankle chain remains physically connected");
            }
            finally{UnityEngine.Object.Destroy(go);}
        }
        private static void CheckFinite(Vector3 p){if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))throw new InvalidOperationException("Nonfinite skinned vertex");}
    }
}
