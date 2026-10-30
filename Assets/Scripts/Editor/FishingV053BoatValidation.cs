using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BoatAttack.Fishing;
using BoatAttack.VR;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV053BoatValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V053-BOAT");
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("[V053-BOAT] CHECK: "+message);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.Automated && c.HasIsolatedProfile,"Boat checks use an isolated profile");
            if(c.StartMenuOpen)c.FinishTitleVoyage();if(c.Paused)c.TogglePause();
            if(c.Ashore)c.Shore.BoardImmediately();c.ReturnToHarbor();
            var router=USVInputRouter.Instance;router.enabled=false;
            var input=typeof(USVInputRouter).GetProperty("Current",BindingFlags.Public|BindingFlags.Instance);
            var rb=c.Player.engine.RB;var rows=new List<string>{"phase,time,hullY,verticalSpeed,eyeAboveHull,speed,boost"};
            var start=c.spawnPosition;start.y=.7f;
            // Keep the full production buoyancy and propulsion; only the isolated input is supplied here.
            for(int phase=0;phase<3;phase++)
            {
                rb.velocity=rb.angularVelocity=Vector3.zero;rb.position=start;rb.rotation=Quaternion.identity;
                c.Player.transform.SetPositionAndRotation(start,Quaternion.identity);Physics.SyncTransforms();
                input.SetValue(router,default(USVInputState));
                float settle=Time.time+5;while(Time.time<settle)yield return null;
                float baseline=rb.position.y;
                if(phase==2)rb.AddForce(Vector3.up*7,ForceMode.VelocityChange);
                input.SetValue(router,new USVInputState{throttle=phase<2?1:0,boost=phase==1});
                float until=Time.time+6,minEye=float.MaxValue,maxRise=0,maxVertical=0,speed=0;bool boosted=false;
                while(Time.time<until)
                {
                    float eye=FishingXRSeat.ComfortableBoatHeight(c.XRSeat.WaterlineHeight,rb.position.y)+.25f+FishingXRSeat.EyeHeight-rb.position.y;
                    minEye=Mathf.Min(minEye,eye);maxRise=Mathf.Max(maxRise,rb.position.y-baseline);maxVertical=Mathf.Max(maxVertical,Mathf.Abs(rb.velocity.y));
                    speed=Mathf.Max(speed,c.Speed);boosted|=c.Boost.Active;
                    rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F3},{2:F4},{3:F4},{4:F4},{5:F4},{6}",phase,Time.time,rb.position.y,rb.velocity.y,eye,c.Speed,c.Boost.Active));
                    yield return null;
                }
                Debug.Log("[V053-BOAT] PHASE="+phase+" maxSpeed="+speed+" rise="+maxRise+" maxVertical="+maxVertical+" minimumEyeAboveHull="+minEye+" boost="+boosted);
                if(phase==0)Check(speed>1,"Cruise moves the real hull");
                if(phase==1)Check(speed>1 && boosted,"Boost engages the real propulsion");
                if(phase==2 && !FishingPrototype.HasArgument("-boat-baseline"))Check(minEye>.9f && maxRise<.9f,"Contact impulse settles without lifting the hull through the stable seated eye");
            }
            input.SetValue(router,default(USVInputState));router.enabled=true;
            Directory.CreateDirectory("Logs/V053");File.WriteAllLines("Logs/V053/boat-"+(FishingPrototype.HasArgument("-boat-baseline")?"baseline":"fixed")+".csv",rows);
        }
    }
}
