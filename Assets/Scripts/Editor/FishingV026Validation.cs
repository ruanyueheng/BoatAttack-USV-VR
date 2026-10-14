using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV026Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V026");
        public static IEnumerator Checks(FishingPrototype c)
        {
            if(FishingPrototype.Version!="0.26.0")throw new InvalidOperationException("Wrong release under validation.");
            FishingV026ArtValidation.CheckAssets(c);
            foreach(var checks in new[]{FishingV026HomeValidation.Checks(c),FishingV026SwimmingValidation.Checks(c)})
            {
                try{while(checks.MoveNext())yield return checks.Current;}
                finally{(checks as IDisposable)?.Dispose();}
            }
            Debug.Log("[V026] CHECK: Home interactions, station migration and swimming geometry complete.");
        }
    }
}
