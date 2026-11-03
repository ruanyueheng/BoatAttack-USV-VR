using System;
using System.Collections;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV063Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V063");
        private static void Check(bool ok,string label)
        {if(!ok)throw new InvalidOperationException(label);Debug.Log("[V063] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.HasIsolatedProfile && c.Automated,"All classroom changes run in an explicit isolated profile");
            FishingXRCollectionValidation.Checks(c);
            var realms=FishingV063RealmValidation.Checks(c);
            try{while(realms.MoveNext())yield return realms.Current;}finally{(realms as IDisposable)?.Dispose();}
            var calendar=FishingV063CalendarValidation.Checks(c);
            try{while(calendar.MoveNext())yield return calendar.Current;}finally{(calendar as IDisposable)?.Dispose();}
            var saved=c.Career.Snapshot();bool creative=c.CreativeMode,travel=c.CreativeRealmTravel;
            try
            {
                c.Realms.Abort();c.RestoreShopTestProfile(FishingClassroomDemo.Create().Snapshot());c.SetCreativeMode(false);
                var before=c.Player.transform.position;
                Check(!c.TeleportToRealmEntrance(0) && Vector3.Distance(before,c.Player.transform.position)<.01f,"Normal mode cannot invoke creative travel");
                c.SetCreativeMode(true);c.SetCreativeRealmTravel(false);
                Check(!c.TeleportToRealmEntrance(0),"Creative travel has a separate opt-in switch");
                c.SetCreativeRealmTravel(true);
                for(int island=0;island<4;island++)
                {
                    int coins=c.Career.Coins;string bag=JsonUtility.ToJson(c.Career.Backpack.Snapshot());
                    Check(c.TeleportToRealmEntrance(island),"Creative travel arrives at island "+island);
                    Check(c.Ashore && !c.Realms.Inside && c.Shore.Clearance(c.Shore.Position),"Travel ends on clear, supported island ground "+island);
                    Check(c.Realms.TryTarget(out int target,out _) && target==island+8,"The real portal is reachable after travel "+island);
                    Check(c.Career.Coins==coins && JsonUtility.ToJson(c.Career.Backpack.Snapshot())==bag,"Travel retains coins and inventory "+island);
                }
                for(int i=0;i<4;i++)c.Career.Lore.Clear(i);
                Check(c.ResetRealmsForDemo(-1),"All realms reset through the saved creative action");
                for(int i=0;i<4;i++)Check(!c.Career.Lore.Cleared(i) && c.Career.Lore.RewardClaimed(i),"Reset retains paid relics and starts trial "+i);
                var loaded=FishingSaveStore.Load(c.SavePath,out string error);
                Check(string.IsNullOrEmpty(error) && !loaded.Lore.Cleared(3) && loaded.Lore.RewardClaimed(3),"Realm reset persists and validates through the real save store");
                Check(c.PrepareAnimalShowcase(),"Creative animal preparation commits and saves");
                for(int i=0;i<5;i++)Check(c.Career.HousedAnimalCount((LandAnimalKind)i)>=2,"Each animal habitat contains a demonstration pair "+i);
                Check(FishingCareer.TryRestore(c.Career.Snapshot(),out _),"Prepared classroom progress remains a legal saved career");
                var notebook=typeof(FishingXRNotebook);const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
                notebook.GetField("_page",fields).SetValue(c.XRNotebook,FishingXRNotebook.RealmDemoPage);
                notebook.GetMethod("Refresh",fields).Invoke(c.XRNotebook,null);
                var canvas=(Canvas)notebook.GetField("_canvas",fields).GetValue(c.XRNotebook);
                var actions=(Action[])notebook.GetField("_actions",fields).GetValue(c.XRNotebook);
                Check(actions[0]!=null && actions[4]!=null,"The realm page builds real portal and toggle button actions; native XR verifies opening separately");
                FishingXRPresentationValidation.CaptureCanvas(canvas,"Logs/V063/realm-demo-page.png");
                yield return null;
            }
            finally{c.XRNotebook.Close();c.Realms.Abort();c.RestoreShopTestProfile(saved);c.SetCreativeMode(creative);c.SetCreativeRealmTravel(travel);}
        }
    }
}
