using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        private bool RequestFarmInteraction(int id,Transform root)
        {
            var work=Farm.Work(id);
            if(_xrActive || id>=12 || work==FarmWork.Water || Farm.CarryingCan || work==FarmWork.Wait || (work==FarmWork.Plant && Career.Backpack.Count(FieldItem.TurnipSeeds)==0))return Farm.InteractAt(id);
            var contact=FarmContact(root);
            bool started=BeginWorldAction(FishingActionKind.Pickup,"farm:"+id,root,contact,Shore.Actor.rotation,()=>Farm.CommitAt(id,work));
            if(started)Farm.PresentAction(id,work);return started;
        }
        internal void ReportFarmContact(long id,bool succeeded)
        {LastActionId=id;LastActionSucceeded=succeeded;ActionCommitAttempts++;_actionConsumedFrame=Time.frameCount;}
        internal bool CanReachFarmHandle(Vector3 palm,Quaternion palmRotation,out Vector3 lean)
        {
            lean=Vector3.zero;if(!Shore.Grounded)return false;
            var character=Shore.Character;var shoulder=character.ShoulderPosition(false)-character.AppliedInteractionLean+character.AshoreStanceOffset(true);
            var wrist=character.WristForPalm(false,palm,palmRotation);float reach=character.ArmReach(false);
            var far=(wrist-shoulder).sqrMagnitude>(palm-shoulder).sqrMagnitude?wrist:palm;var delta=far-shoulder;
            if(delta.sqrMagnitude>.0001f)lean=delta.normalized*Mathf.Clamp(delta.magnitude-(reach-.04f),0,.22f);
            return Vector3.Distance(shoulder+lean,palm)<=reach-.015f && Vector3.Distance(shoulder+lean,wrist)<=reach-.015f
                && ClearActionSight(shoulder+lean,palm,Farm.WateringCan);
        }
    }
}
