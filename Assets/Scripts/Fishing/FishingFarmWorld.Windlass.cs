using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFarmWorld
    {
        // Desktop well drawing: the can is set on the rim, both hands turn a real windlass crank whose rope coil winds
        // with the bucket height, then both hands tip the dripping bucket into the can and the crank lets it back down.
        private static readonly Vector3 CrankPivotLocal=new Vector3(11.72f,1.65f,-8);
        private const float CrankRadius=.2f,CoilCore=.11f,CoilDepth=.08f,DrumRadius=.17f;
        private Transform _crank,_coil;private Vector3 _rimSeat;private Quaternion _rimSeatRotation;
        private float _crankAngle;

        private static bool CrankVertex(Vector3 p)=>Mathf.Abs(p.z+8)<.09f && p.y>1.28f && p.y<1.70f && ((p.x>11.66f && p.y<1.58f) || p.x>11.75f);
        private static bool CoilVertex(Vector3 p)
        {
            float radial=new Vector2(p.y-1.65f,p.z+8).magnitude;
            return Mathf.Abs(p.x-10.2f)<.09f && radial>.13f && radial<.23f;
        }
        private void BuildWindlass()
        {
            _crank=new GameObject("Well windlass crank").transform;_crank.SetParent(Root,false);_crank.localPosition=CrankPivotLocal;
            FishingArt.Shape("Crank arm",PrimitiveType.Cube,_crank,new Vector3(.02f,-CrankRadius*.5f,0),new Vector3(.035f,CrankRadius+.03f,.035f),_c.wood);
            var handle=FishingArt.Shape("Crank handle",PrimitiveType.Cylinder,_crank,new Vector3(.13f,-CrankRadius,0),new Vector3(.034f,.11f,.034f),_c.wood);handle.transform.localRotation=Quaternion.Euler(0,0,90);
            _coil=FishingArt.Shape("Wound well rope",PrimitiveType.Cylinder,Root,new Vector3(10.2f,1.65f,-8),Vector3.one,_wellRope!=null?_wellRope.sharedMaterial:_c.wood).transform;
            _coil.localRotation=Quaternion.Euler(0,0,90);
            PresentWindlass(0);
        }
        public Vector3 CrankGrip=>_crank!=null?CrankHandle(-1):Vector3.zero;
        public Vector3 StandPosition=>_targets[15].position;
        private Vector3 CrankHandle(float side)=>_crank.TransformPoint(new Vector3(.06f+side*.12f,-CrankRadius,0));
        /// <summary>Bucket height drives the crank angle and the visible rope coil, so winding is never decorative.</summary>
        private void PresentWindlass(float wound)
        {
            if(_crank==null)return;
            _crankAngle=wound*(WellLift/DrumRadius)*Mathf.Rad2Deg;
            _crank.localRotation=Quaternion.AngleAxis(_crankAngle,Vector3.right);
            float radius=CoilCore+CoilDepth*Mathf.Clamp01(wound);_coil.localScale=new Vector3(radius*2,.075f,radius*2);
        }
        private float WellLift=>1.05f-(WellWaterLevel-.80f);
        private float Wound(Vector3 bucket)=>Mathf.Clamp01((bucket.y-(_targets[12].position.y+WellWaterLevel-.80f))/WellLift);
        private Vector3 RopeTop=>_targets[12].position+Vector3.up*1.65f-Root.forward*(CoilCore+CoilDepth*Wound(_bucket.position));

        /// <summary>Where the can rests on the rim while both hands work: the rim point nearest the given stance.</summary>
        private void RimSeat(Vector3 feet,out Vector3 position,out Quaternion rotation)
        {
            var centre=_targets[12].position;var toward=Vector3.ProjectOnPlane(feet-centre,Vector3.up);
            toward=toward.sqrMagnitude>.001f?toward.normalized:-Root.forward;
            position=centre+toward*.90f+Vector3.up*(.88f+.218f);rotation=Quaternion.LookRotation(toward,Vector3.up);
        }
        /// <summary>A desktop stance must reach the crank's top and bottom and the rim seat with the real right arm.</summary>
        private bool WellStanceReaches(Vector3 actor,Quaternion face)
        {
            var feet=actor-Vector3.up*.08f;RimSeat(feet,out var seat,out _);
            if(Mathf.Abs(Vector3.Dot(face*Vector3.forward,Root.right))>.6f)return false;
            var pivot=Root.TransformPoint(CrankPivotLocal)+Root.right*.18f;
            foreach(var point in new[]{pivot})
                if(!_c.CanReachFarmHandle(point,face,actor,face,out _))return false;
            return true;
        }
        private Quaternion WorkFacing=>Quaternion.Euler(0,_c.Shore.Actor.eulerAngles.y,0);

        private void PresentRefillHands(float p)
        {
            var face=WorkFacing;var hands=_c.Hands;
            var left=CrankHandle(-1);var right=CrankHandle(1);
            // Overhand on the crank; on the bail the fingers wrap across the bucket's X-axis handle.
            var crankGrip=Quaternion.LookRotation(Vector3.ProjectOnPlane(face*Vector3.forward,Root.right).normalized,Vector3.up);
            var bailAxis=_bucket.right;var bailGrip=Quaternion.LookRotation(Vector3.ProjectOnPlane(face*Vector3.forward,bailAxis).normalized,_bucket.up);
            var bailL=_bucket.TransformPoint(new Vector3(-.07f,.60f,0));var bailR=_bucket.TransformPoint(new Vector3(.07f,.60f,0));
            float onBail=Ease(.53f,.57f,p)*(1-Ease(.80f,.84f,p));
            // Left: rest → crank (0-.2) → bail (pour) → crank brake while the bucket goes down.
            float leftOn=Ease(.0f,.12f,p);
            var leftPos=Vector3.Lerp(left,bailL,onBail);var leftRot=Quaternion.Slerp(crankGrip,bailGrip,onBail);
            hands.Left.SetPose(Vector3.Lerp(hands.Left.Palm.position,leftPos,leftOn),Quaternion.Slerp(hands.Left.Palm.rotation,leftRot,leftOn),.85f);
            hands.Left.SetFingers(new FishingFingerPose(.78f,.85f,.88f,.92f,.76f));
            hands.Left.SetGripSurface(leftPos-(onBail>.5f?bailAxis:Root.right)*.05f,leftPos+(onBail>.5f?bailAxis:Root.right)*.05f,.017f);
            // Right: carries the can to the rim (0-.18), cranks, takes the bail, then picks the can back up (.84-.95).
            float rightFree=Ease(.18f,.24f,p)*(1-Ease(.86f,.92f,p));
            if(rightFree<=0)return;
            var rightPos=Vector3.Lerp(right,bailR,onBail);var rightRot=Quaternion.Slerp(crankGrip,bailGrip,onBail);
            var canPalm=_can.TransformPoint(CanPalm);var canRot=_can.rotation*CanPalmRotation;
            hands.Right.SetPose(Vector3.Lerp(canPalm,rightPos,rightFree),Quaternion.Slerp(canRot,rightRot,rightFree),.85f);
            hands.Right.SetFingers(new FishingFingerPose(.78f,.85f,.88f,.92f,.76f));
            hands.Right.SetGripSurface(rightPos-(onBail>.5f?bailAxis:Root.right)*.05f,rightPos+(onBail>.5f?bailAxis:Root.right)*.05f,.017f);
        }
        /// <summary>Body weight follows the work: lean into the crank, bob with each turn, settle while pouring.</summary>
        private Vector3 RefillLean(float p)
        {
            var character=_c.Shore.Character;var shoulder=character.ShoulderPosition(false)-character.AppliedInteractionLean;
            var target=Vector3.Lerp(CrankHandle(1),_bucket.TransformPoint(new Vector3(.07f,.60f,0)),Ease(.53f,.57f,p)*(1-Ease(.80f,.84f,p)));
            var delta=target-shoulder;var lean=delta.normalized*Mathf.Clamp(delta.magnitude-(character.ArmReach(false)-.04f),0,.22f);
            float cranking=Ease(.22f,.26f,p)*(1-Ease(.50f,.54f,p));
            lean=Vector3.ClampMagnitude(lean+Vector3.up*Mathf.Sin(_crankAngle*Mathf.Deg2Rad)*.035f*cranking,.24f);
            // The windlass beam sits at eye height: shorten the lean until the head keeps clear of it.
            var eye=character.EyePosition-character.AppliedInteractionLean;
            var a=Root.TransformPoint(new Vector3(10.2f-1.3f,1.65f,-8));var b=Root.TransformPoint(new Vector3(10.2f+1.5f,1.65f,-8));
            for(int i=0;i<6 && BeamDistance(eye+lean,a,b)<BeamClearance;i++)lean*=.6f;
            return lean;
        }
        public const float BeamClearance=.3f;
        public static float BeamDistance(Vector3 p,Vector3 a,Vector3 b)
        {
            var ab=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude);return Vector3.Distance(p,a+ab*t);
        }
    }
}
