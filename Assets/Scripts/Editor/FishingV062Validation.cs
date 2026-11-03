using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV062Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V062");
        private static void Check(bool ok,string message)
        {if(!ok)throw new InvalidOperationException(message);Debug.Log("[V062] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Classroom fixes use only an isolated career");
            FishingV062OpticsValidation.Checks(c);
            foreach(var checks in new[]{FishingV062SawValidation.Checks(c),FishingV062CreativeValidation.Checks(c)})
            {
                try{while(checks.MoveNext())yield return checks.Current;}
                finally{(checks as IDisposable)?.Dispose();}
            }
            var saved=c.Career.Snapshot();bool creative=c.CreativeMode;
            try
            {
                c.SetCreativeMode(false);c.RestoreShopTestProfile(new FishingCareer().Snapshot());
                Check(c.MainStoryNavigationTarget==0,"A fresh voyage points to Lin Zhou at the harbour");
                c.Career.AcceptMayorship();Check(c.MainStoryNavigationTarget==c.HomeNavigationIndex,"The appointment points home to the old chart");
                c.Career.DiscoverChart();
                for(int island=0;island<4;island++)
                {
                    Check(c.MainStoryNavigationTarget==c.IslandNavigationIndex(island),"The next uncleared realm is tracked: "+island);
                    c.Career.Lore.Clear(island);
                }
                for(int island=0;island<4;island++)
                {
                    Check(c.MainStoryNavigationTarget==c.IslandNavigationIndex(island),"Unclaimed relics remain a real objective: "+island);
                    Check(c.Career.ClaimShrineReward(island),"Relic reward is collected through production rules: "+island);
                }
                Check(c.MainStoryNavigationTarget==c.HomeNavigationIndex,"The four relics point to the home workbench");
                Check(c.Career.CraftVoyageCompass(CraftStation.Workbench),"The quest crafts its real compass");
                Check(c.MainStoryNavigationTarget==c.HomeNavigationIndex+1,"The carried compass points to White Sail Cove");
                Check(c.Career.OpenGrandTreasure()>0 && c.MainStoryNavigationTarget==c.HomeNavigationIndex,"The completed story returns to home life");
                Check(FishingCareer.TryRestore(c.Career.Snapshot(),out _),"Story tracking does not invalidate the saved quest inventory");
            }
            finally{c.XRNotebook.Close();c.RestoreShopTestProfile(saved);c.SetCreativeMode(creative);}
            yield return null;
        }
    }
}
