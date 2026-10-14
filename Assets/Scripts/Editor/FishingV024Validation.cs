using System;
using System.Collections;
using System.IO;
using BoatAttack.Fishing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingV024Validation
    {
        public static void Run() => FishingV011Validation.Begin("V024");
        private static void Check(bool ok,string text)
        { if(!ok)throw new InvalidOperationException(text);Debug.Log("[V024] CHECK: "+text); }
        public static IEnumerator Checks(FishingPrototype c)
        {
            string evidence=Path.GetFullPath("Logs/v024-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
            for(int i=0;i<35;i++)yield return null;
            var home=c.Home;var shop=c.TackleShop;var market=c.transform.Find("Tide and Fin floating fish market");
            Check(FishingPrototype.Version=="0.24.0" && home!=null && shop!=null && market!=null,"V024 production home, shop and harbor market exist");
            Check(FishingPrototype.FlatDistance(home.Origin,c.harbor)>40 && FishingPrototype.FlatDistance(home.Origin,shop.Position)>40,"Home is separated from both shops");
            foreach(var spot in c.fishingSpots)Check(FishingPrototype.FlatDistance(home.Origin,spot)>45,"Home leaves fishing ground clearance");
            var homeColliders=new System.Collections.Generic.List<Collider>(home.House.GetComponentsInChildren<Collider>());
            foreach(string name in new[]{"Home boardwalk","Home boarding platform","Boardwalk foundation","Home landing foundation","Boardwalk handrail","Boardwalk baluster"})
                foreach(Transform child in c.transform)if(child.name==name)homeColliders.AddRange(child.GetComponentsInChildren<Collider>());
            bool marketClear=true,shopClear=true;
            foreach(var col in homeColliders)
            {
                if(!col.enabled || col.isTrigger)continue;
                foreach(var other in market.GetComponentsInChildren<Collider>())
                    if(other.enabled && !other.isTrigger && col.bounds.Intersects(other.bounds))marketClear=false;
                foreach(var other in shop.GetComponentsInChildren<Collider>())
                    if(other.enabled && !other.isTrigger && col.bounds.Intersects(other.bounds))
                        if(Physics.ComputePenetration(col,col.transform.position,col.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out _))shopClear=false;
            }
            Check(marketClear && shopClear,"House, porch, boardwalk, rails and landing do not intersect either shop");
            c.SelectNavigation(c.HomeNavigationIndex);
            bool planned=c.WaterRoute.Plan();
            Debug.Log("[V024] ROUTE: "+c.NavigationName+" target="+c.NavigationPosition+" berth="+home.Berth+" start="+c.Player.transform.position+" clearGoal="+c.WaterRoute.ClearPoint(home.Berth)+" failure="+c.WaterRoute.Failure);
            Check(c.NavigationName=="回潮渔屋" && c.NavigationPosition==home.Berth && planned,"Home marker target resolves to reachable dedicated berth");
            Check(c.WaterRoute.ClearPoint(home.Berth),"Home berth preserves hull clearance");
            c.ReturnToHarbor();yield return null;
            var rb=c.Player.engine.RB;var hull=c.Player.GetComponent<MeshCollider>();
            Check(hull!=null && hull.enabled && !hull.isTrigger && hull.convex,"Tests use the real convex skiff hull");
            foreach(var target in new[]{home.House.Find("Home foundation").GetComponent<Collider>(),home.House.Find("Porch foundation").GetComponent<Collider>(),
                home.transform.Find("Boardwalk foundation").GetComponent<Collider>(),home.transform.Find("Home landing foundation").GetComponent<Collider>(),
                market.Find("Dock collision").GetComponent<Collider>(),market.Find("Boat building barrier").GetComponent<Collider>(),shop.transform.Find("Shop foundation").GetComponent<Collider>(),shop.transform.Find("Boat building barrier").GetComponent<Collider>()})
            {
                Check(target.enabled && !target.isTrigger && !Physics.GetIgnoreLayerCollision(hull.gameObject.layer,target.gameObject.layer),"Hull collision enabled / "+target.name);
                var p=new Vector3(target.bounds.center.x,.2f,target.bounds.center.z);
                Check(Physics.ComputePenetration(hull,p,Quaternion.identity,target,target.transform.position,target.transform.rotation,out _,out _),"Actual hull meets solid geometry at waterline / "+target.name);
            }
            Check(Physics.GetIgnoreCollision(c.Shore.Body,market.Find("Boat building barrier").GetComponent<Collider>()) && Physics.GetIgnoreCollision(c.Shore.Body,shop.transform.Find("Boat building barrier").GetComponent<Collider>()),"Shop boat envelopes do not obstruct pedestrians");
            foreach(var target in new[]{home.House.Find("Home foundation"),market.Find("Boat building barrier"),shop.transform.Find("Boat building barrier")})
            {
                bool tackle=target.IsChildOf(shop.transform);
                var collider=target.GetComponent<BoxCollider>();var direction=tackle?-shop.transform.forward:target.right;
                float edge=tackle?collider.size.z*.5f:target.lossyScale.x*collider.size.x*.5f;
                var start=target.TransformPoint(collider.center)+direction*(edge+8);start.y=.2f;
                if(tackle){start=shop.mooringPoint.position+direction*6;start.y=.2f;Check(c.WaterRoute.ClearPoint(start),"Shop collision approach starts in actual clear water");}
                rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(start,Quaternion.LookRotation(-direction));rb.position=start;rb.rotation=c.Player.transform.rotation;Physics.SyncTransforms();
                float closest=edge+8;
                for(int i=0;i<(tackle?300:160);i++)
                {
                    rb.velocity=-direction*6+Vector3.up*rb.velocity.y;closest=Mathf.Min(closest,Vector3.Dot(rb.position-target.TransformPoint(collider.center),direction));yield return null;
                }
                rb.velocity=Vector3.zero;
                Debug.Log("[V024] BOAT: "+target.name+" edge="+edge+" closest="+closest+" position="+rb.position);
                Check(closest>edge-.15f,"Physics stops a moving production boat at "+target.name);
            }
            c.ReturnToHarbor();yield return null;
            c.SelectNavigation(c.HomeNavigationIndex);PlaceBoat(c,home.Berth);
            for(int i=0;i<25;i++)yield return null;
            Check(c.Shore.TryDisembark() && !c.Shore.HasSurveySite,"Dedicated home landing is not a survey site");yield return null;
            foreach(var approach in new[]{home.BoardwalkStart,home.Entry})
            {
                for(int i=0;i<450 && FishingPrototype.FlatDistance(c.Shore.Position,approach)>.6f;i++)
                {
                    c.viewCamera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(approach-c.Shore.Position,Vector3.up));
                    c.Shore.Walk(Vector2.up,false,.05f);yield return null;
                }
                Check(!c.Shore.Swimming && FishingPrototype.FlatDistance(c.Shore.Position,approach)<.75f,"Actual character walks home route / "+approach);
            }
            Check(c.Shore.Relocate(home.WakePoint,Quaternion.identity) && home.CanSleep,"Home bedroom remains reachable");
            c.viewCamera.transform.SetPositionAndRotation(home.Origin+new Vector3(24,28,-32),Quaternion.LookRotation(home.Origin-(home.Origin+new Vector3(24,28,-32))));
            FishingV016Validation.Capture(c,evidence,"home-layout");
            Check(c.Shore.Relocate(market.TransformPoint(new Vector3(-1,.62f,-1.7f)),Quaternion.identity),"Market pedestrian deck keeps real footing and clearance");
            var drops=Drops(c);while(drops.MoveNext())yield return null;
            c.ReturnToHarbor();yield return null;c.SelectNavigation(c.ShopNavigationIndex);PlaceBoat(c,shop.mooringPoint.position);
            for(int i=0;i<25;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Shop landing stays usable with foundation");yield return null;
            Check(c.Shore.Relocate(shop.purchasePoint.position,shop.transform.rotation),"Counter remains clear for customer");yield return null;
            Check(c.Residents.Person(4)==shop.Cashier.transform && c.Residents.TryNearby(out int id) && id==4,"Original beauty model is the nearby NPC");
            int coins=c.Career.Coins;var feet=c.Shore.Position;
            Check(c.RequestWorldInteraction() && c.DialogueOpen && c.Residents.ActiveId==4,"World E route opens cashier dialogue");
            c.Residents.Choose(1);c.Residents.Reveal();c.Shore.Walk(Vector2.up,true,.05f);
            Check(c.Residents.Speech.Contains("钓竿") && c.Residents.Speech.Contains("望远镜") && c.Residents.Speech.Contains("制作材料"),"NPC explains upgrades, tools and supplies");
            Check(c.Career.Coins==coins && c.Shore.Position==feet && !shop.BuySupply(0),"Dialogue blocks movement and purchases without charging");
            c.viewCamera.transform.SetPositionAndRotation(shop.transform.TransformPoint(new Vector3(-1.1f,1.55f,1.2f)),Quaternion.LookRotation(shop.Cashier.FacePosition-shop.transform.TransformPoint(new Vector3(-1.1f,1.55f,1.2f))));
            FishingV016Validation.Capture(c,evidence,"cashier-npc");
            c.Residents.Close();yield return null;
            Check(c.ToggleShop() && shop.Open,"Dialogue can continue into existing shop");yield return null;
            shop.Close();c.ReturnToHarbor();
            Debug.Log("[V024] EVIDENCE: "+evidence);
        }
        private static void PlaceBoat(FishingPrototype c,Vector3 p)
        {
            var rb=c.Player.engine.RB;rb.velocity=rb.angularVelocity=Vector3.zero;
            c.Player.transform.SetPositionAndRotation(p+Vector3.up*.2f,Quaternion.identity);rb.position=c.Player.transform.position;rb.rotation=Quaternion.identity;Physics.SyncTransforms();
        }
        private static IEnumerator Drops(FishingPrototype c)
        {
            var high=GameObject.CreatePrimitive(PrimitiveType.Cube);var low=GameObject.CreatePrimitive(PrimitiveType.Cube);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                low.transform.position=new Vector3(5000,.9f,5003);low.transform.localScale=new Vector3(10,.6f,8);
                high.transform.localScale=new Vector3(10,.6f,2);
                wall.transform.position=new Vector3(5000,4,5001.8f);wall.transform.localScale=new Vector3(10,7,.2f);wall.SetActive(false);
                foreach(float height in new[]{2.3f,3.7f,6.7f})
                foreach(bool jump in new[]{false,true})
                {
                    high.transform.position=new Vector3(5000,height-.3f,4998);Physics.SyncTransforms();
                    Check(c.Shore.Relocate(new Vector3(5000,height,4998),Quaternion.identity),"Dry drop fixture has clear starting feet");yield return null;
                    c.viewCamera.transform.rotation=Quaternion.identity;
                    for(int i=0;i<10;i++){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}
                    for(int i=0;i<45;i++){c.Shore.Move(Vector2.up,false,jump && i>=4 && i<10,false,.05f);yield return null;}
                    for(int i=0;i<30;i++){c.Shore.Move(Vector2.zero,false,false,false,.05f);yield return null;}
                    Check(!c.Shore.Swimming && c.Shore.Grounded && c.Shore.Position.z>5001 && Mathf.Abs(c.Shore.Position.y-1.2f)<.12f,"Gravity lands on real lower land / height="+height+" jump="+jump+" feet="+c.Shore.Position);
                }
                wall.SetActive(true);Physics.SyncTransforms();
                Check(c.Shore.Relocate(new Vector3(5000,6.7f,4998),Quaternion.identity),"Wall drop fixture has clear starting feet");yield return null;
                c.viewCamera.transform.rotation=Quaternion.identity;
                for(int i=0;i<65;i++){c.Shore.Walk(Vector2.up,false,.05f);yield return null;}
                Check(c.Shore.Position.z<5001.6f,"Land-drop support cannot bypass a full-height wall");
                wall.SetActive(false);low.SetActive(false);Physics.SyncTransforms();
                Check(c.Shore.Relocate(new Vector3(5000,6.7f,4998),Quaternion.identity),"Unsupported high ledge fixture starts on land");yield return null;
                c.viewCamera.transform.rotation=Quaternion.identity;
                for(int i=0;i<50;i++){c.Shore.Walk(Vector2.up,false,.05f);yield return null;}
                Check(c.Shore.Position.z<4999.5f && !c.Shore.Swimming,"No lower land means high cliff stays guarded");
            }
            finally{high.SetActive(false);low.SetActive(false);wall.SetActive(false);Object.Destroy(high);Object.Destroy(low);Object.Destroy(wall);}
        }
    }
}
