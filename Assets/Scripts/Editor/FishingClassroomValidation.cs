using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingClassroomValidation
    {
        public static void Run()=>FishingV011Validation.Begin("CLASSROOM");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[CLASSROOM] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile && c.SavePath.StartsWith(Application.temporaryCachePath),"isolated automation only");
            string normalPath=c.SavePath;int normalSlot=c.ActiveSaveSlot;int coins=c.Career.Coins;
            string dir=Path.GetFullPath("Logs/V053/Classroom");Directory.CreateDirectory(dir);
            FishingSaveStore.Save(Path.Combine(dir,"classroom-demo.json"),FishingClassroomDemo.Create());
            c.OpenClassroomMenu();Check(c.ClassroomMenuOpen,"classroom entry opens at a safe checkpoint");
            c.ClassroomYear=2;c.ClassroomSeason=3;c.ClassroomDate=28;c.ClassroomHour=22;c.ApplyClassroomDate();
            Check(c.ClassroomDemoActive && c.SavePath!=normalPath && Path.GetDirectoryName(c.SavePath).EndsWith("ClassroomDemo"),"classroom uses a separate save namespace");
            Check(c.Career.Calendar.Year==2 && c.Career.Calendar.Season==3 && c.Career.Calendar.Date==28 && (int)c.Career.Calendar.Hour==22,"exact selected date applies");
            Check(!c.Career.Calendar.Automatic && c.Career.AquariumCount==4 && c.Career.HomeAnimalCount==6 && c.Ashore,"funded populated showcase starts inside home with fixed time");
            string preserved=File.ReadAllText(normalPath);
            typeof(FishingPrototype).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);
            Check(File.ReadAllText(normalPath)==preserved,"demonstration autosave cannot write player profile");
            yield return null;
            c.ClassroomYear=1;c.ClassroomSeason=0;c.ClassroomDate=1;c.ClassroomHour=0;c.ApplyClassroomDate();
            Check(c.Career.Calendar.Day==0 && c.Career.Calendar.Minute==0 && c.Career.Coins>90000 && c.Career.Farm.Ready(0),"earlier date rebuilds a valid funded demonstration without temporal corruption");
            Check(File.ReadAllText(normalPath)==preserved,"date reset leaves player profile untouched");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(FishingXRNotebook).GetField("_page",flags).SetValue(c.XRNotebook,FishingXRNotebook.ClassroomPage);
            typeof(FishingXRNotebook).GetMethod("Refresh",flags).Invoke(c.XRNotebook,null);
            var calendar=(GameObject)typeof(FishingXRNotebook).GetField("_calendarSurface",flags).GetValue(c.XRNotebook);
            var calendarButtons=calendar.GetComponentsInChildren<FishingNotebookButton>(true);
            Check(Array.Find(calendarButtons,b=>b.Index==763)?.Command!=null && Array.Find(calendarButtons,b=>b.Index==764)?.Command!=null,"Unified XR calendar exposes explicit independent reset and return-to-voyage actions");
            Check(Array.FindAll(calendarButtons,b=>b.Index>=700 && b.Index<728).Length==28,"XR calendar has twenty-eight selectable date cells");
            var status=(Text)typeof(FishingXRNotebook).GetField("_calendarSelection",flags).GetValue(c.XRNotebook);
            Check(status.preferredHeight<=status.rectTransform.rect.height,"calendar selection details fit the reading panel");
            c.LeaveClassroomDemo();
            Check(!c.ClassroomDemoActive && c.SavePath==normalPath && c.ActiveSaveSlot==normalSlot && c.Career.Coins==coins,"leaving restores original voyage and active slot");
            c.ContinueClassroomDemo();
            Check(c.ClassroomDemoActive,"saved classroom voyage can be resumed");
            var demoPath=c.SavePath;
            var alternate=FishingClassroomDemo.Create(3,2,14,18);
            c.LeaveClassroomDemo();
            FishingSlotStore.Write(demoPath,2,FishingSlotKind.Manual,alternate,new FishingResumeData(),"classroom-alternate");
            c.ContinueClassroomDemo();
            Check(c.ActiveSaveSlot==2 && c.Career.Calendar.Year==3 && c.Career.Calendar.Season==2,"continue finds newest classroom progress in any independent slot");
            c.LeaveClassroomDemo();
            Check(File.ReadAllText(normalPath)==preserved && c.Career.Coins==coins,"multiple classroom entries never replace normal career");
            c.ClassroomYear=1;c.ClassroomSeason=0;c.ClassroomDate=14;c.ClassroomHour=10;c.ApplyClassroomDate();
            yield return null;yield return null;
            var market=c.SeaMarket;
            Check(!market.ForceOpen && market.Visible && FishingSeaMarket.Open(c.Career.Calendar.Day,c.Career.Calendar.Hour),"selected festival date spawns the real scheduled market without a force-open shortcut");
            c.Shore.BoardImmediately();
            var ship=(Transform)typeof(FishingSeaMarketWorld).GetField("_ship",flags).GetValue(market);
            var stall=ship.Find("Stall Keepsake 3");
            var p=market.Position-ship.right*10+Vector3.up*.5f;var rb=c.Player.engine.RB;
            rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=p;c.Player.transform.position=p;Physics.SyncTransforms();
            c.viewCamera.transform.SetPositionAndRotation(p+Vector3.up*1.7f,Quaternion.LookRotation(stall.position-(p+Vector3.up*1.7f)));
            typeof(FishingSeaMarketWorld).GetMethod("Gaze",flags).Invoke(market,null);
            Check(market.Target==MarketGoods.Keepsake,"look at the real festival stall to select its limited goods");
            int balance=c.Career.Coins;c.Interact();
            Check(c.Career.Coins==balance-FishingSeaMarket.KeepsakePrice && FishingSeaMarket.Bit(c.Career.Lore.SeaPost,FishingSeaMarket.KeepsakeBit)==1,"normal interaction buys the limited item at its real price");
            c.Letter.Close();
            Check(market.Trade(MarketGoods.Bait) && market.BaitActive,"same visit buys seasonal bait");
            c.ApplyClassroomDate();yield return null;yield return null;
            Check(!market.BaitActive && market.Visible,"new independent voyage clears the previous visitor's bait purchase");
            c.ClassroomHour=19;c.ApplyClassroomDate();yield return null;yield return null;
            Check(!market.Visible,"at 19:00 the scheduled ship closes normally");
            c.LeaveClassroomDemo();
            Check(!c.ClassroomDemoActive && c.SavePath==normalPath && c.Career.Coins==coins,"market visit leaves the original voyage intact");
        }
    }
}
