using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingPickaxeValidation
    {
        public static void Run()=>FishingV011Validation.Begin("PICKAXE");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[PICKAXE] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            const string output="Logs/UserPickaxe";Directory.CreateDirectory(output);
            var mesh=FishingUserTools.Mesh(FieldItem.IronPickaxe);var material=FishingUserTools.Material(FieldItem.IronPickaxe);
            Debug.Log("[PICKAXE] IMPORT: triangles="+(mesh==null?-1:mesh.triangles.Length/3)+" bounds="+(mesh==null?"missing":mesh.bounds.ToString()));
            Check(mesh!=null && mesh.triangles.Length/3>2800 && mesh.triangles.Length/3<4000 && Mathf.Abs(mesh.bounds.size.y-.58f)<.005f,"real supplied compact head/wood mesh, no display background");
            Check(material!=null && material.shader.name=="Universal Render Pipeline/Lit" && material.GetTexture("_BaseMap")!=null && material.GetTexture("_BumpMap")!=null,"supplied wood/metal source atlas and normals use URP material");
            Debug.Log("[PICKAXE] UV: count="+mesh.uv.Length+" first="+mesh.uv[0]+" last="+mesh.uv[mesh.uv.Length-1]);
            var albedo=(Texture2D)material.GetTexture("_BaseMap");int woodSamples=0;
            for(int i=0;i<mesh.vertexCount;i++)
            {
                var v=mesh.vertices[i];if(v.y<0 || v.y>.2f)continue;
                var uv=mesh.uv[i];var colour=albedo.GetPixelBilinear(uv.x,uv.y);
                if(colour.r>colour.b*1.4f && colour.r>.2f)woodSamples++;
            }
            Debug.Log("[PICKAXE] WOOD SAMPLES: "+woodSamples);
            Check(woodSamples>10,"imported wooden shaft UV samples preserve the actual brown wood atlas");
            Check(mesh.bounds.Contains(FishingFieldWorld.PickTip) && Mathf.Abs(FishingFieldWorld.PickPalm.y)<.01f,"The contact is on the supplied head and the surface mount is beside the wooden shaft");
            c.ReturnToHarbor();var rb=c.Player.engine.RB;rb.position=c.Shore.Sites[0].Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"production shore landing for pick pose");
            Check(c.FieldWorld.TryLocation(c.Shore.Sites[0].Landing,4,90,out var feet) && c.Shore.Relocate(feet+Vector3.up*.05f,Quaternion.identity),"pick stance uses dry clear footing");
            if(!c.FirstPerson)c.ToggleView();c.View.ResetLook();
            for(int i=0;i<25;i++){c.Shore.Move(Vector2.zero,false,false,false,.02f);c.PresentCharacterFrame();yield return null;}
            var character=c.Shore.Character;float reach=character.ArmReach(false);
            foreach(var item in new[]{FieldItem.StonePickaxe,FieldItem.IronPickaxe})
            {
                Check(c.Career.Backpack.TryAdd(item,1),"fixture owns supplied pick "+item);int slot=-1;
                for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==item){slot=i;break;}
                if(slot!=1)Check(c.Career.Backpack.Move(slot,1),"equip actual pick item "+item);
                c.SelectFieldSlot(1);c.FieldWorld.ClearToolPresentation();
                foreach(bool crouch in new[]{false,true})
                {
                    c.Shore.SetCrouch(crouch);
                    for(int i=0;i<30;i++){c.Shore.Move(Vector2.zero,false,false,crouch,.02f);c.PresentCharacterFrame();yield return null;}
                    foreach(float pitch in new[]{-30f,0f,30f})
                    {
                        c.View.ResetLook();c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));
                        // Looking down lowers the tool; let the v0.41 IK jump-smoothing (0.1 s half-life) settle before measuring.
                        for(int i=0;i<30;i++){c.PresentCharacterFrame();yield return null;}
                        Check(c.FieldWorld.PresentedItem==item && c.FieldWorld.HeldTool.gameObject.activeInHierarchy,"supplied pick is held "+item+" crouch="+crouch+" pitch="+pitch);
                        Check(c.FieldWorld.HeldTool.GetComponent<MeshFilter>().sharedMesh.vertexCount==mesh.vertexCount && c.FieldWorld.HeldToolRenderer.sharedMaterial==material,"held model and material are the supplied asset");
                        var forearm=character.RightWrist.position-character.ElbowPosition(false);
                        float wristBend=Vector3.Angle(forearm,character.PalmRotation(false)*Vector3.forward);
                        float elbow=Vector3.Angle(character.ShoulderPosition(false)-character.ElbowPosition(false),forearm);
                        Debug.Log("[PICKAXE] POSE: "+item+" crouch="+crouch+" pitch="+pitch+" wristBend="+wristBend+" elbow="+elbow+" palm="+c.viewCamera.WorldToViewportPoint(c.Hands.Right.Palm.position)
                            +" targetBend="+Vector3.Angle(forearm,c.Hands.Right.Palm.rotation*Vector3.forward)+" targetError="+Quaternion.Angle(c.Hands.Right.Palm.rotation,character.PalmRotation(false))
                            +" shoulder="+(character.ShoulderPosition(false)-c.Shore.Actor.position).ToString("F3")+" palmLocal="+(c.Hands.Right.Palm.position-c.Shore.Actor.position).ToString("F3")+" lean="+character.AppliedInteractionLean.ToString("F3")
                            +" forearmLocal="+c.Shore.Actor.InverseTransformDirection(forearm.normalized).ToString("F3")+" palmFwdLocal="+c.Shore.Actor.InverseTransformDirection(character.PalmRotation(false)*Vector3.forward).ToString("F3")+" palmUpLocal="+c.Shore.Actor.InverseTransformDirection(character.PalmRotation(false)*Vector3.up).ToString("F3"));
                        Check(wristBend<40 && elbow>35 && elbow<170,"wrist/hinged elbow retain neutral resting range without sharp wrist bend");
                        if(pitch==0)
                        {
                            var palm=c.viewCamera.WorldToViewportPoint(character.PalmPosition(false));
                            Check(palm.z>.18f && palm.x>.04f && palm.x<.96f && palm.y>.025f && palm.y<.56f,"supplied pick grip remains in the lower visible screen area when looking level");
                        }
                        Check(Mathf.Abs(character.ArmReach(false)-reach)<.003f && Vector3.Distance(character.PalmPosition(false),c.Hands.Right.Palm.position)<.03f,"source arm lengths preserved and palm reaches the calibrated wooden grip · reachDelta="+Mathf.Abs(character.ArmReach(false)-reach)+" palmError="+Vector3.Distance(character.PalmPosition(false),c.Hands.Right.Palm.position)+" target="+(c.Hands.Right.Palm.position-character.ShoulderPosition(false)).magnitude);
                        FishingV016Validation.Capture(c,output,item+"-grip-"+(crouch?"crouched":"standing")+"-"+pitch);
                        FishingV038Validation.InspectGrip(c,"pick "+item+" crouch="+crouch+" pitch="+pitch);
                        foreach(var vertex in mesh.vertices)
                        {
                            var point=c.viewCamera.transform.InverseTransformPoint(c.FieldWorld.HeldTool.TransformPoint(vertex));
                            CheckNear(point,c.viewCamera.nearClipPlane);
                        }
                        FishingV016Validation.Capture(c,output,item+"-"+(crouch?"crouched":"standing")+"-"+pitch);
                    }
                }
                c.Shore.SetCrouch(false);c.View.ResetLook();c.ToggleView();
                for(int i=0;i<10;i++){c.PresentCharacterFrame();yield return null;}
                Check(c.FieldWorld.PresentedItem==item,"third person uses same source pick and real arm rig");
                FishingV016Validation.Capture(c,output,item+"-third-person");c.ToggleView();
                c.ToggleView();c.PresentCharacterFrame();
                var look=character.HandReferencePosition;
                c.viewCamera.transform.SetPositionAndRotation(look+character.transform.right*1.7f+Vector3.up*.6f-character.transform.forward*.7f,Quaternion.LookRotation(look-(look+character.transform.right*1.7f+Vector3.up*.6f-character.transform.forward*.7f)));
                FishingV016Validation.Capture(c,output,item+"-side-view");c.ToggleView();
                InspectTrackedPick(c,item,output);
            }
        }
        private static void InspectTrackedPick(FishingPrototype c,FieldItem item,string output)
        {
            var character=c.Shore.Character;var skin=character.FirstPersonSkin;
            var originalSkin=skin.sharedMesh;bool originalRecalc=skin.forceMatrixRecalculationPerRender;
            bool originalBodyHidden=character.BodySkin.forceRenderingOff;
            var camera=c.viewCamera;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;float fov=camera.fieldOfView;
            const string key="FishingXR.RightPalmXY";bool hadPreference=PlayerPrefs.HasKey(key);float prior=PlayerPrefs.GetFloat(key);
            c.PresentCharacterFrame();
            var held=c.FieldWorld.HeldTool;var originalMesh=held.GetComponent<MeshFilter>().sharedMesh;
            var originalVertices=originalMesh.vertices;
            var frame=Quaternion.Euler(0,character.transform.eulerAngles.y,0);
            var palm=character.ShoulderPosition(false)+frame*new Vector3(.025f,-.08f,.30f);
            var controllerPoses=new[]{Vector3.zero,new Vector3(45,20,60),new Vector3(-35,-60,135),new Vector3(75,30,-90)};
            try
            {
                skin.sharedMesh=character.TrackedHandsMesh;skin.forceMatrixRecalculationPerRender=true;
                character.BodySkin.forceRenderingOff=true;
                foreach(float calibration in new[]{-30f,0,30})
                {
                    FishingHands.SetControllerRoll(false,calibration);
                    for(int pose=0;pose<controllerPoses.Length;pose++)
                    {
                        var controller=frame*Quaternion.Euler(controllerPoses[pose]);
                        var palmFrame=FishingHands.AlignControllerRotation(false,controller,frame)*Quaternion.Euler(0,0,-90);
                        Check(c.FieldWorld.ApplyTrackedPickPose(palm,palmFrame),"production XR pose accepts "+item+" calibration="+calibration+" pose="+pose);
                        character.ApplyHandTargets(c.Hands.Left,c.Hands.Right,true);
                        var hand=c.Hands.Right;var shaft=(hand.GripEnd-hand.GripStart).normalized;
                        Check(Mathf.Abs(Vector3.Dot(shaft,character.PalmRotation(false)*Vector3.forward))<.01f,
                            "the real shaft crosses the fist instead of running along the fingertips");
                        Check(Vector3.Distance(character.PalmPosition(false),palm)<.003f && Quaternion.Angle(character.PalmRotation(false),palmFrame)<.1f,
                            "XY calibration does not separate the tracked pick from its actual visible palm");
                        Check(held.GetComponent<MeshFilter>().sharedMesh==originalMesh && originalMesh.vertices[0]==originalVertices[0]
                            && originalMesh.vertices[originalVertices.Length-1]==originalVertices[originalVertices.Length-1],
                            "tracked mounting leaves the supplied pick mesh untouched");
                        InspectTrackedSkin(c,item+" calibration="+calibration+" pose="+pose);
                        FishingV038Validation.InspectGrip(c,"tracked-pick "+item+" "+calibration+" "+pose);
                        if(calibration==30 && (pose==0 || pose==1))
                        {
                            var centre=hand.Palm.position;
                            var offset=hand.Palm.right*.10f-hand.Palm.up*.15f+hand.Palm.forward*.24f;
                            camera.fieldOfView=62;
                            camera.transform.SetPositionAndRotation(centre+offset,Quaternion.LookRotation(-offset,Vector3.up));
                            FishingV016Validation.Capture(c,output,item+"-tracked-grip-"+pose);
                        }
                    }
                }
                var p=held.position;var q=held.rotation;
                Check(!c.FieldWorld.ApplyTrackedPickPose(new Vector3(float.NaN,0,0),Quaternion.identity)
                    && held.position==p && Quaternion.Angle(held.rotation,q)<.001f,"invalid tracked samples cannot corrupt an equipped pick");
            }
            finally
            {
                if(hadPreference)PlayerPrefs.SetFloat(key,prior);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
                skin.sharedMesh=originalSkin;skin.forceMatrixRecalculationPerRender=originalRecalc;
                character.BodySkin.forceRenderingOff=originalBodyHidden;
                camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);camera.fieldOfView=fov;
                c.PresentCharacterFrame();
            }
        }
        private static void InspectTrackedSkin(FishingPrototype c,string phase)
        {
            var hand=c.Hands.Right;var skin=c.Shore.Character.FirstPersonSkin;
            Check(skin.quality==SkinQuality.Bone2,"Contact validation uses the actual two-weight renderer: "+phase);
            var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bones=skin.bones;var bindposes=mesh.bindposes;
            var matrices=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bindposes[i];
            var visible=new HashSet<int>(mesh.triangles);
            var nearest=new[]{float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue};
            string[] names={"Index","Middle","Ring","Pinky","Thumb"};var axis=hand.GripEnd-hand.GripStart;
            int inside=0,samples=0;
            foreach(int i in visible)
            {
                var w=weights[i];int digit=-1;
                for(int d=0;d<5;d++)
                    if((w.weight0>.2f && bones[w.boneIndex0].name.StartsWith(names[d],StringComparison.Ordinal) && bones[w.boneIndex0].name.EndsWith(".R",StringComparison.Ordinal))
                        || (w.weight1>.2f && bones[w.boneIndex1].name.StartsWith(names[d],StringComparison.Ordinal) && bones[w.boneIndex1].name.EndsWith(".R",StringComparison.Ordinal))){digit=d;break;}
                if(digit<0)continue;samples++;
                // Match the renderer's Bone2 skinning rather than assuming a bake snapshot uses identical influences.
                float total=w.weight0+w.weight1;
                var p=(matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1)/total;
                var q=hand.GripStart+axis*Mathf.Clamp01(Vector3.Dot(p-hand.GripStart,axis)/axis.sqrMagnitude);
                float distance=Vector3.Distance(p,q);nearest[digit]=Mathf.Min(nearest[digit],distance);
                if(distance<hand.GripRadius-.002f)inside++;
            }
            Debug.Log("[PICKAXE] TRACKED SKIN "+phase+" inside="+inside+" distances="+string.Join(",",nearest));
            Check(samples>20 && inside==0,"visible tracked fingers clear the solid wooden shaft: "+phase);
            for(int d=0;d<5;d++)Check(nearest[d]<hand.GripRadius+.014f,
                "each visible digit contacts the pick, not just the pinky: "+names[d]+" "+phase);
        }
        private static void CheckNear(Vector3 point,float near)
        {
            if(Mathf.Abs(point.x)<.08f && Mathf.Abs(point.y)<.08f && point.z<near+.005f)
                throw new InvalidOperationException("Pick mesh crosses eye/near plane: "+point);
        }
    }
}
