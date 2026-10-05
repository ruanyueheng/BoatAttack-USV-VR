using System;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingAutoplay
    {
        private void ValidateBeachShop()
        {
            var c=Controller;var shop=c.Shop;
            if(shop==null)throw new Exception("Shop regression: scene is missing the authored beach shop.");
            var original=c.Career.Snapshot();
            try
            {
                RequireShop(shop.clerkSpaces.Length==2 && shop.customerSpaces.Length==4,"NPC reservation markers missing.");
                RequireShop(shop.Cashier!=null && shop.Cashier.transform.parent==shop.clerkSpaces[0],"Cashier missing from reserved counter position.");
                RequireShop(shop.GetComponentsInChildren<FishingShopCashier>().Length==1,"Duplicate cashiers spawned.");
                RequireShop(FishingShopCashier.Create(shop)==shop.Cashier,"Cashier creation is not idempotent.");
                RequireShop(Vector3.Dot(shop.Cashier.transform.forward,-shop.transform.forward)>.99f,"Cashier faces away from customers.");
                RequireShop(shop.Cashier.GetComponentsInChildren<Collider>().Length==0,"Cashier blocks shop movement.");
                var cashierLods=shop.Cashier.GetComponent<LODGroup>();
                RequireShop(cashierLods!=null && cashierLods.GetLODs().Length==2,"FantasyGirl mesh LODs missing.");
                foreach(var renderer in shop.Cashier.GetComponentsInChildren<Renderer>())
                {
                    var mat=renderer.sharedMaterial;
                    RequireShop(mat!=null && mat.shader.name=="Universal Render Pipeline/Lit" && mat.GetTexture("_BaseMap")!=null && mat.GetTexture("_BumpMap")!=null && mat.GetTexture("_MetallicGlossMap")!=null,"Cashier URP textures missing.");
                    RequireShop(renderer.bounds.size.y>1.68f && renderer.bounds.size.y<1.76f,"Cashier height not human scale.");
                }
                foreach(var marker in shop.clerkSpaces)RequireShop(ClearNpcSpace(marker.position),"Clerk space blocked.");
                foreach(var marker in shop.customerSpaces)RequireShop(ClearNpcSpace(marker.position),"Customer space blocked.");
                RequireShop(!shop.Toggle() && !shop.Buy(UpgradeTrack.Line),"Boat player can remotely shop.");
                SetShoreBoatPose(shop.mooringPoint.position,shop.transform.rotation);
                RequireShop(c.Shore.TryDisembark(),"Cannot disembark at shop landing deck.");
                c.viewCamera.transform.rotation=shop.transform.rotation;
                var landed=shop.transform.InverseTransformPoint(c.Shore.Position);
                var centerline=shop.transform.TransformPoint(new Vector3(0,landed.y,landed.z));
                for(int i=0;i<30 && FishingPrototype.FlatDistance(c.Shore.Position,centerline)>.12f;i++)ShopWalkToward(centerline);
                // Walk the whole deck/ramp/door route through the actual CharacterController.
                for(int i=0;i<400 && Vector3.Distance(c.Shore.Position,shop.purchasePoint.position)> .35f;i++)ShopWalkToward(shop.purchasePoint.position);
                if(Vector3.Distance(c.Shore.Position,shop.purchasePoint.position)>=.5f)
                {
                    foreach(var hit in Physics.OverlapSphere(c.Shore.Position+Vector3.up*.85f,1.4f,~(1<<4),QueryTriggerInteraction.Ignore))
                        Debug.Log("[SHOP-PATH] Nearby collision: "+hit.name+" center="+shop.transform.InverseTransformPoint(hit.bounds.center)+" closest="+shop.transform.InverseTransformPoint(hit.ClosestPoint(c.Shore.Position+Vector3.up*.85f)));
                }
                RequireShop(Vector3.Distance(c.Shore.Position,shop.purchasePoint.position)<.5f,"Ramp/door/counter route blocked at "+shop.transform.InverseTransformPoint(c.Shore.Position));
                RequireShop(shop.Inside && shop.CanBrowse,"Counter does not recognize an inside customer.");
                c.Interact();RequireShop(c.ShopOpen,"E interaction did not open shop.");
                var feet=c.Shore.Position;c.Shore.Walk(Vector2.up,false,.05f);RequireShop(feet==c.Shore.Position,"Player walked while browsing.");
                c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                RequireShop(!shop.Buy(UpgradeTrack.Line) && c.Career.Coins==0,"Insufficient funds allowed purchase.");
                // Fund the shared wallet through real catch settlement/sales, never by assigning coins.
                for(int sale=0;sale<16;sale++)
                {
                    var fish=new CatchRecord{id="shop-income-"+sale,kind=FishKind.Puffer,grams=2000,value=FishingSpecies.Value(FishKind.Puffer,false,2000)};
                    RequireShop(c.Career.BeginPending(fish,2,0,0) && c.Career.ResolvePending(true,out _,out _),"Shop income catch settlement failed.");
                    int before=c.Career.Coins;
                    RequireShop(c.Career.SellAll()==fish.value && c.Career.Coins==before+fish.value,"Fish sale did not credit the shared shop wallet.");
                }
                c.Career.CompleteShoreTask(0);
                RequireShop(c.Career.Backpack.TryCollect(0,FieldItem.Shell,3),"Backpack fixture failed.");
                c.Career.Backpack.Observe(LandAnimalKind.Fox);c.Career.Backpack.Select(2);
                var viewBefore=c.viewCamera.transform.rotation;
                c.View.ApplyShoreLookDelta(new Vector2(100,100));
                c.View.ApplyView(true,false);
                RequireShop(Quaternion.Angle(viewBefore,c.viewCamera.transform.rotation)<.01f && !c.View.IsLooking,"Shop modal allowed shore look.");
                for(int i=0;i<3;i++)
                {
                    var track=(UpgradeTrack)i;int coins=c.Career.Coins,price=c.Career.NextUpgradeCost(track);
                    RequireShop(shop.Buy(track) && c.Career.Level(track)==1 && c.Career.Coins==coins-price,"Purchase did not apply correct price/equipment: "+track);
                }
                var reloaded=FishingSaveStore.Load(c.SavePath,out var warning);
                RequireShop(warning.Length==0 && reloaded.Coins==c.Career.Coins && reloaded.LineLevel==1 && reloaded.ReelLevel==1 && reloaded.HoldLevel==1,"Shop purchase did not persist.");
                RequireShop(reloaded.ShoreTaskMask==1 && reloaded.Backpack.Collected(0) && reloaded.Backpack.Seen(LandAnimalKind.Fox) && reloaded.Backpack.Selected==2,"Shop purchase lost v0.9 exploration/backpack progress.");
                RequireShop(shop.Buy(UpgradeTrack.Line) && c.Career.LineLevel==2,"Second equipment tier failed.");
                int after=c.Career.Coins;RequireShop(!shop.Buy(UpgradeTrack.Line) && c.Career.Coins==after,"Max-level purchase charged coins.");
                c.TogglePause();RequireShop(!shop.Buy(UpgradeTrack.Reel),"Paused shop allowed purchase.");c.TogglePause();
                shop.Close();RequireShop(!shop.Buy(UpgradeTrack.Reel),"Closed shop allowed purchase.");
                c.viewCamera.transform.rotation=shop.transform.rotation*Quaternion.Euler(0,180,0);
                for(int i=0;i<400 && Vector3.Distance(c.Shore.Position,c.Shore.BoardingPoint)>3f;i++)ShopWalkToward(c.Shore.BoardingPoint);
                RequireShop(c.Shore.TryBoard(),"Cannot exit shop and return to boat.");
                RequireShop(!shop.CanBrowse,"Shop remained usable after boarding.");
                Debug.Log("[BEACH-SHOP] PASS: empty NPC spaces, real disembark/ramp/door/interior/exit/boarding route, shop modal, insufficient funds, three equipment purchases, exact charges, max-level/pause/closed guards and saved upgrades.");
            }
            finally{shop.Close();if(c.Paused)c.TogglePause();c.ReturnToHarbor();c.RestoreShopTestProfile(original);c.SelectNavigation(1);}
        }
        private void ShopWalkToward(Vector3 destination)
        {
            var direction=Quaternion.Inverse(Quaternion.Euler(0,Controller.viewCamera.transform.eulerAngles.y,0))*(destination-Controller.Shore.Position);
            Controller.Shore.Walk(new Vector2(direction.x,direction.z).normalized,false,.05f);
        }
        private void SetShoreBoatPose(Vector3 position,Quaternion rotation)
        {
            var rb=Controller.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;
            rb.position=position;rb.rotation=rotation;Controller.Player.transform.SetPositionAndRotation(position,rotation);Physics.SyncTransforms();
        }
        private static bool ClearNpcSpace(Vector3 feet)
            => !Physics.CheckCapsule(feet+Vector3.up*.38f,feet+Vector3.up*1.5f,.3f,~(1<<4),QueryTriggerInteraction.Ignore);
        private static void RequireShop(bool ok,string message){if(!ok)throw new Exception("Shop regression: "+message);}
    }
    public sealed partial class FishingPrototype
    {
        internal void RestoreShopTestProfile(FishingSaveData data)
        {
            if(!Automated || !HasIsolatedProfile)throw new InvalidOperationException("Shop fixtures require automation and an isolated profile.");
            if(!FishingCareer.TryRestore(data,out var career))throw new InvalidOperationException("Invalid shop test profile.");
            Career=career;RefreshHold();Save();
        }
    }
}
