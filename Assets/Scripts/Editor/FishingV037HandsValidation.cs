using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV037HandsValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V037-HANDS");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V037-HANDS] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Logs/V037");
            var character=c.Shore.Character;
            Check(character.LeftFingerJointCount==19 && character.RightFingerJointCount==19,"All five digits retain the source-skinned joints on both hands");
            Check(character.BodySkin.quality==SkinQuality.Bone2 && character.FirstPersonSkin.quality==SkinQuality.Bone2,"Contact and both rendered player skins use the same project two-influence skinning");
            FishingCharacterValidation.CheckPoses(c);
            var farm=FishingV036FarmValidation.Checks(c);var seen=new HashSet<string>();bool measured=false;
            float maximumDeltaTime=Time.maximumDeltaTime;
            // Synchronous evidence renders must not skip the short return-to-stand contact phase.
            Time.maximumDeltaTime=1f/60;
            try
            {
                while(farm.MoveNext())
                {
                    yield return farm.Current;
                    if((!c.Farm.CarryingCan && !c.Farm.Busy) || !c.Hands.Right.HasGripSurface || c.Paused)continue;
                    c.PresentCharacterFrame();
                    if(!measured)
                    {
                        measured=true;
                        for(int warm=0;warm<5;warm++)character.ApplyHandTargets(c.Hands.Left,c.Hands.Right);
                        int solved=character.ContactSolveCount;
                        var timer=new System.Diagnostics.Stopwatch();
                        long before=GC.GetAllocatedBytesForCurrentThread();timer.Start();
                        for(int sample=0;sample<30;sample++)character.ApplyHandTargets(c.Hands.Left,c.Hands.Right);
                        timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                        Debug.Log("[V037-HANDS] COST grip skin solve averageMs="+(timer.Elapsed.TotalMilliseconds/30).ToString("F3")+" allocatedBytes="+allocated);
                        Check(allocated==0,"Warmed grip skin contact solving allocates no managed memory");
                        Check(character.ContactSolveCount==solved,"Rigid palm/shaft geometry reuses cached source finger rotations");
                        Check(timer.Elapsed.TotalMilliseconds/30<.5,"A rigid grip reuses the solved finger pose within a 0.5 ms editor CPU budget");
                        var hand=c.Hands.Right;float radius=hand.GripRadius;var start=hand.GripStart;var end=hand.GripEnd;
                        hand.SetGripSurface(start,end,radius+.002f);character.ApplyHandTargets(c.Hands.Left,hand);
                        Check(character.ContactSolveCount>solved,"Changing the real handle radius invalidates the cached contact solve");
                        hand.SetGripSurface(start,end,radius);character.ApplyHandTargets(c.Hands.Left,hand);
                        Directory.CreateDirectory("Logs/V038");
                        File.WriteAllText("Logs/V038/grip-performance.txt","Version: "+FishingPrototype.Version+"\nUTC: "+DateTime.UtcNow.ToString("O")+"\nWarm mean: "+(timer.Elapsed.TotalMilliseconds/30).ToString("F3")+" ms\nManaged bytes for 30 calls: "+allocated+"\nSame geometry reuses rotations; changed radius invalidates. Editor CPU timing, not headset FPS.\n");
                    }
                    string phase=(c.Farm.Busy?c.Farm.ActionHint.Split('·')[0]:"carry")+Mathf.FloorToInt(c.Farm.WorkProgress*5);
                    if(!seen.Add(phase))continue;
                    FishingV016Validation.Capture(c,"Logs/V037","hand-can-"+seen.Count.ToString("D2"));
                    CaptureGripSide(c,"hand-grip-side-"+seen.Count.ToString("D2"));
                    if(phase=="carry0" || (c.Farm.Busy && c.Farm.WorkProgress>=.4f && c.Farm.WorkProgress<.6f))
                        CaptureGripSide(c,"hand-grip-palm-"+seen.Count.ToString("D2"),true);
                    InspectCan(c,phase);
                    FishingV038Validation.InspectCan(c,phase);
                }
            }
            finally{try{(farm as IDisposable)?.Dispose();}finally{Time.maximumDeltaTime=maximumDeltaTime;}}
            Check(seen.Count>=8,"Real pickup, carry, refill, watering and return expose multiple solved contact phases");
            var craft=FishingV035Validation.Checks(c);
            try{while(craft.MoveNext())yield return craft.Current;}finally{(craft as IDisposable)?.Dispose();}
        }
        private static void CaptureGripSide(FishingPrototype c,string name,bool palm=false)
        {
            var camera=c.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;var can=c.Farm.WateringCan;
            try
            {
                var contact=can.TransformPoint(FishingFarmWorld.CanHandle);
                var offset=palm?can.right*.18f-can.up*.045f+can.forward*.30f:can.right*.5f+can.up*.20f-can.forward*.4f;
                camera.SetPositionAndRotation(contact+offset,Quaternion.LookRotation(-offset+(palm?can.up*.025f:Vector3.zero),can.up));
                FishingV016Validation.Capture(c,"Logs/V037",name);
            }
            finally{camera.SetPositionAndRotation(position,rotation);}
        }
        private static void InspectCan(FishingPrototype c,string phase)
        {
            var character=c.Shore.Character;var can=c.Farm.WateringCan;var hand=c.Hands.Right;
            Check(Vector3.Distance(character.PalmPosition(false),hand.Palm.position)<.065f,"The visible palm reaches its independent surface mount: "+phase);
            var mount=can.InverseTransformPoint(character.PalmPosition(false));
            Debug.Log("[V037-HANDS] MOUNT "+phase+" actual="+mount.ToString("F4")+" goal="+can.InverseTransformPoint(hand.Palm.position).ToString("F4")+" error="+c.Farm.CanGripError);
            Check(mount.y>FishingFarmWorld.CanHandle.y+.025f && Vector3.Dot(character.PalmRotation(false)*Vector3.up,can.up)>.98f,
                "Palm thickness clears the handle and the fingers close downwards: "+phase);
            var skin=character.BodySkin;var mesh=new Mesh();
            try
            {
                // Compensate renderer scale before TransformPoint; the default bake already includes that scale.
                skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                foreach(string digit in new[]{"Index","Middle","Ring","Pinky"})
                {
                    var a=Array.Find(bones,bone=>bone.name==digit+"2.R");
                    var b=Array.Find(bones,bone=>bone.name==digit+"3.R");
                    var d=Array.Find(bones,bone=>bone.name==digit+"4.R");
                    float bend=Vector3.Angle(b.position-a.position,d.position-b.position);
                    Check(bend>15,"Finger bends around the handle beyond proximal contact: "+digit+" degrees="+bend+" "+phase);
                }
                var source=skin.sharedMesh.vertices;var bind=skin.sharedMesh.bindposes;var matrices=new Matrix4x4[bones.Length];
                for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];
                float predictionError=0;
                int tested=0,insideHandle=0,insideBody=0;float nearest=float.MaxValue;var closestVertex=Vector3.zero;
                var nearestDigits=new[]{float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue};
                for(int i=0;i<vertices.Length;i++)
                {
                    var weight=weights[i];
                    float total=weight.weight0+weight.weight1;if(total<=0)continue;
                    weight.weight0/=total;weight.weight1/=total;
                    if(!IsFinger(bones[weight.boneIndex0],weight.weight0) && !IsFinger(bones[weight.boneIndex1],weight.weight1))continue;
                    var p=can.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));tested++;
                    var v=source[i];var predicted=matrices[weight.boneIndex0].MultiplyPoint3x4(v)*weight.weight0+matrices[weight.boneIndex1].MultiplyPoint3x4(v)*weight.weight1;
                    predictionError=Mathf.Max(predictionError,Vector3.Distance(predicted,skin.transform.TransformPoint(vertices[i])));
                    var closest=FishingFarmWorld.CanHandle;closest.x=Mathf.Clamp(p.x,-.09f,.06f);float distance=Vector3.Distance(p,closest);
                    for(int digit=0;digit<5;digit++)
                        if(IsDigit(bones[weight.boneIndex0],weight.weight0,digit) || IsDigit(bones[weight.boneIndex1],weight.weight1,digit))nearestDigits[digit]=Mathf.Min(nearestDigits[digit],distance);
                    if(distance<nearest){nearest=distance;closestVertex=p;}
                    if(distance<.007f){insideHandle++;if(insideHandle<7)Debug.Log("[V037-HANDS] HANDLE "+p.ToString("F4")+" bone="+bones[weight.boneIndex0].name+" weight="+weight.weight0);}
                    if(p.y<-.015f && p.y>-.21f && p.x*p.x+(p.z+.12f)*(p.z+.12f)/(.88f*.88f)<.095f*.095f)
                    {insideBody++;if(insideBody<7)Debug.Log("[V037-HANDS] INSIDE "+p.ToString("F4")+" bone="+bones[weight.boneIndex0].name+" weight="+weight.weight0);}
                }
                Debug.Log("[V037-HANDS] SKIN "+phase+" fingerVertices="+tested+" minimumHandleDistance="+nearest+" closest="+closestVertex.ToString("F4")+" insideHandle="+insideHandle+" deepInsideCan="+insideBody+" predictionError="+predictionError);
                Debug.Log("[V037-HANDS] DIGIT handle distances="+string.Join(",",nearestDigits));
                Check(predictionError<.001f,"Cached source-weight skin prediction matches Unity BakeMesh: "+phase);
                Check(tested>20 && nearest<.028f && insideHandle==0 && insideBody==0,"Actual finger skin grasps the handle while clearing its core and the can interior: "+phase);
                for(int digit=0;digit<5;digit++)Check(nearestDigits[digit]<(digit==4?.035f:.028f),"Every digit participates in the handle grasp: "+digit+" "+phase);
            }
            finally{UnityEngine.Object.Destroy(mesh);}
            bool IsFinger(Transform bone,float weight)=>weight>.2f && bone!=null && bone.name.EndsWith(".R",StringComparison.Ordinal)
                && (bone.name.StartsWith("Index",StringComparison.Ordinal)||bone.name.StartsWith("Middle",StringComparison.Ordinal)
                    ||bone.name.StartsWith("Ring",StringComparison.Ordinal)||bone.name.StartsWith("Pinky",StringComparison.Ordinal)||bone.name.StartsWith("Thumb",StringComparison.Ordinal));
            bool IsDigit(Transform bone,float weight,int digit)=>weight>.2f && bone!=null && bone.name.EndsWith(".R",StringComparison.Ordinal)
                && bone.name.StartsWith(digit==0?"Index":digit==1?"Middle":digit==2?"Ring":digit==3?"Pinky":"Thumb",StringComparison.Ordinal);
        }
    }
}
