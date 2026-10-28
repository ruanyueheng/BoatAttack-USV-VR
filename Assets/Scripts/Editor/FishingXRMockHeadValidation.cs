using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace BoatAttack.EditorTools
{
    /// <summary>
    /// Production XR path under the OpenXR Mock Runtime (XRSettings.isDeviceActive is really true).
    /// A batch editor never runs the OpenXR frame loop, so runtime head poses cannot reach input here; instead the
    /// camera's head-local pose is written the way TrackedPoseDriver writes it, and every frame of game code must leave
    /// it intact in world space. The device-side input step is covered by the driver configuration checks and the
    /// XR SDK fallback, and must be confirmed on the headset (Docs/VR_CONTROLS_V044.md, 实机验收清单).
    /// </summary>
    public static class FishingXRMockHeadValidation
    {
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("[XR-MOCK] CHECK: "+text);}
        private static Type MockType=>Type.GetType("UnityEngine.XR.OpenXR.Features.Mock.MockRuntime, Unity.XR.OpenXR.Features.MockRuntime");
        private static float Yaw(Transform t)=>t.eulerAngles.y;
        /// <summary>Holds a head-local pose for some frames and returns the camera's world yaw/pitch offset from the origin.</summary>
        private static IEnumerator Probe(FishingPrototype c,Quaternion head,int frames,float[] result)
        {
            var cam=c.viewCamera.transform;
            // The seat re-enables the driver when XR starts; the probe owns the head-local pose from here.
            cam.GetComponent<TrackedPoseDriver>().enabled=false;
            for(int i=0;i<frames;i++){cam.localRotation=head;yield return null;}
            result[0]=Mathf.DeltaAngle(Yaw(c.XRSeat.Origin.transform),Yaw(cam));result[1]=Mathf.DeltaAngle(0,cam.eulerAngles.x);
        }
        public static IEnumerator Checks(FishingPrototype c)
        {
            var driver=c.viewCamera.GetComponent<TrackedPoseDriver>();
            Check(driver!=null && driver.ignoreTrackingState,"Head driver ignores runtime trackingState (PICO reported Position-only state froze rotation in v0.43)");
            Check(driver.rotationInput.action.bindings.Any(b=>b.path=="<XRHMD>/deviceRotation") && driver.positionInput.action.bindings.Any(b=>b.path=="<XRHMD>/devicePosition"),"Head driver falls back to device pose bindings when centre-eye controls are absent");
            Check(FishingXRSeat.HeadDisagrees(Quaternion.identity,Quaternion.Euler(0,60,0)) && !FishingXRSeat.HeadDisagrees(Quaternion.Euler(0,60,0),Quaternion.Euler(0,61,0)),"XR SDK head fallback overrides a frozen camera but not normal tracking jitter");
            Check(MockType!=null,"OpenXR Mock Runtime assembly is available");
            var feature=OpenXRSettings.Instance.GetFeatures().First(f=>f.GetType()==MockType);bool wasEnabled=feature.enabled;
            var loader=ScriptableObject.CreateInstance<OpenXRLoader>();bool started=false;
            // OpenXR pumps its event loop from Application.onBeforeRender, which a batch editor never raises.
            var pumpMethod=typeof(OpenXRLoader).GetMethod("ProcessOpenXRMessageLoop",BindingFlags.Instance|BindingFlags.NonPublic);
            UnityEditor.EditorApplication.CallbackFunction pump=()=>{if(loader!=null)pumpMethod.Invoke(loader,null);};
            UnityEditor.EditorApplication.update+=pump;
            var result=new float[2];
            try
            {
                feature.enabled=true;FishingXRSeat.ValidationHoldHead=true;
                started=loader.Initialize() && loader.Start();
                float deadline=Time.realtimeSinceStartup+15;
                while(Time.realtimeSinceStartup<deadline && !XRSettings.isDeviceActive)yield return null;
                Check(started && XRSettings.isDeviceActive,"Mock OpenXR session drives the production XR path");
                for(int i=0;i<10;i++)yield return null;

                c.ReturnToHarbor();yield return null;
                var rb=c.Player.engine.RB;rb.position=c.Home.Berth+Vector3.up*.7f;rb.rotation=Quaternion.identity;rb.velocity=rb.angularVelocity=Vector3.zero;
                c.Player.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
                for(int i=0;i<20;i++)yield return null;
                Check(c.Shore.TryDisembark(),"Disembark at home berth for the shore head check");
                {var probe=Probe(c,Quaternion.Euler(-20,60,0),20,result);while(probe.MoveNext())yield return probe.Current;} // the runner does not flatten nested coroutines
                Debug.Log("[XR-MOCK] SHORE: head yaw 60 / pitch -20 -> world "+result[0].ToString("0.0")+" / "+result[1].ToString("0.0"));
                Check(Mathf.Abs(result[0]-60)<1 && Mathf.Abs(result[1]+20)<1,"Ashore, no game code rewrites the XR camera or origin: 60° head yaw and -20° pitch stay in world space");
                Check(c.Shore.TryBoard(),"Board again for the afloat checks");
                for(int i=0;i<30;i++)yield return null;
                var boat=c.Player.transform;var basePosition=boat.position;float boatYaw=Yaw(boat);
                driver.enabled=false;
                float low=float.MaxValue,high=float.MinValue,tilt=0;
                for(int i=0;i<360;i++)
                {
                    float t=i/72f;
                    boat.SetPositionAndRotation(basePosition+Vector3.up*.6f*Mathf.Sin(t*Mathf.PI*2*.7f),Quaternion.Euler(4*Mathf.Sin(t*5),boatYaw,8*Mathf.Sin(t*4)));
                    c.viewCamera.transform.localRotation=Quaternion.identity;
                    yield return null;
                    float y=c.viewCamera.transform.position.y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);
                    tilt=Mathf.Max(tilt,Vector3.Angle(c.viewCamera.transform.up,Vector3.up));
                }
                Debug.Log("[XR-MOCK] BOAT HEAVE: hull ±0.60 m / roll ±8° / pitch ±4° -> camera height range "+(high-low).ToString("0.000")+" m, max camera tilt "+tilt.ToString("0.0")+"°, comfort="+c.ComfortMode);
                Check(c.ComfortMode && high-low<=.065f,"Afloat (comfort default), ±0.6 m hull heave moves the eye at most ±3 cm (range "+(high-low).ToString("0.000")+" m)");
                Check(tilt<1,"Hull roll and pitch never tilt the head (max "+tilt.ToString("0.0")+"°)");
                {var probe=Probe(c,Quaternion.Euler(0,60,0),20,result);while(probe.MoveNext())yield return probe.Current;} // the runner does not flatten nested coroutines
                Check(Mathf.Abs(result[0]-60)<1,"Afloat, a 60° head yaw stays 60° in world space");
            }
            finally
            {
                UnityEditor.EditorApplication.update-=pump;FishingXRSeat.ValidationHoldHead=false;
                if(driver!=null)driver.enabled=true;
                if(started)loader.Stop();loader.Deinitialize();UnityEngine.Object.Destroy(loader);feature.enabled=wasEnabled;
            }
            for(int i=0;i<10;i++)yield return null;
            Check(!XRSettings.isDeviceActive && c.View.OwnsCamera,"Desktop camera ownership returns after the mock session");
        }
    }
}
