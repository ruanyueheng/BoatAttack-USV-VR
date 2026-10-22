using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV037UIValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V037-UI");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V037-UI] CHECK: "+label);}
        private static float Luminance(Color color)
        {
            color=color.linear;return color.r*.2126f+color.g*.7152f+color.b*.0722f;
        }
        private static void CheckSettingsContrast(Texture2D texture)
        {
            var target=RenderTexture.GetTemporary(96,54,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(96,54,TextureFormat.RGB24,false);var active=RenderTexture.active;
            try
            {
                Graphics.Blit(texture,target);RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,96,54),0,0);pixels.Apply();
                float minimum=1,maximum=0,contrast=float.MaxValue;
                float ink=Luminance(FishingUITheme.Ink);
                for(int y=10;y<44;y++)for(int x=24;x<85;x++)
                {
                    var paper=pixels.GetPixel(x,y);float raw=Luminance(paper);
                    minimum=Mathf.Min(minimum,raw);maximum=Mathf.Max(maximum,raw);
                    float background=Luminance(paper*FishingPaperDesign.SettingsTint);
                    contrast=Mathf.Min(contrast,(Mathf.Max(ink,background)+.05f)/(Mathf.Min(ink,background)+.05f));
                }
                Check(maximum-minimum<.2f,"Settings controls retain a quiet painted reading area");
                Check(contrast>=4.5f,"Settings body text contrast is at least 4.5:1 in "+(FishingUITheme.NightAmount>.5f?"night":"day")+" palette: "+contrast.ToString("F2"));
            }
            finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(pixels);}
        }
        private static Canvas Present(FishingXRNotebook notebook,int page)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(FishingXRNotebook).GetField("_page",flags).SetValue(notebook,page);
            typeof(FishingXRNotebook).GetField("_offset",flags).SetValue(notebook,0);
            typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(notebook,true);
            var canvas=(Canvas)typeof(FishingXRNotebook).GetField("_canvas",flags).GetValue(notebook);
            canvas.gameObject.SetActive(true);
            typeof(FishingXRNotebook).GetMethod("Refresh",flags).Invoke(notebook,null);
            return canvas;
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Directory.CreateDirectory("Docs/Images/V037");
            var celestial=FishingPaperDesign.Surface(FishingPaperTheme.Celestial);
            var herbarium=FishingPaperDesign.Surface(FishingPaperTheme.Herbarium);
            var workshop=FishingPaperDesign.Surface(FishingPaperTheme.Workshop);
            var settings=FishingPaperDesign.Surface(FishingPaperTheme.DreamSettings);
            Check(celestial.texture!=herbarium.texture && herbarium.texture!=workshop.texture && workshop.texture!=celestial.texture,"Three themes use distinct generated textures");
            Check(settings.texture!=celestial.texture && settings.texture!=herbarium.texture && settings.texture!=workshop.texture,"Settings have their own continuous watercolor composition");
            Check(celestial==FishingPaperDesign.Surface(FishingPaperTheme.Celestial),"Theme sprites are cached");
            Check(settings==FishingPaperDesign.Surface(FishingPaperTheme.DreamSettings),"Settings sprite is cached");
            foreach(var surface in new[]{celestial,herbarium,workshop,settings})Check(surface.texture.width<=1024 && surface.texture.height<=1024,"Imported theme has a bounded 1024-pixel budget");
            bool night=c.Career.Calendar.Night,controllerEnabled=c.enabled;
            try
            {
                foreach(bool dark in new[]{false,true})
                {
                    FishingUITheme.Tick(dark?0:1,10);CheckSettingsContrast(settings.texture);
                }
                FishingUITheme.Tick(night?0:1,10);
                if(c.StartMenuOpen)c.FinishTitleVoyage();
                // The desktop input bridge otherwise closes this explicitly simulated XR notebook.
                c.enabled=false;
                foreach(int page in new[]{0,2,10,12,14,15,18})
                {
                    var canvas=Present(c.XRNotebook,page);yield return null;
                    Check(canvas!=null && canvas.GetComponent<Image>().sprite==FishingPaperDesign.Surface(FishingPaperDesign.NotebookTheme(page)),"Actual notebook page uses its topic: "+page);
                    Check(Array.TrueForAll(canvas.GetComponentsInChildren<Image>(),image=>!image.raycastTarget),"Decoration cannot intercept controls: "+page);
                    var title=(Text)typeof(FishingXRNotebook).GetField("_pageTitle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c.XRNotebook);
                    if(page!=0)Check(title.text.Length>0 && title.preferredHeight<=title.rectTransform.rect.height+1,"The themed page title remains visible: "+page);
                    var decor=canvas.transform.Find(page==0?"Handpainted notebook home":"Coastal paper feature pages");
                    var footer=decor.Find("Footer reading strip").GetComponent<Image>();
                    Check(footer.gameObject.activeInHierarchy && footer.color.a>.95f && footer.rectTransform.rect.height>=43,"The footer has an opaque reading surface over the illustrated sea: "+page);
                    FishingXRPresentationValidation.CaptureCanvas(canvas,"Docs/Images/V037/notebook-"+page+".png");
                    c.XRNotebook.Close();
                }
                Present(c.XRNotebook,10);c.SetDayNight(true);FishingUITheme.Tick(0,10);
                for(int frame=0;frame<15;frame++)yield return null;
                var panel=c.XRNotebook.GetComponentInChildren<Canvas>().GetComponent<Image>();
                Check(c.XRNotebook.IsOpen && panel.color==FishingPaperDesign.PaperTint,"Night transition keeps texture tint without a competing palette binding");
                c.XRNotebook.Close();
            }
            finally{c.XRNotebook.Close();c.enabled=controllerEnabled;c.SetDayNight(night);FishingUITheme.Tick(night?0:1,10);}
            var legacy=FishingV036UIValidation.Checks(c);
            try{while(legacy.MoveNext())yield return legacy.Current;}finally{(legacy as IDisposable)?.Dispose();}
        }
    }
}
