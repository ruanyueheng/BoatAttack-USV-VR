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
                var pickup=c.FieldWorld.Pickups[0];
                Check(c.Shore.Relocate(pickup.Feet+Vector3.back*.8f,Quaternion.identity),"Nearby-menu check has supported ground by an actual pickup");
                c.viewCamera.transform.SetPositionAndRotation(c.Shore.Position+Vector3.up*1.5f,Quaternion.LookRotation(pickup.Root.position-c.Shore.Position-Vector3.up*1.5f));
                Physics.SyncTransforms();
                Check(c.FieldWorld.TryPickupTarget(out var selected) && selected==pickup,"Nearby-menu check aims at an uncollected production pickup");
                Check(!c.Hands.TryControllerPalm(false,out _,out _),"Nearby-menu boundary check has no tracked right palm");
                var pending=typeof(FishingXRNotebook).GetField("_nearbyInteractionPending",Private);
                foreach(int page in new[]{6,9})
                {
                    typeof(FishingXRNotebook).GetProperty("IsOpen").SetValue(c.XRNotebook,true);
                    typeof(FishingXRNotebook).GetField("_page",Private).SetValue(c.XRNotebook,page);Call(c.XRNotebook,"Refresh");
                    var nearby=(Action[])typeof(FishingXRNotebook).GetField("_actions",Private).GetValue(c.XRNotebook);
                    Check(nearby[page==6?4:5]!=null,"Nearby action exists on the actual notebook page "+page);
                    var plates=(UnityEngine.UI.Image[])typeof(FishingXRNotebook).GetField("_plates",Private).GetValue(c.XRNotebook);
                    typeof(FishingXRNotebook).GetMethod("PressNotebookButton",Private).Invoke(c.XRNotebook,new object[]{plates[page==6?4:5].GetComponent<FishingNotebookButton>()});
                    Check(!c.XRNotebook.IsOpen && !c.WorldActionBusy && !c.Career.Backpack.Collected(pickup.Id),
                        "Nearby action on page "+page+" preserves the XR tracking gate without starting a desktop action");
                    Check((bool)pending.GetValue(c.XRNotebook) && !c.XRNotebook.MotionReady,"Nearby button queues exactly one request without bypassing neutral input on page "+page);
                    bool ready=(bool)typeof(FishingXRNotebook).GetMethod("ReArmMotion",Private).Invoke(c.XRNotebook,new object[]{Vector2.up,Vector2.zero,true});
                    Check(!ready && (bool)pending.GetValue(c.XRNotebook),"Held motion cannot rearm or dispatch the pending nearby request");
                    c.XRNotebook.ResetInput();
                    Check(!(bool)pending.GetValue(c.XRNotebook) && !c.XRNotebook.MotionReady,"Reset cancels the pending interaction as well as motion readiness");
                    nearby[page==6?4:5]();c.XRNotebook.Tick(true);
                    Check(!(bool)pending.GetValue(c.XRNotebook) && !c.WorldActionBusy && !c.Career.Backpack.Collected(pickup.Id),
                        "A pending request without XR tracking is cancelled without consuming the pickup");
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
