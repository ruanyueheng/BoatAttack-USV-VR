using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    public static class FishingLanternValidation
    {
        public static void Run()=>FishingV011Validation.Begin("LANTERN");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[LANTERN] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            const string output="Logs/UserLantern";Directory.CreateDirectory(output);
            var mesh=FishingUserTools.Mesh(FieldItem.Lantern);var materials=FishingUserTools.Materials(FieldItem.Lantern);
            Check(mesh!=null && mesh.subMeshCount==2 && mesh.triangles.Length/3<=13000 && Mathf.Abs(mesh.bounds.size.y-.4f)<.005f,"supplied lamp and real horizontal handle, two frame/paper submeshes");
            Check(materials.Length==2 && materials[0].GetTexture("_BaseMap")!=null && materials[1].IsKeywordEnabled("_EMISSION"),"actual source PBR texture and switchable luminous paper material");
            c.ReturnToHarbor();var rb=c.Player.engine.RB;rb.position=c.Shore.Sites[0].Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
            Check(c.Shore.TryDisembark(),"production lantern shore landing");
            Check(c.FieldWorld.TryLocation(c.Shore.Sites[0].Landing,4,90,out var feet) && c.Shore.Relocate(feet+Vector3.up*.05f,Quaternion.identity),"lamp pose uses real dry footing");
            if(!c.FirstPerson)c.ToggleView();c.View.ResetLook();
            Check(c.Career.Backpack.TryAdd(FieldItem.Lantern,1),"inventory contains craft-compatible lantern");int slot=-1;
            for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==FieldItem.Lantern){slot=i;break;}
            if(slot!=1)Check(c.Career.Backpack.Move(slot,1),"move actual lamp to hotbar");
            c.SelectFieldSlot(1);c.FieldWorld.ClearToolPresentation();c.SetDayNight(true);
            float reach=c.Shore.Character.ArmReach(false);var character=c.Shore.Character;
            foreach(bool crouch in new[]{false,true})
            {
                c.Shore.SetCrouch(crouch);
                for(int i=0;i<30;i++){c.Shore.Move(Vector2.zero,false,false,crouch,.02f);c.PresentCharacterFrame();yield return null;}
                foreach(float pitch in new[]{-30f,0f,30f})
                {
                    c.View.ResetLook();c.View.ApplyShoreLookDelta(new Vector2(0,-pitch/.12f));
                    for(int i=0;i<4;i++){c.PresentCharacterFrame();yield return null;}
                    Check(c.FieldWorld.PresentedItem==FieldItem.Lantern && c.FieldWorld.HeldToolRenderer.sharedMaterials.Length==2,"source lamp and paper both render while held");
                    var forearm=character.RightWrist.position-character.ElbowPosition(false);
                    float bend=Vector3.Angle(forearm,character.PalmRotation(false)*Vector3.forward);
                    float elbow=Vector3.Angle(character.ShoulderPosition(false)-character.ElbowPosition(false),forearm);
                    Debug.Log("[LANTERN] POSE: crouch="+crouch+" pitch="+pitch+" wrist="+bend+" elbow="+elbow+" palm="+c.viewCamera.WorldToViewportPoint(c.Hands.Right.Palm.position));
                    Check(bend<40 && elbow>35 && elbow<170,"neutral wrist and articulated elbow, not a twisted carry pose");
                    Check(Mathf.Abs(character.ArmReach(false)-reach)<.003f && Vector3.Distance(character.PalmPosition(false),c.Hands.Right.Palm.position)<.03f,"real hand reaches source handle without stretching bones");
                    foreach(var v in mesh.vertices)
                    {
                        var p=c.viewCamera.transform.InverseTransformPoint(c.FieldWorld.HeldTool.TransformPoint(v));
                        if(Mathf.Abs(p.x)<.07f && Mathf.Abs(p.y)<.07f && p.z<c.viewCamera.nearClipPlane+.005f)throw new InvalidOperationException("Lantern crosses the eye near-plane");
                    }
                    FishingV016Validation.Capture(c,output,"pose-"+(crouch?"crouched":"standing")+"-"+pitch);
                }
            }
            c.Shore.SetCrouch(false);c.View.ResetLook();for(int i=0;i<20;i++){c.PresentCharacterFrame();yield return null;}
            var light=c.FieldWorld.transform.Find("Field lantern light").GetComponent<Light>();
            Check(!c.LanternOn && !light.enabled,"unequipped/new lamp starts with lighting off");
            var block=new MaterialPropertyBlock();c.FieldWorld.HeldToolRenderer.GetPropertyBlock(block,1);
            Check(ColourEnergy(block.GetColor("_EmissionColor"))==0,"off lamp has no fake luminous window");
            c.UseFieldItem();c.PresentCharacterFrame();c.FieldWorld.HeldToolRenderer.GetPropertyBlock(block,1);
            Check(c.LanternOn && light.enabled && light.range>=8 && light.intensity>0 && ColourEnergy(block.GetColor("_EmissionColor"))>3,"F turns on both the warm point light and the lamp window emission");
            Check(Vector3.Distance(light.transform.position,c.FieldWorld.HeldTool.TransformPoint(FishingFieldWorld.LanternLightPoint))<.002f,"light follows the actual lantern chamber");
            var paperPoint=c.FieldWorld.HeldTool.TransformPoint(new Vector3(0,.14f,-.065f));
            c.UseFieldItem();c.PresentCharacterFrame();c.viewCamera.transform.LookAt(paperPoint);float paperOff=CaptureLightTarget(c,output,"paper-off");
            c.UseFieldItem();c.PresentCharacterFrame();light.enabled=false;c.viewCamera.transform.LookAt(paperPoint);float paperOn=CaptureLightTarget(c,output,"paper-glow-only");
            Debug.Log("[LANTERN] PAPER: off="+paperOff+" glowOnly="+paperOn);
            Check(paperOn>paperOff+.08f,"actual lamp paper visibly glows even without the point light");
            c.View.ResetLook();c.PresentCharacterFrame();
            FishingV016Validation.Capture(c,output,"first-person-lit");
            c.ToggleView();for(int i=0;i<5;i++){c.PresentCharacterFrame();yield return null;}
            var look=character.HandReferencePosition;var cameraPosition=look+character.transform.right*1.7f+Vector3.up*.6f-character.transform.forward*.7f;
            c.viewCamera.transform.SetPositionAndRotation(cameraPosition,Quaternion.LookRotation(look-cameraPosition));FishingV016Validation.Capture(c,output,"side-carry-lit");c.ToggleView();
            c.UseFieldItem();c.PresentCharacterFrame();
            // The same coastal palette used by crafted props must receive real light,
            // not just a brighter lamp texture. Compare an actual rendered grey target.
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="Isolated lantern light target";
            var grey=new Material(c.PaletteShader);grey.SetColor("_BaseColor",new Color(.5f,.5f,.5f));plate.GetComponent<Renderer>().sharedMaterial=grey;
            var plateMesh=UnityEngine.Object.Instantiate(plate.GetComponent<MeshFilter>().sharedMesh);var colours=new Color[plateMesh.vertexCount];for(int i=0;i<colours.Length;i++)colours[i]=Color.white;plateMesh.colors=colours;plate.GetComponent<MeshFilter>().sharedMesh=plateMesh;
            plate.transform.SetPositionAndRotation(light.transform.position+character.transform.forward*.85f,character.transform.rotation);plate.transform.localScale=new Vector3(.7f,.7f,.05f);Physics.SyncTransforms();
            for(int i=0;i<3;i++)yield return null;
            c.PresentCharacterFrame();c.viewCamera.transform.LookAt(plate.transform.position);float dark=CaptureLightTarget(c,output,"light-off");
            c.UseFieldItem();c.PresentCharacterFrame();c.viewCamera.transform.LookAt(plate.transform.position);float lit=CaptureLightTarget(c,output,"light-on");
            Debug.Log("[LANTERN] LIGHT: off="+dark+" on="+lit);
            Check(lit>dark+.025f,"nearby coastal props are visibly illuminated by the real lamp");
            c.UseFieldItem();c.PresentCharacterFrame();c.FieldWorld.HeldToolRenderer.GetPropertyBlock(block,1);
            Check(!light.enabled && ColourEnergy(block.GetColor("_EmissionColor"))==0,"F extinguishes both direct light and glowing paper");
            c.SelectFieldSlot(0);c.PresentCharacterFrame();Check(!light.enabled,"changing tools cannot leave an invisible light behind");
            UnityEngine.Object.Destroy(plate);UnityEngine.Object.Destroy(plateMesh);UnityEngine.Object.Destroy(grey);
            c.SelectFieldSlot(1);c.SetDayNight(false);c.View.ResetLook();for(int i=0;i<3;i++){c.PresentCharacterFrame();yield return null;}
            FishingV016Validation.Capture(c,output,"first-person-daylight");
        }
        private static float ColourEnergy(Color c)=>c.r+c.g+c.b;
        private static float CaptureLightTarget(FishingPrototype c,string output,string name)
        {
            var camera=c.viewCamera;var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(640,360,24);var image=new Texture2D(640,360,TextureFormat.RGB24,false);float total=0;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();
                for(int y=140;y<220;y++)for(int x=280;x<360;x++)total+=image.GetPixel(x,y).grayscale;
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());return total/6400;
            }
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);}
        }
    }
}
