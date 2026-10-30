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
        /// <summary>Injected 90 Hz controller speeds drive the real VR cast and stick-reel recognisers.</summary>
        private static void XRGestureChecks()
        {
            float Cast(float peak,bool release=true)
            {
                var g=new FishingCastGesture();const float dt=1/90f;g.Step(true,false,0,dt);bool cast=false;
                for(int i=0;i<30;i++)cast|=g.Step(true,true,peak*Mathf.Sin(i/29f*Mathf.PI),dt);
                if(release)cast|=g.Step(true,false,0,dt);return cast?g.Power:-1;
            }
            Check(Cast(0)>0 && Cast(.6f)>0,"A deliberate trigger charge and release casts even with a stationary wrist");
            Check(Cast(2f)<0==false && Cast(3.5f)>Cast(1.6f),"A forward flick casts, and a faster flick casts farther");
            Check(Cast(3f,false)<0,"Holding the trigger keeps the line until it is released");
            float last=float.NaN,swept=0;
            for(int i=0;i<36;i++){float a=i*10*Mathf.Deg2Rad;swept+=FishingHands.StickSweep(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),ref last);}
            Check(swept>330,"Circling the right stick reels continuously, swept "+swept);
            last=float.NaN;swept=0;for(int i=0;i<36;i++)swept+=FishingHands.StickSweep(new Vector2(.3f*Mathf.Cos(i),.3f*Mathf.Sin(i)),ref last);
            Check(swept==0,"Stick drift near centre never reels");
            var o=Vector3.zero;var far=Vector3.one*5;var handle=new Vector3(0,1,0);var stand=new Vector3(0,.88f,0);var crank=new Vector3(1,1.6f,0);
            FishingXRGrab.FarmHand D(bool carrying,bool press,bool held,bool left,Vector3 r,Vector3 l,float tilt,float spout)=>FishingXRGrab.DecideFarm(carrying,press,held,left,r,l,handle,stand,crank,tilt,spout);
            Check(D(false,true,true,false,handle,far,0,5)==FishingXRGrab.FarmHand.TakeCan && D(false,true,true,false,far,far,0,5)==FishingXRGrab.FarmHand.None,"VR: only a grip closed on the can handle takes it");
            Check(D(true,false,false,false,handle,far,0,5)==FishingXRGrab.FarmHand.ReturnCan && D(true,false,true,false,handle,far,0,5)==FishingXRGrab.FarmHand.None,"VR: opening the hand over the stand returns the can; holding keeps it");
            Check(D(true,false,true,true,far,crank,0,5)==FishingXRGrab.FarmHand.Refill && D(true,false,true,true,far,far,0,5)==FishingXRGrab.FarmHand.None,"VR: the left hand gripping the windlass crank draws water");
            Check(D(true,false,true,false,far,far,55,.5f)==FishingXRGrab.FarmHand.Water && D(true,false,true,false,far,far,20,.5f)==FishingXRGrab.FarmHand.None && D(true,false,true,false,far,far,55,2)==FishingXRGrab.FarmHand.None,"VR: tipping the spout over a plot waters it; upright or away does not");
            var head=new GameObject("validation head").transform;
            try
            {
                head.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.Euler(30,90,0));var hang=FishingPrototype.HandLandingPoint(head);
                Check(Mathf.Abs(Vector3.Distance(new Vector3(hang.x,0,hang.z),Vector3.zero)-.5f)<.001f && hang.y<1.6f && hang.x>.49f,"The landed fish hangs 0.5 m in front of the face, below the eyes, regardless of head pitch");
                Check(FishingCatchRig.HandLandingReach(hang+Vector3.right*.1f,hang) && !FishingCatchRig.HandLandingReach(hang+Vector3.down*.4f,hang),"Only a hand closing on the fish takes it");
                Check(FishingCatchRig.HandLandingTimeout==8,"An untaken fish is landed after 8 s");
            }
            finally{UnityEngine.Object.DestroyImmediate(head.gameObject);}
        }
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V037-HANDS] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            XRGestureChecks();
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
                        bool allocationCounter=before>0;
                        Debug.Log("[V037-HANDS] COST grip skin solve averageMs="+(timer.Elapsed.TotalMilliseconds/30).ToString("F3")+" allocatedBytes="+(allocationCounter?allocated.ToString():"NA"));
                        if(allocationCounter)Check(allocated==0,"Warmed grip skin contact solving allocates no managed memory");
                        else Debug.Log("[V037-HANDS] LIMIT: this Mono runtime exposes no thread allocation counter; zero is not an allocation measurement.");
                        Check(character.ContactSolveCount==solved,"Rigid palm/shaft geometry reuses cached source finger rotations");
                        Check(timer.Elapsed.TotalMilliseconds/30<.5,"A rigid grip reuses the solved finger pose within a 0.5 ms editor CPU budget");
                        var hand=c.Hands.Right;float radius=hand.GripRadius;var start=hand.GripStart;var end=hand.GripEnd;
                        hand.SetGripSurface(start,end,radius+.002f);character.ApplyHandTargets(c.Hands.Left,hand);
                        Check(character.ContactSolveCount>solved,"Changing the real handle radius invalidates the cached contact solve");
                        hand.SetGripSurface(start,end,radius);character.ApplyHandTargets(c.Hands.Left,hand);
                        Directory.CreateDirectory("Logs/V038");
                        File.WriteAllText("Logs/V038/grip-performance.txt","Version: "+FishingPrototype.Version+"\nUTC: "+DateTime.UtcNow.ToString("O")+"\nWarm mean: "+(timer.Elapsed.TotalMilliseconds/30).ToString("F3")+" ms\nManaged bytes for 30 calls: "+(allocationCounter?allocated.ToString():"NA")+"\nSame geometry reuses rotations; changed radius invalidates. Editor CPU timing, not headset FPS.\n");
                    }
                    string phase=(c.Farm.Busy?c.Farm.ActionHint.Split('·')[0]:"carry")+Mathf.FloorToInt(c.Farm.WorkProgress*5);
                    if(!seen.Add(phase))continue;
                    FishingV016Validation.Capture(c,"Logs/V037","hand-can-"+seen.Count.ToString("D2"));
                    CaptureGripSide(c,"hand-grip-side-"+seen.Count.ToString("D2"));
                    if(phase=="carry0" || (c.Farm.Busy && c.Farm.WorkProgress>=.4f && c.Farm.WorkProgress<.6f))
                        CaptureGripSide(c,"hand-grip-palm-"+seen.Count.ToString("D2"),true);
                    // While drawing water the can rests on the rim and both hands work the windlass and bucket.
                    bool canOnRim=c.Farm.Busy && c.Farm.ActionHint.StartsWith("摇井绳") && c.Farm.WorkProgress>.2f && c.Farm.WorkProgress<.85f;
                    if(canOnRim){Check(c.Farm.WellBucket!=null,"Refill hands are on the windlass/bucket: "+phase);continue;}
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
        internal static void InspectRod(FishingPrototype c,string phase)
        {
            var character=c.Shore.Character;var rod=c.Hands.Rod;var hand=c.Hands.Right;
            Check(hand.Visible && hand.HasGripSurface && Vector3.Distance(character.PalmPosition(false),hand.Palm.position)<.035f,
                "The visible right palm reaches the actual rod grip: "+phase);
            var palmRotation=character.PalmRotation(false);
            Check(Vector3.Dot(palmRotation*Vector3.forward,-rod.up)>.98f && Vector3.Dot(palmRotation*Vector3.up,rod.right)>.98f,
                "Rod knuckles point down with the back of the hand outside the handle: "+phase);
            var skin=character.BodySkin;var mesh=new Mesh();var nearest=new[]{float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue};
            try
            {
                var bones=skin.bones;
                foreach(string digit in new[]{"Index","Middle","Ring","Pinky"})
                {
                    var knuckle=Array.Find(bones,b=>b.name==digit+"2.R");var tip=Array.Find(bones,b=>b.name==digit+"4.R");
                    Check(Vector3.Dot(tip.position-knuckle.position,-rod.up)>.005f,"Actual "+digit+" joints wrap down around the rod: "+phase);
                }
                skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
                var shaft=hand.GripEnd-hand.GripStart;int tested=0,inside=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    var weight=weights[i];var p=skin.transform.TransformPoint(vertices[i]);
                    var closest=hand.GripStart+shaft*Mathf.Clamp01(Vector3.Dot(p-hand.GripStart,shaft)/shaft.sqrMagnitude);
                    float distance=Vector3.Distance(p,closest);bool finger=false;
                    for(int digit=0;digit<5;digit++)
                    {
                        string name=digit==0?"Index":digit==1?"Middle":digit==2?"Ring":digit==3?"Pinky":"Thumb";
                        bool belongs=(weight.weight0>.2f && bones[weight.boneIndex0].name.StartsWith(name,StringComparison.Ordinal) && bones[weight.boneIndex0].name.EndsWith(".R",StringComparison.Ordinal))
                            || (weight.weight1>.2f && bones[weight.boneIndex1].name.StartsWith(name,StringComparison.Ordinal) && bones[weight.boneIndex1].name.EndsWith(".R",StringComparison.Ordinal));
                        if(!belongs)continue;finger=true;nearest[digit]=Mathf.Min(nearest[digit],distance);
                    }
                    if(finger){tested++;if(distance<hand.GripRadius-.004f)inside++;}
                }
                Debug.Log("[V037-HANDS] ROD "+phase+" digitDistances="+string.Join(",",nearest)+" radius="+hand.GripRadius+" inside="+inside);
                Check(tested>20 && inside==0,"Actual rod finger skin clears the solid handle core: "+phase);
                for(int digit=0;digit<5;digit++)Check(nearest[digit]<hand.GripRadius+.022f,"Every digit contacts the rod rather than closing in empty air: "+digit+" "+phase);
            }
            finally{UnityEngine.Object.Destroy(mesh);}
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
