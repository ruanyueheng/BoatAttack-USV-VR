using System;
using System.Collections;
using System.IO;
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
                        for(int i=0;i<4;i++){c.PresentCharacterFrame();yield return null;}
                        Check(c.FieldWorld.PresentedItem==item && c.FieldWorld.HeldTool.gameObject.activeInHierarchy,"supplied pick is held "+item+" crouch="+crouch+" pitch="+pitch);
                        Check(c.FieldWorld.HeldTool.GetComponent<MeshFilter>().sharedMesh.vertexCount==mesh.vertexCount && c.FieldWorld.HeldToolRenderer.sharedMaterial==material,"held model and material are the supplied asset");
                        var forearm=character.RightWrist.position-character.ElbowPosition(false);
                        float wristBend=Vector3.Angle(forearm,character.PalmRotation(false)*Vector3.forward);
                        float elbow=Vector3.Angle(character.ShoulderPosition(false)-character.ElbowPosition(false),forearm);
                        Debug.Log("[PICKAXE] POSE: "+item+" crouch="+crouch+" pitch="+pitch+" wristBend="+wristBend+" elbow="+elbow+" palm="+c.viewCamera.WorldToViewportPoint(c.Hands.Right.Palm.position));
                        Check(wristBend<40 && elbow>35 && elbow<170,"wrist/hinged elbow retain neutral resting range without sharp wrist bend");
                        if(pitch==0)
                        {
                            var palm=c.viewCamera.WorldToViewportPoint(character.PalmPosition(false));
                            Check(palm.z>.18f && palm.x>.04f && palm.x<.96f && palm.y>.025f && palm.y<.56f,"supplied pick grip remains in the lower visible screen area when looking level");
                        }
                        Check(Mathf.Abs(character.ArmReach(false)-reach)<.003f && Vector3.Distance(character.PalmPosition(false),c.Hands.Right.Palm.position)<.03f,"source arm lengths preserved and palm reaches the calibrated wooden grip");
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
            }
        }
        private static void CheckNear(Vector3 point,float near)
        {
            if(Mathf.Abs(point.x)<.08f && Mathf.Abs(point.y)<.08f && point.z<near+.005f)
                throw new InvalidOperationException("Pick mesh crosses eye/near plane: "+point);
        }
    }
}
