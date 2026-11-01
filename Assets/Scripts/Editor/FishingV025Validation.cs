using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV025Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V025");
        public static IEnumerator Checks(FishingPrototype c)
        {
            foreach(var checks in new[]{FishingXRComfortV044Validation.CreativeTransactions(c),FishingFishSalesValidation.Checks(c),FishingXRComfortValidation.Checks(c),FishingXRPresentationValidation.Checks(c),FishingXRMockHeadValidation.Checks(c)})
            {
                try{while(checks.MoveNext())yield return checks.Current;}
                finally{(checks as IDisposable)?.Dispose();}
            }
            Debug.Log("[V025] CHECK: Pico presentation and comfort geometry complete; legacy suites use separate isolated profiles and headset wear testing remains separate.");
        }
    }
}
