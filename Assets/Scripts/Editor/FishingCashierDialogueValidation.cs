using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.UI;
namespace BoatAttack.EditorTools
{
    public static class FishingCashierDialogueValidation
    {
        public static void Run()=>FishingV011Validation.Begin("CASHIER-DIALOGUE");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[CASHIER-DIALOGUE] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            const string output="Logs/CashierDialogue";Directory.CreateDirectory(output);
            var shop=c.TackleShop;var residents=c.Residents;
            for(int i=0;i<8;i++)c.Career.CompleteShoreTask(i);c.Career.ClaimShoreRewards();
            Check(c.Career.Backpack.TryAdd(FieldItem.HuntingRifle,1),"fixture owns real gun to verify dialogue fire suppression");c.SelectFieldSlot(1);
            var rb=c.Player.engine.RB;rb.position=shop.mooringPoint.position;rb.rotation=shop.transform.rotation;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            Check(c.Shore.TryDisembark() && c.Shore.Relocate(shop.purchasePoint.position,shop.transform.rotation),"reach original female cashier through production landing");
            for(int i=0;i<10;i++)yield return null;
            Check(residents.Person(4)==shop.Cashier.transform && FishingShopCashier.AdultHeight==1.98f,"dialogue uses the user's enlarged original cashier model");
            int coins=c.Career.Coins,slots=c.Career.Backpack.UsedSlots;var feet=c.Shore.Position;
            Check(c.RequestWorldInteraction() && residents.ActiveId==4 && residents.ChoiceCount==4,"real E route opens four-topic cashier conversation");
            Check(residents.Speech.Contains("第一次"),"first real cashier encounter has its own greeting");
            c.Shore.Move(Vector2.up,true,false,false,.1f);
            Check(c.Shore.Position==feet && !shop.BuySupply(0) && !c.RifleHunting.TryFire() && c.Career.Coins==coins,"dialogue blocks walking, buying and shooting without charging");
            residents.Choose(1);residents.Reveal();
            Check(residents.CashierTopic==CashierTopic.Goods && residents.Speech.Contains(coins+" 金币") && residents.Speech.Contains("制作材料"),"goods speech reflects wallet and actual shop types");
            residents.Choose(3);residents.Choose(2);residents.Choose(0);
            Check(residents.CashierTopic==CashierTopic.Stations && residents.Speech.Contains("铁镐头") && residents.Speech.Contains("枪械铁件"),"cashier explains the integrated three-station production chain");
            residents.Choose(0);Check(residents.Speech.Contains("铁镐头 ×1"),"tool speech takes current recipe costs");
            residents.Choose(2);residents.Choose(2);Check(residents.CashierTopic==CashierTopic.Lantern && residents.Speech.Contains("F 点亮"),"desktop lamp advice matches supplied glowing lantern");
            residents.Reveal();var speech=residents.Speech;residents.Choose(99);Check(residents.Speech==speech,"stale/out-of-range dialogue choices do not mutate state");
            Check(c.Career.Coins==coins && c.Career.Backpack.UsedSlots==slots,"reading guides gives no free coins or items");

            // Desktop batch validation inspects the shared canvas without bypassing the production XR-only Open guard.
            var book=c.XRNotebook;
            typeof(FishingXRNotebook).GetField("_page",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(book,8);
            typeof(FishingXRNotebook).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(book,null);
            var status=(Text)typeof(FishingXRNotebook).GetField("_status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(book);
            var actions=(Action[])typeof(FishingXRNotebook).GetField("_actions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(book);
            Debug.Log("[CASHIER-DIALOGUE] XR TEXT: height="+status.preferredHeight+" limit="+status.rectTransform.rect.height+" font="+status.fontSize+" actions="+(actions[0]!=null)+","+(actions[1]!=null)+","+(actions[2]!=null)+","+(actions[6]!=null));
            Check(status.preferredHeight<=status.rectTransform.rect.height && actions[0]!=null && actions[1]!=null && actions[2]!=null && actions[6]!=null,"world-space dialogue text fits and shared branch/shop controls exist");
            book.Close();
            var measure=new GUIStyle(GUIStyle.none){font=FishingUIFont.Font,wordWrap=true,fontSize=21};
            while(measure.fontSize>15 && measure.CalcHeight(new GUIContent(speech),712)>176)measure.fontSize--;
            Check(measure.CalcHeight(new GUIContent(speech),712)<=176,"full dialogue fits desktop body instead of overflowing into footer");
            ValidateAllTopics(c,status,measure);
            typeof(FishingXRNotebook).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(book,null);
            var canvas=(Canvas)typeof(FishingXRNotebook).GetField("_canvas",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(book);
            typeof(FishingXRPresentationValidation).GetMethod("CaptureCanvas",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{canvas,Path.Combine(output,"cashier-dialogue-xr.png")});
            c.viewCamera.transform.SetPositionAndRotation(shop.transform.TransformPoint(new Vector3(-1.1f,1.60f,1.2f)),Quaternion.LookRotation(shop.Cashier.FacePosition-shop.transform.TransformPoint(new Vector3(-1.1f,1.60f,1.2f))));
            FishingV016Validation.Capture(c,output,"cashier-original-model");

            Check(residents.BrowseCashierGoods() && shop.Open && !residents.Open && c.Career.Coins==coins,"view goods closes dialogue and opens original shop without buying");yield return null;
            int product=0;var item=FishingSupplies.Items[product];int count=c.Career.Backpack.Count(item);
            Check(shop.BuySupply(product) && c.Career.Coins==coins-FishingSupplies.Price(product) && c.Career.Backpack.Count(item)==count+FishingSupplies.Quantity(product),"actual purchase still uses original price and transactional inventory");
            shop.Close();yield return null;
            Check(residents.Talk(4) && residents.Speech.Contains("又见面"),"repeat encounter reuses persisted meeting flag");
            residents.Choose(3);residents.Choose(1);residents.Choose(0);
            Check(!residents.Open,"home waypoint action closes modal safely");
            for(int i=0;i<3;i++)yield return null;
            Check(c.NavigationTarget==c.HomeNavigationIndex && c.NavigationPosition==c.Home.ApproachPosition,"cashier marks actual home after the consumed close frame");
            var loaded=FishingSaveStore.Load(c.SavePath,out var warning);
            Check(warning=="" && loaded.Lore.Met(4),"cashier encounter survives disk save without a new schema");
            Check(residents.Talk(4),"final talk opens for range invalidation");
            Check(c.Shore.Relocate(shop.LandingPosition,shop.transform.rotation),"move safely away from cashier");for(int i=0;i<4;i++)yield return null;
            Check(!residents.Open && !shop.Open && !residents.BrowseCashierGoods(),"leaving counter closes conversation and blocks remote shopping shortcut");
        }
        private static void ValidateAllTopics(FishingPrototype c,Text status,GUIStyle measure)
        {
            var queue=new Queue<int[]>();var visited=new HashSet<CashierTopic>();queue.Enqueue(new int[0]);
            while(queue.Count>0)
            {
                var path=queue.Dequeue();var node=new FishingCashierConversation();node.Begin(false);
                foreach(int choice in path)node.Choose(choice);
                if(!visited.Add(node.Topic))continue;
                foreach(bool xr in new[]{false,true})
                {
                    var text=node.Speech(c.Career,xr);measure.fontSize=21;
                    while(measure.fontSize>15 && measure.CalcHeight(new GUIContent(text),712)>176)measure.fontSize--;
                    Check(measure.CalcHeight(new GUIContent(text),712)<=176,"desktop text fits "+node.Topic+" / "+xr);
                    status.text="汐禾 · "+node.Title+"\n"+text;status.fontSize=22;
                    while(status.fontSize>20 && status.preferredHeight>status.rectTransform.rect.height)status.fontSize--;
                    Check(status.preferredHeight<=status.rectTransform.rect.height,"world-space text fits "+node.Topic+" / "+xr);
                }
                for(int i=0;i<node.ChoiceCount;i++){var next=new int[path.Length+1];Array.Copy(path,next,path.Length);next[next.Length-1]=i;queue.Enqueue(next);}
            }
            Check(visited.Count==Enum.GetValues(typeof(CashierTopic)).Length,"layout checked for every reachable conversation topic on both platforms");
        }
    }
}
