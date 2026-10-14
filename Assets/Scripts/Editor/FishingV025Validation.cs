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
            if(FishingPrototype.Version!="0.25.0")throw new InvalidOperationException("Wrong release under validation.");
            foreach(var checks in new[]{FishingXRComfortValidation.Checks(c),FishingXRPresentationValidation.Checks(c)})
            {
                try{while(checks.MoveNext())yield return checks.Current;}
                finally{(checks as IDisposable)?.Dispose();}
            }
            Debug.Log("[V025] CHECK: Pico presentation and comfort geometry complete; legacy suites use separate isolated profiles and headset wear testing remains separate.");
        }
    }
}
