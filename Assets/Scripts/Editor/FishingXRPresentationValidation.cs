using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingXRPresentationValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static FieldInfo Field(string name)=>typeof(FishingXRNotebook).GetField(name,Private);
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V025 UI] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            c.ReturnToHarbor();yield return null;yield return null;
            ValidateModalMotion(c);
            string evidence=Path.GetFullPath("Logs/v025-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            var notebook=c.XRNotebook;var refresh=typeof(FishingXRNotebook).GetMethod("Refresh",Private);
            var screen=(Canvas)Field("_canvas").GetValue(notebook);var status=(Text)Field("_status").GetValue(notebook);
            var labels=(Text[])Field("_labels").GetValue(notebook);var actions=(Action[])Field("_actions").GetValue(notebook);
            var feedback=(Canvas)Field("_feedback").GetValue(notebook);var feedbackText=(Text)Field("_feedbackText").GetValue(notebook);
            Check(feedback.renderMode==RenderMode.WorldSpace && feedback.GetComponentsInChildren<Collider>().Length==0,"Transient Chinese feedback cannot intercept tool or menu rays");
            typeof(FishingXRNotebook).GetMethod("ApplyGuidanceText",Private).Invoke(notebook,new object[]{"已收取海鸥肉与羽毛\n背包空间不足时，猎物会保留在原处；B 打开背包整理后再收取。"});
            Check(feedbackText.preferredHeight<=feedbackText.rectTransform.rect.height,"Normal pickup and full-bag feedback fits the readable transient panel");
            CaptureCanvas(feedback,Path.Combine(evidence,"transient-feedback.png"));
            Check(!c.DockConsole.Visible,"No permanent gray command panel at startup");
            var guide=(string[][])typeof(FishingXRNotebook).GetField("GuideCards",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            string controls=string.Join("\n",guide[0])+string.Join("\n",guide[1]);
            foreach(string key in new[]{"右 A","右 B","左 X","左 Y","左摇杆","右摇杆","右扳机","左扳机","左握把","右握把","按下左摇杆","按下右摇杆"})Check(controls.Contains(key),"Chinese controller guide covers "+key);
            try
            {
                var motion=typeof(FishingXRNotebook).GetMethod("ReArmMotion",Private);
                notebook.ResetInput();
                Check(!(bool)motion.Invoke(notebook,new object[]{Vector2.up,Vector2.zero,false}),"Closing a lesson cannot turn a held throttle into movement");
                Check(!(bool)motion.Invoke(notebook,new object[]{Vector2.zero,Vector2.right,false}),"Held steering cannot bypass the post-menu neutral gate");
                Check(!(bool)motion.Invoke(notebook,new object[]{Vector2.zero,Vector2.zero,true}),"Held boost must be released before motion resumes");
                Check(!(bool)motion.Invoke(notebook,new object[]{new Vector2(float.NaN,0),Vector2.zero,false}),"Invalid tracking axes cannot rearm motion");
                Check((bool)motion.Invoke(notebook,new object[]{Vector2.zero,Vector2.zero,false}) && notebook.MotionReady,"Neutral sticks and released boost restore motion");
                Check((bool)motion.Invoke(notebook,new object[]{Vector2.up,Vector2.right,true}),"Fresh input remains usable after the neutral gate rearms");
                notebook.ResetInput();
                Field("_page").SetValue(notebook,11);Field("_guideLesson").SetValue(notebook,0);Field("_guidePracticed").SetValue(notebook,0);refresh.Invoke(notebook,null);
                Check(actions[7]==null,"Lesson cannot advance before the five safe practice inputs");
                Field("_guidePracticed").SetValue(notebook,31);refresh.Invoke(notebook,null);Check(actions[7]!=null,"Completed controls unlock the next lesson");
                actions[7]();Check((int)Field("_guideLesson").GetValue(notebook)==1 && (int)Field("_guidePracticed").GetValue(notebook)==0,"Next lesson clears practice and held input");
                Field("_guidePracticed").SetValue(notebook,31);refresh.Invoke(notebook,null);actions[7]();Check((int)Field("_guideLesson").GetValue(notebook)==2 && actions[7]!=null,"Tool, fishing and crafting explanations precede completion");
                Field("_guideLesson").SetValue(notebook,0);refresh.Invoke(notebook,null);CaptureCanvas(screen,Path.Combine(evidence,"controller-guide.png"));
                foreach(int page in new[]{0,1,2,3,7,12,13,14,15,16,FishingXRNotebook.ClassroomPage,FishingXRNotebook.HandAlignmentPage,FishingXRNotebook.QuitPage,FishingXRNotebook.CreativePage,FishingXRNotebook.FishSalesPage})
                {
                    Field("_page").SetValue(notebook,page);Field("_offset").SetValue(notebook,0);refresh.Invoke(notebook,null);
                    Check(status.text.Length>0 && actions[7]!=null,"Shared game feature page has content and a return path: "+page);
                    if(page>=FishingXRNotebook.ClassroomPage)CaptureCanvas(screen,Path.Combine(evidence,"settings-"+page+".png"));
                    if(page==12)
                    {
                        var map=(RawImage)Field("_map").GetValue(notebook);Check(map.texture==Resources.Load<FishingMapDefinition>("FishingMap").texture,"XR uses the same baked terrain map as Windows");
                        foreach(string field in new[]{"_mapPlayer","_mapTarget"})
                        {var marker=(RectTransform)Field(field).GetValue(notebook);Check(marker.gameObject.activeSelf && marker.GetComponent<Text>().preferredHeight<=marker.rect.height,"Map position glyph fits without being truncated: "+field);}
                        CaptureCanvas(screen,Path.Combine(evidence,"notebook-map.png"));
                    }
                    if(page==0){CaptureCanvas(screen,Path.Combine(evidence,"notebook-home.png"));FishingNotebookUIValidation.Checks(c,evidence);}
                }
                var dock=(Canvas)typeof(FishingDockConsole).GetField("_canvas",Private).GetValue(c.DockConsole);
                foreach(var text in dock.GetComponentsInChildren<Text>(true))Check(!text.text.Contains("CONFIRM") && !text.text.Contains("HELM") && !text.text.Contains("order"),"Dock commands no longer expose old English labels");
                c.DockConsole.PreviewForValidation();CaptureCanvas(dock,Path.Combine(evidence,"dock-commands.png"));
                typeof(FishingDockConsole).GetField("_preview",Private).SetValue(c.DockConsole,false);c.DockConsole.Hide();
                Check(c.Prologue.Begin(),"Opening can be replayed through its production entry");
                c.Prologue.Advance(false);Check(c.Prologue.Shot==0 && c.Prologue.Age>0,"XR A shares the desktop path that reveals the current subtitle");
                c.Prologue.Advance(false);Check(c.Prologue.Shot==1,"The next XR A continues to the next shot instead of skipping the whole film");
                c.Prologue.Tick(.25f,false);float age=c.Prologue.Age;
                c.Prologue.Tick(.25f,true);Check(c.Prologue.Age==age,"XR waits for a real head pose instead of finishing an invisible film");
                var configure=typeof(FishingPrologue).GetMethod("ConfigureScreen",Private);configure.Invoke(c.Prologue,new object[]{true});
                Check(c.Prologue.CinemaScreen.renderMode==RenderMode.WorldSpace && c.Prologue.CinemaCamera.stereoTargetEye==StereoTargetEyeMask.None,"Delayed XR presentation uses a stationary screen and a mono film camera");
                var position=c.Prologue.CinemaScreen.transform.position;c.Prologue.Tick(.25f,false);configure.Invoke(c.Prologue,new object[]{true});
                c.Prologue.Tick(.25f,true);Check(c.Prologue.CinemaScreen.transform.position==position,"Missing tracking cannot move the cinema screen with the head");
                c.Prologue.CinemaScreen.gameObject.SetActive(true);c.Prologue.CinemaCamera.Render();CaptureCanvas(c.Prologue.CinemaScreen,Path.Combine(evidence,"opening-cinema.png"));
                c.Prologue.Suspend();Check(!c.Prologue.Open,"Suspending the opening restores play without finishing the lesson");
            }
            finally
            {
                c.Prologue.Suspend();Field("_page").SetValue(notebook,0);Field("_offset").SetValue(notebook,0);notebook.Close();
                typeof(FishingDockConsole).GetField("_preview",Private).SetValue(c.DockConsole,false);c.DockConsole.Hide();
            }
            var interaction=FishingXRInteractionValidation.Checks(c);try{while(interaction.MoveNext())yield return interaction.Current;}finally{(interaction as IDisposable)?.Dispose();}
            Debug.Log("[V025 UI] EVIDENCE: "+evidence);yield return null;
        }
        private static void ValidateModalMotion(FishingPrototype c)
        {
            var xr=typeof(FishingPrototype).GetField("_xrActive",Private);bool wasXR=(bool)xr.GetValue(c);
            var rb=c.Player.engine.RB;var velocity=rb.velocity;var angular=rb.angularVelocity;
            try
            {
                xr.SetValue(c,true);typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(c.XRNotebook,true);
                Check(!c.Ashore && !c.Anchored,"Modal drift check starts with the actual unanchored vessel");
                rb.velocity=new Vector3(4,2,-3);rb.angularVelocity=new Vector3(.3f,.4f,-.2f);
                float vertical=rb.velocity.y,roll=rb.angularVelocity.x,pitch=rb.angularVelocity.z;
                typeof(FishingPrototype).GetMethod("FixedUpdate",Private).Invoke(c,null);
                Check(rb.velocity.x==0 && rb.velocity.z==0 && rb.angularVelocity.y==0,"Opening XR menus halts boat drift and yaw so panels stay within reach");
                Check(Mathf.Abs(rb.velocity.y-vertical)<.001f && Mathf.Abs(rb.angularVelocity.x-roll)<.001f && Mathf.Abs(rb.angularVelocity.z-pitch)<.001f,"XR menus retain the vertical and roll velocities accepted by the real hull constraints");
            }
            finally{xr.SetValue(c,wasXR);c.XRNotebook.Close();rb.velocity=velocity;rb.angularVelocity=angular;}
        }
        internal static void CaptureCanvas(Canvas canvas,string path,bool verifyText=false)
        {
            bool active=canvas.gameObject.activeSelf;var mode=canvas.renderMode;var camera=canvas.worldCamera;
            var p=canvas.transform.position;var q=canvas.transform.rotation;var scale=canvas.transform.localScale;
            var go=new GameObject("Isolated UI validation camera",typeof(Camera));var view=go.GetComponent<Camera>();
            var bounds=((RectTransform)canvas.transform).rect;int width=1200,height=Mathf.CeilToInt(width*bounds.height/bounds.width);
            var rt=new RenderTexture(width,height,24);var photo=new Texture2D(width,height,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                canvas.renderMode=RenderMode.WorldSpace;canvas.transform.SetPositionAndRotation(new Vector3(0,10000,0),Quaternion.identity);canvas.transform.localScale=Vector3.one*.0016f;canvas.gameObject.SetActive(true);
                view.enabled=false;view.stereoTargetEye=StereoTargetEyeMask.None;view.orthographic=true;view.orthographicSize=((RectTransform)canvas.transform).rect.height*.0016f*.54f;view.cullingMask=1<<5;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.025f,.05f,.07f);view.targetTexture=rt;
                view.transform.SetPositionAndRotation(canvas.transform.position-Vector3.forward*2,Quaternion.identity);canvas.worldCamera=view;Canvas.ForceUpdateCanvases();view.Render();RenderTexture.active=rt;photo.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);photo.Apply();
                var pixels=photo.GetPixels32();var background=pixels[0];int varied=0;
                foreach(var pixel in pixels)if(Math.Abs(pixel.r-background.r)+Math.Abs(pixel.g-background.g)+Math.Abs(pixel.b-background.b)>30)varied++;
                if(varied<pixels.Length/100)throw new InvalidOperationException("UI capture is empty: "+path);
                File.WriteAllBytes(path,photo.EncodeToPNG());
                if(verifyText)
                {
                    var labels=canvas.GetComponentsInChildren<Text>().Where(t=>t.enabled && !string.IsNullOrWhiteSpace(t.text) && t.color.a>.1f).ToArray();
                    foreach(var label in labels)label.enabled=false;
                    try
                    {
                        Canvas.ForceUpdateCanvases();view.Render();photo.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);photo.Apply();
                        var without=photo.GetPixels32();var corners=new Vector3[4];
                        foreach(var label in labels)
                        {
                            label.rectTransform.GetWorldCorners(corners);int left=rt.width,right=0,bottom=rt.height,top=0;
                            foreach(var corner in corners){var at=view.WorldToScreenPoint(corner);left=Math.Min(left,Mathf.FloorToInt(at.x));right=Math.Max(right,Mathf.CeilToInt(at.x));bottom=Math.Min(bottom,Mathf.FloorToInt(at.y));top=Math.Max(top,Mathf.CeilToInt(at.y));}
                            int changed=0;for(int y=Math.Max(0,bottom);y<Math.Min(rt.height,top);y++)for(int x=Math.Max(0,left);x<Math.Min(rt.width,right);x++)
                            {int index=y*rt.width+x;if(Math.Abs(pixels[index].r-without[index].r)+Math.Abs(pixels[index].g-without[index].g)+Math.Abs(pixels[index].b-without[index].b)>25)changed++;}
                            if(changed<5)throw new InvalidOperationException("Text has no rendered pixels: "+label.name+" / "+label.text+" in "+path);
                        }
                        Debug.Log("[XR-TEXT-PIXELS] CHECK: "+labels.Length+" visible labels contribute actual pixels in "+Path.GetFileName(path));
                    }
                    finally{foreach(var label in labels)label.enabled=true;Canvas.ForceUpdateCanvases();}
                }
            }
            finally{canvas.transform.SetPositionAndRotation(p,q);canvas.transform.localScale=scale;canvas.renderMode=mode;canvas.worldCamera=camera;canvas.gameObject.SetActive(active);RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(photo);Object.Destroy(go);}
        }
    }
}
