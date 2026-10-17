using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingShore
    {
        public bool TrySaveFooting(out Vector3 feet)
        {
            feet=Actor.position;
            if(!Ashore || Swimming || Climbing || !Ground(Actor.position,out var ground)
                || Mathf.Abs(ground.y-Actor.position.y)>.35f || !Clearance(ground))return false;
            feet=ground;return true;
        }
        public bool RestoreSavedFooting(Vector3 feet,float yaw)
        {
            if(!Ground(feet,out var supported) || Mathf.Abs(supported.y-feet.y)>.35f || !Clearance(supported))return false;
            Body.enabled=false;ResetTraversal();_motion.Reset();SetCrouch(false,true);
            _mooring=Controller.Player.engine.RB.position;_mooringRotation=Controller.Player.engine.RB.rotation;
            Ashore=true;FreeLanding=true;ShopLanding=false;CurrentSite=-1;LandingPoint=supported;
            UpdateProximity();
            if(FindLanding(out var landing,out var named,out var surveySite,out var shopLanding) && named)
            {FreeLanding=false;CurrentSite=surveySite;ShopLanding=shopLanding;LandingPoint=landing;}
            _lastSafe=supported+Vector3.up*.08f;Actor.SetPositionAndRotation(_lastSafe,Quaternion.Euler(0,yaw,0));
            Body.enabled=true;WalkSpeed=0;Controller.View.ResetLook();SyncRider();UpdateProximity();return true;
        }
    }
}
