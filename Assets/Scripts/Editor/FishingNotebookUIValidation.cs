using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingNotebookUIValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use the isolated batch preview; real saves are never preview fixtures.");
            foreach(var name in new[]{"Paper","InkEdge","NavigationAtlas","IslandPostcard"})
            {
                string path="Assets/Resources/FishingUIPrototype/"+name+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Missing UI texture "+path);
                importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.npotScale=TextureImporterNPOTScale.None;
                importer.alphaIsTransparency=name!="IslandPostcard";importer.maxTextureSize=1024;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            FishingV011Validation.Begin("V025");
        }

        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException(message);Debug.Log("[NOTEBOOK UI] CHECK: "+message);}

        public static void Checks(FishingPrototype c,string directory)
        {
            var notebook=c.XRNotebook;var type=typeof(FishingXRNotebook);
            var canvas=(Canvas)type.GetField("_canvas",Private).GetValue(notebook);
            var plates=(Image[])type.GetField("_plates",Private).GetValue(notebook);
            var labels=(Text[])type.GetField("_labels",Private).GetValue(notebook);
            var actions=(Action[])type.GetField("_actions",Private).GetValue(notebook);
            var rect=(RectTransform)canvas.transform;
            Check(canvas.renderMode==RenderMode.WorldSpace && rect.sizeDelta==new Vector2(1100,780),"Prototype uses the production world-space notebook");
            Check(Mathf.Abs(rect.localScale.x-.00105f)<.000001f,"Prototype physical width is 1.155 metres");
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true))Check(graphic.material.renderQueue==3200,"Notebook draws after the sea route's transparent queue 3100");
            bool active=canvas.gameObject.activeSelf;canvas.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();Physics.SyncTransforms();
            try
            {
                foreach(var text in canvas.GetComponentsInChildren<Text>())
                {
                    if(string.IsNullOrEmpty(text.text))continue;
                    Check(text.font!=null && text.fontSize>=20,"Readable font assigned: "+text.text.Replace('\n',' '));
                    foreach(char ch in text.text)if(!char.IsWhiteSpace(ch))Check(text.font.HasCharacter(ch),"Chinese/symbol glyph exists: "+ch);
                    Check(text.preferredHeight<=text.rectTransform.rect.height+.5f,"Text fits its allocated height: "+text.text.Replace('\n',' '));
                }
                for(int i=0;i<9;i++)
                {
                    var r=plates[i].rectTransform;var box=plates[i].GetComponent<BoxCollider>();
                    Check(actions[i]!=null && box.size.x==r.rect.width && box.size.y==r.rect.height,"Original route and matching collider preserved: "+labels[i].text);
                    Vector3 centre=r.TransformPoint(new Vector3(r.rect.width*.5f,-r.rect.height*.5f,0));
                    bool hit=Physics.Raycast(centre-r.forward*.3f,r.forward,out var target,.32f,1<<5,QueryTriggerInteraction.Collide);
                    Check(hit && target.collider==box,"Controller ray hits the visible button: "+i);
                }
                var apply=type.GetMethod("ApplyHomeState",Private);var stateType=type.GetNestedType("HomeState",BindingFlags.NonPublic);
                var pointer=type.GetMethod("UpdateHomePointer",Private);
                var arrows=(Text[])type.GetField("_homeArrows",Private).GetValue(notebook);
                foreach(string state in new[]{"Normal","Hover","Pressed","Selected","Disabled"})
                {
                    apply.Invoke(notebook,new[]{(object)2,Enum.Parse(stateType,state),true});
                    Check(actions[2]!=null,"State preview does not replace the navigation action: "+state);
                    Check(arrows[2].font.HasCharacter(arrows[2].text[0]) && arrows[2].preferredWidth<=arrows[2].rectTransform.rect.width
                        && arrows[2].preferredHeight<=arrows[2].rectTransform.rect.height,"State marker is visible and fits: "+state);
                    FishingXRPresentationValidation.CaptureCanvas(canvas,Path.Combine(directory,"home-"+state.ToLowerInvariant()+".png"));
                }
                var original=actions[2];
                try{actions[2]=null;pointer.Invoke(notebook,new object[]{2,true});Check(type.GetMethod("HomeButtonState",Private).Invoke(notebook,new object[]{2}).ToString()=="Disabled","Disabled overrides hovering and trigger hold");}
                finally{actions[2]=original;}
                pointer.Invoke(notebook,new object[]{-1,false});
                Check((int)type.GetField("_homeHover",Private).GetValue(notebook)==-1,"Leaving a button clears hover in the same frame");
                apply.Invoke(notebook,new[]{(object)2,Enum.Parse(stateType,"Normal"),true});
                CaptureInBay(c,canvas,Path.Combine(directory,"home-in-bay.png"));
            }
            finally{canvas.gameObject.SetActive(active);}
        }

        private static void CaptureInBay(FishingPrototype c,Canvas canvas,string path)
        {
            var p=canvas.transform.position;var q=canvas.transform.rotation;var camera=canvas.worldCamera;
            var view=c.viewCamera;var viewPosition=view.transform.position;var viewRotation=view.transform.rotation;
            var target=view.targetTexture;float fov=view.fieldOfView,aspect=view.aspect;var stereo=view.stereoTargetEye;
            int navigation=c.NavigationTarget;c.SelectNavigation(1);
            Check(c.WaterRoute.Plan(),"Occlusion fixture plans a real route to the first fishing ground");
            typeof(FishingWaterRoute).GetMethod("LateUpdate",Private).Invoke(c.WaterRoute,null);
            view.stereoTargetEye=StereoTargetEyeMask.None;view.fieldOfView=64;view.aspect=1.6f;
            var forward=Vector3.ProjectOnPlane(c.viewCamera.transform.forward,Vector3.up).normalized;
            var rotation=Quaternion.LookRotation(forward.sqrMagnitude>.1f?forward:c.Player.transform.forward,Vector3.up);
            view.transform.SetPositionAndRotation(c.viewCamera.transform.position,rotation);
            var rt=new RenderTexture(1600,1000,24);var photo=new Texture2D(1600,1000,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                canvas.transform.SetPositionAndRotation(view.transform.position+rotation*Vector3.forward*1.3f-Vector3.up*.12f,rotation);
                canvas.worldCamera=view;view.targetTexture=rt;Canvas.ForceUpdateCanvases();
                canvas.gameObject.SetActive(false);view.Render();RenderTexture.active=rt;
                photo.ReadPixels(new Rect(0,0,1600,1000),0,0);photo.Apply();File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path),"bay-without-notebook.png"),photo.EncodeToPNG());
                canvas.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();view.Render();RenderTexture.active=rt;
                photo.ReadPixels(new Rect(0,0,1600,1000),0,0);photo.Apply();File.WriteAllBytes(path,photo.EncodeToPNG());
                var normal=photo.GetPixels32();var routes=new List<Renderer>();
                foreach(var renderer in Object.FindObjectsOfType<Renderer>())if(renderer.enabled && renderer.sharedMaterial!=null
                    && renderer.sharedMaterial.shader.name=="BoatAttack/FishingWaterGuidance")routes.Add(renderer);
                Check(routes.Count>0,"Scene proof retains the actual visible navigation route");
                try
                {
                    foreach(var route in routes)route.enabled=false;
                    view.Render();RenderTexture.active=rt;photo.ReadPixels(new Rect(0,0,1600,1000),0,0);photo.Apply();
                    var reference=photo.GetPixels32();var corners=new Vector3[4];((RectTransform)canvas.transform).GetWorldCorners(corners);
                    var low=view.WorldToScreenPoint(corners[0]);var high=view.WorldToScreenPoint(corners[2]);int changed=0;
                    for(int y=Mathf.CeilToInt(low.y+18);y<Mathf.FloorToInt(high.y-18);y++)
                        for(int x=Mathf.CeilToInt(low.x+18);x<Mathf.FloorToInt(high.x-18);x++)
                        {int i=y*1600+x;var a=normal[i];var b=reference[i];if(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>4)changed++;}
                    Check(changed==0,"Visible sea route changes zero pixels inside the notebook paper");
                }
                finally{foreach(var route in routes)route.enabled=true;}
                foreach(var material in Resources.FindObjectsOfTypeAll<Material>())if(material.shader!=null && material.shader.name=="BoatAttack/Water")
                    Debug.Log("[NOTEBOOK UI] WATER: "+material.name+"; supported="+material.shader.isSupported+"; keywords="+string.Join(",",material.shaderKeywords));
                Debug.Log("[NOTEBOOK UI] SCENE PREVIEW: "+path+"; desktop mono scene render, not a headset capture");
            }
            finally{canvas.transform.SetPositionAndRotation(p,q);canvas.worldCamera=camera;view.targetTexture=target;view.fieldOfView=fov;view.aspect=aspect;view.stereoTargetEye=stereo;
                view.transform.SetPositionAndRotation(viewPosition,viewRotation);c.SelectNavigation(navigation);RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(photo);}
        }
    }
}
