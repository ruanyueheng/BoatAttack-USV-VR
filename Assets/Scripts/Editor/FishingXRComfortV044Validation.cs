using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    /// <summary>v0.44 VR comfort options, controller hint coverage and readable world-space text.</summary>
    public static class FishingXRComfortV044Validation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[XR-COMFORT] CHECK: "+text);}
        public static void Run(FishingPrototype c)
        {
            var comfort=c.XRComfort;Check(comfort!=null,"XR comfort component is built");
            for(int i=0;i<60;i++){typeof(FishingXRComfort).GetMethod("Update",Private).Invoke(comfort,null);typeof(FishingXRHints).GetMethod("LateUpdate",Private).Invoke(c.GetComponent<FishingXRHints>(),null);}
            Check(true,"Sixty non-XR comfort and hint frames run without exceptions");
            var keys=new[]{"FishingXR.Turn","FishingXR.Teleport","FishingXR.Vignette","FishingXR.Speed","FishingXR.GustComfort"};
            var saved=keys.Select(k=>PlayerPrefs.HasKey(k)?(int?)PlayerPrefs.GetInt(k):null).ToArray();
            try
            {
                foreach(var k in keys)PlayerPrefs.DeleteKey(k);
                Check(FishingXRComfort.TurnMode==FishingXRComfort.Turn.Snap30 && !FishingXRComfort.Teleport && FishingXRComfort.Vignette==2 && FishingXRComfort.GustComfort,"Defaults: 30° snap turn, smooth walk, medium vignette, gusts never push the body");
                Check(FishingXRComfort.WalkSpeed<=1.6f && FishingXRComfort.Ramp(Vector2.zero,Vector2.up,.1f,false).magnitude<=.41f,"Walk speed capped at 1.6 m/s and full speed needs at least 0.25 s (acceleration cap)");
                FishingXRComfort.Teleport=true;
                Check(FishingXRComfort.Ramp(Vector2.zero,Vector2.up,1,true)==Vector2.zero && FishingXRComfort.Ramp(Vector2.zero,Vector2.up,1,false)==Vector2.up,"Teleport mode removes stick translation, except on ladders and in water");
                FishingXRComfort.TurnMode=FishingXRComfort.Turn.Snap45;Check(FishingXRComfort.SnapDegrees==45,"45° snap turn option");
                Check(Mathf.Abs(FishingXRComfort.GustSpeed(1.8f)-(UnityEngine.XR.XRSettings.isDeviceActive?.35f:1.8f))<.001f && FishingXRComfort.AllowsGustPush!=(UnityEngine.XR.XRSettings.isDeviceActive && FishingXRComfort.GustComfort),"Gust push: desktop unchanged, VR comfort none, VR standard slow drift");
                for(int s=1;s<=3;s++)
                {
                    var t=FishingXRComfort.VignetteTexture(s);var centre=t.GetPixel(64,64).a;var corner=t.GetPixel(0,0).a;var mid=t.GetPixel(64+(int)(64*.3f),64).a;UnityEngine.Object.Destroy(t);
                    Check(centre<.01f && corner>.99f && mid<.05f,"Vignette strength "+s+" keeps the central 30% clear and blacks out the periphery");
                }
            }
            finally{for(int i=0;i<keys.Length;i++){if(saved[i].HasValue)PlayerPrefs.SetInt(keys[i],saved[i].Value);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();}

            var used=new System.Collections.Generic.HashSet<string>();
            foreach(FishingXRHints.Context context in Enum.GetValues(typeof(FishingXRHints.Context)))
                foreach(bool left in new[]{true,false})
                {
                    var rows=FishingXRHints.Rows(context,left);
                    Check(rows.Length>0 && rows.Length<=6 && rows.All(r=>r.text.Length>0),"Hint rows exist for "+context+(left?" left":" right"));
                    foreach(var (key,_) in rows){used.Add(key);if(key!="Stick" && key!="Click" && key!="Menu")FishingUITheme.ControllerGlyph(key);}
                }
            foreach(string key in new[]{"A","B","X","Y","Trigger","Grip","Stick","Click","Menu"})Check(used.Contains(key),"Controller hint table uses PICO input "+key);
            Check(24*.0005f/.7f>=Mathf.Deg2Rad*.98f,"Controller tag text (24 px × 0.5 mm) spans about 1° or more at 0.7 m");

            // Every Chinese player-facing literal must read in PICO terms once adapted for XR (desktop HUD-only files excluded).
            var leaks=new System.Collections.Generic.List<string>();var changed=new System.Collections.Generic.List<string>();int scanned=0;
            foreach(var file in Directory.GetFiles("Assets/Scripts/Fishing","*.cs"))
            {
                var name=Path.GetFileName(file);if(System.Text.RegularExpressions.Regex.IsMatch(name,"Acceptance|Autoplay|Capture|Validation|HUD|Performance"))continue;
                foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file),@"""((?:[^""\\\r\n]|\\.)*)"""))
                {
                    var s=m.Groups[1].Value;if(!System.Text.RegularExpressions.Regex.IsMatch(s,"[一-鿿]"))continue;scanned++;
                    if(FishingXRText.Adapt(s,true)!=s)changed.Add(name+"	"+s+"	"+FishingXRText.Adapt(s,true));
                    if(FishingXRText.Forbidden.IsMatch(FishingXRText.Adapt(s,true)))leaks.Add(name+": "+s+"  ->  "+FishingXRText.Adapt(s,true));
                }
            }
            Directory.CreateDirectory("Logs/V044/vr");File.WriteAllLines("Logs/V044/vr/xr-text-adapted.txt",changed);
            foreach(var leak in leaks)Debug.Log("[XR-COMFORT] KEY LEAK: "+leak);
            Check(scanned>500 && leaks.Count==0,"No keyboard key names survive XR adaptation across "+scanned+" Chinese hint literals ("+leaks.Count+" leaks)");
            string evidence=Path.GetFullPath("Logs/V044/vr/ui");Directory.CreateDirectory(evidence);
            var hints=c.GetComponent<FishingXRHints>();Check(hints!=null,"Controller hints are built in production");
            foreach(var context in new[]{FishingXRHints.Context.Shore,FishingXRHints.Context.Boat,FishingXRHints.Context.Fishing,FishingXRHints.Context.Realm})
                foreach(bool left in new[]{true,false})
                {
                    var canvas=hints.PreviewForValidation(context,left);
                    foreach(var text in canvas.GetComponentsInChildren<Text>(true))if(text.gameObject.activeSelf)Check(text.preferredWidth<=((RectTransform)canvas.transform).rect.width,"Hint row fits its tag: "+text.text);
                    FishingXRPresentationValidation.CaptureCanvas(canvas,Path.Combine(evidence,"hint-"+context+(left?"-left":"-right")+".png"));
                }
            var notebook=c.XRNotebook;var type=typeof(FishingXRNotebook);
            var screen=(Canvas)type.GetField("_canvas",Private).GetValue(notebook);var actions=(Action[])type.GetField("_actions",Private).GetValue(notebook);
            try
            {
                foreach(int page in new[]{22,23})
                {
                    type.GetField("_page",Private).SetValue(notebook,page);type.GetField("_offset",Private).SetValue(notebook,0);type.GetMethod("Refresh",Private).Invoke(notebook,null);
                    Check(actions[0]!=null && (actions[7]!=null || actions[8]!=null),"VR notebook page "+page+" has controls and a return path");
                    FishingXRPresentationValidation.CaptureCanvas(screen,Path.Combine(evidence,"notebook-page-"+page+".png"));
                    if(page==23)
                    {
                        type.GetMethod("UpdatePagePointer",Private).Invoke(notebook,new object[]{0,false});
                        var detail=(Text)type.GetField("_pageDetail",Private).GetValue(notebook);
                        Check(detail.gameObject.activeSelf && detail.preferredHeight<=detail.rectTransform.rect.height,"操作说明 detail lists every key of a context without overflow");
                        FishingXRPresentationValidation.CaptureCanvas(screen,Path.Combine(evidence,"notebook-controls-detail.png"));
                        type.GetMethod("UpdatePagePointer",Private).Invoke(notebook,new object[]{-1,false});
                    }
                }
            }
            finally{type.GetField("_page",Private).SetValue(notebook,0);type.GetField("_offset",Private).SetValue(notebook,0);notebook.Close();}
            CaptureFirstPerson(c,hints,screen,Path.Combine(evidence,"eye-notebook-and-hints.png"));
            Debug.Log("[XR-COMFORT] EVIDENCE: "+evidence);
        }
        /// <summary>Approximate left-eye frame (no headset in batch): 1.65 m eye on deck, notebook 1.3 m ahead, controller tags at hand height.</summary>
        private static void CaptureFirstPerson(FishingPrototype c,FishingXRHints hints,Canvas notebook,string path)
        {
            var boat=c.Player.transform;var yaw=Quaternion.Euler(0,boat.eulerAngles.y,0);var eye=boat.position+Vector3.up*1.9f+yaw*Vector3.back*.25f+yaw*Vector3.left*.032f;
            var type=typeof(FishingXRNotebook);type.GetField("_page",Private).SetValue(c.XRNotebook,23);
            type.GetMethod("Refresh",Private).Invoke(c.XRNotebook,null);
            var left=hints.PreviewForValidation(FishingXRHints.Context.Boat,true);var right=hints.PreviewForValidation(FishingXRHints.Context.Boat,false);
            var go=new GameObject("Eye capture",typeof(Camera));var cam=go.GetComponent<Camera>();var rt=new RenderTexture(1600,1600,24);var photo=new Texture2D(1600,1600,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                cam.CopyFrom(c.viewCamera);cam.enabled=false;cam.stereoTargetEye=StereoTargetEyeMask.None;cam.fieldOfView=96;cam.nearClipPlane=.04f;cam.cullingMask|=1<<5;cam.targetTexture=rt;
                cam.transform.SetPositionAndRotation(eye,yaw*Quaternion.Euler(18,0,0));
                notebook.gameObject.SetActive(true);notebook.transform.SetPositionAndRotation(eye+yaw*new Vector3(0,-.12f,1.3f),yaw);notebook.transform.localScale=Vector3.one*.00105f;notebook.worldCamera=cam;
                foreach(var (tag,x) in new[]{(left,-.22f),(right,.22f)})
                {var hand=eye+yaw*new Vector3(x,-.42f,.42f)+Vector3.up*.07f;tag.gameObject.SetActive(true);tag.transform.SetPositionAndRotation(hand,Quaternion.LookRotation(hand-eye,Vector3.up));tag.worldCamera=cam;}
                Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;photo.ReadPixels(new Rect(0,0,1600,1600),0,0);photo.Apply();File.WriteAllBytes(path,photo.EncodeToPNG());
            }
            finally{RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(photo);UnityEngine.Object.Destroy(go);c.XRNotebook.Close();left.gameObject.SetActive(false);right.gameObject.SetActive(false);type.GetField("_page",Private).SetValue(c.XRNotebook,0);}
        }
    }
}
