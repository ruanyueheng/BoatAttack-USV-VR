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
            var snapshot=c.Career.Snapshot();GameObject plate=null;Material grey=null;Mesh plateMesh=null;
            try
            {
                var mesh=FishingUserTools.Mesh(FieldItem.Lantern);var materials=FishingUserTools.Materials(FieldItem.Lantern);
                Check(mesh!=null && mesh.subMeshCount==2 && mesh.triangles.Length/3<=13000 && Mathf.Abs(mesh.bounds.size.y-.4f)<.005f,"supplied upright lantern preserves frame and luminous panes within budget");
                Check(materials.Length==2 && materials[0].GetTexture("_BaseMap")!=null && materials[1].IsKeywordEnabled("_EMISSION"),"source PBR textures and switchable pane emission");
                c.ReturnToHarbor();var rb=c.Player.engine.RB;rb.position=c.Shore.Sites[0].Berth+Vector3.up;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.position=rb.position;Physics.SyncTransforms();
                Check(c.Shore.TryDisembark(),"production shoreline entry");
                Check(c.Career.Backpack.TryAdd(FieldItem.Lantern,1),"isolated profile owns the lamp");
                int slot=-1;for(int i=0;i<FishingBackpack.SlotCount;i++)if(c.Career.Backpack.At(i).item==FieldItem.Lantern){slot=i;break;}
                if(slot>=9){Check(c.Career.Backpack.Move(slot,1),"lamp is selectable from hotbar");slot=1;}
                c.SelectFieldSlot(slot);c.SetDayNight(true);
                for(int i=0;i<4;i++){c.PresentCharacterFrame();yield return null;}
                var held=c.FieldWorld.HeldTool;var renderer=held.GetComponent<Renderer>();
                var light=c.FieldWorld.transform.Find("Field lantern light").GetComponent<Light>();
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,1);
                Check(c.FieldWorld.LanternHeld && !c.LanternOn && !light.enabled && ColourEnergy(block.GetColor("_EmissionColor"))==0,"selected lamp starts in the hand, initially unlit");
                c.UseFieldItem();c.PresentCharacterFrame();renderer.GetPropertyBlock(block,1);
                Check(c.LanternOn && light.enabled && light.range>=8 && ColourEnergy(block.GetColor("_EmissionColor"))>3,"F enables real amber light and luminous panes");
                Check(Vector3.Distance(light.transform.position,held.TransformPoint(FishingFieldWorld.LanternLightPoint))<.002f,"light follows the actual held lantern chamber");
                c.SelectFieldSlot(0);c.PresentCharacterFrame();var holster=c.FieldWorld.HolsteredLantern;
                Check(holster!=null && holster.gameObject.activeSelf && !c.FieldWorld.LanternHeld,"Idle lamp hangs at the waist after switching away");
                foreach(bool crouch in new[]{false,true})
                {
                    c.Shore.SetCrouch(crouch);
                    for(int i=0;i<24;i++){c.PresentCharacterFrame();yield return null;}
                    Check(Vector3.Dot(holster.up,Vector3.up)>.999f,"gravity keeps the lamp vertical while standing/crouching");
                    FishingV016Validation.Capture(c,output,crouch?"waist-crouched":"waist-standing");
                }
                c.Shore.SetCrouch(false);c.SelectFieldSlot(slot);c.UseFieldItem();c.PresentCharacterFrame();
                plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="Isolated lantern illumination target";
                grey=new Material(c.PaletteShader);grey.SetColor("_BaseColor",new Color(.5f,.5f,.5f));plate.GetComponent<Renderer>().sharedMaterial=grey;
                plateMesh=UnityEngine.Object.Instantiate(plate.GetComponent<MeshFilter>().sharedMesh);var colours=new Color[plateMesh.vertexCount];for(int i=0;i<colours.Length;i++)colours[i]=Color.white;plateMesh.colors=colours;plate.GetComponent<MeshFilter>().sharedMesh=plateMesh;
                plate.transform.SetPositionAndRotation(light.transform.position+c.Shore.Actor.forward*.85f,c.Shore.Actor.rotation);plate.transform.localScale=new Vector3(.7f,.7f,.05f);Physics.SyncTransforms();
                for(int i=0;i<3;i++){c.PresentCharacterFrame();yield return null;}
                c.viewCamera.transform.LookAt(plate.transform.position);float dark=CaptureLightTarget(c,output,"light-off");
                c.UseFieldItem();c.PresentCharacterFrame();c.viewCamera.transform.LookAt(plate.transform.position);float lit=CaptureLightTarget(c,output,"light-on");
                Check(lit>dark+.025f,"real handheld light visibly illuminates nearby coastal material");
                c.UseFieldItem();c.PresentCharacterFrame();renderer.GetPropertyBlock(block,1);
                Check(!light.enabled && ColourEnergy(block.GetColor("_EmissionColor"))==0,"F extinguishes direct light and pane emission");
                Check(c.InspectFieldItem(slot),"explicit inspection can still lift the source lamp");c.PresentCharacterFrame();
                Check(c.FieldWorld.LanternHeld && !holster.gameObject.activeSelf,"inspection shows exactly one lantern");c.EndItemInspection();
            }
            finally
            {
                c.EndItemInspection();c.ReturnToHarbor();c.RestoreShopTestProfile(snapshot);
                if(plate!=null)UnityEngine.Object.Destroy(plate);if(plateMesh!=null)UnityEngine.Object.Destroy(plateMesh);if(grey!=null)UnityEngine.Object.Destroy(grey);
            }
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
