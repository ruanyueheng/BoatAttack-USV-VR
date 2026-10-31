using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using BoatAttack.VR;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingSeaMarketValidation
    {
        public static void Run()=>FishingV011Validation.Begin("SEA-MARKET");
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[SEA-MARKET] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"market checks use an isolated voyage");
            if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.Paused)c.TogglePause();
            c.ClassroomYear=1;c.ClassroomSeason=0;c.ClassroomDate=14;c.ClassroomHour=10;c.ApplyClassroomDate();
            yield return null;yield return null;
            c.Shore.BoardImmediately();c.Session.Cancel();
            var market=c.SeaMarket;var ship=(Transform)typeof(FishingSeaMarketWorld).GetField("_ship",Private).GetValue(market);
            string dir=Path.GetFullPath("Logs/V054/Visual");Directory.CreateDirectory(dir);
            var stall=ship.Find("Stall Keepsake 3");var label=stall.GetComponentInChildren<TextMesh>();
            var checkout=ship.Find("Market purchase instructions").GetComponent<TextMesh>();
            var hulls=ship.GetComponentsInChildren<BoxCollider>().Where(x=>x.name.StartsWith("Solid market hull ")).ToArray();
            Check(market.Visible && hulls.Length==5 && hulls.All(x=>x.enabled && !x.isTrigger && x.attachedRigidbody.isKinematic),"scheduled ship has a solid compound hull and deck");
            Physics.SyncTransforms();
            Check(Physics.SphereCast(market.Position-ship.right*10+Vector3.up*.8f,.3f,ship.right,out var hit,20,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore)
                && hit.collider.transform.IsChildOf(ship),"a boat-height approach sweep is blocked by the market hull");

            var rb=c.Player.engine.RB;var p=market.Position-ship.right*10+Vector3.up*.5f;
            rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=p;c.Player.transform.position=p;Physics.SyncTransforms();
            void Look(Vector3 target)=>c.viewCamera.transform.SetPositionAndRotation(p+Vector3.up*1.7f,Quaternion.LookRotation(target-(p+Vector3.up*1.7f)));
            Look(label.transform.position);typeof(FishingSeaMarketWorld).GetMethod("Gaze",Private).Invoke(market,null);
            Check(market.Target==MarketGoods.Keepsake && label.text.Contains("60") && label.text.Contains("购买"),"looking at a festival price card explicitly selects its purchase action");
            var capture=Capture(c,dir,"market-purchase",p+Vector3.up*1.7f,label.transform.position,label);
            try{while(capture.MoveNext())yield return capture.Current;}finally{(capture as IDisposable)?.Dispose();}
            string source=label.text;
            FishingXRText.Refresh(true);
            Check(label.text.Contains("60") && label.text.Contains("按 A 购买") && !FishingXRText.Forbidden.IsMatch(label.text),"XR mode preserves the live product name and price and adapts the purchase key");
            FishingXRText.Refresh(false);Check(label.text==source,"leaving XR restores the current product text instead of the original empty label");
            var staticLabel=new GameObject("Static XR binding regression").AddComponent<TextMesh>();
            try
            {
                FishingXRText.Bind(staticLabel,"E 静态交互");FishingXRText.Refresh(true);
                FishingXRText.Bind(label,source);FishingXRText.Tick();
                Check(staticLabel.text=="E 静态交互","a dynamic rebind cannot consume the global XR mode change before static signs refresh");
            }
            finally{UnityEngine.Object.Destroy(staticLabel.gameObject);}

            Look(p-ship.right*20);typeof(FishingSeaMarketWorld).GetMethod("Gaze",Private).Invoke(market,null);
            int balance=c.Career.Coins;
            Check(market.Target==null && !market.TryInteract() && c.Career.Coins==balance,"looking away cannot accidentally buy a hidden or rearward stall");
            Look(label.transform.position);
            typeof(FishingPrototype).GetProperty("Anchored").SetValue(c,true);
            c.Interact();
            Check(c.Career.Coins==balance-FishingSeaMarket.KeepsakePrice && FishingSeaMarket.Bit(c.Career.Lore.SeaPost,FishingSeaMarket.KeepsakeBit)==1,
                "normal interaction buys a selected festival item even while moored");
            Check(checkout.text.Contains("收藏纪念品") && label.text.Contains("已购"),"purchase receipt and sold state stay visible in the world for VR players");
            c.Letter.Close();
            capture=Capture(c,dir,"market-receipt",p+Vector3.up*1.7f,checkout.transform.position,checkout);
            try{while(capture.MoveNext())yield return capture.Current;}finally{(capture as IDisposable)?.Dispose();}
            balance=c.Career.Coins;Check(!market.Trade(MarketGoods.Keepsake) && c.Career.Coins==balance,"a collected keepsake cannot charge the player twice");
            c.TogglePause();Look(ship.Find("Stall Seeds 1").GetComponentInChildren<TextMesh>().transform.position);
            Check(!market.TryInteract() && c.Career.Coins==balance,"paused or modal input cannot spend market coins");c.TogglePause();
            Check(!market.Trade((MarketGoods)99) && !market.Trade(MarketGoods.Letter,99) && c.Career.Coins==balance,"invalid market choices have no economic side effects");
            c.Session.Cancel();Look(p-ship.right*20);typeof(FishingSeaMarketWorld).GetMethod("Gaze",Private).Invoke(market,null);
            Check(market.Target==null && c.Anchored && c.Session.Phase==CatchPhase.Idle,"without an explicit market target the moored fishing state remains intact");

            // Use the real boat Rigidbody and hull contacts, with only input isolated.
            typeof(FishingPrototype).GetProperty("Anchored").SetValue(c,false);
            var router=USVInputRouter.Instance;bool enabled=router.enabled;router.enabled=false;
            typeof(USVInputRouter).GetProperty("Current").SetValue(router,default(USVInputState));
            var probe=c.Player.gameObject.AddComponent<FishingMarketContactProbe>();
            try
            {
                p=market.Position-ship.right*9+Vector3.up*.5f;
                rb.position=p;rb.rotation=Quaternion.LookRotation(ship.right);c.Player.transform.SetPositionAndRotation(p,rb.rotation);
                rb.angularVelocity=Vector3.zero;rb.velocity=ship.right*6;Physics.SyncTransforms();
                float until=Time.time+4;while(Time.time<until && !probe.Contact)yield return null;
                Check(probe.Contact && ship.InverseTransformPoint(rb.position).x<0,"the real moving skiff contacts the solid side without passing through the ship");
            }
            finally{UnityEngine.Object.Destroy(probe);rb.velocity=rb.angularVelocity=Vector3.zero;router.enabled=enabled;}

            capture=Capture(c,dir,"market-solid-side",market.Position-ship.right*17+Vector3.up*3,market.Position+Vector3.up*3,checkout);
            try{while(capture.MoveNext())yield return capture.Current;}finally{(capture as IDisposable)?.Dispose();}
            c.ClassroomDate=15;c.ApplyClassroomDate();yield return null;yield return null;
            balance=c.Career.Coins;Check(!market.Trade(MarketGoods.Keepsake) && c.Career.Coins==balance,"festival-only stock cannot be purchased outside a festival");
            c.LeaveClassroomDemo();
        }
        private static IEnumerator Capture(FishingPrototype c,string dir,string name,Vector3 eye,Vector3 target,TextMesh required)
        {
            var camera=c.viewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;bool enabled=c.enabled;
            try
            {
                // Hold only the review camera; production signs need real LateUpdates to face it and fade in.
                c.enabled=false;float until=Time.unscaledTime+.6f;
                while(Time.unscaledTime<until)
                {camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));yield return null;}
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));Physics.SyncTransforms();
                var sign=required.GetComponent<FishingWorldSign>();var text=required.GetComponent<MeshRenderer>();
                var plate=required.GetComponentInChildren<SpriteRenderer>();var viewport=camera.WorldToViewportPoint(text.bounds.center);
                Check(sign.Alpha>.9f && text.enabled && required.color.a>.9f && plate!=null && plate.enabled && text.bounds.size.sqrMagnitude>.001f,
                    name+" captures a fully visible, nonempty production price/instruction plaque after real fade updates");
                Check(viewport.z>0 && viewport.x>.03f && viewport.x<.97f && viewport.y>.03f && viewport.y<.97f
                    && Vector3.Dot(required.transform.forward,required.transform.position-eye)>0
                    && c.Shore.ClearSight(eye,required.transform.position,required.transform),name+" plaque faces the review camera inside its frame without a physics occluder");
                FishingV016Validation.Capture(c,dir,name);
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);c.enabled=enabled;}
        }
    }
    public sealed class FishingMarketContactProbe : MonoBehaviour
    {
        public bool Contact;
        private void OnCollisionEnter(Collision collision){if(collision.collider.name.StartsWith("Solid market hull "))Contact=true;}
    }
}
