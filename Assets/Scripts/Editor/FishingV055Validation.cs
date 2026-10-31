using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV055Validation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()=>FishingV011Validation.Begin("V055");
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V055] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            // Measure the hull before scene fixtures change the calendar, water and player state.
            foreach(var checks in new[]{FishingV053BoatValidation.Checks(c),FishingV025Validation.Checks(c),Integration(c),FishingLanternValidation.Checks(c),FishingV054Validation.Checks(c)})
            {
                try{while(checks.MoveNext())yield return checks.Current;}
                finally{(checks as IDisposable)?.Dispose();}
            }
        }
        private static IEnumerator Integration(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Merged feature checks require the isolated production profile");
            var book=c.XRNotebook;var page=typeof(FishingXRNotebook).GetField("_page",Private);
            var refresh=typeof(FishingXRNotebook).GetMethod("Refresh",Private);
            var status=(Text)typeof(FishingXRNotebook).GetField("_status",Private).GetValue(book);
            var title=(Text)typeof(FishingXRNotebook).GetField("_pageTitle",Private).GetValue(book);
            var labels=(Text[])typeof(FishingXRNotebook).GetField("_labels",Private).GetValue(book);
            var actions=(Action[])typeof(FishingXRNotebook).GetField("_actions",Private).GetValue(book);
            int originalPage=(int)page.GetValue(book);
            var keys=new[]{"FishingXR.LeftPalmXY","FishingXR.RightPalmXY"};
            var existed=new[]{PlayerPrefs.HasKey(keys[0]),PlayerPrefs.HasKey(keys[1])};
            var values=new[]{PlayerPrefs.GetFloat(keys[0]),PlayerPrefs.GetFloat(keys[1])};
            string directory=Path.GetFullPath("Logs/V055/Visual");Directory.CreateDirectory(directory);
            try
            {
                Check(new System.Collections.Generic.HashSet<int>{FishingXRNotebook.ClassroomPage,FishingXRNotebook.HandAlignmentPage,FishingXRNotebook.QuitPage,FishingXRNotebook.CreativePage}.Count==4,"Voyage date, hand alignment, quit and creative pages have independent IDs");
                foreach(int selected in new[]{14,FishingXRNotebook.ClassroomPage,FishingXRNotebook.HandAlignmentPage,FishingXRNotebook.QuitPage,FishingXRNotebook.CreativePage})
                {
                    page.SetValue(book,selected);refresh.Invoke(book,null);Canvas.ForceUpdateCanvases();
                    Check(status.preferredHeight<=status.rectTransform.rect.height,"Merged settings text fits page "+selected);
                    Check(actions[7]!=null && actions[8]!=null,"Merged settings page retains reachable navigation "+selected);
                    if(selected==14)Check(labels[6].text.Contains("日期") && labels[8].text.Contains("手部"),"Date/materials and hand alignment are both present in Settings");
                    if(selected==FishingXRNotebook.ClassroomPage)Check(title.text=="航程设置" && labels[6].text.Contains("日期"),"The original date/materials page is preserved");
                    if(selected==FishingXRNotebook.HandAlignmentPage)
                    {
                        Check(title.text=="手部对齐" && labels[7].text.Contains("退出"),"Alignment keeps its own title and a visible quit entry");
                        FishingHands.SetControllerRoll(true,0);FishingHands.SetControllerRoll(false,0);actions[0]();
                        Check(FishingHands.ControllerRoll(true)==5 && FishingHands.ControllerRoll(false)==0,"Left UI calibration changes only the left hand");
                        actions[3]();Check(FishingHands.ControllerRoll(true)==5 && FishingHands.ControllerRoll(false)==-5,"Right UI calibration changes only the right hand");
                        actions[4]();Check(FishingHands.ControllerRoll(true)==30 && FishingHands.ControllerRoll(false)==30,"Default calibration is reachable through the actual page action");
                        actions[5]();Check(FishingHands.ControllerRoll(true)==0 && FishingHands.ControllerRoll(false)==0,"Zero calibration remains reachable");
                    }
                    if(selected==FishingXRNotebook.QuitPage)Check(title.text=="退出游戏" && labels[1].text.Contains("取消"),"Exit requires a separate confirmation page with cancellation");
                    if(selected==FishingXRNotebook.CreativePage)
                    {
                        Check(title.text=="创造模式" && !c.CreativeMode,"Creative mode is a separate page and starts disabled");
                        int balance=c.Career.Coins;actions[0]();
                        Check(c.CreativeMode && c.Career.CreativeMode && c.Career.CoinDisplay=="∞" && c.Career.Coins==balance,"The real creative page action changes policy and display without replacing the wallet");
                        var data=c.Career.Snapshot();c.RestoreShopTestProfile(data);
                        Check(c.Career.CreativeMode && c.Career.Coins==balance,"Loading a career during this session preserves the chosen purchasing policy");
                        actions[0]();Check(!c.CreativeMode && !c.Career.CreativeMode && c.Career.Coins==balance,"The real page action restores normal pricing and the actual wallet");
                    }
                    FishingXRPresentationValidation.CaptureCanvas((Canvas)typeof(FishingXRNotebook).GetField("_canvas",Private).GetValue(book),Path.Combine(directory,"settings-"+selected+".png"));
                }
            }
            finally
            {
                for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetFloat(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();
                page.SetValue(book,originalPage);refresh.Invoke(book,null);book.Close();
            }
            var hud=c.GetComponent<FishingHUD>();var confirmation=typeof(FishingHUD).GetField("_exitConfirmation",Private);
            bool controls=c.MenuControls;confirmation.SetValue(hud,true);c.MenuControls=false;
            typeof(FishingHUD).GetMethod("Update",Private).Invoke(hud,null);
            Check(!(bool)confirmation.GetValue(hud),"Closing desktop settings clears an old exit confirmation");c.MenuControls=controls;

            string path=c.SavePath,warning=c.SaveWarning;int slot=c.ActiveSaveSlot;
            string blocker=Path.Combine(Path.GetDirectoryName(path),"v055-save-blocker");File.WriteAllText(blocker,"not a directory");
            try
            {
                typeof(FishingPrototype).GetProperty("SavePath").SetValue(c,Path.Combine(blocker,"save.json"));
                Check(!c.QuitGame() && EditorApplication.isPlaying && c.SaveWarning.Length>0,"A real failed save keeps the editor/game running instead of exiting");
                typeof(FishingPrototype).GetProperty("ActiveSaveSlot").SetValue(c,-1);
                Check(!c.QuitGame() && EditorApplication.isPlaying,"A failed save also blocks exit when the original save slot is unavailable");
            }
            finally
            {
                typeof(FishingPrototype).GetProperty("SavePath").SetValue(c,path);typeof(FishingPrototype).GetProperty("ActiveSaveSlot").SetValue(c,slot);
                typeof(FishingPrototype).GetProperty("SaveWarning").SetValue(c,warning);File.Delete(blocker);
            }
            yield return null;
        }
    }
}
