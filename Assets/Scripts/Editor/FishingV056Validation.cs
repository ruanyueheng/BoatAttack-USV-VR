using System;
using System.Collections;
using BoatAttack.Fishing;

namespace BoatAttack.EditorTools
{
    public static class FishingV056Validation
    {
        public static void Run()=>FishingV011Validation.Begin("V056");
        public static IEnumerator Checks(FishingPrototype c)
        {
            foreach(var checks in new[]{FishingV055Validation.Checks(c),FishingAnglingValidation.Checks(c),FishingMouseFishingValidation.Checks(c)})
            {
                try{while(checks.MoveNext())yield return checks.Current;}
                finally{(checks as IDisposable)?.Dispose();}
            }
        }
    }
}
