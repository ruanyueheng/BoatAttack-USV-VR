using System;
using System.Collections;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV036UIValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V036-UI");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V036 UI] CHECK: "+label);}
        private static IEnumerator Capture(string output,string label)
        {
            string path=Path.Combine(output,label+".png");ScreenCapture.CaptureScreenshot(path);
            for(int frame=0;frame<6;frame++)yield return null;
            if(!File.Exists(path))Debug.Log("[V036 UI] IMGUI screen capture needs a visible player window: "+label);
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            var output=Path.GetFullPath("Logs/V036/UI");Directory.CreateDirectory(output);
            var hud=c.GetComponent<FishingHUD>();var original=c.Career.Snapshot();bool night=c.Career.Calendar.Night;
            Check(FishingPaperDesign.Vellum.texture.width==1024 && FishingPaperDesign.Vellum.texture.height==768,"Vellum retains its authored resolution");
            Check(ReferenceEquals(FishingPaperDesign.Corner,FishingPaperDesign.Corner) && FishingPaperDesign.Corner.texture.width==192,"Ornament sprite uses one bounded shared cache");
            try
            {
                c.ReturnToHarbor();if(c.StartMenuOpen)c.FinishTitleVoyage();
                for(int palette=0;palette<2;palette++)
                {
                    c.SetDayNight(palette==1);FishingUITheme.Tick(palette==1?0:1,10);yield return null;
                    c.OpenStartMenu();yield return null;
                    var capture=Capture(output,(palette==0?"day":"night")+"-title");while(capture.MoveNext())yield return capture.Current;
                    c.MenuControls=true;
                    for(int tab=0;tab<3;tab++)
                    {
                        hud.SettingsTab=tab;yield return null;
                        capture=Capture(output,(palette==0?"day":"night")+"-settings-"+tab);while(capture.MoveNext())yield return capture.Current;
                    }
                    c.MenuControls=false;c.FinishTitleVoyage();hud.JournalPage=0;c.ToggleJournal();
                    for(int frame=0;frame<20;frame++)yield return null;
                    capture=Capture(output,(palette==0?"day":"night")+"-catalogue");while(capture.MoveNext())yield return capture.Current;
                    hud.JournalPage=5;for(int frame=0;frame<6;frame++)yield return null;
                    capture=Capture(output,(palette==0?"day":"night")+"-islands");while(capture.MoveNext())yield return capture.Current;
                    c.ToggleJournal();c.Letter.ShowIsland(0);c.Letter.Reveal();Canvas.ForceUpdateCanvases();
                    Check(c.Letter.Ready && c.Letter.Paper.GetComponent<Image>().sprite==FishingPaperDesign.Vellum,"Letter shares original ivory vellum");
                    Check(c.Letter.Paper.GetComponentsInChildren<Image>().All(image=>!image.raycastTarget),"Botanical frame, bookmark and crest never block input");
                    foreach(var label in c.Letter.Paper.GetComponentsInChildren<Text>())Check(label.preferredHeight<=label.rectTransform.rect.height+2,"Letter fits: "+label.name);
                    var corners=new Vector3[4];((RectTransform)c.Letter.Paper).GetWorldCorners(corners);
                    foreach(var point in corners)
                    {
                        var v=c.viewCamera.WorldToViewportPoint(point);
                        Check(v.z>.2f && v.x>.02f && v.x<.98f && v.y>.02f && v.y<.98f,"Enlarged letter stays inside the actual camera view");
                    }
                    var root=c.Letter.Paper.parent;var p=root.position;var q=root.rotation;
                    c.Letter.Tick(.1f,false,false);Check(root.position==p && root.rotation==q,"Reading pose remains world locked");
                    FishingV016Validation.Capture(c,output,(palette==0?"day":"night")+"-letter");
                    FishingXRPresentationValidation.CaptureCanvas(c.Letter.Paper.GetComponent<Canvas>(),Path.Combine(output,(palette==0?"day":"night")+"-letter-detail.png"));
                    c.Letter.Close();
                }
            }
            finally
            {
                c.Letter.Close();if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.JournalOpen)c.ToggleJournal();
                c.ReturnToHarbor();c.RestoreShopTestProfile(original);FishingUITheme.Tick(night?0:1,10);
            }
        }
    }
}
