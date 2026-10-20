using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV033Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V033");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V033] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            FishingSlotStoreValidation.Checks();
            Check(FishingHUD.SettingValueAt(100,100,440,0,1)==0 && FishingHUD.SettingValueAt(540,100,440,0,1)==1,"Setting track endpoints reach true zero and one");
            Check(FishingHUD.SettingValueAt(-200,100,440,.25f,2.5f)==.25f,"Setting track clamps mouse dragging outside its edge");
            var original=c.Career;var boat=c.Player.engine.RB.position;var nav=c.NavigationTarget;
            c.OpenStartMenu();yield return null;
            Check(c.StartMenuOpen && c.Paused && c.CanContinue,"Returning to title retains a resumable live session");
            c.OpenSaveMenu("save");c.RequestSlotAction(0,"save");if(c.SaveConfirmation.Length>0)c.ConfirmSaveAction();
            Check(c.SlotInfo(0,FishingSlotKind.Manual).Valid,"Manual slot contains real career and checkpoint");
            string manualPath=FishingSlotStore.PathFor(c.SavePath,0,FishingSlotKind.Manual);
            string manual=File.ReadAllText(manualPath);
            c.Career.Calendar.AdvanceToHour(13);typeof(FishingPrototype).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);
            Check(File.ReadAllText(manualPath)==manual,"Autosave never overwrites the manual snapshot");
            Check(c.SlotInfo(0,FishingSlotKind.Manual).Data.career.calendar.minute!=c.Career.Calendar.Minute,"Manual checkpoint remains independently readable");
            c.CloseSaveMenu();c.ContinueFromMenu();if(c.TitleVoyage.Sailing)c.FinishTitleVoyage();yield return null;
            Check(!c.StartMenuOpen && !c.Paused && ReferenceEquals(original,c.Career),"Continue does not reload or replace career");
            Check(FishingPrototype.FlatDistance(boat,c.Player.transform.position)<1 && c.NavigationTarget==nav,"Continue preserves playable boat and navigation");
            c.OpenStartMenu();c.OpenSaveMenu("load");c.RequestSlotAction(0,"load");
            Check(c.SaveConfirmation.Length>0 && ReferenceEquals(original,c.Career),"Load from a live session requires confirmation before replacement");
            c.CancelSaveConfirmation();Check(ReferenceEquals(original,c.Career),"Cancelling load preserves live career");
            c.RequestSlotAction(0,"load");
            var automated=typeof(FishingPrototype).GetField("<Automated>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
            automated.SetValue(c,false);
            try{typeof(FishingCatchRig).GetMethod("ResetForLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.CatchRig,null);}
            finally{automated.SetValue(c,true);}
            Check(c.HasIsolatedProfile,"Production-safe catch reset does not require a fixture profile; all preference-writing operations remain isolated");
            c.ConfirmSaveAction();yield return null;
            Check(!ReferenceEquals(original,c.Career) && !c.StartMenuOpen && !c.Paused,"Explicit load restores the disk snapshot and exits title");
            Check(c.Career.Calendar.Minute<13*60,"Load differs from Continue by restoring saved clock");
            c.OpenStartMenu();c.OpenSaveMenu("new");c.RequestSlotAction(0,"new");
            Check(c.SaveConfirmation.Contains("覆盖"),"Starting in an occupied slot has an overwrite confirmation");c.CancelSaveConfirmation();c.CloseSaveMenu();c.ContinueFromMenu();if(c.TitleVoyage.Sailing)c.FinishTitleVoyage();
            c.CloseSaveMenu();
            var resumable=typeof(FishingPrototype).GetField("_sessionResumable",BindingFlags.Instance|BindingFlags.NonPublic);
            c.OpenStartMenu();resumable.SetValue(c,false);c.ContinueFromMenu();if(c.TitleVoyage.Sailing)c.FinishTitleVoyage();
            Check(c.StartMenuOpen && c.SaveMenuMessage.Contains("没有"),"No-session Continue gives a helpful prompt without starting a new game");
            resumable.SetValue(c,true);c.ContinueFromMenu();if(c.TitleVoyage.Sailing)c.FinishTitleVoyage();
            c.SelectNavigation(c.HomeNavigationIndex);
            var berth=c.Home.Berth+Vector3.up;var rb=c.Player.engine.RB;rb.position=berth;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;c.Player.transform.SetPositionAndRotation(berth,Quaternion.identity);Physics.SyncTransforms();
            for(int i=0;i<12;i++)yield return null;
            Check(c.Shore.TryDisembark(),"Home landing is reachable for save restoration");
            Check(c.Shore.Relocate(c.Home.WakePoint,Quaternion.identity),"Character can stand at the home's safe checkpoint");
            yield return null;
            c.OpenStartMenu();c.OpenSaveMenu("save");c.RequestSlotAction(1,"save");if(c.SaveConfirmation.Length>0)c.ConfirmSaveAction();
            var shore=c.SlotInfo(1,FishingSlotKind.Manual);Check(shore.Valid && shore.Data.resume.ashore,"Ashore manual snapshot stores supported feet and moored boat");
            c.CloseSaveMenu();c.ContinueFromMenu();if(c.TitleVoyage.Sailing)c.FinishTitleVoyage();c.ReturnToHarbor();c.OpenStartMenu();c.OpenSaveMenu("load");c.RequestSlotAction(1,"load");c.ConfirmSaveAction();yield return null;
            Check(c.Ashore && FishingPrototype.FlatDistance(c.Shore.Position,c.Home.WakePoint)<.2f,"Load restores home scene progress rather than restarting at harbor");
            // Additional settings, real chart rendering and consecutive-frame skeleton assertions.
            var settings=FishingV033SettingsValidation.Checks(c);while(settings.MoveNext())yield return settings.Current;
            var chart=FishingHomeChartTitleValidation.Checks(c);while(chart.MoveNext())yield return chart.Current;
            var character=FishingV033CharacterValidation.Checks(c);while(character.MoveNext())yield return character.Current;
            var motion=FishingCharacterMotionValidation.Checks(c);while(motion.MoveNext())yield return motion.Current;
        }
    }
}
