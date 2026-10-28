using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        private bool RequestFarmInteraction(int id,Transform root)
        {
            var work=Farm.Work(id);
            if(_xrActive || id>=12 || work==FarmWork.Water || Farm.CarryingCan || work==FarmWork.Wait || (work==FarmWork.Plant && Career.Backpack.Count(FieldItem.TurnipSeeds)==0))return Farm.InteractAt(id);
            var contact=FarmActionContact(id,root);
            bool started=BeginWorldAction(FishingActionKind.Pickup,"farm:"+id,root,contact,Shore.Actor.rotation,()=>Farm.CommitAt(id,work),retry:()=>RequestFarmInteraction(id,root));
            if(started && _action!=null)Farm.PresentAction(id,work);return started;
        }
        private Vector3 FarmActionContact(int id,Transform root)
        {
            var contact=FarmContact(root);
            if(Farm.Work(id)==FarmWork.Till && Career.Backpack.Equipped==FieldItem.Rake)
            {
                // The rake head meets the soil; the generic pickup height is a palm target.
                contact.y=root.TransformPoint(Vector3.up*.12f).y;
                contact-=Shore.Actor.rotation*Quaternion.Euler(145,0,-12)*(FishingFieldWorld.WorkToolTip(FieldItem.Rake)-new Vector3(0,.1f,0));
            }
            return contact;
        }
        internal void ReportFarmContact(long id,bool succeeded)
        {LastActionId=id;LastActionSucceeded=succeeded;ActionCommitAttempts++;_actionConsumedFrame=Time.frameCount;}
        internal bool CanReachFarmHandle(Vector3 palm,Quaternion palmRotation,out Vector3 lean)=>CanReachFarmHandle(palm,palmRotation,Shore.Actor.position,Shore.Actor.rotation,out lean);
        // actor/facing may be a candidate stance: the solved shoulder is carried rigidly with the body to test it before moving.
        internal bool CanReachFarmHandle(Vector3 palm,Quaternion palmRotation,Vector3 actor,Quaternion facing,out Vector3 lean)
        {
            lean=Vector3.zero;if(!Shore.Grounded)return false;
            var character=Shore.Character;var shoulder=character.ShoulderPosition(false)-character.AppliedInteractionLean+character.AshoreStanceOffset(Shore.Crouched);
            shoulder=actor+facing*(Quaternion.Inverse(Shore.Actor.rotation)*(shoulder-Shore.Actor.position));
            var wrist=character.WristForPalm(false,palm,palmRotation);float reach=character.ArmReach(false);
            var far=(wrist-shoulder).sqrMagnitude>(palm-shoulder).sqrMagnitude?wrist:palm;var delta=far-shoulder;
            if(delta.sqrMagnitude>.0001f)lean=delta.normalized*Mathf.Clamp(delta.magnitude-(reach-.04f),0,.22f);
            return Vector3.Distance(shoulder+lean,palm)<=reach-.015f && Vector3.Distance(shoulder+lean,wrist)<=reach-.015f
                && ClearActionSight(shoulder+lean,palm,Farm.WateringCan);
        }
    }
}
