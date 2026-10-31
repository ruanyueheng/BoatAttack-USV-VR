using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BoatAttack.EditorTools
{
    public static class FishingWorldTextValidation
    {
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("[WORLD-TEXT] "+message);Debug.Log("[WORLD-TEXT] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            string dir=Path.GetFullPath("Logs/V054/Visual");Directory.CreateDirectory(dir);
            var root=new GameObject("World text render fixture");root.transform.position=new Vector3(0,1200,0);
            var cameraObject=new GameObject("World text validation camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            // The project's 3D renderer excludes unused layers such as 29; layer 16 is an authored render layer.
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<16;
            camera.stereoTargetEye=StereoTargetEyeMask.None;camera.orthographic=true;camera.nearClipPlane=.05f;camera.farClipPlane=10;
            var fontMaterial=FishingUIFont.Font.material;
            var wallMaterial=new Material(c.PaletteShader);wallMaterial.SetColor("_BaseColor",Color.black);wallMaterial.SetFloat("_Unlit",1);
            try
            {
                var sign=FishingV019Art.Sign("WORLD DEPTH · 回潮渔屋",root.transform,Vector3.zero,.12f,Color.red);sign.gameObject.layer=16;
                yield return null;yield return null;
                var renderer=sign.GetComponent<MeshRenderer>();var bounds=renderer.bounds;
                camera.transform.SetPositionAndRotation(bounds.center-Vector3.forward*4,Quaternion.identity);
                camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x/2)*.7f;
                Check(renderer.sharedMaterial!=fontMaterial && FishingUIFont.Font.material==fontMaterial,"World text has its own shared material and preserves the UI font material");
                Check(renderer.sharedMaterial.mainTexture==sign.font.material.mainTexture,"World text samples the current dynamic font atlas");
                int visible=Render(camera,dir,"world-text-visible");
                var wall=FishingArt.Shape("Opaque wall for world text test",PrimitiveType.Cube,root.transform,root.transform.InverseTransformPoint(bounds.center)-Vector3.forward,
                    new Vector3(Mathf.Max(bounds.size.x*2,1),Mathf.Max(bounds.size.y*2,1),.1f),wallMaterial);wall.layer=16;
                int hidden=Render(camera,dir,"world-text-hidden");
                sign.transform.position-=Vector3.forward*2;
                int front=Render(camera,dir,"world-text-front");
                Check(visible>100 && hidden<visible*.01f && front>visible*.8f,"Rendered glyph pixels are visible, blocked by an opaque wall, and visible in front: "+visible+" / "+hidden+" / "+front);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(wallMaterial);}
        }
        private static int Render(Camera camera,string directory,string label)
        {
            var rt=new RenderTexture(512,256,24);var pixels=new Texture2D(512,256,TextureFormat.RGB24,false);var active=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.aspect=2;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,512,256),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory,label+".png"),pixels.EncodeToPNG());
                int red=0;foreach(var p in pixels.GetPixels32())if(p.r>50 && p.g<35 && p.b<35)red++;return red;
            }
            finally{camera.targetTexture=null;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
