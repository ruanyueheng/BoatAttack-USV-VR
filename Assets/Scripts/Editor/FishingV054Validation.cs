using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV054Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V054");
        public static IEnumerator Checks(FishingPrototype c)
        {
            foreach(var checks in new[]{FishingWorldTextValidation.Checks(c),FishingV054HomeGardenValidation.Checks(c),FishingSeaMarketValidation.Checks(c),FishingHomeHabitatValidation.Checks(c)})
            {
                try{while(checks.MoveNext())yield return checks.Current;}
                finally{(checks as IDisposable)?.Dispose();}
            }
            Debug.Log("[V054] CHECK: home art, physical garden fence, seasonal market transactions and animal habitats passed; native XR controls are checked separately.");
        }
    }
}
