using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingShore
    {
        public bool FreeLanding { get; private set; }
        public Vector3 LandingPoint { get; private set; }
        // Named jetties remain convenient walking routes. All other coastline is sampled from physics,
        // not a finite list of authored markers. No ground is created and no cliff is made walkable.
        public bool FindLanding(out Vector3 feet,out bool namedSite)
            => FindLanding(out feet,out namedSite,out _,out _);
        private bool FindLanding(out Vector3 feet,out bool namedSite,out int surveySite,out bool shopLanding)
        {
            feet=Vector3.zero;namedSite=false;surveySite=-1;shopLanding=false;
            var home=Controller.Home;
            if(home!=null && FishingPrototype.FlatDistance(Controller.Player.transform.position,home.Berth)<=9)
            {
                var landing=home.BoardwalkStart;
                if(Ground(landing,out feet) && Mathf.Abs(feet.y-landing.y)<.35f && Clearance(feet) && SafeCoastalFooting(feet) && ClearTransfer(feet))
                {namedSite=true;return true;}
            }
            var shop=Controller.TackleShop;
            if(shop!=null && shop.Available && FishingPrototype.FlatDistance(Controller.Player.transform.position,shop.mooringPoint.position)<=9f)
            {
                var landing=shop.LandingPosition;
                if(Ground(landing,out feet) && Mathf.Abs(feet.y-landing.y)<.5f && Clearance(feet)
                    && SafeCoastalFooting(feet) && ClearTransfer(feet))
                {namedSite=true;shopLanding=true;return true;}
            }
            if(NearestSite>=0 && NearestSite<Sites.Length && BerthDistance<=9f)
            {
                var site=Sites[NearestSite];
                if(Ground(site.Landing,out feet) && Mathf.Abs(feet.y-site.Landing.y)<1 && Clearance(feet)
                    && SafeCoastalFooting(feet) && ClearTransfer(feet))
                {namedSite=true;surveySite=NearestSite;return true;}
            }
            var boat=Controller.Player.transform.position;
            for(int i=0;i<FishingAdventure.Count;i++)
            {
                if(FishingPrototype.FlatDistance(boat,FishingIslandWorld.Berth(i))>9f)continue;
                var landing=FishingIslandWorld.Landing(i);
                if(Ground(landing,out feet) && Mathf.Abs(feet.y-landing.y)<.5f && Clearance(feet)
                    && SafeCoastalFooting(feet) && ClearTransfer(feet)){namedSite=true;return true;}
            }
            float best=float.MaxValue;bool found=false;
            for(float radius=2;radius<=10;radius+=.75f)
                for(int i=0;i<48;i++)
                {
                    float a=i*Mathf.PI/24;
                    var seed=boat+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                    if(!GroundBelow(seed,boat.y+2.8f,4,out var p) || !SafeCoastalFooting(p) || !Clearance(p))continue;
                    if(!ClearTransfer(p))continue;
                    float score=FishingPrototype.FlatDistance(boat,p)+Mathf.Abs(p.y-boat.y)*.7f;
                    if(score>=best)continue;best=score;feet=p;found=true;
                }
            return found;
        }
        public bool SafeCoastalFooting(Vector3 p)
        {
            if(p.y<.35f)return false;
            // Four supporting samples reject narrow ledges, posts and a capsule hanging over water.
            foreach(var offset in new[]{new Vector3(.24f,0,0),new Vector3(-.24f,0,0),new Vector3(0,0,.24f),new Vector3(0,0,-.24f)})
                if(!GroundBelow(p+offset,p.y+.25f,.65f,out var floor) || Mathf.Abs(floor.y-p.y)>.28f)return false;
            return true;
        }
        private bool ClearTransfer(Vector3 feet)
        {
            var boat=Controller.Player.transform.position;
            var to=feet+Vector3.up*1.1f;
            var from=new Vector3(boat.x,Mathf.Max(boat.y+1.4f,to.y),boat.z);
            var d=to-from;
            foreach(var hit in Physics.SphereCastAll(from,.22f,d.normalized,d.magnitude,~(1<<4),QueryTriggerInteraction.Ignore))
                if(!Ignored(hit.collider))return false;
            return true;
        }
        public float BoatEdgeDistance()
        {
            if(Controller==null || Controller.Player==null || Body==null)return float.MaxValue;
            var p=Actor.position;float best=float.MaxValue;
            foreach(var col in Controller.Player.GetComponentsInChildren<Collider>())
            {
                if(!col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy)continue;
                // Use a point at hull mid-height, then compare horizontally: dock height and bobbing
                // must not turn a nearby bow/stern into a distant boat-centre check.
                var query=new Vector3(p.x,col.bounds.center.y,p.z);
                var closest=col is MeshCollider mesh && !mesh.convex?col.ClosestPointOnBounds(query):col.ClosestPoint(query);
                best=Mathf.Min(best,FishingPrototype.FlatDistance(p,closest));
            }
            return best==float.MaxValue?FishingPrototype.FlatDistance(p,Controller.Player.transform.position):best;
        }
        public bool CanBoard()
        {
            if(!Ashore || Controller.ShopBlocksGameplay)return false;
            var boat=Controller.Player.transform.position;
            if(BoatEdgeDistance()<=4f && Mathf.Abs(Actor.position.y-(boat.y+.6f))<=3f && ClearTransfer(Actor.position))return true;
            // Existing long access ramps still offer a return at their landward end, provided the
            // boat has not left this mooring. Free-coast landings never use this convenience route.
            return !FreeLanding && FishingPrototype.FlatDistance(Actor.position,LandingPoint)<=4f
                && Mathf.Abs(Actor.position.y-LandingPoint.y)<1.5f
                && FishingPrototype.FlatDistance(boat,_mooring)<2f
                && ClearTransfer(Actor.position);
        }
    }
}
