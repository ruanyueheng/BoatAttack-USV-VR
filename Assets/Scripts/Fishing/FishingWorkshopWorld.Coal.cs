using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingWorkshopWorld
    {
        // Existing coal IDs and cooldown slots are retained when relocating them.
        // Ground and actor clearance are sampled beside a real rock/terrain wall;
        // no solid voxel is removed from the mountain collision geometry.
        private bool FindCoalFoot(Vector3 landing,int index,out Vector3 feet)
        {
            feet=landing;
            for(int ring=4;ring<=28;ring+=2)for(int step=0;step<36;step++)
            {
                float angle=step*10+index*47;
                var probe=landing+Quaternion.Euler(0,angle,0)*Vector3.forward*ring;probe.y=0;
                if(!_c.Shore.Ground(probe,out var floor) || Mathf.Abs(floor.y-landing.y)>3
                    || !_c.Shore.Clearance(floor) || !_c.Shore.SafeCoastalFooting(floor) || BlocksAccess(floor))continue;
                bool apart=true;
                foreach(var n in _nodes)if(Vector3.Distance(floor,n.Feet)<1.1f){apart=false;break;}
                foreach(var station in _stations.Values)if(Vector3.Distance(floor,station.position)<3.5f){apart=false;break;}
                if(!apart || !MountainWallNearby(floor))continue;
                feet=floor;return true;
            }
            return false;
        }
        public bool MountainWallNearby(Vector3 feet)
        {
            for(int direction=0;direction<360;direction+=30)
            foreach(var hit in Physics.RaycastAll(feet+Vector3.up*.7f,Quaternion.Euler(0,direction,0)*Vector3.forward,1.8f,~(1<<4),QueryTriggerInteraction.Ignore))
            {
                var col=hit.collider;
                if(!(col is MeshCollider || col is TerrainCollider) || col.bounds.size.y<4 || Mathf.Abs(hit.normal.y)>.65f
                    || FishingShore.NonSolidSurface(col) || col.transform.IsChildOf(_c.Player.transform)
                    || col.transform.IsChildOf(_c.Shore.Actor))continue;
                if(_c.TackleShop!=null && col.transform.IsChildOf(_c.TackleShop.transform))continue;
                if(_c.Home!=null && _c.Home.House!=null && col.transform.IsChildOf(_c.Home.House.transform))continue;
                return true;
            }
            return false;
        }
    }
}
