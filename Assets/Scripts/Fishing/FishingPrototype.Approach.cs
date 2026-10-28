using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        // Desktop step-in: the stylised rig's arms are short, so when a target is out of reach the body slides to a
        // stance that truly reaches it (around an obstacle if needed) instead of stretching the arm. XR never uses this.
        private const float ApproachStraight=1.2f,ApproachDetour=1.6f;
        private Vector3[] _approachPath;private float[] _approachLengths;
        private float _approachTime,_approachDuration,_approachPitch;
        private Quaternion _approachFrom,_approachFace;private Vector3 _approachLook;
        private Func<bool> _approachThen;private bool _approachRetrying;
        public bool ReachApproachActive=>_approachPath!=null;

        internal bool TryPlanReachApproach(Vector3 centre,Vector3 look,Func<Vector3,Quaternion,bool> reaches,Func<bool> then,float ring=1f)
        {
            if(_approachRetrying || _xrActive || !Ashore || !Shore.Grounded || Shore.Swimming || Shore.Climbing || then==null)return false;
            var from=Shore.Actor.position-Vector3.up*.08f;
            var free=new List<Vector3>();var goals=new List<(Vector3 feet,Quaternion face,float travel)>();
            for(int a=0;a<24;a++)for(float d=.3f;d<=ring;d+=.05f)
            {
                var seed=centre+Quaternion.Euler(0,a*15,0)*Vector3.forward*d;seed.y=from.y;
                if(!StandableAt(seed,from.y,out var feet))continue;
                float travel=FlatDistance(from,feet);if(travel>ApproachDetour)continue;
                free.Add(feet);
                var flat=Vector3.ProjectOnPlane(look-feet,Vector3.up);if(flat.sqrMagnitude<.0001f)continue;
                var face=Quaternion.LookRotation(flat);
                if(reaches(feet+Vector3.up*.08f,face))goals.Add((feet,face,travel));
            }
            goals.Sort((x,y)=>x.travel.CompareTo(y.travel));
            Vector3[] path=null;float length=float.MaxValue;Quaternion goalFace=Quaternion.identity;
            foreach(var goal in goals)
                if(goal.travel<=ApproachStraight && ClearSlide(from,goal.feet)){path=new[]{from,goal.feet};length=goal.travel;goalFace=goal.face;break;}
            if(path==null)
                // Two-leg detour around the blocker through any free stance; nearest goals first keeps the search short.
                for(int g=0;g<goals.Count && g<8;g++)foreach(var via in free)
                {
                    float total=FlatDistance(from,via)+FlatDistance(via,goals[g].feet);
                    if(total>=length || total>ApproachDetour || !ClearSlide(from,via) || !ClearSlide(via,goals[g].feet))continue;
                    path=new[]{from,via,goals[g].feet};length=total;goalFace=goals[g].face;
                }
            if(path==null)return false;
            _approachPath=path;_approachLengths=new float[path.Length];
            for(int i=1;i<path.Length;i++)_approachLengths[i]=_approachLengths[i-1]+Vector3.Distance(path[i-1],path[i]);
            _approachTime=0;_approachDuration=.3f+length/1.5f;_approachPitch=View.LookPitch;
            _approachFrom=Shore.Actor.rotation;_approachFace=goalFace;_approachLook=look;_approachThen=then;
            Shore.ResetMoveInput();return true;
        }
        private bool StandableAt(Vector3 seed,float floorY,out Vector3 feet)
            =>Shore.Ground(seed,out feet) && Mathf.Abs(feet.y-floorY)<=.3f && Shore.Clearance(feet);
        private bool ClearSlide(Vector3 from,Vector3 to)
        {
            int steps=Mathf.Max(2,Mathf.CeilToInt(FlatDistance(from,to)/.1f));
            for(int i=1;i<steps;i++)if(!StandableAt(Vector3.Lerp(from,to,(float)i/steps),from.y,out _))return false;
            return true;
        }
        private Vector3 ApproachPoint(float p)
        {
            float s=p*_approachLengths[_approachLengths.Length-1];
            for(int i=1;i<_approachPath.Length;i++)
                if(s<=_approachLengths[i] || i==_approachPath.Length-1)
                    return Vector3.Lerp(_approachPath[i-1],_approachPath[i],Mathf.InverseLerp(_approachLengths[i-1],_approachLengths[i],s));
            return _approachPath[_approachPath.Length-1];
        }
        /// <summary>Advances a planned step-in; true while it owns this frame.</summary>
        private bool TickReachApproach(float deltaTime,bool focused,bool xrActive)
        {
            if(_approachPath==null)return false;
            if(!focused || xrActive || Paused || InventoryOpen || JournalOpen || ShopOpen || StoryBlocksGameplay || !Ashore){CancelReachApproach();return true;}
            _approachTime+=deltaTime;float p=Mathf.SmoothStep(0,1,Mathf.Clamp01(_approachTime/_approachDuration));
            var face=Quaternion.Slerp(_approachFrom,_approachFace,Mathf.SmoothStep(0,1,Mathf.Clamp01(_approachTime/(_approachDuration*.7f))));
            if(!Shore.Relocate(ApproachPoint(p),face)){CancelReachApproach();Notice("前方被挡住了，请换个方向靠近。",3);return true;}
            // Keep the player's own pitch and ease it toward the target instead of levelling the view.
            var gaze=(_approachLook-Shore.Character.EyePosition).normalized;
            View.FaceShore(Mathf.Lerp(_approachPitch,-Mathf.Asin(Mathf.Clamp(gaze.y,-1,1))*Mathf.Rad2Deg,p));Shore.ResetMoveInput();
            if(p<1)return true;
            var then=_approachThen;CancelReachApproach();
            _approachRetrying=true;try{then();}finally{_approachRetrying=false;}
            return true;
        }
        private void CancelReachApproach(){_approachPath=null;_approachLengths=null;_approachThen=null;}
    }
}
