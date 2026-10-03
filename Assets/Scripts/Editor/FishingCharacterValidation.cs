using System;
using System.Collections;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.EditorTools
{
    /// <summary>Invoked by the isolated expedition runner; never rebuilds a fishing scene.</summary>
    public static class FishingCharacterValidation
    {
        public static void Run()=>FishingV011Validation.Begin("CHARACTER");
        private static void Check(bool ok,string label)
        {
            if(!ok)throw new InvalidOperationException("Character: "+label);
            Debug.Log("[CHARACTER] CHECK: "+label);
        }
        public static void CheckSource(FishingPrototype c)
        {
            var character=c.Shore.Character;
            Check(character!=null && character.Ready,"one ready source-skinned avatar");
            Check(character.LeftFingerJointCount==19 && character.RightFingerJointCount==19,"source fingers: four four-bone chains and one three-bone thumb per hand");
            var source=Resources.Load<GameObject>("FishingCharacter/BeachCharacter");
            SkinnedMeshRenderer body=null,head=null;
            foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
            {if(skin.name=="Beach_Body")body=skin;if(skin.name=="Beach_Head")head=skin;}
            Check(body!=null && head!=null && character.BodySkin.sharedMesh==body.sharedMesh && character.HeadSkin.sharedMesh==head.sharedMesh,"actual FBX body/head shared meshes, not replacement hand geometry");
            Check(character.BodySkin.sharedMaterial==body.sharedMaterial,"body and hands retain source material");
            Check(character.BodySkin.sharedMesh.vertexCount<12000,"source body/hand vertex budget");
            Check(c.Hands.Left.Root==c.Hands.Left.transform && c.Hands.Left.Palm==c.Hands.Left.transform
                && !c.Hands.Left.transform.IsChildOf(character.transform),"Root/Palm are independent goals, not IK output bones");
            Check(c.Hands.Left.GetComponentsInChildren<Renderer>(true).Length==0 && c.Hands.Right.GetComponentsInChildren<Renderer>(true).Length==0,"hand goals own no renderers");
            Check(c.GetComponentsInChildren<FishingGlove>(true).Length==0,"no legacy glove instances");
            var names=new HashSet<string>();foreach(var bone in character.BodySkin.bones)if(bone!=null)names.Add(bone.name);
            foreach(var side in new[]{".L",".R"})
            {
                Check(names.Contains("Wrist"+side),"source skin binds wrist "+side);
                foreach(var digit in new[]{"Index","Middle","Ring","Pinky","Thumb"})
                    for(int i=1;i<=(digit=="Thumb"?3:4);i++)Check(names.Contains(digit+i+side),"source skin binds "+digit+i+side);
            }
            Check(character.ArmReach(true)>.25f && character.ArmReach(true)<.9f && character.ArmReach(false)>.25f && character.ArmReach(false)<.9f,"actual imported arm lengths are human-sized after skin calibration");
        }
        public static IEnumerator CheckTargetExpiry()
        {
            var go=new GameObject("Isolated hand goal lifetime check");
            try
            {
                var target=go.AddComponent<FishingHandTarget>();target.Build(true);
                target.SetPose(Vector3.zero,Quaternion.identity,.8f);Check(target.Visible,"fresh goal participates this frame");
                yield return null;
                Check(!target.Visible,"unsupplied goal expires on the next real frame");
                target.Pose(1);Check(!target.Visible,"grip-only update cannot resurrect expired tracking");
            }
            finally {UnityEngine.Object.Destroy(go);}
        }
        public static void CheckVisibility(FishingPrototype c,bool firstPerson)
        {
            var character=c.Shore.Character;
            foreach(var renderer in character.GetComponentsInChildren<Renderer>())
                Check(renderer.enabled && renderer.gameObject.activeInHierarchy
                    && renderer.shadowCastingMode==(firstPerson && renderer==character.HeadSkin?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On),
                    (firstPerson?"FP head-only shadowing; body/arms visible: ":"TP complete avatar restored: ")+renderer.name);
            var head=Bone(character,"Head");Check(head.gameObject.activeInHierarchy && head.localScale.sqrMagnitude>1e-8f,"Head bone remains active and unscaled");
        }
        private static Transform Bone(FishingCharacter character,string name)
        {
            foreach(var bone in character.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;
            throw new InvalidOperationException("Missing source bone "+name);
        }
        private struct Pose
        {
            public Transform Bone;public Vector3 Position,Scale;public Quaternion Rotation;
            public Pose(Transform bone){Bone=bone;Position=bone.localPosition;Rotation=bone.localRotation;Scale=bone.localScale;}
            public bool Same()=>Vector3.Distance(Position,Bone.localPosition)<.00001f && Vector3.Distance(Scale,Bone.localScale)<.00001f && Quaternion.Angle(Rotation,Bone.localRotation)<.05f;
        }
        public static void CheckPoses(FishingPrototype c)
        {
            var character=c.Shore.Character;var left=c.Hands.Left;var right=c.Hands.Right;
            var baked=new Mesh();
            try
            {
                character.Pose(true,0,false,true,true,false);
                var before=new List<Pose>();foreach(var bone in character.LeftWrist.parent.parent.parent.GetComponentsInChildren<Transform>())before.Add(new Pose(bone));
                foreach(var bone in character.RightWrist.parent.parent.parent.GetComponentsInChildren<Transform>())before.Add(new Pose(bone));
                var leg=new Pose(Bone(character,"UpperLeg.L"));var footParent=Bone(character,"Foot.L").parent;
                var ankleParent=Bone(character,"Ankle.L").parent;
                var head=new Pose(Bone(character,"Head"));
                var leftPosition=character.ShoulderPosition(true)+character.transform.TransformDirection(new Vector3(character.HandSide(true)*.06f,-.2f,.22f));
                var rightPosition=character.ShoulderPosition(false)+character.transform.TransformDirection(new Vector3(character.HandSide(false)*.06f,-.2f,.22f));
                left.SetPose(leftPosition,character.transform.rotation,0);right.SetPose(rightPosition,character.transform.rotation,0);
                character.ApplyHandTargets(left,right);
                Check(Vector3.Distance(character.PalmPosition(true),leftPosition)<.025f && Vector3.Distance(character.PalmPosition(false),rightPosition)<.025f,"both palms reach independent goals");
                Check(Quaternion.Angle(character.PalmRotation(true),left.transform.rotation)<.1f && Quaternion.Angle(character.PalmRotation(false),right.transform.rotation)<.1f,"both source wrists use calibrated palm orientation");
                var indexOpen=Bone(character,"Index2.L").localRotation;var ringOpen=Bone(character,"Ring2.L").localRotation;
                character.BodySkin.BakeMesh(baked);var open=baked.vertices;
                left.Pose(1);right.Pose(.6f);character.ApplyHandTargets(left,right);character.BodySkin.BakeMesh(baked);var closed=baked.vertices;
                int changed=0;for(int i=0;i<open.Length;i++)if(character.BodySkin.transform.TransformVector(open[i]-closed[i]).sqrMagnitude>.000001f)changed++;
                Check(changed>100,"grip deforms actual same-source skinned vertices");
                var solved=new List<Pose>();foreach(var pose in before)solved.Add(new Pose(pose.Bone));
                character.ApplyHandTargets(left,right);foreach(var pose in solved)Check(pose.Same(),"repeat final solve is idempotent: "+pose.Bone.name);
                Check(Vector3.Distance(left.transform.position,leftPosition)<.00001f && Vector3.Distance(right.transform.position,rightPosition)<.00001f,"IK never moves tool goals");
                Check(leg.Same() && head.Same() && Bone(character,"Foot.L").parent==footParent && footParent.name=="Root"
                    && Bone(character,"Ankle.L").parent==ankleParent && ankleParent.name=="LowerLeg.L","arm overlay leaves head, leg IK and independent Foot hierarchy untouched");
                left.Pose(1,true);character.ApplyHandTargets(left,right);
                Check(Quaternion.Angle(Bone(character,"Index2.L").localRotation,indexOpen)<.05f
                    && Quaternion.Angle(Bone(character,"Ring2.L").localRotation,ringOpen)>10,"point leaves index open while other source fingers grip");
                float reach=character.ArmReach(false);var wristScale=character.RightWrist.localScale;
                var rootPosition=character.transform.position;
                right.SetPose(character.ShoulderPosition(false)+character.transform.forward*20,character.transform.rotation,1);
                character.ApplyHandTargets(left,right);
                Check(Vector3.Distance(character.RightWrist.position,character.ShoulderPosition(false))<=reach+.002f
                    && Vector3.Distance(character.RightWrist.localScale,wristScale)<.00001f && character.transform.position==rootPosition,"unreachable goal cannot stretch bones or move avatar root");
                left.Disable();right.Disable();character.ApplyHandTargets(left,right);
                foreach(var pose in before)Check(pose.Same(),"tracking loss restores sampled TRS: "+pose.Bone.name);
                // Repeated stance transitions must not accumulate changes on finger bones lacking curves.
                character.Pose(false,0,false,true,false,false);character.Pose(true,0,true,true,false,false);character.Pose(true,0,false,true,true,false);
                foreach(var pose in before)Check(pose.Same(),"stance round-trip restores arm base: "+pose.Bone.name);
                left.SetPose(leftPosition,character.transform.rotation,1);left.BeginFrame();
                Check(!left.Visible && left.Curl==0,"new frame clears previous owner/grip");
                right.SetPose(new Vector3(float.NaN,0,0),Quaternion.identity,1);Check(!right.Visible,"invalid goal releases IK");
                left.SetPose(leftPosition,character.transform.rotation,1);character.Pose(true,0,false,true,true,true);character.ApplyHandTargets(left,right);
                foreach(var pose in before)Check(pose.Same(),"pause restores animated arm base: "+pose.Bone.name);
            }
            finally
            {
                UnityEngine.Object.Destroy(baked);
                c.Shore.SyncRider();c.View.ApplyView();c.Hands.Apply();c.FieldWorld.ApplyHeldTool();c.CatchRig.Apply(0);
                character.ApplyHandTargets(left,right);
            }
        }
    }
}
