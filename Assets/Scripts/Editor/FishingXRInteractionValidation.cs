using System;
using System.Collections;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingXRInteractionValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[XR-INTERACTION] CHECK: "+label);}
        private static void Call(FishingXRNotebook notebook,string method)=>typeof(FishingXRNotebook).GetMethod(method,Private).Invoke(notebook,null);
        private static void Stand(FishingPrototype c,Transform target,int id)
        {
            bool placed=false;
            for(int i=0;i<8 && !placed;i++)
            {
                var seed=target.position+Quaternion.Euler(0,i*45,0)*Vector3.forward*.85f;
                if(c.Shore.Ground(seed,out var feet))placed=c.Shore.Relocate(feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(target.position-feet,Vector3.up)));
            }
            Check(placed,"An actual supported stance exists by island target "+id);
            c.viewCamera.transform.SetPositionAndRotation(c.Shore.Position+Vector3.up*1.5f,Quaternion.LookRotation(target.position+Vector3.up*.5f-c.Shore.Position-Vector3.up*1.5f));
            Physics.SyncTransforms();
            Check(c.Islands.TryTarget(out int near,out _) && near==id,"Island target is reachable before tracked A: "+id);
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Interaction checks use an isolated profile");
            var original=c.Career.Snapshot();int activePreference=PlayerPrefs.GetInt("Fishing.ActiveSaveSlot",-1);
            try
            {
                c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                for(int island=0;island<4;island++)
                {
                    c.ReturnToHarbor();yield return null;yield return null;
                    typeof(FishingPrototype).GetMethod("Teleport",Private).Invoke(c,new object[]{FishingIslandWorld.Berth(island),Quaternion.identity});
                    for(int f=0;f<25;f++)yield return null;
                    Check(c.Shore.TryDisembark(),"Island landing remains available "+island);yield return null;
                    var root=c.Islands.WorldRoot.GetChild(island);
                    Stand(c,root.Find("Voyage letter"),island*3);Call(c.XRNotebook,"InteractTracked");
                    Check(c.Career.Adventure.Visited(island) && c.Letter.Open,"Tracked A reads the actual island letter "+island);
                    c.Letter.Close();yield return null;yield return null;
                    Stand(c,root.Find("Hidden treasure"),island*3+1);int before=c.Career.Coins;Call(c.XRNotebook,"InteractTracked");
                    Check(c.Career.Adventure.TreasureFound(island) && c.Career.Coins==before+40,"Tracked A opens the actual chest exactly once "+island);
                    before=c.Career.Coins;Check(!c.Islands.InteractAt(island*3+1) && c.Career.Coins==before,"Opened chest cannot pay twice "+island);
                    Stand(c,root.Find("Compass beacon"),island*3+2);
                    int wanted=island==0?3:island==1?0:island==2?1:2;
                    for(int turn=0;turn<wanted;turn++)Call(c.XRNotebook,"InteractTracked");
                    Check(c.Career.Adventure.Direction(island)==wanted,"Tracked A turns the actual beacon "+island);
                    Call(c.XRNotebook,"UseTrackedTool");
                    Check(c.Career.Adventure.Restored(island),"Right trigger confirms the actual beacon direction "+island);
                }
                c.ReturnToHarbor();yield return null;yield return null;
                Check(c.Islands.CompleteAtHarbor()==150,"VR island route can complete at the shared harbor");
                var notebook=c.XRNotebook;var type=typeof(FishingXRNotebook);
                Call(notebook,"OpenSaveSlots");type.GetField("_page",Private).SetValue(notebook,30);Call(notebook,"Refresh");
                var actions=(Action[])type.GetField("_actions",Private).GetValue(notebook);
                Check(c.Paused && c.SaveMenuOpen && actions[3]!=null && actions[4]!=null && actions[5]!=null,"VR exposes all three slots through the existing save transaction");
                actions[3]();Check(c.SaveConfirmation.Length>0,"An occupied slot requires explicit confirmation");
                int coins=c.Career.Coins;Call(notebook,"Refresh");actions[1]();
                Check(c.Career.Coins==coins && c.SaveConfirmation.Length==0,"Cancel preserves the current voyage");
                actions[1]();actions[4]();
                if(c.SaveConfirmation.Length>0){Call(notebook,"Refresh");actions[0]();}
                Check(c.ActiveSaveSlot==1 && c.SlotInfo(1,FishingSlotKind.Manual).Valid,"VR manual save uses the actual second disk slot");
                Check(PlayerPrefs.GetInt("Fishing.ActiveSaveSlot",-1)==activePreference,"Isolated regression saves do not change the player's preferred active slot");
                notebook.Close();Check(!c.Paused && !c.SaveMenuOpen,"Closing the VR save page restores the prior running state");
            }
            finally
            {
                c.XRNotebook.Close();if(c.Paused)c.TogglePause();c.Letter.Close();c.ReturnToHarbor();c.RestoreShopTestProfile(original);
            }
        }
    }
}
