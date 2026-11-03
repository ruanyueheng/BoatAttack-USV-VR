using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.EditorTools
{
    public static class FishingV063CalendarValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException(message);Debug.Log("[V063-CALENDAR] CHECK: "+message);}

        // Fixture setup only: the purchase itself must still pass through native A / controller-ray input.
        public static void PrepareMarketReview(FishingPrototype c)
        {
            Check(c.HasIsolatedProfile,"Market input fixture requires the temporary profile");
            c.XRNotebook.Close();c.ReturnToHarbor();
            c.RestoreShopTestProfile(FishingClassroomDemo.Create(1,0,14,10).Snapshot());
            var market=c.SeaMarket;market.ForceOpen=false;
            typeof(FishingSeaMarketWorld).GetMethod("Update",Private).Invoke(market,null);
            var ship=market.transform.Find("V044 roaming market ship");
            var feet=market.Position-ship.right*10;feet.y=c.Player.transform.position.y;
            var rotation=Quaternion.LookRotation(ship.right);var rb=c.Player.engine.RB;
            rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=feet;rb.rotation=rotation;
            c.Player.transform.SetPositionAndRotation(feet,rotation);Physics.SyncTransforms();c.View.ResetLook();
            Check(market.Visible && Vector3.Distance(c.ExplorerPosition,market.Position)<14,"Festival market is actually open and within boarding-side reach");
        }

        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Calendar integration checks use only the isolated career");
            var original=c.Career.Snapshot();int oldPage=(int)typeof(FishingXRNotebook).GetField("_page",Private).GetValue(c.XRNotebook);
            var page=typeof(FishingXRNotebook).GetField("_page",Private);var refresh=typeof(FishingXRNotebook).GetMethod("Refresh",Private);
            string directory=Path.GetFullPath("Logs/V063/Calendar");Directory.CreateDirectory(directory);
            try
            {
                c.ReturnToHarbor();c.RestoreShopTestProfile(FishingClassroomDemo.Create(1,0,20,10).Snapshot());c.Career.Lore.Clear(0);
                yield return null;
                int coins=c.Career.Coins,animals=c.Career.HomeAnimalCount;
                c.ClassroomYear=1;c.ClassroomSeason=0;c.SelectNextSeasonFestival();
                Check(c.ClassroomYear==2 && c.ClassroomDate==14 && c.ClassroomHour==10,"A past spring festival selects the next occurrence instead of rolling the career backwards");
                Check(c.JumpToSelectedCalendarDate() && c.Career.Calendar.Day==125,"The selected next festival changes the real calendar");
                Check(c.Career.Coins==coins && c.Career.HomeAnimalCount==animals && c.Career.Lore.Cleared(0)
                    && FishingCareer.TryRestore(c.Career.Snapshot(),out _),"A date jump preserves money, animals and completed realms with a valid save");
                string before=JsonUtility.ToJson(c.Career.Snapshot());c.ClassroomYear=1;c.ClassroomDate=1;
                Check(!c.JumpToSelectedCalendarDate() && JsonUtility.ToJson(c.Career.Snapshot())==before,"Rejected backward jumps leave the entire career unchanged");
                page.SetValue(c.XRNotebook,3);refresh.Invoke(c.XRNotebook,null);
                var calendar=(GameObject)typeof(FishingXRNotebook).GetField("_calendarSurface",Private).GetValue(c.XRNotebook);
                var buttons=calendar.GetComponentsInChildren<FishingNotebookButton>(true);
                Check(buttons.Count(b=>b.Index>=700 && b.Index<728)==28,"Calendar displays all twenty-eight selectable dates");
                Check(buttons.Single(b=>b.Index==760).Command!=null && buttons.Single(b=>b.Index==763).Command!=null,"Calendar separates preserve-progress jump from explicit independent reset");
                var festivals=(Text[])typeof(FishingXRNotebook).GetField("_calendarFestivals",Private).GetValue(c.XRNotebook);
                Check(festivals[13].text=="风筝节","Spring's festival is marked on day fourteen");
                c.ClassroomSeason=1;refresh.Invoke(c.XRNotebook,null);
                Check(Enumerable.Range(13,3).All(i=>festivals[i].text=="夜光季"),"All three summer festival days are marked");
                Check(!((GameObject)typeof(FishingXRNotebook).GetField("_desktopJournal",Private).GetValue(c.XRNotebook)).activeSelf
                    && !((Text)typeof(FishingXRNotebook).GetField("_status",Private).GetValue(c.XRNotebook)).gameObject.activeSelf,"The calendar replaces the old date-card layout instead of overlaying a second panel");
                Canvas.ForceUpdateCanvases();
                foreach(var text in calendar.GetComponentsInChildren<Text>(true))
                    Check(text.preferredHeight<=text.rectTransform.rect.height+.5f,"Calendar text fits: "+text.name);
                var canvas=(Canvas)typeof(FishingXRNotebook).GetField("_canvas",Private).GetValue(c.XRNotebook);
                FishingXRPresentationValidation.CaptureCanvas(canvas,Path.Combine(directory,"vr-month-calendar.png"));
                page.SetValue(c.XRNotebook,FishingXRNotebook.ClassroomPage);refresh.Invoke(c.XRNotebook,null);
                Check(ReferenceEquals(calendar,typeof(FishingXRNotebook).GetField("_calendarSurface",Private).GetValue(c.XRNotebook)) && calendar.activeSelf,"Settings and journal calendar entries share one month UI");
                c.XRNotebook.Close();PrepareMarketReview(c);yield return null;
                var fishing=c.Session;
                Check(fishing.Cast(FishKind.Coral,false,2,Guid.NewGuid().ToString("N"),reelLevel:2),"Merchant idle-state regression starts a real catch session");
                for(int i=0;i<400 && fishing.Phase==CatchPhase.Waiting;i++)fishing.Tick(.1f,false);
                Check(fishing.Hook(),"Merchant idle-state regression hooks the fish through production rules");
                for(int i=0;i<6000 && fishing.Phase==CatchPhase.Fighting;i++)fishing.Tick(.02f,fishing.Tension<.6f);
                Check(fishing.LandFromNet() && c.Career.Store(fishing.Claim()) && !c.HasPendingCatch && c.SeaMarket.CanBrowse,"A settled caught fish does not block shopping");
                Check(fishing.Cast(FishKind.Coral,false,0,Guid.NewGuid().ToString("N")),"Merchant escaped-state regression starts another cast");
                Check(!c.SeaMarket.CanBrowse,"An unfinished cast still blocks shopping");
                for(int i=0;i<800 && fishing.Phase!=CatchPhase.Escaped;i++)fishing.Tick(.1f,false);
                Check(fishing.Phase==CatchPhase.Escaped && c.SeaMarket.CanBrowse,"An escaped fish does not leave the market permanently unavailable");
                fishing.Cancel();
                Check(!c.SeaMarket.TradeFromNotebook(MarketGoods.Seeds),"Market UI transactions cannot execute while the notebook shop is closed");
                var boat=c.Player.engine.RB;var close=boat.position;boat.position=close+Vector3.right*50;c.Player.transform.position=boat.position;Physics.SyncTransforms();
                Check(!c.SeaMarket.CanBrowse,"A distant player cannot use the market catalogue");
                boat.position=close;c.Player.transform.position=close;boat.velocity=Vector3.forward*4;Physics.SyncTransforms();
                Check(!c.SeaMarket.CanBrowse,"A moving boat cannot browse the market catalogue");
                boat.velocity=Vector3.zero;c.ClassroomYear=1;c.ClassroomSeason=0;c.ClassroomDate=14;c.ClassroomHour=20;
                Check(c.JumpToSelectedCalendarDate() && !c.SeaMarket.CanBrowse,"Market purchases remain blocked after scheduled closing time");
            }
            finally
            {
                c.XRNotebook.Close();c.SeaMarket.ForceOpen=false;c.RestoreShopTestProfile(original);
                page.SetValue(c.XRNotebook,oldPage);refresh.Invoke(c.XRNotebook,null);
            }
        }
    }
}
