using System;
using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        internal void TeleportForPerformance(Vector3 position,Quaternion rotation)
        {
            if(!Automated || !HasIsolatedProfile || !HasArgument(FishingPerformanceVoyage.EnableFlag))
                throw new InvalidOperationException("Performance relocation requires the explicit isolated benchmark.");
            Teleport(position,rotation);
        }
    }
}
