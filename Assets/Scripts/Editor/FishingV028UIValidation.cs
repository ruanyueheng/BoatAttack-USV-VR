using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV028UIValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V028-UI");
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V028 UI] CHECK: "+message);}
        private static float Luminance(Color color){var c=color.linear;return .2126f*c.r+.7152f*c.g+.0722f*c.b;}
        private static float Contrast(Color a,Color b)=>(Mathf.Max(Luminance(a),Luminance(b))+.05f)/(Mathf.Min(Luminance(a),Luminance(b))+.05f);
        public static IEnumerator Checks(FishingPrototype c)
        {
            var hidden=new GameObject("Inactive theme binding regression",typeof(RectTransform),typeof(Canvas));hidden.SetActive(false);
            try
            {
                var label=new GameObject("Hidden label",typeof(RectTransform),typeof(Text));label.transform.SetParent(hidden.transform,false);
                FishingUITheme.Bind(label.GetComponent<Text>(),2);
                Check(hidden.GetComponent<FishingUIThemeBindings>()!=null && label.GetComponent<Text>().color==FishingUITheme.Ink,"Inactive feedback canvases bind their theme before first display");
            }
            finally{Object.DestroyImmediate(hidden);}
            float early=FishingUITheme.AdvanceNight(0,0,.016f),settled=FishingUITheme.AdvanceNight(0,0,1.5f);
            Check(early>0 && early<.1f && settled>.98f,"Forced time changes ease toward night without a one-frame colour jump");
            Check(FishingUITheme.AdvanceNight(.5f,.49f,100)==1 && FishingUITheme.AdvanceNight(.5f,.51f,100)==0,"Frozen twilight settles on a readable light or dark theme instead of low-contrast grey");
            Check(FishingUITheme.AdvanceNight(.5f,1,0)==.5f,"Paused rendering time does not advance a transition");
            float sixty=0,ninety=0;for(int i=0;i<60;i++)sixty=FishingUITheme.AdvanceNight(sixty,0,1f/60);for(int i=0;i<90;i++)ninety=FishingUITheme.AdvanceNight(ninety,0,1f/90);
            Check(Mathf.Abs(sixty-ninety)<.0001f,"Theme transition duration is stable at desktop and headset frame rates");
            bool headingsComplete=true;
            Check(FishingUITheme.Heading.name=="MaShanZheng-Regular","Game headings load the expressive Ma Shan Zheng brush-script asset");
            foreach(char glyph in "枪械铁件海风烧烤渔屋鱼箱海边烧烤铁镐头沉海宝匣香烤海鱼海漂旧靴锈蚀铁罐")headingsComplete&=FishingUITheme.Heading.HasCharacter(glyph);
            Check(headingsComplete,"Bundled handwritten font covers the V028 crafting and coast headings");
            foreach(var sprite in new[]{FishingUITheme.Coins,FishingUITheme.FishBox})Check(sprite!=null && sprite.texture.width>=128 && sprite.texture.height>=128,"Resource counter has a bundled pictogram: "+sprite.texture.name);
            Check(Resources.Load<TextAsset>("FishingUIPrototype/IconCredits").text.Contains("Delapouite"),"Icon creator, source and attribution license are bundled");
            var original=c.Career.Snapshot();var notebook=c.XRNotebook;var type=typeof(FishingXRNotebook);
            var page=type.GetField("_page",Private);var refresh=type.GetMethod("Refresh",Private);var late=type.GetMethod("LateUpdate",Private);
            var canvas=(Canvas)type.GetField("_canvas",Private).GetValue(notebook);
            var map=(RawImage)type.GetField("_map",Private).GetValue(notebook);
            int oldPage=(int)page.GetValue(notebook);bool active=canvas.gameObject.activeSelf;
            string evidence=Path.GetFullPath("Logs/v028-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            try
            {
                c.ReturnToHarbor();if(!c.ChartExpanded)c.ToggleChart();
                for(int night=0;night<2;night++)
                {
                    var fixture=c.Career.Snapshot();fixture.calendar.minute=night==0?480:1260;fixture.calendar.timeFixed=true;c.RestoreShopTestProfile(fixture);
                    FishingUITheme.Tick(night==0?1:0,100);
                    Check(Contrast(FishingUITheme.Ink,FishingUITheme.Paper)>7,"Primary text has strong contrast in "+(night==0?"day":"night"));
                    Check(Contrast(FishingUITheme.Muted,FishingUITheme.Light)>4.5f,"Secondary text stays readable in "+(night==0?"day":"night"));
                    for(int style=0;style<5;style++)Check(Contrast(FishingUITheme.TitleFill(style),FishingUITheme.Paper)>4.5f,"Coloured glyph fill contrasts with its day/night reading surface: "+night+" style="+style);
                    if(night==0)Check(Luminance(FishingUITheme.Paper)>.70f,"Day reading surfaces use light parchment instead of a fixed dark wash");
                    if(night==0)Check(Luminance(FishingUITheme.MapWater)>.55f,"Day map water belongs to the cream/light palette");
                    else Check(FishingUITheme.Paper.b>FishingUITheme.Paper.r && Luminance(FishingUITheme.Paper)<.04f,"Night paper uses a restrained deep blue slate");
                    string label=night==0?"day":"night";
                    for(int i=0;i<3;i++)yield return null;
                    var gameplayHud=c.GetComponent<FishingHUD>();var mask=(Texture2D)typeof(FishingHUD).GetMethod("ReadingMask",Private).Invoke(gameplayHud,null);
                    Check(mask!=null,"The production reading-mask generator creates the actual gameplay texture");
                    var rt=RenderTexture.GetTemporary(128,64,0,RenderTextureFormat.ARGB32);var oldTarget=RenderTexture.active;var sample=new Texture2D(128,64,TextureFormat.RGBA32,false);
                    try
                    {
                        Graphics.Blit(mask,rt);RenderTexture.active=rt;sample.ReadPixels(new Rect(0,0,128,64),0,0);sample.Apply();var pixel=sample.GetPixel(64,32);
                        Check(pixel.r>.98f && pixel.g>.98f && pixel.b>.98f && pixel.a>.85f,"Rendered reading mask remains neutral so the shared day/night tint controls its colour");
                    }
                    finally{RenderTexture.active=oldTarget;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(sample);}
                    string hud=Path.Combine(evidence,"desktop-hud-"+label+".png");ScreenCapture.CaptureScreenshot(hud);
                    for(int frame=0;frame<10 && !File.Exists(hud);frame++)yield return null;
                    if(File.Exists(hud))Debug.Log("[V028 UI] DESKTOP CAPTURE: "+hud+" ("+Screen.width+"x"+Screen.height+")");
                    else Debug.LogWarning("[V028 UI] Desktop capture unavailable in this batch session; use the 1600x900 standalone acceptance capture.");
                    foreach(int p in new[]{0,12})
                    {
                        page.SetValue(notebook,p);refresh.Invoke(notebook,null);canvas.gameObject.SetActive(true);late.Invoke(notebook,null);
                        canvas.GetComponent<FishingUIThemeBindings>().SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();
                        foreach(var text in canvas.GetComponentsInChildren<Text>())if(!string.IsNullOrEmpty(text.text))
                            Check(text.fontSize>=20 && text.preferredHeight<=text.rectTransform.rect.height+.5f,"Day/night notebook text fits: "+text.text.Replace('\n',' '));
                        if(p==12)
                        {
                            Check(map.material.shader.name=="Fishing/TerrainMap" && map.material.GetFloat("_Circular")==0 && map.material.GetFloat("_ZTest")==4,"XR map uses the shared theme and retains world depth testing");
                            Check(map.material.GetColor("_Water")!=(Color)new Color(.10f,.24f,.28f),"Map shader receives the current water palette");
                        }
                        string capture=Path.Combine(evidence,"xr-"+(p==0?"home":"map")+"-"+label+".png");
                        FishingXRPresentationValidation.CaptureCanvas(canvas,capture);canvas.gameObject.SetActive(false);
                        if(p==12)
                        {
                            var photo=new Texture2D(2,2);
                            try
                            {
                                Check(photo.LoadImage(File.ReadAllBytes(capture)),"Rendered chart capture is readable");
                                var pixels=photo.GetPixels32();var expected=(Color32)FishingUITheme.MapWater;int waterPixels=0;
                                foreach(var pixel in pixels)if(Mathf.Abs(pixel.r-expected.r)+Mathf.Abs(pixel.g-expected.g)+Mathf.Abs(pixel.b-expected.b)<=8)waterPixels++;
                                Check(waterPixels>pixels.Length/50,"Rendered "+label+" chart water matches its intended palette instead of being misclassified as land");
                            }
                            finally{Object.DestroyImmediate(photo);}
                        }
                    }
                }
            }
            finally
            {
                c.RestoreShopTestProfile(original);FishingUITheme.Tick(c.Career.Calendar.Daylight,100);page.SetValue(notebook,oldPage);refresh.Invoke(notebook,null);canvas.gameObject.SetActive(active);
            }
            Debug.Log("[V028 UI] EVIDENCE: "+evidence);
        }
    }
}
