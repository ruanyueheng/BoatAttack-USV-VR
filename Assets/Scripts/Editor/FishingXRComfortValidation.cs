using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace BoatAttack.EditorTools
{
    public static class FishingXRComfortValidation
    {
        private static void Check(bool ok,string text)
        {if(!ok)throw new InvalidOperationException(text);Debug.Log("[XR-COMFORT] CHECK: "+text);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            Check(c.XRSeat!=null && c.XRSeat.Origin!=null,"Production XR seat exists");
            var driver=c.viewCamera.GetComponent<TrackedPoseDriver>();
            Check(driver!=null && driver.trackingType==TrackedPoseDriver.TrackingType.RotationAndPosition
                && driver.updateType==TrackedPoseDriver.UpdateType.UpdateAndBeforeRender,"Head position and rotation remain owned by tracked pose driver, including before render");
            Check(driver.positionInput.action.bindings[0].path=="<XRHMD>/centerEyePosition"
                && driver.rotationInput.action.bindings[0].path=="<XRHMD>/centerEyeRotation","HMD bindings retain physical movement without artificial head poses");
            if(!XRSettings.isDeviceActive)
            {
                Check(!driver.enabled && !c.XRSeat.Origin.enabled,"Desktop camera remains independent from XR origin");
                bool calibrated=(bool)typeof(FishingXRSeat).GetMethod("CalibrateEye",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(c.XRSeat,null);
                Check(!calibrated && !c.XRSeat.Ready,"Absent headset cannot establish a fake seated eye height");
            }
            float height=2,velocity=0;
            for(int i=0;i<1800;i++)
            {
                float boat=2+5*Mathf.Sin(i*.11f);
                float target=FishingXRSeat.ComfortableBoatHeight(2,boat);
                height=Mathf.SmoothDamp(height,target,ref velocity,2.5f,Mathf.Infinity,1f/90);
                CheckBound(target,height);
            }
            Check(true,"Even five-metre vessel heave adds at most three centimetres of artificial eye displacement");
            Check(Mathf.Abs(FishingXRSeat.ComfortableBoatHeight(2,2.1f)-2.008f)<.0001f,"Small vessel motion is reduced to eight percent");
            float waterline=c.XRSeat.WaterlineHeight;
            Check(Mathf.Abs(FishingXRSeat.ComfortableBoatHeight(waterline,waterline+1)-waterline)<=.0301f
                && Mathf.Abs(FishingXRSeat.ComfortableBoatHeight(waterline,waterline)-waterline)<.0001f,"Raised spawn and rescue position cannot become a permanent one-metre camera offset");
            Debug.Log("[XR-COMFORT] WATERLINE: rest="+waterline+" hull="+c.Player.transform.position.y+" spawn="+c.spawnPosition.y);
            Check(Vector3.Distance(FishingHands.XRRestRodPosition,new Vector3(.25f,FishingXRSeat.EyeHeight-.5f,.4f))<.24f,"Resting rod is within the grip radius of a seated right hand, fifty centimetres below the eyes");
            var serialized=new SerializedObject(c.Hands);
            foreach(bool left in new[]{true,false})
            {
                var rotation=Quaternion.Euler(serialized.FindProperty(left?"_leftControllerPalmEuler":"_rightControllerPalmEuler").vector3Value);
                Check(Vector3.Dot(rotation*Vector3.forward,Vector3.forward)>.999f,"Controller grip keeps "+(left?"left":"right")+" knuckles forward instead of pointing upward");
                Check(Vector3.Dot(rotation*Vector3.up,left?Vector3.left:Vector3.right)>.999f,"Controller palms mirror their dorsal axes");
            }
            var input=new FishingHandInput();
            input.Update(true,1,true,true,1,true,0);
            Check(!input.Holding && !input.Activated,"Held controls at reconnect cannot acquire or activate rod");
            input.Update(true,0,false,true,0,true,0);input.Update(true,1,false,true,1,true,0);
            Check(input.Holding && !input.Reeling,"Release then grip acquires rod without stationary reeling");
            input.Update(true,1,true,true,1,true,15);
            Check(input.Activated && input.Reeling,"Fresh trigger activates and a real crank angle change reels");
            input.Update(true,1,true,true,1,true,15);
            Check(!input.Activated && !input.Reeling,"Held trigger and stationary crank do not repeat actions");
            input.Update(false,1,true,true,1,true,30);input.Update(true,1,true,true,1,true,45);
            Check(!input.Holding && !input.Activated && !input.Reeling,"Tracking loss releases every tool latch and reconnect still requires release");
            input.Update(true,0,false,true,0,true,0);input.Update(true,1,false,true,1,true,0);
            input.Update(true,float.NaN,true,true,1,true,15);
            Check(!input.Holding && !input.Activated && !input.Reeling,"Invalid grip data cannot retain a tool");
            var root=new GameObject("XR independent hand validation");
            try
            {
                var hand=root.AddComponent<FishingHandTarget>();hand.Build(true);
                hand.SetPose(Vector3.one,Quaternion.identity,.6f);Check(hand.Visible,"Independent tracked goal accepts a valid frame pose");
                hand.SetPose(new Vector3(float.NaN,0,0),Quaternion.identity,.6f);Check(!hand.Visible,"Invalid tracked position hides the hand instead of displaying an origin pose");
                hand.SetPose(Vector3.one,Quaternion.identity,.6f);hand.BeginFrame();Check(!hand.Visible,"Stale hand goals expire at the next presentation frame");
            }
            finally{UnityEngine.Object.Destroy(root);}
            FishingXRComfortV044Validation.Run(c);
            yield return null;
        }
        private static void CheckBound(float target,float height)
        {if(Mathf.Abs(target-2)>.0301f || Mathf.Abs(height-2)>.0301f)throw new InvalidOperationException("Comfortable XR vessel frame exceeded three centimetres of artificial heave");}
    }
}
