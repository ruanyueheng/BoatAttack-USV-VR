using System;
using System.Collections;
using System.Linq;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingFishSalesValidation
    {
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[FISH-SALES] CHECK: "+text);}
        private static CatchRecord Fish(string id,FishKind kind,bool rare=false)
        {
            int grams=FishingSpecies.Grams(kind,1);
            return new CatchRecord{id=id,kind=kind,rare=rare,grams=grams,value=FishingSpecies.Value(kind,rare,grams)};
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Sales use an isolated profile");
            var original=c.Career.Snapshot();
            try
            {
                var paged=new FishingCareer();paged.SetCreativeMode(true);
                Check(paged.Upgrade(UpgradeTrack.Hold),"Paging fixture has room for more than six fish");
                for(int i=0;i<7;i++)Check(paged.Store(Fish("sale-page-"+i,FishKind.Coral)),"Paging fixture stores fish "+i);
                c.RestoreShopTestProfile(paged.Snapshot());
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var notebook=c.XRNotebook;var type=typeof(FishingXRNotebook);
                type.GetField("_page",flags).SetValue(notebook,13);type.GetField("_offset",flags).SetValue(notebook,0);
                type.GetMethod("Refresh",flags).Invoke(notebook,null);
                var actions=(Action[])type.GetField("_actions",flags).GetValue(notebook);
                var labels=(UnityEngine.UI.Text[])type.GetField("_labels",flags).GetValue(notebook);
                Check(actions[8]!=null,"Fish box exposes the second page");actions[8]();
                Check((int)type.GetField("_offset",flags).GetValue(notebook)==6 && labels[6].text=="上一页" && actions[6]!=null,"The sale shortcut preserves the previous-page button");
                actions[6]();
                Check((int)type.GetField("_offset",flags).GetValue(notebook)==0 && labels[7].text.Contains("卖鱼") && actions[7]!=null,"The first fish page and sale shortcut remain reachable together");
                var fixture=new FishingCareer();fixture.MarkOrder();
                var line=fixture.CurrentOrder.Line(0);
                var reserved=Fish("sale-reserved",line.Kind,line.Rare);
                var spare=Fish("sale-spare",Enum.GetValues(typeof(FishKind)).Cast<FishKind>().First(k=>!FishingSpecies.Of(k).Legendary && !Enumerable.Range(0,fixture.CurrentOrder.LineCount).Any(i=>fixture.CurrentOrder.Line(i).Kind==k)));
                Check(fixture.Store(reserved) && fixture.Store(spare) && fixture.ReservedCount==1 && fixture.SaleValue==spare.value,"Fixture separates reserved order fish from saleable fish");
                c.ReturnToHarbor();c.RestoreShopTestProfile(fixture.Snapshot());yield return null;yield return null;
                Check(c.CanSellFish,"A stopped boat in the original harbor can sell");
                int coins=c.Career.Coins;
                Check(!c.XRNotebook.ConfirmFishSale(false) && c.Career.Coins==coins && c.Career.Count==2,"First sale click only quotes, without consuming fish");
                Check(c.XRNotebook.ConfirmFishSale(false) && c.Career.Count==1 && c.Career.Inventory[0].id==reserved.id && c.Career.Coins==coins+spare.value,"Confirmed normal sale preserves the order fish and pays exactly once");
                Check(!c.XRNotebook.ConfirmFishSale(false) && c.Career.Count==1,"Repeated normal sale cannot pay twice");
                c.MoveToSpotForAutomation(0);yield return null;yield return null;
                Check(!c.CanSellFish && !c.XRNotebook.ConfirmFishSale(true) && c.Career.Count==1,"Open-sea sales are rejected, even through a stale page");
                c.LeaveStation();yield return null;yield return null;
                var shop=c.TackleShop;var rb=c.Player.engine.RB;
                rb.position=shop.mooringPoint.position;rb.rotation=shop.transform.rotation;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
                Check(c.Shore.TryDisembark() && c.Shore.Relocate(shop.purchasePoint.position,shop.transform.rotation),"Reach the original shop counter by production landing");
                yield return null;yield return null;
                Check(c.Ashore && shop.NearCounter && c.CanSellFish,"The female cashier counter can buy the boat's stored fish while the owner is ashore");
                Check(!c.XRNotebook.ConfirmFishSale(true),"Selling reserved fish still needs confirmation at the counter");
                var changed=Fish("sale-changed",spare.kind);Check(c.Career.Store(changed),"Catch box can change before confirmation");
                Check(!c.XRNotebook.ConfirmFishSale(true) && c.Career.Count==2,"Changed inventory invalidates the old quote instead of selling silently");
                c.TogglePause();Check(!c.XRNotebook.ConfirmFishSale(true) && c.Career.Count==2,"Pause blocks a pending sale");c.TogglePause();
                yield return null;yield return null;
                int total=c.Career.HoldValue;coins=c.Career.Coins;
                Check(!c.XRNotebook.ConfirmFishSale(true) && c.XRNotebook.ConfirmFishSale(true) && c.Career.Count==0 && c.Career.Coins==coins+total,"Explicit all-fish sale works at the shop and includes the reserved fish once");
                Check(!c.XRNotebook.ConfirmFishSale(true),"Empty box cannot pay again");
            }
            finally{if(c.Paused)c.TogglePause();c.XRNotebook.Close();c.ReturnToHarbor();c.RestoreShopTestProfile(original);}
        }
    }
}
